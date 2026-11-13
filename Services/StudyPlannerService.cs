using System.Globalization;
using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services;

public record PlannerRowDto(
    int AssignmentId,
    int TeacherId,
    string TeacherName,
    int ClassroomId,
    string ClassroomName,
    int GradeId,
    string GradeName,
    int SubjectId,
    string SubjectName,
    bool Delivered,
    DocumentDto? Document,
    bool RequestPending);

public record PlannersDto(short? YearId, DateTime? DueAt, bool Overdue, bool CanUploadAny, List<PlannerRowDto> Rows);

public class StudyPlannerService(
    AppDbContext db,
    DocumentService documents,
    GradingDeadlineService deadlines,
    NotificationService notifications)
{
    public const string JobPrefix = "PlannerOverdue:";

    private const string SourceTable = "AcademicAssignment";

    private static readonly TimeSpan ColombiaOffset = TimeSpan.FromHours(-5);

    private sealed record AssignmentInfo(
        int AssignmentId,
        short YearId,
        int TeacherId,
        string TeacherFirstName,
        string TeacherLastName,
        int ClassroomId,
        string ClassroomName,
        int CampusId,
        int GradeId,
        string GradeName,
        int SubjectId,
        string SubjectName)
    {
        public string TeacherName => $"{TeacherFirstName} {TeacherLastName}";
    }

    public Task<PlannersDto> ListOwnAsync(GradingAccess access, DateTime nowUtc) =>
        BuildAsync(access, access.UserId, nowUtc);

    public Task<PlannersDto> PanelAsync(GradingAccess access, DateTime nowUtc) =>
        BuildAsync(access, null, nowUtc);

    public async Task<DocumentDto> UploadAsync(GradingAccess access, int assignmentId, DocumentUpload file, DateTime nowUtc)
    {
        var assignment = await EnsureCanChangeAsync(access, assignmentId, nowUtc);

        return await documents.UploadAsync(DocumentTypes.StudyPlanner, assignmentId, assignment.YearId, file, access.UserId, nowUtc);
    }

    public async Task DeleteAsync(GradingAccess access, int assignmentId, DateTime nowUtc)
    {
        await EnsureCanChangeAsync(access, assignmentId, nowUtc);

        if (!await documents.RemoveCurrentAsync(DocumentTypes.StudyPlanner, assignmentId, access.UserId, nowUtc))
            throw new InvalidOperationException("La asignación no tiene plan de estudio.");
    }

    private async Task<AssignmentInfo> EnsureCanChangeAsync(GradingAccess access, int assignmentId, DateTime nowUtc)
    {
        var assignment = await FindAsync(assignmentId);

        if (access.StudyPlanner.HasFlag(PermissionFlags.Update))
            return assignment;

        if (assignment.TeacherId != access.UserId || !access.StudyPlanner.HasFlag(PermissionFlags.Create))
            throw new UnauthorizedAccessException();

        var dueAt = await deadlines.PlannerDueAsync(assignment.YearId);

        if (IsOverdue(dueAt, nowUtc))
            throw new InvalidOperationException($"El plazo para cargar el plan de estudio venció el {Format(dueAt!.Value)}.");

        return assignment;
    }

    public async Task<(Document Document, Stream Content)> OpenAsync(GradingAccess access, bool panelRead, int assignmentId)
    {
        var assignment = await FindAsync(assignmentId);

        if (assignment.TeacherId != access.UserId && !access.StudyPlanner.HasFlag(PermissionFlags.Read) && !panelRead)
            throw new UnauthorizedAccessException();

        return await documents.OpenCurrentAsync(DocumentTypes.StudyPlanner, assignmentId)
            ?? throw new InvalidOperationException("La asignación no tiene plan de estudio.");
    }

    public async Task RequestAsync(GradingAccess access, int assignmentId, DateTime nowUtc)
    {
        var assignment = await FindAsync(assignmentId);

        if (assignment.TeacherId != access.UserId)
            throw new UnauthorizedAccessException();

        if (!IsOverdue(await deadlines.PlannerDueAsync(assignment.YearId), nowUtc))
            throw new InvalidOperationException("La solicitud solo está disponible cuando el plazo venció.");

        if ((await documents.CurrentByEntityAsync(DocumentTypes.StudyPlanner, [assignmentId])).ContainsKey(assignmentId))
            throw new InvalidOperationException("La asignación ya tiene plan de estudio.");

        if ((await PendingRequestsAsync([assignmentId])).Contains(assignmentId))
            throw new InvalidOperationException("La solicitud ya fue enviada.");

        await notifications.QueueAsync(
            NotificationCodes.PlannerRequest,
            new Dictionary<string, string>
            {
                ["docente"] = assignment.TeacherName,
                ["materia"] = assignment.SubjectName,
                ["curso"] = assignment.ClassroomName,
            },
            new NotificationTarget(
                YearId: assignment.YearId,
                CampusId: assignment.CampusId,
                SourceTable: SourceTable,
                SourceId: assignmentId));

        await db.SaveChangesAsync();
    }

    public async Task<int> NotifyOverdueAsync(DateTime nowUtc)
    {
        var yearId = (await deadlines.CurrentYearAsync(nowUtc))?.YearId;

        if (yearId is null)
            return 0;

        var dueAt = await deadlines.PlannerDueAsync(yearId.Value);

        if (!IsOverdue(dueAt, nowUtc))
            return 0;

        var stamp = dueAt!.Value.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture);
        var jobName = $"{JobPrefix}{yearId.Value}:{stamp}";

        if (await db.JobRuns.AnyAsync(j => j.JobName == jobName))
            return 0;

        var activeTeachers = await db.Teachers
            .Where(t => t.Active && t.User.Active)
            .Select(t => t.TeacherId)
            .ToListAsync();

        var assignments = (await AssignmentsAsync(yearId.Value, null))
            .Where(a => activeTeachers.Contains(a.TeacherId))
            .ToList();

        var delivered = await documents.CurrentByEntityAsync(DocumentTypes.StudyPlanner, assignments.Select(a => a.AssignmentId));
        var published = 0;

        foreach (var group in assignments.Where(a => !delivered.ContainsKey(a.AssignmentId)).GroupBy(a => a.TeacherId))
        {
            var first = group.First();
            var notification = await notifications.QueueAsync(
                NotificationCodes.PlannerOverdue,
                new Dictionary<string, string>
                {
                    ["docente"] = first.TeacherName,
                    ["asignaciones"] = string.Join(", ", group
                        .OrderBy(a => a.SubjectName)
                        .ThenBy(a => a.ClassroomName)
                        .Select(a => $"{a.SubjectName} · {a.ClassroomName}")),
                    ["fecha"] = Format(dueAt!.Value),
                },
                new NotificationTarget(
                    YearId: yearId.Value,
                    CampusId: first.CampusId,
                    DedupKey: $"{NotificationCodes.PlannerOverdue}:{yearId.Value}:{stamp}:{group.Key}"));

            if (notification is not null)
                published++;
        }

        db.JobRuns.Add(new JobRun { JobName = jobName, LastRunDate = DateOnly.FromDateTime(nowUtc.Add(ColombiaOffset)) });
        await db.SaveChangesAsync();

        return published;
    }

    private async Task<PlannersDto> BuildAsync(GradingAccess access, int? teacherId, DateTime nowUtc)
    {
        var yearId = (await deadlines.CurrentYearAsync(nowUtc))?.YearId;

        var canUploadAny = access.StudyPlanner.HasFlag(PermissionFlags.Update);

        if (yearId is null)
            return new PlannersDto(null, null, false, canUploadAny, []);

        var dueAt = await deadlines.PlannerDueAsync(yearId.Value);
        var assignments = await AssignmentsAsync(yearId.Value, teacherId);
        var ids = assignments.Select(a => a.AssignmentId).ToList();
        var delivered = await documents.CurrentByEntityAsync(DocumentTypes.StudyPlanner, ids);
        var pending = await PendingRequestsAsync(ids);

        var rows = assignments
            .Select(a => new PlannerRowDto(
                a.AssignmentId,
                a.TeacherId,
                a.TeacherName,
                a.ClassroomId,
                a.ClassroomName,
                a.GradeId,
                a.GradeName,
                a.SubjectId,
                a.SubjectName,
                delivered.ContainsKey(a.AssignmentId),
                delivered.GetValueOrDefault(a.AssignmentId),
                pending.Contains(a.AssignmentId)))
            .OrderBy(r => r.TeacherName)
            .ThenBy(r => r.ClassroomName)
            .ThenBy(r => r.SubjectName)
            .ToList();

        return new PlannersDto(yearId, dueAt, IsOverdue(dueAt, nowUtc), canUploadAny, rows);
    }

    private Task<List<AssignmentInfo>> AssignmentsAsync(short yearId, int? teacherId) =>
        db.AcademicAssignments
            .AsNoTracking()
            .Where(a => a.Active && a.YearId == yearId && a.Classroom.Active)
            .Where(a => teacherId == null || a.TeacherId == teacherId)
            .Select(a => new AssignmentInfo(
                a.AssignmentId,
                a.YearId,
                a.TeacherId,
                a.Teacher.User.FirstName,
                a.Teacher.User.LastName,
                a.ClassroomId,
                a.Classroom.Name,
                a.Classroom.CampusId,
                a.Classroom.GradeId,
                a.Classroom.Grade.Name,
                a.SubjectId,
                a.Subject.Name))
            .ToListAsync();

    private async Task<AssignmentInfo> FindAsync(int assignmentId) =>
        await db.AcademicAssignments
            .AsNoTracking()
            .Where(a => a.AssignmentId == assignmentId)
            .Select(a => new AssignmentInfo(
                a.AssignmentId,
                a.YearId,
                a.TeacherId,
                a.Teacher.User.FirstName,
                a.Teacher.User.LastName,
                a.ClassroomId,
                a.Classroom.Name,
                a.Classroom.CampusId,
                a.Classroom.GradeId,
                a.Classroom.Grade.Name,
                a.SubjectId,
                a.Subject.Name))
            .FirstOrDefaultAsync()
        ?? throw new InvalidOperationException("La asignación académica no existe.");

    private async Task<HashSet<int>> PendingRequestsAsync(List<int> assignmentIds) =>
        (await db.Notifications
            .AsNoTracking()
            .Where(n =>
                n.NotificationType.Code == NotificationCodes.PlannerRequest &&
                n.Status == NotificationStatuses.Pending &&
                n.SourceTable == SourceTable &&
                n.SourceId != null &&
                assignmentIds.Contains(n.SourceId.Value))
            .Select(n => n.SourceId!.Value)
            .ToListAsync()).ToHashSet();

    private static bool IsOverdue(DateTime? dueAt, DateTime nowUtc) => dueAt is { } due && nowUtc >= due;

    private static string Format(DateTime utc) =>
        utc.Add(ColombiaOffset).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
}

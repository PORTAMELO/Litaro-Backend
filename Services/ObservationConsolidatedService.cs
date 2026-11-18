using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services;

public record RankingFilter(short? YearId = null, int? CampusId = null, int? GradeId = null, int? ClassroomId = null);

public record ObservationCountsDto(int TypeI, int TypeII, int TypeIII, int Academic, int Total);

public record CommitmentSummaryDto(int Total, int Pending, int Met, int NotMet, int Observation);

public record StudentRankDto(
    int StudentId,
    string StudentName,
    string ClassroomName,
    string CampusName,
    ObservationCountsDto Counts,
    int PendingCommitments,
    bool ReachedThreshold);

public record RankingDto(short? YearId, int Threshold, List<StudentRankDto> Students);

public record ConsolidatedObservationDto(
    int ObservationId,
    DateOnly Date,
    string Description,
    bool IsAcademic,
    bool IsCoexistence,
    string? CoexistenceType,
    string? SubjectName,
    string RecordedBy,
    string? Statement,
    List<CommitmentDto> Commitments);

public record ConsolidatedStudentDto(
    int StudentId,
    string StudentName,
    string DocumentNumber,
    string ClassroomName,
    string CampusName,
    short? YearId,
    int Threshold,
    ObservationCountsDto Counts,
    CommitmentSummaryDto Commitments,
    List<ConsolidatedObservationDto> Observations);

public class ObservationConsolidatedService(AppDbContext db, ObservationSettingsService settings, NotificationService notifications)
{
    private const int RankingLimit = 200;

    public async Task<RankingDto> RankingAsync(RankingFilter filter)
    {
        var yearId = filter.YearId ?? await ObservationScope.CurrentYearIdAsync(db);
        var threshold = (await settings.GetAsync()).TypeIThreshold;

        var rows = await db.Observations
            .AsNoTracking()
            .Where(o => o.Enrollment.YearId == yearId)
            .Where(o => filter.CampusId == null || o.Enrollment.Classroom.CampusId == filter.CampusId)
            .Where(o => filter.GradeId == null || o.Enrollment.Classroom.GradeId == filter.GradeId)
            .Where(o => filter.ClassroomId == null || o.Enrollment.ClassroomId == filter.ClassroomId)
            .Select(o => new
            {
                o.Enrollment.StudentId,
                StudentName = o.Enrollment.Student.User.LastName + ", " + o.Enrollment.Student.User.FirstName,
                ClassroomName = o.Enrollment.Classroom.Name,
                CampusName = o.Enrollment.Classroom.Campus.Name,
                o.EnrollmentId,
                o.IsAcademic,
                o.IsCoexistence,
                o.CoexistenceType,
                Pending = o.Commitments.Count(c => c.Status == CommitmentStatuses.Pending),
            })
            .ToListAsync();

        var students = rows
            .GroupBy(r => r.StudentId)
            .Select(g =>
            {
                var latest = g.OrderByDescending(r => r.EnrollmentId).First();
                var counts = Count(g.Select(r => (r.IsAcademic, r.IsCoexistence, r.CoexistenceType)));

                return new StudentRankDto(
                    g.Key,
                    latest.StudentName,
                    latest.ClassroomName,
                    latest.CampusName,
                    counts,
                    g.Sum(r => r.Pending),
                    counts.TypeI >= threshold);
            })
            .OrderByDescending(s => s.Counts.TypeI)
            .ThenByDescending(s => s.Counts.TypeII + s.Counts.TypeIII)
            .ThenByDescending(s => s.Counts.Total)
            .ThenBy(s => s.StudentName)
            .Take(RankingLimit)
            .ToList();

        return new RankingDto(yearId, threshold, students);
    }

    public async Task<ConsolidatedStudentDto> StudentAsync(int studentId, short? yearId)
    {
        var student = await db.Students
            .AsNoTracking()
            .Where(s => s.StudentId == studentId)
            .Select(s => new
            {
                Name = s.User.LastName + ", " + s.User.FirstName,
                s.User.DocumentNumber,
            })
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("No existe el estudiante.");

        var year = yearId ?? await db.Enrollments
            .Where(e => e.StudentId == studentId)
            .OrderByDescending(e => e.YearId)
            .Select(e => (short?)e.YearId)
            .FirstOrDefaultAsync();

        var enrollment = await db.Enrollments
            .AsNoTracking()
            .Where(e => e.StudentId == studentId && e.YearId == year)
            .OrderByDescending(e => e.EnrollmentId)
            .Select(e => new { ClassroomName = e.Classroom.Name, CampusName = e.Classroom.Campus.Name })
            .FirstOrDefaultAsync();

        var observations = await db.Observations
            .AsNoTracking()
            .Where(o => o.Enrollment.StudentId == studentId && o.Enrollment.YearId == year)
            .OrderBy(o => o.Date)
            .ThenBy(o => o.ObservationId)
            .Select(o => new ConsolidatedObservationDto(
                o.ObservationId,
                o.Date,
                o.Description,
                o.IsAcademic,
                o.IsCoexistence,
                o.CoexistenceType,
                o.Subject != null ? o.Subject.Name : null,
                o.RecordedBy.FirstName + " " + o.RecordedBy.LastName,
                o.Statement,
                o.Commitments
                    .OrderBy(c => c.CreatedAt)
                    .Select(c => new CommitmentDto(
                        c.ObservationCommitmentId,
                        c.Text,
                        c.Status,
                        c.FollowUpDate,
                        c.Note,
                        c.CreatedBy.FirstName + " " + c.CreatedBy.LastName,
                        c.CreatedAt))
                    .ToList()))
            .ToListAsync();

        var commitments = observations.SelectMany(o => o.Commitments).ToList();

        return new ConsolidatedStudentDto(
            studentId,
            student.Name,
            student.DocumentNumber,
            enrollment?.ClassroomName ?? string.Empty,
            enrollment?.CampusName ?? string.Empty,
            year,
            (await settings.GetAsync()).TypeIThreshold,
            Count(observations.Select(o => (o.IsAcademic, o.IsCoexistence, o.CoexistenceType))),
            new CommitmentSummaryDto(
                commitments.Count,
                commitments.Count(c => c.Status == CommitmentStatuses.Pending),
                commitments.Count(c => c.Status == CommitmentStatuses.Met),
                commitments.Count(c => c.Status == CommitmentStatuses.NotMet),
                commitments.Count(c => c.Status == CommitmentStatuses.Observation)),
            observations);
    }

    public async Task CheckTypeIThresholdAsync(int studentId, short yearId)
    {
        var count = await db.Observations.CountAsync(o =>
            o.Enrollment.StudentId == studentId &&
            o.Enrollment.YearId == yearId &&
            o.IsCoexistence &&
            o.CoexistenceType == CoexistenceTypes.TypeI);

        var threshold = (await settings.GetAsync()).TypeIThreshold;
        if (count < threshold)
            return;

        var student = await db.Enrollments
            .Where(e => e.StudentId == studentId && e.YearId == yearId)
            .OrderByDescending(e => e.EnrollmentId)
            .Select(e => new
            {
                Name = e.Student.User.FirstName + " " + e.Student.User.LastName,
                Classroom = e.Classroom.Name,
                e.Classroom.CampusId,
            })
            .FirstAsync();

        var queued = await notifications.QueueAsync(
            NotificationCodes.ObservationTypeIThreshold,
            new Dictionary<string, string>
            {
                ["estudiante"] = student.Name,
                ["curso"] = student.Classroom,
                ["cantidad"] = count.ToString(),
                ["anio"] = yearId.ToString(),
            },
            new NotificationTarget(
                StudentId: studentId,
                YearId: yearId,
                CampusId: student.CampusId,
                SourceTable: "Student",
                SourceId: studentId,
                DedupKey: $"OBS_TYPE1:{studentId}:{yearId}"));

        if (queued is not null)
            await db.SaveChangesAsync();
    }

    private static ObservationCountsDto Count(IEnumerable<(bool IsAcademic, bool IsCoexistence, string? Type)> rows)
    {
        var list = rows.ToList();

        return new ObservationCountsDto(
            list.Count(r => r.IsCoexistence && r.Type == CoexistenceTypes.TypeI),
            list.Count(r => r.IsCoexistence && r.Type == CoexistenceTypes.TypeII),
            list.Count(r => r.IsCoexistence && r.Type == CoexistenceTypes.TypeIII),
            list.Count(r => r.IsAcademic),
            list.Count);
    }
}

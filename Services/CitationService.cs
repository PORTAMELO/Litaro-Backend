using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services;

public record CreateCitationRequest(int StudentId, int? ParentId, string Reason);

public record ScheduleCitationRequest(DateOnly Date, TimeOnly? Time, int? ScheduleId, int? ParentId = null);

public record CitationFlagsRequest(bool StudentInformed, bool ParentInformed, bool Attended);

public record CitationStatusRequest(string Status, string? Note);

public record CitationFilter(
    int? StudentId = null,
    int? ParentId = null,
    int? ResponsibleUserId = null,
    DateOnly? From = null,
    DateOnly? To = null,
    string? Status = null);

public record CitationDto(
    int CitationId,
    int StudentId,
    string StudentName,
    string ClassroomName,
    int? ParentId,
    string? ParentName,
    int? ObservationId,
    string Origin,
    string Reason,
    int? ResponsibleUserId,
    string ResponsibleName,
    DateOnly? ScheduledDate,
    TimeOnly? ScheduledTime,
    int? ScheduleId,
    string Status,
    bool StudentInformed,
    bool ParentInformed,
    bool Attended,
    int? RescheduledFromId,
    string? Note,
    string CreatedBy,
    DateTime CreatedAt,
    int ParentConsecutiveMisses,
    bool CanManage);

public record CitationParentDto(int ParentId, string Name, string Relationship, bool PrimaryContact);

public record AttentionSlotDto(int ScheduleId, byte Weekday, TimeOnly StartTime, TimeOnly EndTime, string CampusName);

public class CitationService(AppDbContext db, NotificationService notifications)
{
    public const string CoordinationName = "Coordinación de Convivencia";

    private const int ReasonLength = 500;
    private const int NoteLength = 300;
    private const int ListLimit = 500;

    private static readonly string[] DayNames = ["domingo", "lunes", "martes", "miércoles", "jueves", "viernes", "sábado"];

    public async Task CreateAutomaticAsync(Observation observation, Enrollment enrollment, int userId)
    {
        var toCoordination = observation.CoexistenceType is CoexistenceTypes.TypeII or CoexistenceTypes.TypeIII;

        db.Citations.Add(new Citation
        {
            StudentId = enrollment.StudentId,
            ParentId = await PrimaryParentIdAsync(enrollment.StudentId),
            Observation = observation,
            Origin = CitationOrigins.Automatic,
            Reason = $"Observación {ObservationService.KindLabel(observation.IsAcademic, observation.IsCoexistence, observation.CoexistenceType)} del {observation.Date:dd/MM/yyyy}.",
            ResponsibleUserId = toCoordination ? null : userId,
            CreatedById = userId,
        });

        if (!toCoordination)
            return;

        var student = await StudentInfoAsync(enrollment.StudentId);

        await notifications.QueueAsync(
            NotificationCodes.CitationCoordination,
            new Dictionary<string, string>
            {
                ["estudiante"] = student.Name,
                ["curso"] = student.Classroom,
                ["tipo"] = observation.CoexistenceType!,
                ["fecha"] = observation.Date.ToString("dd/MM/yyyy"),
            },
            new NotificationTarget(StudentId: enrollment.StudentId, YearId: enrollment.YearId, SourceTable: "Observation"));
    }

    public async Task<int> CreateManualAsync(ObservationAccess access, CreateCitationRequest request)
    {
        var inScope = await ObservationScope.Enrollments(db, access)
            .AnyAsync(e => e.StudentId == request.StudentId && e.AcademicYear.Active);

        if (!inScope)
            throw new UnauthorizedAccessException();

        var reason = request.Reason?.Trim() ?? string.Empty;
        if (reason.Length == 0)
            throw new InvalidOperationException("Escriba el motivo de la citación.");

        if (reason.Length > ReasonLength)
            throw new InvalidOperationException($"El motivo no puede superar {ReasonLength} caracteres.");

        var parentId = await ResolveParentAsync(request.StudentId, request.ParentId);

        var citation = new Citation
        {
            StudentId = request.StudentId,
            ParentId = parentId,
            Origin = CitationOrigins.Manual,
            Reason = reason,
            ResponsibleUserId = access.UserId,
            CreatedById = access.UserId,
        };

        db.Citations.Add(citation);
        await db.SaveChangesAsync();

        return citation.CitationId;
    }

    public async Task<List<CitationParentDto>> ParentsAsync(ObservationAccess access, int studentId)
    {
        if (!await ObservationScope.Enrollments(db, access).AnyAsync(e => e.StudentId == studentId))
            throw new UnauthorizedAccessException();

        return await db.ParentStudents
            .AsNoTracking()
            .Where(ps => ps.StudentId == studentId && ps.Parent.Active)
            .OrderByDescending(ps => ps.PrimaryContact)
            .ThenBy(ps => ps.Parent.User.LastName)
            .Select(ps => new CitationParentDto(
                ps.ParentId,
                ps.Parent.User.FirstName + " " + ps.Parent.User.LastName,
                ps.Parent.Relationship,
                ps.PrimaryContact))
            .ToListAsync();
    }

    public async Task<List<AttentionSlotDto>> SlotsAsync(int userId)
    {
        var yearId = await ObservationScope.CurrentYearIdAsync(db);

        return await db.Schedules
            .AsNoTracking()
            .Where(s => s.TeacherId == userId && s.Type == ScheduleTypes.ParentAttention && s.YearId == yearId)
            .OrderBy(s => s.Weekday)
            .ThenBy(s => s.StartTime)
            .Select(s => new AttentionSlotDto(s.ScheduleId, s.Weekday, s.StartTime, s.EndTime, s.Campus.Name))
            .ToListAsync();
    }

    public async Task ScheduleAsync(ObservationAccess access, int citationId, ScheduleCitationRequest request, DateOnly today)
    {
        var citation = await FindManageableAsync(access, citationId);

        if (citation.Status != CitationStatuses.ToSchedule)
            throw new InvalidOperationException("Solo se agendan citaciones pendientes. Para cambiar la fecha, reprográmela.");

        citation.ResponsibleUserId ??= access.UserId;
        citation.ParentId = await ResolveParentAsync(citation.StudentId, request.ParentId) ?? citation.ParentId;

        await ApplyScheduleAsync(citation, request, today);
        await db.SaveChangesAsync();
        await NotifyParentAsync(citation);
    }

    public async Task<int> RescheduleAsync(ObservationAccess access, int citationId, ScheduleCitationRequest request, string? note, DateOnly today)
    {
        var original = await FindManageableAsync(access, citationId);

        if (original.Status != CitationStatuses.Scheduled)
            throw new InvalidOperationException("Solo se reprograman citaciones agendadas.");

        original.Status = CitationStatuses.Rescheduled;
        original.Note = CleanNote(note) ?? original.Note;

        var next = new Citation
        {
            StudentId = original.StudentId,
            ParentId = original.ParentId,
            ObservationId = original.ObservationId,
            Origin = original.Origin,
            Reason = original.Reason,
            ResponsibleUserId = original.ResponsibleUserId,
            RescheduledFrom = original,
            CreatedById = access.UserId,
        };

        await ApplyScheduleAsync(next, request, today);
        db.Citations.Add(next);
        await db.SaveChangesAsync();
        await NotifyParentAsync(next);

        return next.CitationId;
    }

    public async Task SetFlagsAsync(ObservationAccess access, int citationId, CitationFlagsRequest request)
    {
        var citation = await FindManageableAsync(access, citationId);

        if (citation.Status == CitationStatuses.Cancelled)
            throw new InvalidOperationException("La citación está cancelada.");

        if (request.Attended && citation.ScheduledDate is null)
            throw new InvalidOperationException("Agende la citación antes de marcar la asistencia.");

        citation.StudentInformed = request.StudentInformed;
        citation.ParentInformed = request.ParentInformed;
        citation.Attended = request.Attended;
        await db.SaveChangesAsync();
    }

    public async Task SetStatusAsync(ObservationAccess access, int citationId, CitationStatusRequest request)
    {
        var citation = await FindManageableAsync(access, citationId);
        var status = request.Status?.Trim().ToUpperInvariant();

        if (status is not (CitationStatuses.Closed or CitationStatuses.Cancelled))
            throw new InvalidOperationException("Solo se puede cerrar o cancelar una citación.");

        if (citation.Status is not (CitationStatuses.ToSchedule or CitationStatuses.Scheduled))
            throw new InvalidOperationException("La citación ya no está abierta.");

        if (status == CitationStatuses.Closed && citation.ScheduledDate is null)
            throw new InvalidOperationException("Agende la citación antes de cerrarla.");

        citation.Status = status;
        citation.Note = CleanNote(request.Note) ?? citation.Note;
        await db.SaveChangesAsync();
    }

    public async Task<List<CitationDto>> ListAsync(ObservationAccess access, CitationFilter filter, DateOnly today)
    {
        var userId = access.UserId;
        var status = filter.Status?.Trim().ToUpperInvariant();

        var rows = await db.Citations
            .AsNoTracking()
            .Where(c => access.SeesAll || c.ResponsibleUserId == userId || c.CreatedById == userId)
            .Where(c => filter.StudentId == null || c.StudentId == filter.StudentId)
            .Where(c => filter.ParentId == null || c.ParentId == filter.ParentId)
            .Where(c => filter.ResponsibleUserId == null || c.ResponsibleUserId == filter.ResponsibleUserId)
            .Where(c => filter.From == null || c.ScheduledDate >= filter.From)
            .Where(c => filter.To == null || c.ScheduledDate <= filter.To)
            .Where(c => string.IsNullOrEmpty(status) || c.Status == status)
            .OrderBy(c => c.ScheduledDate == null ? 0 : 1)
            .ThenByDescending(c => c.ScheduledDate)
            .ThenByDescending(c => c.ScheduledTime)
            .ThenByDescending(c => c.CitationId)
            .Take(ListLimit)
            .Select(c => new
            {
                c.CitationId,
                c.StudentId,
                StudentName = c.Student.User.LastName + ", " + c.Student.User.FirstName,
                ClassroomName = db.Enrollments
                    .Where(e => e.StudentId == c.StudentId)
                    .OrderByDescending(e => e.YearId)
                    .Select(e => e.Classroom.Name)
                    .FirstOrDefault(),
                c.ParentId,
                ParentName = c.Parent != null ? c.Parent.User.FirstName + " " + c.Parent.User.LastName : null,
                c.ObservationId,
                c.Origin,
                c.Reason,
                c.ResponsibleUserId,
                ResponsibleName = c.ResponsibleUser != null ? c.ResponsibleUser.FirstName + " " + c.ResponsibleUser.LastName : null,
                c.ScheduledDate,
                c.ScheduledTime,
                c.ScheduleId,
                c.Status,
                c.StudentInformed,
                c.ParentInformed,
                c.Attended,
                c.RescheduledFromId,
                c.Note,
                CreatedBy = c.CreatedBy.FirstName + " " + c.CreatedBy.LastName,
                c.CreatedById,
                c.CreatedAt,
            })
            .ToListAsync();

        var misses = await ConsecutiveMissesAsync(rows.Where(r => r.ParentId != null).Select(r => r.ParentId!.Value), today);

        return rows
            .Select(r => new CitationDto(
                r.CitationId,
                r.StudentId,
                r.StudentName,
                r.ClassroomName ?? string.Empty,
                r.ParentId,
                r.ParentName,
                r.ObservationId,
                r.Origin,
                r.Reason,
                r.ResponsibleUserId,
                r.ResponsibleName ?? CoordinationName,
                r.ScheduledDate,
                r.ScheduledTime,
                r.ScheduleId,
                r.Status,
                r.StudentInformed,
                r.ParentInformed,
                r.Attended,
                r.RescheduledFromId,
                r.Note,
                r.CreatedBy,
                r.CreatedAt,
                r.ParentId is { } parentId ? misses.GetValueOrDefault(parentId) : 0,
                CanManage(access, r.ResponsibleUserId, r.CreatedById)))
            .ToList();
    }

    public async Task<Dictionary<int, int>> ConsecutiveMissesAsync(IEnumerable<int> parentIds, DateOnly today)
    {
        var ids = parentIds.Distinct().ToList();
        if (ids.Count == 0)
            return [];

        var past = await db.Citations
            .AsNoTracking()
            .Where(c => c.ParentId != null && ids.Contains(c.ParentId.Value))
            .Where(c => c.ScheduledDate != null && c.ScheduledDate < today)
            .Where(c => c.Status == CitationStatuses.Scheduled ||
                        c.Status == CitationStatuses.Rescheduled ||
                        c.Status == CitationStatuses.Closed)
            .Select(c => new { ParentId = c.ParentId!.Value, c.ScheduledDate, c.ScheduledTime, c.CitationId, c.Attended })
            .ToListAsync();

        return past
            .GroupBy(c => c.ParentId)
            .ToDictionary(
                g => g.Key,
                g => g
                    .OrderByDescending(c => c.ScheduledDate)
                    .ThenByDescending(c => c.ScheduledTime)
                    .ThenByDescending(c => c.CitationId)
                    .TakeWhile(c => !c.Attended)
                    .Count());
    }

    private static bool CanManage(ObservationAccess access, int? responsibleUserId, int createdById) =>
        access.SeesAll || responsibleUserId == access.UserId || createdById == access.UserId;

    private async Task<Citation> FindManageableAsync(ObservationAccess access, int citationId)
    {
        var citation = await db.Citations.FirstOrDefaultAsync(c => c.CitationId == citationId)
            ?? throw new InvalidOperationException("No existe la citación.");

        if (!CanManage(access, citation.ResponsibleUserId, citation.CreatedById))
            throw new UnauthorizedAccessException();

        return citation;
    }

    private async Task ApplyScheduleAsync(Citation citation, ScheduleCitationRequest request, DateOnly today)
    {
        if (request.Date < today)
            throw new InvalidOperationException("La fecha de la citación no puede ser pasada.");

        var slots = citation.ResponsibleUserId is { } responsible ? await SlotsAsync(responsible) : [];
        TimeOnly time;
        int? scheduleId = null;

        if (slots.Count > 0)
        {
            var slot = slots.FirstOrDefault(s => s.ScheduleId == request.ScheduleId)
                ?? throw new InvalidOperationException("Elija una de sus franjas de atención a padres.");

            if (slot.Weekday != AttendanceRules.WeekdayOf(request.Date))
                throw new InvalidOperationException($"La franja elegida es los {DayNames[slot.Weekday]}; la fecha no coincide.");

            time = request.Time ?? slot.StartTime;
            if (time < slot.StartTime || time >= slot.EndTime)
                throw new InvalidOperationException($"La hora debe estar entre {slot.StartTime:HH\\:mm} y {slot.EndTime:HH\\:mm}.");

            scheduleId = slot.ScheduleId;
        }
        else
        {
            time = request.Time ?? throw new InvalidOperationException("Indique la hora de la citación.");
        }

        citation.ScheduledDate = request.Date;
        citation.ScheduledTime = time;
        citation.ScheduleId = scheduleId;
        citation.Status = CitationStatuses.Scheduled;
    }

    private async Task NotifyParentAsync(Citation citation)
    {
        if (citation.ParentId is not { } parentId || citation.ScheduledDate is not { } date || citation.ScheduledTime is not { } time)
            return;

        var student = await StudentInfoAsync(citation.StudentId);
        var responsible = citation.ResponsibleUserId is { } responsibleId
            ? await db.Users.Where(u => u.Id == responsibleId).Select(u => u.FirstName + " " + u.LastName).FirstOrDefaultAsync()
            : null;

        await notifications.QueueAsync(
            NotificationCodes.CitationParent,
            new Dictionary<string, string>
            {
                ["fecha"] = $"{DayNames[(int)date.DayOfWeek]} {date:dd/MM/yyyy}",
                ["hora"] = time.ToString("HH\\:mm"),
                ["responsable"] = responsible ?? CoordinationName,
                ["estudiante"] = student.Name,
                ["motivo"] = citation.Reason,
            },
            new NotificationTarget(
                StudentId: citation.StudentId,
                SourceTable: "Citation",
                SourceId: citation.CitationId,
                RecipientUserId: parentId));

        await db.SaveChangesAsync();
    }

    // Acudiente elegido (debe estar vinculado al estudiante) o, si no se elige, el principal vigente.
    private async Task<int?> ResolveParentAsync(int studentId, int? requested)
    {
        if (requested is not { } parentId)
            return await PrimaryParentIdAsync(studentId);

        if (!await db.ParentStudents.AnyAsync(ps => ps.StudentId == studentId && ps.ParentId == parentId))
            throw new InvalidOperationException("El acudiente no está asociado al estudiante.");

        return parentId;
    }

    private Task<int?> PrimaryParentIdAsync(int studentId) =>
        db.ParentStudents
            .Where(ps => ps.StudentId == studentId && ps.Parent.Active)
            .OrderByDescending(ps => ps.PrimaryContact)
            .ThenBy(ps => ps.ParentStudentId)
            .Select(ps => (int?)ps.ParentId)
            .FirstOrDefaultAsync();

    private async Task<(string Name, string Classroom)> StudentInfoAsync(int studentId)
    {
        var info = await db.Students
            .Where(s => s.StudentId == studentId)
            .Select(s => new
            {
                Name = s.User.FirstName + " " + s.User.LastName,
                Classroom = db.Enrollments
                    .Where(e => e.StudentId == studentId)
                    .OrderByDescending(e => e.YearId)
                    .Select(e => e.Classroom.Name)
                    .FirstOrDefault(),
            })
            .FirstAsync();

        return (info.Name, info.Classroom ?? string.Empty);
    }

    private static string? CleanNote(string? note)
    {
        var text = note?.Trim();
        if (string.IsNullOrEmpty(text))
            return null;

        return text.Length > NoteLength ? text[..NoteLength] : text;
    }
}

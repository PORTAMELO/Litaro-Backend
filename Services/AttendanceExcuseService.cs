using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services;

public record SaveExcuseRequest(int StudentId, DateTime StartAt, DateTime EndAt, string? Reason);

public record ExcusePreviewDto(int Absences, bool Expired, DateOnly Deadline);

public record ExcuseDto(
    int AttendanceExcuseId,
    int StudentId,
    string StudentName,
    string ClassroomName,
    DateTime StartAt,
    DateTime EndAt,
    string? Reason,
    int Absences,
    string CreatedBy,
    DateTime CreatedAt);

public class AttendanceExcuseService(AppDbContext db, AttendanceSettingsService settings)
{
    private const int ListLimit = 200;
    private const int ReasonLength = 300;

    public async Task<List<ExcuseDto>> ListAsync(short? yearId, string? query)
    {
        var text = (query ?? string.Empty).Trim().ToLower();

        var rows = await db.AttendanceExcuses
            .AsNoTracking()
            .Where(e => yearId == null || e.StartAt.Year == yearId)
            .Where(e => text == string.Empty ||
                        e.Student.User.FirstName.ToLower().Contains(text) ||
                        e.Student.User.LastName.ToLower().Contains(text))
            .OrderByDescending(e => e.CreatedAt)
            .ThenByDescending(e => e.AttendanceExcuseId)
            .Take(ListLimit)
            .Select(e => new
            {
                e.AttendanceExcuseId,
                e.StudentId,
                StudentName = e.Student.User.LastName + ", " + e.Student.User.FirstName,
                ClassroomName = db.Enrollments
                    .Where(en => en.StudentId == e.StudentId)
                    .OrderByDescending(en => en.YearId)
                    .Select(en => en.Classroom.Name)
                    .FirstOrDefault(),
                e.StartAt,
                e.EndAt,
                e.Reason,
                Absences = db.AttendanceAbsences.Count(a => a.AttendanceExcuseId == e.AttendanceExcuseId),
                CreatedBy = e.CreatedBy.FirstName + " " + e.CreatedBy.LastName,
                e.CreatedAt,
            })
            .ToListAsync();

        return rows
            .Select(r => new ExcuseDto(
                r.AttendanceExcuseId,
                r.StudentId,
                r.StudentName,
                r.ClassroomName ?? string.Empty,
                r.StartAt,
                r.EndAt,
                r.Reason,
                r.Absences,
                r.CreatedBy,
                r.CreatedAt))
            .ToList();
    }

    public async Task<ExcusePreviewDto> PreviewAsync(SaveExcuseRequest request, DateOnly today)
    {
        var deadline = await DeadlineAsync(request.EndAt);
        var covered = await UncoveredAbsencesAsync(request.StudentId, request.StartAt, request.EndAt);

        return new ExcusePreviewDto(covered.Count, today > deadline, deadline);
    }

    public async Task<int> CreateAsync(SaveExcuseRequest request, int userId, DateOnly today)
    {
        if (!await db.Students.AnyAsync(s => s.StudentId == request.StudentId))
            throw new InvalidOperationException("No existe el estudiante.");

        if (request.EndAt <= request.StartAt)
            throw new InvalidOperationException("La fecha de fin debe ser posterior a la de inicio.");

        var deadline = await DeadlineAsync(request.EndAt);
        if (today > deadline)
            throw new InvalidOperationException($"El plazo para presentar esta excusa venció el {deadline:dd/MM/yyyy}.");

        var reason = request.Reason?.Trim();
        if (reason is { Length: > ReasonLength })
            reason = reason[..ReasonLength];

        var excuse = new AttendanceExcuse
        {
            StudentId = request.StudentId,
            StartAt = Local(request.StartAt),
            EndAt = Local(request.EndAt),
            Reason = string.IsNullOrEmpty(reason) ? null : reason,
            CreatedById = userId,
        };
        db.AttendanceExcuses.Add(excuse);

        foreach (var absence in await UncoveredAbsencesAsync(request.StudentId, request.StartAt, request.EndAt))
            absence.Excuse = excuse;

        await db.SaveChangesAsync();
        return excuse.AttendanceExcuseId;
    }

    public async Task<List<int>> DeleteAsync(int id)
    {
        var excuse = await db.AttendanceExcuses.FirstOrDefaultAsync(e => e.AttendanceExcuseId == id);
        if (excuse is null)
            return [];

        var absences = await db.AttendanceAbsences.Where(a => a.AttendanceExcuseId == id).ToListAsync();

        foreach (var absence in absences)
        {
            absence.AttendanceExcuseId = null;
            absence.Excuse = null;
        }

        db.AttendanceExcuses.Remove(excuse);
        await db.SaveChangesAsync();

        return absences.Select(a => a.EnrollmentId).Distinct().ToList();
    }

    public async Task LinkCoveringAsync(AttendanceSession session, IReadOnlyCollection<AttendanceAbsence> newAbsences)
    {
        if (newAbsences.Count == 0)
            return;

        var enrollmentIds = newAbsences.Select(a => a.EnrollmentId).ToList();
        var students = await db.Enrollments
            .Where(e => enrollmentIds.Contains(e.EnrollmentId))
            .Select(e => new { e.EnrollmentId, e.StudentId })
            .ToListAsync();

        var studentIds = students.Select(s => s.StudentId).ToList();
        var dayStart = session.Date.ToDateTime(TimeOnly.MinValue);
        var dayEnd = session.Date.ToDateTime(TimeOnly.MaxValue);

        var excuses = await db.AttendanceExcuses
            .Where(e => studentIds.Contains(e.StudentId) && e.StartAt <= dayEnd && e.EndAt >= dayStart)
            .ToListAsync();

        foreach (var absence in newAbsences)
        {
            var studentId = students.FirstOrDefault(s => s.EnrollmentId == absence.EnrollmentId)?.StudentId;
            var excuse = excuses.FirstOrDefault(e => e.StudentId == studentId && Covers(session, e.StartAt, e.EndAt));

            if (excuse is not null)
                absence.Excuse = excuse;
        }
    }

    public static bool Covers(AttendanceSession session, DateTime startAt, DateTime endAt)
    {
        if (session.Kind == AttendanceSessionKinds.Daily || session.StartTime is null)
            return session.Date >= DateOnly.FromDateTime(startAt) && session.Date <= DateOnly.FromDateTime(endAt);

        var moment = session.Date.ToDateTime(session.StartTime.Value);
        return moment >= startAt && moment <= endAt;
    }

    private async Task<DateOnly> DeadlineAsync(DateTime endAt)
    {
        var rules = await settings.GetAsync();
        return AttendanceRules.ExcuseDeadline(DateOnly.FromDateTime(endAt), rules.ExcuseDeadlineDays);
    }

    private async Task<List<AttendanceAbsence>> UncoveredAbsencesAsync(int studentId, DateTime startAt, DateTime endAt)
    {
        var from = DateOnly.FromDateTime(startAt);
        var to = DateOnly.FromDateTime(endAt);

        var candidates = await db.AttendanceAbsences
            .Include(a => a.Session)
            .Where(a => a.Enrollment.StudentId == studentId && a.AttendanceExcuseId == null &&
                        a.Session.Date >= from && a.Session.Date <= to)
            .ToListAsync();

        return candidates.Where(a => Covers(a.Session, startAt, endAt)).ToList();
    }

    private static DateTime Local(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Unspecified);
}

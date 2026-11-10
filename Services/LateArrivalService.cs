using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services;

public record LateArrivalDto(
    int LateArrivalId,
    int EnrollmentId,
    string StudentName,
    string ClassroomName,
    DateTime ArrivedAt,
    int PeriodCount);

public record StudentSearchDto(
    int EnrollmentId,
    int StudentId,
    string Name,
    string Document,
    string StudentCode,
    string ClassroomName);

public class LateArrivalService(AppDbContext db, AttendanceSettingsService settings, NotificationService notifications)
{
    private const int SearchLimit = 20;

    public async Task<List<LateArrivalDto>> ListAsync(DateOnly date)
    {
        var rows = await db.LateArrivals
            .AsNoTracking()
            .Where(l => l.Date == date)
            .OrderByDescending(l => l.ArrivedAt)
            .Select(l => new
            {
                l.LateArrivalId,
                l.EnrollmentId,
                l.Enrollment.YearId,
                StudentName = l.Enrollment.Student.User.FirstName + " " + l.Enrollment.Student.User.LastName,
                ClassroomName = l.Enrollment.Classroom.Name,
                l.ArrivedAt,
            })
            .ToListAsync();

        var result = new List<LateArrivalDto>();

        foreach (var yearRows in rows.GroupBy(r => r.YearId))
        {
            var period = await AttendancePeriods.FindAsync(db, yearRows.Key, date);
            var counts = period is null
                ? []
                : await CountInPeriodAsync(yearRows.Select(r => r.EnrollmentId).ToList(), period);

            result.AddRange(yearRows.Select(r => new LateArrivalDto(
                r.LateArrivalId,
                r.EnrollmentId,
                r.StudentName,
                r.ClassroomName,
                r.ArrivedAt,
                counts.GetValueOrDefault(r.EnrollmentId))));
        }

        return result.OrderByDescending(r => r.ArrivedAt).ToList();
    }

    public async Task<LateArrivalDto> RegisterAsync(int enrollmentId, DateOnly date, int userId, DateOnly today)
    {
        var enrollment = await db.Enrollments
            .Where(e => e.EnrollmentId == enrollmentId && e.Status == EnrollmentStatuses.Active)
            .Select(e => new
            {
                e.EnrollmentId,
                e.StudentId,
                e.YearId,
                e.Classroom.CampusId,
                ClassroomName = e.Classroom.Name,
                StudentName = e.Student.User.FirstName + " " + e.Student.User.LastName,
            })
            .FirstOrDefaultAsync();

        if (enrollment is null)
            throw new InvalidOperationException("El estudiante no tiene matrícula activa.");

        if (date > today)
            throw new InvalidOperationException("No se puede registrar una llegada en una fecha futura.");

        var period = await AttendancePeriods.FindAsync(db, enrollment.YearId, date);
        if (period is null)
            throw new InvalidOperationException("La fecha no pertenece a ningún periodo académico.");

        if (await db.LateArrivals.AnyAsync(l => l.EnrollmentId == enrollmentId && l.Date == date))
            throw new InvalidOperationException("Ya se registró la llegada tarde de hoy para este estudiante.");

        var arrival = new LateArrival
        {
            EnrollmentId = enrollmentId,
            CampusId = enrollment.CampusId,
            Date = date,
            RecordedById = userId,
        };
        db.LateArrivals.Add(arrival);

        var previous = (await CountInPeriodAsync([enrollmentId], period)).GetValueOrDefault(enrollmentId);
        var count = previous + 1;
        var rules = await settings.GetAsync();

        var code = count == rules.LateCommitmentCount
            ? NotificationCodes.AttLateCommitment
            : count == rules.LateCitationCount ? NotificationCodes.AttLateCitation : null;

        if (code is not null)
        {
            await notifications.QueueAsync(
                code,
                new Dictionary<string, string>
                {
                    ["estudiante"] = enrollment.StudentName,
                    ["curso"] = enrollment.ClassroomName,
                    ["cantidad"] = count.ToString(),
                    ["periodo"] = period.PeriodNumber.ToString(),
                },
                new NotificationTarget(
                    StudentId: enrollment.StudentId,
                    YearId: enrollment.YearId,
                    CampusId: enrollment.CampusId,
                    DedupKey: $"LATE:{enrollmentId}:{period.PeriodId}:{count}"));
        }

        await db.SaveChangesAsync();

        return new LateArrivalDto(
            arrival.LateArrivalId,
            enrollmentId,
            enrollment.StudentName,
            enrollment.ClassroomName,
            arrival.ArrivedAt,
            count);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var arrival = await db.LateArrivals.FirstOrDefaultAsync(l => l.LateArrivalId == id);
        if (arrival is null)
            return false;

        db.LateArrivals.Remove(arrival);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<List<StudentSearchDto>> SearchStudentsAsync(string? query)
    {
        var text = (query ?? string.Empty).Trim().ToLower();
        if (text.Length < 2)
            return [];

        return await db.Enrollments
            .AsNoTracking()
            .Where(e => e.Status == EnrollmentStatuses.Active && e.AcademicYear.Active)
            .Where(e => e.Student.User.FirstName.ToLower().Contains(text) ||
                        e.Student.User.LastName.ToLower().Contains(text) ||
                        e.Student.User.DocumentNumber.ToLower().Contains(text) ||
                        e.Student.StudentCode.ToLower().Contains(text))
            .OrderBy(e => e.Student.User.LastName)
            .ThenBy(e => e.Student.User.FirstName)
            .Take(SearchLimit)
            .Select(e => new StudentSearchDto(
                e.EnrollmentId,
                e.StudentId,
                e.Student.User.FirstName + " " + e.Student.User.LastName,
                e.Student.User.DocumentNumber,
                e.Student.StudentCode,
                e.Classroom.Name))
            .ToListAsync();
    }

    private async Task<Dictionary<int, int>> CountInPeriodAsync(List<int> enrollmentIds, AcademicPeriod period)
    {
        var start = AttendancePeriods.Start(period);
        var end = AttendancePeriods.End(period);

        var rows = await db.LateArrivals
            .Where(l => enrollmentIds.Contains(l.EnrollmentId) && l.Date >= start && l.Date <= end)
            .GroupBy(l => l.EnrollmentId)
            .Select(g => new { EnrollmentId = g.Key, Count = g.Count() })
            .ToListAsync();

        return rows.ToDictionary(r => r.EnrollmentId, r => r.Count);
    }
}

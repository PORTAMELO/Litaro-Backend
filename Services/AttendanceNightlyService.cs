using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services;

public class AttendanceNightlyService(
    AppDbContext db,
    AttendanceSettingsService settings,
    NotificationService notifications,
    AttendanceCalculationService calculation)
{
    public const string JobName = "AttendanceNightly";

    private sealed record Student(int EnrollmentId, int StudentId, int ClassroomId, int CampusId, string Name, string ClassroomName);

    private sealed record TakenSession(int AttendanceSessionId, int ClassroomId, DateOnly Date);

    public async Task RunAsync(DateOnly today)
    {
        var rules = await settings.GetAsync();
        var yearIds = await db.AcademicYears.Where(y => y.Active).Select(y => y.YearId).ToListAsync();

        foreach (var yearId in yearIds)
            await RunYearAsync(yearId, today, rules);

        await db.SaveChangesAsync();
    }

    private async Task RunYearAsync(short yearId, DateOnly today, AttendanceSettings rules)
    {
        var students = await db.Enrollments
            .AsNoTracking()
            .Where(e => e.YearId == yearId && e.Status == EnrollmentStatuses.Active)
            .Select(e => new Student(
                e.EnrollmentId,
                e.StudentId,
                e.ClassroomId,
                e.Classroom.CampusId,
                e.Student.User.FirstName + " " + e.Student.User.LastName,
                e.Classroom.Name))
            .ToListAsync();

        var sessions = await db.AttendanceSessions
            .AsNoTracking()
            .Where(s => s.YearId == yearId && s.Date <= today)
            .Select(s => new TakenSession(s.AttendanceSessionId, s.ClassroomId, s.Date))
            .ToListAsync();

        var unjustified = (await db.AttendanceAbsences
                .AsNoTracking()
                .Where(a => a.Session.YearId == yearId && a.Session.Date <= today && a.AttendanceExcuseId == null)
                .Select(a => new { a.AttendanceSessionId, a.EnrollmentId })
                .ToListAsync())
            .Select(a => (a.AttendanceSessionId, a.EnrollmentId))
            .ToHashSet();

        var periods = await db.AcademicPeriods
            .AsNoTracking()
            .Where(p => p.YearId == yearId)
            .OrderBy(p => p.PeriodNumber)
            .ToListAsync();

        var sessionsByClassroom = sessions.ToLookup(s => s.ClassroomId);

        foreach (var student in students)
        {
            var own = sessionsByClassroom[student.ClassroomId].ToList();

            await NotifyStreaksAsync(student, own, unjustified, yearId, rules.AbsenceAlertDays);
            await NotifyPeriodsAsync(student, own, unjustified, yearId, periods.Where(p => AttendancePeriods.End(p) < today));
        }

        var current = periods.FirstOrDefault(p => AttendancePeriods.Contains(p, today));
        if (current is not null)
            await calculation.RecalculateAsync(students.Select(s => s.EnrollmentId), current);

        await FreezeAsync(yearId, periods.Where(p => AttendancePeriods.End(p) < today).ToList());
    }

    private async Task NotifyStreaksAsync(
        Student student,
        List<TakenSession> sessions,
        HashSet<(int SessionId, int EnrollmentId)> unjustified,
        short yearId,
        int alertDays)
    {
        var days = sessions
            .GroupBy(s => s.Date)
            .OrderBy(g => g.Key)
            .Select(g => (Day: g.Key, FullyAbsent: g.All(s => unjustified.Contains((s.AttendanceSessionId, student.EnrollmentId)))))
            .ToList();

        foreach (var start in AttendanceRules.AbsenceStreakStarts(days, alertDays))
        {
            await notifications.QueueAsync(
                NotificationCodes.AttWeekAbsence,
                new Dictionary<string, string>
                {
                    ["estudiante"] = student.Name,
                    ["curso"] = student.ClassroomName,
                    ["dias"] = alertDays.ToString(),
                },
                new NotificationTarget(
                    StudentId: student.StudentId,
                    YearId: yearId,
                    CampusId: student.CampusId,
                    DedupKey: $"WEEK:{student.EnrollmentId}:{start:yyyyMMdd}"));
        }
    }

    private async Task NotifyPeriodsAsync(
        Student student,
        List<TakenSession> sessions,
        HashSet<(int SessionId, int EnrollmentId)> unjustified,
        short yearId,
        IEnumerable<AcademicPeriod> closedPeriods)
    {
        foreach (var period in closedPeriods)
        {
            var inPeriod = sessions.Where(s => AttendancePeriods.Contains(period, s.Date)).ToList();

            if (inPeriod.Count == 0 ||
                !inPeriod.All(s => unjustified.Contains((s.AttendanceSessionId, student.EnrollmentId))))
                continue;

            await notifications.QueueAsync(
                NotificationCodes.AttPeriodAbsence,
                new Dictionary<string, string>
                {
                    ["estudiante"] = student.Name,
                    ["curso"] = student.ClassroomName,
                    ["periodo"] = period.PeriodNumber.ToString(),
                },
                new NotificationTarget(
                    StudentId: student.StudentId,
                    YearId: yearId,
                    CampusId: student.CampusId,
                    DedupKey: $"PERIOD:{student.EnrollmentId}:{period.PeriodId}"));
        }
    }

    private async Task FreezeAsync(short yearId, List<AcademicPeriod> closedPeriods)
    {
        if (closedPeriods.Count == 0)
            return;

        var closedIds = closedPeriods.Select(p => p.PeriodId).ToList();
        var alreadyFrozen = await db.PeriodPlannedHours
            .Where(p => closedIds.Contains(p.PeriodId))
            .Select(p => p.PeriodId)
            .Distinct()
            .ToListAsync();

        var pending = closedPeriods.Where(p => !alreadyFrozen.Contains(p.PeriodId)).ToList();
        if (pending.Count == 0)
            return;

        var classroomIds = await db.Schedules
            .Where(s => s.YearId == yearId && s.Type == ScheduleTypes.Class && s.ClassroomId != null)
            .Select(s => s.ClassroomId!.Value)
            .Distinct()
            .ToListAsync();

        foreach (var period in pending)
        {
            foreach (var classroomId in classroomIds)
            {
                var planned = await calculation.PlannedHoursAsync(classroomId, period);

                db.PeriodPlannedHours.AddRange(planned.Select(entry => new PeriodPlannedHours
                {
                    PeriodId = period.PeriodId,
                    ClassroomId = classroomId,
                    SubjectId = entry.Key,
                    PlannedHours = entry.Value,
                }));
            }
        }
    }
}

using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services;

public record SummaryRowDto(
    int EnrollmentId,
    string StudentName,
    decimal Unjustified,
    decimal Justified,
    decimal Percent,
    bool Lost);

public record SummaryDto(
    short PeriodId,
    int ClassroomId,
    int SubjectId,
    decimal PlannedHours,
    decimal LossPercent,
    List<SummaryRowDto> Students);

public class AttendanceCalculationService(AppDbContext db, AttendanceSettingsService settings)
{
    private sealed record AbsenceHours(decimal Unjustified, decimal Justified);

    private sealed record ClassroomSchedule(short? ClassMinutes, List<ScheduleBlock> Blocks);

    public async Task<Dictionary<int, decimal>> PlannedHoursAsync(int classroomId, AcademicPeriod period)
    {
        var frozen = await db.PeriodPlannedHours
            .AsNoTracking()
            .Where(p => p.PeriodId == period.PeriodId && p.ClassroomId == classroomId)
            .ToListAsync();

        if (frozen.Count > 0)
            return frozen.ToDictionary(p => p.SubjectId, p => p.PlannedHours);

        var schedule = await LoadScheduleAsync(classroomId, period.YearId);

        return AttendanceRules.PlannedHours(
            schedule.Blocks,
            AttendancePeriods.Start(period),
            AttendancePeriods.End(period),
            schedule.ClassMinutes);
    }

    public async Task<SummaryDto> GetSummaryAsync(short periodId, int classroomId, int subjectId)
    {
        var period = await db.AcademicPeriods.AsNoTracking().FirstOrDefaultAsync(p => p.PeriodId == periodId)
            ?? throw new InvalidOperationException("No existe el periodo académico.");

        var planned = (await PlannedHoursAsync(classroomId, period)).GetValueOrDefault(subjectId);
        var lossPercent = (await settings.GetAsync()).LossPercent;

        var roster = await db.Enrollments
            .AsNoTracking()
            .Where(e => e.ClassroomId == classroomId && e.YearId == period.YearId)
            .OrderBy(e => e.Student.User.LastName)
            .ThenBy(e => e.Student.User.FirstName)
            .Select(e => new
            {
                e.EnrollmentId,
                Name = e.Student.User.LastName + ", " + e.Student.User.FirstName,
                Active = e.Status == EnrollmentStatuses.Active,
            })
            .ToListAsync();

        var hours = await LoadHoursAsync(roster.Select(r => r.EnrollmentId).ToList(), period);
        var withAbsences = hours.Keys.Select(key => key.EnrollmentId).ToHashSet();

        var students = roster
            .Where(r => r.Active || withAbsences.Contains(r.EnrollmentId))
            .Select(r =>
            {
                var own = hours.GetValueOrDefault((r.EnrollmentId, subjectId)) ?? new AbsenceHours(0, 0);
                var percent = AttendanceRules.Percent(own.Unjustified, planned);

                return new SummaryRowDto(
                    r.EnrollmentId,
                    r.Name,
                    own.Unjustified,
                    own.Justified,
                    percent,
                    planned > 0 && AttendanceRules.IsLoss(percent, lossPercent));
            })
            .ToList();

        return new SummaryDto(periodId, classroomId, subjectId, planned, lossPercent, students);
    }

    public async Task RecalculateAsync(IEnumerable<int> enrollmentIds, AcademicPeriod period)
    {
        var ids = enrollmentIds.Distinct().ToList();
        if (ids.Count == 0)
            return;

        var enrollments = await db.Enrollments
            .Where(e => ids.Contains(e.EnrollmentId))
            .Select(e => new { e.EnrollmentId, e.ClassroomId })
            .ToListAsync();

        var hours = await LoadHoursAsync(ids, period);
        var lossPercent = (await settings.GetAsync()).LossPercent;

        var existing = await db.AbsenceLosses
            .Where(l => ids.Contains(l.EnrollmentId) && l.PeriodId == period.PeriodId)
            .ToListAsync();

        var plannedByClassroom = new Dictionary<int, Dictionary<int, decimal>>();

        foreach (var enrollment in enrollments)
        {
            if (!plannedByClassroom.TryGetValue(enrollment.ClassroomId, out var planned))
            {
                planned = await PlannedHoursAsync(enrollment.ClassroomId, period);
                plannedByClassroom[enrollment.ClassroomId] = planned;
            }

            var lost = new HashSet<int>();

            foreach (var (subjectId, plannedHours) in planned.Where(p => p.Value > 0))
            {
                var unjustified = hours.GetValueOrDefault((enrollment.EnrollmentId, subjectId))?.Unjustified ?? 0;
                var percent = AttendanceRules.Percent(unjustified, plannedHours);

                if (!AttendanceRules.IsLoss(percent, lossPercent))
                    continue;

                lost.Add(subjectId);

                var row = existing.FirstOrDefault(l => l.EnrollmentId == enrollment.EnrollmentId && l.SubjectId == subjectId);
                if (row is null)
                {
                    row = new AbsenceLoss
                    {
                        EnrollmentId = enrollment.EnrollmentId,
                        SubjectId = subjectId,
                        PeriodId = period.PeriodId,
                    };
                    db.AbsenceLosses.Add(row);
                }

                row.UnjustifiedHours = unjustified;
                row.PlannedHours = plannedHours;
                row.Percent = percent;
            }

            db.AbsenceLosses.RemoveRange(existing.Where(l =>
                l.EnrollmentId == enrollment.EnrollmentId && !lost.Contains(l.SubjectId)));
        }
    }

    public Task<bool> TeachesAsync(int userId, int classroomId, int subjectId) =>
        db.AcademicAssignments.AnyAsync(a =>
            a.TeacherId == userId && a.ClassroomId == classroomId && a.SubjectId == subjectId && a.Active);

    private async Task<Dictionary<(int EnrollmentId, int SubjectId), AbsenceHours>> LoadHoursAsync(
        List<int> enrollmentIds,
        AcademicPeriod period)
    {
        var start = AttendancePeriods.Start(period);
        var end = AttendancePeriods.End(period);

        var absences = await db.AttendanceAbsences
            .AsNoTracking()
            .Where(a => enrollmentIds.Contains(a.EnrollmentId) && a.Session.Date >= start && a.Session.Date <= end)
            .Select(a => new
            {
                a.EnrollmentId,
                a.Session.Kind,
                a.Session.SubjectId,
                a.Session.StartTime,
                a.Session.EndTime,
                a.Session.Date,
                a.Session.ClassroomId,
                Justified = a.AttendanceExcuseId != null,
            })
            .ToListAsync();

        var schedules = new Dictionary<int, ClassroomSchedule>();
        var totals = new Dictionary<(int EnrollmentId, int SubjectId), AbsenceHours>();

        void Add(int enrollmentId, int subjectId, decimal hours, bool justified)
        {
            var key = (enrollmentId, subjectId);
            var current = totals.GetValueOrDefault(key) ?? new AbsenceHours(0, 0);

            totals[key] = justified
                ? current with { Justified = current.Justified + hours }
                : current with { Unjustified = current.Unjustified + hours };
        }

        foreach (var absence in absences)
        {
            if (!schedules.TryGetValue(absence.ClassroomId, out var schedule))
            {
                schedule = await LoadScheduleAsync(absence.ClassroomId, period.YearId);
                schedules[absence.ClassroomId] = schedule;
            }

            if (absence.Kind == AttendanceSessionKinds.Class)
            {
                Add(
                    absence.EnrollmentId,
                    absence.SubjectId!.Value,
                    AttendanceRules.Hours(absence.StartTime!.Value, absence.EndTime!.Value, schedule.ClassMinutes),
                    absence.Justified);
                continue;
            }

            var weekday = AttendanceRules.WeekdayOf(absence.Date);

            foreach (var block in schedule.Blocks.Where(b => b.Weekday == weekday))
            {
                Add(
                    absence.EnrollmentId,
                    block.SubjectId,
                    AttendanceRules.Hours(block.StartTime, block.EndTime, schedule.ClassMinutes),
                    absence.Justified);
            }
        }

        return totals;
    }

    private async Task<ClassroomSchedule> LoadScheduleAsync(int classroomId, short yearId)
    {
        var classMinutes = await db.Classrooms
            .Where(c => c.ClassroomId == classroomId)
            .Select(c => c.ClassMinutes)
            .FirstOrDefaultAsync();

        var blocks = await db.Schedules
            .AsNoTracking()
            .Where(s => s.ClassroomId == classroomId && s.YearId == yearId && s.Type == ScheduleTypes.Class)
            .Select(s => new { s.Weekday, s.AcademicAssignment!.SubjectId, s.StartTime, s.EndTime })
            .ToListAsync();

        return new ClassroomSchedule(
            classMinutes,
            blocks.Select(b => new ScheduleBlock(b.Weekday, b.SubjectId, b.StartTime, b.EndTime)).ToList());
    }
}

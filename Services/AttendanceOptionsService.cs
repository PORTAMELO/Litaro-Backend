using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services;

public record OptionPeriodDto(short PeriodId, byte PeriodNumber, short YearId, DateOnly StartDate, DateOnly EndDate, bool Current);

public record OptionClassroomDto(int ClassroomId, string Name, bool Daily);

public record OptionSubjectDto(int ClassroomId, int SubjectId, string Name);

public record AttendanceOptionsDto(
    DateOnly Today,
    List<OptionPeriodDto> Periods,
    List<OptionClassroomDto> Classrooms,
    List<OptionSubjectDto> Subjects);

public class AttendanceOptionsService(AppDbContext db, AttendanceSettingsService settings)
{
    public Task<bool> IsTeacherAsync(int userId) =>
        db.Teachers.AnyAsync(t => t.TeacherId == userId && t.Active);

    public async Task<AttendanceOptionsDto> GetAsync(int userId, bool seesAll, DateOnly today)
    {
        var restricted = !seesAll && await IsTeacherAsync(userId);
        var dailyLevels = (await settings.GetAsync()).DailyLevels;

        var periods = (await db.AcademicPeriods
                .AsNoTracking()
                .Where(p => p.AcademicYear.Active)
                .OrderByDescending(p => p.YearId)
                .ThenBy(p => p.PeriodNumber)
                .ToListAsync())
            .Select(p => new OptionPeriodDto(
                p.PeriodId,
                p.PeriodNumber,
                p.YearId,
                AttendancePeriods.Start(p),
                AttendancePeriods.End(p),
                AttendancePeriods.Contains(p, today)))
            .ToList();

        var assignments = await db.AcademicAssignments
            .AsNoTracking()
            .Where(a => a.Active && a.AcademicYear.Active && a.Classroom.Active)
            .Where(a => !restricted || a.TeacherId == userId)
            .Select(a => new
            {
                a.ClassroomId,
                ClassroomName = a.Classroom.Name,
                a.Classroom.Grade.Level,
                a.SubjectId,
                SubjectName = a.Subject.Name,
            })
            .ToListAsync();

        var directed = restricted
            ? await db.Classrooms
                .AsNoTracking()
                .Where(c => c.Active && c.DirectorTeacherId == userId)
                .Select(c => new { c.ClassroomId, ClassroomName = c.Name, c.Grade.Level })
                .ToListAsync()
            : [];

        var classrooms = assignments
            .Select(a => new { a.ClassroomId, a.ClassroomName, a.Level })
            .Concat(directed)
            .GroupBy(c => c.ClassroomId)
            .Select(g => g.First())
            .OrderBy(c => c.ClassroomName)
            .Select(c => new OptionClassroomDto(c.ClassroomId, c.ClassroomName, AttendanceRules.IsDailyLevel(c.Level, dailyLevels)))
            .ToList();

        var subjects = assignments
            .GroupBy(a => new { a.ClassroomId, a.SubjectId })
            .Select(g => new OptionSubjectDto(g.Key.ClassroomId, g.Key.SubjectId, g.First().SubjectName))
            .OrderBy(s => s.Name)
            .ToList();

        return new AttendanceOptionsDto(today, periods, classrooms, subjects);
    }
}

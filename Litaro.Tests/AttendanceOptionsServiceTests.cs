using Litaro.Services;

namespace Litaro.Tests;

public class AttendanceOptionsServiceTests
{
    [Fact]
    public async Task A_teacher_only_gets_their_classrooms_and_subjects()
    {
        using var db = TestDb.Create();
        var school = await TestSchool.CreateAsync(db);
        var svc = new AttendanceOptionsService(db, new AttendanceSettingsService(db));

        var options = await svc.GetAsync(school.T2.Id, false, school.Tuesday);

        Assert.Equal(new[] { "1001" }, options.Classrooms.Select(c => c.Name));
        Assert.Equal(new[] { "Matemáticas", "Química" }, options.Subjects.Select(s => s.Name).OrderBy(n => n));
        Assert.Equal(school.Tuesday, options.Today);
    }

    [Fact]
    public async Task Someone_who_sees_everything_gets_every_classroom()
    {
        using var db = TestDb.Create();
        var school = await TestSchool.CreateAsync(db);
        var svc = new AttendanceOptionsService(db, new AttendanceSettingsService(db));

        var options = await svc.GetAsync(school.Marta.Id, true, school.Tuesday);

        Assert.Equal(new[] { "1001", "3A" }, options.Classrooms.Select(c => c.Name));
        Assert.True(options.Classrooms.Single(c => c.Name == "3A").Daily);
        Assert.False(options.Classrooms.Single(c => c.Name == "1001").Daily);
        Assert.Equal(5, options.Subjects.Count);
    }

    [Fact]
    public async Task The_period_that_contains_today_is_marked_current()
    {
        using var db = TestDb.Create();
        var school = await TestSchool.CreateAsync(db);
        var svc = new AttendanceOptionsService(db, new AttendanceSettingsService(db));

        var options = await svc.GetAsync(school.Marta.Id, true, school.Tuesday);

        Assert.Equal(2, options.Periods.Count);
        Assert.Equal(school.Period.PeriodId, options.Periods.Single(p => p.Current).PeriodId);
    }
}

using Litaro.Services;

namespace Litaro.Tests;

public class AttendanceRulesTests
{
    private static TimeOnly T(int hour, int minute) => new(hour, minute);

    private static DateOnly D(int day) => new(2026, 11, day);

    [Fact]
    public void Hours_uses_the_classroom_minutes()
    {
        Assert.Equal(1m, AttendanceRules.Hours(T(7, 0), T(7, 55), 55));
        Assert.Equal(2m, AttendanceRules.Hours(T(7, 0), T(9, 0), null));
    }

    [Fact]
    public void WeekdayOf_follows_the_schedule_convention()
    {
        Assert.Equal(1, AttendanceRules.WeekdayOf(D(9)));
        Assert.Equal(2, AttendanceRules.WeekdayOf(D(10)));
        Assert.Equal(6, AttendanceRules.WeekdayOf(D(14)));
        Assert.Equal(0, AttendanceRules.WeekdayOf(D(15)));
    }

    [Fact]
    public void PlannedHours_adds_every_block_of_every_date()
    {
        var blocks = new[]
        {
            new ScheduleBlock(1, 10, T(7, 0), T(8, 0)),
            new ScheduleBlock(3, 10, T(7, 0), T(9, 0)),
            new ScheduleBlock(1, 20, T(8, 0), T(9, 0)),
        };

        var planned = AttendanceRules.PlannedHours(blocks, D(2), D(15), null);

        Assert.Equal(6m, planned[10]);
        Assert.Equal(2m, planned[20]);
    }

    [Fact]
    public void PlannedHours_is_empty_without_blocks()
        => Assert.Empty(AttendanceRules.PlannedHours(Array.Empty<ScheduleBlock>(), D(2), D(15), null));

    [Fact]
    public void Percent_and_loss()
    {
        Assert.Equal(22.22m, AttendanceRules.Percent(8m, 36m));
        Assert.Equal(0m, AttendanceRules.Percent(3m, 0m));
        Assert.True(AttendanceRules.IsLoss(20m, 20m));
        Assert.False(AttendanceRules.IsLoss(19.99m, 20m));
    }

    [Fact]
    public void IsEvasion_needs_an_earlier_class_attended()
    {
        Assert.True(AttendanceRules.IsEvasion(T(9, 0), new[] { (T(7, 25), false) }));
        Assert.False(AttendanceRules.IsEvasion(T(9, 0), new[] { (T(7, 25), true) }));
        Assert.False(AttendanceRules.IsEvasion(T(9, 0), new[] { (T(10, 50), false) }));
        Assert.False(AttendanceRules.IsEvasion(T(6, 30), Array.Empty<(TimeOnly, bool)>()));
    }

    [Fact]
    public void IsFirstClassOfDay()
    {
        var starts = new[] { T(6, 30), T(7, 25), T(9, 0) };

        Assert.True(AttendanceRules.IsFirstClassOfDay(T(6, 30), starts));
        Assert.False(AttendanceRules.IsFirstClassOfDay(T(7, 25), starts));
    }

    [Fact]
    public void ExcuseDeadline_counts_business_days()
    {
        Assert.Equal(D(10), AttendanceRules.ExcuseDeadline(D(6), 2));
        Assert.Equal(D(11), AttendanceRules.ExcuseDeadline(D(9), 2));
        Assert.Equal(D(9), AttendanceRules.ExcuseDeadline(D(9), 0));
    }

    [Fact]
    public void AbsenceStreakStarts_reports_each_run_once()
    {
        var five = new[] { (D(2), true), (D(3), true), (D(4), true), (D(5), true), (D(6), true) };
        Assert.Equal(new[] { D(2) }, AttendanceRules.AbsenceStreakStarts(five, 5));

        var broken = new[] { (D(2), true), (D(3), true), (D(4), true), (D(5), true), (D(6), false), (D(9), true) };
        Assert.Empty(AttendanceRules.AbsenceStreakStarts(broken, 5));

        var seven = new[] { (D(2), true), (D(3), true), (D(4), true), (D(5), true), (D(6), true), (D(9), true), (D(10), true) };
        Assert.Equal(new[] { D(2) }, AttendanceRules.AbsenceStreakStarts(seven, 5));
    }

    [Theory]
    [InlineData("primaria", true)]
    [InlineData(" PRIMARIA ", true)]
    [InlineData("Básica Secundaria", false)]
    [InlineData(null, false)]
    public void IsDailyLevel(string? level, bool expected)
        => Assert.Equal(expected, AttendanceRules.IsDailyLevel(level, new[] { "Primaria", "Preescolar" }));

    [Fact]
    public void IsDailyLevel_ignores_accents()
        => Assert.True(AttendanceRules.IsDailyLevel("Transicion", new[] { "Transición" }));

    [Fact]
    public void Nightly_run_decision()
    {
        Assert.True(AttendanceRules.ShouldRunNightly(new DateTime(2026, 11, 10, 23, 5, 0), D(9), 23));
        Assert.False(AttendanceRules.ShouldRunNightly(new DateTime(2026, 11, 10, 23, 30, 0), D(10), 23));
        Assert.False(AttendanceRules.ShouldRunNightly(new DateTime(2026, 11, 10, 10, 0, 0), D(9), 23));
        Assert.True(AttendanceRules.ShouldRunNightly(new DateTime(2026, 11, 10, 10, 0, 0), D(8), 23));
        Assert.True(AttendanceRules.ShouldRunNightly(new DateTime(2026, 11, 10, 10, 0, 0), null, 23));
        Assert.Equal(D(9), AttendanceRules.NightlyRunDate(new DateTime(2026, 11, 10, 10, 0, 0), 23));
    }
}

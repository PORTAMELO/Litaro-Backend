using Litaro.Models;
using Litaro.Services;

namespace Litaro.Tests;

public class GradingRulesTests
{
    [Fact]
    public void IsValidRaw_accepts_10_to_50_with_one_decimal()
    {
        Assert.True(GradingRules.IsValidRaw(10m));
        Assert.True(GradingRules.IsValidRaw(43.5m));
        Assert.True(GradingRules.IsValidRaw(50m));
        Assert.False(GradingRules.IsValidRaw(9.9m));
        Assert.False(GradingRules.IsValidRaw(50.1m));
        Assert.False(GradingRules.IsValidRaw(43.55m));
        Assert.False(GradingRules.IsValidRaw(0m));
    }

    [Fact]
    public void Performance_bands_follow_the_SIEE()
    {
        Assert.Equal("LOW", GradingRules.Performance(3.49m));
        Assert.Equal("BASIC", GradingRules.Performance(3.50m));
        Assert.Equal("BASIC", GradingRules.Performance(3.99m));
        Assert.Equal("HIGH", GradingRules.Performance(4.00m));
        Assert.Equal("HIGH", GradingRules.Performance(4.49m));
        Assert.Equal("SUPERIOR", GradingRules.Performance(4.50m));
        Assert.True(GradingRules.Passes(3.50m));
        Assert.False(GradingRules.Passes(3.49m));
    }

    [Fact]
    public void Round_is_away_from_zero() => Assert.Equal(3.50m, GradingRules.Round(3.495m));

    [Fact]
    public void Weighted_final_uses_70_10_10_10()
    {
        var result = GradingRules.Compute(new(GradingMode.Weighted, [new(60m, 40m), new(40m, 30m)], 45m, 50m, 40m), null);

        Assert.True(result.Complete);
        Assert.Equal(3.87m, result.Final);
        Assert.Equal("BASIC", result.Performance);
        Assert.True(result.Passing);
    }

    [Fact]
    public void Average_final_rounds_to_two_decimals()
    {
        var result = GradingRules.Compute(new(GradingMode.Average, [new(null, 40m), new(null, 35m)], 30m, 30m, 30m), null);

        Assert.Equal(3.53m, result.Final);
    }

    [Fact]
    public void Accumulated_spreads_weight_over_graded_items_only()
    {
        var only = GradingRules.Compute(new(GradingMode.Weighted, [new(50m, 40m), new(50m, null)], null, null, null), null);

        Assert.Equal(4.00m, only.Accumulated);
        Assert.Null(only.Final);
        Assert.False(only.Complete);
        Assert.Equal("HIGH", only.Performance);

        var mixed = GradingRules.Compute(new(GradingMode.Weighted, [new(50m, 40m), new(50m, null)], 30m, null, null), null);

        Assert.Equal(3.88m, mixed.Accumulated);
    }

    [Fact]
    public void Weighted_without_100_percent_has_no_final()
    {
        var result = GradingRules.Compute(new(GradingMode.Weighted, [new(60m, 40m), new(30m, 40m)], 40m, 40m, 40m), null);

        Assert.False(result.Complete);
        Assert.Null(result.Final);
        Assert.Equal(4.00m, result.Accumulated);
    }

    [Fact]
    public void Weighted_with_no_weights_does_not_divide_by_zero()
    {
        var result = GradingRules.Compute(new(GradingMode.Weighted, [new(null, 40m)], null, null, null), null);

        Assert.Null(result.Accumulated);
        Assert.Null(result.Performance);
        Assert.Null(result.Passing);
    }

    [Fact]
    public void No_scores_means_incomplete_without_performance()
    {
        var result = GradingRules.Compute(new(GradingMode.Average, [], null, null, null), null);

        Assert.Null(result.Accumulated);
        Assert.Null(result.Final);
        Assert.False(result.Complete);
        Assert.Null(result.Performance);
        Assert.Null(result.Passing);
    }

    [Fact]
    public void Absence_loss_replaces_the_final_even_if_incomplete()
    {
        var result = GradingRules.Compute(new(GradingMode.Average, [new(null, 48m)], null, null, null), 1.0m);

        Assert.True(result.AbsenceLoss);
        Assert.Equal(1.00m, result.Final);
        Assert.Equal(4.80m, result.Accumulated);
        Assert.Equal("LOW", result.Performance);
        Assert.False(result.Passing);
    }

    [Fact]
    public void Locks_and_closing()
    {
        var now = new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);

        Assert.False(GradingRules.IsDesignLocked(null, now));
        Assert.True(GradingRules.IsDesignLocked(now, now));
        Assert.False(GradingRules.IsDesignLocked(now.AddMinutes(1), now));

        var period = new AcademicPeriod
        {
            StartDate = new DateTime(2026, 1, 19, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 4, 17, 0, 0, 0, DateTimeKind.Utc),
        };

        Assert.False(GradingRules.IsPeriodClosed(period, new DateOnly(2026, 4, 17)));
        Assert.True(GradingRules.IsPeriodClosed(period, new DateOnly(2026, 4, 18)));
    }
}

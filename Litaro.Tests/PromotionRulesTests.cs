using Litaro.Services;

namespace Litaro.Tests;

public class PromotionRulesTests
{
    private static readonly PromotionSettings Settings = PromotionSettingsService.Defaults with { FundamentalSubjectIds = [1, 2] };

    private static SubjectFinal Subject(int id, string area, decimal? final, decimal hours = 4m, bool lostLastYear = false) =>
        new(id, $"Asignatura {id}", area, hours, final, lostLastYear);

    private static PromotionOutcome Evaluate(
        byte grade, IReadOnlyList<SubjectFinal> subjects, bool piar = false, bool preschool = false, decimal attendance = 100m) =>
        PromotionRules.Evaluate(new PromotionInput(grade, preschool, piar, subjects, attendance), Settings);

    [Fact]
    public void Year_final_weighs_the_three_periods()
    {
        Assert.Equal(3.70m, PromotionRules.YearFinal([(30m, 3.8m), (30m, 3.2m), (40m, 4.0m)]));
        Assert.Null(PromotionRules.YearFinal([(30m, 3.8m), (30m, null), (40m, 4.0m)]));
    }

    [Fact]
    public void Area_is_weighted_by_weekly_hours()
    {
        var areas = PromotionRules.Areas([Subject(1, "Naturales", 3.0m, 1m), Subject(2, "Naturales", 4.0m, 3m)]);

        var area = Assert.Single(areas);
        Assert.Equal(3.75m, area.Final);
        Assert.True(area.Passed);
    }

    [Fact]
    public void Preschool_is_always_promoted()
        => Assert.Equal(PromotionRules.Promoted, Evaluate(0, [], preschool: true).Result);

    [Fact]
    public void Missing_final_grade_leaves_the_student_pending()
        => Assert.Equal(PromotionRules.Pending, Evaluate(6, [Subject(1, "A", 4m), Subject(2, "B", null)]).Result);

    [Fact]
    public void First_and_second_grade_only_need_the_fundamental_subjects()
    {
        Assert.Equal(PromotionRules.Promoted, Evaluate(2, [Subject(1, "A", 3.6m), Subject(2, "B", 3.5m), Subject(3, "C", 2.0m)]).Result);
        Assert.Equal(PromotionRules.Failed, Evaluate(1, [Subject(1, "A", 3.4m), Subject(2, "B", 4.5m), Subject(3, "C", 5.0m)]).Result);
        Assert.Equal(PromotionRules.Pending, Evaluate(1, [Subject(8, "A", 4m), Subject(9, "B", 4m)]).Result);
    }

    [Fact]
    public void All_areas_passed_promotes()
        => Assert.Equal(PromotionRules.Promoted, Evaluate(7, [Subject(1, "A", 3.5m), Subject(2, "B", 4.2m)]).Result);

    [Fact]
    public void One_lost_subject_can_be_promoted_by_exception()
    {
        var subjects = new[] { Subject(1, "A", 3.3m), Subject(2, "B", 4.2m), Subject(3, "C", 4.0m) };

        var outcome = Evaluate(7, subjects);

        Assert.Equal(PromotionRules.Promoted, outcome.Result);
        Assert.True(outcome.UsedException);
    }

    [Theory]
    [InlineData(3.1, 4.2, 100, 7, false)]
    [InlineData(3.3, 3.9, 100, 7, false)]
    [InlineData(3.3, 4.2, 89, 7, false)]
    [InlineData(3.3, 4.2, 100, 5, false)]
    [InlineData(3.3, 4.2, 100, 7, true)]
    public void Exception_fails_when_a_condition_is_not_met(double lost, double rest, double attendance, byte grade, bool lostLastYear)
    {
        var subjects = new[]
        {
            Subject(1, "A", (decimal)lost, lostLastYear: lostLastYear),
            Subject(2, "B", (decimal)rest),
            Subject(3, "C", (decimal)rest),
        };

        Assert.Equal(PromotionRules.Failed, Evaluate(grade, subjects, attendance: (decimal)attendance).Result);
    }

    [Fact]
    public void Two_lost_areas_fail()
        => Assert.Equal(PromotionRules.Failed, Evaluate(8, [Subject(1, "A", 3.3m), Subject(2, "B", 3.3m), Subject(3, "C", 5m)]).Result);

    [Fact]
    public void Eleventh_grade_needs_every_subject()
    {
        var subjects = new[] { Subject(1, "Naturales", 3.4m, 1m), Subject(2, "Naturales", 4.5m, 3m), Subject(3, "C", 4.5m) };

        Assert.Equal(PromotionRules.Failed, Evaluate(11, subjects).Result);
        Assert.Equal(PromotionRules.Promoted, Evaluate(10, subjects).Result);
    }

    [Fact]
    public void Piar_student_fails_only_with_three_lost_areas()
    {
        var two = new[] { Subject(1, "A", 2m), Subject(2, "B", 2m), Subject(3, "C", 4m) };
        var three = new[] { Subject(1, "A", 2m), Subject(2, "B", 2m), Subject(3, "C", 2m) };

        Assert.Equal(PromotionRules.Promoted, Evaluate(6, two, piar: true).Result);
        Assert.Equal(PromotionRules.Failed, Evaluate(6, three, piar: true).Result);
    }

    [Theory]
    [InlineData(8, true)]
    [InlineData(9, false)]
    [InlineData(10, false)]
    [InlineData(11, false)]
    public void Early_promotion_excludes_ninth_grade_and_media(byte grade, bool allowed)
        => Assert.Equal(allowed, PromotionRules.AllowsEarlyPromotion(grade));
}

using Litaro.Models;

namespace Litaro.Services;

public enum GradingMode { Weighted, Average }

public record HeteroScore(decimal? Weight, decimal? Raw);

public record PeriodScores(GradingMode Mode, IReadOnlyList<HeteroScore> Hetero, decimal? Saber, decimal? Self, decimal? Peer);

public record PeriodResult(decimal? Accumulated, decimal? Final, bool Complete, bool AbsenceLoss, string? Performance, bool? Passing);

public static class GradingRules
{
    public const decimal MinRaw = 10m;
    public const decimal MaxRaw = 50m;
    public const decimal PassingGrade = 3.50m;
    public const decimal HeteroShare = 0.70m;
    public const decimal ComponentShare = 0.10m;
    public const decimal FullWeight = 100m;

    public const string Low = "LOW";
    public const string Basic = "BASIC";
    public const string High = "HIGH";
    public const string Superior = "SUPERIOR";

    public static bool IsValidRaw(decimal raw) =>
        raw >= MinRaw && raw <= MaxRaw && decimal.Round(raw, 1) == raw;

    public static decimal Round(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    public static string Performance(decimal grade) => grade switch
    {
        < PassingGrade => Low,
        < 4.00m => Basic,
        < 4.50m => High,
        _ => Superior,
    };

    public static bool Passes(decimal grade) => grade >= PassingGrade;

    public static decimal WeightSum(IEnumerable<HeteroScore> hetero) =>
        hetero.Sum(item => item.Weight ?? 0m);

    public static PeriodResult Compute(PeriodScores scores, decimal? absenceLossGrade)
    {
        var heteroAccumulated = HeteroAccumulated(scores);

        var components = new List<(decimal Share, decimal Value)>();

        if (heteroAccumulated is { } hetero)
            components.Add((HeteroShare, hetero));

        foreach (var raw in new[] { scores.Saber, scores.Self, scores.Peer })
        {
            if (raw is { } value)
                components.Add((ComponentShare, Scale(value)));
        }

        decimal? accumulated = components.Count == 0
            ? null
            : Round(components.Sum(c => c.Share * c.Value) / components.Sum(c => c.Share));

        var complete = IsComplete(scores);

        decimal? final = absenceLossGrade is { } loss
            ? Round(loss)
            : complete
                ? Round(HeteroShare * HeteroFinal(scores) + ComponentShare * (Scale(scores.Saber!.Value) + Scale(scores.Self!.Value) + Scale(scores.Peer!.Value)))
                : null;

        var shown = final ?? accumulated;

        return new PeriodResult(
            accumulated,
            final,
            complete,
            absenceLossGrade is not null,
            shown is { } grade ? Performance(grade) : null,
            shown is { } passing ? Passes(passing) : null);
    }

    public static bool IsDesignLocked(DateTime? lockAtUtc, DateTime nowUtc) =>
        lockAtUtc is { } lockAt && nowUtc >= lockAt;

    public static bool IsPeriodClosed(AcademicPeriod period, DateOnly today) =>
        today > AttendancePeriods.End(period);

    private static decimal Scale(decimal raw) => raw / 10m;

    private static decimal? HeteroAccumulated(PeriodScores scores)
    {
        if (scores.Mode == GradingMode.Weighted)
        {
            var graded = scores.Hetero.Where(h => h.Raw is not null && (h.Weight ?? 0m) > 0m).ToList();
            var weight = graded.Sum(h => h.Weight!.Value);

            return weight == 0m ? null : graded.Sum(h => h.Weight!.Value * Scale(h.Raw!.Value)) / weight;
        }

        var values = scores.Hetero.Where(h => h.Raw is not null).Select(h => Scale(h.Raw!.Value)).ToList();

        return values.Count == 0 ? null : values.Average();
    }

    private static bool IsComplete(PeriodScores scores)
    {
        if (scores.Hetero.Count == 0 || scores.Hetero.Any(h => h.Raw is null))
            return false;

        if (scores.Saber is null || scores.Self is null || scores.Peer is null)
            return false;

        return scores.Mode == GradingMode.Average ||
               (scores.Hetero.All(h => (h.Weight ?? 0m) > 0m) && WeightSum(scores.Hetero) == FullWeight);
    }

    private static decimal HeteroFinal(PeriodScores scores) =>
        scores.Mode == GradingMode.Weighted
            ? scores.Hetero.Sum(h => h.Weight!.Value * Scale(h.Raw!.Value)) / FullWeight
            : scores.Hetero.Average(h => Scale(h.Raw!.Value));
}

using Litaro.Services;

namespace Litaro.Tests;

public class SimatDataDiffTests
{
    [Theory]
    [InlineData(null, "SURA", "SURA", false)]
    [InlineData("SURA", null, "SURA", false)]
    [InlineData("SURA", "  ", "SURA", false)]
    [InlineData(null, null, null, false)]
    [InlineData("Nueva EPS", " NUEVA  EPS ", "Nueva EPS", false)]
    [InlineData("Compensar E.P.S.", "COMPENSÁR E.P.S.", "Compensar E.P.S.", false)]
    [InlineData("SURA", "NUEVA EPS", "SURA", true)]
    public void Resolve_keeps_litaro_and_reports_only_real_differences(string? litaro, string? simat, string? expected, bool differs)
    {
        var (value, diff) = SimatDataDiff.Resolve(SimatDataDiff.EpsField, litaro, simat);

        Assert.Equal(expected, value);
        Assert.Equal(differs, diff is not null);

        if (differs)
            Assert.Equal(new SimatFieldDiff("EPS", litaro!, simat!.Trim()), diff);
    }

    [Fact]
    public void Describe_names_each_field_with_both_values()
    {
        SimatFieldDiff[] diffs =
        [
            new(SimatDataDiff.EpsField, "SURA", "NUEVA EPS"),
            new(SimatDataDiff.BloodTypeField, "O+", "A+"),
        ];

        Assert.Equal(
            "EPS: LITARO «SURA», SIMAT «NUEVA EPS»; Tipo de sangre: LITARO «O+», SIMAT «A+»",
            SimatDataDiff.Describe(diffs));
    }

    [Fact]
    public void DedupKey_changes_only_when_a_value_changes()
    {
        SimatFieldDiff[] diffs = [new(SimatDataDiff.EpsField, "SURA", "NUEVA EPS")];
        SimatFieldDiff[] same = [new(SimatDataDiff.EpsField, "sura", " NUEVA EPS")];
        SimatFieldDiff[] other = [new(SimatDataDiff.EpsField, "SURA", "COMPENSAR")];

        var key = SimatDataDiff.DedupKey(42, 2027, diffs);

        Assert.StartsWith("SIMAT_DATA_MISMATCH:42:2027:", key);
        Assert.True(key.Length <= 150);
        Assert.Equal(key, SimatDataDiff.DedupKey(42, 2027, same));
        Assert.NotEqual(key, SimatDataDiff.DedupKey(42, 2027, other));
        Assert.NotEqual(key, SimatDataDiff.DedupKey(43, 2027, diffs));
    }
}

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Litaro.Models;

namespace Litaro.Services;

public record SimatFieldDiff(string Field, string Litaro, string Simat);

public static class SimatDataDiff
{
    public const string EpsField = "EPS";
    public const string BloodTypeField = "Tipo de sangre";

    public static (string? Value, SimatFieldDiff? Diff) Resolve(string field, string? litaro, string? simat)
    {
        if (string.IsNullOrWhiteSpace(simat))
            return (litaro, null);

        var simatValue = simat.Trim();

        if (string.IsNullOrWhiteSpace(litaro))
            return (simatValue, null);

        return Normalize(litaro) == Normalize(simatValue)
            ? (litaro, null)
            : (litaro, new SimatFieldDiff(field, litaro, simatValue));
    }

    public static string Describe(IReadOnlyCollection<SimatFieldDiff> diffs) =>
        string.Join("; ", diffs.Select(d => $"{d.Field}: LITARO «{d.Litaro}», SIMAT «{d.Simat}»"));

    public static string DedupKey(int studentId, short yearId, IReadOnlyCollection<SimatFieldDiff> diffs)
    {
        var text = string.Join("|", diffs
            .OrderBy(d => d.Field, StringComparer.Ordinal)
            .Select(d => $"{d.Field}={Normalize(d.Litaro)}>{Normalize(d.Simat)}"));

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16];

        return $"{NotificationCodes.SimatDataMismatch}:{studentId}:{yearId}:{hash}";
    }

    private static string Normalize(string value)
    {
        var decomposed = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark || char.IsWhiteSpace(c))
                continue;

            builder.Append(char.ToUpperInvariant(c));
        }

        return builder.ToString();
    }
}

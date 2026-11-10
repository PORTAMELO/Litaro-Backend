using System.Text.RegularExpressions;

namespace Litaro.Services;

public static partial class NotificationTemplate
{
    public const int MaxLength = 500;

    public static string Render(string template, IReadOnlyDictionary<string, string> values)
    {
        var text = Placeholder()
            .Replace(template ?? string.Empty, match => values.TryGetValue(match.Groups[1].Value, out var value) ? value : string.Empty)
            .Trim();

        return text.Length <= MaxLength ? text : text[..MaxLength];
    }

    [GeneratedRegex(@"\{(\w+)\}")]
    private static partial Regex Placeholder();
}

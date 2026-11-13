namespace Litaro.Services;

public record DocumentTypeRule(
    string Code,
    string Module,
    string EntityTable,
    string[] Extensions,
    string[] ContentTypes,
    byte[] Signature,
    long MaxBytes,
    string FormatMessage,
    string SizeMessage);

public static class DocumentTypes
{
    public const string StudyPlanner = "STUDY_PLANNER";

    private static readonly Dictionary<string, DocumentTypeRule> Rules = new()
    {
        [StudyPlanner] = new DocumentTypeRule(
            StudyPlanner,
            "Grades",
            "AcademicAssignment",
            [".pdf"],
            ["application/pdf"],
            "%PDF-"u8.ToArray(),
            10_485_760,
            "El archivo debe ser PDF.",
            "El archivo supera el tamaño máximo de 10 MB."),
    };

    public static DocumentTypeRule Get(string code) =>
        Rules.TryGetValue(code, out var rule)
            ? rule
            : throw new InvalidOperationException("Tipo de documento no válido.");
}

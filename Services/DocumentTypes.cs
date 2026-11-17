namespace Litaro.Services;

public record DocumentFormat(string[] Extensions, string ContentType, byte[] Signature);

public record DocumentTypeRule(
    string Code,
    string Module,
    string EntityTable,
    DocumentFormat[] Formats,
    long MaxBytes,
    string FormatMessage,
    string SizeMessage);

public static class DocumentTypes
{
    public const string StudyPlanner = "STUDY_PLANNER";
    public const string EnrollmentPrefix = "ENROLLMENT_DOC:";
    public const string EnrollmentModule = "Enrollment";
    public const string AdmissionTable = "AdmissionRequest";
    public const string StudentTable = "Student";

    private const long MaxBytes = 10_485_760;
    private const string SizeMessage = "El archivo supera el tamaño máximo de 10 MB.";

    public static readonly DocumentFormat Pdf = new([".pdf"], "application/pdf", "%PDF-"u8.ToArray());
    public static readonly DocumentFormat Jpeg = new([".jpg", ".jpeg"], "image/jpeg", [0xFF, 0xD8, 0xFF]);
    public static readonly DocumentFormat Png = new([".png"], "image/png", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

    private static readonly Dictionary<string, DocumentTypeRule> Rules = new()
    {
        [StudyPlanner] = new DocumentTypeRule(
            StudyPlanner,
            "Grades",
            "AcademicAssignment",
            [Pdf],
            MaxBytes,
            "El archivo debe ser PDF.",
            SizeMessage),
    };

    public static DocumentTypeRule Get(string code) =>
        Rules.TryGetValue(code, out var rule)
            ? rule
            : throw new InvalidOperationException("Tipo de documento no válido.");

    public static DocumentTypeRule Enrollment(int detailId, string entityTable) =>
        new(
            $"{EnrollmentPrefix}{detailId}",
            EnrollmentModule,
            entityTable,
            [Pdf, Jpeg, Png],
            MaxBytes,
            "El archivo debe ser PDF, JPG o PNG.",
            SizeMessage);

    public static int? EnrollmentDetailId(string documentType) =>
        documentType.StartsWith(EnrollmentPrefix, StringComparison.Ordinal) &&
        int.TryParse(documentType[EnrollmentPrefix.Length..], out var id)
            ? id
            : null;
}

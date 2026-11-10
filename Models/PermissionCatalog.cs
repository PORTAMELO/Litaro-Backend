namespace Litaro.Models;

public static class ProcessKeys
{
    public const string AdministrationModule = "AdministrationModule";
    public const string EnrollmentProcess = "EnrollmentProcess";
    public const string AdmissionDecision = "AdmissionDecision";
    public const string MySchedule = "MySchedule";
    public const string GradesModule = "GradesModule";
    public const string ObservationsModule = "ObservationsModule";
    public const string StatisticsModule = "StatisticsModule";
    public const string ReportsModule = "ReportsModule";
    public const string CommunicationsModule = "CommunicationsModule";
    public const string ForumModule = "ForumModule";
    public const string SecurityModule = "SecurityModule";
    public const string WebpageModule = "WebpageModule";
}

public static class PermissionCatalog
{
    public const string TableKind = "table";
    public const string ProcessKind = "process";
    public const string DeniedMessage = "No tienes permiso para realizar esta acción.";

    private static readonly Dictionary<string, string> RoleAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Acudiente"] = "Padre",
    };

    public static readonly IReadOnlyList<string> Tables =
    [
        "School", "Campus", "AcademicYear", "AcademicPeriod",
        "Characteristic", "CharacteristicDetail",
        "User", "Student", "Parent", "Teacher",
        "Grade", "Classroom", "Subject", "AcademicAssignment", "Schedule",
        "Enrollment", "GradeScore", "Attendance", "StudentLog",
        "Space", "StudyPlan", "TeacherAvailability",
    ];

    public static readonly IReadOnlyList<string> Processes =
    [
        ProcessKeys.AdministrationModule,
        ProcessKeys.EnrollmentProcess,
        ProcessKeys.AdmissionDecision,
        ProcessKeys.MySchedule,
        ProcessKeys.GradesModule,
        ProcessKeys.ObservationsModule,
        ProcessKeys.StatisticsModule,
        ProcessKeys.ReportsModule,
        ProcessKeys.CommunicationsModule,
        ProcessKeys.ForumModule,
        ProcessKeys.SecurityModule,
        ProcessKeys.WebpageModule,
    ];

    public static bool IsManaged(string key) => Tables.Contains(key) || Processes.Contains(key);

    public static string KindOf(string key) => Processes.Contains(key) ? ProcessKind : TableKind;

    public static IReadOnlyList<string> ResolveActiveRoles(IReadOnlyCollection<string> userRoles, string? requestedRole)
    {
        if (string.IsNullOrWhiteSpace(requestedRole))
            return userRoles.ToList();

        var requested = requestedRole.Trim();
        var owned = Find(userRoles, requested);

        if (owned is null && RoleAliases.TryGetValue(requested, out var alias))
            owned = Find(userRoles, alias);

        return owned is null ? [] : [owned];
    }

    private static string? Find(IEnumerable<string> roles, string name) =>
        roles.FirstOrDefault(role => string.Equals(role, name, StringComparison.OrdinalIgnoreCase));
}

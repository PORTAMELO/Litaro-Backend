using System.Security.Claims;
using Litaro.Models;
using Litaro.Services;

namespace Litaro.Middleware;

public class TablePermissionMiddleware(RequestDelegate next)
{

    private static readonly Dictionary<string, string> RouteToTable = new(StringComparer.OrdinalIgnoreCase)
    {
        ["schools"] = "School",
        ["campus"] = "Campus",
        ["academic-years"] = "AcademicYear",
        ["academic-periods"] = "AcademicPeriod",
        ["users"] = "User",
        ["students"] = "Student",
        ["parents"] = "Parent",
        ["teachers"] = "Teacher",
        ["parent-students"] = "ParentStudent",
        ["grades"] = "Grade",
        ["classrooms"] = "Classroom",
        ["subjects"] = "Subject",
        ["academic-assignments"] = "AcademicAssignment",
        ["schedules"] = "Schedule",
        ["spaces"] = "Space",
        ["study-plans"] = "StudyPlan",
        ["teacher-availabilities"] = "TeacherAvailability",
        ["enrollments"] = "Enrollment",
        ["grade-scores"] = "GradeScore",
        ["attendances"] = "Attendance",
        ["student-logs"] = "StudentLog",
        ["roles"] = "Role",
    };

    public async Task InvokeAsync(HttpContext context, PermissionService permissions)
    {
        var segments = context.Request.Path.Value?
            .Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments is { Length: > 0 } &&
            RouteToTable.TryGetValue(segments[0], out var tableName) &&
            GetRequiredFlag(context.Request.Method) is { } required)
        {
            var roles = context.User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();

            if (!await permissions.HasPermissionAsync(roles, tableName, required))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new
                {
                    message = "No tienes permiso para realizar esta acción."
                });
                return;
            }
        }

        await next(context);
    }

    private static PermissionFlags? GetRequiredFlag(string method) => method.ToUpperInvariant() switch
    {
        "GET" or "HEAD" => PermissionFlags.Read,
        "POST" => PermissionFlags.Create,
        "PUT" or "PATCH" => PermissionFlags.Update,
        "DELETE" => PermissionFlags.Delete,
        _ => null
    };
}
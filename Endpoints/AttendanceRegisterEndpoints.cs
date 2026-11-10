using System.Security.Claims;
using Litaro.Models;
using Litaro.Services;

namespace Litaro.Endpoints;

public static class AttendanceRegisterEndpoints
{
    public record RegisterLateArrivalRequest(int EnrollmentId);

    private static readonly (string Key, PermissionFlags Flag)[] AnyAttendanceRead =
    [
        (ProcessKeys.AttendanceRoll, PermissionFlags.Read),
        (ProcessKeys.AttendanceSummary, PermissionFlags.Read),
        (ProcessKeys.LateArrival, PermissionFlags.Read),
        (ProcessKeys.AttendanceExcuse, PermissionFlags.Read),
        (ProcessKeys.AttendanceSettings, PermissionFlags.Read),
    ];

    public static void MapAttendanceRegisterEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/attendance-register");

        group.MapGet("/options", (HttpContext http, AttendanceOptionsService svc) =>
                Guard(async () =>
                {
                    if (CurrentUserId(http.User) is not { } userId)
                        return Results.Unauthorized();

                    return Results.Ok(await svc.GetAsync(userId, await CanEditAnyAsync(http), Today()));
                }))
            .RequireAnyPermission(AnyAttendanceRead);

        group.MapGet("/agenda", (DateOnly date, int? classroomId, HttpContext http, AttendanceRollService svc) =>
                Guard(async () =>
                    await AccessAsync(http) is { } access
                        ? Results.Ok(await svc.GetAgendaAsync(access, date, classroomId))
                        : Results.Unauthorized()))
            .RequirePermission(ProcessKeys.AttendanceRoll, PermissionFlags.Read);

        group.MapGet("/sheet", (int classroomId, DateOnly date, TimeOnly? startTime, HttpContext http, AttendanceRollService svc) =>
                Guard(async () =>
                    await AccessAsync(http) is { } access
                        ? Results.Ok(await svc.GetSheetAsync(access, classroomId, date, startTime, Today()))
                        : Results.Unauthorized()))
            .RequirePermission(ProcessKeys.AttendanceRoll, PermissionFlags.Read);

        group.MapPut("/sheet", (SaveSheetRequest request, HttpContext http, AttendanceRollService svc) =>
                Guard(async () =>
                    await AccessAsync(http) is { } access
                        ? Results.Ok(await svc.SaveSheetAsync(access, request, Today()))
                        : Results.Unauthorized()))
            .RequireAnyPermission(
                (ProcessKeys.AttendanceRoll, PermissionFlags.Create),
                (ProcessKeys.AttendanceRoll, PermissionFlags.Update));

        group.MapGet("/late-arrivals", (DateOnly? date, LateArrivalService svc) =>
                Guard(async () => Results.Ok(await svc.ListAsync(date ?? Today()))))
            .RequirePermission(ProcessKeys.LateArrival, PermissionFlags.Read);

        group.MapPost("/late-arrivals", (RegisterLateArrivalRequest request, ClaimsPrincipal user, LateArrivalService svc) =>
                Guard(async () =>
                {
                    if (CurrentUserId(user) is not { } userId)
                        return Results.Unauthorized();

                    var today = Today();
                    return Results.Ok(await svc.RegisterAsync(request.EnrollmentId, today, userId, today));
                }))
            .RequirePermission(ProcessKeys.LateArrival, PermissionFlags.Create);

        group.MapDelete("/late-arrivals/{id:int}", (int id, LateArrivalService svc) =>
                Guard(async () => await svc.DeleteAsync(id) ? Results.NoContent() : Results.NotFound()))
            .RequirePermission(ProcessKeys.LateArrival, PermissionFlags.Delete);

        group.MapGet("/students/search", (string? q, LateArrivalService svc) =>
                Guard(async () => Results.Ok(await svc.SearchStudentsAsync(q))))
            .RequireAnyPermission(
                (ProcessKeys.LateArrival, PermissionFlags.Read),
                (ProcessKeys.AttendanceExcuse, PermissionFlags.Read));

        group.MapGet("/excuses", (short? yearId, string? q, AttendanceExcuseService svc) =>
                Guard(async () => Results.Ok(await svc.ListAsync(yearId, q))))
            .RequirePermission(ProcessKeys.AttendanceExcuse, PermissionFlags.Read);

        group.MapPost("/excuses/preview", (SaveExcuseRequest request, AttendanceExcuseService svc) =>
                Guard(async () => Results.Ok(await svc.PreviewAsync(request, Today()))))
            .RequirePermission(ProcessKeys.AttendanceExcuse, PermissionFlags.Create);

        group.MapPost("/excuses", (SaveExcuseRequest request, ClaimsPrincipal user, AttendanceExcuseService svc) =>
                Guard(async () =>
                {
                    if (CurrentUserId(user) is not { } userId)
                        return Results.Unauthorized();

                    var id = await svc.CreateAsync(request, userId, Today());
                    return Results.Ok(new { attendanceExcuseId = id });
                }))
            .RequirePermission(ProcessKeys.AttendanceExcuse, PermissionFlags.Create);

        group.MapDelete("/excuses/{id:int}", (int id, AttendanceExcuseService svc) =>
                Guard(async () =>
                {
                    await svc.DeleteAsync(id);
                    return Results.NoContent();
                }))
            .RequirePermission(ProcessKeys.AttendanceExcuse, PermissionFlags.Delete);

        group.MapGet("/summary", (
                short periodId,
                int classroomId,
                int subjectId,
                HttpContext http,
                AttendanceCalculationService calculation,
                AttendanceOptionsService options) =>
                Guard(async () =>
                {
                    if (CurrentUserId(http.User) is not { } userId)
                        return Results.Unauthorized();

                    var restricted = !await CanEditAnyAsync(http) && await options.IsTeacherAsync(userId);

                    if (restricted && !await calculation.TeachesAsync(userId, classroomId, subjectId))
                        throw new UnauthorizedAccessException();

                    return Results.Ok(await calculation.GetSummaryAsync(periodId, classroomId, subjectId));
                }))
            .RequirePermission(ProcessKeys.AttendanceSummary, PermissionFlags.Read);

        group.MapGet("/settings", (AttendanceSettingsService svc) =>
                Guard(async () => Results.Ok(new
                {
                    settings = await svc.GetAsync(),
                    gradeLevels = await svc.GetGradeLevelsAsync(),
                })))
            .RequirePermission(ProcessKeys.AttendanceSettings, PermissionFlags.Read);

        group.MapPut("/settings", (AttendanceSettings request, AttendanceSettingsService svc) =>
                Guard(async () => Results.Ok(await svc.SaveAsync(request))))
            .RequirePermission(ProcessKeys.AttendanceSettings, PermissionFlags.Update);
    }

    private static async Task<IResult> Guard(Func<Task<IResult>> action)
    {
        try
        {
            return await action();
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Json(new { message = PermissionCatalog.DeniedMessage }, statusCode: StatusCodes.Status403Forbidden);
        }
    }

    private static DateOnly Today() => EnrollmentPeriodService.Today();

    private static Task<bool> CanEditAnyAsync(HttpContext http) =>
        PermissionFilters.HasAnyAsync(http, (ProcessKeys.AttendanceRoll, PermissionFlags.Update));

    private static async Task<RollAccess?> AccessAsync(HttpContext http)
    {
        if (CurrentUserId(http.User) is not { } userId)
            return null;

        var canCreate = await PermissionFilters.HasAnyAsync(http, (ProcessKeys.AttendanceRoll, PermissionFlags.Create));

        return new RollAccess(userId, canCreate, await CanEditAnyAsync(http));
    }

    private static int? CurrentUserId(ClaimsPrincipal user) =>
        int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}

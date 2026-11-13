using System.Security.Claims;
using Litaro.Models;
using Litaro.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Endpoints;

public static class GradingEndpoints
{
    private const long UploadLimitBytes = 11 * 1024 * 1024;
    private const string ClientHeader = "X-Requested-With";
    private const string ClientHeaderValue = "XMLHttpRequest";

    private static readonly PermissionFlags[] Flags =
        [PermissionFlags.Read, PermissionFlags.Create, PermissionFlags.Update, PermissionFlags.Delete];

    private static readonly (string Key, PermissionFlags Flag)[] AnyGrading =
    [
        (ProcessKeys.GradeBook, PermissionFlags.Read),
        (ProcessKeys.GradeBook, PermissionFlags.Update),
        (ProcessKeys.GradeDesign, PermissionFlags.Create),
        (ProcessKeys.GradeDesign, PermissionFlags.Update),
        (ProcessKeys.StudyPlanner, PermissionFlags.Read),
        (ProcessKeys.StudyPlanner, PermissionFlags.Create),
        (ProcessKeys.StudyPlanner, PermissionFlags.Update),
        (ProcessKeys.StudyPlannerPanel, PermissionFlags.Read),
        (ProcessKeys.GradeSettings, PermissionFlags.Read),
    ];

    public static void MapGradingEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/grading");

        group.MapGet("/options", (HttpContext http, GradebookService svc) =>
                Guard(async () =>
                    await AccessAsync(http) is { } access
                        ? Results.Ok(await svc.GetOptionsAsync(access, DateTime.UtcNow, Today()))
                        : Results.Unauthorized()))
            .RequireAnyPermission(AnyGrading);

        group.MapGet("/sheet", (int classroomId, int subjectId, short periodId, HttpContext http, GradebookService svc) =>
                Guard(async () =>
                    await AccessAsync(http) is { } access
                        ? Results.Ok(await svc.GetSheetAsync(access, classroomId, subjectId, periodId, DateTime.UtcNow, Today()))
                        : Results.Unauthorized()))
            .RequireAnyPermission(
                (ProcessKeys.GradeBook, PermissionFlags.Read),
                (ProcessKeys.GradeBook, PermissionFlags.Update));

        group.MapPut("/sheet/mode", (SetModeRequest request, HttpContext http, GradebookService svc) =>
                Guard(async () =>
                    await AccessAsync(http) is { } access
                        ? Results.Ok(await svc.SetModeAsync(access, request, DateTime.UtcNow, Today()))
                        : Results.Unauthorized()))
            .RequireAnyPermission(DesignWrite);

        group.MapPost("/activities", (SaveActivityRequest request, HttpContext http, GradebookService svc) =>
                Guard(async () =>
                    await AccessAsync(http) is { } access
                        ? Results.Ok(await svc.SaveActivityAsync(access, request with { ActivityId = null }, DateTime.UtcNow, Today()))
                        : Results.Unauthorized()))
            .RequireAnyPermission(DesignWrite);

        group.MapPut("/activities/{id:int}", (int id, SaveActivityRequest request, HttpContext http, GradebookService svc) =>
                Guard(async () =>
                    await AccessAsync(http) is { } access
                        ? Results.Ok(await svc.SaveActivityAsync(access, request with { ActivityId = id }, DateTime.UtcNow, Today()))
                        : Results.Unauthorized()))
            .RequireAnyPermission(DesignWrite);

        group.MapDelete("/activities/{id:int}", (int id, HttpContext http, GradebookService svc) =>
                Guard(async () =>
                    await AccessAsync(http) is { } access
                        ? Results.Ok(await svc.DeleteActivityAsync(access, id, DateTime.UtcNow, Today()))
                        : Results.Unauthorized()))
            .RequireAnyPermission(
                (ProcessKeys.GradeDesign, PermissionFlags.Delete),
                (ProcessKeys.GradeDesign, PermissionFlags.Update));

        group.MapPut("/scores", (SaveScoresRequest request, HttpContext http, GradebookService svc) =>
                Guard(async () =>
                    await AccessAsync(http) is { } access
                        ? Results.Ok(await svc.SaveScoresAsync(access, request, DateTime.UtcNow, Today()))
                        : Results.Unauthorized()))
            .RequireAnyPermission(
                (ProcessKeys.GradeBook, PermissionFlags.Create),
                (ProcessKeys.GradeBook, PermissionFlags.Update));

        group.MapGet("/planners", (HttpContext http, StudyPlannerService svc) =>
                Guard(async () =>
                    await AccessAsync(http) is { } access
                        ? Results.Ok(await svc.ListOwnAsync(access, DateTime.UtcNow))
                        : Results.Unauthorized()))
            .RequireAnyPermission(
                (ProcessKeys.StudyPlanner, PermissionFlags.Read),
                (ProcessKeys.StudyPlanner, PermissionFlags.Create),
                (ProcessKeys.StudyPlanner, PermissionFlags.Update));

        group.MapGet("/planners/panel", (HttpContext http, StudyPlannerService svc) =>
                Guard(async () =>
                    await AccessAsync(http) is { } access
                        ? Results.Ok(await svc.PanelAsync(access, DateTime.UtcNow))
                        : Results.Unauthorized()))
            .RequirePermission(ProcessKeys.StudyPlannerPanel, PermissionFlags.Read);

        group.MapPost("/planners/{assignmentId:int}", (int assignmentId, IFormFile file, HttpContext http, StudyPlannerService svc) =>
                Guard(async () =>
                {
                    if (await AccessAsync(http) is not { } access)
                        return Results.Unauthorized();

                    var upload = new DocumentUpload(file.FileName, file.ContentType, file.Length, file.OpenReadStream);
                    return Results.Ok(await svc.UploadAsync(access, assignmentId, upload, DateTime.UtcNow));
                }))
            .RequireAnyPermission(
                (ProcessKeys.StudyPlanner, PermissionFlags.Create),
                (ProcessKeys.StudyPlanner, PermissionFlags.Update))
            .RequireClientHeader()
            .WithMetadata(new RequestSizeLimitAttribute(UploadLimitBytes))
            .DisableAntiforgery();

        group.MapDelete("/planners/{assignmentId:int}", (int assignmentId, HttpContext http, StudyPlannerService svc) =>
                Guard(async () =>
                {
                    if (await AccessAsync(http) is not { } access)
                        return Results.Unauthorized();

                    await svc.DeleteAsync(access, assignmentId, DateTime.UtcNow);
                    return Results.NoContent();
                }))
            .RequireAnyPermission(
                (ProcessKeys.StudyPlanner, PermissionFlags.Create),
                (ProcessKeys.StudyPlanner, PermissionFlags.Update));

        group.MapGet("/planners/{assignmentId:int}/file", (int assignmentId, HttpContext http, StudyPlannerService svc) =>
                Guard(async () =>
                {
                    if (await AccessAsync(http) is not { } access)
                        return Results.Unauthorized();

                    var panelRead = await PermissionFilters.HasAnyAsync(http, (ProcessKeys.StudyPlannerPanel, PermissionFlags.Read));
                    var (document, content) = await svc.OpenAsync(access, panelRead, assignmentId);
                    return Results.File(content, document.ContentType, document.FileName);
                }))
            .RequireAuthorization();

        group.MapPost("/planners/{assignmentId:int}/request", (int assignmentId, HttpContext http, StudyPlannerService svc) =>
                Guard(async () =>
                {
                    if (await AccessAsync(http) is not { } access)
                        return Results.Unauthorized();

                    await svc.RequestAsync(access, assignmentId, DateTime.UtcNow);
                    return Results.NoContent();
                }))
            .RequirePermission(ProcessKeys.StudyPlanner, PermissionFlags.Create)
            .RequireClientHeader();

        group.MapGet("/deadlines", (short yearId, GradingDeadlineService svc) =>
                Guard(async () => Results.Ok(await svc.GetAsync(yearId))))
            .RequirePermission(ProcessKeys.GradeSettings, PermissionFlags.Read);

        group.MapPut("/deadlines", (DeadlinesDto request, ClaimsPrincipal user, GradingDeadlineService svc) =>
                Guard(async () =>
                    CurrentUserId(user) is { } userId
                        ? Results.Ok(await svc.SaveAsync(request, userId, DateTime.UtcNow))
                        : Results.Unauthorized()))
            .RequirePermission(ProcessKeys.GradeSettings, PermissionFlags.Update);
    }

    private static readonly (string Key, PermissionFlags Flag)[] DesignWrite =
    [
        (ProcessKeys.GradeDesign, PermissionFlags.Create),
        (ProcessKeys.GradeDesign, PermissionFlags.Update),
    ];

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
        catch (DbUpdateException)
        {
            return Results.Conflict(new { message = "Otra persona modificó esta información al mismo tiempo. Intente de nuevo." });
        }
        catch (HttpRequestException)
        {
            return Results.BadRequest(new { message = "No fue posible comunicarse con el almacenamiento de documentos." });
        }
        catch (TaskCanceledException)
        {
            return Results.BadRequest(new { message = "No fue posible comunicarse con el almacenamiento de documentos." });
        }
    }

    private static RouteHandlerBuilder RequireClientHeader(this RouteHandlerBuilder builder) =>
        builder.AddEndpointFilter(async (context, next) =>
            context.HttpContext.Request.Headers[ClientHeader] == ClientHeaderValue
                ? await next(context)
                : Results.Json(new { message = PermissionCatalog.DeniedMessage }, statusCode: StatusCodes.Status403Forbidden));

    private static DateOnly Today() => EnrollmentPeriodService.Today();

    private static async Task<GradingAccess?> AccessAsync(HttpContext http)
    {
        if (CurrentUserId(http.User) is not { } userId)
            return null;

        return new GradingAccess(
            userId,
            await FlagsAsync(http, ProcessKeys.GradeBook),
            await FlagsAsync(http, ProcessKeys.GradeDesign),
            await FlagsAsync(http, ProcessKeys.StudyPlanner));
    }

    private static async Task<PermissionFlags> FlagsAsync(HttpContext http, string key)
    {
        var result = PermissionFlags.None;

        foreach (var flag in Flags)
        {
            if (await PermissionFilters.HasAnyAsync(http, (key, flag)))
                result |= flag;
        }

        return result;
    }

    private static int? CurrentUserId(ClaimsPrincipal user) =>
        int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}

using System.Security.Claims;
using Litaro.Models;
using Litaro.Services;
using Microsoft.AspNetCore.Mvc;

namespace Litaro.Endpoints;

public static class ObservationEndpoints
{
    private const long UploadLimitBytes = 11 * 1024 * 1024;

    private static readonly (string Key, PermissionFlags Flag)[] AnyRead =
    [
        (ProcessKeys.ObservationRecord, PermissionFlags.Read),
        (ProcessKeys.ObservationConsolidated, PermissionFlags.Read),
    ];

    private static readonly (string Key, PermissionFlags Flag)[] AnyCreate =
    [
        (ProcessKeys.ObservationRecord, PermissionFlags.Create),
        (ProcessKeys.ObservationConsolidated, PermissionFlags.Create),
    ];

    private static readonly (string Key, PermissionFlags Flag)[] AnyUpdate =
    [
        (ProcessKeys.ObservationRecord, PermissionFlags.Update),
        (ProcessKeys.ObservationConsolidated, PermissionFlags.Update),
    ];

    public static void MapObservationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/observations");

        group.MapGet("/options", (HttpContext http, ObservationService svc) =>
                Guard(http, async access => Results.Ok(await svc.GetOptionsAsync(access))))
            .RequireAnyPermission(AnyRead);

        group.MapGet("/students", (int? classroomId, string? q, HttpContext http, ObservationService svc) =>
                Guard(http, async access => Results.Ok(await svc.SearchStudentsAsync(access, classroomId, q))))
            .RequireAnyPermission(AnyRead);

        group.MapGet("/", ([AsParameters] ObservationQuery query, HttpContext http, ObservationService svc) =>
                Guard(http, async access => Results.Ok(await svc.ListAsync(access, query.ToFilter()))))
            .RequireAnyPermission(AnyRead);

        group.MapGet("/ranking", ([AsParameters] RankingFilter filter, ObservationConsolidatedService svc) =>
                Plain(async () => Results.Ok(await svc.RankingAsync(filter))))
            .RequirePermission(ProcessKeys.ObservationConsolidated, PermissionFlags.Read);

        group.MapGet("/students/{studentId:int}/consolidated", (int studentId, short? yearId, ObservationConsolidatedService svc) =>
                Plain(async () => Results.Ok(await svc.StudentAsync(studentId, yearId))))
            .RequirePermission(ProcessKeys.ObservationConsolidated, PermissionFlags.Read);

        group.MapGet("/settings", (ObservationSettingsService svc) =>
                Plain(async () => Results.Ok(await svc.GetAsync())))
            .RequirePermission(ProcessKeys.ObservationConsolidated, PermissionFlags.Read);

        group.MapPut("/settings", (ObservationSettings request, ObservationSettingsService svc) =>
                Plain(async () => Results.Ok(await svc.SaveAsync(request))))
            .RequirePermission(ProcessKeys.ObservationConsolidated, PermissionFlags.Update);

        group.MapGet("/{id:int}", (int id, HttpContext http, ObservationService svc) =>
                Guard(http, async access => Results.Ok(await svc.GetAsync(access, id))))
            .RequireAnyPermission(AnyRead);

        group.MapPost("/", (SaveObservationRequest request, HttpContext http, ObservationService svc) =>
                Guard(http, async access =>
                {
                    var id = await svc.CreateAsync(access, request, Today());
                    return Results.Created($"/observations/{id}", new { observationId = id });
                }))
            .RequireAnyPermission(AnyCreate);

        group.MapPut("/{id:int}", (int id, SaveObservationRequest request, HttpContext http, ObservationService svc) =>
                Guard(http, async access =>
                {
                    await svc.UpdateAsync(access, id, request, Today());
                    return Results.NoContent();
                }))
            .RequireAnyPermission(AnyUpdate);

        group.MapPut("/{id:int}/statement", (int id, SaveStatementRequest request, HttpContext http, ObservationService svc) =>
                Guard(http, async access =>
                {
                    await svc.SaveStatementAsync(access, id, request);
                    return Results.NoContent();
                }))
            .RequireAnyPermission(AnyUpdate);

        group.MapPost("/{id:int}/statement-file", (int id, IFormFile file, HttpContext http, ObservationService svc) =>
                Guard(http, async access =>
                {
                    var upload = new DocumentUpload(file.FileName, file.ContentType, file.Length, file.OpenReadStream);
                    return Results.Ok(await svc.UploadStatementFileAsync(access, id, upload, DateTime.UtcNow));
                }))
            .RequireAnyPermission(AnyUpdate)
            .RequireClientHeader()
            .WithMetadata(new RequestSizeLimitAttribute(UploadLimitBytes))
            .DisableAntiforgery();

        group.MapDelete("/{id:int}/statement-file", (int id, HttpContext http, ObservationService svc) =>
                Guard(http, async access =>
                {
                    await svc.RemoveStatementFileAsync(access, id, DateTime.UtcNow);
                    return Results.NoContent();
                }))
            .RequireAnyPermission(AnyUpdate);

        group.MapGet("/{id:int}/statement-file", (int id, HttpContext http, ObservationService svc) =>
                Guard(http, async access =>
                {
                    var (document, content) = await svc.OpenStatementFileAsync(access, id);
                    return Results.File(content, document.ContentType, document.FileName);
                }))
            .RequireAnyPermission(AnyRead);

        group.MapPost("/{id:int}/commitments", (int id, AddCommitmentRequest request, HttpContext http, ObservationService svc) =>
                Guard(http, async access =>
                {
                    var commitmentId = await svc.AddCommitmentAsync(access, id, request);
                    return Results.Ok(new { commitmentId });
                }))
            .RequireAnyPermission(AnyUpdate);

        group.MapPut("/commitments/{commitmentId:int}/status", (int commitmentId, SetCommitmentStatusRequest request, HttpContext http, ObservationService svc) =>
                Guard(http, async access =>
                {
                    await svc.SetCommitmentStatusAsync(access, commitmentId, request);
                    return Results.NoContent();
                }))
            .RequireAnyPermission(AnyUpdate);
    }

    public record ObservationQuery(
        short? YearId,
        int? ClassroomId,
        int? CampusId,
        int? GradeId,
        int? StudentId,
        string? Kind,
        string? CoexistenceType,
        string? Q)
    {
        public ObservationFilter ToFilter() => new(YearId, ClassroomId, CampusId, GradeId, StudentId, Kind, CoexistenceType, Q);
    }

    private static async Task<IResult> Guard(HttpContext http, Func<ObservationAccess, Task<IResult>> action)
    {
        try
        {
            if (await AccessAsync(http) is not { } access)
                return Results.Unauthorized();

            return await action(access);
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

    private static async Task<IResult> Plain(Func<Task<IResult>> action)
    {
        try
        {
            return await action();
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static async Task<ObservationAccess?> AccessAsync(HttpContext http)
    {
        if (CurrentUserId(http.User) is not { } userId)
            return null;

        var seesAll = await PermissionFilters.HasAnyAsync(http, (ProcessKeys.ObservationConsolidated, PermissionFlags.Read));
        return new ObservationAccess(userId, seesAll);
    }

    private static DateOnly Today() => EnrollmentPeriodService.Today();

    private static int? CurrentUserId(ClaimsPrincipal user) =>
        int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}

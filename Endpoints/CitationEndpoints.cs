using System.Security.Claims;
using Litaro.Models;
using Litaro.Services;

namespace Litaro.Endpoints;

public static class CitationEndpoints
{
    public record RescheduleCitationRequest(DateOnly Date, TimeOnly? Time, int? ScheduleId, string? Note);

    public record CitationQuery(
        int? StudentId,
        int? ParentId,
        int? ResponsibleUserId,
        DateOnly? From,
        DateOnly? To,
        string? Status)
    {
        public CitationFilter ToFilter() => new(StudentId, ParentId, ResponsibleUserId, From, To, Status);
    }

    public static void MapCitationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/citations");

        group.MapGet("/", ([AsParameters] CitationQuery query, HttpContext http, CitationService svc) =>
                Guard(http, async access => Results.Ok(await svc.ListAsync(access, query.ToFilter(), Today()))))
            .RequirePermission(ProcessKeys.Citation, PermissionFlags.Read);

        group.MapGet("/students", (int? classroomId, string? q, HttpContext http, ObservationService svc) =>
                Guard(http, async access => Results.Ok(await svc.SearchStudentsAsync(access, classroomId, q))))
            .RequirePermission(ProcessKeys.Citation, PermissionFlags.Read);

        group.MapGet("/parents", (int studentId, HttpContext http, CitationService svc) =>
                Guard(http, async access => Results.Ok(await svc.ParentsAsync(access, studentId))))
            .RequirePermission(ProcessKeys.Citation, PermissionFlags.Read);

        group.MapGet("/slots", (int? userId, HttpContext http, CitationService svc) =>
                Guard(http, async access => Results.Ok(await svc.SlotsAsync(userId ?? access.UserId))))
            .RequirePermission(ProcessKeys.Citation, PermissionFlags.Read);

        group.MapPost("/", (CreateCitationRequest request, HttpContext http, CitationService svc) =>
                Guard(http, async access =>
                {
                    var id = await svc.CreateManualAsync(access, request);
                    return Results.Created($"/citations/{id}", new { citationId = id });
                }))
            .RequirePermission(ProcessKeys.Citation, PermissionFlags.Create);

        group.MapPut("/{id:int}/schedule", (int id, ScheduleCitationRequest request, HttpContext http, CitationService svc) =>
                Guard(http, async access =>
                {
                    await svc.ScheduleAsync(access, id, request, Today());
                    return Results.NoContent();
                }))
            .RequirePermission(ProcessKeys.Citation, PermissionFlags.Update);

        group.MapPost("/{id:int}/reschedule", (int id, RescheduleCitationRequest request, HttpContext http, CitationService svc) =>
                Guard(http, async access =>
                {
                    var schedule = new ScheduleCitationRequest(request.Date, request.Time, request.ScheduleId);
                    var newId = await svc.RescheduleAsync(access, id, schedule, request.Note, Today());
                    return Results.Ok(new { citationId = newId });
                }))
            .RequirePermission(ProcessKeys.Citation, PermissionFlags.Update);

        group.MapPut("/{id:int}/flags", (int id, CitationFlagsRequest request, HttpContext http, CitationService svc) =>
                Guard(http, async access =>
                {
                    await svc.SetFlagsAsync(access, id, request);
                    return Results.NoContent();
                }))
            .RequirePermission(ProcessKeys.Citation, PermissionFlags.Update);

        group.MapPut("/{id:int}/status", (int id, CitationStatusRequest request, HttpContext http, CitationService svc) =>
                Guard(http, async access =>
                {
                    await svc.SetStatusAsync(access, id, request);
                    return Results.NoContent();
                }))
            .RequirePermission(ProcessKeys.Citation, PermissionFlags.Update);
    }

    private static async Task<IResult> Guard(HttpContext http, Func<ObservationAccess, Task<IResult>> action)
    {
        try
        {
            if (CurrentUserId(http.User) is not { } userId)
                return Results.Unauthorized();

            var seesAll = await PermissionFilters.HasAnyAsync(http, (ProcessKeys.ObservationConsolidated, PermissionFlags.Read));
            return await action(new ObservationAccess(userId, seesAll));
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

    private static int? CurrentUserId(ClaimsPrincipal user) =>
        int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}

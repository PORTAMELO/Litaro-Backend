using System.Security.Claims;
using Litaro.Models;
using Litaro.Services;

namespace Litaro.Endpoints;

public static class NotificationEndpoints
{
    public record MarkDoneRequest(List<int>? Ids, string? Note);

    public static void MapNotificationEndpoints(this WebApplication app)
    {
        var inbox = app.MapGroup("/notifications");

        inbox.MapGet("", async (string? state, int? typeId, ClaimsPrincipal user, NotificationService svc) =>
                CurrentUserId(user) is { } userId
                    ? Results.Ok(await svc.ListAsync(userId, state, typeId))
                    : Results.Unauthorized())
            .RequirePermission(ProcessKeys.Notification, PermissionFlags.Read);

        inbox.MapGet("/summary", async (HttpContext http, NotificationService svc) =>
        {
            if (CurrentUserId(http.User) is not { } userId)
                return Results.Unauthorized();

            var allowed = await PermissionFilters.HasAnyAsync(http, (ProcessKeys.Notification, PermissionFlags.Read));

            return Results.Ok(allowed ? await svc.SummaryAsync(userId) : new NotificationSummaryDto(0, 0));
        });

        inbox.MapPost("/{id:int}/read", async (int id, ClaimsPrincipal user, NotificationService svc) =>
            {
                if (CurrentUserId(user) is not { } userId)
                    return Results.Unauthorized();

                await svc.MarkReadAsync(id, userId);
                return Results.NoContent();
            })
            .RequirePermission(ProcessKeys.Notification, PermissionFlags.Read);

        inbox.MapPost("/done", async (MarkDoneRequest request, ClaimsPrincipal user, NotificationService svc) =>
            {
                if (CurrentUserId(user) is not { } userId)
                    return Results.Unauthorized();

                try
                {
                    var done = await svc.MarkDoneAsync(request.Ids ?? [], request.Note, userId);
                    return Results.Ok(new { done });
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { message = ex.Message });
                }
            })
            .RequirePermission(ProcessKeys.Notification, PermissionFlags.Update);

        var types = app.MapGroup("/notification-types");

        types.MapGet("", async (NotificationSettingsService svc) =>
                Results.Ok(new { types = await svc.ListAsync(), roles = await svc.ListRolesAsync() }))
            .RequirePermission(ProcessKeys.NotificationSettings, PermissionFlags.Read);

        types.MapGet("/users", async (string? q, NotificationSettingsService svc) =>
                Results.Ok(await svc.SearchUsersAsync(q)))
            .RequirePermission(ProcessKeys.NotificationSettings, PermissionFlags.Update);

        types.MapPut("/{id:int}", async (int id, UpdateNotificationTypeRequest request, NotificationSettingsService svc) =>
            {
                try
                {
                    return Results.Ok(await svc.UpdateAsync(id, request));
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { message = ex.Message });
                }
            })
            .RequirePermission(ProcessKeys.NotificationSettings, PermissionFlags.Update);
    }

    private static int? CurrentUserId(ClaimsPrincipal user) =>
        int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}

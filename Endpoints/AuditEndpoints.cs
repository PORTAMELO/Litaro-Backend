using Litaro.Models;
using Litaro.Services;

namespace Litaro.Endpoints;

public static class AuditEndpoints
{
    public static void MapAuditEndpoints(this WebApplication app)
    {
        var audit = app.MapGroup("/audit");

        audit.MapGet("/events", async (
                string? category,
                DateOnly? from,
                DateOnly? to,
                int? userId,
                string? eventType,
                string? tableName,
                string? recordId,
                Guid? operationId,
                int? page,
                int? pageSize,
                AuditQueryService svc) =>
            {
                try
                {
                    var query = new AuditQuery(category ?? string.Empty, from, to, userId, eventType, tableName, recordId,
                        operationId, page ?? 1, pageSize ?? 50);
                    return Results.Ok(await svc.ListAsync(query, DateTime.UtcNow));
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { message = ex.Message });
                }
            })
            .RequirePermission(ProcessKeys.AuditModule, PermissionFlags.Read);

        audit.MapGet("/events/{id:long}", async (long id, AuditQueryService svc) =>
                await svc.GetAsync(id) is { } detail ? Results.Ok(detail) : Results.NotFound())
            .RequirePermission(ProcessKeys.AuditModule, PermissionFlags.Read);

        audit.MapGet("/options", (AuditQueryService svc) => Results.Ok(svc.Options()))
            .RequirePermission(ProcessKeys.AuditModule, PermissionFlags.Read);

        audit.MapGet("/users", async (string? q, AuditQueryService svc) =>
                Results.Ok(await svc.SearchUsersAsync(q ?? string.Empty)))
            .RequirePermission(ProcessKeys.AuditModule, PermissionFlags.Read);
    }
}

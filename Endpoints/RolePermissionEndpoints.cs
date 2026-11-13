using Litaro.Data;
using Litaro.Models;
using Litaro.Services;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Endpoints;

public static class RolePermissionEndpoints
{
    public record RolePermissionRow(string TableName, int Permissions, string? View, string Kind);
    public record SaveRolePermissionRequest(int RoleId, string TableName, int Permissions, string? View);

    public static void MapRolePermissionEndpoints(this WebApplication app)
    {
        app.MapGet("/role-permissions/{roleId:int}", async (int roleId, AppDbContext db) =>
        {
            var existing = await db.RolePermissions
                .Where(rp => rp.RoleId == roleId)
                .ToDictionaryAsync(rp => rp.TableName);

            var rows = PermissionCatalog.Tables.Concat(PermissionCatalog.Processes).Select(key =>
                existing.TryGetValue(key, out var rp)
                    ? new RolePermissionRow(key, rp.Permissions, rp.View, PermissionCatalog.KindOf(key))
                    : new RolePermissionRow(key, 0, null, PermissionCatalog.KindOf(key)));

            return Results.Ok(rows);
        }).RequireAuthorization(policy => policy.RequireRole("Administrador"));

        app.MapPut("/role-permissions", async (SaveRolePermissionRequest request, AppDbContext db, PermissionCache cache) =>
        {
            if (!PermissionCatalog.IsManaged(request.TableName))
                return Results.BadRequest("Tabla no administrable.");

            if (request.Permissions is < 0 or > 15)
                return Results.BadRequest("Permisos inválidos.");

            var row = await db.RolePermissions
                .FirstOrDefaultAsync(rp => rp.RoleId == request.RoleId && rp.TableName == request.TableName);

            if (row is null)
            {
                row = new RolePermission { RoleId = request.RoleId, TableName = request.TableName };
                db.RolePermissions.Add(row);
            }

            row.Permissions = request.Permissions;
            row.View = string.IsNullOrWhiteSpace(request.View) ? null : request.View.Trim();

            await db.SaveChangesAsync();
            cache.Invalidate();

            return Results.Ok(new RolePermissionRow(row.TableName, row.Permissions, row.View, PermissionCatalog.KindOf(row.TableName)));
        }).RequireAuthorization(policy => policy.RequireRole("Administrador"));
    }
}

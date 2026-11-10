using System.Security.Claims;
using Litaro.Models;
using Litaro.Services;

namespace Litaro.Endpoints;

public static class PermissionEndpoints
{
    public static void MapPermissionEndpoints(this WebApplication app)
    {
        app.MapGet("/permissions/me", async (string? role, ClaimsPrincipal user, PermissionService svc) =>
        {
            var owned = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
            var roles = PermissionCatalog.ResolveActiveRoles(owned, role);
            var permissions = await svc.GetForRolesAsync(roles);

            return Results.Ok(new { roles, permissions });
        });
    }
}

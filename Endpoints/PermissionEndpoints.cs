using System.Security.Claims;
using Litaro.Services;

namespace Litaro.Endpoints;

public static class PermissionEndpoints
{
    public static void MapPermissionEndpoints(this WebApplication app)
    {
        app.MapGet("/permissions/me", async (ClaimsPrincipal user, PermissionService svc) =>
        {
            var roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
            var permissions = await svc.GetForRolesAsync(roles);

            return Results.Ok(new { roles, permissions });
        });
    }
}
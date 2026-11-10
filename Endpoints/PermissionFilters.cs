using System.Security.Claims;
using Litaro.Models;
using Litaro.Services;

namespace Litaro.Endpoints;

public static class PermissionFilters
{
    public static RouteHandlerBuilder RequirePermission(this RouteHandlerBuilder builder, string key, PermissionFlags flag) =>
        builder.RequireAnyPermission((key, flag));

    public static RouteHandlerBuilder RequireAnyPermission(
        this RouteHandlerBuilder builder,
        params (string Key, PermissionFlags Flag)[] options) =>
        builder.AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;

            if (!await HasAnyAsync(http, options))
                return Results.Json(new { message = PermissionCatalog.DeniedMessage }, statusCode: StatusCodes.Status403Forbidden);

            return await next(context);
        });

    public static async Task<bool> HasAnyAsync(HttpContext http, params (string Key, PermissionFlags Flag)[] options)
    {
        var permissions = http.RequestServices.GetRequiredService<PermissionService>();
        var roles = http.User.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToList();

        foreach (var (key, flag) in options)
        {
            if (await permissions.HasPermissionAsync(roles, key, flag))
                return true;
        }

        return false;
    }
}

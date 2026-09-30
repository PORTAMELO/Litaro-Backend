using Litaro.Models;
using Microsoft.AspNetCore.Identity;

namespace Litaro.Endpoints;

public static class UserRoleEndpoints
{
    public record UserRolesDto(int UserId, List<string> Roles);
    public record SetUserRolesRequest(List<string> Roles);

    public static void MapUserRoleEndpoints(this WebApplication app)
    {
        app.MapGet("/users/{id:int}/roles", async (int id, UserManager<User> userManager) =>
        {
            var user = await userManager.FindByIdAsync(id.ToString());

            if (user is null)
                return Results.NotFound($"No existe un usuario con id {id}.");

            var roles = await userManager.GetRolesAsync(user);

            return Results.Ok(new UserRolesDto(id, roles.ToList()));
        }).RequireAuthorization(policy => policy.RequireRole("Administrador"));

        app.MapPut("/users/{id:int}/roles", async (
            int id,
            SetUserRolesRequest request,
            UserManager<User> userManager,
            RoleManager<IdentityRole<int>> roleManager) =>
        {
            var user = await userManager.FindByIdAsync(id.ToString());

            if (user is null)
                return Results.NotFound($"No existe un usuario con id {id}.");

            var requestedRoles = (request.Roles ?? new List<string>())
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var roleName in requestedRoles)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                    return Results.BadRequest($"El rol '{roleName}' no existe.");
            }

            var currentRoles = await userManager.GetRolesAsync(user);

            var toRemove = currentRoles.Except(requestedRoles, StringComparer.OrdinalIgnoreCase).ToList();
            var toAdd = requestedRoles.Except(currentRoles, StringComparer.OrdinalIgnoreCase).ToList();

            if (toRemove.Count > 0)
            {
                var removeResult = await userManager.RemoveFromRolesAsync(user, toRemove);

                if (!removeResult.Succeeded)
                    return Results.BadRequest(string.Join(" ", removeResult.Errors.Select(e => e.Description)));
            }

            if (toAdd.Count > 0)
            {
                var addResult = await userManager.AddToRolesAsync(user, toAdd);

                if (!addResult.Succeeded)
                    return Results.BadRequest(string.Join(" ", addResult.Errors.Select(e => e.Description)));
            }

            var updatedRoles = await userManager.GetRolesAsync(user);

            return Results.Ok(new UserRolesDto(id, updatedRoles.ToList()));
        }).RequireAuthorization(policy => policy.RequireRole("Administrador"));
    }
}
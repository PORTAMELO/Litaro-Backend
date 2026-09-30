using Microsoft.AspNetCore.Identity;

namespace Litaro.Endpoints;

public static class RoleEndpoints
{
    public record RoleDto(int Id, string Name);
    public record CreateRoleRequest(string Name);

    public static void MapRoleEndpoints(this WebApplication app)
    {
        app.MapGet("/roles", (RoleManager<IdentityRole<int>> roleManager) =>
        {
            var roles = roleManager.Roles
                .OrderBy(r => r.Name)
                .Select(r => new RoleDto(r.Id, r.Name!))
                .ToList();

            return Results.Ok(roles);
        });

        app.MapPost("/roles", async (CreateRoleRequest request, RoleManager<IdentityRole<int>> roleManager) =>
        {
            var name = request.Name?.Trim();

            if (string.IsNullOrWhiteSpace(name))
                return Results.BadRequest("El nombre del rol es obligatorio.");

            if (await roleManager.RoleExistsAsync(name))
                return Results.BadRequest("Ya existe un rol con ese nombre.");

            var result = await roleManager.CreateAsync(new IdentityRole<int>(name));

            if (!result.Succeeded)
                return Results.BadRequest(string.Join(" ", result.Errors.Select(e => e.Description)));

            var role = await roleManager.FindByNameAsync(name);

            return Results.Ok(new RoleDto(role!.Id, role.Name!));
        }).RequireAuthorization(policy => policy.RequireRole("Administrador"));
    }
}
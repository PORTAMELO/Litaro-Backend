using Litaro.Models;
using Litaro.Services;

namespace Litaro.Endpoints
{
    public static class UserEndpoints
    {
        public static void MapUserEndpoints(this WebApplication app)
        {
            app.MapGet("/users", async (UserService svc) =>
                Results.Ok(await svc.GetAllAsync()));

            app.MapGet("/users/{id}", async (int id, UserService svc) =>
                await svc.GetByIdAsync(id) is User u
                    ? Results.Ok(u)
                    : Results.NotFound());

            app.MapPost("/users", async (User user, UserService svc) =>
            {
                var created = await svc.CreateAsync(user);
                return Results.Created($"/users/{created.UserId}", created);
            });

            app.MapPut("/users/{id}", async (int id, User user, UserService svc) =>
                await svc.UpdateAsync(id, user)
                    ? Results.NoContent()
                    : Results.NotFound());

            app.MapDelete("/users/{id}/close", async (int id, UserService svc) =>
                await svc.DeactivateAsync(id)
                    ? Results.NoContent()
                    : Results.NotFound());

            app.MapPatch("/users/{id}/activate", async (int id, UserService svc) =>
                await svc.ActivateAsync(id)
                    ? Results.NoContent()
                    : Results.NotFound());
        }
    }
}

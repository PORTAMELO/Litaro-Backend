using Litaro.Models;
using Litaro.Services;
using Microsoft.AspNetCore.Mvc;

namespace Litaro.Endpoints
{
    public static class UserEndpoints
    {
        public static void MapUserEndpoints(this WebApplication app)
        {
            app.MapGet("/users", async ([FromServices] UserService svc) =>
                Results.Ok(await svc.GetAllAsync()));

            app.MapGet("/users/{id}", async (int id, [FromServices] UserService svc) =>
                await svc.GetByIdAsync(id) is User u
                    ? Results.Ok(u)
                    : Results.NotFound());

            app.MapPut("/users/{id}", async (int id, User user, [FromServices] UserService svc) =>
                await svc.UpdateAsync(id, user)
                    ? Results.NoContent()
                    : Results.NotFound());

            app.MapPatch("/users/{id}/deactivate", async (int id, [FromServices] UserService svc) =>
                await svc.DeactivateAsync(id)
                    ? Results.NoContent()
                    : Results.NotFound());

            app.MapPatch("/users/{id}/activate", async (int id, [FromServices] UserService svc) =>
                await svc.ActivateAsync(id)
                    ? Results.NoContent()
                    : Results.NotFound());
        }
    }
}

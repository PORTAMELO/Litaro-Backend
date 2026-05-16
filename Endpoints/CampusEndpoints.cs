using Litaro.Models;
using Litaro.Services;

namespace Litaro.Endpoints
{
    public static class CampusEndpoints
    {
        public static void MapCampusEndpoints(this WebApplication app)
        {
            app.MapGet("/campus", async (CampusService svc) =>
                Results.Ok(await svc.GetAllAsync()));

            app.MapGet("/campus/{id}", async (int id, CampusService svc) =>
                await svc.GetByIdAsync(id) is Campus c
                    ? Results.Ok(c)
                    : Results.NotFound());

            app.MapPost("/campus", async (Campus campus, CampusService svc) =>
            {
                var created = await svc.CreateAsync(campus);
                return Results.Created($"/campus/{created.CampusId}", created);
            });

            app.MapPut("/campus/{id}", async(int id, Campus campus, CampusService svc) =>
                await svc.UpdateAsync(id, campus)
                ? Results.NoContent()
                : Results.NotFound());

            app.MapDelete("/campus/{id}/close", async (int id, CampusService svc) =>
                await svc.DeactivateAsync(id)
                ? Results.NoContent()
                : Results.NotFound());

            app.MapPatch("/campus/{id}/activate", async (int id, CampusService svc) =>
                await svc.ActivateAsync(id)
                ? Results.NoContent()
                : Results.NotFound());

        }

    }
}

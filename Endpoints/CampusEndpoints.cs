using Litaro.Models;
using Litaro.Services;
using Microsoft.AspNetCore.Mvc;
namespace Litaro.Endpoints
{
    public static class CampusEndpoints
    {
        public static void MapCampusEndpoints(this WebApplication app)
        {
            app.MapGet("/campus", async ([FromServices] CampusService svc) =>
                Results.Ok(await svc.GetAllAsync()));

            app.MapGet("/campus/{id}", async (int id, [FromServices] CampusService svc) =>
                await svc.GetByIdAsync(id) is Campus c
                    ? Results.Ok(c)
                    : Results.NotFound());

            app.MapPost("/campus", async (Campus campus, [FromServices] CampusService svc) =>
            {
                var created = await svc.CreateAsync(campus);
                return Results.Created($"/campus/{created.CampusId}", created);
            });

            app.MapPut("/campus/{id}", async (int id, Campus campus, [FromServices] CampusService svc) =>
                await svc.UpdateAsync(id, campus)
                ? Results.NoContent()
                : Results.NotFound());

            app.MapDelete("/campus/{id}/close", async (int id, [FromServices] CampusService svc) =>
                await svc.DeactivateAsync(id)
                ? Results.NoContent()
                : Results.NotFound());

            app.MapPatch("/campus/{id}/activate", async (int id, [FromServices] CampusService svc) =>
                await svc.ActivateAsync(id)
                ? Results.NoContent()
                : Results.NotFound());

            app.MapPost("/campus/import", async (IFormFile file, [FromServices] CampusService svc) =>
            {
                if (file is null || file.Length == 0)
                    return Results.BadRequest("No se recibió ningún archivo.");

                if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                    return Results.BadRequest("El archivo debe ser un CSV.");

                await using var stream = file.OpenReadStream();
                var (imported, errors) = await svc.ImportFromCsvAsync(stream);

                return Results.Ok(new
                {
                    Imported = imported,
                    Errors = errors
                });
            }).DisableAntiforgery();

        }

    }
}

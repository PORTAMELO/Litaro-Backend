using Litaro.Models;
using Litaro.Services;
using Microsoft.AspNetCore.Mvc;

namespace Litaro.Endpoints;

public static class SchoolEndpoints
{
    public static void MapSchoolEndpoints(this WebApplication app)
    {
        app.MapGet("/schools", async ([FromServices] SchoolService svc) =>
            Results.Ok(await svc.GetAllAsync()));

        app.MapGet("/schools/{id}", async (int id, [FromServices] SchoolService svc) =>
            await svc.GetByIdAsync(id) is School s
                ? Results.Ok(s)
                : Results.NotFound());

        app.MapPost("/schools", async (School school, [FromServices] SchoolService svc) =>
        {
            var created = await svc.CreateAsync(school);
            return Results.Created($"/schools/{created.SchoolId}", created);
        });

        app.MapPut("/schools/{id}", async (int id, School school, [FromServices] SchoolService svc) =>
            await svc.UpdateAsync(id, school)
                ? Results.NoContent()
                : Results.NotFound());

        app.MapDelete("/schools/{id}/close", async (int id, [FromServices] SchoolService svc) =>
            await svc.DeactivateAsync(id)
                ? Results.NoContent()
                : Results.NotFound());

        app.MapPatch(pattern: "/schools/{id}/activate", async (int id, [FromServices] SchoolService svc) =>
            await svc.ActivateAsync(id)
                ? Results.NoContent()
                : Results.NotFound());

        app.MapPost("/schools/import", async (IFormFile file, [FromServices] SchoolService svc) =>
        {
            if (file is null || file.Length == 0)
                return Results.BadRequest("No se recibió ningún archivo.");

            if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                return Results.BadRequest("El archivo debe ser un archivo CSV.");

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
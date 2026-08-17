using Litaro.Models;
using Litaro.Services;
using Microsoft.AspNetCore.Mvc;

namespace Litaro.Endpoints
{
    public static class AcademicYearEndpoints
    {
        public static void MapAcademicYearEndpoints(this WebApplication app)
        {
            app.MapGet("/academic-years", async (AcademicYearService svc) =>
                Results.Ok(await svc.GetAllAsync()));

            app.MapGet("/academic-years/{id}", async (short id, AcademicYearService svc) =>
                await svc.GetByIdAsync(id) is AcademicYear y
                    ? Results.Ok(y)
                    : Results.NotFound());

            app.MapPost("/academic-years", async (AcademicYear year, AcademicYearService svc) =>
            {
                var created = await svc.CreateAsync(year);
                return Results.Created($"/academic-years/{created.YearId}", created);
            });

            app.MapPut("/academic-years/{id}", async (short id, AcademicYear year, AcademicYearService svc) =>
                await svc.UpdateAsync(id, year)
                    ? Results.NoContent()
                    : Results.NotFound());

            app.MapPatch("/academic-years/{id}/deactivate", async (short id, AcademicYearService svc) =>
                await svc.DeactivateAsync(id)
                    ? Results.NoContent()
                    : Results.NotFound());

            app.MapPatch("/academic-years/{id}/activate", async (short id, AcademicYearService svc) =>
                await svc.ActivateAsync(id)
                    ? Results.NoContent()
                    : Results.NotFound());

            app.MapPost("/academic-years/import", async (IFormFile file, AcademicYearService svc) =>
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

using Litaro.Models;
using Litaro.Services;
using Microsoft.AspNetCore.Mvc;

namespace Litaro.Endpoints
{
    public static class AcademicPeriodEndpoints
    {
        public static void MapAcademicPeriodEndpoints(this WebApplication app)
        {
            app.MapGet("/academic-periods", async ([FromServices] AcademicPeriodService svc) =>
                Results.Ok(await svc.GetAllAsync()));

            app.MapGet("/academic-periods/{id}", async (short id, [FromServices] AcademicPeriodService svc) =>
                await svc.GetByIdAsync(id) is AcademicPeriod p
                    ? Results.Ok(p)
                    : Results.NotFound());

            app.MapPost("/academic-periods", async (AcademicPeriod period, [FromServices] AcademicPeriodService svc) =>
            {
                var created = await svc.CreateAsync(period);
                return Results.Created($"/academic-periods/{created.PeriodId}", created);
            });

            app.MapPut("/academic-periods/{id}", async (short id, AcademicPeriod period, [FromServices] AcademicPeriodService svc) =>
                await svc.UpdateAsync(id, period)
                    ? Results.NoContent()
                    : Results.NotFound());

            app.MapDelete("/academic-periods/{id}", async (short id, [FromServices] AcademicPeriodService svc) =>
                await svc.DeleteAsync(id)
                    ? Results.NoContent()
                    : Results.NotFound());

            app.MapPost("/academic-periods/import", async (IFormFile file, [FromServices] AcademicPeriodService svc) =>
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

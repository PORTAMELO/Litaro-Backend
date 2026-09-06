using Litaro.Models;
using Litaro.Services;

namespace Litaro.Endpoints
{
    public static class AcademicPeriodEndpoints
    {
        public static void MapAcademicPeriodEndpoints(this WebApplication app)
        {
            app.MapGet("/academic-periods", async (HttpRequest request, AcademicPeriodService svc, ForeignKeyResolverService fkSvc) =>
            {
                var filters = new Dictionary<string, string>();
                foreach (var (key, value) in request.Query)
                {
                    if (!string.IsNullOrEmpty(value))
                        filters[key] = value;
                }

                var records = await svc.GetAllAsync(filters);
                var lookups = await fkSvc.BuildLookupsAsync("AcademicPeriod", records);
                return Results.Ok(new { records, lookups });
            });

            app.MapPost("/academic-periods", async (AcademicPeriodService.CreateAcademicPeriodRequest request, AcademicPeriodService svc) =>
            {
                try
                {
                    var created = await svc.CreateAsync(request);
                    return Results.Ok(created);
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(ex.Message);
                }
            }).RequireAuthorization(policy => policy.RequireRole("Administrador"));

            app.MapPost("/academic-periods/import", async (IFormFile file, AcademicPeriodService svc) =>
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

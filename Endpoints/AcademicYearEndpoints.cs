using Litaro.Models;
using Litaro.Services;

namespace Litaro.Endpoints
{
    public static class AcademicYearEndpoints
    {
        public static void MapAcademicYearEndpoints(this WebApplication app)
        {
            app.MapGet("/academic-years", async (HttpRequest request, AcademicYearService svc, ForeignKeyResolverService fkSvc) =>
            {
                var filters = request.Query.ToDictionary(q => q.Key, q => q.Value.ToString());
                var records = await svc.GetAllAsync(filters);
                var lookups = await fkSvc.BuildLookupsAsync("AcademicYear", records);
                return Results.Ok(new { records, lookups });
            });

            app.MapPost("/academic-years", async (AcademicYearService.CreateAcademicYearRequest request, AcademicYearService svc) =>
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

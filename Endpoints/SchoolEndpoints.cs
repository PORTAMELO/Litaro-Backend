using Litaro.Models;
using Litaro.Services;

namespace Litaro.Endpoints;

public static class SchoolEndpoints
{
    public static void MapSchoolEndpoints(this WebApplication app)
    {
        app.MapGet("/schools", async (HttpRequest request, SchoolService svc, ForeignKeyResolverService fkSvc) =>
        {
            var filters = request.Query.ToDictionary(q => q.Key, q => q.Value.ToString());
            var records = await svc.GetAllAsync(filters);
            var lookups = await fkSvc.BuildLookupsAsync("School", records);
            return Results.Ok(new { records, lookups });
        });

        app.MapPost("/schools", async (SchoolService.CreateSchoolRequest request, SchoolService svc) =>
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

        app.MapPost("/schools/import", async (IFormFile file, SchoolService svc) =>
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
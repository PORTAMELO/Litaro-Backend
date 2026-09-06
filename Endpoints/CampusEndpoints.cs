using Litaro.Models;
using Litaro.Services;
namespace Litaro.Endpoints
{
    public static class CampusEndpoints
    {
        public static void MapCampusEndpoints(this WebApplication app)
        {
            app.MapGet("/campus", async (HttpRequest request, CampusService svc, ForeignKeyResolverService fkSvc) =>
            {
                var filters = request.Query.ToDictionary(q => q.Key, q => q.Value.ToString());
                var records = await svc.GetAllAsync(filters);
                var lookups = await fkSvc.BuildLookupsAsync("Campus", records);
                return Results.Ok(new { records, lookups });
            });

            app.MapPost("/campus", async (CampusService.CreateCampusRequest request, CampusService svc) =>
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

            app.MapPost("/campus/import", async (IFormFile file, CampusService svc) =>
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

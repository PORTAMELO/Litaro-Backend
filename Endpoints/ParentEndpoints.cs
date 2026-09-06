using Litaro.Models;
using Litaro.Services;

namespace Litaro.Endpoints
{
    public static class ParentEndpoints
    {
        public static void MapParentEndpoints(this WebApplication app)
        {
            app.MapGet("/parents", async (HttpRequest request, ParentService svc, ForeignKeyResolverService fkSvc) =>
            {
                var filters = request.Query.ToDictionary(q => q.Key, q => q.Value.ToString());
                var records = await svc.GetAllAsync(filters);
                var lookups = await fkSvc.BuildLookupsAsync("Parent", records);
                return Results.Ok(new { records, lookups });
            });

            app.MapGet("/parents/{id:int}", async (int id, ParentService svc) =>
            {
                if (id <= 0)
                    return Results.BadRequest("El id debe ser un entero positivo.");

                var parent = await svc.GetByIdAsync(id);

                return parent is not null
                    ? Results.Ok(parent)
                    : Results.NotFound($"No existe un acudiente con id {id}.");
            });

            app.MapPost("/parents", async (ParentService.CreateParentRequest request, ParentService svc) =>
            {
                try
                {
                    var result = await svc.CreateAsync(request);
                    return Results.Ok(new
                    {
                        parent = result.Parent,
                        links = result.Links,
                        temporaryPassword = result.TemporaryPassword
                    });
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { message = ex.Message });
                }
            }).RequireAuthorization(policy => policy.RequireRole("Administrador"));

            app.MapPost("/parents/import", async (IFormFile file, ParentService svc) =>
            {
                if (file is null || file.Length == 0)
                    return Results.BadRequest("No se recibió ningún archivo.");

                if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                    return Results.BadRequest("El archivo debe ser un CSV.");

                await using var stream = file.OpenReadStream();
                var (imported, errors) = await svc.ImportFromCsvAsync(stream);

                return Results.Ok(new { Imported = imported, Errors = errors });
            }).DisableAntiforgery();

        }
    }
}

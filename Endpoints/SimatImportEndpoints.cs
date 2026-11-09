using Litaro.Services;

namespace Litaro.Endpoints
{
    public static class SimatImportEndpoints
    {
        public static void MapSimatImportEndpoints(this WebApplication app)
        {
            app.MapPost("/simat/import", async (IFormFile file, short yearId, bool? dryRun, SimatImportService svc) =>
            {
                if (file is null || file.Length == 0)
                    return Results.BadRequest(new { message = "No se recibió ningún archivo." });

                if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                    return Results.BadRequest(new { message = "El archivo debe ser el Excel (.xlsx) descargado del SIMAT." });

                await using var stream = file.OpenReadStream();
                var result = await svc.ImportAsync(stream, yearId, dryRun ?? true);
                return Results.Ok(result);
            })
            .DisableAntiforgery()
            .RequireAuthorization(policy => policy.RequireRole("Administrador"));
        }
    }
}

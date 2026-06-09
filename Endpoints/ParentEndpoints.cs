using Litaro.Models;
using Litaro.Services;

namespace Litaro.Endpoints
{
    public static class ParentEndpoints
    {
        public static void MapParentEndpoints(this WebApplication app)
        {
            app.MapGet("/parents", async (ParentService svc) =>
                Results.Ok(await svc.GetAllAsync()));

            app.MapGet("/parents/{id}", async (int id, ParentService svc) =>
                await svc.GetByIdAsync(id) is Parent p
                    ? Results.Ok(p)
                    : Results.NotFound());

            app.MapPost("/parents", async (Parent parent, ParentService svc) =>
            {
                var created = await svc.CreateAsync(parent);
                return Results.Created($"/parents/{created.ParentId}", created);
            });

            app.MapPut("/parents/{id}", async (int id, Parent parent, ParentService svc) =>
                await svc.UpdateAsync(id, parent)
                    ? Results.NoContent()
                    : Results.NotFound());

            app.MapDelete("/parents/{id}", async (int id, ParentService svc) =>
                await svc.DeleteAsync(id)
                    ? Results.NoContent()
                    : Results.NotFound());

            app.MapPost("/parents/import", async (IFormFile file, ParentService svc) =>
            {
                if (file is null || file.Length == 0)
                    return Results.BadRequest("No se recibió ningún archivo.");

                if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                    return Results.BadRequest("El archivo debe ser un CSV.");

                await using var stream = file.OpenReadStream();
                var (imported, errors) = await svc.ImportFromCsvAsync(stream);

                return Results.Ok(new { Imported = imported, Errors = errors });
            })
.DisableAntiforgery();

        }
    }
}

using Litaro.Models;
using Litaro.Services;

namespace Litaro.Endpoints
{
    public static class SpaceEndpoints
    {
        public static void MapSpaceEndpoints(this WebApplication app)
        {
            app.MapGet("/spaces", async (HttpRequest request, SpaceService svc, ForeignKeyResolverService fkSvc) =>
            {
                var filters = request.Query.ToDictionary(q => q.Key, q => q.Value.ToString());
                var records = await svc.GetAllAsync(filters);
                var lookups = await fkSvc.BuildLookupsAsync("Space", records);
                return Results.Ok(new { records, lookups });
            });

            app.MapGet("/spaces/{id:int}", async (int id, SpaceService svc) =>
            {
                if (id <= 0)
                    return Results.BadRequest("El id debe ser un entero positivo.");

                var space = await svc.GetByIdAsync(id);

                return space is not null
                    ? Results.Ok(space)
                    : Results.NotFound($"No existe un espacio con id {id}.");
            });

            app.MapPost("/spaces", async (SpaceService.SaveSpaceRequest request, SpaceService svc) =>
            {
                try
                {
                    return Results.Ok(await svc.CreateAsync(request));
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(ex.Message);
                }
            });

            app.MapPut("/spaces/{id:int}", async (int id, SpaceService.SaveSpaceRequest request, SpaceService svc) =>
            {
                try
                {
                    var space = await svc.UpdateAsync(id, request);
                    return space is not null
                        ? Results.Ok(space)
                        : Results.NotFound($"No existe un espacio con id {id}.");
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(ex.Message);
                }
            });

            app.MapPut("/spaces/{id:int}/estado", async (int id, SetActiveRequest request, SpaceService svc) =>
                await svc.SetActiveAsync(id, request.Active)
                    ? Results.NoContent()
                    : Results.NotFound($"No existe un espacio con id {id}."));
        }
    }
}

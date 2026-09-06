using Litaro.Services;

namespace Litaro.Endpoints;

public static class CharacteristicEndpoints
{
    public static void MapCharacteristicEndpoints(this WebApplication app)
    {
        app.MapGet("/characteristics", async (HttpRequest request, CharacteristicService svc, ForeignKeyResolverService fkSvc) =>
        {
            var filters = request.Query.ToDictionary(q => q.Key, q => q.Value.ToString());
            var records = await svc.GetAllAsync(filters);
            var lookups = await fkSvc.BuildLookupsAsync("Characteristic", records);
            return Results.Ok(new { records, lookups });
        });

        app.MapPost("/characteristics", async (CharacteristicService.CreateCharacteristicRequest request, CharacteristicService svc) =>
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

        app.MapGet("/characteristic-details", async (HttpRequest request, CharacteristicService svc, ForeignKeyResolverService fkSvc) =>
        {
            var filters = request.Query.ToDictionary(q => q.Key, q => q.Value.ToString());
            var records = await svc.GetAllDetailsAsync(filters);
            var lookups = await fkSvc.BuildLookupsAsync("CharacteristicDetail", records);
            return Results.Ok(new { records, lookups });
        });

        app.MapPost("/characteristic-details", async (CharacteristicService.CreateCharacteristicDetailRequest request, CharacteristicService svc) =>
        {
            try
            {
                var created = await svc.CreateDetailAsync(request);
                return Results.Ok(created);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(ex.Message);
            }
        }).RequireAuthorization(policy => policy.RequireRole("Administrador"));

        app.MapGet("/characteristics/{characteristicId:int}/details", async (int characteristicId, CharacteristicService svc) =>
        {
            var details = await svc.GetDetailsAsync(characteristicId);
            return Results.Ok(details);
        });
    }
}

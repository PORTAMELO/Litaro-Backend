using Litaro.Services;

namespace Litaro.Endpoints;

public static class LookupEndpoints
{
    public static void MapLookupEndpoints(this WebApplication app)
    {
        app.MapGet("/lookup-options/{tableName}", async (string tableName, LookupService svc) =>
        {
            var options = await svc.GetOptionsAsync(tableName);

            return options is not null
                ? Results.Ok(options)
                : Results.NotFound();
        });
    }
}

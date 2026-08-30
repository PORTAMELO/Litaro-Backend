using Litaro.Services;

namespace Litaro.Endpoints;

public static class SchemaEndpoints
{
    public static void MapSchemaEndpoints(this WebApplication app)
    {
        app.MapGet("/schema/{tableName}", async (string tableName, SchemaService svc) =>
        {
            var columns = await svc.GetByTableAsync(tableName);

            return columns is not null
            ? Results.Ok(columns)
            : Results.NotFound();
        }).RequireAuthorization(policy => policy.RequireRole("Administrador", "Profesor"));
    }
}
using Litaro.Services;

namespace Litaro.Endpoints;

public static class ColumnConfigurationEndpoints
{
    public record UpdateColumnConfigurationRequest(List<ColumnConfigItem> Columns);

    public static void MapColumnConfigurationEndpoints(this WebApplication app)
    {
        app.MapGet("/column-configuration/{tableName}", async (string tableName, SchemaService svc) =>
        {
            var schema = await svc.GetByTableAsync(tableName);

            return schema is not null
            ? Results.Ok(schema.Select(s => new { s.ColumnName, s.Filterable, s.Visible, s.Alias, s.CharacteristicId }))
            : Results.NotFound();
        }).RequireAuthorization(policy => policy.RequireRole("Administrador", "Profesor"));

        app.MapPut("/column-configuration/{tableName}", async (
            string tableName,
            UpdateColumnConfigurationRequest request,
            SchemaService schemaSvc,
            ColumnConfigurationService columnConfigSvc) =>
        {
            var schema = await schemaSvc.GetByTableAsync(tableName);

            if (schema is null)
                return Results.NotFound();

            var validColumns = schema.Select(s => s.ColumnName).ToHashSet();
            var invalid = request.Columns.Where(c => !validColumns.Contains(c.ColumnName)).ToList();

            if (invalid.Count > 0)
                return Results.BadRequest($"Columnas inválidas: {string.Join(", ", invalid.Select(c => c.ColumnName))}");

            var joinedTable = schemaSvc.GetJoinedTable(tableName);

            if (joinedTable is null)
            {
                await columnConfigSvc.SetColumnConfigurationAsync(tableName, request.Columns);
            }
            else
            {
                var ownColumnNames = await schemaSvc.GetOwnColumnNamesAsync(tableName);

                var ownColumns = request.Columns.Where(c => ownColumnNames.Contains(c.ColumnName)).ToList();
                var joinedColumns = request.Columns.Where(c => !ownColumnNames.Contains(c.ColumnName)).ToList();

                await columnConfigSvc.SetColumnConfigurationAsync(tableName, ownColumns);
                await columnConfigSvc.SetColumnConfigurationAsync(joinedTable, joinedColumns);
            }

            return Results.NoContent();
        }).RequireAuthorization(policy => policy.RequireRole("Administrador"));
    }
}

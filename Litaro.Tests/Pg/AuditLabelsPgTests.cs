using Litaro.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Litaro.Tests.Pg;

public sealed class AuditLabelsPgTests(PgDatabaseFixture fixture) : IClassFixture<PgDatabaseFixture>
{
    [PgFact]
    public async Task Every_audited_table_has_a_label()
    {
        await using var connection = new NpgsqlConnection(fixture.Database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT tablename::text FROM pg_tables WHERE schemaname = 'public' AND NOT (tablename::text = ANY (audit_excluded_tables()))",
            connection);

        var tables = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tables.Add(reader.GetString(0));
        }

        Assert.NotEmpty(tables);
        var missing = tables.Where(t => !AuditLabels.Tables.ContainsKey(t)).ToList();
        Assert.True(missing.Count == 0, "Tablas sin etiqueta: " + string.Join(", ", missing));
    }

    [PgFact]
    public async Task Coverage_query_used_at_startup_runs()
    {
        await using var db = fixture.Database.NewContext();

        var uncovered = await db.Database
            .SqlQueryRaw<string>("SELECT audit_uncovered_tables() AS \"Value\"")
            .ToListAsync();

        Assert.Empty(uncovered);
    }
}

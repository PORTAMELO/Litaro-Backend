using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Litaro.Tests.Pg;

public sealed class AuditDocumentMigrationPgTests(PgDatabaseFixture fixture) : IClassFixture<PgDatabaseFixture>
{
    private const string AuditWriteSignature = "audit_write(text, text, text, jsonb, jsonb, text[], jsonb)";

    private const string TriggerFunctionSql = """
        SELECT p.proname::text
          FROM pg_trigger g
          JOIN pg_proc p ON p.oid = g.tgfoid
         WHERE g.tgrelid = 'public."Document"'::regclass AND g.tgname = 'audit_row'
        """;

    [PgFact]
    public async Task Down_restores_generic_document_trigger_and_up_reapplies_it()
    {
        await using var db = fixture.Database.NewContext();
        var migrator = db.GetService<IMigrator>();

        Assert.Equal("audit_document_change", await ScalarAsync<string>(TriggerFunctionSql));

        await migrator.MigrateAsync("AddAuditLog");

        Assert.Equal("audit_row_change", await ScalarAsync<string>(TriggerFunctionSql));
        Assert.Equal(0L, await ScalarAsync<long>("SELECT count(*) FROM pg_proc WHERE proname IN ('audit_write','audit_document_change')"));
        Assert.DoesNotContain("audit_write", await ScalarAsync<string>("SELECT prosrc FROM pg_proc WHERE proname = 'audit_row_change'"));
        Assert.DoesNotContain("audit_document_change", await ScalarAsync<string>("SELECT prosrc FROM pg_proc WHERE proname = 'audit_attach'"));
        Assert.Equal(0L, await ScalarAsync<long>("SELECT count(*) FROM audit_uncovered_tables()"));

        await migrator.MigrateAsync();

        Assert.Equal("audit_document_change", await ScalarAsync<string>(TriggerFunctionSql));
        Assert.Equal(0L, await ScalarAsync<long>("SELECT count(*) FROM audit_uncovered_tables()"));
    }

    [PgFact]
    public async Task Attach_on_document_uses_the_document_function()
    {
        await ScalarAsync<object>("SELECT audit_attach('Document')");

        Assert.Equal("audit_document_change", await ScalarAsync<string>(TriggerFunctionSql));
    }

    [PgFact]
    public async Task Audit_write_cannot_be_called_outside_a_trigger()
    {
        var error = await Assert.ThrowsAsync<PostgresException>(() => ScalarAsync<object>(
            "SELECT audit_write('INSERT', 'Grade', '1', NULL, NULL, NULL, NULL)"));

        Assert.Equal("La auditoría solo se escribe desde un trigger.", error.MessageText);
    }

    [PgFact]
    public async Task Audit_write_is_not_executable_by_public_or_supabase_roles()
    {
        Assert.False(await ScalarAsync<bool>($"SELECT has_function_privilege('public', '{AuditWriteSignature}', 'EXECUTE')"));
        Assert.Equal(0L, await ScalarAsync<long>($"""
            SELECT count(*) FROM pg_roles
             WHERE rolname IN ('anon', 'authenticated', 'service_role')
               AND has_function_privilege(oid, '{AuditWriteSignature}', 'EXECUTE')
            """));
    }

    [PgFact]
    public async Task New_role_cannot_execute_audit_write()
    {
        var role = "litaro_test_probe_" + Guid.NewGuid().ToString("N");
        await using var connection = new NpgsqlConnection(fixture.Database.ConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await using (var create = new NpgsqlCommand($"CREATE ROLE \"{role}\" NOLOGIN", connection, transaction))
        {
            await create.ExecuteNonQueryAsync();
        }

        await using var check = new NpgsqlCommand($"SELECT has_function_privilege(@role, '{AuditWriteSignature}', 'EXECUTE')", connection, transaction);
        check.Parameters.AddWithValue("role", role);
        Assert.False((bool)(await check.ExecuteScalarAsync())!);
        await transaction.RollbackAsync();
    }

    [PgFact]
    public async Task AuditLog_is_closed_to_the_supabase_api_roles()
    {
        await using var db = fixture.Database.NewContext();
        var migrator = db.GetService<IMigrator>();
        var roles = await ApiRolesAsync();

        await migrator.MigrateAsync("AddEnrollmentForm");
        try
        {
            foreach (var role in roles)
                await ScalarAsync<object>($"""
                    ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO "{role}";
                    ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON SEQUENCES TO "{role}";
                    """);
            await migrator.MigrateAsync();
        }
        finally
        {
            foreach (var role in roles)
                await ScalarAsync<object>($"""
                    ALTER DEFAULT PRIVILEGES IN SCHEMA public REVOKE ALL ON TABLES FROM "{role}";
                    ALTER DEFAULT PRIVILEGES IN SCHEMA public REVOKE ALL ON SEQUENCES FROM "{role}";
                    """);
        }

        Assert.True(await ScalarAsync<bool>("SELECT relrowsecurity FROM pg_class WHERE oid = 'public.\"AuditLog\"'::regclass"));
        foreach (var role in roles)
        {
            Assert.False(await ScalarAsync<bool>(
                $"SELECT has_table_privilege('{role}', 'public.\"AuditLog\"', 'SELECT, INSERT, UPDATE, DELETE, TRUNCATE, REFERENCES, TRIGGER')"));
            Assert.False(await ScalarAsync<bool>(
                $"SELECT has_sequence_privilege('{role}', pg_get_serial_sequence('public.\"AuditLog\"', 'AuditLogId'), 'USAGE, SELECT, UPDATE')"));
        }
    }

    private async Task<List<string>> ApiRolesAsync()
    {
        await using var connection = new NpgsqlConnection(fixture.Database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT rolname::text FROM pg_roles WHERE rolname IN ('anon', 'authenticated', 'service_role') ORDER BY 1", connection);
        await using var reader = await command.ExecuteReaderAsync();
        var roles = new List<string>();
        while (await reader.ReadAsync())
            roles.Add(reader.GetString(0));
        return roles;
    }

    private async Task<T> ScalarAsync<T>(string sql)
    {
        await using var connection = new NpgsqlConnection(fixture.Database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return (T)(await command.ExecuteScalarAsync())!;
    }
}

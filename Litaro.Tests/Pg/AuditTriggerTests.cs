using System.Text.Json.Nodes;
using Npgsql;

namespace Litaro.Tests.Pg;

public sealed class AuditTriggerTests(PgDatabaseFixture fixture) : IClassFixture<PgDatabaseFixture>
{
    private static int nextOrderNum = 100;

    [PgFact]
    public async Task Insert_is_logged_as_direct_db()
    {
        await using var connection = await OpenAsync();
        var gradeId = await InsertGradeAsync(connection, "Sexto");
        var sessionUser = await ScalarAsync<string>(connection, "SELECT session_user::text");

        var row = Assert.Single(await AuditRowsAsync(connection, "Grade", gradeId.ToString()));

        Assert.Equal("INSERT", row.EventType);
        Assert.Equal("Grade", row.TableName);
        Assert.Equal(gradeId.ToString(), row.RecordId);
        Assert.Null(row.OldData);
        Assert.Equal("Sexto", Text(row.NewData, "Name"));
        Assert.Null(row.ChangedColumns);
        Assert.Equal("DIRECT_DB", row.ActorKind);
        Assert.Null(row.UserId);
        Assert.Null(row.Source);
        Assert.Equal(sessionUser, row.DbUser);
    }

    [PgFact]
    public async Task Update_logs_old_new_and_changed_columns()
    {
        await using var connection = await OpenAsync();
        var gradeId = await InsertGradeAsync(connection, "Sexto");

        await ExecuteAsync(connection, "UPDATE \"Grade\" SET \"Name\" = 'Séptimo' WHERE \"GradeId\" = @id", ("id", gradeId));

        var row = Assert.Single(await AuditRowsAsync(connection, "Grade", gradeId.ToString(), "UPDATE"));
        Assert.Equal(new[] { "Name" }, row.ChangedColumns);
        Assert.Equal("Sexto", Text(row.OldData, "Name"));
        Assert.Equal("Séptimo", Text(row.NewData, "Name"));
    }

    [PgFact]
    public async Task Update_without_real_change_is_not_logged()
    {
        await using var connection = await OpenAsync();
        var gradeId = await InsertGradeAsync(connection, "Sexto");

        await ExecuteAsync(connection, "UPDATE \"Grade\" SET \"Name\" = \"Name\" WHERE \"GradeId\" = @id", ("id", gradeId));

        Assert.Empty(await AuditRowsAsync(connection, "Grade", gradeId.ToString(), "UPDATE"));
    }

    [PgFact]
    public async Task Delete_logs_old_data()
    {
        await using var connection = await OpenAsync();
        var gradeId = await InsertGradeAsync(connection, "Sexto");

        await ExecuteAsync(connection, "DELETE FROM \"Grade\" WHERE \"GradeId\" = @id", ("id", gradeId));

        var row = Assert.Single(await AuditRowsAsync(connection, "Grade", gradeId.ToString(), "DELETE"));
        Assert.Null(row.NewData);
        Assert.Equal("Sexto", Text(row.OldData, "Name"));
    }

    [PgFact]
    public async Task Composite_key_is_written_as_pairs()
    {
        await using var connection = await OpenAsync();
        var userId = await InsertUserAsync(connection);
        var roleName = "Rol " + Guid.NewGuid().ToString("N");
        var roleId = await ScalarAsync<int>(connection,
            "INSERT INTO \"AspNetRoles\"(\"Name\",\"NormalizedName\") VALUES (@name, upper(@name)) RETURNING \"Id\"",
            ("name", roleName));

        await ExecuteAsync(connection,
            "INSERT INTO \"AspNetUserRoles\"(\"UserId\",\"RoleId\") VALUES (@userId, @roleId)",
            ("userId", userId), ("roleId", roleId));

        var row = Assert.Single(await AuditRowsAsync(connection, "AspNetUserRoles", $"UserId={userId};RoleId={roleId}"));
        Assert.Equal("INSERT", row.EventType);
    }

    [PgFact]
    public async Task Sensitive_columns_are_masked()
    {
        await using var connection = await OpenAsync();
        var userId = await InsertUserAsync(connection);

        await ExecuteAsync(connection,
            "UPDATE \"User\" SET \"PasswordHash\" = 'x', \"ConcurrencyStamp\" = 'y' WHERE \"Id\" = @id",
            ("id", userId));

        var row = Assert.Single(await AuditRowsAsync(connection, "User", userId.ToString(), "UPDATE"));
        Assert.Equal("***", Text(row.OldData, "PasswordHash"));
        Assert.Equal("***", Text(row.NewData, "PasswordHash"));
        Assert.Equal("***", Text(row.OldData, "ConcurrencyStamp"));
        Assert.Equal("***", Text(row.NewData, "ConcurrencyStamp"));
        Assert.Equal("***", Text(row.NewData, "SecurityStamp"));
        Assert.NotNull(row.ChangedColumns);
        Assert.Contains("PasswordHash", row.ChangedColumns);
        Assert.DoesNotContain("ConcurrencyStamp", row.ChangedColumns);
    }

    [PgFact]
    public async Task Only_concurrency_stamp_change_is_not_logged()
    {
        await using var connection = await OpenAsync();
        var userId = await InsertUserAsync(connection);

        await ExecuteAsync(connection, "UPDATE \"User\" SET \"ConcurrencyStamp\" = 'z' WHERE \"Id\" = @id", ("id", userId));

        Assert.Empty(await AuditRowsAsync(connection, "User", userId.ToString(), "UPDATE"));
    }

    [PgFact]
    public async Task Excluded_tables_are_not_logged()
    {
        await using var connection = await OpenAsync();

        await ExecuteAsync(connection,
            "INSERT INTO \"JobRun\"(\"JobName\",\"LastRunDate\") VALUES (@name, CURRENT_DATE)",
            ("name", "Job" + Guid.NewGuid().ToString("N")));

        Assert.Equal(0L, await ScalarAsync<long>(connection, "SELECT count(*) FROM \"AuditLog\" WHERE \"TableName\" = 'JobRun'"));
    }

    [PgFact]
    public async Task AuditLog_rejects_update_delete_and_truncate()
    {
        await using var connection = await OpenAsync();
        var gradeId = await InsertGradeAsync(connection, "Sexto");
        var auditLogId = Assert.Single(await AuditRowsAsync(connection, "Grade", gradeId.ToString())).AuditLogId;

        string[] statements =
        [
            "UPDATE \"AuditLog\" SET \"Source\" = 'x' WHERE \"AuditLogId\" = @id",
            "DELETE FROM \"AuditLog\" WHERE \"AuditLogId\" = @id",
            "TRUNCATE \"AuditLog\"",
        ];
        foreach (var statement in statements)
        {
            var error = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(connection, statement, ("id", auditLogId)));
            Assert.Equal("La auditoría no se puede modificar ni eliminar.", error.MessageText);
        }

        var row = Assert.Single(await AuditRowsAsync(connection, "Grade", gradeId.ToString()));
        Assert.Null(row.Source);
    }

    [PgFact]
    public async Task Every_public_table_is_audited_or_excluded()
    {
        await using var connection = await OpenAsync();

        Assert.Empty(await UncoveredTablesAsync(connection));
    }

    [PgFact]
    public async Task Disabled_trigger_is_reported_as_uncovered()
    {
        await using var connection = await OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await ExecuteAsync(connection, "ALTER TABLE \"Grade\" DISABLE TRIGGER audit_row");

        Assert.Contains("Grade", await UncoveredTablesAsync(connection));
        await transaction.RollbackAsync();
    }

    [PgFact]
    public async Task Null_secret_is_not_masked()
    {
        await using var connection = await OpenAsync();
        var userId = await InsertUserAsync(connection, passwordHash: null);

        await ExecuteAsync(connection, "UPDATE \"User\" SET \"FirstName\" = 'Beto' WHERE \"Id\" = @id", ("id", userId));

        var row = Assert.Single(await AuditRowsAsync(connection, "User", userId.ToString(), "UPDATE"));
        Assert.Equal(new[] { "FirstName" }, row.ChangedColumns);
        Assert.True(row.OldData!.AsObject().ContainsKey("PasswordHash"));
        Assert.Null(row.OldData["PasswordHash"]);
        Assert.True(row.NewData!.AsObject().ContainsKey("PasswordHash"));
        Assert.Null(row.NewData["PasswordHash"]);
        Assert.Equal("***", Text(row.NewData, "SecurityStamp"));
    }

    [PgFact]
    public async Task DbUser_is_the_session_role_not_the_function_owner()
    {
        await using var connection = await OpenAsync();
        var sessionUser = await ScalarAsync<string>(connection, "SELECT session_user::text");
        var owner = "litaro_test_owner_" + Guid.NewGuid().ToString("N");
        await using var transaction = await connection.BeginTransactionAsync();

        await ExecuteAsync(connection, $"""
            CREATE ROLE "{owner}" NOLOGIN;
            GRANT "{owner}" TO CURRENT_USER;
            GRANT USAGE, CREATE ON SCHEMA public TO "{owner}";
            GRANT INSERT ON "AuditLog" TO "{owner}";
            CREATE POLICY litaro_test_owner_insert ON "AuditLog" FOR INSERT TO "{owner}" WITH CHECK (true);
            GRANT USAGE ON ALL SEQUENCES IN SCHEMA public TO "{owner}";
            ALTER FUNCTION audit_row_change() OWNER TO "{owner}";
            ALTER FUNCTION audit_write(text, text, text, jsonb, jsonb, text[], jsonb) OWNER TO "{owner}";
            """);
        var gradeId = await InsertGradeAsync(connection, "Sexto", transaction);

        var row = Assert.Single(await AuditRowsAsync(connection, "Grade", gradeId.ToString()));
        Assert.Equal(sessionUser, row.DbUser);
        Assert.NotEqual(owner, row.DbUser);
        await transaction.RollbackAsync();
    }

    [PgFact]
    public async Task Attach_on_a_table_without_trigger_does_not_wait_for_readers()
    {
        var table = "LitaroTestAttach" + Guid.NewGuid().ToString("N")[..8];
        await using var setup = await OpenAsync();
        await ExecuteAsync(setup, $"CREATE TABLE \"{table}\"(\"Id\" int PRIMARY KEY)");
        try
        {
            await using var reader = await OpenAsync();
            await using var readTransaction = await reader.BeginTransactionAsync();
            await ScalarAsync<long>(reader, readTransaction, $"SELECT count(*) FROM \"{table}\"");

            await using var attacher = await OpenAsync();
            await ExecuteAsync(attacher, $"SET lock_timeout = '2s'; SELECT audit_attach('{table}')");

            await readTransaction.RollbackAsync();
            Assert.DoesNotContain(table, await UncoveredTablesAsync(setup));
            await ExecuteAsync(setup, $"SELECT audit_attach('{table}')");
            Assert.Equal(1L, await ScalarAsync<long>(setup,
                $"SELECT count(*) FROM pg_trigger WHERE tgrelid = 'public.\"{table}\"'::regclass AND tgname = 'audit_row'"));
        }
        finally
        {
            await ExecuteAsync(setup, $"DROP TABLE IF EXISTS \"{table}\"");
        }
    }

    [PgFact]
    public async Task DbUser_under_set_role_is_the_session_role()
    {
        await using var connection = await OpenAsync();
        var sessionUser = await ScalarAsync<string>(connection, "SELECT session_user::text");
        var writer = "litaro_test_writer_" + Guid.NewGuid().ToString("N");
        await using var transaction = await connection.BeginTransactionAsync();

        await ExecuteAsync(connection, $"""
            CREATE ROLE "{writer}" NOLOGIN;
            GRANT "{writer}" TO CURRENT_USER;
            GRANT USAGE ON SCHEMA public TO "{writer}";
            GRANT INSERT, SELECT ON "Grade" TO "{writer}";
            GRANT USAGE ON ALL SEQUENCES IN SCHEMA public TO "{writer}";
            SET LOCAL ROLE "{writer}";
            """);
        var gradeId = await InsertGradeAsync(connection, "Sexto", transaction);
        var currentUser = await ScalarAsync<string>(connection, "SELECT current_user::text");
        await ExecuteAsync(connection, "RESET ROLE");

        var row = Assert.Single(await AuditRowsAsync(connection, "Grade", gradeId.ToString()));
        Assert.Equal(writer, currentUser);
        Assert.Equal(sessionUser, row.DbUser);
        await transaction.RollbackAsync();
    }

    [PgFact]
    public async Task Context_variables_are_recorded()
    {
        await using var connection = await OpenAsync();
        var operationId = Guid.NewGuid();

        await using var transaction = await connection.BeginTransactionAsync();
        await SetContextAsync(connection, transaction, UserContext(operationId));
        var gradeId = await InsertGradeAsync(connection, "Sexto", transaction);
        await transaction.CommitAsync();

        var row = Assert.Single(await AuditRowsAsync(connection, "Grade", gradeId.ToString()));
        Assert.Equal("USER", row.ActorKind);
        Assert.Equal(7, row.UserId);
        Assert.Equal("Ana Pérez", row.UserName);
        Assert.Equal("PUT /grades/1", row.Source);
        Assert.Equal("10.0.0.1", row.IpAddress);
        Assert.Equal("UA", row.UserAgent);
        Assert.Equal(operationId, row.OperationId);
    }

    [PgFact]
    public async Task App_without_actor_is_system_without_context()
    {
        await using var connection = await OpenAsync();

        await using var transaction = await connection.BeginTransactionAsync();
        await SetContextAsync(connection, transaction, new Dictionary<string, string>
        {
            ["litaro.app"] = "on",
            ["litaro.actor_kind"] = "",
            ["litaro.user_id"] = "",
            ["litaro.user_name"] = "",
            ["litaro.source"] = "",
            ["litaro.ip"] = "",
            ["litaro.user_agent"] = "",
            ["litaro.operation_id"] = "",
            ["litaro.action"] = "",
        });
        var gradeId = await InsertGradeAsync(connection, "Sexto", transaction);
        await transaction.CommitAsync();

        var row = Assert.Single(await AuditRowsAsync(connection, "Grade", gradeId.ToString()));
        Assert.Equal("SYSTEM", row.ActorKind);
        Assert.Equal("Litaro (sin contexto)", row.Source);
        Assert.Null(row.UserId);
        Assert.Null(row.UserName);
        Assert.Null(row.OperationId);
    }

    [PgFact]
    public async Task Variables_do_not_survive_the_transaction()
    {
        await using var connection = await OpenAsync();

        await using (var transaction = await connection.BeginTransactionAsync())
        {
            await SetContextAsync(connection, transaction, UserContext(Guid.NewGuid()));
            var firstId = await InsertGradeAsync(connection, "Sexto", transaction);
            await transaction.CommitAsync();
            Assert.Equal("USER", Assert.Single(await AuditRowsAsync(connection, "Grade", firstId.ToString())).ActorKind);
        }

        var secondId = await InsertGradeAsync(connection, "Séptimo");

        var row = Assert.Single(await AuditRowsAsync(connection, "Grade", secondId.ToString()));
        Assert.Equal("DIRECT_DB", row.ActorKind);
        Assert.Null(row.UserId);
        Assert.Null(row.UserName);
        Assert.Null(row.Source);
        Assert.Null(row.OperationId);
    }

    private sealed record AuditRow(
        long AuditLogId,
        string EventType,
        string? TableName,
        string? RecordId,
        JsonNode? OldData,
        JsonNode? NewData,
        string[]? ChangedColumns,
        string ActorKind,
        int? UserId,
        string? UserName,
        string DbUser,
        string? Source,
        string? IpAddress,
        string? UserAgent,
        Guid? OperationId);

    private static Dictionary<string, string> UserContext(Guid operationId) => new()
    {
        ["litaro.app"] = "on",
        ["litaro.actor_kind"] = "USER",
        ["litaro.user_id"] = "7",
        ["litaro.user_name"] = "Ana Pérez",
        ["litaro.source"] = "PUT /grades/1",
        ["litaro.ip"] = "10.0.0.1",
        ["litaro.user_agent"] = "UA",
        ["litaro.operation_id"] = operationId.ToString(),
        ["litaro.action"] = "",
    };

    private async Task<NpgsqlConnection> OpenAsync()
    {
        var connection = new NpgsqlConnection(fixture.Database.ConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    private static string? Text(JsonNode? data, string key) => data?[key]?.GetValue<string>();

    private static async Task SetContextAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Dictionary<string, string> values)
    {
        var calls = values.Keys.Select((_, i) => $"set_config(@n{i}, @v{i}, true)");
        await using var command = new NpgsqlCommand($"SELECT {string.Join(", ", calls)}", connection, transaction);
        var index = 0;
        foreach (var (name, value) in values)
        {
            command.Parameters.AddWithValue($"n{index}", name);
            command.Parameters.AddWithValue($"v{index}", value);
            index++;
        }

        await command.ExecuteNonQueryAsync();
    }

    private static Task<int> InsertGradeAsync(NpgsqlConnection connection, string name, NpgsqlTransaction? transaction = null)
    {
        var orderNum = (short)Interlocked.Increment(ref nextOrderNum);
        return ScalarAsync<int>(connection, transaction,
            "INSERT INTO \"Grade\"(\"Name\",\"OrderNum\",\"Level\") VALUES (@name, @orderNum, 'Secundaria') RETURNING \"GradeId\"",
            ("name", name), ("orderNum", orderNum));
    }

    private static Task<int> InsertUserAsync(NpgsqlConnection connection, string? passwordHash = "hash")
    {
        var key = Guid.NewGuid().ToString("N")[..16];
        return ScalarAsync<int>(connection, """
            WITH s AS (
                INSERT INTO "School"("Name","Nit","Address") VALUES ('Colegio', @key, 'Calle 1') RETURNING "SchoolId"
            ), c AS (
                INSERT INTO "Campus"("Name","Address","Dane","SchoolId")
                SELECT 'Sede', 'Calle 1', '125875000001', "SchoolId" FROM s RETURNING "CampusId"
            )
            INSERT INTO "User"("UserName","NormalizedUserName","DocumentType","DocumentNumber","FirstName","LastName",
                "EmailConfirmed","PhoneNumberConfirmed","TwoFactorEnabled","LockoutEnabled","AccessFailedCount",
                "MustChangePassword","PasswordHash","SecurityStamp","ConcurrencyStamp","CampusId")
            SELECT @key, upper(@key), 'CC', @key, 'Ana', 'Pérez',
                false, false, false, true, 0, false, @hash::text, 'security', 'concurrency', "CampusId" FROM c
            RETURNING "Id"
            """, ("key", key), ("hash", (object?)passwordHash ?? DBNull.Value));
    }

    private static async Task<List<AuditRow>> AuditRowsAsync(NpgsqlConnection connection, string table, string recordId, string? eventType = null)
    {
        await using var command = new NpgsqlCommand("""
            SELECT "AuditLogId","EventType","TableName","RecordId","OldData"::text,"NewData"::text,"ChangedColumns",
                   "ActorKind","UserId","UserName","DbUser","Source","IpAddress","UserAgent","OperationId"
              FROM "AuditLog"
             WHERE "TableName" = @table AND "RecordId" = @recordId AND (@eventType::text IS NULL OR "EventType" = @eventType)
             ORDER BY "AuditLogId"
            """, connection);
        command.Parameters.AddWithValue("table", table);
        command.Parameters.AddWithValue("recordId", recordId);
        command.Parameters.Add(new NpgsqlParameter<string?>("eventType", eventType) { NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Text });

        var rows = new List<AuditRow>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(new AuditRow(
                reader.GetInt64(0),
                reader.GetString(1),
                Field<string>(reader, 2),
                Field<string>(reader, 3),
                Field<string>(reader, 4) is { } oldData ? JsonNode.Parse(oldData) : null,
                Field<string>(reader, 5) is { } newData ? JsonNode.Parse(newData) : null,
                Field<string[]>(reader, 6),
                reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetInt32(8),
                Field<string>(reader, 9),
                reader.GetString(10),
                Field<string>(reader, 11),
                Field<string>(reader, 12),
                Field<string>(reader, 13),
                reader.IsDBNull(14) ? null : reader.GetGuid(14)));
        }

        return rows;
    }

    private static async Task<List<string>> UncoveredTablesAsync(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand("SELECT * FROM audit_uncovered_tables()", connection);
        await using var reader = await command.ExecuteReaderAsync();

        var tables = new List<string>();
        while (await reader.ReadAsync())
        {
            tables.Add(reader.GetString(0));
        }

        return tables;
    }

    private static T? Field<T>(NpgsqlDataReader reader, int ordinal) where T : class =>
        reader.IsDBNull(ordinal) ? null : reader.GetFieldValue<T>(ordinal);

    private static Task ExecuteAsync(NpgsqlConnection connection, string sql, params (string Name, object Value)[] parameters) =>
        ScalarAsync<object>(connection, null, sql, parameters);

    private static Task<T> ScalarAsync<T>(NpgsqlConnection connection, string sql, params (string Name, object Value)[] parameters) =>
        ScalarAsync<T>(connection, null, sql, parameters);

    private static async Task<T> ScalarAsync<T>(NpgsqlConnection connection, NpgsqlTransaction? transaction, string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return (T)(await command.ExecuteScalarAsync())!;
    }
}

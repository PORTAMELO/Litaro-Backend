using System.Data.Common;
using Litaro.Data;
using Litaro.Models;
using Litaro.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace Litaro.Tests.Pg;

public sealed class AuditContextPgTests(PgDatabaseFixture fixture) : IClassFixture<PgDatabaseFixture>
{
    private static int nextOrderNum;

    [PgFact]
    public async Task SaveChanges_carries_user_context()
    {
        var audit = UserAudit(3, "O'Brien Muñoz");
        var grade = NewGrade();

        await using (var db = fixture.Database.NewContext(audit))
        {
            db.Grades.Add(grade);
            await db.SaveChangesAsync();
        }

        var row = await InsertRowAsync(grade.GradeId);
        Assert.Equal("USER", row.ActorKind);
        Assert.Equal(3, row.UserId);
        Assert.Equal("O'Brien Muñoz", row.UserName);
        Assert.Equal("POST /grades", row.Source);
        Assert.Equal("1.1.1.1", row.IpAddress);
        Assert.Equal("UA", row.UserAgent);
        Assert.Equal(audit.OperationId, row.OperationId);
    }

    [PgFact]
    public async Task Large_save_is_fully_attributed()
    {
        var audit = UserAudit(3);
        var counter = new GradeInsertCounter();
        var grades = Enumerable.Range(0, 100).Select(_ => NewGrade()).ToList();

        await using (var db = fixture.Database.NewContext(audit, maxBatchSize: 10, observer: counter))
        {
            db.Grades.AddRange(grades);
            await db.SaveChangesAsync();
        }

        Assert.True(counter.Commands >= 10, $"Comandos INSERT: {counter.Commands}");
        await using var reader = fixture.Database.NewContext();
        var recordIds = grades.Select(g => g.GradeId.ToString()).ToList();
        var rows = await reader.AuditLogs
            .Where(a => a.TableName == "Grade" && a.EventType == "INSERT" && recordIds.Contains(a.RecordId!))
            .ToListAsync();

        Assert.Equal(100, rows.Count);
        Assert.Equal(100, rows.Select(r => r.RecordId).Distinct().Count());
        Assert.All(rows, row =>
        {
            Assert.Equal("USER", row.ActorKind);
            Assert.Equal(3, row.UserId);
            Assert.Equal(audit.OperationId, row.OperationId);
        });
    }

    [PgFact]
    public async Task Login_events_are_stored_on_postgres()
    {
        var audit = new AuditContext();
        audit.SetRequest("POST /auth/login", "1.1.1.1", "UA");
        audit.SetAnonymous();

        await using (var db = fixture.Database.NewContext(audit))
        {
            var service = new AuditService(db, audit);
            await service.LogAccessAsync(AuditEventTypes.LoginFail, null, "nadie@x.co", LoginAuditReasons.UserNotFound, DateTime.UtcNow);
            await service.LogAccessAsync(AuditEventTypes.LoginOk, 9, "O'Brien Muñoz", null, DateTime.UtcNow);
        }

        await using var connection = new NpgsqlConnection(fixture.Database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            SELECT a."EventType", a."TableName", a."ActorKind", a."UserId", a."UserName", a."DbUser",
                   a."Detail" ->> 'reason', jsonb_typeof(a."Detail"), a."Source", a."IpAddress", a."UserAgent",
                   current_user::text, session_user::text
              FROM "AuditLog" a
             WHERE a."OperationId" = @op
             ORDER BY a."AuditLogId"
            """, connection);
        command.Parameters.AddWithValue("op", audit.OperationId);
        var rows = new List<object?[]>();
        await using (var result = await command.ExecuteReaderAsync())
        {
            while (await result.ReadAsync())
            {
                var values = new object?[result.FieldCount];
                for (var i = 0; i < result.FieldCount; i++)
                    values[i] = result.IsDBNull(i) ? null : result.GetValue(i);
                rows.Add(values);
            }
        }

        Assert.Equal(2, rows.Count);
        var fail = rows[0];
        var ok = rows[1];
        Assert.Equal(fail[11], fail[12]);
        Assert.Equal(new object?[] { "LOGIN_FAIL", "Auth", "ANONYMOUS", null, "nadie@x.co", fail[11], "USER_NOT_FOUND", "object" }, fail[..8]);
        Assert.Equal(new object?[] { "POST /auth/login", "1.1.1.1", "UA" }, fail[8..11]);
        Assert.Equal(new object?[] { "LOGIN_OK", "Auth", "USER", 9, "O'Brien Muñoz", ok[11], null, null }, ok[..8]);
    }

    [PgFact]
    public async Task Context_does_not_leak_through_the_pool()
    {
        var grade = NewGrade();
        int contextPid;

        await using (var db = fixture.Database.NewContext(UserAudit(3), maxPoolSize: 1))
        {
            db.Grades.Add(grade);
            await db.SaveChangesAsync();
            contextPid = await db.Database.SqlQuery<int>($"SELECT pg_backend_pid() AS \"Value\"").SingleAsync();
        }

        await using var connection = new NpgsqlConnection(fixture.Database.PooledConnectionString(1));
        await connection.OpenAsync();
        await using var pid = new NpgsqlCommand("SELECT pg_backend_pid()", connection);
        Assert.Equal(contextPid, (int)(await pid.ExecuteScalarAsync())!);

        var order = NextOrderNum();
        await using var insert = new NpgsqlCommand(
            "INSERT INTO \"Grade\"(\"Name\",\"OrderNum\",\"Level\") VALUES ('Directo', @order, 'Secundaria') RETURNING \"GradeId\"",
            connection);
        insert.Parameters.AddWithValue("order", (short)order);
        var directId = (int)(await insert.ExecuteScalarAsync())!;

        Assert.Equal("USER", (await InsertRowAsync(grade.GradeId)).ActorKind);
        var direct = await InsertRowAsync(directId);
        Assert.Equal("DIRECT_DB", direct.ActorKind);
        Assert.Null(direct.UserId);
        Assert.Null(direct.OperationId);
    }

    [PgFact]
    public async Task Explicit_transaction_uses_latest_context()
    {
        var audit = UserAudit(3);
        var first = NewGrade();
        var second = NewGrade();

        await using (var db = fixture.Database.NewContext(audit))
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            db.Grades.Add(first);
            await db.SaveChangesAsync();
            audit.SetUser(4, "Otro Usuario");
            db.Grades.Add(second);
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        Assert.Equal(3, (await InsertRowAsync(first.GradeId)).UserId);
        var row = await InsertRowAsync(second.GradeId);
        Assert.Equal(4, row.UserId);
        Assert.Equal("Otro Usuario", row.UserName);
    }

    [PgFact]
    public async Task Same_context_twice_in_one_transaction_is_attributed()
    {
        var audit = UserAudit(3);
        var first = NewGrade();
        var second = NewGrade();

        await using (var db = fixture.Database.NewContext(audit))
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            db.Grades.Add(first);
            await db.SaveChangesAsync();
            db.Grades.Add(second);
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        foreach (var id in new[] { first.GradeId, second.GradeId })
        {
            var row = await InsertRowAsync(id);
            Assert.Equal("USER", row.ActorKind);
            Assert.Equal(3, row.UserId);
            Assert.Equal(audit.OperationId, row.OperationId);
        }
    }

    [PgFact]
    public async Task Consecutive_saves_without_explicit_transaction_are_attributed()
    {
        var audit = UserAudit(3);
        var first = NewGrade();
        var second = NewGrade();

        await using (var db = fixture.Database.NewContext(audit, maxPoolSize: 1))
        {
            db.Grades.Add(first);
            await db.SaveChangesAsync();
            db.Grades.Add(second);
            await db.SaveChangesAsync();
        }

        foreach (var id in new[] { first.GradeId, second.GradeId })
        {
            var row = await InsertRowAsync(id);
            Assert.Equal("USER", row.ActorKind);
            Assert.Equal(3, row.UserId);
        }
    }

    [PgFact]
    public async Task Reused_transaction_object_gets_the_context_again()
    {
        var audit = UserAudit(3);
        var first = NewGrade();
        var second = NewGrade();

        await using (var connection = new NpgsqlConnection(fixture.Database.ConnectionString))
        {
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(connection)
                .AddInterceptors(new AuditCommandInterceptor())
                .Options;
            await using var db = new AppDbContext(options, audit);

            await using var firstTransaction = await connection.BeginTransactionAsync();
            await db.Database.UseTransactionAsync(firstTransaction);
            db.Grades.Add(first);
            await db.SaveChangesAsync();
            await firstTransaction.CommitAsync();
            await db.Database.UseTransactionAsync(null);

            await using var secondTransaction = await connection.BeginTransactionAsync();
            Assert.Same(firstTransaction, secondTransaction);
            await db.Database.UseTransactionAsync(secondTransaction);
            db.Grades.Add(second);
            await db.SaveChangesAsync();
            await secondTransaction.CommitAsync();
        }

        foreach (var id in new[] { first.GradeId, second.GradeId })
        {
            var row = await InsertRowAsync(id);
            Assert.Equal("USER", row.ActorKind);
            Assert.Equal(3, row.UserId);
        }
    }

    [PgFact]
    public async Task Failed_save_inside_transaction_does_not_lose_context()
    {
        var existing = NewGrade();
        await using (var plain = fixture.Database.NewContext())
        {
            plain.Grades.Add(existing);
            await plain.SaveChangesAsync();
        }

        var audit = UserAudit(3);
        var valid = NewGrade();

        await using (var db = fixture.Database.NewContext(audit))
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            db.Grades.Add(new Grade { Name = "Duplicado", OrderNum = existing.OrderNum, Level = "Secundaria" });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            db.ChangeTracker.Clear();
            db.Grades.Add(valid);
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        var row = await InsertRowAsync(valid.GradeId);
        Assert.Equal("USER", row.ActorKind);
        Assert.Equal(3, row.UserId);
    }

    [PgFact]
    public async Task Bulk_update_is_attributed()
    {
        var grade = NewGrade();
        await using (var plain = fixture.Database.NewContext())
        {
            plain.Grades.Add(grade);
            await plain.SaveChangesAsync();
        }

        var audit = UserAudit(3);
        await using (var db = fixture.Database.NewContext(audit))
        {
            var updated = await db.Grades
                .Where(g => g.GradeId == grade.GradeId)
                .ExecuteUpdateAsync(s => s.SetProperty(g => g.Name, "Masivo"));
            Assert.Equal(1, updated);
        }

        var row = Assert.Single(await RowsAsync(grade.GradeId, "UPDATE"));
        Assert.Equal("USER", row.ActorKind);
        Assert.Equal(3, row.UserId);
        Assert.Equal(audit.OperationId, row.OperationId);
    }

    [PgFact]
    public async Task Concurrency_conflict_is_still_detected()
    {
        var grade = NewGrade();
        await using var db = fixture.Database.NewContext(UserAudit(3));
        db.Grades.Add(grade);
        await db.SaveChangesAsync();

        await using (var connection = new NpgsqlConnection(fixture.Database.ConnectionString))
        {
            await connection.OpenAsync();
            await using var delete = new NpgsqlCommand("DELETE FROM \"Grade\" WHERE \"GradeId\" = @id", connection);
            delete.Parameters.AddWithValue("id", grade.GradeId);
            await delete.ExecuteNonQueryAsync();
        }

        grade.Name = "Cambiado";

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => db.SaveChangesAsync());
    }

    [PgFact]
    public async Task Context_without_user_is_system()
    {
        var audit = new AuditContext();
        audit.SetSystem("AttendanceNightly");
        var grade = NewGrade();

        await using (var db = fixture.Database.NewContext(audit))
        {
            db.Grades.Add(grade);
            await db.SaveChangesAsync();
        }

        var row = await InsertRowAsync(grade.GradeId);
        Assert.Equal("SYSTEM", row.ActorKind);
        Assert.Equal("AttendanceNightly", row.Source);
        Assert.Null(row.UserId);
        Assert.Equal(audit.OperationId, row.OperationId);
    }

    private sealed class GradeInsertCounter : DbCommandInterceptor
    {
        private int commands;

        public int Commands => commands;

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("INSERT INTO public.\"Grade\"", StringComparison.Ordinal))
                Interlocked.Increment(ref commands);

            return ValueTask.FromResult(result);
        }
    }

    private static AuditContext UserAudit(int userId, string userName = "Ana Pérez")
    {
        var audit = new AuditContext();
        audit.SetRequest("POST /grades", "1.1.1.1", "UA");
        audit.SetUser(userId, userName);
        return audit;
    }

    private static byte NextOrderNum() => (byte)Interlocked.Increment(ref nextOrderNum);

    private static Grade NewGrade()
    {
        var order = NextOrderNum();
        return new Grade { Name = $"Grado {order}", OrderNum = order, Level = "Secundaria" };
    }

    private async Task<AuditLog> InsertRowAsync(int gradeId) => Assert.Single(await RowsAsync(gradeId, "INSERT"));

    private async Task<List<AuditLog>> RowsAsync(int gradeId, string eventType)
    {
        await using var db = fixture.Database.NewContext();
        var recordId = gradeId.ToString();
        return await db.AuditLogs
            .Where(a => a.TableName == "Grade" && a.RecordId == recordId && a.EventType == eventType)
            .ToListAsync();
    }
}

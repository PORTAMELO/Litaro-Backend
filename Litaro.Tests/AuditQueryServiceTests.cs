using Litaro.Data;
using Litaro.Models;
using Litaro.Services;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Tests;

public class AuditQueryServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 17, 15, 0, 0, DateTimeKind.Utc);

    private static AuditQuery Query(string category = "data", DateOnly? from = null, DateOnly? to = null,
        int? userId = null, string? tableName = null, string? recordId = null, Guid? operationId = null,
        int page = 1, int pageSize = 50, string? eventType = null) =>
        new(category, from, to, userId, eventType, tableName, recordId, operationId, page, pageSize);

    private static AuditLog Row(DateTime at, string eventType = "UPDATE", string? table = "Enrollment",
        string? recordId = "1", string actorKind = "USER", int? userId = 3, string? userName = "Ana Pérez",
        string? source = null, string dbUser = "app", Guid? operationId = null, string? oldData = null,
        string? newData = null, string[]? changed = null, string? detail = null) => new()
    {
        OccurredAt = at,
        EventType = eventType,
        TableName = table,
        RecordId = recordId,
        ActorKind = actorKind,
        UserId = userId,
        UserName = userName,
        DbUser = dbUser,
        Source = source,
        OperationId = operationId,
        OldData = oldData,
        NewData = newData,
        ChangedColumns = changed,
        Detail = detail,
    };

    private static async Task<(AppDbContext Db, AuditQueryService Svc)> SeedAsync(params AuditLog[] rows)
    {
        var db = TestDb.Create();
        db.AuditLogs.AddRange(rows);
        await db.SaveChangesAsync();
        return (db, new AuditQueryService(db));
    }

    [Fact]
    public async Task List_defaults_to_last_7_days()
    {
        var (_, svc) = await SeedAsync(Row(Now.AddDays(-3)), Row(Now.AddDays(-10)));

        var page = await svc.ListAsync(Query(), Now);

        Assert.Single(page.Items);
    }

    [Fact]
    public async Task List_rejects_more_than_93_days()
    {
        var (_, svc) = await SeedAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.ListAsync(Query(from: new DateOnly(2026, 1, 1), to: new DateOnly(2026, 4, 4)), Now));

        Assert.Equal("El rango máximo de consulta es de 93 días.", ex.Message);
        await svc.ListAsync(Query(from: new DateOnly(2026, 1, 1), to: new DateOnly(2026, 4, 3)), Now);
    }

    [Fact]
    public async Task List_rejects_inverted_range_and_invalid_category()
    {
        var (_, svc) = await SeedAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.ListAsync(Query(from: new DateOnly(2026, 11, 17), to: new DateOnly(2026, 11, 16)), Now));
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.ListAsync(Query("otra"), Now));
    }

    [Fact]
    public async Task From_and_to_are_colombia_days()
    {
        var inside = Row(new DateTime(2026, 11, 18, 4, 30, 0, DateTimeKind.Utc), recordId: "in");
        var before = Row(new DateTime(2026, 11, 17, 4, 59, 0, DateTimeKind.Utc), recordId: "before");
        var after = Row(new DateTime(2026, 11, 18, 5, 0, 0, DateTimeKind.Utc), recordId: "after");
        var (_, svc) = await SeedAsync(inside, before, after);
        var day = new DateOnly(2026, 11, 17);

        var page = await svc.ListAsync(Query(from: day, to: day), Now);

        Assert.Equal(["in"], page.Items.Select(i => i.RecordId));
    }

    [Fact]
    public async Task Category_filters_event_types()
    {
        var at = Now.AddHours(-1);
        var (_, svc) = await SeedAsync(
            Row(at, "INSERT"), Row(at, "UPDATE"), Row(at, "DELETE"),
            Row(at, "LOGIN_OK", "Auth"), Row(at, "LOGIN_FAIL", "Auth"), Row(at, "LOGOUT", "Auth"),
            Row(at, "DOC_UPLOAD", "Document"), Row(at, "DOC_REPLACE", "Document"),
            Row(at, "DOC_REPLACED", "Document"), Row(at, "DOC_REMOVE", "Document"));

        var data = await svc.ListAsync(Query("data"), Now);
        var access = await svc.ListAsync(Query("access"), Now);
        var document = await svc.ListAsync(Query("document"), Now);

        Assert.Equal(["DELETE", "INSERT", "UPDATE"], data.Items.Select(i => i.EventType).Order());
        Assert.Equal(["LOGIN_FAIL", "LOGIN_OK", "LOGOUT"], access.Items.Select(i => i.EventType).Order());
        Assert.Equal(["DOC_REMOVE", "DOC_REPLACE", "DOC_UPLOAD"], document.Items.Select(i => i.EventType).Order());
    }

    [Fact]
    public async Task Filters_by_user_table_record_and_operation()
    {
        var at = Now.AddHours(-1);
        var op = Guid.NewGuid();
        var (_, svc) = await SeedAsync(
            Row(at, userId: 3, table: "Enrollment", recordId: "10", operationId: op),
            Row(at, userId: 4, table: "Enrollment", recordId: "10"),
            Row(at, userId: 3, table: "Grade", recordId: "10"),
            Row(at, userId: 3, table: "Enrollment", recordId: "11"));

        Assert.Equal(3, (await svc.ListAsync(Query(userId: 3), Now)).Items.Count);
        Assert.Equal(3, (await svc.ListAsync(Query(tableName: "Enrollment"), Now)).Items.Count);
        Assert.Equal(3, (await svc.ListAsync(Query(recordId: "10"), Now)).Items.Count);
        Assert.Single((await svc.ListAsync(Query(operationId: op), Now)).Items);
        Assert.Single((await svc.ListAsync(Query(userId: 3, tableName: "Enrollment", recordId: "10"), Now)).Items);
    }

    [Fact]
    public async Task Results_are_newest_first_and_page_has_more()
    {
        var rows = Enumerable.Range(0, 51).Select(i => i * 7 % 51).Select(i => Row(Now.AddMinutes(-i - 1), recordId: i.ToString())).ToArray();
        var (_, svc) = await SeedAsync(rows);

        var first = await svc.ListAsync(Query(), Now);
        var second = await svc.ListAsync(Query(page: 2), Now);

        Assert.Equal(50, first.Items.Count);
        Assert.True(first.HasMore);
        Assert.Equal("0", first.Items[0].RecordId);
        Assert.Single(second.Items);
        Assert.False(second.HasMore);
        Assert.Equal("50", second.Items[0].RecordId);
    }

    [Fact]
    public async Task Ties_on_time_are_ordered_by_higher_id_first()
    {
        var at = Now.AddHours(-1);
        var (_, svc) = await SeedAsync(
            Row(at, recordId: "a"), Row(at, recordId: "b"), Row(at, recordId: "c"), Row(Now.AddHours(-2), recordId: "old"));

        var page = await svc.ListAsync(Query(), Now);

        Assert.Equal(["c", "b", "a", "old"], page.Items.Select(i => i.RecordId));
    }

    [Fact]
    public async Task Day_start_boundary_is_included()
    {
        var day = new DateOnly(2026, 11, 17);
        var (_, svc) = await SeedAsync(
            Row(new DateTime(2026, 11, 17, 5, 0, 0, DateTimeKind.Utc), recordId: "start"),
            Row(new DateTime(2026, 11, 17, 4, 59, 59, DateTimeKind.Utc), recordId: "prev"));

        var page = await svc.ListAsync(Query(from: day, to: day), Now);

        Assert.Equal(["start"], page.Items.Select(i => i.RecordId));
    }

    [Fact]
    public async Task Default_window_covers_seven_colombia_days()
    {
        var (_, svc) = await SeedAsync(
            Row(new DateTime(2026, 11, 11, 5, 0, 0, DateTimeKind.Utc), recordId: "day-6"),
            Row(new DateTime(2026, 11, 11, 4, 59, 0, DateTimeKind.Utc), recordId: "day-7"));

        var page = await svc.ListAsync(Query(), Now);

        Assert.Equal(["day-6"], page.Items.Select(i => i.RecordId));
    }

    [Fact]
    public async Task Detail_masks_only_the_masked_side()
    {
        var (_, svc) = await SeedAsync(Row(Now.AddHours(-1), table: "User",
            oldData: """{"PasswordHash":"***","SecurityStamp":null}""",
            newData: """{"PasswordHash":null,"SecurityStamp":"***"}""",
            changed: ["PasswordHash", "SecurityStamp"]));

        var changes = (await svc.GetAsync(1))!.Changes;

        var hash = changes.Single(c => c.Column == "PasswordHash");
        Assert.True(hash.Masked);
        Assert.True(hash.BeforeMasked);
        Assert.False(hash.AfterMasked);
        Assert.Null(hash.Before);
        Assert.Null(hash.After);
        var stamp = changes.Single(c => c.Column == "SecurityStamp");
        Assert.True(stamp.Masked);
        Assert.False(stamp.BeforeMasked);
        Assert.True(stamp.AfterMasked);
    }

    [Fact]
    public async Task Detail_non_secret_column_with_mask_text_is_returned()
    {
        var (_, svc) = await SeedAsync(Row(Now.AddHours(-1), "INSERT", newData: """{"Note":"***"}"""));

        var change = Assert.Single((await svc.GetAsync(1))!.Changes);

        Assert.False(change.Masked);
        Assert.False(change.AfterMasked);
        Assert.Equal("***", change.After);
    }

    [Fact]
    public async Task Huge_page_is_rejected_with_message()
    {
        var (_, svc) = await SeedAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.ListAsync(Query(page: int.MaxValue, pageSize: 100), Now));

        Assert.False(string.IsNullOrWhiteSpace(ex.Message));
    }

    [Theory]
    [InlineData("0001-01-01", null)]
    [InlineData(null, "9999-12-31")]
    [InlineData("0001-01-01", "0001-01-01")]
    [InlineData("9999-12-31", "9999-12-31")]
    [InlineData("0001-01-01", "9999-12-31")]
    public async Task Extreme_dates_are_rejected_with_message(string? from, string? to)
    {
        var (_, svc) = await SeedAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.ListAsync(
            Query(from: from is null ? null : DateOnly.Parse(from), to: to is null ? null : DateOnly.Parse(to)), Now));
    }

    [Fact]
    public async Task Malformed_json_does_not_throw()
    {
        var at = Now.AddHours(-1);
        var (_, svc) = await SeedAsync(
            Row(at, "INSERT", detail: "{not json", oldData: "[1]", newData: "{broken"),
            Row(at, "UPDATE", detail: "[1]", oldData: "\"x\"", newData: "[1]", changed: ["A"]),
            Row(at, "DELETE", detail: "\"x\"", oldData: "null", newData: ""),
            Row(at, "DOC_UPLOAD", "Document", detail: "{not json"));

        var data = await svc.ListAsync(Query("data"), Now);
        var documents = await svc.ListAsync(Query("document"), Now);

        Assert.Equal(3, data.Items.Count);
        Assert.Single(documents.Items);
        for (var id = 1L; id <= 4; id++)
        {
            var detail = await svc.GetAsync(id);
            Assert.NotNull(detail);
            Assert.Empty(detail.AllFields);
        }
    }

    [Fact]
    public async Task Actor_uses_stored_snapshot()
    {
        var (_, svc) = await SeedAsync(Row(Now.AddHours(-1), userId: 99, userName: "Ana Pérez"));

        var page = await svc.ListAsync(Query(), Now);

        Assert.Equal("Ana Pérez", page.Items[0].Actor);
    }

    [Fact]
    public async Task Actor_without_stored_name_uses_user_number()
    {
        var (_, svc) = await SeedAsync(Row(Now.AddHours(-1), userId: 7, userName: null));

        Assert.Equal("Usuario #7", (await svc.ListAsync(Query(), Now)).Items[0].Actor);
    }

    [Fact]
    public async Task Actor_labels_for_system_direct_and_anonymous()
    {
        var at = Now.AddHours(-1);
        var (_, svc) = await SeedAsync(
            Row(at.AddMinutes(-1), actorKind: "SYSTEM", userId: null, userName: null, source: "AttendanceNightly"),
            Row(at.AddMinutes(-2), actorKind: "DIRECT_DB", userId: null, userName: null, dbUser: "postgres"),
            Row(at.AddMinutes(-3), actorKind: "ANONYMOUS", userId: null, userName: null),
            Row(at.AddMinutes(-4), actorKind: "ANONYMOUS", userId: null, userName: "maria@x.co"));

        var actors = (await svc.ListAsync(Query(), Now)).Items.Select(i => i.Actor).ToList();

        Assert.Equal(["Sistema · AttendanceNightly", "BD directa (postgres)", "Anónimo", "maria@x.co"], actors);
    }

    [Fact]
    public async Task Changed_labels_use_column_alias()
    {
        var (db, svc) = await SeedAsync(Row(Now.AddHours(-1), changed: ["Status", "Reason"]));
        db.ColumnConfigurations.Add(new ColumnConfiguration { TableName = "Enrollment", ColumnName = "Status", Alias = "Estado" });
        await db.SaveChangesAsync();

        var item = (await svc.ListAsync(Query(), Now)).Items[0];

        Assert.Equal(["Estado", "Reason"], item.ChangedLabels);
        Assert.Equal("Matrícula", item.TableLabel);
    }

    [Fact]
    public async Task Detail_update_returns_only_changed_columns()
    {
        var (_, svc) = await SeedAsync(Row(Now.AddHours(-1),
            oldData: """{"A":1,"Status":"ACTIVE","Note":"x"}""",
            newData: """{"A":1,"Status":"WITHDRAWN","Note":"y"}""",
            changed: ["Note", "Status"]));

        var detail = (await svc.GetAsync(1))!;

        Assert.Equal(["Status", "Note"], detail.Changes.Select(c => c.Column));
        Assert.Equal("ACTIVE", detail.Changes[0].Before);
        Assert.Equal("WITHDRAWN", detail.Changes[0].After);
        Assert.Equal(["A", "Status", "Note"], detail.AllFields.Select(c => c.Column));
        Assert.Equal("1", detail.AllFields[0].Before);
        Assert.Equal("app", detail.DbUser);
    }

    [Fact]
    public async Task Detail_insert_has_only_after_and_delete_only_before()
    {
        var (_, svc) = await SeedAsync(
            Row(Now.AddHours(-2), "INSERT", newData: """{"Id":5,"Active":true,"Tags":["a"]}"""),
            Row(Now.AddHours(-1), "DELETE", oldData: """{"Id":5,"Active":true}"""));

        var insert = (await svc.GetAsync(1))!;
        var delete = (await svc.GetAsync(2))!;

        Assert.All(insert.Changes, c => Assert.Null(c.Before));
        Assert.Equal(["5", "true", """["a"]"""], insert.Changes.Select(c => c.After));
        Assert.All(delete.Changes, c => Assert.Null(c.After));
        Assert.Equal(["5", "true"], delete.Changes.Select(c => c.Before));
    }

    [Fact]
    public async Task Detail_null_values_are_null()
    {
        var (_, svc) = await SeedAsync(Row(Now.AddHours(-1), "INSERT", newData: """{"Reason":null}"""));

        var change = Assert.Single((await svc.GetAsync(1))!.Changes);

        Assert.Null(change.After);
        Assert.False(change.Masked);
    }

    [Fact]
    public async Task Detail_masks_secret_columns()
    {
        var (_, svc) = await SeedAsync(Row(Now.AddHours(-1), table: "User",
            oldData: """{"PasswordHash":"***","SecurityStamp":null}""",
            newData: """{"PasswordHash":"***","SecurityStamp":null}""",
            changed: ["PasswordHash"]));

        var detail = (await svc.GetAsync(1))!;

        var hash = Assert.Single(detail.Changes);
        Assert.True(hash.Masked);
        Assert.Null(hash.Before);
        Assert.Null(hash.After);
        Assert.False(detail.AllFields.Single(f => f.Column == "SecurityStamp").Masked);
    }

    [Fact]
    public async Task Detail_missing_event_is_null()
    {
        var (_, svc) = await SeedAsync();

        Assert.Null(await svc.GetAsync(5));
    }

    [Fact]
    public async Task Detail_replacement_includes_previous_document()
    {
        var op = Guid.NewGuid();
        var at = Now.AddHours(-1);
        const string previousDetail = """{"DocumentType":"ID","FileName":"old.pdf","EntityTable":"Student","EntityId":8}""";
        const string otherDetail = """{"DocumentType":"ID","FileName":"other.pdf","EntityTable":"Student","EntityId":9}""";
        const string currentDetail = """{"DocumentType":"ID","FileName":"new.pdf","EntityTable":"Student","EntityId":8,"role":"current"}""";
        var (_, svc) = await SeedAsync(
            Row(at, "DOC_REPLACED", "Document", "1", operationId: op, detail: previousDetail),
            Row(at, "DOC_REPLACED", "Document", "2", operationId: op, detail: otherDetail),
            Row(at, "DOC_REPLACE", "Document", "3", operationId: op, detail: currentDetail));

        var detail = (await svc.GetAsync(3))!;

        Assert.Equal(1, detail.PreviousDocument!.Id);
        Assert.Equal("old.pdf", detail.PreviousDocument.FileName);
        Assert.Equal("new.pdf", detail.Summary.FileName);
        Assert.Equal("Estudiantes #8", detail.Summary.EntityLabel);
    }

    [Fact]
    public async Task Detail_replacement_picks_closest_preceding_previous()
    {
        var op = Guid.NewGuid();
        var at = Now.AddHours(-1);
        const string d = """{"DocumentType":"ID","FileName":"a.pdf","EntityTable":"Student","EntityId":8}""";
        const string c = """{"DocumentType":"ID","FileName":"c.pdf","EntityTable":"Student","EntityId":8}""";
        var (_, svc) = await SeedAsync(
            Row(at, "DOC_REPLACED", "Document", "1", operationId: op, detail: d),
            Row(at, "DOC_REPLACED", "Document", "2", operationId: op, detail: d),
            Row(at, "DOC_REPLACE", "Document", "3", operationId: op, detail: c));

        Assert.Equal(2, (await svc.GetAsync(3))!.PreviousDocument!.Id);
    }

    [Fact]
    public async Task Detail_lists_related_events_of_same_operation()
    {
        var op = Guid.NewGuid();
        var (_, svc) = await SeedAsync(
            Row(Now.AddMinutes(-5), operationId: op),
            Row(Now.AddMinutes(-3), operationId: op),
            Row(Now.AddMinutes(-4), operationId: op),
            Row(Now.AddMinutes(-1), operationId: Guid.NewGuid()));

        var detail = (await svc.GetAsync(1))!;

        Assert.Equal([3L, 2L], detail.Related.Select(r => r.Id));
        Assert.Equal(op, detail.OperationId);
    }

    [Fact]
    public async Task Access_summary_has_reason()
    {
        var (_, svc) = await SeedAsync(Row(Now.AddHours(-1), "LOGIN_FAIL", "Auth", null,
            detail: """{"reason":"WRONG_PASSWORD"}"""));

        var item = (await svc.ListAsync(Query("access"), Now)).Items[0];
        var detail = (await svc.GetAsync(1))!;

        Assert.Equal("WRONG_PASSWORD", item.Reason);
        Assert.Empty(detail.AllFields);
        Assert.Empty(detail.Changes);
    }

    [Fact]
    public async Task Document_summary_marks_moves()
    {
        var (_, svc) = await SeedAsync(Row(Now.AddHours(-1), "DOC_UPDATE", "Document", "4",
            detail: """{"DocumentType":"ID","FileName":"f.pdf","EntityTable":"Student","EntityId":8,"movedFrom":{"EntityTable":"Student","EntityId":7}}"""));

        var item = (await svc.ListAsync(Query("document"), Now)).Items[0];

        Assert.True(item.Moved);
        Assert.Equal("ID", item.DocumentType);
        Assert.Equal($"{AuditLabels.Table("Student")} #8", item.EntityLabel);
        Assert.Equal($"{AuditLabels.Table("Student")} #7", item.MovedFromLabel);
    }

    [Fact]
    public async Task Document_summary_without_move_has_no_moved_from_label()
    {
        var (_, svc) = await SeedAsync(Row(Now.AddHours(-1), "DOC_UPDATE", "Document", "4",
            detail: """{"DocumentType":"ID","FileName":"f.pdf","EntityTable":"Student","EntityId":8}"""));

        var item = (await svc.ListAsync(Query("document"), Now)).Items[0];

        Assert.False(item.Moved);
        Assert.Null(item.MovedFromLabel);
    }

    [Fact]
    public async Task Search_users_needs_two_characters_and_matches_fields()
    {
        var db = TestDb.Create();
        db.Users.Add(new User { FirstName = "Ana", LastName = "Pérez", DocumentNumber = "123456", UserName = "aperez@x.co", DocumentType = "CC" });
        db.Users.Add(new User { FirstName = "Luis", LastName = "Gómez", DocumentNumber = "999", UserName = "lg", DocumentType = "CC" });
        await db.SaveChangesAsync();
        var svc = new AuditQueryService(db);

        Assert.Empty(await svc.SearchUsersAsync("a"));
        Assert.Single(await svc.SearchUsersAsync("ANA"));
        Assert.Single(await svc.SearchUsersAsync("pérez"));
        Assert.Single(await svc.SearchUsersAsync("1234"));
        Assert.Single(await svc.SearchUsersAsync("aperez@"));
    }

    [Fact]
    public async Task Search_users_matches_full_name()
    {
        var db = TestDb.Create();
        db.Users.Add(new User { FirstName = "Ana", LastName = "Pérez", DocumentNumber = "123456", UserName = "aperez@x.co", DocumentType = "CC" });
        db.Users.Add(new User { FirstName = "Ana", LastName = "Gómez", DocumentNumber = "999", UserName = "agomez", DocumentType = "CC" });
        await db.SaveChangesAsync();
        var svc = new AuditQueryService(db);

        var found = Assert.Single(await svc.SearchUsersAsync("Ana Pérez"));
        Assert.Equal("Ana Pérez", found.Name);
        Assert.Single(await svc.SearchUsersAsync("  ana PÉREZ "));
        Assert.Empty(await svc.SearchUsersAsync("Ana Ruiz"));
    }

    [Fact]
    public async Task Actor_for_system_without_source_says_without_context()
    {
        var at = Now.AddHours(-1);
        var (_, svc) = await SeedAsync(
            Row(at.AddMinutes(-1), actorKind: "SYSTEM", userId: null, userName: null, source: null),
            Row(at.AddMinutes(-2), actorKind: "SYSTEM", userId: null, userName: null, source: ""));

        var actors = (await svc.ListAsync(Query(), Now)).Items.Select(i => i.Actor).ToList();

        Assert.Equal(["Sistema · Litaro (sin contexto)", "Sistema · Litaro (sin contexto)"], actors);
    }

    [Fact]
    public async Task Detail_changed_flag_is_true_only_for_changed_columns_of_an_update()
    {
        var (_, svc) = await SeedAsync(
            Row(Now.AddHours(-3), table: "User",
                oldData: """{"FirstName":"Ana","PasswordHash":"***"}""",
                newData: """{"FirstName":"Beto","PasswordHash":"***"}""",
                changed: ["FirstName"]),
            Row(Now.AddHours(-2), table: "User",
                oldData: """{"FirstName":"Beto","PasswordHash":"***"}""",
                newData: """{"FirstName":"Beto","PasswordHash":"***"}""",
                changed: ["PasswordHash"]),
            Row(Now.AddHours(-1), "INSERT", table: "User",
                newData: """{"FirstName":"Ana","PasswordHash":"***"}"""));

        var nameEdit = (await svc.GetAsync(1))!;
        var passwordEdit = (await svc.GetAsync(2))!;
        var insert = (await svc.GetAsync(3))!;

        Assert.False(nameEdit.AllFields.Single(f => f.Column == "PasswordHash").Changed);
        Assert.True(nameEdit.AllFields.Single(f => f.Column == "FirstName").Changed);
        Assert.True(Assert.Single(nameEdit.Changes).Changed);
        Assert.True(Assert.Single(passwordEdit.Changes).Changed);
        Assert.False(passwordEdit.AllFields.Single(f => f.Column == "FirstName").Changed);
        Assert.All(insert.Changes, c => Assert.False(c.Changed));
        Assert.True(insert.Changes.Single(c => c.Column == "PasswordHash").Masked);
    }

    [Fact]
    public void Options_lists_tables_and_event_types()
    {
        var svc = new AuditQueryService(TestDb.Create());

        var options = svc.Options();

        Assert.Equal(["tables", "eventTypes"], options.GetType().GetProperties().Select(p => p.Name));
        Assert.Equal("Matrícula", AuditLabels.Table("Enrollment"));
        Assert.Equal("Desconocida", AuditLabels.Table("Desconocida"));
    }

    [Fact]
    public void Options_exclude_replaced_events_and_non_data_tables()
    {
        var svc = new AuditQueryService(TestDb.Create());

        var json = System.Text.Json.JsonSerializer.SerializeToElement(
            svc.Options(), new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        var tables = json.GetProperty("tables").EnumerateArray().Select(t => t.GetProperty("value").GetString()).ToList();
        var documentTypes = json.GetProperty("eventTypes").GetProperty("document").EnumerateArray()
            .Select(t => t.GetProperty("value").GetString()).ToList();

        Assert.DoesNotContain("DOC_REPLACED", documentTypes);
        Assert.Contains("DOC_REPLACE", documentTypes);
        Assert.DoesNotContain("Auth", tables);
        Assert.DoesNotContain("Document", tables);
        Assert.Contains("Enrollment", tables);
    }
}

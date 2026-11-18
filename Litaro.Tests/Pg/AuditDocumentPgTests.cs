using System.Text.Json.Nodes;
using Litaro.Models;
using Litaro.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Litaro.Tests.Pg;

public sealed class AuditDocumentPgTests(PgDatabaseFixture fixture) : IClassFixture<PgDatabaseFixture>
{
    private static readonly DateTime Now = new(2026, 2, 1, 15, 0, 0, DateTimeKind.Utc);
    private static int nextEntityId = 1000;

    private readonly FakeDocumentStorage storage = new();

    [PgFact]
    public async Task Upload_new_is_doc_upload()
    {
        var userId = await UserIdAsync();
        var entityId = NextEntityId();
        var audit = UserAudit();

        var dto = await UploadAsync(audit, DocumentTypes.StudyPlanner, entityId, FakeDocumentStorage.Pdf("plan.pdf"), userId);

        var rows = await DocumentRowsAsync(dto.DocumentId);
        var row = Assert.Single(rows);
        Assert.Equal("DOC_UPLOAD", row.EventType);
        Assert.Equal("Document", row.TableName);
        Assert.Equal(dto.DocumentId.ToString(), row.RecordId);
        Assert.Equal("USER", row.ActorKind);
        Assert.Equal(3, row.UserId);
        Assert.Equal("Ana Pérez", row.UserName);
        Assert.Equal(audit.OperationId, row.OperationId);
        Assert.Null(row.OldData);
        Assert.NotNull(row.NewData);

        var detail = Detail(row);
        Assert.Equal(DocumentTypes.StudyPlanner, detail["DocumentType"]!.GetValue<string>());
        Assert.Equal("plan.pdf", detail["FileName"]!.GetValue<string>());
        Assert.Equal("AcademicAssignment", detail["EntityTable"]!.GetValue<string>());
        Assert.Equal(entityId, detail["EntityId"]!.GetValue<int>());
        Assert.Equal("Grades", detail["Module"]!.GetValue<string>());
        Assert.Equal(dto.SizeBytes, detail["SizeBytes"]!.GetValue<long>());
        Assert.False(detail.ContainsKey("role"));
        Assert.False(detail.ContainsKey("movedFrom"));

        Assert.DoesNotContain(rows, r => r.EventType is "INSERT" or "UPDATE");
    }

    [PgFact]
    public async Task Upload_over_existing_is_one_replacement()
    {
        var userId = await UserIdAsync();
        var entityId = NextEntityId();

        var first = await UploadAsync(UserAudit(), DocumentTypes.StudyPlanner, entityId, FakeDocumentStorage.Pdf("v1.pdf"), userId);
        var audit = UserAudit();
        var second = await UploadAsync(audit, DocumentTypes.StudyPlanner, entityId, FakeDocumentStorage.Pdf("v2.pdf"), userId);

        var rows = await OperationRowsAsync(audit.OperationId);
        Assert.Equal(2, rows.Count);

        var previous = Assert.Single(rows, r => r.EventType == "DOC_REPLACED");
        Assert.Equal(first.DocumentId.ToString(), previous.RecordId);
        Assert.Equal("previous", Detail(previous)["role"]!.GetValue<string>());
        Assert.Equal("v1.pdf", Detail(previous)["FileName"]!.GetValue<string>());

        var current = Assert.Single(rows, r => r.EventType == "DOC_REPLACE");
        Assert.Equal(second.DocumentId.ToString(), current.RecordId);
        Assert.Equal("current", Detail(current)["role"]!.GetValue<string>());
        Assert.Equal("v2.pdf", Detail(current)["FileName"]!.GetValue<string>());
        Assert.Equal(previous.OperationId, current.OperationId);
    }

    [PgFact]
    public async Task RemoveCurrent_is_doc_remove()
    {
        var userId = await UserIdAsync();
        var entityId = NextEntityId();
        var dto = await UploadAsync(UserAudit(), DocumentTypes.StudyPlanner, entityId, FakeDocumentStorage.Pdf(), userId);
        var audit = UserAudit();

        await using (var db = fixture.Database.NewContext(audit))
        {
            Assert.True(await new DocumentService(db, storage, audit).RemoveCurrentAsync(DocumentTypes.StudyPlanner, entityId, userId, Now.AddHours(1)));
        }

        var row = Assert.Single(await OperationRowsAsync(audit.OperationId));
        Assert.Equal("DOC_REMOVE", row.EventType);
        Assert.Equal(dto.DocumentId.ToString(), row.RecordId);
        Assert.False(Detail(row).ContainsKey("role"));
        Assert.Contains("IsCurrent", row.ChangedColumns!);
    }

    [PgFact]
    public async Task Reassign_without_target_is_doc_update_with_moved_from()
    {
        var userId = await UserIdAsync();
        var admissionId = NextEntityId();
        var studentId = NextEntityId();
        var rule = DocumentTypes.Enrollment(1, DocumentTypes.AdmissionTable);
        var dto = await UploadAsync(UserAudit(), rule, admissionId, FakeDocumentStorage.Pdf("a.pdf"), userId);
        var audit = UserAudit();

        Assert.Equal(1, await ReassignAsync(audit, admissionId, studentId, userId));

        var row = Assert.Single(await OperationRowsAsync(audit.OperationId));
        Assert.Equal("DOC_UPDATE", row.EventType);
        Assert.Equal(dto.DocumentId.ToString(), row.RecordId);
        var detail = Detail(row);
        Assert.Equal(DocumentTypes.StudentTable, detail["EntityTable"]!.GetValue<string>());
        Assert.Equal(studentId, detail["EntityId"]!.GetValue<int>());
        Assert.False(detail.ContainsKey("role"));
        Assert.True(JsonNode.DeepEquals(
            new JsonObject { ["EntityTable"] = DocumentTypes.AdmissionTable, ["EntityId"] = admissionId },
            detail["movedFrom"]));
    }

    [PgFact]
    public async Task Reassign_over_existing_is_replacement()
    {
        var userId = await UserIdAsync();
        var admissionId = NextEntityId();
        var studentId = NextEntityId();
        var moving = await UploadAsync(UserAudit(), DocumentTypes.Enrollment(1, DocumentTypes.AdmissionTable), admissionId, FakeDocumentStorage.Pdf("nuevo.pdf"), userId);
        var target = await UploadAsync(UserAudit(), DocumentTypes.Enrollment(1, DocumentTypes.StudentTable), studentId, FakeDocumentStorage.Pdf("viejo.pdf"), userId);
        var audit = UserAudit();

        Assert.Equal(1, await ReassignAsync(audit, admissionId, studentId, userId));

        var rows = await OperationRowsAsync(audit.OperationId);
        Assert.Equal(2, rows.Count);

        var previous = Assert.Single(rows, r => r.EventType == "DOC_REPLACED");
        Assert.Equal(target.DocumentId.ToString(), previous.RecordId);
        Assert.Equal("previous", Detail(previous)["role"]!.GetValue<string>());

        var current = Assert.Single(rows, r => r.EventType == "DOC_REPLACE");
        Assert.Equal(moving.DocumentId.ToString(), current.RecordId);
        var detail = Detail(current);
        Assert.Equal("current", detail["role"]!.GetValue<string>());
        Assert.Equal(studentId, detail["EntityId"]!.GetValue<int>());
        Assert.True(JsonNode.DeepEquals(
            new JsonObject { ["EntityTable"] = DocumentTypes.AdmissionTable, ["EntityId"] = admissionId },
            detail["movedFrom"]));
    }

    [PgFact]
    public async Task Purge_is_doc_purge()
    {
        var userId = await UserIdAsync();
        var document = new Document
        {
            Module = "Grades",
            DocumentType = DocumentTypes.StudyPlanner,
            EntityTable = "AcademicAssignment",
            EntityId = NextEntityId(),
            StoragePath = $"Grades/purga/{Guid.NewGuid():N}.pdf",
            FileName = "retirado.pdf",
            ContentType = "application/pdf",
            SizeBytes = 10,
            IsCurrent = false,
            UploadedById = userId,
            UploadedAt = Now,
        };
        await using (var plain = fixture.Database.NewContext())
        {
            plain.Documents.Add(document);
            await plain.SaveChangesAsync();
        }

        var audit = new AuditContext();
        audit.SetSystem("DocumentPurge");
        await using (var db = fixture.Database.NewContext(audit))
        {
            Assert.True(await new DocumentService(db, storage, audit).PurgeRemovedAsync(Now) >= 1);
        }

        var row = Assert.Single(await OperationRowsAsync(audit.OperationId), r => r.RecordId == document.DocumentId.ToString());
        Assert.Equal("DOC_PURGE", row.EventType);
        Assert.Equal("SYSTEM", row.ActorKind);
        Assert.Equal("DocumentPurge", row.Source);
        Assert.Null(row.UserId);
        Assert.Equal(new[] { "FileRemovedAt" }, row.ChangedColumns);
    }

    [PgFact]
    public async Task Direct_delete_is_doc_remove_direct_db()
    {
        var userId = await UserIdAsync();
        var dto = await UploadAsync(UserAudit(), DocumentTypes.StudyPlanner, NextEntityId(), FakeDocumentStorage.Pdf(), userId);

        await using (var connection = new NpgsqlConnection(fixture.Database.ConnectionString))
        {
            await connection.OpenAsync();
            await using var delete = new NpgsqlCommand("DELETE FROM \"Document\" WHERE \"DocumentId\" = @id", connection);
            delete.Parameters.AddWithValue("id", dto.DocumentId);
            Assert.Equal(1, await delete.ExecuteNonQueryAsync());
        }

        var row = Assert.Single(await DocumentRowsAsync(dto.DocumentId), r => r.EventType == "DOC_REMOVE");
        Assert.Equal("DIRECT_DB", row.ActorKind);
        Assert.Null(row.UserId);
        Assert.Null(row.OperationId);
        Assert.Null(row.NewData);
        Assert.NotNull(row.OldData);
        Assert.Equal(DocumentTypes.StudyPlanner, Detail(row)["DocumentType"]!.GetValue<string>());
        Assert.DoesNotContain(await DocumentRowsAsync(dto.DocumentId), r => r.EventType == "DELETE");
    }

    [PgFact]
    public async Task Direct_update_without_real_change_is_not_logged()
    {
        var userId = await UserIdAsync();
        var dto = await UploadAsync(UserAudit(), DocumentTypes.StudyPlanner, NextEntityId(), FakeDocumentStorage.Pdf(), userId);

        await using (var connection = new NpgsqlConnection(fixture.Database.ConnectionString))
        {
            await connection.OpenAsync();
            await using var touch = new NpgsqlCommand("UPDATE \"Document\" SET \"FileName\" = \"FileName\" WHERE \"DocumentId\" = @id", connection);
            touch.Parameters.AddWithValue("id", dto.DocumentId);
            await touch.ExecuteNonQueryAsync();
            await using var rename = new NpgsqlCommand("UPDATE \"Document\" SET \"FileName\" = 'renombrado.pdf' WHERE \"DocumentId\" = @id", connection);
            rename.Parameters.AddWithValue("id", dto.DocumentId);
            await rename.ExecuteNonQueryAsync();
        }

        var rows = await DocumentRowsAsync(dto.DocumentId);
        Assert.Equal(new[] { "DOC_UPLOAD", "DOC_UPDATE" }, rows.Select(r => r.EventType));
        Assert.Equal(new[] { "FileName" }, rows[1].ChangedColumns);
        Assert.Equal("DIRECT_DB", rows[1].ActorKind);
    }

    [PgFact]
    public async Task Reassign_pairs_each_document_type_separately()
    {
        var userId = await UserIdAsync();
        var admissionId = NextEntityId();
        var studentId = NextEntityId();
        var replacedType = await UploadAsync(UserAudit(), DocumentTypes.Enrollment(1, DocumentTypes.AdmissionTable), admissionId, FakeDocumentStorage.Pdf("uno.pdf"), userId);
        var movedType = await UploadAsync(UserAudit(), DocumentTypes.Enrollment(2, DocumentTypes.AdmissionTable), admissionId, FakeDocumentStorage.Pdf("dos.pdf"), userId);
        var target = await UploadAsync(UserAudit(), DocumentTypes.Enrollment(1, DocumentTypes.StudentTable), studentId, FakeDocumentStorage.Pdf("viejo.pdf"), userId);
        var audit = UserAudit();

        Assert.Equal(2, await ReassignAsync(audit, admissionId, studentId, userId));

        var rows = await OperationRowsAsync(audit.OperationId);
        Assert.Equal(3, rows.Count);
        Assert.All(rows, r => Assert.Equal(audit.OperationId, r.OperationId));
        var movedFrom = new JsonObject { ["EntityTable"] = DocumentTypes.AdmissionTable, ["EntityId"] = admissionId };

        var previous = Assert.Single(rows, r => r.EventType == "DOC_REPLACED");
        Assert.Equal(target.DocumentId.ToString(), previous.RecordId);
        Assert.Equal("ENROLLMENT_DOC:1", Detail(previous)["DocumentType"]!.GetValue<string>());

        var current = Assert.Single(rows, r => r.EventType == "DOC_REPLACE");
        Assert.Equal(replacedType.DocumentId.ToString(), current.RecordId);
        Assert.Equal("current", Detail(current)["role"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(movedFrom, Detail(current)["movedFrom"]));

        var update = Assert.Single(rows, r => r.EventType == "DOC_UPDATE");
        Assert.Equal(movedType.DocumentId.ToString(), update.RecordId);
        Assert.Equal("ENROLLMENT_DOC:2", Detail(update)["DocumentType"]!.GetValue<string>());
        Assert.False(Detail(update).ContainsKey("role"));
        Assert.True(JsonNode.DeepEquals(movedFrom, Detail(update)["movedFrom"]));
    }

    [PgFact]
    public async Task Replacement_pairs_only_within_its_operation()
    {
        var userId = await UserIdAsync();
        var entityId = NextEntityId();
        var first = await UploadAsync(UserAudit(), DocumentTypes.StudyPlanner, entityId, FakeDocumentStorage.Pdf("v1.pdf"), userId);
        var secondAudit = UserAudit();
        var second = await UploadAsync(secondAudit, DocumentTypes.StudyPlanner, entityId, FakeDocumentStorage.Pdf("v2.pdf"), userId);
        var thirdAudit = UserAudit();
        var third = await UploadAsync(thirdAudit, DocumentTypes.StudyPlanner, entityId, FakeDocumentStorage.Pdf("v3.pdf"), userId);

        Assert.NotEqual(secondAudit.OperationId, thirdAudit.OperationId);
        foreach (var (audit, replaced, current) in new[] { (secondAudit, first, second), (thirdAudit, second, third) })
        {
            var rows = await OperationRowsAsync(audit.OperationId);
            Assert.Equal(2, rows.Count);
            Assert.Equal(replaced.DocumentId.ToString(), Assert.Single(rows, r => r.EventType == "DOC_REPLACED").RecordId);
            Assert.Equal(current.DocumentId.ToString(), Assert.Single(rows, r => r.EventType == "DOC_REPLACE").RecordId);
        }

        var removeAudit = UserAudit();
        await using (var db = fixture.Database.NewContext(removeAudit))
        {
            Assert.True(await new DocumentService(db, storage, removeAudit).RemoveCurrentAsync(DocumentTypes.StudyPlanner, entityId, userId, Now.AddHours(1)));
        }

        var freshAudit = UserAudit();
        var fresh = await UploadAsync(freshAudit, DocumentTypes.StudyPlanner, entityId, FakeDocumentStorage.Pdf("v4.pdf"), userId);

        Assert.Equal("DOC_REMOVE", Assert.Single(await OperationRowsAsync(removeAudit.OperationId)).EventType);
        var upload = Assert.Single(await OperationRowsAsync(freshAudit.OperationId));
        Assert.Equal("DOC_UPLOAD", upload.EventType);
        Assert.Equal(fresh.DocumentId.ToString(), upload.RecordId);
    }

    private static int NextEntityId() => Interlocked.Increment(ref nextEntityId);

    private static AuditContext UserAudit()
    {
        var audit = new AuditContext();
        audit.SetRequest("POST /documents", "1.1.1.1", "UA");
        audit.SetUser(3, "Ana Pérez");
        return audit;
    }

    private static JsonObject Detail(AuditLog row) => JsonNode.Parse(row.Detail!)!.AsObject();

    private async Task<DocumentDto> UploadAsync(AuditContext audit, string typeCode, int entityId, DocumentUpload file, int userId) =>
        await UploadAsync(audit, DocumentTypes.Get(typeCode), entityId, file, userId);

    private async Task<DocumentDto> UploadAsync(AuditContext audit, DocumentTypeRule rule, int entityId, DocumentUpload file, int userId)
    {
        await using var db = fixture.Database.NewContext(audit);
        return await new DocumentService(db, storage, audit).UploadAsync(rule, entityId, null, file, userId, Now);
    }

    private async Task<int> ReassignAsync(AuditContext audit, int admissionId, int studentId, int userId)
    {
        await using var db = fixture.Database.NewContext(audit);
        return await new DocumentService(db, storage, audit).ReassignAsync(
            DocumentTypes.EnrollmentPrefix, DocumentTypes.AdmissionTable, admissionId, DocumentTypes.StudentTable, studentId, userId, Now.AddHours(1));
    }

    private async Task<List<AuditLog>> DocumentRowsAsync(int documentId)
    {
        await using var db = fixture.Database.NewContext();
        var recordId = documentId.ToString();
        return await db.AuditLogs
            .Where(a => a.TableName == "Document" && a.RecordId == recordId)
            .OrderBy(a => a.AuditLogId)
            .ToListAsync();
    }

    private async Task<List<AuditLog>> OperationRowsAsync(Guid operationId)
    {
        await using var db = fixture.Database.NewContext();
        return await db.AuditLogs
            .Where(a => a.TableName == "Document" && a.OperationId == operationId)
            .OrderBy(a => a.AuditLogId)
            .ToListAsync();
    }

    private async Task<int> UserIdAsync()
    {
        await using var connection = new NpgsqlConnection(fixture.Database.ConnectionString);
        await connection.OpenAsync();
        var key = Guid.NewGuid().ToString("N")[..16];
        await using var command = new NpgsqlCommand("""
            WITH s AS (
                INSERT INTO "School"("Name","Nit","Address") VALUES ('Colegio', @key, 'Calle 1') RETURNING "SchoolId"
            ), c AS (
                INSERT INTO "Campus"("Name","Address","Dane","SchoolId")
                SELECT 'Sede', 'Calle 1', '125875000001', "SchoolId" FROM s RETURNING "CampusId"
            )
            INSERT INTO "User"("UserName","NormalizedUserName","DocumentType","DocumentNumber","FirstName","LastName",
                "EmailConfirmed","PhoneNumberConfirmed","TwoFactorEnabled","LockoutEnabled","AccessFailedCount",
                "MustChangePassword","SecurityStamp","ConcurrencyStamp","CampusId")
            SELECT @key, upper(@key), 'CC', @key, 'Ana', 'Pérez',
                false, false, false, true, 0, false, 'security', 'concurrency', "CampusId" FROM c
            RETURNING "Id"
            """, connection);
        command.Parameters.AddWithValue("key", key);
        return (int)(await command.ExecuteScalarAsync())!;
    }
}

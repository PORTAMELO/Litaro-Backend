using Litaro.Data;
using Litaro.Models;
using Litaro.Services;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Tests;

public class DocumentServiceTests
{
    private static readonly DateTime Now = new(2026, 2, 1, 15, 0, 0, DateTimeKind.Utc);

    private static (AppDbContext Db, FakeDocumentStorage Storage, DocumentService Service, User User) Create()
    {
        var db = TestDb.Create();
        var storage = new FakeDocumentStorage();
        var user = TestData.AddUser(db, "Marta", "Rojas");
        return (db, storage, new DocumentService(db, storage), user);
    }

    [Fact]
    public async Task Upload_rejects_non_pdf_extension_type_signature_empty_and_oversize()
    {
        var (_, _, service, user) = Create();

        async Task<string> Fails(DocumentUpload file) =>
            (await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.UploadAsync(DocumentTypes.StudyPlanner, 1, null, file, user.Id, Now))).Message;

        Assert.Equal("El archivo debe ser PDF.", await Fails(FakeDocumentStorage.Upload("plan.docx", "application/pdf", "%PDF-1"u8.ToArray())));
        Assert.Equal("El archivo debe ser PDF.", await Fails(FakeDocumentStorage.Upload("plan.pdf", "image/png", "%PDF-1"u8.ToArray())));
        Assert.Equal("El archivo debe ser PDF.", await Fails(FakeDocumentStorage.Upload("plan.pdf", "application/pdf", "hello world"u8.ToArray())));
        Assert.Equal("El archivo está vacío.", await Fails(FakeDocumentStorage.Upload("plan.pdf", "application/pdf", [])));
        Assert.Equal("El archivo supera el tamaño máximo de 10 MB.", await Fails(FakeDocumentStorage.Pdf(extraBytes: 10_485_760)));
    }

    [Fact]
    public async Task Upload_without_configuration_fails_with_clear_message()
    {
        var (_, storage, service, user) = Create();
        storage.IsConfigured = false;

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UploadAsync(DocumentTypes.StudyPlanner, 1, null, FakeDocumentStorage.Pdf(), user.Id, Now));

        Assert.Equal("El almacenamiento de documentos no está configurado.", error.Message);
    }

    [Fact]
    public async Task Upload_replaces_previous_and_removes_old_file()
    {
        var (db, storage, service, user) = Create();

        await service.UploadAsync(DocumentTypes.StudyPlanner, 7, null, FakeDocumentStorage.Pdf("v1.pdf"), user.Id, Now);
        var second = await service.UploadAsync(DocumentTypes.StudyPlanner, 7, null, FakeDocumentStorage.Pdf("v2.pdf"), user.Id, Now.AddHours(1));

        var rows = await db.Documents.OrderBy(d => d.DocumentId).ToListAsync();

        Assert.Equal(2, rows.Count);
        Assert.False(rows[0].IsCurrent);
        Assert.Equal(user.Id, rows[0].ReplacedById);
        Assert.Equal(Now.AddHours(1), rows[0].ReplacedAt);
        Assert.Equal(Now.AddHours(1), rows[0].FileRemovedAt);
        Assert.True(rows[1].IsCurrent);
        Assert.Null(rows[1].FileRemovedAt);
        Assert.Equal(second.DocumentId, rows[1].DocumentId);
        Assert.Equal("v2.pdf", second.FileName);
        Assert.Single(storage.Files);
        Assert.True(storage.Files.ContainsKey(rows[1].StoragePath));
    }

    [Fact]
    public async Task Remove_deletes_file_and_keeps_trace()
    {
        var (db, storage, service, user) = Create();

        await service.UploadAsync(DocumentTypes.StudyPlanner, 7, null, FakeDocumentStorage.Pdf(), user.Id, Now);

        Assert.True(await service.RemoveCurrentAsync(DocumentTypes.StudyPlanner, 7, user.Id, Now.AddHours(2)));

        var row = await db.Documents.SingleAsync();

        Assert.Empty(storage.Files);
        Assert.False(row.IsCurrent);
        Assert.Equal(user.Id, row.ReplacedById);
        Assert.Equal(Now.AddHours(2), row.ReplacedAt);
        Assert.Equal(Now.AddHours(2), row.FileRemovedAt);
        Assert.False(await service.RemoveCurrentAsync(DocumentTypes.StudyPlanner, 7, user.Id, Now.AddHours(3)));
    }

    [Fact]
    public async Task Failed_storage_delete_keeps_document_current()
    {
        var (db, storage, service, user) = Create();

        await service.UploadAsync(DocumentTypes.StudyPlanner, 7, null, FakeDocumentStorage.Pdf(), user.Id, Now);
        storage.FailNextDelete = true;

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RemoveCurrentAsync(DocumentTypes.StudyPlanner, 7, user.Id, Now));

        var row = await db.Documents.AsNoTracking().SingleAsync();

        Assert.Equal("No fue posible eliminar el documento.", error.Message);
        Assert.True(row.IsCurrent);
        Assert.Null(row.FileRemovedAt);
        Assert.Single(storage.Files);
    }

    [Fact]
    public async Task Failed_delete_on_replace_keeps_previous_and_drops_new_file()
    {
        var (db, storage, service, user) = Create();

        await service.UploadAsync(DocumentTypes.StudyPlanner, 7, null, FakeDocumentStorage.Pdf("v1.pdf"), user.Id, Now);
        storage.FailNextDelete = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UploadAsync(DocumentTypes.StudyPlanner, 7, null, FakeDocumentStorage.Pdf("v2.pdf"), user.Id, Now.AddHours(1)));

        var only = await db.Documents.AsNoTracking().SingleAsync();

        Assert.True(only.IsCurrent);
        Assert.Equal("v1.pdf", only.FileName);
        Assert.Single(storage.Files);
        Assert.True(storage.Files.ContainsKey(only.StoragePath));
    }

    [Fact]
    public async Task Enrollment_rule_accepts_pdf_jpg_png_and_stores_real_content_type()
    {
        var (db, _, service, user) = Create();
        var rule = DocumentTypes.Enrollment(12, DocumentTypes.AdmissionTable);

        Assert.Equal("ENROLLMENT_DOC:12", rule.Code);
        Assert.Equal(12, DocumentTypes.EnrollmentDetailId(rule.Code));
        Assert.Null(DocumentTypes.EnrollmentDetailId(DocumentTypes.StudyPlanner));

        await service.UploadAsync(rule, 1, 2027, FakeDocumentStorage.Jpg("foto.jpeg"), user.Id, Now);
        await service.UploadAsync(rule, 2, 2027, FakeDocumentStorage.Png(), user.Id, Now);
        await service.UploadAsync(rule, 3, 2027, FakeDocumentStorage.Pdf("registro.pdf"), user.Id, Now);

        var rows = await db.Documents.OrderBy(d => d.EntityId).ToListAsync();

        Assert.Equal(["image/jpeg", "image/png", "application/pdf"], rows.Select(r => r.ContentType));
        Assert.All(rows, r => Assert.Equal("Enrollment", r.Module));
        Assert.All(rows, r => Assert.Equal("AdmissionRequest", r.EntityTable));
        Assert.All(rows, r => Assert.DoesNotContain(":", r.StoragePath));
        Assert.EndsWith(".jpeg", rows[0].StoragePath);

        var mismatch = FakeDocumentStorage.Upload("foto.png", "image/png", [0xFF, 0xD8, 0xFF, 0, 0, 0, 0, 0, 0]);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UploadAsync(rule, 4, 2027, mismatch, user.Id, Now));

        Assert.Equal("El archivo debe ser PDF, JPG o PNG.", error.Message);
    }

    [Fact]
    public async Task Reassign_moves_current_and_replaces_target_same_type()
    {
        var (db, storage, service, user) = Create();
        var first = DocumentTypes.Enrollment(1, DocumentTypes.AdmissionTable);
        var second = DocumentTypes.Enrollment(2, DocumentTypes.AdmissionTable);
        var studentFirst = DocumentTypes.Enrollment(1, DocumentTypes.StudentTable);

        await service.UploadAsync(first, 5, 2027, FakeDocumentStorage.Pdf("a.pdf"), user.Id, Now);
        await service.UploadAsync(second, 5, 2027, FakeDocumentStorage.Pdf("b.pdf"), user.Id, Now);
        await service.UploadAsync(studentFirst, 9, 2026, FakeDocumentStorage.Pdf("viejo.pdf"), user.Id, Now);

        var moved = await service.ReassignAsync(
            DocumentTypes.EnrollmentPrefix, DocumentTypes.AdmissionTable, 5, DocumentTypes.StudentTable, 9, user.Id, Now.AddHours(1));

        var rows = await db.Documents.AsNoTracking().ToListAsync();
        var current = rows.Where(r => r.IsCurrent).ToList();
        var old = rows.Single(r => !r.IsCurrent);

        Assert.Equal(2, moved);
        Assert.Equal(2, current.Count);
        Assert.All(current, r => Assert.Equal("Student", r.EntityTable));
        Assert.All(current, r => Assert.Equal(9, r.EntityId));
        Assert.Equal(["a.pdf", "b.pdf"], current.Select(r => r.FileName).Order());
        Assert.Equal("viejo.pdf", old.FileName);
        Assert.Equal(Now.AddHours(1), old.FileRemovedAt);
        Assert.False(storage.Files.ContainsKey(old.StoragePath));
        Assert.Equal(2, storage.Files.Count);
    }

    [Fact]
    public async Task Current_by_prefix_lists_every_enrollment_type_of_the_entities()
    {
        var (_, _, service, user) = Create();

        await service.UploadAsync(DocumentTypes.Enrollment(1, DocumentTypes.StudentTable), 9, 2027, FakeDocumentStorage.Pdf(), user.Id, Now);
        await service.UploadAsync(DocumentTypes.Enrollment(2, DocumentTypes.StudentTable), 9, 2027, FakeDocumentStorage.Png(), user.Id, Now);
        await service.UploadAsync(DocumentTypes.Enrollment(1, DocumentTypes.AdmissionTable), 9, 2027, FakeDocumentStorage.Pdf(), user.Id, Now);
        await service.UploadAsync(DocumentTypes.StudyPlanner, 9, null, FakeDocumentStorage.Pdf(), user.Id, Now);

        var found = await service.CurrentByPrefixAsync(DocumentTypes.EnrollmentPrefix, DocumentTypes.StudentTable, [9, 10]);

        Assert.Equal(["ENROLLMENT_DOC:1", "ENROLLMENT_DOC:2"], found.Select(f => f.DocumentType).Order());
        Assert.All(found, f => Assert.Equal(9, f.EntityId));
        Assert.True(await service.HasAnyAsync("ENROLLMENT_DOC:2"));
        Assert.False(await service.HasAnyAsync("ENROLLMENT_DOC:3"));
    }

    [Fact]
    public async Task Purge_removes_files_of_non_current_rows_without_stamp()
    {
        var (db, storage, service, user) = Create();

        await service.UploadAsync(DocumentTypes.StudyPlanner, 7, null, FakeDocumentStorage.Pdf(), user.Id, Now);

        var row = await db.Documents.SingleAsync();
        row.IsCurrent = false;
        await db.SaveChangesAsync();

        Assert.Equal(1, await service.PurgeRemovedAsync(Now.AddDays(1)));
        Assert.Equal(Now.AddDays(1), (await db.Documents.AsNoTracking().SingleAsync()).FileRemovedAt);
        Assert.Empty(storage.Files);
        Assert.Equal(0, await service.PurgeRemovedAsync(Now.AddDays(2)));
    }

    [Fact]
    public async Task Upload_does_not_touch_other_entities()
    {
        var (db, _, service, user) = Create();

        await service.UploadAsync(DocumentTypes.StudyPlanner, 1, null, FakeDocumentStorage.Pdf(), user.Id, Now);
        await service.UploadAsync(DocumentTypes.StudyPlanner, 2, null, FakeDocumentStorage.Pdf(), user.Id, Now);

        Assert.Equal(2, await db.Documents.CountAsync(d => d.IsCurrent));

        var current = await service.CurrentByEntityAsync(DocumentTypes.StudyPlanner, [1, 2, 3]);

        Assert.Equal([1, 2], current.Keys.Order());
    }

    [Fact]
    public async Task Failed_storage_upload_leaves_previous_current()
    {
        var (db, storage, service, user) = Create();

        var first = await service.UploadAsync(DocumentTypes.StudyPlanner, 1, null, FakeDocumentStorage.Pdf(), user.Id, Now);
        storage.FailNextUpload = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UploadAsync(DocumentTypes.StudyPlanner, 1, null, FakeDocumentStorage.Pdf(), user.Id, Now));

        var rows = await db.Documents.ToListAsync();

        Assert.Single(rows);
        Assert.True(rows[0].IsCurrent);
        Assert.Equal(first.DocumentId, rows[0].DocumentId);
    }

    [Fact]
    public async Task Storage_path_ignores_user_file_name()
    {
        var (db, _, service, user) = Create();

        await service.UploadAsync(DocumentTypes.StudyPlanner, 9, null, FakeDocumentStorage.Pdf("../../secreto.pdf"), user.Id, Now);

        var row = await db.Documents.SingleAsync();

        Assert.StartsWith("Grades/STUDY_PLANNER/AcademicAssignment/9/", row.StoragePath);
        Assert.EndsWith(".pdf", row.StoragePath);
        Assert.DoesNotContain("secreto", row.StoragePath);
        Assert.Equal("secreto.pdf", row.FileName);
    }

    [Fact]
    public async Task OpenCurrent_returns_the_current_file()
    {
        var (_, _, service, user) = Create();

        await service.UploadAsync(DocumentTypes.StudyPlanner, 4, null, FakeDocumentStorage.Pdf(), user.Id, Now);

        var opened = await service.OpenCurrentAsync(DocumentTypes.StudyPlanner, 4);

        Assert.NotNull(opened);
        Assert.True(opened.Value.Content.Length > 5);
        Assert.Null(await service.OpenCurrentAsync(DocumentTypes.StudyPlanner, 5));
    }
}

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
    public async Task Upload_replaces_previous_and_keeps_history()
    {
        var (db, storage, service, user) = Create();

        await service.UploadAsync(DocumentTypes.StudyPlanner, 7, null, FakeDocumentStorage.Pdf("v1.pdf"), user.Id, Now);
        var second = await service.UploadAsync(DocumentTypes.StudyPlanner, 7, null, FakeDocumentStorage.Pdf("v2.pdf"), user.Id, Now.AddHours(1));

        var rows = await db.Documents.OrderBy(d => d.DocumentId).ToListAsync();

        Assert.Equal(2, rows.Count);
        Assert.False(rows[0].IsCurrent);
        Assert.Equal(user.Id, rows[0].ReplacedById);
        Assert.Equal(Now.AddHours(1), rows[0].ReplacedAt);
        Assert.True(rows[1].IsCurrent);
        Assert.Equal(second.DocumentId, rows[1].DocumentId);
        Assert.Equal("v2.pdf", second.FileName);
        Assert.Equal(2, storage.Files.Count);
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

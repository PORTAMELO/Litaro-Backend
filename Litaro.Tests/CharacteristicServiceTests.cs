using Litaro.Data;
using Litaro.Models;
using Litaro.Services;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Tests;

public class CharacteristicServiceTests
{
    private static CharacteristicService Create(AppDbContext db) =>
        new(db, new DocumentService(db, new FakeDocumentStorage()));

    [Fact]
    public async Task Update_changes_name_value_and_code()
    {
        var db = TestDb.Create();
        var detail = TestData.AddCatalog(db, Catalogs.DocumentType, link: true, ("CC", "Cedula", null)).Single();

        var updated = await Create(db).UpdateDetailAsync(
            detail.CharacteristicDetailId, new SaveCharacteristicDetailRequest(" Cédula de ciudadanía ", "CC", "1"));

        Assert.NotNull(updated);
        Assert.Equal("Cédula de ciudadanía", updated.Nombre);
        Assert.Equal("1", updated.Codigo);
        Assert.Null(await Create(db).UpdateDetailAsync(9999, new SaveCharacteristicDetailRequest("X", "Y", null)));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Create(db).UpdateDetailAsync(detail.CharacteristicDetailId, new SaveCharacteristicDetailRequest("", "CC", null)));

        Assert.Equal("Nombre y valor son obligatorios.", error.Message);
    }

    [Fact]
    public async Task Delete_removes_a_detail_without_files()
    {
        var db = TestDb.Create();
        var detail = TestData.AddCatalog(db, Catalogs.Eps, link: true, ("SURA", "SURA", null)).Single();

        Assert.True(await Create(db).DeleteDetailAsync(detail.CharacteristicDetailId));
        Assert.Empty(await db.CharacteristicDetails.ToListAsync());
        Assert.False(await Create(db).DeleteDetailAsync(detail.CharacteristicDetailId));
    }

    [Fact]
    public async Task Delete_rejects_an_enrollment_document_type_with_files()
    {
        var db = TestDb.Create();
        var user = TestData.AddUser(db, "Marta", "Rojas");
        var detail = TestData.AddDocumentTypes(db, ("Registro civil", "OBLIGATORIO")).Single();

        db.Documents.Add(new Document
        {
            Module = "Enrollment",
            DocumentType = $"ENROLLMENT_DOC:{detail.CharacteristicDetailId}",
            EntityTable = "Student",
            EntityId = 1,
            StoragePath = "x",
            FileName = "registro.pdf",
            ContentType = "application/pdf",
            SizeBytes = 10,
            IsCurrent = false,
            UploadedById = user.Id,
        });
        await db.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Create(db).DeleteDetailAsync(detail.CharacteristicDetailId));

        Assert.Equal("El documento tiene archivos cargados; ocúltelo en Parámetros de matrícula.", error.Message);
        Assert.Single(await db.CharacteristicDetails.ToListAsync());
    }
}

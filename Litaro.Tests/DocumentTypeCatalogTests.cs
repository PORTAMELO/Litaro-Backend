using Litaro.Data;
using Litaro.Services;

namespace Litaro.Tests;

public class DocumentTypeCatalogTests
{
    private static CharacteristicService Characteristics(AppDbContext db) =>
        new(db, new DocumentService(db, new FakeDocumentStorage()));

    [Fact]
    public async Task Student_document_type_comes_from_the_catalog()
    {
        var db = TestDb.Create();
        TestData.AddCatalog(db, Catalogs.DocumentType, link: true, ("CC", "Cédula de ciudadanía", "1"), ("TI", "Tarjeta de identidad", "2"));
        var service = new StudentService(db, null!, Characteristics(db));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(
            new StudentService.CreateStudentRequest("PAS", "123", "Ana", "Paz", null, null, "Clave123", 1, "C1", new DateTime(2015, 1, 1), 'F')));

        Assert.Equal("Tipo de documento 'PAS' no válido.", error.Message);
    }

    [Fact]
    public async Task Parent_document_type_comes_from_the_catalog()
    {
        var db = TestDb.Create();
        TestData.AddCatalog(db, Catalogs.DocumentType, link: true, ("CC", "Cédula de ciudadanía", "1"));
        var service = new ParentService(db, null!, Characteristics(db));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(
            new ParentService.CreateParentRequest("PAS", "123", "Luz", "Paz", "luz@correo.co", null, "Clave123", 1, "MOTHER", null)));

        Assert.Equal("Tipo de documento 'PAS' no válido.", error.Message);
    }
}

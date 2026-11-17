using Litaro.Services;

namespace Litaro.Tests;

public class CatalogServiceTests
{
    [Fact]
    public async Task Options_come_from_the_linked_characteristic_in_creation_order()
    {
        var db = TestDb.Create();
        TestData.AddCatalog(db, Catalogs.EthnicGroup, link: true, ("00", "No aplica", null), ("200", "Negritudes", null), ("01", "Achagua", null));

        var options = await new CatalogService(db).GetAsync(Catalogs.EthnicGroup);

        Assert.Equal(["00", "200", "01"], options.Select(o => o.Value));
        Assert.Equal("No aplica", options[0].Label);
    }

    [Fact]
    public async Task Falls_back_to_the_seeded_name_when_the_column_is_not_linked()
    {
        var db = TestDb.Create();
        TestData.AddCatalog(db, Catalogs.Eps, link: false, ("SURA", "SURA", null));

        var options = await new CatalogService(db).GetAsync(Catalogs.Eps);

        Assert.Equal("SURA", Assert.Single(options).Value);
        Assert.Empty(await new CatalogService(db).GetAsync(Catalogs.Gender));
    }

    [Fact]
    public async Task Document_type_options_carry_the_official_code()
    {
        var db = TestDb.Create();
        TestData.AddCatalog(db, Catalogs.DocumentType, link: true, ("CC", "Cédula de ciudadanía", "1"), ("PPT", "Permiso de protección temporal", null));

        var options = await new CatalogService(db).GetAsync(Catalogs.DocumentType);

        Assert.Equal(new CatalogOption("CC", "Cédula de ciudadanía", "1"), options[0]);
        Assert.Null(options[1].Code);
    }

    [Fact]
    public async Task IsValid_accepts_only_current_values()
    {
        var db = TestDb.Create();
        TestData.AddCatalog(db, Catalogs.Gender, link: true, ("F", "Femenino", null), ("M", "Masculino", null));
        var service = new CatalogService(db);

        Assert.True(await service.IsValidAsync(Catalogs.Gender, "F"));
        Assert.False(await service.IsValidAsync(Catalogs.Gender, "X"));
        Assert.False(await service.IsValidAsync(Catalogs.Gender, null));
    }

    [Fact]
    public async Task GetAll_returns_every_catalog_by_its_key()
    {
        var db = TestDb.Create();
        TestData.AddCatalog(db, Catalogs.Gender, link: true, ("F", "Femenino", null));

        var all = await new CatalogService(db).GetAllAsync();

        Assert.Equal(Catalogs.All.Select(c => c.Key).Order(), all.Keys.Order());
        Assert.Equal(10, all.Count);
        Assert.Single(all["genders"]);
        Assert.Empty(all["eps"]);
    }
}

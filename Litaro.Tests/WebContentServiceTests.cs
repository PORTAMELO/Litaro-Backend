using Litaro.Data;
using Litaro.Models;
using Litaro.Services;

namespace Litaro.Tests;

public class WebContentServiceTests
{
    private static WebContent Content(int id, string page, string section, string key, int order, bool active = true) => new()
    {
        WebContentId = id,
        PageName = page,
        SectionName = section,
        ContentKey = key,
        DisplayOrder = order,
        DataJson = $"{{\"id\":{id}}}",
        Active = active,
    };

    private static async Task<AppDbContext> SeedAsync()
    {
        var db = TestDb.Create();
        db.WebContentConfigurations.Add(new WebContentConfiguration
        {
            WebContentConfigurationId = 1,
            PageName = "Home",
            SectionName = "Valores",
            ContentKey = "Values",
            MinItems = 0,
            MaxItems = 10,
            Active = true,
        });
        db.WebContents.AddRange(
            Content(1, "Home", "Valores", "Values", 2),
            Content(2, "Home", "Valores", "Values", 1),
            Content(3, "Home", "Valores", "Values", 3, active: false),
            Content(4, "Home", "Sedes", "Cards", 1),
            Content(5, "Nosotros", "Profesores", "Cards", 1));
        await db.SaveChangesAsync();
        return db;
    }

    [Fact]
    public async Task GetPage_returns_only_active_rows_of_that_page_in_order()
    {
        using var db = await SeedAsync();
        var svc = new WebContentService(db, new WebContentCache());

        var page = await svc.GetPageAsync("Home");

        Assert.Equal(new[] { 4, 2, 1 }, page.Select(c => c.WebContentId));
    }

    [Fact]
    public async Task GetPage_matches_the_section_endpoint()
    {
        using var db = await SeedAsync();
        var svc = new WebContentService(db, new WebContentCache());
        var page = await svc.GetPageAsync("Home");

        foreach (var (section, key) in new[] { ("Valores", "Values"), ("Sedes", "Cards"), ("Eventos", "Cards") })
        {
            var expected = (await svc.GetByPageSectionContentKeyAsync("Home", section, key)).Select(c => c.WebContentId);
            var actual = page.Where(c => c.SectionName == section && c.ContentKey == key).Select(c => c.WebContentId);

            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public async Task GetPage_does_not_cache_unknown_pages()
    {
        using var db = await SeedAsync();
        var cache = new WebContentCache();
        var svc = new WebContentService(db, cache);

        Assert.Empty(await svc.GetPageAsync("NoExiste"));
        Assert.Null(cache.Get("NoExiste"));
    }

    [Fact]
    public void Set_after_an_invalidation_does_not_store_stale_content()
    {
        var cache = new WebContentCache();
        var version = cache.Version;

        cache.Invalidate();
        cache.Set("Home", [Content(1, "Home", "Valores", "Values", 1)], version);

        Assert.Null(cache.Get("Home"));
    }

    [Fact]
    public async Task Update_invalidates_the_page_cache()
    {
        using var db = await SeedAsync();
        var svc = new WebContentService(db, new WebContentCache());
        await svc.GetPageAsync("Home");

        await svc.UpdateAsync(2, new WebContent { DataJson = "{\"nuevo\":true}" });

        Assert.Equal("{\"nuevo\":true}", (await svc.GetPageAsync("Home")).Single(c => c.WebContentId == 2).DataJson);
    }

    [Fact]
    public async Task Deactivate_and_activate_invalidate_the_page_cache()
    {
        using var db = await SeedAsync();
        var svc = new WebContentService(db, new WebContentCache());
        await svc.GetPageAsync("Home");

        await svc.DeactivateAsync(1);
        Assert.DoesNotContain(await svc.GetPageAsync("Home"), c => c.WebContentId == 1);

        await svc.ActivateAsync(3);
        Assert.Contains(await svc.GetPageAsync("Home"), c => c.WebContentId == 3);
    }

    [Fact]
    public async Task Create_and_delete_invalidate_the_page_cache()
    {
        using var db = await SeedAsync();
        var svc = new WebContentService(db, new WebContentCache());
        await svc.GetPageAsync("Home");

        var (_, created) = await svc.CreateAsync(new WebContent { PageName = "Home", SectionName = "Valores", ContentKey = "Values", DataJson = "{}" });
        Assert.Contains(await svc.GetPageAsync("Home"), c => c.WebContentId == created!.WebContentId);

        await svc.DeleteAsync(created!.WebContentId);
        Assert.DoesNotContain(await svc.GetPageAsync("Home"), c => c.WebContentId == created.WebContentId);
    }
}

using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services;

public class WebContentService(AppDbContext db)
{

    public Task<List<WebContent>> GetAllForAdminAsync()
    {
        return db.WebContents
            .OrderBy(w => w.PageName)
            .ThenBy(w => w.SectionName)
            .ThenBy(w => w.ContentKey)
            .ThenBy(w => w.DisplayOrder)
            .ToListAsync();
    }
    public Task<List<WebContent>> GetAllAsync()
    {
        return db.WebContents
        .Where(w => w.Active)
        .OrderBy(w => w.PageName)
        .ThenBy(w => w.SectionName)
        .ThenBy(w => w.ContentKey)
        .ThenBy(x => x.DisplayOrder)
        .ToListAsync();
    }

    public async Task<WebContent?> GetByIdAsync(int id)
    {
        return await db.WebContents.FindAsync(id);
    }

    public async Task<List<WebContent>> GetByPageSectionContentKeyAsync(string pageName, string sectionName, string contentKey)
    {
        return await db.WebContents
            .Where(x =>
                x.PageName == pageName &&
                x.SectionName == sectionName &&
                x.ContentKey == contentKey &&
                x.Active)
            .OrderBy(x => x.DisplayOrder)
            .ToListAsync();
    }

    public async Task<(WebContentOperationResult Result, WebContent? Content)> CreateAsync(WebContent content)
    {
        var configuration = await db.WebContentConfigurations
            .FirstOrDefaultAsync(x =>
                x.PageName == content.PageName &&
                x.SectionName == content.SectionName &&
                x.ContentKey == content.ContentKey &&
                x.Active);

        if (configuration is null)
        {
            return (WebContentOperationResult.ConfigurationNotFound, null);
        }

        var currentItems = await db.WebContents.CountAsync(x =>
            x.PageName == content.PageName &&
            x.SectionName == content.SectionName &&
            x.ContentKey == content.ContentKey &&
            x.Active);

        if (configuration.MaxItems.HasValue &&
            currentItems >= configuration.MaxItems.Value)
        {
            return (WebContentOperationResult.MaxItemsReached, null);
        }

        var nextDisplayOrder = await db.WebContents
            .Where(x =>
                x.PageName == content.PageName &&
                x.SectionName == content.SectionName &&
                x.ContentKey == content.ContentKey)
            .Select(x => (int?)x.DisplayOrder)
            .MaxAsync() ?? 0;

        content.DisplayOrder = nextDisplayOrder + 1;
        content.CreationDate = DateTime.UtcNow;

        db.WebContents.Add(content);

        await db.SaveChangesAsync();

        return (WebContentOperationResult.Success, content);
    }

    public async Task<bool> UpdateAsync(int id, WebContent updated)
    {
        var content = await db.WebContents.FindAsync(id);

        if (content is null)
            return false;

        content.DataJson = updated.DataJson;
        content.UpdateDate = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return true;
    }

    public async Task<WebContentOperationResult> DeactivateAsync(int id)
    {
        var content = await db.WebContents.FindAsync(id);

        if (content is null)
            return WebContentOperationResult.NotFound;

        if (!content.Active)
            return WebContentOperationResult.Success;

        var configuration = await db.WebContentConfigurations
            .FirstOrDefaultAsync(x =>
                x.PageName == content.PageName &&
                x.SectionName == content.SectionName &&
                x.ContentKey == content.ContentKey &&
                x.Active);

        if (configuration is null)
            return WebContentOperationResult.ConfigurationNotFound;

        var currentItems = await db.WebContents.CountAsync(x =>
            x.PageName == content.PageName &&
            x.SectionName == content.SectionName &&
            x.ContentKey == content.ContentKey &&
            x.Active);

        if (currentItems <= configuration.MinItems)
        {
            return WebContentOperationResult.MinItemsReached;
        }

        content.Active = false;
        content.UpdateDate = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return WebContentOperationResult.Success;
    }

    public async Task<WebContentOperationResult> ActivateAsync(int id)
    {
        var content = await db.WebContents.FindAsync(id);

        if (content is null)
            return WebContentOperationResult.NotFound;

        if (content.Active)
            return WebContentOperationResult.Success;

        var configuration = await db.WebContentConfigurations
            .FirstOrDefaultAsync(x =>
                x.PageName == content.PageName &&
                x.SectionName == content.SectionName &&
                x.ContentKey == content.ContentKey &&
                x.Active);

        if (configuration is null)
            return WebContentOperationResult.ConfigurationNotFound;

        var currentItems = await db.WebContents.CountAsync(x =>
            x.PageName == content.PageName &&
            x.SectionName == content.SectionName &&
            x.ContentKey == content.ContentKey &&
            x.Active);

        if (configuration.MaxItems.HasValue &&
            currentItems >= configuration.MaxItems.Value)
        {
            return WebContentOperationResult.MaxItemsReached;
        }

        content.Active = true;
        content.UpdateDate = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return WebContentOperationResult.Success;
    }

    public async Task<WebContentOperationResult> DeleteAsync(int id)
    {
        var content = await db.WebContents.FindAsync(id);

        if (content is null)
            return WebContentOperationResult.NotFound;

        var configuration = await db.WebContentConfigurations
            .FirstOrDefaultAsync(x =>
                x.PageName == content.PageName &&
                x.SectionName == content.SectionName &&
                x.ContentKey == content.ContentKey &&
                x.Active);

        if (configuration is null)
            return WebContentOperationResult.ConfigurationNotFound;

        var currentItems = await db.WebContents.CountAsync(x =>
            x.PageName == content.PageName &&
            x.SectionName == content.SectionName &&
            x.ContentKey == content.ContentKey &&
            x.Active);

        if (currentItems <= configuration.MinItems)
            return WebContentOperationResult.MinItemsReached;

        db.WebContents.Remove(content);

        await db.SaveChangesAsync();

        var remaining = await db.WebContents
            .Where(x =>
                x.PageName == content.PageName &&
                x.SectionName == content.SectionName &&
                x.ContentKey == content.ContentKey)
            .OrderBy(x => x.DisplayOrder)
            .ToListAsync();

        for (int i = 0; i < remaining.Count; i++)
        {
            remaining[i].DisplayOrder = i + 1;
        }

        await db.SaveChangesAsync();

        return WebContentOperationResult.Success;
    }
}

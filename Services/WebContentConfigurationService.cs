using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services
{
    public class WebContentConfigurationService(AppDbContext db)
    {
        public Task<List<WebContentConfiguration>> GetAllAsync()
        {
            return db.WebContentConfigurations
            .Where(w => w.Active)
            .OrderBy(w => w.PageName)
            .ThenBy(w => w.SectionName)
            .ThenBy(w => w.ContentKey)
            .ToListAsync();
        }

        public async Task<WebContentConfiguration?> GetByIdAsync(int id)
        {
            return await db.WebContentConfigurations.FindAsync(id);
        }

        public async Task<WebContentConfiguration?> GetByPageSectionContentKeyAsync(string pageName, string sectionName, string contentKey)
        {
            return await db.WebContentConfigurations.FirstOrDefaultAsync(x =>
                x.PageName == pageName &&
                x.SectionName == sectionName &&
                x.ContentKey == contentKey &&
                x.Active);
        }

        public async Task<WebContentConfiguration> CreateAsync(WebContentConfiguration configuration)
        {
            db.WebContentConfigurations.Add(configuration);
            await db.SaveChangesAsync();
            return configuration;
        }

        public async Task<bool> UpdateAsync(int id, WebContentConfiguration updated)
        {
            var configuration = await db.WebContentConfigurations.FindAsync(id);
            if (configuration is null) return false;

            configuration.PageName = updated.PageName;
            configuration.SectionName = updated.SectionName;
            configuration.ContentKey = updated.ContentKey;
            configuration.MinItems = updated.MinItems;
            configuration.MaxItems = updated.MaxItems;
            configuration.Active = updated.Active;

            await db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeactivateAsync(int id)
        {
            var configuration = await db.WebContentConfigurations.FindAsync(id);
            if (configuration is null) return false;

            configuration.Active = false;
            await db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ActivateAsync(int id)
        {
            var configuration = await db.WebContentConfigurations.FindAsync(id);
            if (configuration is null) return false;

            configuration.Active = true;
            await db.SaveChangesAsync();
            return true;
        }
    }
}
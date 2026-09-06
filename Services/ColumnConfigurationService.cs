using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services;

public record ColumnConfigItem(string ColumnName, bool Filterable, bool Visible, string? Alias, int? CharacteristicId);

public class ColumnConfigurationService(AppDbContext db)
{
    public async Task<List<string>> GetFilterableColumnsAsync(string tableName)
    {
        return await db.ColumnConfigurations
            .Where(c => c.TableName == tableName && c.Filterable)
            .Select(c => c.ColumnName)
            .ToListAsync();
    }

    public async Task SetColumnConfigurationAsync(string tableName, List<ColumnConfigItem> columns)
    {
        var existing = await db.ColumnConfigurations
            .Where(c => c.TableName == tableName)
            .ToListAsync();

        db.ColumnConfigurations.RemoveRange(existing);

        var newEntries = columns
            .GroupBy(c => c.ColumnName)
            .Select(g => g.First())
            .Select(c => new ColumnConfiguration
            {
                TableName = tableName,
                ColumnName = c.ColumnName,
                Filterable = c.Filterable,
                Visible = c.Visible,
                Alias = string.IsNullOrWhiteSpace(c.Alias) ? null : c.Alias.Trim(),
                CharacteristicId = c.CharacteristicId,
                CreationDate = DateTime.UtcNow,
            });

        db.ColumnConfigurations.AddRange(newEntries);

        await db.SaveChangesAsync();
    }
}

using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services;

public record PermissionDto(string TableName, int Permissions, string? View);

public class PermissionService(AppDbContext db)
{
    public async Task<List<PermissionDto>> GetForRolesAsync(IEnumerable<string> roleNames)
    {
        var names = roleNames.ToList();
        if (names.Count == 0)
            return [];

        var rows = await db.RolePermissions
            .Where(rp => names.Contains(rp.Role.Name!))
            .Select(rp => new { rp.TableName, rp.Permissions, rp.View })
            .ToListAsync();

        return rows
            .GroupBy(r => r.TableName)
            .Select(g => new PermissionDto(
                g.Key,
                g.Aggregate(0, (acc, r) => acc | r.Permissions),
                g.Select(r => r.View).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))))
            .OrderBy(p => p.TableName)
            .ToList();
    }

    public async Task<bool> HasPermissionAsync(IEnumerable<string> roleNames, string tableName, PermissionFlags flag)
    {
        var names = roleNames.ToList();
        if (names.Count == 0)
            return false;

        var values = await db.RolePermissions
            .Where(rp => rp.TableName == tableName && names.Contains(rp.Role.Name!))
            .Select(rp => rp.Permissions)
            .ToListAsync();

        return values.Any(v => (v & (int)flag) == (int)flag);
    }
}
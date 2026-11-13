using Litaro.Data;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services;

public record PermissionRow(string RoleName, string TableName, int Permissions, string? View);

public class PermissionCache
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly object sync = new();
    private IReadOnlyList<PermissionRow>? snapshot;
    private DateTime loadedAt;
    private long version;

    public async Task<IReadOnlyList<PermissionRow>> GetAsync(AppDbContext db)
    {
        if (Current() is { } current)
            return current;

        await gate.WaitAsync();
        try
        {
            if (Current() is { } loaded)
                return loaded;

            long startVersion;
            lock (sync)
                startVersion = version;

            var rows = await db.RolePermissions
                .AsNoTracking()
                .Select(rp => new PermissionRow(rp.Role.Name!, rp.TableName, rp.Permissions, rp.View))
                .ToListAsync();

            lock (sync)
            {
                if (version == startVersion)
                {
                    snapshot = rows;
                    loadedAt = DateTime.UtcNow;
                }
            }

            return rows;
        }
        finally
        {
            gate.Release();
        }
    }

    public void Invalidate()
    {
        lock (sync)
        {
            version++;
            snapshot = null;
        }
    }

    private IReadOnlyList<PermissionRow>? Current()
    {
        lock (sync)
            return snapshot is not null && DateTime.UtcNow - loadedAt < Lifetime ? snapshot : null;
    }
}

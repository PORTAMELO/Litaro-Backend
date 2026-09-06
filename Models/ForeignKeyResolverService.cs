using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Litaro.Data;

namespace Litaro.Services;

public class ForeignKeyResolverService(AppDbContext db)
{
    private record ForeignKeyInfo(string ColumnName, string ReferencedTable, string ReferencedColumn);
    private record NameRow(string Id, string Name);
    private static readonly ConcurrentDictionary<string, List<ForeignKeyInfo>> _fkCache = new();
    private static readonly ConcurrentDictionary<string, bool> _hasNameCache = new();

    private async Task<List<ForeignKeyInfo>> GetForeignKeysAsync(string tableName)
    {
        if (_fkCache.TryGetValue(tableName, out var cached))
            return cached;

        var raw = await db.Database.SqlQuery<ForeignKeyInfo>($@"
                    SELECT
                    kcu.column_name AS ""ColumnName"",
                    ccu.table_name AS ""ReferencedTable"",
                    ccu.column_name AS ""ReferencedColumn""
                    FROM information_schema.table_constraints tc
                    JOIN information_schema.key_column_usage kcu
                    ON tc.constraint_name = kcu.constraint_name AND tc.table_schema = kcu.table_schema
                    JOIN information_schema.constraint_column_usage ccu
                    ON ccu.constraint_name = tc.constraint_name AND ccu.table_schema = tc.table_schema
                    WHERE tc.constraint_type = 'FOREIGN KEY'
                    AND tc.table_schema = 'public'
                    AND tc.table_name = {tableName}")
        .ToListAsync();

        _fkCache[tableName] = raw;
        return raw;
    }

    public async Task<Dictionary<string, string>> GetForeignKeyTablesAsync(string tableName)
    {
        var foreignKeys = await GetForeignKeysAsync(tableName);
        return foreignKeys.ToDictionary(fk => fk.ColumnName, fk => fk.ReferencedTable, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<bool> HasNameColumnAsync(string referencedTable)
    {
        if (_hasNameCache.TryGetValue(referencedTable, out var cached))
            return cached;

        var count = await db.Database.SqlQuery<int>($@"
                    SELECT COUNT(*)::int AS ""Value""
                    FROM information_schema.columns
                    WHERE table_schema = 'public'
                    AND table_name = {referencedTable}
                    AND column_name = 'Name'")
                    .FirstAsync();

        var exists = count > 0;
        _hasNameCache[referencedTable] = exists;
        return exists;
    }

    public async Task<Dictionary<string, Dictionary<string, string>>> BuildLookupsAsync<T>(string tableName, List<T> records)
    {
        var lookups = new Dictionary<string, Dictionary<string, string>>();

        if (records.Count == 0)
            return lookups;

        var foreignKeys = await GetForeignKeysAsync(tableName);
        var properties = typeof(T).GetProperties();

        foreach (var fk in foreignKeys)
        {
            var property = properties.FirstOrDefault(p => p.Name == fk.ColumnName);
            if (property is null) continue;

            if (!await HasNameColumnAsync(fk.ReferencedTable)) continue;

            var ids = records
                .Select(r => property.GetValue(r))
                .Where(v => v is not null)
                .Select(v => v!.ToString()!)
                .Distinct()
                .ToArray();

            if (ids.Length == 0) continue;

            var sql = $@"
                SELECT ""{fk.ReferencedColumn}""::text AS ""Id"", ""Name"" AS ""Name""
                FROM ""{fk.ReferencedTable}""
                WHERE ""{fk.ReferencedColumn}""::text = ANY(@ids)";

            var resolved = await db.Database
                .SqlQueryRaw<NameRow>(sql, new Npgsql.NpgsqlParameter("ids", ids))
                .ToListAsync();

            var fieldName = fk.ColumnName[..1].ToLower() + fk.ColumnName[1..];
            lookups[fieldName] = resolved.ToDictionary(r => r.Id, r => r.Name);
        }

        return lookups;
    }
}
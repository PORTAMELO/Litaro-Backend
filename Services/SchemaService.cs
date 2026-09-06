using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services;

public record SchemaData(
    string ColumnName,
    string DataType,
    int? MaxLength,
    string IsNullable,
    string? ColumnDefault,
    bool Filterable,
    bool Visible,
    string? Alias,
    int? CharacteristicId,
    string? CharacteristicName,
    bool IsIdentity,
    string? ForeignKeyTable
);

public class SchemaService(AppDbContext db, ForeignKeyResolverService fkSvc)
{
    public static readonly HashSet<string> AllowedTables = new()
    {
        "AcademicAssignment", "AcademicPeriod", "AcademicYear", "Attendance",
        "Campus", "Classroom", "Enrollment", "Grade", "GradeScore", "Parent",
        "ParentStudent", "Schedule", "School", "Student", "StudentLog",
        "Subject", "Teacher", "User", "WebContent", "WebContentConfiguration",
        "Characteristic", "CharacteristicDetail"
    };

    private static readonly Dictionary<string, string> JoinedUserTables = new()
    {
        ["Student"] = "User",
        ["Parent"] = "User",
        ["Teacher"] = "User",
    };

    public string? GetJoinedTable(string tableName) =>
        JoinedUserTables.TryGetValue(tableName, out var joinedTable) ? joinedTable : null;

    public async Task<HashSet<string>> GetOwnColumnNamesAsync(string tableName)
    {
        var ownColumns = await GetRawColumnsAsync(tableName);
        return ownColumns.Select(c => c.ColumnName).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public async Task<List<SchemaData>?> GetByTableAsync(string tableName)
    {
        if (!AllowedTables.Contains(tableName))
            return null;

        var ownColumns = await GetRawColumnsAsync(tableName);

        var ownConfig = await GetConfigByColumnAsync(tableName);
        var ownForeignKeys = await fkSvc.GetForeignKeyTablesAsync(tableName);
        var result = ownColumns.Select(c => BuildSchemaData(c, ownConfig, ownForeignKeys)).ToList();

        if (JoinedUserTables.TryGetValue(tableName, out var joinedTable))
        {
            var joinedColumns = await GetRawColumnsAsync(joinedTable);

            var ownNames = ownColumns.Select(c => c.ColumnName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var joinedConfig = await GetConfigByColumnAsync(joinedTable);
            var joinedForeignKeys = await fkSvc.GetForeignKeyTablesAsync(joinedTable);

            result.AddRange(
                joinedColumns
                    .Where(c => !ownNames.Contains(c.ColumnName))
                    .Select(c => BuildSchemaData(c, joinedConfig, joinedForeignKeys))
            );
        }

        return result;
    }

    private async Task<Dictionary<string, ColumnConfiguration>> GetConfigByColumnAsync(string tableName)
    {
        return await db.ColumnConfigurations
            .Where(c => c.TableName == tableName)
            .Include(c => c.Characteristic)
            .ToDictionaryAsync(c => c.ColumnName);
    }

    private static SchemaData BuildSchemaData(RawColumn c, Dictionary<string, ColumnConfiguration> configByColumn, Dictionary<string, string> foreignKeys)
    {
        configByColumn.TryGetValue(c.ColumnName, out var config);
        foreignKeys.TryGetValue(c.ColumnName, out var foreignKeyTable);

        return new SchemaData(
            c.ColumnName,
            c.DataType,
            c.MaxLength,
            c.IsNullable,
            c.ColumnDefault,
            config?.Filterable ?? false,
            config?.Visible ?? true,
            config?.Alias,
            config?.CharacteristicId,
            config?.Characteristic?.Name,
            c.IsIdentity,
            foreignKeyTable
        );
    }

    private async Task<List<RawColumn>> GetRawColumnsAsync(string tableName)
    {
        return await db.Database.SqlQuery<RawColumn>($@"
        SELECT
            column_name AS ""ColumnName"",
            data_type AS ""DataType"",
            character_maximum_length AS ""MaxLength"",
            is_nullable AS ""IsNullable"",
            column_default AS ""ColumnDefault"",
            (is_identity = 'YES') AS ""IsIdentity""
        FROM information_schema.columns
        WHERE table_schema = 'public'
        AND table_name = {tableName}
        ORDER BY ordinal_position;")
        .ToListAsync();
    }
    private record RawColumn(string ColumnName, string DataType, int? MaxLength, string IsNullable, string? ColumnDefault, bool IsIdentity);
}
using Litaro.Data;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services;

public record CatalogKey(string Key, string Table, string Column, string Name, string Label);

public record CatalogOption(string Value, string Label, string? Code);

public static class Catalogs
{
    public const string EnrollmentDocuments = "Documentos de matrícula";

    public static readonly CatalogKey DocumentType = new("documentTypes", "User", "DocumentType", "Tipo de documento", "Tipo de documento");
    public static readonly CatalogKey Gender = new("genders", "Student", "Gender", "Género", "Género");
    public static readonly CatalogKey BloodType = new("bloodTypes", "Student", "BloodType", "Tipo de sangre", "Tipo de sangre");
    public static readonly CatalogKey Eps = new("eps", "Student", "Eps", "EPS", "EPS");
    public static readonly CatalogKey Relationship = new("relationships", "Parent", "Relationship", "Parentesco", "Parentesco");
    public static readonly CatalogKey EthnicGroup = new("ethnicGroups", "Student", "EthnicGroup", "Grupo étnico", "Grupo étnico");
    public static readonly CatalogKey DisabilityType = new("disabilityTypes", "Student", "DisabilityType", "Tipo de discapacidad", "Tipo de discapacidad");
    public static readonly CatalogKey ExceptionalCapacity = new("exceptionalCapacities", "Student", "ExceptionalCapacity", "Capacidades excepcionales", "Capacidad excepcional");
    public static readonly CatalogKey ConflictVictim = new("conflictVictims", "Student", "ConflictVictim", "Víctima del conflicto", "Condición de víctima");
    public static readonly CatalogKey Methodology = new("methodologies", "Student", "Methodology", "Metodología", "Metodología");

    public static readonly IReadOnlyList<CatalogKey> All =
    [
        DocumentType, Gender, BloodType, Eps, Relationship,
        EthnicGroup, DisabilityType, ExceptionalCapacity, ConflictVictim, Methodology,
    ];
}

public class CatalogService(AppDbContext db)
{
    private readonly Dictionary<string, List<CatalogOption>> cache = [];

    public async Task<List<CatalogOption>> GetAsync(CatalogKey key)
    {
        if (cache.TryGetValue(key.Key, out var cached))
            return cached;

        var characteristicId = await db.ColumnConfigurations
            .AsNoTracking()
            .Where(c => c.TableName == key.Table && c.ColumnName == key.Column && c.CharacteristicId != null)
            .Select(c => c.CharacteristicId)
            .FirstOrDefaultAsync();

        characteristicId ??= await db.Characteristics
            .AsNoTracking()
            .Where(c => c.Name == key.Name)
            .Select(c => (int?)c.CharacteristicId)
            .FirstOrDefaultAsync();

        var options = characteristicId is null
            ? []
            : await db.CharacteristicDetails
                .AsNoTracking()
                .Where(d => d.CharacteristicId == characteristicId)
                .OrderBy(d => d.CharacteristicDetailId)
                .Select(d => new CatalogOption(d.Valor, d.Nombre, d.Codigo))
                .ToListAsync();

        cache[key.Key] = options;
        return options;
    }

    public async Task<Dictionary<string, List<CatalogOption>>> GetAllAsync()
    {
        var result = new Dictionary<string, List<CatalogOption>>();

        foreach (var key in Catalogs.All)
            result[key.Key] = await GetAsync(key);

        return result;
    }

    public async Task<bool> IsValidAsync(CatalogKey key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return (await GetAsync(key)).Any(o => o.Value == value);
    }
}

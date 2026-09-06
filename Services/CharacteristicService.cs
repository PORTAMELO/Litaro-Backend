using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services;

public class CharacteristicService(AppDbContext db)
{
    public record CreateCharacteristicRequest(string Name);

    public record CreateCharacteristicDetailRequest(int CharacteristicId, string Nombre, string Valor);

    public Task<List<Characteristic>> GetAllAsync(IDictionary<string, string>? filters = null)
    {
        var query = db.Characteristics.AsQueryable();

        if (filters is not null && filters.Count > 0)
            query = query.ApplyFilters(filters);

        return query.OrderBy(c => c.Name).ToListAsync();
    }

    public async Task<Characteristic> CreateAsync(CreateCharacteristicRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new InvalidOperationException("El nombre de la característica es obligatorio.");

        var exists = await db.Characteristics.AnyAsync(c => c.Name == request.Name);
        if (exists)
            throw new InvalidOperationException($"Ya existe una característica llamada '{request.Name}'.");

        var characteristic = new Characteristic
        {
            Name = request.Name.Trim(),
            CreationDate = DateTime.UtcNow,
        };

        db.Characteristics.Add(characteristic);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException(ex.InnerException?.Message ?? ex.Message);
        }

        return characteristic;
    }

    public Task<List<CharacteristicDetail>> GetDetailsAsync(int characteristicId)
    {
        return db.CharacteristicDetails
            .Where(d => d.CharacteristicId == characteristicId)
            .OrderBy(d => d.Nombre)
            .ToListAsync();
    }

    public Task<List<CharacteristicDetail>> GetAllDetailsAsync(IDictionary<string, string>? filters = null)
    {
        var query = db.CharacteristicDetails.AsQueryable();

        if (filters is not null && filters.Count > 0)
            query = query.ApplyFilters(filters);

        return query.ToListAsync();
    }

    public async Task<CharacteristicDetail> CreateDetailAsync(CreateCharacteristicDetailRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Nombre) || string.IsNullOrWhiteSpace(request.Valor))
            throw new InvalidOperationException("Nombre y valor son obligatorios.");

        var characteristicExists = await db.Characteristics.AnyAsync(c => c.CharacteristicId == request.CharacteristicId);
        if (!characteristicExists)
            throw new InvalidOperationException("La característica indicada no existe.");

        var detail = new CharacteristicDetail
        {
            CharacteristicId = request.CharacteristicId,
            Nombre = request.Nombre.Trim(),
            Valor = request.Valor.Trim(),
            CreationDate = DateTime.UtcNow,
        };

        db.CharacteristicDetails.Add(detail);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException(ex.InnerException?.Message ?? ex.Message);
        }

        return detail;
    }

    public async Task<List<string>?> GetValidValuesForColumnAsync(string tableName, string columnName)
    {
        var characteristicId = await db.ColumnConfigurations
            .Where(c => c.TableName == tableName && c.ColumnName == columnName)
            .Select(c => c.CharacteristicId)
            .FirstOrDefaultAsync();

        if (characteristicId is null)
            return null;

        return await db.CharacteristicDetails
            .Where(d => d.CharacteristicId == characteristicId)
            .Select(d => d.Valor)
            .ToListAsync();
    }
}

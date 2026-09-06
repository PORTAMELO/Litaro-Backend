using Microsoft.EntityFrameworkCore;
using Litaro.Data;
using Litaro.Models;

namespace Litaro.Services;

public class SchoolService(AppDbContext db)
{

    public record CreateSchoolRequest(string Name, string Nit, string Address, string? Phone);

    public Task<List<School>> GetAllAsync(IDictionary<string, string>? filters = null)
    {
        var query = db.Schools.Where(s => s.Active).AsQueryable();

        if (filters is not null && filters.Count > 0)
            query = query.ApplyFilters(filters);

        return query.ToListAsync();
    }
    public async Task<School> CreateAsync(CreateSchoolRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) ||
            string.IsNullOrWhiteSpace(request.Nit) ||
            string.IsNullOrWhiteSpace(request.Address))
            throw new InvalidOperationException("Nombre, NIT y dirección son obligatorios.");

        var nitExists = await db.Schools.AnyAsync(s => s.Nit == request.Nit);
        if (nitExists)
            throw new InvalidOperationException($"El NIT '{request.Nit}' ya existe.");

        var school = new School
        {
            Name = request.Name.Trim(),
            Nit = request.Nit.Trim(),
            Address = request.Address.Trim(),
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            Active = true,
            CreationDate = DateTime.UtcNow,
        };

        db.Schools.Add(school);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException(ex.InnerException?.Message ?? ex.Message);
        }

        return school;
    }

    public async Task<(int Imported, List<string> Errors)> ImportFromCsvAsync(Stream csvStream)
    {
        var errors = new List<string>();
        int imported = 0;

        using var reader = new StreamReader(csvStream);

        await reader.ReadLineAsync();

        int lineNumber = 1;
        string? line;
        while ((line = await reader.ReadLineAsync()) is not null)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line)) continue;

            var cols = line.Split(',');
            if (cols.Length < 4)
            {
                errors.Add($"Linea {lineNumber}: columnas insuficientes (se esperan 4).");
                continue;
            }

            var name = cols[0].Trim();
            var nit = cols[1].Trim();
            var address = cols[2].Trim();
            var phone = cols[3].Trim();

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(nit) || string.IsNullOrEmpty(address))
            {
                errors.Add($"Linea {lineNumber}: Nombre, NIT o direccion son obligatorios");
                continue;
            }

            bool nitExists = await db.Schools.AnyAsync(s => s.Nit == nit);
            if (nitExists)
            {
                errors.Add($"Linea {lineNumber}: NIT '{nit}' ya existe en la base de datos");
                continue;
            }

            var School = new School
            {
                Name = name,
                Nit = nit,
                Address = address,
                Phone = string.IsNullOrEmpty(phone) ? null : phone,
            };

            db.Schools.Add(School);

            try
            {
                await db.SaveChangesAsync();
                imported++;
            }
            catch (DbUpdateException ex)
            {
                db.ChangeTracker.Clear();
                errors.Add($"Linea {lineNumber}: error al guardar - {ex.InnerException?.Message ?? ex.Message}");
            }

        }

        return (imported, errors);

    }

}
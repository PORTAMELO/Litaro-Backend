using Microsoft.EntityFrameworkCore;
using Litaro.Data;
using Litaro.Models;

namespace Litaro.Services;

public class SchoolService(AppDbContext db)
{
    public Task<List<School>> GetAllAsync()
    {
        return db.Schools.Where(s => s.Active).ToListAsync();
    }

    public async Task<School?> GetByIdAsync(int id)
    {
        return await db.Schools.FindAsync(id);
    }

    public async Task<School> CreateAsync(School school)
    {
        db.Schools.Add(school);
        await db.SaveChangesAsync();
        return school;
    }

    public async Task<bool> UpdateAsync(int id, School updated)
    {
        var school = await db.Schools.FindAsync(id);
        if (school is null) return false;

        school.Name    = updated.Name;
        school.Address = updated.Address;
        school.Phone   = updated.Phone;

        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeactivateAsync(int id)
    {
        var school = await db.Schools.FindAsync(id);
        if (school is null) return false;

        school.Active = false;
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ActivateAsync(int id)
    {
        var school = await db.Schools.FindAsync(id);
        if (school is null) return false;

        school.Active = true;
        await db.SaveChangesAsync();
        return true;
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

            if(string.IsNullOrEmpty(name) || string.IsNullOrEmpty(nit) || string.IsNullOrEmpty(address))
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
            catch(DbUpdateException ex)
            {
                db.ChangeTracker.Clear();
                errors.Add($"Linea {lineNumber}: error al guardar - {ex.InnerException?.Message ?? ex.Message}");
            }

        }

        return (imported, errors);

    }

}
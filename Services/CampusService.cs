using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services;

public class CampusService(AppDbContext db)
{
    public Task<List<Campus>> GetAllAsync()
    {
        return db.Campuses.Where(c => c.Active).ToListAsync();
    }

    public async Task<Campus?> GetByIdAsync(int id)
    {
        return await db.Campuses.FindAsync(id);
    }

    public async Task<Campus> CreateAsync(Campus campus)
    {
        db.Campuses.Add(campus);
        await db.SaveChangesAsync();
        return campus;
    }

    public async Task<bool> UpdateAsync(int id, Campus updated)
    {
        var campus = await db.Campuses.FindAsync(id);
        if (campus is null) return false;

        campus.Name = updated.Name;
        campus.Address = updated.Address;
        campus.Phone = updated.Phone;

        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeactivateAsync(int id)
    {
        var campus = await db.Campuses.FindAsync(id);
        if (campus is null) return false;

        campus.Active = false;
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ActivateAsync(int id)
    {
        var campus = await db.Campuses.FindAsync(id);
        if (campus is null) return false;

        campus.Active = true;
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<(int Imported, List<String> Errors)> ImportFromCsvAsync(Stream csvStream)
    {
        var errors = new List<string>();
        var imported = 0;

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
                errors.Add($"Línea {lineNumber}: columnas insuficientes (se esperan 4).");
                continue;
            }

            var name = cols[0].Trim();
            var address = cols[1].Trim();
            var phone = cols[2].Trim();
            var nitSchool = cols[3].Trim();

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(address) || string.IsNullOrEmpty(nitSchool))
            {
                errors.Add($"Línea {lineNumber}: Nombre, Dirección y NITColegio son obligatorios.");
                continue;
            }

            var school = await db.Schools.FirstOrDefaultAsync(s => s.Nit == nitSchool);
            if (school is null)
            {
                errors.Add($"Línea {lineNumber}: No existe un colegio con NIT '{nitSchool}'.");
                continue;
            }

            var campus = new Campus
            {
                Name = name,
                Address = address,
                Phone = string.IsNullOrEmpty(phone) ? null : phone,
                SchoolId = school.SchoolId,
            };

            db.Campuses.Add(campus);

            try
            {
                await db.SaveChangesAsync();
                imported++;
            }
            catch (DbUpdateException ex)
            {
                db.ChangeTracker.Clear();
                errors.Add($"Línea {lineNumber}: error al guardar — {ex.InnerException?.Message ?? ex.Message}");
            }

        }

        return (imported, errors);

    }

}
using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services;

public class CampusService(AppDbContext db)
{

    public record CreateCampusRequest(string Name, string Address, string? Phone, string Dane, int SchoolId);

    public Task<List<Campus>> GetAllAsync(IDictionary<string, string>? filters = null)
    {
        var query = db.Campuses.Where(c => c.Active).AsQueryable();

        if (filters is not null && filters.Count > 0)
            query = query.ApplyFilters(filters);

        return query.ToListAsync();
    }

    public async Task<Campus> CreateAsync(CreateCampusRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Address))
            throw new InvalidOperationException("Nombre y dirección son obligatorios.");

        var schoolExists = await db.Schools.AnyAsync(s => s.SchoolId == request.SchoolId);
        if (!schoolExists)
            throw new InvalidOperationException($"El colegio con id {request.SchoolId} no existe.");

        var dane = request.Dane.Trim();
        var daneExists = await db.Campuses.AnyAsync(c => c.Dane == dane);
        if (daneExists)
            throw new InvalidOperationException($"Ya existe una sede registrada con el DANE '{dane}'.");

        var campus = new Campus
        {
            Name = request.Name.Trim(),
            Address = request.Address.Trim(),
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            Dane = request.Dane.Trim(),
            SchoolId = request.SchoolId,
            Active = true,
        };

        db.Campuses.Add(campus);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException(ex.InnerException?.Message ?? ex.Message);
        }

        return campus;
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
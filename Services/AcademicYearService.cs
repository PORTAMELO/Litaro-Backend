using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;


namespace Litaro.Services
{
    public class AcademicYearService(AppDbContext db)
    {
        public Task<List<AcademicYear>> GetAllAsync() =>
            db.AcademicYears.ToListAsync();

        public async Task<AcademicYear?> GetByIdAsync(short id) =>
            await db.AcademicYears.FindAsync(id);

        public async Task<AcademicYear> CreateAsync(AcademicYear year)
        {
            db.AcademicYears.Add(year);
            await db.SaveChangesAsync();
            return year;
        }

        public async Task<bool> UpdateAsync(short id, AcademicYear updated)
        {
            var year = await db.AcademicYears.FindAsync(id);
            if (year is null) return false;

            year.StartDate = updated.StartDate;
            year.EndDate = updated.EndDate;
            year.Status = updated.Status;

            await db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeactivateAsync(short id)
        {
            var year = await db.AcademicYears.FindAsync(id);
            if (year is null) return false;

            year.Status = "CLOSED";
            await db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ActivateAsync(short id)
        {
            var year = await db.AcademicYears.FindAsync(id);
            if (year is null) return false;

            year.Status = "ACTIVE";
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
                    errors.Add($"Línea {lineNumber}: columnas insuficientes (se esperan al menos 4).");
                    continue;
                }

                var yearIdRaw = cols[0].Trim();
                var startDateRaw = cols[1].Trim();
                var endDateRaw = cols[2].Trim();
                var status = cols[3].Trim().ToUpper();

                if (!short.TryParse(yearIdRaw, out short yearId))
                {
                    errors.Add($"Línea {lineNumber}: AnioId '{yearIdRaw}' no es un número válido.");
                    continue;
                }

                if (yearId < 1900 || yearId > 2300)
                {
                    errors.Add($"Línea {lineNumber}: AnioId '{yearId}' debe estar entre 1900 y 2300.");
                    continue;
                }

                if (!DateTime.TryParse(startDateRaw, out DateTime startDate))
                {
                    errors.Add($"Línea {lineNumber}: FechaInicio '{startDateRaw}' no tiene un formato válido.");
                    continue;
                }

                if (!DateTime.TryParse(endDateRaw, out DateTime endDate))
                {
                    errors.Add($"Línea {lineNumber}: FechaFin '{endDateRaw}' no tiene un formato válido.");
                    continue;
                }

                if (endDate <= startDate)
                {
                    errors.Add($"Línea {lineNumber}: FechaFin debe ser posterior a FechaInicio.");
                    continue;
                }

                if (status != "ACTIVE" && status != "CLOSED")
                {
                    errors.Add($"Línea {lineNumber}: Estado '{status}' no válido. Use ACTIVE o CLOSED.");
                    continue;
                }

                bool exists = await db.AcademicYears.AnyAsync(y => y.YearId == yearId);
                if (exists)
                {
                    errors.Add($"Línea {lineNumber}: El año '{yearId}' ya existe en la base de datos.");
                    continue;
                }

                var academicYear = new AcademicYear
                {
                    YearId = yearId,
                    StartDate = startDate,
                    EndDate = endDate,
                    Status = status,
                };

                db.AcademicYears.Add(academicYear);

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
}

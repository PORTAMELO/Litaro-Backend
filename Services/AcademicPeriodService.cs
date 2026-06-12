using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services
{
    public class AcademicPeriodService(AppDbContext db)
    {
        public Task<List<AcademicPeriod>> GetAllAsync() =>
            db.AcademicPeriods.ToListAsync();

        public async Task<AcademicPeriod?> GetByIdAsync(short id) =>
            await db.AcademicPeriods.FindAsync(id);

        public async Task<AcademicPeriod> CreateAsync(AcademicPeriod period)
        {
            db.AcademicPeriods.Add(period);
            await db.SaveChangesAsync();
            return period;
        }

        public async Task<bool> UpdateAsync(short id, AcademicPeriod updated)
        {
            var period = await db.AcademicPeriods.FindAsync(id);
            if (period is null) return false;

            period.PeriodNumber = updated.PeriodNumber;
            period.StartDate = updated.StartDate;
            period.EndDate = updated.EndDate;
            period.YearId = updated.YearId;

            await db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(short id)
        {
            var period = await db.AcademicPeriods.FindAsync(id);
            if (period is null) return false;

            db.AcademicPeriods.Remove(period);
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

                var periodNumberRaw = cols[0].Trim();
                var startDateRaw = cols[1].Trim();
                var endDateRaw = cols[2].Trim();
                var yearIdRaw = cols[3].Trim();

                if (!byte.TryParse(periodNumberRaw, out byte periodNumber))
                {
                    errors.Add($"Línea {lineNumber}: NumeroPeriodo '{periodNumberRaw}' no es un número válido.");
                    continue;
                }

                if (periodNumber < 1 || periodNumber > 12)
                {
                    errors.Add($"Línea {lineNumber}: NumeroPeriodo '{periodNumber}' debe estar entre 1 y 12.");
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

                if (!short.TryParse(yearIdRaw, out short yearId))
                {
                    errors.Add($"Línea {lineNumber}: AnioId '{yearIdRaw}' no es un número válido.");
                    continue;
                }

                bool yearExists = await db.AcademicYears.AnyAsync(y => y.YearId == yearId);
                if (!yearExists)
                {
                    errors.Add($"Línea {lineNumber}: No existe un año académico con AnioId '{yearId}'.");
                    continue;
                }

                bool periodExists = await db.AcademicPeriods
                    .AnyAsync(p => p.PeriodNumber == periodNumber && p.YearId == yearId);
                if (periodExists)
                {
                    errors.Add($"Línea {lineNumber}: Ya existe el período {periodNumber} para el año {yearId}.");
                    continue;
                }

                var period = new AcademicPeriod
                {
                    PeriodNumber = periodNumber,
                    StartDate = startDate,
                    EndDate = endDate,
                    YearId = yearId,
                };

                db.AcademicPeriods.Add(period);

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

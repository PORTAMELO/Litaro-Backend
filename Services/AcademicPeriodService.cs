using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services
{
    public class AcademicPeriodService(AppDbContext db)
    {

        public record CreateAcademicPeriodRequest(short PeriodNumber, DateTime StartDate, DateTime EndDate, short YearId);

        public Task<List<AcademicPeriod>> GetAllAsync(IDictionary<string, string>? filters = null)
        {
            var query = db.AcademicPeriods.Include(p => p.AcademicYear).AsQueryable();

            if (filters is not null && filters.Count > 0)
                query = query.ApplyFilters(filters);

            return query.ToListAsync();
        }

        public async Task<AcademicPeriod> CreateAsync(CreateAcademicPeriodRequest request)
        {
            if (request.PeriodNumber <= 0)
                throw new InvalidOperationException("El número de periodo debe ser mayor a cero.");

            if (request.EndDate <= request.StartDate)
                throw new InvalidOperationException("La fecha de fin debe ser posterior a la fecha de inicio.");

            var yearExists = await db.AcademicYears.AnyAsync(y => y.YearId == request.YearId);
            if (!yearExists)
                throw new InvalidOperationException($"El año académico {request.YearId} no existe.");

            var duplicatePeriod = await db.AcademicPeriods
                .AnyAsync(p => p.YearId == request.YearId && p.PeriodNumber == request.PeriodNumber);
            if (duplicatePeriod)
                throw new InvalidOperationException($"El periodo {request.PeriodNumber} ya existe para el año {request.YearId}.");

            var academicPeriod = new AcademicPeriod
            {
                PeriodNumber = (byte)request.PeriodNumber,
                StartDate = DateTime.SpecifyKind(request.StartDate, DateTimeKind.Utc),
                EndDate = DateTime.SpecifyKind(request.EndDate, DateTimeKind.Utc),
                YearId = request.YearId,
            };

            db.AcademicPeriods.Add(academicPeriod);

            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                throw new InvalidOperationException(ex.InnerException?.Message ?? ex.Message);
            }

            return academicPeriod;
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
                    errors.Add($"Línea {lineNumber}: numero del periodo '{periodNumberRaw}' no es un número válido.");
                    continue;
                }

                if (periodNumber < 1 || periodNumber > 12)
                {
                    errors.Add($"Línea {lineNumber}: numero del periodo '{periodNumber}' debe estar entre 1 y 12.");
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
                    errors.Add($"Línea {lineNumber}: el año '{yearIdRaw}' no es un número válido.");
                    continue;
                }

                bool yearExists = await db.AcademicYears.AnyAsync(y => y.YearId == yearId);
                if (!yearExists)
                {
                    errors.Add($"Línea {lineNumber}: No existe un año académico con '{yearId}'.");
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

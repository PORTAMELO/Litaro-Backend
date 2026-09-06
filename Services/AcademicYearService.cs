using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;


namespace Litaro.Services
{
    public class AcademicYearService(AppDbContext db)
    {

        public record CreateAcademicYearRequest(short YearId, DateTime StartDate, DateTime EndDate);

        public Task<List<AcademicYear>> GetAllAsync(IDictionary<string, string>? filters = null)
        {
            var query = db.AcademicYears.AsQueryable();

            if (filters is not null && filters.Count > 0)
                query = query.ApplyFilters(filters);

            return query.ToListAsync();
        }

        public async Task<AcademicYear> CreateAsync(CreateAcademicYearRequest request)
        {
            if (request.YearId <= 0)
                throw new InvalidOperationException("El año es obligatorio y debe ser válido.");

            if (request.EndDate <= request.StartDate)
                throw new InvalidOperationException("La fecha de fin debe ser posterior a la fecha de inicio.");

            var yearExists = await db.AcademicYears.AnyAsync(y => y.YearId == request.YearId);
            if (yearExists)
                throw new InvalidOperationException($"El año académico {request.YearId} ya existe.");

            var academicYear = new AcademicYear
            {
                YearId = request.YearId,
                StartDate = DateTime.SpecifyKind(request.StartDate, DateTimeKind.Utc),
                EndDate = DateTime.SpecifyKind(request.EndDate, DateTimeKind.Utc),
                Active = true
            };

            db.AcademicYears.Add(academicYear);

            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                throw new InvalidOperationException(ex.InnerException?.Message ?? ex.Message);
            }

            return academicYear;
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
                    EndDate = endDate
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

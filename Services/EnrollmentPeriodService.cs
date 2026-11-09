using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services;

public record EnrollmentPeriodDto(
    short YearId,
    DateOnly PreEnrollmentStart,
    DateOnly PreEnrollmentEnd,
    DateOnly AdmissionStart,
    DateOnly AdmissionEnd);

public record SaveEnrollmentPeriodRequest(
    DateOnly PreEnrollmentStart,
    DateOnly PreEnrollmentEnd,
    DateOnly AdmissionStart,
    DateOnly AdmissionEnd);

public record YearOptionDto(short YearId, bool Active);

public record GradeOptionDto(int GradeId, string Name, byte OrderNum);

public record EnrollmentOptionsDto(List<YearOptionDto> Years, List<GradeOptionDto> Grades);

public class EnrollmentPeriodService(AppDbContext db)
{
    public static DateOnly Today() => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-5));

    public Task<EnrollmentPeriod?> FindAsync(short yearId) =>
        db.EnrollmentPeriods.FirstOrDefaultAsync(p => p.YearId == yearId);

    public async Task<EnrollmentPeriodDto?> GetAsync(short yearId)
    {
        var period = await FindAsync(yearId);
        return period is null ? null : ToDto(period);
    }

    public async Task<EnrollmentOptionsDto> GetOptionsAsync()
    {
        var years = await db.AcademicYears
            .AsNoTracking()
            .OrderByDescending(y => y.YearId)
            .Select(y => new YearOptionDto(y.YearId, y.Active))
            .ToListAsync();

        var grades = await db.Grades
            .AsNoTracking()
            .OrderBy(g => g.OrderNum)
            .Select(g => new GradeOptionDto(g.GradeId, g.Name, g.OrderNum))
            .ToListAsync();

        return new EnrollmentOptionsDto(years, grades);
    }

    public async Task<EnrollmentPeriodDto> SaveAsync(short yearId, SaveEnrollmentPeriodRequest request)
    {
        var yearExists = await db.AcademicYears.AnyAsync(y => y.YearId == yearId);
        if (!yearExists)
            throw new InvalidOperationException(
                $"No existe el año académico {yearId}. Créelo primero en Administración > Años académicos.");

        if (request.PreEnrollmentStart == default || request.PreEnrollmentEnd == default ||
            request.AdmissionStart == default || request.AdmissionEnd == default)
            throw new InvalidOperationException("Las cuatro fechas son obligatorias.");

        if (request.PreEnrollmentEnd < request.PreEnrollmentStart)
            throw new InvalidOperationException("La prematrícula no puede terminar antes de empezar.");

        if (request.AdmissionEnd < request.AdmissionStart)
            throw new InvalidOperationException("Las solicitudes de admisión no pueden terminar antes de empezar.");

        var period = await FindAsync(yearId);
        if (period is null)
        {
            period = new EnrollmentPeriod { YearId = yearId };
            db.EnrollmentPeriods.Add(period);
        }

        period.PreEnrollmentStart = request.PreEnrollmentStart;
        period.PreEnrollmentEnd = request.PreEnrollmentEnd;
        period.AdmissionStart = request.AdmissionStart;
        period.AdmissionEnd = request.AdmissionEnd;

        await db.SaveChangesAsync();
        return ToDto(period);
    }

    private static EnrollmentPeriodDto ToDto(EnrollmentPeriod p) =>
        new(p.YearId, p.PreEnrollmentStart, p.PreEnrollmentEnd, p.AdmissionStart, p.AdmissionEnd);
}
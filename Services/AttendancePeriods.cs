using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services;

public static class AttendancePeriods
{
    public static DateOnly Start(AcademicPeriod period) => DateOnly.FromDateTime(period.StartDate);

    public static DateOnly End(AcademicPeriod period) => DateOnly.FromDateTime(period.EndDate);

    public static bool Contains(AcademicPeriod period, DateOnly date) => date >= Start(period) && date <= End(period);

    public static async Task<AcademicPeriod?> FindAsync(AppDbContext db, short yearId, DateOnly date)
    {
        var periods = await db.AcademicPeriods
            .AsNoTracking()
            .Where(p => p.YearId == yearId)
            .ToListAsync();

        return periods.FirstOrDefault(period => Contains(period, date));
    }
}

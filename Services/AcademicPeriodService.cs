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
    }
}

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
    }
}

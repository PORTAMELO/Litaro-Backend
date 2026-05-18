using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services
{
    public class EnrollmentService(AppDbContext db)
    {
        public Task<List<Enrollment>> GetAllAsync() =>
            db.Enrollments.ToListAsync();

        public async Task<Enrollment?> GetByIdAsync(int id) =>
            await db.Enrollments.FindAsync(id);

        public async Task<Enrollment> CreateAsync(Enrollment enrollment)
        {
            db.Enrollments.Add(enrollment);
            await db.SaveChangesAsync();
            return enrollment;
        }

        public async Task<bool> UpdateStatusAsync(int id, string status)
        {
            var enrollment = await db.Enrollments.FindAsync(id);
            if (enrollment is null) return false;

            enrollment.Status = status;
            await db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var enrollment = await db.Enrollments.FindAsync(id);
            if (enrollment is null) return false;

            db.Enrollments.Remove(enrollment);
            await db.SaveChangesAsync();
            return true;
        }
    }
}

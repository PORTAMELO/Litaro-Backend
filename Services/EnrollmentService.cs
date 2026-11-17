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
    }
}

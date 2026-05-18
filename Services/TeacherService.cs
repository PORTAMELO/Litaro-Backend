using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services
{
    public class TeacherService(AppDbContext db)
    {
        public Task<List<Teacher>> GetAllAsync() =>
            db.Teachers.ToListAsync();

        public async Task<Teacher?> GetByIdAsync(int id) =>
            await db.Teachers.FindAsync(id);

        public async Task<Teacher> CreateAsync(Teacher teacher)
        {
            db.Teachers.Add(teacher);
            await db.SaveChangesAsync();
            return teacher;
        }

        public async Task<bool> UpdateAsync(int id, Teacher updated)
        {
            var teacher = await db.Teachers.FindAsync(id);
            if (teacher is null) return false;

            teacher.Specialty = updated.Specialty;

            await db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var teacher = await db.Teachers.FindAsync(id);
            if (teacher is null) return false;

            db.Teachers.Remove(teacher);
            await db.SaveChangesAsync();
            return true;
        }
    }
}

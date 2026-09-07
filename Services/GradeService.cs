using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services
{
    public class GradeService(AppDbContext db)
    {
        public Task<List<Grade>> GetAllAsync() =>
            db.Grades.ToListAsync();

        public async Task<Grade?> GetByIdAsync(int id) =>
            await db.Grades.FindAsync(id);

        public async Task<Grade> CreateAsync(Grade grade)
        {
            db.Grades.Add(grade);
            await db.SaveChangesAsync();
            return grade;
        }

        public async Task<bool> UpdateAsync(int id, Grade updated)
        {
            var grade = await db.Grades.FindAsync(id);
            if (grade is null) return false;

            grade.Name = updated.Name;
            grade.OrderNum = updated.OrderNum;
            grade.Level = updated.Level;

            await db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var grade = await db.Grades.FindAsync(id);
            if (grade is null) return false;

            db.Grades.Remove(grade);
            await db.SaveChangesAsync();
            return true;
        }
    }
}

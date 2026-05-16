using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services
{
    public class ParentStudentService(AppDbContext db)
    {
        public Task<List<ParentStudent>> GetAllAsync() =>
            db.ParentStudents.ToListAsync();

        public async Task<ParentStudent?> GetByIdAsync(int id) =>
            await db.ParentStudents.FindAsync(id);

        public async Task<ParentStudent> CreateAsync(ParentStudent parentStudent)
        {
            db.ParentStudents.Add(parentStudent);
            await db.SaveChangesAsync();
            return parentStudent;
        }

        public async Task<bool> UpdateAsync(int id, ParentStudent updated)
        {
            var parentStudent = await db.ParentStudents.FindAsync(id);
            if (parentStudent is null) return false;

            parentStudent.PrimaryContact = updated.PrimaryContact;

            await db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var parentStudent = await db.ParentStudents.FindAsync(id);
            if (parentStudent is null) return false;

            db.ParentStudents.Remove(parentStudent);
            await db.SaveChangesAsync();
            return true;
        }
    }
}

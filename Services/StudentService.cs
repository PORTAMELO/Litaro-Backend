using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services
{
    public class StudentService(AppDbContext db)
    {
        public Task<List<Student>> GetAllAsync() =>
            db.Students.ToListAsync();

        public async Task<Student?> GetByIdAsync(int id) =>
            await db.Students.FindAsync(id);

        public async Task<Student> CreateAsync(Student student)
        {
            db.Students.Add(student);
            await db.SaveChangesAsync();
            return student;
        }

        public async Task<bool> UpdateAsync(int id, Student updated)
        {
            var student = await db.Students.FindAsync(id);
            if (student is null) return false;

            student.StudentCode = updated.StudentCode;
            student.BirthDate = updated.BirthDate;
            student.Gender = updated.Gender;

            await db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var student = await db.Students.FindAsync(id);
            if (student is null) return false;

            db.Students.Remove(student);
            await db.SaveChangesAsync();
            return true;
        }
    }
}

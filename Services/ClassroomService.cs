using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services
{
    public class ClassroomService(AppDbContext db)
    {
        public Task<List<Classroom>> GetAllAsync() =>
            db.Classrooms.Where(c => c.Active).ToListAsync();

        public async Task<Classroom?> GetByIdAsync(int id) =>
            await db.Classrooms.FindAsync(id);

        public async Task<Classroom> CreateAsync(Classroom classroom)
        {
            db.Classrooms.Add(classroom);
            await db.SaveChangesAsync();
            return classroom;
        }

        public async Task<bool> UpdateAsync(int id, Classroom updated)
        {
            var classroom = await db.Classrooms.FindAsync(id);
            if (classroom is null) return false;

            classroom.Name = updated.Name;
            classroom.GradeId = updated.GradeId;
            classroom.CampusId = updated.CampusId;

            await db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeactivateAsync(int id)
        {
            var classroom = await db.Classrooms.FindAsync(id);
            if (classroom is null) return false;

            classroom.Active = false;
            await db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ActivateAsync(int id)
        {
            var classroom = await db.Classrooms.FindAsync(id);
            if (classroom is null) return false;

            classroom.Active = true;
            await db.SaveChangesAsync();
            return true;
        }
    }
}

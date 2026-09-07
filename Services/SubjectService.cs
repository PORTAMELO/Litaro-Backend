using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services
{
    public class SubjectService(AppDbContext db)
    {
        public Task<List<Subject>> GetAllAsync() =>
            db.Subjects.ToListAsync();

        public async Task<Subject?> GetByIdAsync(int id) =>
            await db.Subjects.FindAsync(id);

        public async Task<Subject> CreateAsync(Subject subject)
        {
            db.Subjects.Add(subject);
            await db.SaveChangesAsync();
            return subject;
        }

        public async Task<bool> UpdateAsync(int id, Subject updated)
        {
            var subject = await db.Subjects.FindAsync(id);
            if (subject is null) return false;

            subject.Name = updated.Name;
            subject.WeeklyHours = updated.WeeklyHours;
            subject.KnowledgeArea = updated.KnowledgeArea;

            await db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var subject = await db.Subjects.FindAsync(id);
            if (subject is null) return false;

            db.Subjects.Remove(subject);
            await db.SaveChangesAsync();
            return true;
        }
    }
}

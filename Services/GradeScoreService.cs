using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services
{
    public class GradeScoreService(AppDbContext db)
    {
        public Task<List<GradeScore>> GetAllAsync() =>
            db.GradeScores.ToListAsync();

        public async Task<GradeScore?> GetByIdAsync(int id) =>
            await db.GradeScores.FindAsync(id);

        public async Task<GradeScore> CreateAsync(GradeScore gradeScore)
        {
            db.GradeScores.Add(gradeScore);
            await db.SaveChangesAsync();
            return gradeScore;
        }

        public async Task<bool> UpdateAsync(int id, GradeScore updated)
        {
            var gradeScore = await db.GradeScores.FindAsync(id);
            if (gradeScore is null) return false;

            gradeScore.Value = updated.Value;
            gradeScore.Description = updated.Description;

            await db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var gradeScore = await db.GradeScores.FindAsync(id);
            if (gradeScore is null) return false;

            db.GradeScores.Remove(gradeScore);
            await db.SaveChangesAsync();
            return true;
        }
    }
}

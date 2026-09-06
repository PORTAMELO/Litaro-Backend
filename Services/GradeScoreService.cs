using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services
{
    public class GradeScoreService(AppDbContext db)
    {
        public Task<List<GradeScore>> GetAllAsync(IDictionary<string, string>? filters = null)
        {
            var query = db.GradeScores.AsQueryable();

            if (filters is not null && filters.Count > 0)
                query = query.ApplyFilters(filters);

            return query.ToListAsync();
        }

        public Task<GradeScore?> GetByIdAsync(int id) =>
            db.GradeScores.FirstOrDefaultAsync(gs => gs.GradeScoreId == id);
    }
}

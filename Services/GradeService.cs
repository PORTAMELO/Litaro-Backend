using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services
{
    public class GradeService(AppDbContext db)
    {
        public Task<List<Grade>> GetAllAsync(IDictionary<string, string>? filters = null)
        {
            var query = db.Grades.AsQueryable();

            if (filters is not null && filters.Count > 0)
                query = query.ApplyFilters(filters);

            return query.ToListAsync();
        }

        public Task<Grade?> GetByIdAsync(int id) =>
            db.Grades.FirstOrDefaultAsync(g => g.GradeId == id);
    }
}

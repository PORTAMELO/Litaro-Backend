using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services
{
    public class SubjectService(AppDbContext db)
    {
        public Task<List<Subject>> GetAllAsync(IDictionary<string, string>? filters = null)
        {
            var query = db.Subjects.AsQueryable();

            if (filters is not null && filters.Count > 0)
                query = query.ApplyFilters(filters);

            return query.ToListAsync();
        }

        public Task<Subject?> GetByIdAsync(int id) =>
            db.Subjects.FirstOrDefaultAsync(s => s.SubjectId == id);
    }
}

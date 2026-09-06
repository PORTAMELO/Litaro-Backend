using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services
{
    public class ClassroomService(AppDbContext db)
    {
        public Task<List<Classroom>> GetAllAsync(IDictionary<string, string>? filters = null)
        {
            var query = db.Classrooms.Where(c => c.Active).AsQueryable();

            if (filters is not null && filters.Count > 0)
                query = query.ApplyFilters(filters);

            return query.ToListAsync();
        }

        public Task<Classroom?> GetByIdAsync(int id) =>
            db.Classrooms.FirstOrDefaultAsync(c => c.ClassroomId == id && c.Active);
    }
}

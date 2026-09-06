using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services
{
    public class ScheduleService(AppDbContext db)
    {
        public Task<List<Schedule>> GetAllAsync(IDictionary<string, string>? filters = null)
        {
            var query = db.Schedules.AsQueryable();

            if (filters is not null && filters.Count > 0)
                query = query.ApplyFilters(filters);

            return query.ToListAsync();
        }

        public Task<Schedule?> GetByIdAsync(int id) =>
            db.Schedules.FirstOrDefaultAsync(s => s.ScheduleId == id);
    }
}

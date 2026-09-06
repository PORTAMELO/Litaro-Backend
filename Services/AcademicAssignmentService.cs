using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services
{
    public class AcademicAssignmentService(AppDbContext db)
    {
        public Task<List<AcademicAssignment>> GetAllAsync(IDictionary<string, string>? filters = null)
        {
            var query = db.AcademicAssignments.Where(a => a.Active).AsQueryable();

            if (filters is not null && filters.Count > 0)
                query = query.ApplyFilters(filters);

            return query.ToListAsync();
        }

        public Task<AcademicAssignment?> GetByIdAsync(int id) =>
            db.AcademicAssignments.FirstOrDefaultAsync(a => a.AssignmentId == id && a.Active);
    }
}

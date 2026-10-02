using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services
{
    public class ClassroomService(AppDbContext db)
    {
        public record ScheduleSettingsRequest(short? ClassMinutes, int? DirectorTeacherId);

        public Task<List<Classroom>> GetAllAsync(IDictionary<string, string>? filters = null)
        {
            var query = db.Classrooms.Where(c => c.Active).AsQueryable();

            if (filters is not null && filters.Count > 0)
                query = query.ApplyFilters(filters);

            return query.ToListAsync();
        }

        public Task<Classroom?> GetByIdAsync(int id) =>
            db.Classrooms.FirstOrDefaultAsync(c => c.ClassroomId == id && c.Active);

        public async Task<Classroom?> UpdateScheduleSettingsAsync(int id, ScheduleSettingsRequest request)
        {
            var classroom = await db.Classrooms.FirstOrDefaultAsync(c => c.ClassroomId == id && c.Active);
            if (classroom is null) return null;

            if (request.ClassMinutes is < 20 or > 180)
                throw new InvalidOperationException("La duración de la hora de clase debe estar entre 20 y 180 minutos.");

            if (request.DirectorTeacherId is not null &&
                !await db.Teachers.AnyAsync(t => t.TeacherId == request.DirectorTeacherId && t.Active))
                throw new InvalidOperationException("El director de grupo no existe o está inactivo.");

            classroom.ClassMinutes = request.ClassMinutes;
            classroom.DirectorTeacherId = request.DirectorTeacherId;
            await db.SaveChangesAsync();
            return classroom;
        }
    }
}

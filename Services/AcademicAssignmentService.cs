using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services
{
    public class AcademicAssignmentService(AppDbContext db)
    {
        public record ChangeTeacherRequest(int TeacherId);

        public record CreateAssignmentRequest(short YearId, int ClassroomId, int SubjectId, int TeacherId);

        public Task<List<AcademicAssignment>> GetAllAsync(IDictionary<string, string>? filters = null)
        {
            var query = db.AcademicAssignments.Where(a => a.Active).AsQueryable();

            if (filters is not null && filters.Count > 0)
                query = query.ApplyFilters(filters);

            return query.ToListAsync();
        }

        public Task<AcademicAssignment?> GetByIdAsync(int id) =>
            db.AcademicAssignments.FirstOrDefaultAsync(a => a.AssignmentId == id && a.Active);

        public async Task<AcademicAssignment> CreateAsync(CreateAssignmentRequest request)
        {
            if (!await db.AcademicYears.AnyAsync(y => y.YearId == request.YearId))
                throw new InvalidOperationException($"El año lectivo {request.YearId} no existe.");
            if (!await db.Classrooms.AnyAsync(c => c.ClassroomId == request.ClassroomId && c.Active))
                throw new InvalidOperationException("El curso no existe o está inactivo.");
            if (!await db.Subjects.AnyAsync(s => s.SubjectId == request.SubjectId))
                throw new InvalidOperationException("La materia no existe.");
            if (!await db.Teachers.AnyAsync(t => t.TeacherId == request.TeacherId && t.Active))
                throw new InvalidOperationException("El docente no existe o está inactivo.");

            var assignment = new AcademicAssignment
            {
                YearId = request.YearId,
                ClassroomId = request.ClassroomId,
                SubjectId = request.SubjectId,
                TeacherId = request.TeacherId,
            };
            db.AcademicAssignments.Add(assignment);

            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
            {
                throw new InvalidOperationException("Ese curso ya tiene asignada esa materia en el año. Para cambiar el docente, usa \"Cambiar docente\".");
            }
            return assignment;
        }

        public async Task<AcademicAssignment?> ChangeTeacherAsync(int assignmentId, int newTeacherId)
        {
            var assignment = await db.AcademicAssignments.FirstOrDefaultAsync(a => a.AssignmentId == assignmentId && a.Active);
            if (assignment is null) return null;
            if (assignment.TeacherId == newTeacherId) return assignment;

            if (!await db.Teachers.AnyAsync(t => t.TeacherId == newTeacherId && t.Active))
                throw new InvalidOperationException("El docente no existe o está inactivo.");

            await using var tx = await db.Database.BeginTransactionAsync();
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock({ScheduleService.ScheduleLockKey}, {(int)assignment.YearId})");

            var blocks = await db.Schedules.Where(s => s.AssignmentId == assignmentId).ToListAsync();
            foreach (var b in blocks)
            {
                var clash = await db.Schedules.AnyAsync(s =>
                    s.TeacherId == newTeacherId && s.YearId == b.YearId && s.Weekday == b.Weekday &&
                    s.StartTime < b.EndTime && s.EndTime > b.StartTime);
                if (clash)
                    throw new ScheduleConflictException(
                        $"El nuevo docente ya tiene un compromiso el {ScheduleService.DayNames[b.Weekday]} de {b.StartTime:HH\\:mm} a {b.EndTime:HH\\:mm}.");
            }

            assignment.TeacherId = newTeacherId;
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return assignment;
        }
    }
}

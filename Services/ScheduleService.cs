using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services
{
    public class ScheduleConflictException(string message) : Exception(message);

    public class ScheduleService(AppDbContext db)
    {
        public const int ScheduleLockKey = 4201;

        public static readonly string[] DayNames = ["", "lunes", "martes", "miércoles", "jueves", "viernes", "sábado"];

        public record SaveScheduleRequest(
            short YearId,
            string Type,
            byte Weekday,
            TimeOnly StartTime,
            TimeOnly EndTime,
            int? AssignmentId,
            int? TeacherId,
            int? SpaceId,
            int? CampusId,
            string? Title);

        public record ScheduleView(
            int ScheduleId, short YearId, int CampusId, string CampusName, string Type, string? Title,
            byte Weekday, TimeOnly StartTime, TimeOnly EndTime,
            int TeacherId, string TeacherName,
            int? AssignmentId, int? ClassroomId, string? ClassroomName, string? SubjectName,
            int? SpaceId, string? SpaceName);

        public record ScheduleSaveResult(ScheduleView Schedule, List<string> Warnings);

        public record SubjectProgress(int SubjectId, string Subject, int Planned, double Scheduled, string Status);

        public record ParentAttentionRow(int TeacherId, string TeacherName, byte Weekday,
            TimeOnly StartTime, TimeOnly EndTime, string? DirectorOf);

        public Task<List<Schedule>> GetAllAsync(IDictionary<string, string>? filters = null)
        {
            var query = db.Schedules.AsQueryable();

            if (filters is not null && filters.Count > 0)
                query = query.ApplyFilters(filters);

            return query.OrderBy(s => s.Weekday).ThenBy(s => s.StartTime).ToListAsync();
        }

        public Task<Schedule?> GetByIdAsync(int id) =>
            db.Schedules.FirstOrDefaultAsync(s => s.ScheduleId == id);

        private static IQueryable<ScheduleView> Project(IQueryable<Schedule> query) => query.Select(s => new ScheduleView(
            s.ScheduleId, s.YearId, s.CampusId, s.Campus.Name, s.Type, s.Title,
            s.Weekday, s.StartTime, s.EndTime,
            s.TeacherId, s.Teacher.User.FirstName + " " + s.Teacher.User.LastName,
            s.AssignmentId, s.ClassroomId, s.Classroom!.Name, s.AcademicAssignment!.Subject.Name,
            s.SpaceId, s.Space!.Name));

        private static Task<List<ScheduleView>> ToOrderedViewAsync(IQueryable<Schedule> query) =>
            Project(query.OrderBy(s => s.Weekday).ThenBy(s => s.StartTime)).ToListAsync();

        public Task<List<ScheduleView>> GetViewAsync(IDictionary<string, string> filters) =>
            ToOrderedViewAsync(db.Schedules.ApplyFilters(filters));

        public Task<ScheduleView?> GetViewByIdAsync(int id) =>
            Project(db.Schedules.Where(s => s.ScheduleId == id)).FirstOrDefaultAsync();

        public async Task<ScheduleSaveResult> SaveAsync(int? scheduleId, SaveScheduleRequest req)
        {
            var type = (req.Type ?? string.Empty).Trim().ToUpperInvariant();
            if (!ScheduleTypes.All.Contains(type))
                throw new InvalidOperationException($"Tipo de bloque inválido. Valores permitidos: {string.Join(", ", ScheduleTypes.All)}.");
            if (req.Weekday is < 1 or > 6)
                throw new InvalidOperationException("El día debe estar entre 1 (lunes) y 6 (sábado).");
            if (req.EndTime <= req.StartTime)
                throw new InvalidOperationException("La hora de fin debe ser mayor que la hora de inicio.");
            if (!await db.AcademicYears.AnyAsync(y => y.YearId == req.YearId))
                throw new InvalidOperationException($"El año lectivo {req.YearId} no existe.");

            await using var tx = await db.Database.BeginTransactionAsync();
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({ScheduleLockKey}, {(int)req.YearId})");

            Schedule? entity = null;
            if (scheduleId is not null)
            {
                entity = await db.Schedules.FirstOrDefaultAsync(s => s.ScheduleId == scheduleId)
                    ?? throw new KeyNotFoundException($"No existe un bloque con id {scheduleId}.");
            }

            int teacherId;
            int? classroomId = null;
            int? assignmentId = null;
            int? campusId = req.CampusId;
            int gradeId = 0, subjectId = 0;
            short? classMinutes = null;

            if (type == ScheduleTypes.Class)
            {
                if (req.AssignmentId is null)
                    throw new InvalidOperationException("Una clase requiere una asignación académica.");

                var assignment = await db.AcademicAssignments
                    .Include(a => a.Classroom)
                    .FirstOrDefaultAsync(a => a.AssignmentId == req.AssignmentId && a.Active)
                    ?? throw new InvalidOperationException("La asignación académica no existe o está inactiva.");

                if (assignment.YearId != req.YearId)
                    throw new InvalidOperationException("La asignación pertenece a otro año lectivo.");
                if (campusId is not null && campusId != assignment.Classroom.CampusId)
                    throw new InvalidOperationException("El curso de esta asignación no pertenece a la sede seleccionada.");

                teacherId = assignment.TeacherId;
                classroomId = assignment.ClassroomId;
                assignmentId = assignment.AssignmentId;
                campusId = assignment.Classroom.CampusId;
                gradeId = assignment.Classroom.GradeId;
                subjectId = assignment.SubjectId;
                classMinutes = assignment.Classroom.ClassMinutes;
            }
            else
            {
                if (req.TeacherId is null)
                    throw new InvalidOperationException("Debe indicar el docente del bloque.");
                if (!await db.Teachers.AnyAsync(t => t.TeacherId == req.TeacherId && t.Active))
                    throw new InvalidOperationException("El docente no existe o está inactivo.");
                teacherId = req.TeacherId.Value;
            }

            Space? space = null;
            if (req.SpaceId is not null)
            {
                space = await db.Spaces.FirstOrDefaultAsync(s => s.SpaceId == req.SpaceId && s.Active)
                    ?? throw new InvalidOperationException("El espacio no existe o está inactivo.");

                if (campusId is not null && space.CampusId != campusId)
                    throw new InvalidOperationException($"El espacio \"{space.Name}\" está en otra sede.");
                campusId = space.CampusId;
            }

            if (campusId is null)
                throw new InvalidOperationException("Debe indicar la sede del bloque.");
            if (!await db.Campuses.AnyAsync(c => c.CampusId == campusId && c.Active))
                throw new InvalidOperationException("La sede no existe o está inactiva.");

            if (type == ScheduleTypes.Class && space?.Capacity is short capacity)
            {
                var enrolled = await db.Enrollments.CountAsync(e =>
                    e.ClassroomId == classroomId && e.YearId == req.YearId && e.Status == "ACTIVE");
                if (enrolled > capacity)
                    throw new InvalidOperationException(
                        $"El espacio \"{space.Name}\" tiene {capacity} puestos y el curso tiene {enrolled} estudiantes matriculados.");
            }

            var sameSlot = db.Schedules.Where(s =>
                s.YearId == req.YearId &&
                s.Weekday == req.Weekday &&
                s.StartTime < req.EndTime && s.EndTime > req.StartTime &&
                (scheduleId == null || s.ScheduleId != scheduleId));

            var teacherClash = await sameSlot.FirstOrDefaultAsync(s => s.TeacherId == teacherId);
            if (teacherClash is not null)
                throw new ScheduleConflictException("El docente ya tiene " + await DescribeAsync(teacherClash) + ".");

            if (classroomId is not null)
            {
                var classClash = await sameSlot.FirstOrDefaultAsync(s => s.ClassroomId == classroomId);
                if (classClash is not null)
                    throw new ScheduleConflictException("El curso ya tiene " + await DescribeAsync(classClash) + ".");
            }

            if (space is not null && type == ScheduleTypes.Class)
            {
                var spaceClash = await sameSlot.FirstOrDefaultAsync(s =>
                    s.SpaceId == space.SpaceId && s.Type == ScheduleTypes.Class);
                if (spaceClash is not null)
                    throw new ScheduleConflictException($"El espacio \"{space.Name}\" ya está ocupado por " + await DescribeAsync(spaceClash) + ".");
            }

            if (entity is null)
            {
                entity = new Schedule();
                db.Schedules.Add(entity);
            }

            entity.YearId = req.YearId;
            entity.CampusId = campusId.Value;
            entity.Type = type;
            entity.Title = string.IsNullOrWhiteSpace(req.Title) ? null : req.Title.Trim();
            entity.Weekday = req.Weekday;
            entity.StartTime = req.StartTime;
            entity.EndTime = req.EndTime;
            entity.TeacherId = teacherId;
            entity.AssignmentId = assignmentId;
            entity.ClassroomId = classroomId;
            entity.SpaceId = space?.SpaceId;

            try
            {
                await db.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch (DbUpdateException ex)
            {
                throw new InvalidOperationException(ex.InnerException?.Message ?? ex.Message);
            }

            var warnings = new List<string>();

            if (type == ScheduleTypes.Class)
            {
                var ranges = await db.TeacherAvailabilities
                    .Where(r => r.TeacherId == teacherId && r.YearId == req.YearId)
                    .ToListAsync();
                if (ranges.Count > 0)
                {
                    var fits = ranges.Any(r =>
                        r.Weekday == req.Weekday &&
                        r.StartTime <= req.StartTime && r.EndTime >= req.EndTime &&
                        (r.CampusId == null || r.CampusId == campusId));
                    if (!fits)
                        warnings.Add($"El bloque queda fuera de la disponibilidad registrada del docente para el {DayNames[req.Weekday]}.");
                }

                var plan = await db.StudyPlans.FirstOrDefaultAsync(p =>
                    p.YearId == req.YearId && p.GradeId == gradeId && p.SubjectId == subjectId);
                if (plan is not null)
                {
                    var scheduled = await ScheduledHoursAsync(classroomId!.Value, subjectId, req.YearId, classMinutes);
                    if (scheduled > plan.WeeklyHours + 0.05)
                        warnings.Add($"Se superan las horas del plan de estudios: {scheduled:0.#} / {plan.WeeklyHours}.");
                }
            }

            var view = await GetViewByIdAsync(entity.ScheduleId);
            return new ScheduleSaveResult(view!, warnings);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var entity = await db.Schedules.FirstOrDefaultAsync(s => s.ScheduleId == id);
            if (entity is null) return false;

            db.Schedules.Remove(entity);
            await db.SaveChangesAsync();
            return true;
        }

        public async Task<List<SubjectProgress>> GetProgressAsync(int classroomId, short yearId)
        {
            var classroom = await db.Classrooms.FirstOrDefaultAsync(c => c.ClassroomId == classroomId)
                ?? throw new InvalidOperationException("El curso no existe.");

            var plan = await db.StudyPlans
                .Where(p => p.YearId == yearId && p.GradeId == classroom.GradeId)
                .Select(p => new { p.SubjectId, SubjectName = p.Subject.Name, p.WeeklyHours })
                .OrderBy(p => p.SubjectName)
                .ToListAsync();

            var result = new List<SubjectProgress>();
            foreach (var p in plan)
            {
                var scheduled = await ScheduledHoursAsync(classroomId, p.SubjectId, yearId, classroom.ClassMinutes);
                var status = scheduled < p.WeeklyHours - 0.05 ? "PENDING"
                           : scheduled > p.WeeklyHours + 0.05 ? "EXCEEDED" : "COMPLETE";
                result.Add(new SubjectProgress(p.SubjectId, p.SubjectName, p.WeeklyHours, Math.Round(scheduled, 1), status));
            }
            return result;
        }

        private async Task<double> ScheduledHoursAsync(int classroomId, int subjectId, short yearId, short? classMinutes)
        {
            var blocks = await db.Schedules
                .Where(s => s.ClassroomId == classroomId && s.YearId == yearId &&
                            s.Type == ScheduleTypes.Class && s.AcademicAssignment!.SubjectId == subjectId)
                .Select(s => new { s.StartTime, s.EndTime })
                .ToListAsync();

            var minutes = blocks.Sum(b => (b.EndTime - b.StartTime).TotalMinutes);
            return minutes / (classMinutes ?? 60);
        }

        public async Task<object> GetForUserAsync(int userId, short? yearId)
        {
            var today = DateTime.UtcNow;
            var year = yearId ?? await db.AcademicYears
                .Where(y => y.Active)
                .OrderByDescending(y => y.StartDate <= today && y.EndDate >= today)
                .ThenByDescending(y => y.YearId)
                .Select(y => y.YearId)
                .FirstOrDefaultAsync();

            var inYear = db.Schedules.Where(s => s.YearId == year);

            List<ScheduleView>? teacher = null;
            if (await db.Teachers.AnyAsync(t => t.TeacherId == userId && t.Active))
                teacher = await ToOrderedViewAsync(inYear.Where(s => s.TeacherId == userId));

            List<ScheduleView>? student = null;
            var myClassroom = await db.Enrollments
                .Where(e => e.StudentId == userId && e.YearId == year && e.Status == "ACTIVE")
                .Select(e => (int?)e.ClassroomId)
                .FirstOrDefaultAsync();
            if (myClassroom is not null)
                student = await ToOrderedViewAsync(inYear.Where(s => s.ClassroomId == myClassroom));

            var children = await db.ParentStudents
                .Where(ps => ps.ParentId == userId)
                .Select(ps => new
                {
                    ps.StudentId,
                    Name = ps.Student.User.FirstName + " " + ps.Student.User.LastName,
                    ClassroomId = db.Enrollments
                        .Where(e => e.StudentId == ps.StudentId && e.YearId == year && e.Status == "ACTIVE")
                        .Select(e => (int?)e.ClassroomId)
                        .FirstOrDefault()
                })
                .ToListAsync();

            var childViews = new List<object>();
            foreach (var c in children.Where(c => c.ClassroomId is not null))
            {
                var classes = await ToOrderedViewAsync(inYear.Where(s => s.ClassroomId == c.ClassroomId));
                var teacherIds = classes.Select(v => v.TeacherId).Distinct().ToList();
                var parentAttention = await ToOrderedViewAsync(inYear.Where(s =>
                    s.Type == ScheduleTypes.ParentAttention && teacherIds.Contains(s.TeacherId)));
                childViews.Add(new { c.StudentId, c.Name, classes, parentAttention });
            }

            return new { yearId = year, teacher, student, children = childViews };
        }

        public async Task<List<ParentAttentionRow>> GetParentAttentionBoardAsync(short yearId, int? campusId)
        {
            var rows = await db.Schedules
                .Where(s => s.YearId == yearId && s.Type == ScheduleTypes.ParentAttention &&
                            (campusId == null || s.CampusId == campusId))
                .OrderBy(s => s.Teacher.User.FirstName).ThenBy(s => s.Teacher.User.LastName)
                .ThenBy(s => s.Weekday).ThenBy(s => s.StartTime)
                .Select(s => new
                {
                    s.TeacherId,
                    TeacherName = s.Teacher.User.FirstName + " " + s.Teacher.User.LastName,
                    s.Weekday, s.StartTime, s.EndTime,
                    DirectorOf = db.Classrooms
                        .Where(c => c.DirectorTeacherId == s.TeacherId && c.Active)
                        .OrderBy(c => c.Name)
                        .Select(c => c.Name)
                        .ToList()
                })
                .ToListAsync();

            return rows.Select(r => new ParentAttentionRow(
                r.TeacherId, r.TeacherName, r.Weekday, r.StartTime, r.EndTime,
                r.DirectorOf.Count > 0 ? string.Join(", ", r.DirectorOf) : null)).ToList();
        }

        private async Task<string> DescribeAsync(Schedule s)
        {
            var info = await db.Schedules
                .Where(x => x.ScheduleId == s.ScheduleId)
                .Select(x => new
                {
                    Teacher = x.Teacher.User.FirstName + " " + x.Teacher.User.LastName,
                    Classroom = (string?)x.Classroom!.Name,
                    Subject = (string?)x.AcademicAssignment!.Subject.Name,
                })
                .FirstAsync();

            var what = s.Type switch
            {
                ScheduleTypes.Class => $"{info.Subject} con {info.Classroom}",
                ScheduleTypes.ParentAttention => "atención a padres",
                ScheduleTypes.Accompaniment => "zona de acompañamiento",
                ScheduleTypes.Meeting => s.Title ?? "una reunión",
                _ => s.Title ?? "otro compromiso",
            };
            return $"{what} ({info.Teacher}) el {DayNames[s.Weekday]} de {s.StartTime:HH\\:mm} a {s.EndTime:HH\\:mm}";
        }
    }
}

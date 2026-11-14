using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services;

public record MovementCampusDto(int CampusId, string Name);

public record MovementClassroomDto(int ClassroomId, string Name, int GradeId, string GradeName, int CampusId);

public record MovementOptionsDto(
    List<MovementCampusDto> Campuses,
    List<MovementClassroomDto> Classrooms,
    List<string> WithdrawalReasons);

public record MovementStudentDto(
    int EnrollmentId,
    int StudentId,
    string StudentCode,
    string Document,
    string StudentName,
    short YearId,
    string Status,
    string? StatusReason,
    int ClassroomId,
    string ClassroomName,
    int GradeId,
    string GradeName,
    int CampusId,
    string CampusName);

public record MovementEventDto(
    int EnrollmentEventId,
    string Type,
    DateTime CreatedAt,
    string FromClassroom,
    string FromCampus,
    string? ToClassroom,
    string? ToCampus,
    string? Reason,
    string? Note,
    string UserName);

public record MovementCertificateDto(
    int EnrollmentEventId,
    DateTime CreatedAt,
    string SchoolName,
    string StudentName,
    string Document,
    string StudentCode,
    short YearId,
    string GradeName,
    string FromClassroom,
    string FromCampus,
    string FromDane,
    string ToClassroom,
    string ToCampus,
    string ToDane,
    string? Note);

public class StudentMovementService(AppDbContext db, NotificationService notifications)
{
    public const string ReasonsCharacteristic = "Motivos de retiro SIMAT";

    private const int SearchLimit = 20;
    private const int NoteLength = 500;

    public async Task<MovementOptionsDto> GetOptionsAsync()
    {
        var campuses = await db.Campuses
            .AsNoTracking()
            .Where(c => c.Active)
            .OrderBy(c => c.Name)
            .Select(c => new MovementCampusDto(c.CampusId, c.Name))
            .ToListAsync();

        var classrooms = await db.Classrooms
            .AsNoTracking()
            .Where(c => c.Active && c.Campus.Active)
            .OrderBy(c => c.Grade.OrderNum)
            .ThenBy(c => c.Name)
            .Select(c => new MovementClassroomDto(c.ClassroomId, c.Name, c.GradeId, c.Grade.Name, c.CampusId))
            .ToListAsync();

        return new MovementOptionsDto(campuses, classrooms, await ReasonsAsync());
    }

    public async Task<List<MovementStudentDto>> SearchAsync(string? query)
    {
        var text = (query ?? string.Empty).Trim().ToLower();
        if (text.Length < 2)
            return [];

        var rows = await Students()
            .Where(e => e.AcademicYear.Active)
            .Where(e =>
                (e.Student.User.FirstName + " " + e.Student.User.LastName).ToLower().Contains(text) ||
                e.Student.User.DocumentNumber.ToLower().Contains(text) ||
                e.Student.StudentCode.ToLower().Contains(text))
            .OrderBy(e => e.Student.User.LastName)
            .ThenBy(e => e.Student.User.FirstName)
            .ThenByDescending(e => e.YearId)
            .Take(SearchLimit)
            .ToListAsync();

        return rows.Select(ToDto).ToList();
    }

    public async Task<MovementStudentDto?> GetStudentAsync(int enrollmentId)
    {
        var row = await Students().FirstOrDefaultAsync(e => e.EnrollmentId == enrollmentId);
        return row is null ? null : ToDto(row);
    }

    public async Task<List<MovementEventDto>> GetEventsAsync(int enrollmentId)
    {
        return await db.EnrollmentEvents
            .AsNoTracking()
            .Where(e => e.EnrollmentId == enrollmentId)
            .OrderByDescending(e => e.CreatedAt)
            .ThenByDescending(e => e.EnrollmentEventId)
            .Select(e => new MovementEventDto(
                e.EnrollmentEventId,
                e.Type,
                e.CreatedAt,
                e.FromClassroom.Name,
                e.FromClassroom.Campus.Name,
                e.ToClassroom != null ? e.ToClassroom.Name : null,
                e.ToClassroom != null ? e.ToClassroom.Campus.Name : null,
                e.Reason,
                e.Note,
                e.User.FirstName + " " + e.User.LastName))
            .ToListAsync();
    }

    public async Task<MovementCertificateDto?> GetCertificateAsync(int eventId)
    {
        var schoolName = await db.Schools
            .AsNoTracking()
            .Where(s => s.Active)
            .OrderBy(s => s.SchoolId)
            .Select(s => s.Name)
            .FirstOrDefaultAsync() ?? string.Empty;

        return await db.EnrollmentEvents
            .AsNoTracking()
            .Where(e => e.EnrollmentEventId == eventId && e.Type == EnrollmentEventTypes.Campus && e.ToClassroom != null)
            .Select(e => new MovementCertificateDto(
                e.EnrollmentEventId,
                e.CreatedAt,
                schoolName,
                e.Enrollment.Student.User.FirstName + " " + e.Enrollment.Student.User.LastName,
                e.Enrollment.Student.User.DocumentType + " " + e.Enrollment.Student.User.DocumentNumber,
                e.Enrollment.Student.StudentCode,
                e.Enrollment.YearId,
                e.ToClassroom!.Grade.Name,
                e.FromClassroom.Name,
                e.FromClassroom.Campus.Name,
                e.FromClassroom.Campus.Dane,
                e.ToClassroom!.Name,
                e.ToClassroom!.Campus.Name,
                e.ToClassroom!.Campus.Dane,
                e.Note))
            .FirstOrDefaultAsync();
    }

    public async Task<int> ChangeClassroomAsync(int enrollmentId, int toClassroomId, string? note, int userId)
    {
        var enrollment = await LoadActiveAsync(enrollmentId);
        var target = await LoadTargetAsync(enrollment, toClassroomId);

        if (target.CampusId != enrollment.Classroom.CampusId)
            throw new InvalidOperationException(
                "El curso de destino es de otra sede. Use el cambio de sede para trasladar al estudiante.");

        var origin = enrollment.Classroom;
        var movement = Move(enrollment, target, EnrollmentEventTypes.Classroom, note, userId);

        await notifications.QueueAsync(
            NotificationCodes.SimatClassroomChange,
            new Dictionary<string, string>
            {
                ["estudiante"] = FullName(enrollment.Student.User),
                ["documento"] = enrollment.Student.User.DocumentNumber,
                ["origen"] = origin.Name,
                ["destino"] = target.Name,
                ["sede"] = target.Campus.Name,
            },
            new NotificationTarget(StudentId: enrollment.StudentId, YearId: enrollment.YearId, CampusId: target.CampusId));

        await db.SaveChangesAsync();
        return movement.EnrollmentEventId;
    }

    public async Task<int> TransferCampusAsync(int enrollmentId, int toClassroomId, string? note, int userId)
    {
        var enrollment = await LoadActiveAsync(enrollmentId);
        var target = await LoadTargetAsync(enrollment, toClassroomId);

        if (target.CampusId == enrollment.Classroom.CampusId)
            throw new InvalidOperationException(
                "El curso de destino es de la misma sede. Use el cambio de curso.");

        var origin = enrollment.Classroom;
        var movement = Move(enrollment, target, EnrollmentEventTypes.Campus, note, userId);
        enrollment.Student.User.CampusId = target.CampusId;

        await notifications.QueueAsync(
            NotificationCodes.SimatCampusTransfer,
            new Dictionary<string, string>
            {
                ["estudiante"] = FullName(enrollment.Student.User),
                ["documento"] = enrollment.Student.User.DocumentNumber,
                ["origen"] = origin.Name,
                ["sede_origen"] = origin.Campus.Name,
                ["destino"] = target.Name,
                ["sede_destino"] = target.Campus.Name,
            },
            new NotificationTarget(StudentId: enrollment.StudentId, YearId: enrollment.YearId, CampusId: target.CampusId));

        await db.SaveChangesAsync();
        return movement.EnrollmentEventId;
    }

    public async Task<int> WithdrawAsync(int enrollmentId, string? reason, string? note, int userId)
    {
        var enrollment = await LoadActiveAsync(enrollmentId);

        var cleanReason = (reason ?? string.Empty).Trim();
        if (cleanReason.Length == 0)
            throw new InvalidOperationException("Seleccione el motivo del retiro.");

        var reasons = await ReasonsAsync();
        var official = reasons.FirstOrDefault(r => string.Equals(r, cleanReason, StringComparison.OrdinalIgnoreCase));
        if (official is null)
            throw new InvalidOperationException("El motivo del retiro no está en el catálogo de motivos del SIMAT.");

        var movement = new EnrollmentEvent
        {
            EnrollmentId = enrollment.EnrollmentId,
            Type = EnrollmentEventTypes.Withdrawal,
            FromClassroomId = enrollment.ClassroomId,
            Reason = official,
            Note = Clean(note),
            UserId = userId,
        };
        db.EnrollmentEvents.Add(movement);

        enrollment.Status = EnrollmentStatuses.Withdrawn;
        enrollment.StatusReason = official;

        await notifications.QueueAsync(
            NotificationCodes.SimatWithdrawal,
            new Dictionary<string, string>
            {
                ["estudiante"] = FullName(enrollment.Student.User),
                ["documento"] = enrollment.Student.User.DocumentNumber,
                ["curso"] = enrollment.Classroom.Name,
                ["anio"] = enrollment.YearId.ToString(),
                ["motivo"] = official,
            },
            new NotificationTarget(
                StudentId: enrollment.StudentId,
                YearId: enrollment.YearId,
                CampusId: enrollment.Classroom.CampusId));

        await db.SaveChangesAsync();
        return movement.EnrollmentEventId;
    }

    private IQueryable<Enrollment> Students() =>
        db.Enrollments
            .AsNoTracking()
            .Include(e => e.Student).ThenInclude(s => s.User)
            .Include(e => e.Classroom).ThenInclude(c => c.Grade)
            .Include(e => e.Classroom).ThenInclude(c => c.Campus);

    private async Task<List<string>> ReasonsAsync() =>
        await db.CharacteristicDetails
            .AsNoTracking()
            .Where(d => d.Characteristic!.Name == ReasonsCharacteristic)
            .OrderBy(d => d.Valor)
            .Select(d => d.Valor)
            .ToListAsync();

    private async Task<Enrollment> LoadActiveAsync(int enrollmentId)
    {
        var enrollment = await db.Enrollments
            .Include(e => e.Student).ThenInclude(s => s.User)
            .Include(e => e.Classroom).ThenInclude(c => c.Campus)
            .Include(e => e.AcademicYear)
            .FirstOrDefaultAsync(e => e.EnrollmentId == enrollmentId);

        if (enrollment is null)
            throw new InvalidOperationException("No existe la matrícula del estudiante.");

        if (enrollment.Status != EnrollmentStatuses.Active)
            throw new InvalidOperationException("La matrícula del estudiante no está activa.");

        if (!enrollment.AcademicYear.Active)
            throw new InvalidOperationException($"El año lectivo {enrollment.YearId} ya no está activo.");

        return enrollment;
    }

    private async Task<Classroom> LoadTargetAsync(Enrollment enrollment, int toClassroomId)
    {
        if (toClassroomId == enrollment.ClassroomId)
            throw new InvalidOperationException("El estudiante ya está en ese curso.");

        var target = await db.Classrooms
            .Include(c => c.Campus)
            .FirstOrDefaultAsync(c => c.ClassroomId == toClassroomId && c.Active);

        if (target is null)
            throw new InvalidOperationException("El curso de destino no existe o está inactivo.");

        if (target.GradeId != enrollment.Classroom.GradeId)
            throw new InvalidOperationException("El curso de destino debe ser del mismo grado que cursa el estudiante.");

        return target;
    }

    private EnrollmentEvent Move(Enrollment enrollment, Classroom target, string type, string? note, int userId)
    {
        var movement = new EnrollmentEvent
        {
            EnrollmentId = enrollment.EnrollmentId,
            Type = type,
            FromClassroomId = enrollment.ClassroomId,
            ToClassroomId = target.ClassroomId,
            Note = Clean(note),
            UserId = userId,
        };
        db.EnrollmentEvents.Add(movement);

        enrollment.ClassroomId = target.ClassroomId;
        enrollment.Classroom = target;
        return movement;
    }

    private static MovementStudentDto ToDto(Enrollment e) => new(
        e.EnrollmentId,
        e.StudentId,
        e.Student.StudentCode,
        e.Student.User.DocumentType + " " + e.Student.User.DocumentNumber,
        FullName(e.Student.User),
        e.YearId,
        e.Status,
        e.StatusReason,
        e.ClassroomId,
        e.Classroom.Name,
        e.Classroom.GradeId,
        e.Classroom.Grade.Name,
        e.Classroom.CampusId,
        e.Classroom.Campus.Name);

    private static string? Clean(string? note)
    {
        var text = note?.Trim();
        if (string.IsNullOrEmpty(text))
            return null;

        return text.Length > NoteLength ? text[..NoteLength] : text;
    }

    private static string FullName(User user) => $"{user.FirstName} {user.LastName}".Trim();
}

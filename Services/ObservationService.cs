using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services;

public record ObservationAccess(int UserId, bool SeesAll);

public static class ObservationScope
{
    // Año en curso: el activo que contiene hoy; si ninguno lo contiene, el activo más reciente.
    public static Task<short?> CurrentYearIdAsync(AppDbContext db)
    {
        var now = DateTime.UtcNow;

        return db.AcademicYears
            .AsNoTracking()
            .Where(y => y.Active)
            .OrderByDescending(y => y.StartDate <= now && y.EndDate >= now)
            .ThenByDescending(y => y.YearId)
            .Select(y => (short?)y.YearId)
            .FirstOrDefaultAsync();
    }

    public static IQueryable<Enrollment> Enrollments(AppDbContext db, ObservationAccess access)
    {
        if (access.SeesAll)
            return db.Enrollments;

        var userId = access.UserId;

        return db.Enrollments.Where(e =>
            e.Classroom.DirectorTeacherId == userId ||
            db.AcademicAssignments.Any(a =>
                a.ClassroomId == e.ClassroomId &&
                a.YearId == e.YearId &&
                a.TeacherId == userId &&
                a.Active));
    }
}

public record ObservationFilter(
    short? YearId = null,
    int? ClassroomId = null,
    int? CampusId = null,
    int? GradeId = null,
    int? StudentId = null,
    string? Kind = null,
    string? CoexistenceType = null,
    string? Query = null);

public record SaveObservationRequest(
    int EnrollmentId,
    DateOnly Date,
    string Description,
    bool IsAcademic,
    bool IsCoexistence,
    string? CoexistenceType,
    int? SubjectId);

public record SaveStatementRequest(string? Statement);

public record AddCommitmentRequest(string Text, DateOnly? FollowUpDate);

public record SetCommitmentStatusRequest(string Status, string? Note);

public record ObservationListItemDto(
    int ObservationId,
    int EnrollmentId,
    int StudentId,
    string StudentName,
    string ClassroomName,
    DateOnly Date,
    string Description,
    bool IsAcademic,
    bool IsCoexistence,
    string? CoexistenceType,
    string? SubjectName,
    string RecordedBy,
    bool HasStatement,
    int Commitments,
    int PendingCommitments);

public record CommitmentDto(
    int CommitmentId,
    string Text,
    string Status,
    DateOnly? FollowUpDate,
    string? Note,
    string CreatedBy,
    DateTime CreatedAt);

public record ObservationEventDto(string Action, string? Detail, string User, DateTime CreatedAt);

public record ObservationDetailDto(
    int ObservationId,
    int EnrollmentId,
    int StudentId,
    string StudentName,
    string ClassroomName,
    short YearId,
    DateOnly Date,
    string Description,
    bool IsAcademic,
    bool IsCoexistence,
    string? CoexistenceType,
    int? SubjectId,
    string? SubjectName,
    string? Statement,
    DocumentDto? StatementFile,
    int RecordedById,
    string RecordedBy,
    DateTime CreatedAt,
    bool CanEdit,
    List<CommitmentDto> Commitments,
    List<ObservationEventDto> Events);

public record ObservationStudentDto(int EnrollmentId, int StudentId, string StudentName, int ClassroomId, string ClassroomName);

public record ObservationClassroomDto(int ClassroomId, string Name, int CampusId, string CampusName, int GradeId, string GradeName);

public record ObservationSubjectDto(int SubjectId, string Name);

public record ObservationOptionsDto(
    short? YearId,
    List<short> Years,
    List<ObservationClassroomDto> Classrooms,
    List<ObservationSubjectDto> Subjects);

public class ObservationService(
    AppDbContext db,
    DocumentService documents,
    CitationService citations,
    ObservationConsolidatedService consolidated)
{
    public const string KindAcademic = "ACADEMIC";
    public const string KindCoexistence = "COEXISTENCE";

    private const int DescriptionLength = 2000;
    private const int StatementLength = 2000;
    private const int CommitmentLength = 500;
    private const int NoteLength = 300;
    private const int DetailLength = 500;
    private const int ListLimit = 500;
    private const int StudentLimit = 50;

    public async Task<ObservationOptionsDto> GetOptionsAsync(ObservationAccess access)
    {
        var yearId = await ObservationScope.CurrentYearIdAsync(db);

        var classroomIds = await ScopedEnrollments(access)
            .Where(e => e.YearId == yearId)
            .Select(e => e.ClassroomId)
            .Distinct()
            .ToListAsync();

        var classrooms = await db.Classrooms
            .AsNoTracking()
            .Where(c => classroomIds.Contains(c.ClassroomId))
            .OrderBy(c => c.Campus.Name)
            .ThenBy(c => c.Grade.OrderNum)
            .ThenBy(c => c.Name)
            .Select(c => new ObservationClassroomDto(c.ClassroomId, c.Name, c.CampusId, c.Campus.Name, c.GradeId, c.Grade.Name))
            .ToListAsync();

        var subjects = await db.Subjects
            .AsNoTracking()
            .OrderBy(s => s.Name)
            .Select(s => new ObservationSubjectDto(s.SubjectId, s.Name))
            .ToListAsync();

        var years = await db.AcademicYears
            .AsNoTracking()
            .OrderByDescending(y => y.YearId)
            .Select(y => y.YearId)
            .ToListAsync();

        return new ObservationOptionsDto(yearId, years, classrooms, subjects);
    }

    public async Task<List<ObservationStudentDto>> SearchStudentsAsync(ObservationAccess access, int? classroomId, string? query)
    {
        var text = (query ?? string.Empty).Trim().ToLower();
        var yearId = await ObservationScope.CurrentYearIdAsync(db);

        return await ScopedEnrollments(access)
            .AsNoTracking()
            .Where(e => e.YearId == yearId && e.Status == "ACTIVE")
            .Where(e => classroomId == null || e.ClassroomId == classroomId)
            .Where(e => text == string.Empty ||
                        e.Student.User.FirstName.ToLower().Contains(text) ||
                        e.Student.User.LastName.ToLower().Contains(text) ||
                        e.Student.User.DocumentNumber.Contains(text))
            .OrderBy(e => e.Student.User.LastName)
            .ThenBy(e => e.Student.User.FirstName)
            .Take(StudentLimit)
            .Select(e => new ObservationStudentDto(
                e.EnrollmentId,
                e.StudentId,
                e.Student.User.LastName + ", " + e.Student.User.FirstName,
                e.ClassroomId,
                e.Classroom.Name))
            .ToListAsync();
    }

    public async Task<List<ObservationListItemDto>> ListAsync(ObservationAccess access, ObservationFilter filter)
    {
        var text = (filter.Query ?? string.Empty).Trim().ToLower();
        var kind = filter.Kind?.Trim().ToUpperInvariant();
        var type = filter.CoexistenceType?.Trim().ToUpperInvariant();
        var enrollments = ScopedEnrollments(access);

        return await db.Observations
            .AsNoTracking()
            .Where(o => enrollments.Any(e => e.EnrollmentId == o.EnrollmentId))
            .Where(o => filter.YearId == null || o.Enrollment.YearId == filter.YearId)
            .Where(o => filter.ClassroomId == null || o.Enrollment.ClassroomId == filter.ClassroomId)
            .Where(o => filter.CampusId == null || o.Enrollment.Classroom.CampusId == filter.CampusId)
            .Where(o => filter.GradeId == null || o.Enrollment.Classroom.GradeId == filter.GradeId)
            .Where(o => filter.StudentId == null || o.Enrollment.StudentId == filter.StudentId)
            .Where(o => kind != KindAcademic || o.IsAcademic)
            .Where(o => kind != KindCoexistence || o.IsCoexistence)
            .Where(o => string.IsNullOrEmpty(type) || o.CoexistenceType == type)
            .Where(o => text == string.Empty ||
                        o.Description.ToLower().Contains(text) ||
                        o.Enrollment.Student.User.FirstName.ToLower().Contains(text) ||
                        o.Enrollment.Student.User.LastName.ToLower().Contains(text))
            .OrderByDescending(o => o.Date)
            .ThenByDescending(o => o.ObservationId)
            .Take(ListLimit)
            .Select(o => new ObservationListItemDto(
                o.ObservationId,
                o.EnrollmentId,
                o.Enrollment.StudentId,
                o.Enrollment.Student.User.LastName + ", " + o.Enrollment.Student.User.FirstName,
                o.Enrollment.Classroom.Name,
                o.Date,
                o.Description,
                o.IsAcademic,
                o.IsCoexistence,
                o.CoexistenceType,
                o.Subject != null ? o.Subject.Name : null,
                o.RecordedBy.FirstName + " " + o.RecordedBy.LastName,
                o.Statement != null,
                o.Commitments.Count,
                o.Commitments.Count(c => c.Status == CommitmentStatuses.Pending)))
            .ToListAsync();
    }

    public async Task<ObservationDetailDto> GetAsync(ObservationAccess access, int observationId)
    {
        var observation = await FindInScopeAsync(access, observationId);

        var row = await db.Observations
            .AsNoTracking()
            .Where(o => o.ObservationId == observation.ObservationId)
            .Select(o => new
            {
                o.ObservationId,
                o.EnrollmentId,
                o.Enrollment.StudentId,
                StudentName = o.Enrollment.Student.User.LastName + ", " + o.Enrollment.Student.User.FirstName,
                ClassroomName = o.Enrollment.Classroom.Name,
                o.Enrollment.YearId,
                o.Date,
                o.Description,
                o.IsAcademic,
                o.IsCoexistence,
                o.CoexistenceType,
                o.SubjectId,
                SubjectName = o.Subject != null ? o.Subject.Name : null,
                o.Statement,
                o.RecordedById,
                RecordedBy = o.RecordedBy.FirstName + " " + o.RecordedBy.LastName,
                o.CreatedAt,
            })
            .FirstAsync();

        var commitments = await db.ObservationCommitments
            .AsNoTracking()
            .Where(c => c.ObservationId == observationId)
            .OrderBy(c => c.CreatedAt)
            .ThenBy(c => c.ObservationCommitmentId)
            .Select(c => new CommitmentDto(
                c.ObservationCommitmentId,
                c.Text,
                c.Status,
                c.FollowUpDate,
                c.Note,
                c.CreatedBy.FirstName + " " + c.CreatedBy.LastName,
                c.CreatedAt))
            .ToListAsync();

        var events = await db.ObservationEvents
            .AsNoTracking()
            .Where(e => e.ObservationId == observationId)
            .OrderBy(e => e.CreatedAt)
            .ThenBy(e => e.ObservationEventId)
            .Select(e => new ObservationEventDto(
                e.Action,
                e.Detail,
                e.User.FirstName + " " + e.User.LastName,
                e.CreatedAt))
            .ToListAsync();

        var files = await documents.CurrentByEntityAsync(DocumentTypes.ObservationStatement, [observationId]);

        return new ObservationDetailDto(
            row.ObservationId,
            row.EnrollmentId,
            row.StudentId,
            row.StudentName,
            row.ClassroomName,
            row.YearId,
            row.Date,
            row.Description,
            row.IsAcademic,
            row.IsCoexistence,
            row.CoexistenceType,
            row.SubjectId,
            row.SubjectName,
            row.Statement,
            files.GetValueOrDefault(observationId),
            row.RecordedById,
            row.RecordedBy,
            row.CreatedAt,
            CanEdit(access, observation),
            commitments,
            events);
    }

    public async Task<int> CreateAsync(ObservationAccess access, SaveObservationRequest request, DateOnly today)
    {
        var enrollment = await ScopedEnrollments(access)
            .FirstOrDefaultAsync(e => e.EnrollmentId == request.EnrollmentId)
            ?? throw new UnauthorizedAccessException();

        var values = await ValidateAsync(request, enrollment.YearId, today);

        var observation = new Observation
        {
            EnrollmentId = enrollment.EnrollmentId,
            Date = request.Date,
            Description = values.Description,
            IsAcademic = request.IsAcademic,
            IsCoexistence = request.IsCoexistence,
            CoexistenceType = values.CoexistenceType,
            SubjectId = request.SubjectId,
            RecordedById = access.UserId,
        };

        db.Observations.Add(observation);
        AddEvent(observation, ObservationActions.Create, KindLabel(observation.IsAcademic, observation.IsCoexistence, observation.CoexistenceType), access.UserId);
        await citations.CreateAutomaticAsync(observation, enrollment, access.UserId);
        await db.SaveChangesAsync();
        await CheckThresholdAsync(observation, enrollment);

        return observation.ObservationId;
    }

    public async Task UpdateAsync(ObservationAccess access, int observationId, SaveObservationRequest request, DateOnly today)
    {
        var observation = await FindInScopeAsync(access, observationId);

        if (!CanEdit(access, observation))
            throw new UnauthorizedAccessException();

        if (request.EnrollmentId != observation.EnrollmentId)
            throw new InvalidOperationException("No se puede cambiar el estudiante de una observación.");

        var values = await ValidateAsync(request, observation.Enrollment.YearId, today);
        var changes = new List<string>();

        if (observation.Date != request.Date)
            changes.Add($"Fecha: {observation.Date:dd/MM/yyyy} → {request.Date:dd/MM/yyyy}");

        var oldKind = KindLabel(observation.IsAcademic, observation.IsCoexistence, observation.CoexistenceType);
        var newKind = KindLabel(request.IsAcademic, request.IsCoexistence, values.CoexistenceType);
        if (oldKind != newKind)
            changes.Add($"Tipo: {oldKind} → {newKind}");

        if (observation.SubjectId != request.SubjectId)
            changes.Add("Materia modificada");

        if (observation.Description != values.Description)
            changes.Add("Descripción modificada");

        if (changes.Count == 0)
            return;

        observation.Date = request.Date;
        observation.Description = values.Description;
        observation.IsAcademic = request.IsAcademic;
        observation.IsCoexistence = request.IsCoexistence;
        observation.CoexistenceType = values.CoexistenceType;
        observation.SubjectId = request.SubjectId;

        AddEvent(observation, ObservationActions.Update, string.Join("; ", changes), access.UserId);
        await db.SaveChangesAsync();
        await CheckThresholdAsync(observation, observation.Enrollment);
    }

    public async Task SaveStatementAsync(ObservationAccess access, int observationId, SaveStatementRequest request)
    {
        var observation = await FindInScopeAsync(access, observationId);

        var statement = request.Statement?.Trim();
        if (statement is { Length: > StatementLength })
            throw new InvalidOperationException($"Los descargos no pueden superar {StatementLength} caracteres.");

        statement = string.IsNullOrEmpty(statement) ? null : statement;
        if (observation.Statement == statement)
            return;

        var detail = statement is null
            ? "Descargos transcritos eliminados"
            : observation.Statement is null ? "Descargos transcritos agregados" : "Descargos transcritos modificados";

        observation.Statement = statement;
        AddEvent(observation, ObservationActions.Statement, detail, access.UserId);
        await db.SaveChangesAsync();
    }

    public async Task<DocumentDto> UploadStatementFileAsync(ObservationAccess access, int observationId, DocumentUpload file, DateTime nowUtc)
    {
        var observation = await FindInScopeAsync(access, observationId);

        var document = await documents.UploadAsync(
            DocumentTypes.ObservationStatement,
            observation.ObservationId,
            observation.Enrollment.YearId,
            file,
            access.UserId,
            nowUtc);

        AddEvent(observation, ObservationActions.StatementFile, $"Formato de descargos cargado: {document.FileName}", access.UserId);
        await db.SaveChangesAsync();

        return document;
    }

    public async Task RemoveStatementFileAsync(ObservationAccess access, int observationId, DateTime nowUtc)
    {
        var observation = await FindInScopeAsync(access, observationId);

        if (!await documents.RemoveCurrentAsync(DocumentTypes.ObservationStatement, observationId, access.UserId, nowUtc))
            throw new InvalidOperationException("La observación no tiene formato de descargos.");

        AddEvent(observation, ObservationActions.StatementFileRemoved, "Formato de descargos retirado", access.UserId);
        await db.SaveChangesAsync();
    }

    public async Task<(Document Document, Stream Content)> OpenStatementFileAsync(ObservationAccess access, int observationId)
    {
        await FindInScopeAsync(access, observationId);

        return await documents.OpenCurrentAsync(DocumentTypes.ObservationStatement, observationId)
            ?? throw new InvalidOperationException("La observación no tiene formato de descargos.");
    }

    public async Task<int> AddCommitmentAsync(ObservationAccess access, int observationId, AddCommitmentRequest request)
    {
        var observation = await FindInScopeAsync(access, observationId);

        var text = request.Text?.Trim() ?? string.Empty;
        if (text.Length == 0)
            throw new InvalidOperationException("Escriba el compromiso.");

        if (text.Length > CommitmentLength)
            throw new InvalidOperationException($"El compromiso no puede superar {CommitmentLength} caracteres.");

        if (request.FollowUpDate is { } followUp && followUp < observation.Date)
            throw new InvalidOperationException("La fecha de seguimiento no puede ser anterior a la observación.");

        var commitment = new ObservationCommitment
        {
            Observation = observation,
            Text = text,
            FollowUpDate = request.FollowUpDate,
            CreatedById = access.UserId,
        };

        db.ObservationCommitments.Add(commitment);
        AddEvent(observation, ObservationActions.CommitmentAdded, Shorten(text), access.UserId);
        await db.SaveChangesAsync();

        return commitment.ObservationCommitmentId;
    }

    public async Task SetCommitmentStatusAsync(ObservationAccess access, int commitmentId, SetCommitmentStatusRequest request)
    {
        var commitment = await db.ObservationCommitments.FirstOrDefaultAsync(c => c.ObservationCommitmentId == commitmentId)
            ?? throw new InvalidOperationException("No existe el compromiso.");

        var observation = await FindInScopeAsync(access, commitment.ObservationId);

        var status = request.Status?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!CommitmentStatuses.All.Contains(status))
            throw new InvalidOperationException("Estado de compromiso no válido.");

        var note = request.Note?.Trim();
        if (note is { Length: > NoteLength })
            throw new InvalidOperationException($"La nota no puede superar {NoteLength} caracteres.");

        note = string.IsNullOrEmpty(note) ? null : note;

        if (status == CommitmentStatuses.Observation && note is null)
            throw new InvalidOperationException("Escriba la observación del compromiso.");

        if (commitment.Status == status && commitment.Note == note)
            return;

        var detail = commitment.Status == status
            ? $"{Shorten(commitment.Text, 120)}: nota actualizada"
            : $"{Shorten(commitment.Text, 120)}: {StatusLabel(commitment.Status)} → {StatusLabel(status)}";

        commitment.Status = status;
        commitment.Note = note;

        AddEvent(observation, ObservationActions.CommitmentStatus, detail, access.UserId);
        await db.SaveChangesAsync();
    }

    private IQueryable<Enrollment> ScopedEnrollments(ObservationAccess access) =>
        ObservationScope.Enrollments(db, access);

    public static string KindLabel(bool isAcademic, bool isCoexistence, string? coexistenceType)
    {
        var parts = new List<string>();

        if (isAcademic)
            parts.Add("Académica");

        if (isCoexistence)
            parts.Add($"Convivencia Tipo {coexistenceType}");

        return string.Join(" y ", parts);
    }

    private static string StatusLabel(string status) => status switch
    {
        CommitmentStatuses.Met => "Cumplió",
        CommitmentStatuses.NotMet => "No cumplió",
        CommitmentStatuses.Observation => "Observación",
        _ => "Pendiente",
    };

    private Task CheckThresholdAsync(Observation observation, Enrollment enrollment) =>
        observation.CoexistenceType == CoexistenceTypes.TypeI
            ? consolidated.CheckTypeIThresholdAsync(enrollment.StudentId, enrollment.YearId)
            : Task.CompletedTask;

    private static bool CanEdit(ObservationAccess access, Observation observation) =>
        access.SeesAll || observation.RecordedById == access.UserId;

    private async Task<Observation> FindInScopeAsync(ObservationAccess access, int observationId)
    {
        var observation = await db.Observations
            .Include(o => o.Enrollment)
            .FirstOrDefaultAsync(o => o.ObservationId == observationId)
            ?? throw new InvalidOperationException("No existe la observación.");

        var inScope = await ScopedEnrollments(access).AnyAsync(e => e.EnrollmentId == observation.EnrollmentId);
        if (!inScope)
            throw new UnauthorizedAccessException();

        return observation;
    }

    private async Task<(string Description, string? CoexistenceType)> ValidateAsync(SaveObservationRequest request, short yearId, DateOnly today)
    {
        var description = request.Description?.Trim() ?? string.Empty;
        if (description.Length == 0)
            throw new InvalidOperationException("Escriba la descripción de la observación.");

        if (description.Length > DescriptionLength)
            throw new InvalidOperationException($"La descripción no puede superar {DescriptionLength} caracteres.");

        if (!request.IsAcademic && !request.IsCoexistence)
            throw new InvalidOperationException("Marque si la observación es académica, de convivencia o ambas.");

        string? type = null;
        if (request.IsCoexistence)
        {
            type = request.CoexistenceType?.Trim().ToUpperInvariant();
            if (type is null || !CoexistenceTypes.All.Contains(type))
                throw new InvalidOperationException("La observación de convivencia debe tener tipificación Tipo I, II o III.");
        }

        if (request.Date > today)
            throw new InvalidOperationException("La fecha de la observación no puede ser futura.");

        if (request.Date.Year != yearId)
            throw new InvalidOperationException("La fecha debe estar dentro del año lectivo de la matrícula.");

        if (request.SubjectId is { } subjectId && !await db.Subjects.AnyAsync(s => s.SubjectId == subjectId))
            throw new InvalidOperationException("No existe la materia.");

        return (description, type);
    }

    private void AddEvent(Observation observation, string action, string? detail, int userId) =>
        db.ObservationEvents.Add(new ObservationEvent
        {
            Observation = observation,
            Action = action,
            Detail = detail is { Length: > DetailLength } ? detail[..DetailLength] : detail,
            UserId = userId,
        });

    private static string Shorten(string text, int length = DetailLength) =>
        text.Length <= length ? text : text[..(length - 1)] + "…";
}

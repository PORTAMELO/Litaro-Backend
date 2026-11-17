using System.Globalization;
using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services;

public record AdmissionDto(
    int AdmissionRequestId,
    short YearId,
    int GradeId,
    string GradeName,
    string FirstName,
    string LastName,
    string DocumentType,
    string DocumentNumber,
    DateOnly BirthDate,
    string GuardianName,
    string GuardianPhone,
    string? Notes,
    string Status,
    int? WaitlistPosition,
    DateTime CreatedAt,
    string CreatedBy,
    DateTime? DecidedAt,
    string? DecidedBy,
    string? DecisionNote,
    int MissingDocuments,
    bool Complete);

public record AdmissionEventDto(
    DateTime CreatedAt,
    string? FromStatus,
    string ToStatus,
    string UserName,
    string? Note);

public record AdmissionDetailDto(
    AdmissionDto Summary,
    int GradeId,
    StudentFormData Student,
    GuardianFormData Guardian,
    C600FormData C600,
    bool Complete,
    int? StudentId,
    ChecklistDto Documents);

public class AdmissionService(
    AppDbContext db,
    EnrollmentPeriodService periods,
    NotificationService notifications,
    EnrollmentFormValidator validator,
    EnrollmentDocumentService documents,
    EnrollmentPersonService people)
{
    private const int NotesLength = 500;
    private const int GuardianNameLength = 150;
    private const string UpdatedNote = "Solicitud actualizada.";
    private const int ListLength = 250;
    private const string FinalDecision = "La solicitud ya tiene una decisión final y no puede cambiarse.";

    public async Task<List<AdmissionDto>> ListAsync(short yearId)
    {
        var rows = await db.AdmissionRequests
            .AsNoTracking()
            .Where(a => a.YearId == yearId)
            .OrderBy(a => a.CreatedAt)
            .Select(a => new
            {
                Admission = a,
                GradeName = a.Grade.Name,
                CreatedBy = a.CreatedBy.FirstName + " " + a.CreatedBy.LastName,
                DecidedBy = a.DecidedBy != null ? a.DecidedBy.FirstName + " " + a.DecidedBy.LastName : null,
            })
            .ToListAsync();

        var position = new Dictionary<int, int>();
        foreach (var group in rows.Select(r => r.Admission).Where(a => a.Status == AdmissionStatuses.Waitlist).GroupBy(a => a.GradeId))
        {
            var place = 0;
            foreach (var admission in group.OrderBy(a => a.WaitlistedAt).ThenBy(a => a.AdmissionRequestId))
            {
                place++;
                position[admission.AdmissionRequestId] = place;
            }
        }

        var pendingIds = rows.Where(r => r.Admission.StudentId is null).Select(r => r.Admission.AdmissionRequestId).ToList();
        var studentIds = rows.Where(r => r.Admission.StudentId is not null).Select(r => r.Admission.StudentId!.Value).ToList();
        var missingByAdmission = await documents.MissingCountsAsync(DocumentTypes.AdmissionTable, pendingIds);
        var missingByStudent = await documents.MissingCountsAsync(DocumentTypes.StudentTable, studentIds);

        return rows
            .Select(r =>
            {
                var a = r.Admission;
                var missing = a.StudentId is { } studentId
                    ? missingByStudent.GetValueOrDefault(studentId)
                    : missingByAdmission.GetValueOrDefault(a.AdmissionRequestId);

                return new AdmissionDto(
                    a.AdmissionRequestId,
                    a.YearId,
                    a.GradeId,
                    r.GradeName,
                    a.FirstName,
                    a.LastName,
                    a.DocumentType,
                    a.DocumentNumber,
                    a.BirthDate,
                    a.GuardianName,
                    a.GuardianPhone,
                    a.Notes,
                    a.Status,
                    position.TryGetValue(a.AdmissionRequestId, out var waitlistPlace) ? waitlistPlace : null,
                    a.CreatedAt,
                    r.CreatedBy,
                    a.DecidedAt,
                    r.DecidedBy,
                    a.DecisionNote,
                    missing,
                    EnrollmentFormValidator.IsComplete(a));
            })
            .ToList();
    }

    public async Task<AdmissionDetailDto> GetAsync(int id, DateOnly today)
    {
        var admission = await db.AdmissionRequests.AsNoTracking().FirstOrDefaultAsync(a => a.AdmissionRequestId == id)
            ?? throw new InvalidOperationException("No existe la solicitud de admisión.");

        var summary = (await ListAsync(admission.YearId)).Single(a => a.AdmissionRequestId == id);
        var checklist = await documents.ChecklistAsync(await documents.OwnerOfAdmissionAsync(id), today);

        return new AdmissionDetailDto(
            summary,
            admission.GradeId,
            StudentOf(admission),
            GuardianOf(admission),
            C600Of(admission),
            EnrollmentFormValidator.IsComplete(admission),
            admission.StudentId,
            checklist);
    }

    public async Task<int> CreateAsync(SaveAdmissionRequest request, int userId)
    {
        var today = EnrollmentPeriodService.Today();
        var (student, guardian, c600) = await CleanAsync(request, today, null);

        var period = await periods.FindAsync(request.YearId);
        if (period is null)
            throw new InvalidOperationException("Configure primero las fechas de matrícula de ese año.");

        if (today < period.AdmissionStart || today > period.AdmissionEnd)
            throw new InvalidOperationException(
                $"Las solicitudes de admisión de {request.YearId} se reciben del {Format(period.AdmissionStart)} " +
                $"al {Format(period.AdmissionEnd)}.");

        await EnsureGradeAsync(request.GradeId);
        await EnsureNotDuplicatedAsync(request.YearId, student.DocumentNumber!, null);

        var admission = new AdmissionRequest
        {
            YearId = request.YearId,
            CreatedById = userId,
        };

        Apply(admission, request.GradeId, student, guardian, c600, request.Notes);

        db.AdmissionRequests.Add(admission);
        db.AdmissionRequestEvents.Add(new AdmissionRequestEvent
        {
            AdmissionRequest = admission,
            ToStatus = AdmissionStatuses.Pending,
            Note = "Solicitud registrada.",
            UserId = userId
        });

        await db.SaveChangesAsync();
        return admission.AdmissionRequestId;
    }

    public async Task UpdateAsync(int id, SaveAdmissionRequest request, int userId)
    {
        var admission = await db.AdmissionRequests.FirstOrDefaultAsync(a => a.AdmissionRequestId == id)
            ?? throw new InvalidOperationException("No existe la solicitud de admisión.");

        if (admission.Status is not (AdmissionStatuses.Pending or AdmissionStatuses.Waitlist))
            throw new InvalidOperationException(FinalDecision);

        var (student, guardian, c600) = await CleanAsync(request, EnrollmentPeriodService.Today(), admission);

        await EnsureGradeAsync(request.GradeId);
        await EnsureNotDuplicatedAsync(admission.YearId, student.DocumentNumber!, id);

        Apply(admission, request.GradeId, student, guardian, c600, request.Notes);

        db.AdmissionRequestEvents.Add(new AdmissionRequestEvent
        {
            AdmissionRequestId = admission.AdmissionRequestId,
            FromStatus = admission.Status,
            ToStatus = admission.Status,
            Note = UpdatedNote,
            UserId = userId
        });

        await db.SaveChangesAsync();
    }

    public async Task DecideAsync(int id, string? decision, string? note, int userId)
    {
        var newStatus = (decision ?? string.Empty).Trim().ToUpperInvariant();
        if (newStatus is not (AdmissionStatuses.Approved or AdmissionStatuses.Rejected or AdmissionStatuses.Waitlist))
            throw new InvalidOperationException("La decisión debe ser aprobar, rechazar o lista de espera.");

        var admission = await db.AdmissionRequests.FirstOrDefaultAsync(a => a.AdmissionRequestId == id);
        if (admission is null)
            throw new InvalidOperationException("No existe la solicitud de admisión.");

        if (admission.Status is AdmissionStatuses.Approved or AdmissionStatuses.Rejected)
            throw new InvalidOperationException(FinalDecision);

        if (admission.Status == newStatus)
            throw new InvalidOperationException("La solicitud ya está en ese estado.");

        var cleanNote = note?.Trim();
        if (newStatus == AdmissionStatuses.Rejected && string.IsNullOrEmpty(cleanNote))
            throw new InvalidOperationException("Indique el motivo del rechazo.");

        if (cleanNote is { Length: > 500 })
            cleanNote = cleanNote[..500];

        var approving = newStatus == AdmissionStatuses.Approved;

        if (approving)
        {
            if (!EnrollmentFormValidator.IsComplete(admission))
                throw new InvalidOperationException("Complete el formulario antes de aprobar.");

            await people.EnsureCanSaveAsync(StudentOf(admission), GuardianOf(admission));
        }

        var previousStatus = admission.Status;
        var now = DateTime.UtcNow;
        var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync() : null;

        try
        {
            await ApplyDecisionAsync(admission, previousStatus, newStatus, cleanNote, userId, now);

            if (transaction is not null)
                await transaction.CommitAsync();
        }
        catch
        {
            if (transaction is not null)
                await transaction.RollbackAsync();

            throw;
        }
        finally
        {
            if (transaction is not null)
                await transaction.DisposeAsync();
        }
    }

    private async Task ApplyDecisionAsync(
        AdmissionRequest admission,
        string previousStatus,
        string newStatus,
        string? cleanNote,
        int userId,
        DateTime now)
    {
        if (newStatus == AdmissionStatuses.Approved)
        {
            var student = await people.UpsertStudentAsync(StudentOf(admission), C600Of(admission));
            await people.UpsertGuardianAsync(GuardianOf(admission), admission.CampusId!.Value, student.StudentId);

            admission.StudentId = student.StudentId;
            await db.SaveChangesAsync();

            await documents.MoveToStudentAsync(admission.AdmissionRequestId, student.StudentId, userId, now);
        }

        admission.Status = newStatus;
        admission.DecidedAt = now;
        admission.DecidedById = userId;
        admission.DecisionNote = string.IsNullOrEmpty(cleanNote) ? null : cleanNote;

        if (newStatus == AdmissionStatuses.Waitlist)
            admission.WaitlistedAt = now;

        db.AdmissionRequestEvents.Add(new AdmissionRequestEvent
        {
            AdmissionRequestId = admission.AdmissionRequestId,
            FromStatus = previousStatus,
            ToStatus = newStatus,
            Note = string.IsNullOrEmpty(cleanNote) ? null : cleanNote,
            UserId = userId
        });

        if (newStatus == AdmissionStatuses.Approved)
        {
            var gradeName = await db.Grades
                .Where(g => g.GradeId == admission.GradeId)
                .Select(g => g.Name)
                .FirstAsync();

            await notifications.QueueAsync(
                NotificationCodes.SimatAdmissionApproved,
                new Dictionary<string, string>
                {
                    ["aspirante"] = $"{admission.FirstName} {admission.LastName}",
                    ["documento"] = $"{admission.DocumentType} {admission.DocumentNumber}",
                    ["grado"] = gradeName,
                    ["anio"] = admission.YearId.ToString(),
                },
                new NotificationTarget(
                    YearId: admission.YearId,
                    SourceTable: "AdmissionRequest",
                    SourceId: admission.AdmissionRequestId));

            var studentId = admission.StudentId!.Value;
            var missing = await documents.MissingNamesAsync(
                new DocumentOwner(DocumentTypes.StudentTable, studentId, admission.YearId, studentId));

            if (missing.Count > 0)
            {
                await notifications.QueueAsync(
                    NotificationCodes.EnrollDocsIncomplete,
                    new Dictionary<string, string>
                    {
                        ["estudiante"] = $"{admission.FirstName} {admission.LastName}",
                        ["documento"] = $"{admission.DocumentType} {admission.DocumentNumber}",
                        ["anio"] = admission.YearId.ToString(),
                        ["documentos"] = Cut(string.Join(", ", missing), ListLength),
                    },
                    new NotificationTarget(
                        StudentId: studentId,
                        YearId: admission.YearId,
                        CampusId: admission.CampusId,
                        SourceTable: "AdmissionRequest",
                        SourceId: admission.AdmissionRequestId));
            }
        }

        await db.SaveChangesAsync();
    }

    public async Task<List<AdmissionEventDto>> GetEventsAsync(int id)
    {
        return await db.AdmissionRequestEvents
            .AsNoTracking()
            .Where(e => e.AdmissionRequestId == id)
            .OrderBy(e => e.CreatedAt)
            .ThenBy(e => e.AdmissionRequestEventId)
            .Select(e => new AdmissionEventDto(
                e.CreatedAt,
                e.FromStatus,
                e.ToStatus,
                e.User.FirstName + " " + e.User.LastName,
                e.Note))
            .ToListAsync();
    }

    private async Task<(StudentFormData Student, GuardianFormData Guardian, C600FormData C600)> CleanAsync(
        SaveAdmissionRequest request,
        DateOnly today,
        AdmissionRequest? stored)
    {
        if (request.Student is null || request.Guardian is null || request.C600 is null)
            throw new InvalidOperationException(EnrollmentFormValidator.StudentIncomplete);

        var student = await validator.CleanStudentAsync(request.Student, today, stored is null ? null : StudentOf(stored));
        var guardian = await validator.CleanGuardianAsync(request.Guardian, stored is null ? null : GuardianOf(stored));
        var c600 = await validator.CleanC600Async(request.C600, stored is null ? null : C600Of(stored));

        EnrollmentFormValidator.EnsureDistinct(student, guardian);
        await validator.EnsureEmailFreeAsync(student.Email, student.DocumentNumber!);
        await validator.EnsureEmailFreeAsync(guardian.Email, guardian.DocumentNumber!);

        return (student, guardian, c600);
    }

    private async Task EnsureGradeAsync(int gradeId)
    {
        if (!await db.Grades.AnyAsync(g => g.GradeId == gradeId))
            throw new InvalidOperationException("El grado solicitado no existe.");
    }

    private async Task EnsureNotDuplicatedAsync(short yearId, string documentNumber, int? exceptId)
    {
        var duplicate = await db.AdmissionRequests.AnyAsync(a =>
            a.YearId == yearId &&
            a.DocumentNumber == documentNumber &&
            a.Status != AdmissionStatuses.Rejected &&
            (exceptId == null || a.AdmissionRequestId != exceptId));

        if (duplicate)
            throw new InvalidOperationException(
                $"Ya hay una solicitud de admisión vigente para el documento {documentNumber} en {yearId}.");
    }

    private static void Apply(
        AdmissionRequest admission,
        int gradeId,
        StudentFormData student,
        GuardianFormData guardian,
        C600FormData c600,
        string? notes)
    {
        var cleanNotes = notes?.Trim();
        if (cleanNotes is { Length: > NotesLength })
            cleanNotes = cleanNotes[..NotesLength];

        var guardianName = $"{guardian.FirstName} {guardian.LastName}";
        if (guardianName.Length > GuardianNameLength)
            guardianName = guardianName[..GuardianNameLength];

        admission.GradeId = gradeId;
        admission.DocumentType = student.DocumentType!;
        admission.DocumentNumber = student.DocumentNumber!;
        admission.FirstName = student.FirstName!;
        admission.LastName = student.LastName!;
        admission.BirthDate = student.BirthDate!.Value;
        admission.Gender = student.Gender![0];
        admission.BloodType = student.BloodType;
        admission.Eps = student.Eps;
        admission.Email = student.Email;
        admission.CampusId = student.CampusId;
        admission.GuardianDocumentType = guardian.DocumentType;
        admission.GuardianDocumentNumber = guardian.DocumentNumber;
        admission.GuardianFirstName = guardian.FirstName;
        admission.GuardianLastName = guardian.LastName;
        admission.GuardianName = guardianName;
        admission.GuardianPhone = guardian.Phone!;
        admission.GuardianAddress = guardian.Address;
        admission.GuardianEmail = guardian.Email;
        admission.GuardianRelationship = guardian.Relationship;
        admission.EthnicGroup = c600.EthnicGroup;
        admission.DisabilityType = c600.DisabilityType;
        admission.ExceptionalCapacity = c600.ExceptionalCapacity;
        admission.ConflictVictim = c600.ConflictVictim;
        admission.Methodology = c600.Methodology;
        admission.Notes = string.IsNullOrEmpty(cleanNotes) ? null : cleanNotes;
    }

    private static StudentFormData StudentOf(AdmissionRequest a) =>
        new(
            a.DocumentType,
            a.DocumentNumber,
            a.FirstName,
            a.LastName,
            a.BirthDate,
            a.Gender?.ToString(),
            a.BloodType,
            a.Eps,
            a.Email,
            a.CampusId);

    private static GuardianFormData GuardianOf(AdmissionRequest a) =>
        new(
            a.GuardianDocumentType,
            a.GuardianDocumentNumber,
            a.GuardianFirstName,
            a.GuardianLastName,
            a.GuardianPhone,
            a.GuardianAddress,
            a.GuardianEmail,
            a.GuardianRelationship);

    private static C600FormData C600Of(AdmissionRequest a) =>
        new(a.EthnicGroup, a.DisabilityType, a.ExceptionalCapacity, a.ConflictVictim, a.Methodology);

    private static string Cut(string text, int length) =>
        text.Length <= length ? text : text[..(length - 1)] + "…";

    private static string Format(DateOnly date) => date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
}

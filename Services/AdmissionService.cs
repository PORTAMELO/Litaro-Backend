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
    string? DecisionNote);

public record AdmissionEventDto(
    DateTime CreatedAt,
    string? FromStatus,
    string ToStatus,
    string UserName,
    string? Note);

public record CreateAdmissionRequest(
    short YearId,
    int GradeId,
    string FirstName,
    string LastName,
    string DocumentType,
    string DocumentNumber,
    DateOnly BirthDate,
    string GuardianName,
    string GuardianPhone,
    string? Notes);

public class AdmissionService(AppDbContext db, EnrollmentPeriodService periods, NotificationService notifications)
{
    public async Task<List<AdmissionDto>> ListAsync(short yearId)
    {
        var rows = await db.AdmissionRequests
            .AsNoTracking()
            .Where(a => a.YearId == yearId)
            .OrderBy(a => a.CreatedAt)
            .Select(a => new
            {
                a.AdmissionRequestId,
                a.YearId,
                a.GradeId,
                GradeName = a.Grade.Name,
                a.FirstName,
                a.LastName,
                a.DocumentType,
                a.DocumentNumber,
                a.BirthDate,
                a.GuardianName,
                a.GuardianPhone,
                a.Notes,
                a.Status,
                a.WaitlistedAt,
                a.CreatedAt,
                CreatedBy = a.CreatedBy.FirstName + " " + a.CreatedBy.LastName,
                a.DecidedAt,
                DecidedBy = a.DecidedBy != null ? a.DecidedBy.FirstName + " " + a.DecidedBy.LastName : null,
                a.DecisionNote
            })
            .ToListAsync();

        var position = new Dictionary<int, int>();
        foreach (var group in rows.Where(r => r.Status == AdmissionStatuses.Waitlist).GroupBy(r => r.GradeId))
        {
            var place = 0;
            foreach (var row in group.OrderBy(r => r.WaitlistedAt).ThenBy(r => r.AdmissionRequestId))
            {
                place++;
                position[row.AdmissionRequestId] = place;
            }
        }

        return rows
            .Select(r => new AdmissionDto(
                r.AdmissionRequestId,
                r.YearId,
                r.GradeId,
                r.GradeName,
                r.FirstName,
                r.LastName,
                r.DocumentType,
                r.DocumentNumber,
                r.BirthDate,
                r.GuardianName,
                r.GuardianPhone,
                r.Notes,
                r.Status,
                position.TryGetValue(r.AdmissionRequestId, out var waitlistPlace) ? waitlistPlace : (int?)null,
                r.CreatedAt,
                r.CreatedBy,
                r.DecidedAt,
                r.DecidedBy,
                r.DecisionNote))
            .ToList();
    }

    public async Task<int> CreateAsync(CreateAdmissionRequest request, int userId)
    {
        var firstName = (request.FirstName ?? string.Empty).Trim();
        var lastName = (request.LastName ?? string.Empty).Trim();
        var documentType = (request.DocumentType ?? string.Empty).Trim().ToUpperInvariant();
        var documentNumber = (request.DocumentNumber ?? string.Empty).Trim();
        var guardianName = (request.GuardianName ?? string.Empty).Trim();
        var guardianPhone = (request.GuardianPhone ?? string.Empty).Trim();
        var notes = request.Notes?.Trim();

        if (firstName.Length == 0 || lastName.Length == 0)
            throw new InvalidOperationException("Los nombres y apellidos del aspirante son obligatorios.");

        if (documentType.Length is 0 or > 5)
            throw new InvalidOperationException("Seleccione el tipo de documento del aspirante.");

        if (documentNumber.Length is 0 or > 20)
            throw new InvalidOperationException("El número de documento es obligatorio y no puede superar 20 caracteres.");

        if (guardianName.Length == 0 || guardianPhone.Length == 0)
            throw new InvalidOperationException("El nombre y el teléfono del acudiente son obligatorios.");

        if (firstName.Length > 100 || lastName.Length > 100 || guardianName.Length > 150 || guardianPhone.Length > 20)
            throw new InvalidOperationException("Alguno de los textos supera la longitud permitida.");

        if (notes is { Length: > 500 })
            notes = notes[..500];

        var today = EnrollmentPeriodService.Today();
        if (request.BirthDate == default || request.BirthDate > today)
            throw new InvalidOperationException("La fecha de nacimiento no es válida.");

        var period = await periods.FindAsync(request.YearId);
        if (period is null)
            throw new InvalidOperationException("Configure primero las fechas de matrícula de ese año.");

        if (today < period.AdmissionStart || today > period.AdmissionEnd)
            throw new InvalidOperationException(
                $"Las solicitudes de admisión de {request.YearId} se reciben del {Format(period.AdmissionStart)} " +
                $"al {Format(period.AdmissionEnd)}.");

        var gradeExists = await db.Grades.AnyAsync(g => g.GradeId == request.GradeId);
        if (!gradeExists)
            throw new InvalidOperationException("El grado solicitado no existe.");

        var duplicate = await db.AdmissionRequests.AnyAsync(a =>
            a.YearId == request.YearId &&
            a.DocumentNumber == documentNumber &&
            a.Status != AdmissionStatuses.Rejected);
        if (duplicate)
            throw new InvalidOperationException(
                $"Ya hay una solicitud de admisión vigente para el documento {documentNumber} en {request.YearId}.");

        var admission = new AdmissionRequest
        {
            YearId = request.YearId,
            GradeId = request.GradeId,
            FirstName = firstName,
            LastName = lastName,
            DocumentType = documentType,
            DocumentNumber = documentNumber,
            BirthDate = request.BirthDate,
            GuardianName = guardianName,
            GuardianPhone = guardianPhone,
            Notes = string.IsNullOrEmpty(notes) ? null : notes,
            CreatedById = userId
        };

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

    public async Task DecideAsync(int id, string? decision, string? note, int userId)
    {
        var newStatus = (decision ?? string.Empty).Trim().ToUpperInvariant();
        if (newStatus is not (AdmissionStatuses.Approved or AdmissionStatuses.Rejected or AdmissionStatuses.Waitlist))
            throw new InvalidOperationException("La decisión debe ser aprobar, rechazar o lista de espera.");

        var admission = await db.AdmissionRequests.FirstOrDefaultAsync(a => a.AdmissionRequestId == id);
        if (admission is null)
            throw new InvalidOperationException("No existe la solicitud de admisión.");

        if (admission.Status is AdmissionStatuses.Approved or AdmissionStatuses.Rejected)
            throw new InvalidOperationException("La solicitud ya tiene una decisión final y no puede cambiarse.");

        if (admission.Status == newStatus)
            throw new InvalidOperationException("La solicitud ya está en ese estado.");

        var cleanNote = note?.Trim();
        if (newStatus == AdmissionStatuses.Rejected && string.IsNullOrEmpty(cleanNote))
            throw new InvalidOperationException("Indique el motivo del rechazo.");

        if (cleanNote is { Length: > 500 })
            cleanNote = cleanNote[..500];

        var previousStatus = admission.Status;
        var now = DateTime.UtcNow;

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

    private static string Format(DateOnly date) => date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
}
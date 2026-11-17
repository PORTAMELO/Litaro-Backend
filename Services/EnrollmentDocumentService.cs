using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services;

public static class EnrollmentDocumentStates
{
    public const string Required = "OBLIGATORIO";
    public const string Optional = "OPCIONAL";
    public const string Hidden = "OCULTO";

    public static readonly IReadOnlyList<string> All = [Required, Optional, Hidden];
}

public record DocumentOwner(string EntityTable, int EntityId, short YearId, int? StudentId);

public record ChecklistItemDto(int DetailId, string Name, bool Required, bool Overdue, DocumentDto? Document);

public record ChecklistDto(DateOnly? DueDate, int RequiredTotal, int RequiredLoaded, List<ChecklistItemDto> Items);

public record DocumentTypeSettingDto(int? DetailId, string Name, string State, bool HasFiles);

public class EnrollmentDocumentService(AppDbContext db, DocumentService documents)
{
    public const string CompletedNote = "Documentación completa.";

    private const int NameLength = 150;
    private const string InvalidType = "Tipo de documento no válido.";
    private const string NoFile = "No hay un archivo cargado para este documento.";

    private sealed record DocumentKind(int DetailId, string Name, string State)
    {
        public bool Visible => State != EnrollmentDocumentStates.Hidden;
        public bool Required => State == EnrollmentDocumentStates.Required;
        public string Code => $"{DocumentTypes.EnrollmentPrefix}{DetailId}";
    }

    public async Task<DocumentOwner> OwnerOfAdmissionAsync(int admissionId)
    {
        var admission = await db.AdmissionRequests
            .AsNoTracking()
            .Where(a => a.AdmissionRequestId == admissionId)
            .Select(a => new { a.YearId, a.StudentId })
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("No existe la solicitud de admisión.");

        return admission.StudentId is { } studentId
            ? new DocumentOwner(DocumentTypes.StudentTable, studentId, admission.YearId, studentId)
            : new DocumentOwner(DocumentTypes.AdmissionTable, admissionId, admission.YearId, null);
    }

    public async Task<DocumentOwner> OwnerOfStudentAsync(int studentId, short yearId)
    {
        if (!await db.Students.AnyAsync(s => s.StudentId == studentId))
            throw new InvalidOperationException("No existe el estudiante.");

        return new DocumentOwner(DocumentTypes.StudentTable, studentId, yearId, studentId);
    }

    public async Task<ChecklistDto> ChecklistAsync(DocumentOwner owner, DateOnly today)
    {
        var kinds = (await KindsAsync()).Where(k => k.Visible).ToList();
        var current = await CurrentAsync(owner.EntityTable, [owner.EntityId]);

        var dueDate = await db.EnrollmentPeriods
            .AsNoTracking()
            .Where(p => p.YearId == owner.YearId)
            .Select(p => (DateOnly?)p.AdmissionEnd)
            .FirstOrDefaultAsync();

        var items = kinds
            .Select(k =>
            {
                var document = current.GetValueOrDefault((owner.EntityId, k.Code));
                var overdue = k.Required && document is null && dueDate is { } due && today > due;
                return new ChecklistItemDto(k.DetailId, k.Name, k.Required, overdue, document);
            })
            .ToList();

        return new ChecklistDto(
            dueDate,
            items.Count(i => i.Required),
            items.Count(i => i.Required && i.Document is not null),
            items);
    }

    public async Task<DocumentDto> UploadAsync(DocumentOwner owner, int detailId, DocumentUpload file, int userId, DateTime nowUtc)
    {
        await VisibleKindAsync(detailId);

        var document = await documents.UploadAsync(
            DocumentTypes.Enrollment(detailId, owner.EntityTable), owner.EntityId, owner.YearId, file, userId, nowUtc);

        if (owner.StudentId is { } studentId && (await MissingNamesAsync(owner)).Count == 0)
            await CloseAlertsAsync(studentId, userId, nowUtc);

        return document;
    }

    public async Task RemoveAsync(DocumentOwner owner, int detailId, int userId, DateTime nowUtc)
    {
        await VisibleKindAsync(detailId);

        if (!await documents.RemoveCurrentAsync(DocumentTypes.Enrollment(detailId, owner.EntityTable), owner.EntityId, userId, nowUtc))
            throw new InvalidOperationException(NoFile);
    }

    public async Task<(Document Document, Stream Content)> OpenAsync(DocumentOwner owner, int detailId)
    {
        await VisibleKindAsync(detailId);

        return await documents.OpenCurrentAsync(DocumentTypes.Enrollment(detailId, owner.EntityTable), owner.EntityId)
            ?? throw new InvalidOperationException(NoFile);
    }

    public async Task<Dictionary<int, int>> MissingCountsAsync(string entityTable, IReadOnlyCollection<int> entityIds)
    {
        var required = (await KindsAsync()).Where(k => k.Required).ToList();

        if (required.Count == 0 || entityIds.Count == 0)
            return [];

        var current = await CurrentAsync(entityTable, entityIds);
        var result = new Dictionary<int, int>();

        foreach (var id in entityIds.Distinct())
        {
            var missing = required.Count(k => !current.ContainsKey((id, k.Code)));
            if (missing > 0)
                result[id] = missing;
        }

        return result;
    }

    public async Task<List<string>> MissingNamesAsync(DocumentOwner owner)
    {
        var required = (await KindsAsync()).Where(k => k.Required).ToList();
        var current = await CurrentAsync(owner.EntityTable, [owner.EntityId]);

        return required
            .Where(k => !current.ContainsKey((owner.EntityId, k.Code)))
            .Select(k => k.Name)
            .ToList();
    }

    public Task MoveToStudentAsync(int admissionId, int studentId, int userId, DateTime nowUtc) =>
        documents.ReassignAsync(
            DocumentTypes.EnrollmentPrefix,
            DocumentTypes.AdmissionTable,
            admissionId,
            DocumentTypes.StudentTable,
            studentId,
            userId,
            nowUtc);

    public async Task<List<DocumentTypeSettingDto>> GetSettingsAsync()
    {
        var kinds = await KindsAsync();

        var withFiles = (await db.Documents
                .AsNoTracking()
                .Where(d => d.DocumentType.StartsWith(DocumentTypes.EnrollmentPrefix))
                .Select(d => d.DocumentType)
                .Distinct()
                .ToListAsync())
            .ToHashSet();

        return kinds
            .Select(k => new DocumentTypeSettingDto(k.DetailId, k.Name, k.State, withFiles.Contains(k.Code)))
            .ToList();
    }

    public async Task<List<DocumentTypeSettingDto>> SaveSettingsAsync(List<DocumentTypeSettingDto> items)
    {
        var cleaned = (items ?? [])
            .Select(i => i with
            {
                Name = (i.Name ?? string.Empty).Trim(),
                State = (i.State ?? string.Empty).Trim().ToUpperInvariant(),
            })
            .ToList();

        var current = await GetSettingsAsync();
        var currentIds = current.Select(c => c.DetailId!.Value).ToHashSet();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in cleaned)
        {
            if (item.Name.Length == 0)
                throw new InvalidOperationException("El nombre del documento es obligatorio.");

            if (item.Name.Length > NameLength)
                throw new InvalidOperationException("Alguno de los textos supera la longitud permitida.");

            if (!EnrollmentDocumentStates.All.Contains(item.State))
                throw new InvalidOperationException("Estado no válido.");

            if (item.DetailId is { } id && !currentIds.Contains(id))
                throw new InvalidOperationException(InvalidType);

            if (!names.Add(item.Name))
                throw new InvalidOperationException($"Ya existe un documento llamado «{item.Name}».");
        }

        var keptIds = cleaned.Where(i => i.DetailId is not null).Select(i => i.DetailId!.Value).ToHashSet();
        var removed = current.Where(c => !keptIds.Contains(c.DetailId!.Value)).ToList();

        var blocked = removed.FirstOrDefault(r => r.HasFiles);
        if (blocked is not null)
            throw new InvalidOperationException($"«{blocked.Name}» tiene archivos cargados; ocúltelo en lugar de quitarlo.");

        var characteristic = await db.Characteristics.FirstOrDefaultAsync(c => c.Name == Catalogs.EnrollmentDocuments);
        if (characteristic is null)
        {
            characteristic = new Characteristic { Name = Catalogs.EnrollmentDocuments };
            db.Characteristics.Add(characteristic);
            await db.SaveChangesAsync();
        }

        var details = await db.CharacteristicDetails
            .Where(d => d.CharacteristicId == characteristic.CharacteristicId)
            .ToListAsync();

        foreach (var item in cleaned)
        {
            var detail = item.DetailId is { } id ? details.Single(d => d.CharacteristicDetailId == id) : null;

            if (detail is null)
            {
                db.CharacteristicDetails.Add(new CharacteristicDetail
                {
                    CharacteristicId = characteristic.CharacteristicId,
                    Nombre = item.Name,
                    Valor = item.State,
                });
                continue;
            }

            detail.Nombre = item.Name;
            detail.Valor = item.State;
        }

        var removedIds = removed.Select(r => r.DetailId!.Value).ToHashSet();
        db.CharacteristicDetails.RemoveRange(details.Where(d => removedIds.Contains(d.CharacteristicDetailId)));

        await db.SaveChangesAsync();
        return await GetSettingsAsync();
    }

    private async Task<List<DocumentKind>> KindsAsync() =>
        await db.CharacteristicDetails
            .AsNoTracking()
            .Where(d => d.Characteristic!.Name == Catalogs.EnrollmentDocuments)
            .OrderBy(d => d.CharacteristicDetailId)
            .Select(d => new DocumentKind(d.CharacteristicDetailId, d.Nombre, d.Valor))
            .ToListAsync();

    private async Task<DocumentKind> VisibleKindAsync(int detailId)
    {
        var kind = (await KindsAsync()).FirstOrDefault(k => k.DetailId == detailId);

        if (kind is null || !kind.Visible)
            throw new InvalidOperationException(InvalidType);

        return kind;
    }

    private async Task<Dictionary<(int EntityId, string DocumentType), DocumentDto>> CurrentAsync(string entityTable, IReadOnlyCollection<int> entityIds)
    {
        var rows = await documents.CurrentByPrefixAsync(DocumentTypes.EnrollmentPrefix, entityTable, entityIds);
        return rows.ToDictionary(r => (r.EntityId, r.DocumentType), r => r.Document);
    }

    private async Task CloseAlertsAsync(int studentId, int userId, DateTime nowUtc)
    {
        var pending = await db.Notifications
            .Where(n =>
                n.NotificationType.Code == NotificationCodes.EnrollDocsIncomplete &&
                n.StudentId == studentId &&
                n.Status == NotificationStatuses.Pending)
            .ToListAsync();

        if (pending.Count == 0)
            return;

        foreach (var notification in pending)
        {
            notification.Status = NotificationStatuses.Done;
            notification.DoneAt = nowUtc;
            notification.DoneById = userId;
            notification.DoneNote = CompletedNote;
        }

        await db.SaveChangesAsync();
    }
}

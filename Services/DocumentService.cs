using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services;

public record DocumentUpload(string FileName, string ContentType, long Length, Func<Stream> Open);

public record DocumentDto(int DocumentId, string FileName, long SizeBytes, DateTime UploadedAt, string UploadedBy);

public class DocumentService(AppDbContext db, IDocumentStorage storage, AuditContext? audit = null)
{
    private const int FileNameLength = 200;
    private const string NotConfigured = "El almacenamiento de documentos no está configurado.";

    public Task<DocumentDto> UploadAsync(string typeCode, int entityId, short? yearId, DocumentUpload file, int userId, DateTime nowUtc) =>
        UploadAsync(DocumentTypes.Get(typeCode), entityId, yearId, file, userId, nowUtc);

    public async Task<DocumentDto> UploadAsync(DocumentTypeRule rule, int entityId, short? yearId, DocumentUpload file, int userId, DateTime nowUtc)
    {
        if (!storage.IsConfigured)
            throw new InvalidOperationException(NotConfigured);

        var fileName = CleanFileName(file.FileName);
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        var format = rule.Formats.FirstOrDefault(f =>
            f.Extensions.Contains(extension) &&
            string.Equals(f.ContentType, file.ContentType, StringComparison.OrdinalIgnoreCase));

        if (format is null)
            throw new InvalidOperationException(rule.FormatMessage);

        if (file.Length <= 0)
            throw new InvalidOperationException("El archivo está vacío.");

        if (file.Length > rule.MaxBytes)
            throw new InvalidOperationException(rule.SizeMessage);

        if (!await HasSignatureAsync(file, format.Signature))
            throw new InvalidOperationException(rule.FormatMessage);

        var folder = rule.Code.Replace(':', '-');
        var path = $"{rule.Module}/{folder}/{rule.EntityTable}/{entityId}/{Guid.NewGuid():N}{extension}";

        await using (var content = file.Open())
            await storage.UploadAsync(path, content, format.ContentType);

        var document = new Document
        {
            Module = rule.Module,
            DocumentType = rule.Code,
            EntityTable = rule.EntityTable,
            EntityId = entityId,
            YearId = yearId,
            StoragePath = path,
            FileName = fileName,
            ContentType = format.ContentType,
            SizeBytes = file.Length,
            IsCurrent = true,
            UploadedById = userId,
            UploadedAt = nowUtc,
        };

        var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync() : null;

        try
        {
            var previous = await db.Documents
                .Where(d => d.DocumentType == rule.Code && d.EntityTable == rule.EntityTable && d.EntityId == entityId && d.IsCurrent)
                .ToListAsync();

            using (audit?.BeginAction(AuditActions.DocReplace))
            {
                await RetireAsync(previous, userId, nowUtc);

                if (previous.Count > 0)
                    await db.SaveChangesAsync();
            }

            db.Documents.Add(document);
            await db.SaveChangesAsync();

            if (transaction is not null)
                await transaction.CommitAsync();
        }
        catch
        {
            if (transaction is not null)
                await transaction.RollbackAsync();

            db.ChangeTracker.Clear();
            await TryDeleteAsync(path);
            throw;
        }
        finally
        {
            if (transaction is not null)
                await transaction.DisposeAsync();
        }

        return await db.Documents
            .AsNoTracking()
            .Where(d => d.DocumentId == document.DocumentId)
            .Select(d => new DocumentDto(
                d.DocumentId,
                d.FileName,
                d.SizeBytes,
                d.UploadedAt,
                d.UploadedBy.FirstName + " " + d.UploadedBy.LastName))
            .SingleAsync();
    }

    public Task<bool> RemoveCurrentAsync(string typeCode, int entityId, int userId, DateTime nowUtc) =>
        RemoveCurrentAsync(DocumentTypes.Get(typeCode), entityId, userId, nowUtc);

    public async Task<bool> RemoveCurrentAsync(DocumentTypeRule rule, int entityId, int userId, DateTime nowUtc)
    {
        var current = await db.Documents
            .Where(d => d.DocumentType == rule.Code && d.EntityTable == rule.EntityTable && d.EntityId == entityId && d.IsCurrent)
            .ToListAsync();

        if (current.Count == 0)
            return false;

        try
        {
            await RetireAsync(current, userId, nowUtc);
            await db.SaveChangesAsync();
        }
        catch
        {
            db.ChangeTracker.Clear();
            throw;
        }

        return true;
    }

    public async Task<Dictionary<int, DocumentDto>> CurrentByEntityAsync(string typeCode, IEnumerable<int> entityIds)
    {
        var rule = DocumentTypes.Get(typeCode);
        var ids = entityIds.Distinct().ToList();

        var rows = await db.Documents
            .AsNoTracking()
            .Where(d => d.DocumentType == rule.Code && d.EntityTable == rule.EntityTable && d.IsCurrent && ids.Contains(d.EntityId))
            .Select(d => new
            {
                d.EntityId,
                Dto = new DocumentDto(
                    d.DocumentId,
                    d.FileName,
                    d.SizeBytes,
                    d.UploadedAt,
                    d.UploadedBy.FirstName + " " + d.UploadedBy.LastName),
            })
            .ToListAsync();

        return rows.ToDictionary(r => r.EntityId, r => r.Dto);
    }

    public async Task<List<(int EntityId, string DocumentType, DocumentDto Document)>> CurrentByPrefixAsync(
        string typePrefix,
        string entityTable,
        IReadOnlyCollection<int> entityIds)
    {
        if (entityIds.Count == 0)
            return [];

        var ids = entityIds.Distinct().ToList();

        var rows = await db.Documents
            .AsNoTracking()
            .Where(d => d.DocumentType.StartsWith(typePrefix) && d.EntityTable == entityTable && d.IsCurrent && ids.Contains(d.EntityId))
            .Select(d => new
            {
                d.EntityId,
                d.DocumentType,
                Dto = new DocumentDto(
                    d.DocumentId,
                    d.FileName,
                    d.SizeBytes,
                    d.UploadedAt,
                    d.UploadedBy.FirstName + " " + d.UploadedBy.LastName),
            })
            .ToListAsync();

        return rows.Select(r => (r.EntityId, r.DocumentType, r.Dto)).ToList();
    }

    public Task<(Document Document, Stream Content)?> OpenCurrentAsync(string typeCode, int entityId) =>
        OpenCurrentAsync(DocumentTypes.Get(typeCode), entityId);

    public async Task<(Document Document, Stream Content)?> OpenCurrentAsync(DocumentTypeRule rule, int entityId)
    {
        var document = await db.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.DocumentType == rule.Code && d.EntityTable == rule.EntityTable && d.EntityId == entityId && d.IsCurrent);

        if (document is null)
            return null;

        if (!storage.IsConfigured)
            throw new InvalidOperationException(NotConfigured);

        return (document, await storage.OpenReadAsync(document.StoragePath));
    }

    public async Task<int> ReassignAsync(string typePrefix, string fromTable, int fromId, string toTable, int toId, int userId, DateTime nowUtc)
    {
        var moving = await db.Documents
            .Where(d => d.DocumentType.StartsWith(typePrefix) && d.EntityTable == fromTable && d.EntityId == fromId && d.IsCurrent)
            .ToListAsync();

        if (moving.Count == 0)
            return 0;

        var types = moving.Select(d => d.DocumentType).ToList();

        var replaced = await db.Documents
            .Where(d => d.EntityTable == toTable && d.EntityId == toId && d.IsCurrent && types.Contains(d.DocumentType))
            .ToListAsync();

        try
        {
            using (audit?.BeginAction(AuditActions.DocReplace))
            {
                await RetireAsync(replaced, userId, nowUtc);

                if (replaced.Count > 0)
                    await db.SaveChangesAsync();
            }

            foreach (var document in moving)
            {
                document.EntityTable = toTable;
                document.EntityId = toId;
            }

            await db.SaveChangesAsync();
        }
        catch
        {
            db.ChangeTracker.Clear();
            throw;
        }

        return moving.Count;
    }

    public Task<bool> HasAnyAsync(string documentType) =>
        db.Documents.AnyAsync(d => d.DocumentType == documentType);

    public async Task<int> PurgeRemovedAsync(DateTime nowUtc, int batchSize = 100)
    {
        if (!storage.IsConfigured)
            return 0;

        var pending = await db.Documents
            .Where(d => !d.IsCurrent && d.FileRemovedAt == null)
            .OrderBy(d => d.DocumentId)
            .Take(batchSize)
            .ToListAsync();

        var removed = 0;

        foreach (var document in pending)
        {
            try
            {
                await storage.DeleteAsync(document.StoragePath);
            }
            catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException)
            {
                continue;
            }

            document.FileRemovedAt = nowUtc;
            removed++;
        }

        if (removed > 0)
            await db.SaveChangesAsync();

        return removed;
    }

    private async Task RetireAsync(List<Document> documents, int userId, DateTime nowUtc)
    {
        foreach (var document in documents)
        {
            document.IsCurrent = false;
            document.ReplacedById = userId;
            document.ReplacedAt = nowUtc;
            document.FileRemovedAt = nowUtc;
        }

        foreach (var document in documents)
            await storage.DeleteAsync(document.StoragePath);
    }

    private async Task TryDeleteAsync(string path)
    {
        try
        {
            await storage.DeleteAsync(path);
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException)
        {
        }
    }

    private static string CleanFileName(string fileName)
    {
        var name = Path.GetFileName(fileName.Replace('\\', '/').Split('/').Last()).Trim();

        if (name.Length == 0)
            name = "documento";

        return name.Length <= FileNameLength ? name : name[^FileNameLength..];
    }

    private static async Task<bool> HasSignatureAsync(DocumentUpload file, byte[] signature)
    {
        await using var stream = file.Open();
        var buffer = new byte[signature.Length];
        var read = 0;

        while (read < buffer.Length)
        {
            var count = await stream.ReadAsync(buffer.AsMemory(read));
            if (count == 0)
                break;
            read += count;
        }

        return read == signature.Length && buffer.SequenceEqual(signature);
    }
}

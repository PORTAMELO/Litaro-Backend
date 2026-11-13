using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services;

public record DocumentUpload(string FileName, string ContentType, long Length, Func<Stream> Open);

public record DocumentDto(int DocumentId, string FileName, long SizeBytes, DateTime UploadedAt, string UploadedBy);

public class DocumentService(AppDbContext db, IDocumentStorage storage)
{
    private const int FileNameLength = 200;

    public async Task<DocumentDto> UploadAsync(string typeCode, int entityId, short? yearId, DocumentUpload file, int userId, DateTime nowUtc)
    {
        var rule = DocumentTypes.Get(typeCode);

        if (!storage.IsConfigured)
            throw new InvalidOperationException("El almacenamiento de documentos no está configurado.");

        var fileName = CleanFileName(file.FileName);
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        if (!rule.Extensions.Contains(extension) ||
            !rule.ContentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException(rule.FormatMessage);

        if (file.Length <= 0)
            throw new InvalidOperationException("El archivo está vacío.");

        if (file.Length > rule.MaxBytes)
            throw new InvalidOperationException(rule.SizeMessage);

        if (!await HasSignatureAsync(file, rule.Signature))
            throw new InvalidOperationException(rule.FormatMessage);

        var path = $"{rule.Module}/{rule.Code}/{rule.EntityTable}/{entityId}/{Guid.NewGuid():N}{extension}";

        await using (var content = file.Open())
            await storage.UploadAsync(path, content, rule.ContentTypes[0]);

        var document = new Document
        {
            Module = rule.Module,
            DocumentType = rule.Code,
            EntityTable = rule.EntityTable,
            EntityId = entityId,
            YearId = yearId,
            StoragePath = path,
            FileName = fileName,
            ContentType = rule.ContentTypes[0],
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

            foreach (var old in previous)
            {
                old.IsCurrent = false;
                old.ReplacedById = userId;
                old.ReplacedAt = nowUtc;
            }

            if (previous.Count > 0)
                await db.SaveChangesAsync();

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
            await storage.DeleteAsync(path);
            throw;
        }
        finally
        {
            if (transaction is not null)
                await transaction.DisposeAsync();
        }

        return (await CurrentByEntityAsync(rule.Code, [entityId]))[entityId];
    }

    public async Task<bool> RemoveCurrentAsync(string typeCode, int entityId, int userId, DateTime nowUtc)
    {
        var rule = DocumentTypes.Get(typeCode);

        var current = await db.Documents
            .Where(d => d.DocumentType == rule.Code && d.EntityTable == rule.EntityTable && d.EntityId == entityId && d.IsCurrent)
            .ToListAsync();

        if (current.Count == 0)
            return false;

        foreach (var document in current)
        {
            document.IsCurrent = false;
            document.ReplacedById = userId;
            document.ReplacedAt = nowUtc;
        }

        await db.SaveChangesAsync();
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

    public async Task<(Document Document, Stream Content)?> OpenCurrentAsync(string typeCode, int entityId)
    {
        var rule = DocumentTypes.Get(typeCode);

        var document = await db.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.DocumentType == rule.Code && d.EntityTable == rule.EntityTable && d.EntityId == entityId && d.IsCurrent);

        if (document is null)
            return null;

        if (!storage.IsConfigured)
            throw new InvalidOperationException("El almacenamiento de documentos no está configurado.");

        return (document, await storage.OpenReadAsync(document.StoragePath));
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

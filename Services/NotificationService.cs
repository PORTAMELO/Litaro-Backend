using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services;

public record NotificationTarget(
    int? StudentId = null,
    short? YearId = null,
    int? CampusId = null,
    string? SourceTable = null,
    int? SourceId = null,
    string? DedupKey = null);

public record NotificationDto(
    int NotificationId,
    int TypeId,
    string TypeName,
    string Category,
    string Message,
    string State,
    DateTime CreatedAt,
    DateTime? DoneAt,
    string? DoneBy,
    string? DoneNote);

public record NotificationSummaryDto(int Unread, int Pending);

public record NotificationCountsDto(int All, int Unread, int Pending, int Done);

public record NotificationTypeOptionDto(int TypeId, string Name);

public record NotificationPageDto(
    List<NotificationDto> Rows,
    int Total,
    int Page,
    int PageSize,
    NotificationCountsDto Counts,
    List<NotificationTypeOptionDto> Types);

public class NotificationService(AppDbContext db)
{
    public const int MaxPageSize = 100;

    private const int ListLimit = 500;
    private const int NoteLength = 300;

    private readonly Dictionary<string, NotificationType?> types = [];
    private readonly HashSet<string> checkedKeys = [];
    private readonly HashSet<string> existingKeys = [];

    public async Task PreloadDedupKeysAsync(IEnumerable<string> keys)
    {
        var pending = keys.Where(key => !checkedKeys.Contains(key)).Distinct().ToList();
        if (pending.Count == 0)
            return;

        var found = await db.Notifications
            .Where(n => n.DedupKey != null && pending.Contains(n.DedupKey))
            .Select(n => n.DedupKey!)
            .ToListAsync();

        checkedKeys.UnionWith(pending);
        existingKeys.UnionWith(found);
    }

    public async Task<Notification?> QueueAsync(
        string typeCode,
        IReadOnlyDictionary<string, string> values,
        NotificationTarget? target = null)
    {
        if (!types.TryGetValue(typeCode, out var type))
        {
            type = await db.NotificationTypes.AsNoTracking().FirstOrDefaultAsync(t => t.Code == typeCode && t.Active);
            types[typeCode] = type;
        }

        if (type is null)
            return null;

        var dedupKey = target?.DedupKey;
        if (dedupKey is not null)
        {
            if (db.Notifications.Local.Any(n => n.DedupKey == dedupKey))
                return null;

            if (checkedKeys.Contains(dedupKey)
                    ? existingKeys.Contains(dedupKey)
                    : await db.Notifications.AnyAsync(n => n.DedupKey == dedupKey))
                return null;
        }

        var notification = new Notification
        {
            NotificationTypeId = type.NotificationTypeId,
            Message = NotificationTemplate.Render(type.Template, values),
            StudentId = target?.StudentId,
            YearId = target?.YearId,
            CampusId = target?.CampusId,
            SourceTable = target?.SourceTable,
            SourceId = target?.SourceId,
            DedupKey = dedupKey,
        };

        db.Notifications.Add(notification);
        return notification;
    }

    public Task<bool> HasPendingAsync(string typeCode, int studentId, short yearId) =>
        db.Notifications.AnyAsync(n =>
            n.NotificationType.Code == typeCode &&
            n.StudentId == studentId &&
            n.YearId == yearId &&
            n.Status == NotificationStatuses.Pending);

    public async Task<List<NotificationDto>> ListAsync(int userId, string? state, int? typeId)
    {
        var query = ByState(ByType(await VisibleToAsync(userId), typeId), state, userId);
        return await ToDtosAsync(query, userId, 0, ListLimit);
    }

    public async Task<NotificationPageDto> PageAsync(int userId, string? state, int? typeId, int page, int pageSize)
    {
        var size = Math.Clamp(pageSize, 1, MaxPageSize);
        var visible = await VisibleToAsync(userId);
        var ofType = ByType(visible, typeId);
        var query = ByState(ofType, state, userId);

        var counts = await CountsAsync(ofType, userId);
        var total = TotalFor(counts, state);
        var lastPage = Math.Max(1, (int)Math.Ceiling(total / (double)size));
        var current = Math.Clamp(page, 1, lastPage);

        var typeOptions = await visible
            .Select(n => new { n.NotificationTypeId, n.NotificationType.Name })
            .Distinct()
            .OrderBy(t => t.Name)
            .Select(t => new NotificationTypeOptionDto(t.NotificationTypeId, t.Name))
            .ToListAsync();

        var rows = await ToDtosAsync(query, userId, (current - 1) * size, size);

        return new NotificationPageDto(rows, total, current, size, counts, typeOptions);
    }

    public async Task<int> MarkReadMatchingAsync(int userId, string? state, int? typeId)
    {
        var ids = await MatchingOpenIdsAsync(userId, state, typeId);
        return ids.Count == 0 ? 0 : await MarkReadManyAsync(ids, userId);
    }

    public async Task<int> MarkDoneMatchingAsync(int userId, string? state, int? typeId, string? note)
    {
        var ids = await MatchingOpenIdsAsync(userId, state, typeId);
        return ids.Count == 0 ? 0 : await MarkDoneAsync(ids, note, userId);
    }

    private async Task<List<int>> MatchingOpenIdsAsync(int userId, string? state, int? typeId) =>
        await ByState(ByType(await VisibleToAsync(userId), typeId), state, userId)
            .Where(n => n.Status == NotificationStatuses.Pending)
            .Select(n => n.NotificationId)
            .ToListAsync();

    private async Task<NotificationCountsDto> CountsAsync(IQueryable<Notification> query, int userId)
    {
        var groups = await query
            .Select(n => new
            {
                n.Status,
                Read = db.NotificationReads.Any(r => r.NotificationId == n.NotificationId && r.UserId == userId),
            })
            .GroupBy(n => new { n.Status, n.Read })
            .Select(g => new { g.Key.Status, g.Key.Read, Count = g.Count() })
            .ToListAsync();

        return new NotificationCountsDto(
            groups.Sum(g => g.Count),
            groups.Where(g => g.Status == NotificationStatuses.Pending && !g.Read).Sum(g => g.Count),
            groups.Where(g => g.Status == NotificationStatuses.Pending && g.Read).Sum(g => g.Count),
            groups.Where(g => g.Status == NotificationStatuses.Done).Sum(g => g.Count));
    }

    private static int TotalFor(NotificationCountsDto counts, string? state) =>
        state?.Trim().ToUpperInvariant() switch
        {
            NotificationStates.Done => counts.Done,
            NotificationStates.Unread => counts.Unread,
            NotificationStates.Pending => counts.Pending,
            _ => counts.All,
        };

    private static IQueryable<Notification> ByType(IQueryable<Notification> query, int? typeId) =>
        typeId is null ? query : query.Where(n => n.NotificationTypeId == typeId);

    private IQueryable<Notification> ByState(IQueryable<Notification> query, string? state, int userId) =>
        state?.Trim().ToUpperInvariant() switch
        {
            NotificationStates.Done => query.Where(n => n.Status == NotificationStatuses.Done),
            NotificationStates.Unread => query.Where(n =>
                n.Status == NotificationStatuses.Pending &&
                !db.NotificationReads.Any(r => r.NotificationId == n.NotificationId && r.UserId == userId)),
            NotificationStates.Pending => query.Where(n =>
                n.Status == NotificationStatuses.Pending &&
                db.NotificationReads.Any(r => r.NotificationId == n.NotificationId && r.UserId == userId)),
            _ => query,
        };

    private async Task<List<NotificationDto>> ToDtosAsync(IQueryable<Notification> query, int userId, int skip, int take)
    {
        var rows = await query
            .OrderBy(n => n.Status == NotificationStatuses.Pending ? 0 : 1)
            .ThenByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.NotificationId)
            .Select(n => new
            {
                n.NotificationId,
                n.NotificationTypeId,
                TypeName = n.NotificationType.Name,
                n.NotificationType.Category,
                n.Message,
                n.Status,
                Read = db.NotificationReads.Any(r => r.NotificationId == n.NotificationId && r.UserId == userId),
                n.CreatedAt,
                n.DoneAt,
                DoneBy = n.DoneBy != null ? n.DoneBy.FirstName + " " + n.DoneBy.LastName : null,
                n.DoneNote,
            })
            .Skip(skip)
            .Take(take)
            .ToListAsync();

        return rows
            .Select(r => new NotificationDto(
                r.NotificationId,
                r.NotificationTypeId,
                r.TypeName,
                r.Category,
                r.Message,
                StateOf(r.Status, r.Read),
                r.CreatedAt,
                r.DoneAt,
                r.DoneBy,
                r.DoneNote))
            .ToList();
    }

    public async Task<NotificationSummaryDto> SummaryAsync(int userId)
    {
        var open = (await VisibleToAsync(userId)).Where(n => n.Status == NotificationStatuses.Pending);

        var total = await open.CountAsync();
        var read = await open.CountAsync(n =>
            db.NotificationReads.Any(r => r.NotificationId == n.NotificationId && r.UserId == userId));

        return new NotificationSummaryDto(total - read, read);
    }

    public async Task MarkReadAsync(int notificationId, int userId)
    {
        var visible = await (await VisibleToAsync(userId)).AnyAsync(n => n.NotificationId == notificationId);
        if (!visible)
            return;

        var alreadyRead = await db.NotificationReads
            .AnyAsync(r => r.NotificationId == notificationId && r.UserId == userId);
        if (alreadyRead)
            return;

        db.NotificationReads.Add(new NotificationRead { NotificationId = notificationId, UserId = userId });
        await db.SaveChangesAsync();
    }

    public async Task<int> MarkReadManyAsync(List<int> ids, int userId)
    {
        if (ids.Count == 0)
            throw new InvalidOperationException("Seleccione al menos una notificación.");

        var visibleIds = await (await VisibleToAsync(userId))
            .Where(n => ids.Contains(n.NotificationId))
            .Select(n => n.NotificationId)
            .ToListAsync();

        var readIds = await db.NotificationReads
            .Where(r => r.UserId == userId && visibleIds.Contains(r.NotificationId))
            .Select(r => r.NotificationId)
            .ToListAsync();

        var unread = visibleIds.Except(readIds).ToList();
        var now = DateTime.UtcNow;

        foreach (var id in unread)
            db.NotificationReads.Add(new NotificationRead { NotificationId = id, UserId = userId, ReadAt = now });

        await db.SaveChangesAsync();
        return unread.Count;
    }

    public async Task<int> MarkDoneAsync(List<int> ids, string? note, int userId)
    {
        if (ids.Count == 0)
            throw new InvalidOperationException("Seleccione al menos una notificación.");

        var cleanNote = note?.Trim();
        if (cleanNote is { Length: > NoteLength })
            cleanNote = cleanNote[..NoteLength];

        var pending = await (await VisibleToAsync(userId))
            .Where(n => ids.Contains(n.NotificationId) && n.Status == NotificationStatuses.Pending)
            .ToListAsync();

        if (pending.Count == 0)
            return 0;

        var pendingIds = pending.Select(n => n.NotificationId).ToList();
        var readIds = await db.NotificationReads
            .Where(r => r.UserId == userId && pendingIds.Contains(r.NotificationId))
            .Select(r => r.NotificationId)
            .ToListAsync();

        var now = DateTime.UtcNow;

        foreach (var notification in pending)
        {
            notification.Status = NotificationStatuses.Done;
            notification.DoneAt = now;
            notification.DoneById = userId;
            notification.DoneNote = string.IsNullOrEmpty(cleanNote) ? null : cleanNote;

            if (!readIds.Contains(notification.NotificationId))
                db.NotificationReads.Add(new NotificationRead { NotificationId = notification.NotificationId, UserId = userId, ReadAt = now });
        }

        await db.SaveChangesAsync();
        return pending.Count;
    }

    private async Task<IQueryable<Notification>> VisibleToAsync(int userId)
    {
        var roleIds = await db.UserRoles
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.RoleId)
            .ToListAsync();

        var typeIds = await db.NotificationTypeRecipients
            .Where(r => r.UserId == userId || (r.RoleId != null && roleIds.Contains(r.RoleId.Value)))
            .Select(r => r.NotificationTypeId)
            .Distinct()
            .ToListAsync();

        return db.Notifications.Where(n => typeIds.Contains(n.NotificationTypeId));
    }

    private static string StateOf(string status, bool read) =>
        status == NotificationStatuses.Done
            ? NotificationStates.Done
            : read ? NotificationStates.Pending : NotificationStates.Unread;
}

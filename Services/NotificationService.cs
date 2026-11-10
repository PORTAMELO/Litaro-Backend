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

public class NotificationService(AppDbContext db)
{
    private const int ListLimit = 500;
    private const int NoteLength = 300;

    public async Task<Notification?> QueueAsync(
        string typeCode,
        IReadOnlyDictionary<string, string> values,
        NotificationTarget? target = null)
    {
        var type = await db.NotificationTypes.FirstOrDefaultAsync(t => t.Code == typeCode && t.Active);
        if (type is null)
            return null;

        var dedupKey = target?.DedupKey;
        if (dedupKey is not null)
        {
            if (db.Notifications.Local.Any(n => n.DedupKey == dedupKey))
                return null;

            if (await db.Notifications.AnyAsync(n => n.DedupKey == dedupKey))
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
        var query = await VisibleToAsync(userId);

        if (typeId is not null)
            query = query.Where(n => n.NotificationTypeId == typeId);

        var wanted = state?.Trim().ToUpperInvariant();

        query = wanted switch
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
            .Take(ListLimit)
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

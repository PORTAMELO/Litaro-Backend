using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services;

public record RecipientRoleDto(int RoleId, string Name);

public record RecipientUserDto(int UserId, string Name);

public record NotificationTypeDto(
    int NotificationTypeId,
    string Code,
    string Name,
    string Category,
    string Template,
    bool Active,
    List<RecipientRoleDto> Roles,
    List<RecipientUserDto> Users);

public record UpdateNotificationTypeRequest(string? Template, bool Active, List<int>? RoleIds, List<int>? UserIds);

public class NotificationSettingsService(AppDbContext db)
{
    private const int SearchLimit = 20;

    public async Task<List<NotificationTypeDto>> ListAsync()
    {
        var types = await db.NotificationTypes
            .AsNoTracking()
            .OrderBy(t => t.Category)
            .ThenBy(t => t.Name)
            .ToListAsync();

        var recipients = await db.NotificationTypeRecipients
            .AsNoTracking()
            .Select(r => new
            {
                r.NotificationTypeId,
                r.RoleId,
                RoleName = r.Role != null ? r.Role.Name : null,
                r.UserId,
                UserName = r.User != null ? r.User.FirstName + " " + r.User.LastName : null,
            })
            .ToListAsync();

        return types
            .Select(t => new NotificationTypeDto(
                t.NotificationTypeId,
                t.Code,
                t.Name,
                t.Category,
                t.Template,
                t.Active,
                recipients
                    .Where(r => r.NotificationTypeId == t.NotificationTypeId && r.RoleId != null)
                    .Select(r => new RecipientRoleDto(r.RoleId!.Value, r.RoleName ?? string.Empty))
                    .OrderBy(r => r.Name)
                    .ToList(),
                recipients
                    .Where(r => r.NotificationTypeId == t.NotificationTypeId && r.UserId != null)
                    .Select(r => new RecipientUserDto(r.UserId!.Value, r.UserName ?? string.Empty))
                    .OrderBy(r => r.Name)
                    .ToList()))
            .ToList();
    }

    public Task<List<RecipientRoleDto>> ListRolesAsync() =>
        db.Roles
            .AsNoTracking()
            .OrderBy(r => r.Name)
            .Select(r => new RecipientRoleDto(r.Id, r.Name!))
            .ToListAsync();

    public async Task<List<RecipientUserDto>> SearchUsersAsync(string? query)
    {
        var text = (query ?? string.Empty).Trim().ToLower();
        if (text.Length < 2)
            return [];

        return await db.Users
            .AsNoTracking()
            .Where(u => u.Active &&
                        (u.FirstName.ToLower().Contains(text) ||
                         u.LastName.ToLower().Contains(text) ||
                         u.DocumentNumber.Contains(text)))
            .OrderBy(u => u.FirstName)
            .ThenBy(u => u.LastName)
            .Take(SearchLimit)
            .Select(u => new RecipientUserDto(u.Id, u.FirstName + " " + u.LastName))
            .ToListAsync();
    }

    public async Task<NotificationTypeDto> UpdateAsync(int id, UpdateNotificationTypeRequest request)
    {
        var type = await db.NotificationTypes.FirstOrDefaultAsync(t => t.NotificationTypeId == id);
        if (type is null)
            throw new InvalidOperationException("No existe el tipo de notificación.");

        var template = (request.Template ?? string.Empty).Trim();
        if (template.Length == 0)
            throw new InvalidOperationException("El mensaje es obligatorio.");

        if (template.Length > NotificationTemplate.MaxLength)
            throw new InvalidOperationException("El mensaje no puede superar 500 caracteres.");

        var roleIds = (request.RoleIds ?? []).Distinct().ToList();
        var userIds = (request.UserIds ?? []).Distinct().ToList();

        var knownRoles = await db.Roles.CountAsync(r => roleIds.Contains(r.Id));
        var knownUsers = await db.Users.CountAsync(u => userIds.Contains(u.Id));

        if (knownRoles != roleIds.Count || knownUsers != userIds.Count)
            throw new InvalidOperationException("Alguno de los destinatarios no existe.");

        type.Template = template;
        type.Active = request.Active;

        var current = await db.NotificationTypeRecipients
            .Where(r => r.NotificationTypeId == id)
            .ToListAsync();
        db.NotificationTypeRecipients.RemoveRange(current);

        db.NotificationTypeRecipients.AddRange(
            roleIds.Select(roleId => new NotificationTypeRecipient { NotificationTypeId = id, RoleId = roleId }));
        db.NotificationTypeRecipients.AddRange(
            userIds.Select(userId => new NotificationTypeRecipient { NotificationTypeId = id, UserId = userId }));

        await db.SaveChangesAsync();

        return (await ListAsync()).First(t => t.NotificationTypeId == id);
    }
}

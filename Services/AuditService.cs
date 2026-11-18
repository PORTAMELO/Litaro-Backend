using System.Text.Json;
using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services;

public static class LoginAuditReasons
{
    public const string UserNotFound = "USER_NOT_FOUND";
    public const string UserInactive = "USER_INACTIVE";
    public const string WrongPassword = "WRONG_PASSWORD";
    public const string LockedOut = "LOCKED_OUT";
    public const string ActivationRequired = "ACTIVATION_REQUIRED";
    public const string MustChangePassword = "MUST_CHANGE_PASSWORD";

    public static string? For(string step, bool isLockedOut) => step switch
    {
        "USER_NULL" => UserNotFound,
        "USER_INACTIVE" => UserInactive,
        "PASSWORD_CHECK_FAILED" => isLockedOut ? LockedOut : WrongPassword,
        "ACTIVATION_REQUIRED" => ActivationRequired,
        "MUST_CHANGE_PASSWORD" => MustChangePassword,
        _ => null
    };
}

public class AuditService(AppDbContext db, AuditContext audit)
{
    private const int UnknownLoginMaxLength = 100;

    public async Task LogAccessAsync(string eventType, int? userId, string? userName, string? reason, DateTime nowUtc)
    {
        var maxLength = userId.HasValue ? AuditContext.UserNameMaxLength : UnknownLoginMaxLength;

        db.AuditLogs.Add(new AuditLog
        {
            OccurredAt = nowUtc,
            EventType = eventType,
            TableName = "Auth",
            ActorKind = userId.HasValue ? AuditActorKinds.User : AuditActorKinds.Anonymous,
            DbUser = db.Database.IsRelational() ? null! : "inmemory",
            UserId = userId,
            UserName = AuditContext.Truncate(userName, maxLength),
            Source = audit.Source,
            IpAddress = audit.IpAddress,
            UserAgent = audit.UserAgent,
            OperationId = audit.OperationId,
            Detail = reason is null ? null : JsonSerializer.Serialize(new { reason })
        });

        await db.SaveChangesAsync();
    }
}

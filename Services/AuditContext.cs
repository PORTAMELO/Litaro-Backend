using Litaro.Models;

namespace Litaro.Services;

public sealed class AuditContext
{
    public const int UserNameMaxLength = 200;
    public const int SourceMaxLength = 200;
    public const int UserAgentMaxLength = 300;
    public const int IpAddressMaxLength = 45;

    public string ActorKind { get; private set; } = AuditActorKinds.System;

    public int? UserId { get; private set; }

    public string? UserName { get; private set; }

    public string? Source { get; private set; }

    public string? IpAddress { get; private set; }

    public string? UserAgent { get; private set; }

    public Guid OperationId { get; private set; } = Guid.NewGuid();

    public string? Action { get; private set; }

    public void SetRequest(string source, string? ip, string? userAgent)
    {
        Source = Truncate(source, SourceMaxLength);
        IpAddress = Truncate(ip, IpAddressMaxLength);
        UserAgent = Truncate(userAgent, UserAgentMaxLength);
    }

    public void SetUser(int userId, string userName)
    {
        ActorKind = AuditActorKinds.User;
        UserId = userId;
        UserName = Truncate(userName, UserNameMaxLength);
    }

    public void SetAnonymous()
    {
        ActorKind = AuditActorKinds.Anonymous;
        UserId = null;
        UserName = null;
    }

    public void SetSystem(string source)
    {
        ActorKind = AuditActorKinds.System;
        UserId = null;
        UserName = null;
        Source = Truncate(source, SourceMaxLength);
        OperationId = Guid.NewGuid();
    }

    public IDisposable BeginAction(string action)
    {
        var previous = Action;
        Action = action;
        return new ActionScope(this, previous);
    }

    public static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
            return null;

        if (value.Length <= maxLength)
            return value;

        var length = char.IsHighSurrogate(value[maxLength - 1]) ? maxLength - 1 : maxLength;
        return value[..length];
    }

    private sealed class ActionScope(AuditContext audit, string? previous) : IDisposable
    {
        private bool disposed;

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            audit.Action = previous;
        }
    }
}

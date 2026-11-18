namespace Litaro.Models
{
    public static class AuditEventTypes
    {
        public const string Insert = "INSERT";
        public const string Update = "UPDATE";
        public const string Delete = "DELETE";
        public const string LoginOk = "LOGIN_OK";
        public const string LoginFail = "LOGIN_FAIL";
        public const string Logout = "LOGOUT";
        public const string DocUpload = "DOC_UPLOAD";
        public const string DocUpdate = "DOC_UPDATE";
        public const string DocReplace = "DOC_REPLACE";
        public const string DocReplaced = "DOC_REPLACED";
        public const string DocRemove = "DOC_REMOVE";
        public const string DocPurge = "DOC_PURGE";
    }

    public static class AuditActions
    {
        public const string DocReplace = "DOC_REPLACE";
    }

    public static class AuditActorKinds
    {
        public const string User = "USER";
        public const string System = "SYSTEM";
        public const string DirectDb = "DIRECT_DB";
        public const string Anonymous = "ANONYMOUS";
    }

    public static class AuditSettings
    {
        public const string App = "litaro.app";
        public const string ActorKind = "litaro.actor_kind";
        public const string UserId = "litaro.user_id";
        public const string UserName = "litaro.user_name";
        public const string Source = "litaro.source";
        public const string Ip = "litaro.ip";
        public const string UserAgent = "litaro.user_agent";
        public const string OperationId = "litaro.operation_id";
        public const string Action = "litaro.action";
    }

    public class AuditLog
    {
        public long AuditLogId { get; set; }
        public DateTime OccurredAt { get; set; }
        public string EventType { get; set; } = string.Empty;
        public string? TableName { get; set; }
        public string? RecordId { get; set; }
        public string? OldData { get; set; }
        public string? NewData { get; set; }
        public string[]? ChangedColumns { get; set; }
        public string ActorKind { get; set; } = string.Empty;
        public int? UserId { get; set; }
        public string? UserName { get; set; }
        public string DbUser { get; set; } = null!;
        public string? Source { get; set; }
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
        public Guid? OperationId { get; set; }
        public string? Detail { get; set; }
    }
}

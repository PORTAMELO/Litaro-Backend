using Microsoft.AspNetCore.Identity;

namespace Litaro.Models
{
    public static class NotificationCodes
    {
        public const string SimatAdmissionApproved = "SIMAT_ADMISSION_APPROVED";
        public const string SimatCupoDeclined = "SIMAT_CUPO_DECLINED";
        public const string SimatCupoLost = "SIMAT_CUPO_LOST";
        public const string SimatNotInSimat = "SIMAT_NOT_IN_SIMAT";
        public const string SimatClassroomChange = "SIMAT_CLASSROOM_CHANGE";
        public const string SimatCampusTransfer = "SIMAT_CAMPUS_TRANSFER";
        public const string SimatWithdrawal = "SIMAT_WITHDRAWAL";
        public const string AttEvasion = "ATT_EVASION";
        public const string AttClassTardy = "ATT_CLASS_TARDY";
        public const string AttLateCommitment = "ATT_LATE_COMMITMENT";
        public const string AttLateCitation = "ATT_LATE_CITATION";
        public const string AttWeekAbsence = "ATT_WEEK_ABSENCE";
        public const string AttPeriodAbsence = "ATT_PERIOD_ABSENCE";
    }

    public static class NotificationStatuses
    {
        public const string Pending = "PENDING";
        public const string Done = "DONE";
    }

    public static class NotificationStates
    {
        public const string Unread = "UNREAD";
        public const string Pending = "PENDING";
        public const string Done = "DONE";
    }

    public class NotificationType
    {
        public int NotificationTypeId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Template { get; set; } = string.Empty;
        public bool Active { get; set; } = true;
    }

    public class NotificationTypeRecipient
    {
        public int NotificationTypeRecipientId { get; set; }
        public int NotificationTypeId { get; set; }
        public int? RoleId { get; set; }
        public int? UserId { get; set; }
        public NotificationType NotificationType { get; set; } = null!;
        public IdentityRole<int>? Role { get; set; }
        public User? User { get; set; }
    }

    public class Notification
    {
        public int NotificationId { get; set; }
        public int NotificationTypeId { get; set; }
        public string Message { get; set; } = string.Empty;
        public int? StudentId { get; set; }
        public short? YearId { get; set; }
        public int? CampusId { get; set; }
        public string? SourceTable { get; set; }
        public int? SourceId { get; set; }
        public string? DedupKey { get; set; }
        public string Status { get; set; } = NotificationStatuses.Pending;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? DoneAt { get; set; }
        public int? DoneById { get; set; }
        public string? DoneNote { get; set; }
        public NotificationType NotificationType { get; set; } = null!;
        public Student? Student { get; set; }
        public AcademicYear? AcademicYear { get; set; }
        public Campus? Campus { get; set; }
        public User? DoneBy { get; set; }
    }

    public class NotificationRead
    {
        public int NotificationId { get; set; }
        public int UserId { get; set; }
        public DateTime ReadAt { get; set; } = DateTime.UtcNow;
        public Notification Notification { get; set; } = null!;
        public User User { get; set; } = null!;
    }
}

namespace Litaro.Models
{
    public static class PreEnrollmentStatuses
    {
        public const string Reserved = "RESERVED";
        public const string Confirmed = "CONFIRMED";
        public const string Declined = "DECLINED";
        public const string Lost = "LOST";
    }

    public static class AdmissionStatuses
    {
        public const string Pending = "PENDING";
        public const string Approved = "APPROVED";
        public const string Rejected = "REJECTED";
        public const string Waitlist = "WAITLIST";
    }

    public class EnrollmentPeriod
    {
        public int EnrollmentPeriodId { get; set; }
        public short YearId { get; set; }
        public DateOnly PreEnrollmentStart { get; set; }
        public DateOnly PreEnrollmentEnd { get; set; }
        public DateOnly AdmissionStart { get; set; }
        public DateOnly AdmissionEnd { get; set; }
        public AcademicYear AcademicYear { get; set; } = null!;
    }

    public class PreEnrollment
    {
        public int PreEnrollmentId { get; set; }
        public short YearId { get; set; }
        public int StudentId { get; set; }
        public int GradeId { get; set; }
        public string Status { get; set; } = PreEnrollmentStatuses.Reserved;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? RespondedAt { get; set; }
        public string? ResponseNote { get; set; }
        public AcademicYear AcademicYear { get; set; } = null!;
        public Student Student { get; set; } = null!;
        public Grade Grade { get; set; } = null!;
    }

    public class AdmissionRequest
    {
        public int AdmissionRequestId { get; set; }
        public short YearId { get; set; }
        public int GradeId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string DocumentType { get; set; } = string.Empty;
        public string DocumentNumber { get; set; } = string.Empty;
        public DateOnly BirthDate { get; set; }
        public string GuardianName { get; set; } = string.Empty;
        public string GuardianPhone { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public string Status { get; set; } = AdmissionStatuses.Pending;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int CreatedById { get; set; }
        public DateTime? WaitlistedAt { get; set; }
        public DateTime? DecidedAt { get; set; }
        public int? DecidedById { get; set; }
        public string? DecisionNote { get; set; }
        public AcademicYear AcademicYear { get; set; } = null!;
        public Grade Grade { get; set; } = null!;
        public User CreatedBy { get; set; } = null!;
        public User? DecidedBy { get; set; }
    }

    public class AdmissionRequestEvent
    {
        public int AdmissionRequestEventId { get; set; }
        public int AdmissionRequestId { get; set; }
        public string? FromStatus { get; set; }
        public string ToStatus { get; set; } = string.Empty;
        public string? Note { get; set; }
        public int UserId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public AdmissionRequest AdmissionRequest { get; set; } = null!;
        public User User { get; set; } = null!;
    }
}
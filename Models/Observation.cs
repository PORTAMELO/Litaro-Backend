namespace Litaro.Models
{
    public static class CoexistenceTypes
    {
        public const string TypeI = "I";
        public const string TypeII = "II";
        public const string TypeIII = "III";

        public static readonly IReadOnlyList<string> All = [TypeI, TypeII, TypeIII];
    }

    public static class CommitmentStatuses
    {
        public const string Pending = "PENDING";
        public const string Met = "MET";
        public const string NotMet = "NOT_MET";
        public const string Observation = "OBSERVATION";

        public static readonly IReadOnlyList<string> All = [Pending, Met, NotMet, Observation];
    }

    public static class ObservationActions
    {
        public const string Create = "CREATE";
        public const string Update = "UPDATE";
        public const string Statement = "STATEMENT";
        public const string StatementFile = "STATEMENT_FILE";
        public const string StatementFileRemoved = "STATEMENT_FILE_REMOVED";
        public const string CommitmentAdded = "COMMITMENT_ADDED";
        public const string CommitmentStatus = "COMMITMENT_STATUS";
    }

    public class Observation
    {
        public int ObservationId { get; set; }
        public int EnrollmentId { get; set; }
        public DateOnly Date { get; set; }
        public string Description { get; set; } = string.Empty;
        public bool IsAcademic { get; set; }
        public bool IsCoexistence { get; set; }
        public string? CoexistenceType { get; set; }
        public int? SubjectId { get; set; }
        public string? Statement { get; set; }
        public int RecordedById { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public Enrollment Enrollment { get; set; } = null!;
        public Subject? Subject { get; set; }
        public User RecordedBy { get; set; } = null!;
        public List<ObservationCommitment> Commitments { get; set; } = [];
        public List<ObservationEvent> Events { get; set; } = [];
    }

    public class ObservationCommitment
    {
        public int ObservationCommitmentId { get; set; }
        public int ObservationId { get; set; }
        public string Text { get; set; } = string.Empty;
        public string Status { get; set; } = CommitmentStatuses.Pending;
        public DateOnly? FollowUpDate { get; set; }
        public string? Note { get; set; }
        public int CreatedById { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public Observation Observation { get; set; } = null!;
        public User CreatedBy { get; set; } = null!;
    }

    public class ObservationEvent
    {
        public int ObservationEventId { get; set; }
        public int ObservationId { get; set; }
        public string Action { get; set; } = string.Empty;
        public string? Detail { get; set; }
        public int UserId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public Observation Observation { get; set; } = null!;
        public User User { get; set; } = null!;
    }
}

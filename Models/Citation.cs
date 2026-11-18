namespace Litaro.Models
{
    public static class CitationOrigins
    {
        public const string Automatic = "AUTO";
        public const string Manual = "MANUAL";
    }

    public static class CitationStatuses
    {
        public const string ToSchedule = "TO_SCHEDULE";
        public const string Scheduled = "SCHEDULED";
        public const string Rescheduled = "RESCHEDULED";
        public const string Cancelled = "CANCELLED";
        public const string Closed = "CLOSED";

        public static readonly IReadOnlyList<string> All = [ToSchedule, Scheduled, Rescheduled, Cancelled, Closed];
    }

    public class Citation
    {
        public int CitationId { get; set; }
        public int StudentId { get; set; }
        public int? ParentId { get; set; }
        public int? ObservationId { get; set; }
        public string Origin { get; set; } = CitationOrigins.Manual;
        public string Reason { get; set; } = string.Empty;
        public int? ResponsibleUserId { get; set; }
        public DateOnly? ScheduledDate { get; set; }
        public TimeOnly? ScheduledTime { get; set; }
        public int? ScheduleId { get; set; }
        public string Status { get; set; } = CitationStatuses.ToSchedule;
        public bool StudentInformed { get; set; }
        public bool ParentInformed { get; set; }
        public bool Attended { get; set; }
        public int? RescheduledFromId { get; set; }
        public string? Note { get; set; }
        public int CreatedById { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public Student Student { get; set; } = null!;
        public Parent? Parent { get; set; }
        public Observation? Observation { get; set; }
        public User? ResponsibleUser { get; set; }
        public Schedule? Schedule { get; set; }
        public Citation? RescheduledFrom { get; set; }
        public User CreatedBy { get; set; } = null!;
    }
}

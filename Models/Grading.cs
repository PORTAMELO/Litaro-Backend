namespace Litaro.Models
{
    public static class GradingModes
    {
        public const string Weighted = "WEIGHTED";
        public const string Average = "AVERAGE";
    }

    public static class GradeComponents
    {
        public const string Hetero = "HETERO";
        public const string Saber = "SABER";
        public const string Self = "SELF";
        public const string Peer = "PEER";
    }

    public static class DeadlineKinds
    {
        public const string Planner = "PLANNER";
        public const string DesignLock = "DESIGN_LOCK";
    }

    public class GradingScheme
    {
        public int GradingSchemeId { get; set; }
        public int ClassroomId { get; set; }
        public int SubjectId { get; set; }
        public short PeriodId { get; set; }
        public string Mode { get; set; } = GradingModes.Average;
        public int UpdatedById { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public Classroom Classroom { get; set; } = null!;
        public Subject Subject { get; set; } = null!;
        public AcademicPeriod AcademicPeriod { get; set; } = null!;
        public User UpdatedBy { get; set; } = null!;
        public List<GradeActivity> Activities { get; set; } = [];
    }

    public class GradeActivity
    {
        public int GradeActivityId { get; set; }
        public int GradingSchemeId { get; set; }
        public string Component { get; set; } = GradeComponents.Hetero;
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public string? Competencies { get; set; }
        public decimal? Weight { get; set; }
        public int DisplayOrder { get; set; }
        public int CreatedById { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public GradingScheme GradingScheme { get; set; } = null!;
        public User CreatedBy { get; set; } = null!;
        public List<ActivityScore> Scores { get; set; } = [];
    }

    public class ActivityScore
    {
        public int ActivityScoreId { get; set; }
        public int GradeActivityId { get; set; }
        public int EnrollmentId { get; set; }
        public decimal Value { get; set; }
        public int RecordedById { get; set; }
        public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
        public int? UpdatedById { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public GradeActivity GradeActivity { get; set; } = null!;
        public Enrollment Enrollment { get; set; } = null!;
        public User RecordedBy { get; set; } = null!;
        public User? UpdatedBy { get; set; }
    }

    public class GradingDeadline
    {
        public int GradingDeadlineId { get; set; }
        public short YearId { get; set; }
        public short? PeriodId { get; set; }
        public string Kind { get; set; } = null!;
        public DateTime DueAt { get; set; }
        public int UpdatedById { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public AcademicYear AcademicYear { get; set; } = null!;
        public AcademicPeriod? AcademicPeriod { get; set; }
        public User UpdatedBy { get; set; } = null!;
    }
}

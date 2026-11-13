namespace Litaro.Models
{
    public static class EnrollmentEventTypes
    {
        public const string Classroom = "CLASSROOM";
        public const string Campus = "CAMPUS";
        public const string Withdrawal = "WITHDRAWAL";
    }

    public class EnrollmentEvent
    {
        public int EnrollmentEventId { get; set; }
        public int EnrollmentId { get; set; }
        public string Type { get; set; } = string.Empty;
        public int FromClassroomId { get; set; }
        public int? ToClassroomId { get; set; }
        public string? Reason { get; set; }
        public string? Note { get; set; }
        public int UserId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public Enrollment Enrollment { get; set; } = null!;
        public Classroom FromClassroom { get; set; } = null!;
        public Classroom? ToClassroom { get; set; }
        public User User { get; set; } = null!;
    }
}

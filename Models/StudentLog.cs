namespace Litaro.Models
{
    public class StudentLog
    {
        public int LogId { get; set; }
        public int EnrollmentId { get; set; }
        public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
        public string Type { get; set; } = string.Empty;
        public string Observation { get; set; } = string.Empty;
        public int UserRecordedId { get; set; }
        public Enrollment Enrollment { get; set; } = null!;
        public User User { get; set; } = null!;
    }
}

namespace Litaro.Models
{
    public class Attendance
    {
        public int AttendanceId { get; set; }
        public int EnrollmentId { get; set; }
        public int SubjectId { get; set; }
        public DateTime Date { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Observation { get; set; }
        public Enrollment Enrollment { get; set; } = null!;
        public Subject Subject { get; set; } = null!;
    }
}

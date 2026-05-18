namespace Litaro.Models
{
    public class Enrollment
    {
        public int EnrollmentId { get; set; }
        public int StudentId { get; set; }
        public int ClassroomId { get; set; }
        public short YearId { get; set; }
        public string Status { get; set; } = "ACTIVE";
        public DateTime EnrollmentDate { get; set; } = DateTime.UtcNow;
        public Student Student { get; set; } = null!;
        public Classroom Classroom { get; set; } = null!;
        public AcademicYear AcademicYear { get; set; } = null!;
    }
}

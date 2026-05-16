namespace Litaro.Models
{
    public class GradeScore
    {
        public int GradeScoreId { get; set; }
        public int EnrollmentId { get; set; }
        public int SubjectId { get; set; }
        public short PeriodId { get; set; }
        public decimal Value { get; set; }
        public string? Description { get; set; }
        public DateTime RecordedDate { get; set; } = DateTime.UtcNow;
        public Enrollment Enrollment { get; set; } = null!;
        public Subject Subject { get; set; } = null!;
        public AcademicPeriod AcademicPeriod { get; set; } = null!;
    }
}

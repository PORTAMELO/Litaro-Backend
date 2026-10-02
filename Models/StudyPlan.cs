namespace Litaro.Models
{
    public class StudyPlan
    {
        public int StudyPlanId { get; set; }
        public short YearId { get; set; }
        public int GradeId { get; set; }
        public int SubjectId { get; set; }
        public byte WeeklyHours { get; set; }
        public AcademicYear AcademicYear { get; set; } = null!;
        public Grade Grade { get; set; } = null!;
        public Subject Subject { get; set; } = null!;
    }
}

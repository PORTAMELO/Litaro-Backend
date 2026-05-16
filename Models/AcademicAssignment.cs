namespace Litaro.Models
{
    public class AcademicAssignment
    {
        public int AssignmentId { get; set; }
        public int ClassroomId { get; set; }
        public int SubjectId { get; set; }
        public int TeacherId { get; set; }
        public short YearId { get; set; }
        public bool Active { get; set; } = true;
        public Classroom Classroom { get; set; } = null!;
        public Subject Subject { get; set; } = null!;
        public Teacher Teacher { get; set; } = null!;
        public AcademicYear AcademicYear { get; set; } = null!;
    }
}

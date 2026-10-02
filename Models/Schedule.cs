namespace Litaro.Models
{
    public class Schedule
    {
        public int ScheduleId { get; set; }
        public short YearId { get; set; }
        public int CampusId { get; set; }
        public string Type { get; set; } = ScheduleTypes.Class;
        public string? Title { get; set; }
        public byte Weekday { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
        public int TeacherId { get; set; }
        public int? AssignmentId { get; set; }
        public int? ClassroomId { get; set; }
        public int? SpaceId { get; set; }
        public AcademicYear AcademicYear { get; set; } = null!;
        public Campus Campus { get; set; } = null!;
        public Teacher Teacher { get; set; } = null!;
        public AcademicAssignment? AcademicAssignment { get; set; }
        public Classroom? Classroom { get; set; }
        public Space? Space { get; set; }
    }
}

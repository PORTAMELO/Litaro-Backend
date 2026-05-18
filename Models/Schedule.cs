namespace Litaro.Models
{
    public class Schedule
    {
        public int ScheduleId { get; set; }
        public int AssignmentId { get; set; }
        public byte Weekday { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
        public AcademicAssignment AcademicAssignment { get; set; } = null!;
    }
}

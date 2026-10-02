namespace Litaro.Models
{
    public class TeacherAvailability
    {
        public int TeacherAvailabilityId { get; set; }
        public short YearId { get; set; }
        public int TeacherId { get; set; }
        public byte Weekday { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
        public int? CampusId { get; set; }
        public AcademicYear AcademicYear { get; set; } = null!;
        public Teacher Teacher { get; set; } = null!;
        public Campus? Campus { get; set; }
    }
}

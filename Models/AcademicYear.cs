namespace Litaro.Models
{
    public class AcademicYear
    {
        public short YearId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool Active { get; set; } = true;
    }
}

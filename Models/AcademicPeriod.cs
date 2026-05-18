namespace Litaro.Models
{
    public class AcademicPeriod
    {
        public short PeriodId { get; set; }
        public byte PeriodNumber { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public short YearId { get; set; }
        public AcademicYear AcademicYear { get; set; } = null!;
    }
}

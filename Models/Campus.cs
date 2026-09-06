namespace Litaro.Models
{
    public class Campus
    {
        public int CampusId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string Dane { get; set; } = string.Empty;
        public bool Active { get; set; } = true;
        public int SchoolId { get; set; }
        public School School { get; set; } = null!;
    }
}

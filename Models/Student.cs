namespace Litaro.Models
{
    public class Student
    {
        public int StudentId { get; set; }
        public string StudentCode { get; set; } = string.Empty;
        public DateTime BirthDate { get; set; }
        public char Gender { get; set; }
        public bool Active { get; set; } = true;
        public long? SimatPerId { get; set; }
        public string? Neighborhood { get; set; }
        public string? Eps { get; set; }
        public string? BloodType { get; set; }
        public bool HasPiar { get; set; }
        public User User { get; set; } = null!;
    }
}
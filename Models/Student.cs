namespace Litaro.Models
{
    public class Student
    {
        public int StudentId { get; set; }
        public string StudentCode { get; set; } = string.Empty;
        public DateTime BirthDate { get; set; }
        public char Gender { get; set; }
        public User User { get; set; } = null!;
    }
}

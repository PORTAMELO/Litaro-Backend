namespace Litaro.Models
{
    public class Teacher
    {
        public int TeacherId { get; set; }
        public string Specialty { get; set; } = string.Empty;
        public User User { get; set; } = null!;
    }
}

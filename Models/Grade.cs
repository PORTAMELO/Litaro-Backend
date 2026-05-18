namespace Litaro.Models
{
    public class Grade
    {
        public int GradeId { get; set; }
        public string Name { get; set; } = string.Empty;
        public byte OrderNum { get; set; }
        public string Level { get; set; } = string.Empty;
    }
}

namespace Litaro.Models
{
    public class ParentStudent
    {
        public int ParentStudentId { get; set; }
        public int ParentId { get; set; }
        public int StudentId { get; set; }
        public bool PrimaryContact { get; set; } = true;
        public Parent Parent { get; set; } = null!;
        public Student Student { get; set; } = null!;
    }
}

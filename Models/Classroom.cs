namespace Litaro.Models
{
    public class Classroom
    {
        public int ClassroomId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int GradeId { get; set; }
        public int CampusId { get; set; }
        public bool Active { get; set; } = true;
        public short? ClassMinutes { get; set; }
        public int? DirectorTeacherId { get; set; }
        public Grade Grade { get; set; } = null!;
        public Campus Campus { get; set; } = null!;
        public Teacher? DirectorTeacher { get; set; }
    }
}

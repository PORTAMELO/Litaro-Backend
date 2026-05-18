namespace Litaro.Models
{
    public class Subject
    {
        public int SubjectId { get; set; }
        public string Name { get; set; } = string.Empty;
        public byte WeeklyHours { get; set; }
        public string KnowledgeArea { get; set; } = string.Empty;
    }
}

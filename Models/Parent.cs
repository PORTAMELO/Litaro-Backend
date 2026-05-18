namespace Litaro.Models
{
    public class Parent
    {
        public int ParentId { get; set; }
        public string Relationship { get; set; } = string.Empty;
        public User User { get; set; } = null!;
    }
}

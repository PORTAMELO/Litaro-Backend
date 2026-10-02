namespace Litaro.Models
{
    public class Space
    {
        public int SpaceId { get; set; }
        public int CampusId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public short? Capacity { get; set; }
        public bool Active { get; set; } = true;
        public Campus Campus { get; set; } = null!;
    }
}

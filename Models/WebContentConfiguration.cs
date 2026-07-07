namespace Litaro.Models
{
    public class WebContentConfiguration
    {
        public int WebContentConfigurationId { get; set; }
        public string PageName { get; set; } = string.Empty;
        public string SectionName { get; set; } = string.Empty;
        public string ContentKey { get; set; } = string.Empty;
        public int MinItems { get; set; }
        public int? MaxItems { get; set; }
        public bool Active { get; set; } = true;
        public string TemplateJson { get; set; } = string.Empty;
        public DateTime CreationDate { get; set; }
    }
}
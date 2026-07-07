namespace Litaro.Models
{
    public class WebContent
    {
        public int WebContentId { get; set; }

        public string PageName { get; set; } = string.Empty;

        public string SectionName { get; set; } = string.Empty;

        public string ContentKey { get; set; } = string.Empty;

        public int DisplayOrder { get; set; }

        public string DataJson { get; set; } = string.Empty;

        public bool Active { get; set; } = true;

        public DateTime CreationDate { get; set; }

        public DateTime? UpdateDate { get; set; }
    }
}
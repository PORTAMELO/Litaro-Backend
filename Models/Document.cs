namespace Litaro.Models
{
    public class Document
    {
        public int DocumentId { get; set; }
        public string Module { get; set; } = null!;
        public string DocumentType { get; set; } = null!;
        public string EntityTable { get; set; } = null!;
        public int EntityId { get; set; }
        public short? YearId { get; set; }
        public string StoragePath { get; set; } = null!;
        public string FileName { get; set; } = null!;
        public string ContentType { get; set; } = null!;
        public long SizeBytes { get; set; }
        public bool IsCurrent { get; set; } = true;
        public int UploadedById { get; set; }
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
        public int? ReplacedById { get; set; }
        public DateTime? ReplacedAt { get; set; }
        public AcademicYear? AcademicYear { get; set; }
        public User UploadedBy { get; set; } = null!;
        public User? ReplacedBy { get; set; }
    }
}

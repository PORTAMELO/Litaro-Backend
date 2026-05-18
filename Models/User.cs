namespace Litaro.Models
{
    public class User
    {
        public int UserId { get; set; }
        public string DocumentType { get; set; } = string.Empty;
        public string DocumentNumber { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool Active { get; set; } = true;
        public DateTime CreationDate { get; set; } = DateTime.UtcNow;
        public int CampusId { get; set; }
        public Campus Campus { get; set; } = null!;
    }
}

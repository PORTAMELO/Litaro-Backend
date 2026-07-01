using Microsoft.AspNetCore.Identity;

namespace Litaro.Models
{
    public class User: IdentityUser<int>
    {
        public string DocumentType { get; set; } = string.Empty;
        public string DocumentNumber { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public bool Active { get; set; } = true;
        public DateTime CreationDate { get; set; } = DateTime.UtcNow;
        public int CampusId { get; set; }
        public Campus Campus { get; set; } = null!;
    }
}

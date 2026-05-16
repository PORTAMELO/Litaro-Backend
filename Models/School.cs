namespace Litaro.Models;

public class School
{
    public int SchoolId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Nit { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public bool Active { get; set; } = true;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
}
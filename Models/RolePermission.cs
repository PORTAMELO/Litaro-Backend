using Microsoft.AspNetCore.Identity;

namespace Litaro.Models;

public class RolePermission
{
    public int RolePermissionId { get; set; }

    public int RoleId { get; set; }
    public IdentityRole<int> Role { get; set; } = null!;

    public string TableName { get; set; } = string.Empty;

    public int Permissions { get; set; }

    public string? View { get; set; }
}
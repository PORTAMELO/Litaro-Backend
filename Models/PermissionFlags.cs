namespace Litaro.Models;

[Flags]
public enum PermissionFlags
{
    None = 0,
    Read = 1,
    Create = 2,
    Update = 4,
    Delete = 8,
    Full = Read | Create | Update | Delete // 15
}
using Litaro.Data;
using Microsoft.AspNetCore.Identity;

namespace Litaro.Tests;

public static class TestData
{
    public static IdentityRole<int> AddRole(AppDbContext db, string name)
    {
        var role = new IdentityRole<int>(name) { NormalizedName = name.ToUpperInvariant() };
        db.Roles.Add(role);
        db.SaveChanges();
        return role;
    }
}

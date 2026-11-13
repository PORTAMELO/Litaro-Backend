using Litaro.Models;
using Litaro.Services;

namespace Litaro.Tests;

public class PermissionServiceTests
{
    [Fact]
    public async Task HasPermission_is_true_for_update_only_keys()
    {
        using var db = TestDb.Create();
        var rector = TestData.AddRole(db, "Rector");
        db.RolePermissions.Add(new RolePermission { RoleId = rector.Id, TableName = ProcessKeys.AdmissionDecision, Permissions = 4 });
        await db.SaveChangesAsync();
        var svc = new PermissionService(db);

        Assert.True(await svc.HasPermissionAsync(new[] { "Rector" }, ProcessKeys.AdmissionDecision, PermissionFlags.Update));
        Assert.False(await svc.HasPermissionAsync(new[] { "Rector" }, ProcessKeys.AdmissionDecision, PermissionFlags.Read));
    }

    [Fact]
    public async Task HasPermission_is_false_when_the_row_is_zero()
    {
        using var db = TestDb.Create();
        var role = TestData.AddRole(db, "Profesor");
        db.RolePermissions.Add(new RolePermission { RoleId = role.Id, TableName = ProcessKeys.GradesModule, Permissions = 0 });
        await db.SaveChangesAsync();

        Assert.False(await new PermissionService(db).HasPermissionAsync(new[] { "Profesor" }, ProcessKeys.GradesModule, PermissionFlags.Read));
    }

    [Fact]
    public async Task Cached_service_matches_uncached_results()
    {
        using var db = TestDb.Create();
        var admin = TestData.AddRole(db, "Administrador");
        var teacher = TestData.AddRole(db, "Profesor");
        var rector = TestData.AddRole(db, "Rector");
        db.RolePermissions.AddRange(
            new RolePermission { RoleId = admin.Id, TableName = ProcessKeys.AdministrationModule, Permissions = 15, View = "admin" },
            new RolePermission { RoleId = teacher.Id, TableName = ProcessKeys.GradesModule, Permissions = 1 },
            new RolePermission { RoleId = teacher.Id, TableName = ProcessKeys.AdministrationModule, Permissions = 2 },
            new RolePermission { RoleId = rector.Id, TableName = ProcessKeys.AdmissionDecision, Permissions = 4 });
        await db.SaveChangesAsync();

        var plain = new PermissionService(db);
        var cached = new PermissionService(db, new PermissionCache());

        foreach (var roles in new[] { new[] { "Profesor" }, new[] { "Administrador", "Profesor" }, new[] { "Rector" }, new[] { "Nadie" }, Array.Empty<string>() })
        {
            Assert.Equal(await plain.GetForRolesAsync(roles), await cached.GetForRolesAsync(roles));

            foreach (var key in new[] { ProcessKeys.AdministrationModule, ProcessKeys.GradesModule, ProcessKeys.AdmissionDecision })
            foreach (var flag in new[] { PermissionFlags.Read, PermissionFlags.Create, PermissionFlags.Update, PermissionFlags.Delete })
                Assert.Equal(await plain.HasPermissionAsync(roles, key, flag), await cached.HasPermissionAsync(roles, key, flag));
        }
    }

    [Fact]
    public async Task Invalidate_makes_the_next_check_see_the_change()
    {
        using var db = TestDb.Create();
        var teacher = TestData.AddRole(db, "Profesor");
        var row = new RolePermission { RoleId = teacher.Id, TableName = ProcessKeys.GradesModule, Permissions = 1 };
        db.RolePermissions.Add(row);
        await db.SaveChangesAsync();
        var cache = new PermissionCache();
        var svc = new PermissionService(db, cache);

        Assert.True(await svc.HasPermissionAsync(new[] { "Profesor" }, ProcessKeys.GradesModule, PermissionFlags.Read));

        row.Permissions = 0;
        await db.SaveChangesAsync();
        cache.Invalidate();

        Assert.False(await svc.HasPermissionAsync(new[] { "Profesor" }, ProcessKeys.GradesModule, PermissionFlags.Read));
    }

    [Fact]
    public async Task Without_invalidate_the_snapshot_is_reused()
    {
        using var db = TestDb.Create();
        var teacher = TestData.AddRole(db, "Profesor");
        var row = new RolePermission { RoleId = teacher.Id, TableName = ProcessKeys.GradesModule, Permissions = 1 };
        db.RolePermissions.Add(row);
        await db.SaveChangesAsync();
        var svc = new PermissionService(db, new PermissionCache());

        Assert.True(await svc.HasPermissionAsync(new[] { "Profesor" }, ProcessKeys.GradesModule, PermissionFlags.Read));

        row.Permissions = 0;
        await db.SaveChangesAsync();

        Assert.True(await svc.HasPermissionAsync(new[] { "Profesor" }, ProcessKeys.GradesModule, PermissionFlags.Read));
    }

    [Fact]
    public async Task GetForRoles_with_one_role_does_not_leak_the_other()
    {
        using var db = TestDb.Create();
        var admin = TestData.AddRole(db, "Administrador");
        var teacher = TestData.AddRole(db, "Profesor");
        db.RolePermissions.AddRange(
            new RolePermission { RoleId = admin.Id, TableName = ProcessKeys.AdministrationModule, Permissions = 15 },
            new RolePermission { RoleId = teacher.Id, TableName = ProcessKeys.GradesModule, Permissions = 1 });
        await db.SaveChangesAsync();

        var result = await new PermissionService(db).GetForRolesAsync(new[] { "Profesor" });

        Assert.Single(result);
        Assert.Equal(ProcessKeys.GradesModule, result[0].TableName);
    }
}

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

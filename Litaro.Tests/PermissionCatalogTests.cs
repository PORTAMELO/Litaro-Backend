using Litaro.Models;

namespace Litaro.Tests;

public class PermissionCatalogTests
{
    [Fact]
    public void Tables_keeps_the_current_22_entries()
    {
        Assert.Equal(22, PermissionCatalog.Tables.Count);
        Assert.Equal("School", PermissionCatalog.Tables[0]);
        Assert.Contains("TeacherAvailability", PermissionCatalog.Tables);
    }

    [Fact]
    public void Processes_lists_every_process_key()
    {
        Assert.Equal(14, PermissionCatalog.Processes.Count);
        Assert.Contains(ProcessKeys.EnrollmentProcess, PermissionCatalog.Processes);
        Assert.Contains(ProcessKeys.AdmissionDecision, PermissionCatalog.Processes);
    }

    [Theory]
    [InlineData("Student", "table", true)]
    [InlineData("EnrollmentProcess", "process", true)]
    [InlineData("Nope", "table", false)]
    public void KindOf_and_IsManaged(string key, string kind, bool managed)
    {
        Assert.Equal(managed, PermissionCatalog.IsManaged(key));
        if (managed) Assert.Equal(kind, PermissionCatalog.KindOf(key));
    }

    [Fact]
    public void ResolveActiveRoles_without_request_returns_all()
        => Assert.Equal(new[] { "Administrador", "Profesor" },
            PermissionCatalog.ResolveActiveRoles(new[] { "Administrador", "Profesor" }, null));

    [Fact]
    public void ResolveActiveRoles_returns_only_the_requested_role()
        => Assert.Equal(new[] { "Profesor" },
            PermissionCatalog.ResolveActiveRoles(new[] { "Administrador", "Profesor" }, "profesor"));

    [Fact]
    public void ResolveActiveRoles_maps_Acudiente_to_Padre()
        => Assert.Equal(new[] { "Padre" },
            PermissionCatalog.ResolveActiveRoles(new[] { "Padre" }, "Acudiente"));

    [Fact]
    public void ResolveActiveRoles_denies_a_role_the_user_does_not_have()
        => Assert.Empty(PermissionCatalog.ResolveActiveRoles(new[] { "Profesor" }, "Administrador"));
}

using System.Text.Json;
using Litaro.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Litaro.Tests;

public class RolePermissionEndpointsTests
{
    private const string ReadOnlyMessage = "Este permiso solo admite la opción Ver.";

    private static RolePermissionEndpoints.SaveRolePermissionRequest Request(string key, int permissions)
        => new(1, key, permissions, null);

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(8)]
    public void ValidateSave_rejects_non_read_flags_for_audit(int permissions)
    {
        var result = RolePermissionEndpoints.ValidateSave(Request("AuditModule", permissions));

        var bad = Assert.IsAssignableFrom<IValueHttpResult>(result);
        Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, ((IStatusCodeHttpResult)result!).StatusCode);

        var json = JsonSerializer.Serialize(bad.Value, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        Assert.Equal("{\"message\":\"" + ReadOnlyMessage + "\"}", json);
    }

    [Theory]
    [InlineData("AuditModule", 1)]
    [InlineData("AuditModule", 0)]
    [InlineData("Student", 15)]
    public void ValidateSave_accepts_allowed_flags(string key, int permissions)
        => Assert.Null(RolePermissionEndpoints.ValidateSave(Request(key, permissions)));

    [Fact]
    public void ValidateSave_keeps_the_existing_rejections()
    {
        Assert.IsType<BadRequest<string>>(RolePermissionEndpoints.ValidateSave(Request("Student", 16)));
        Assert.IsType<BadRequest<string>>(RolePermissionEndpoints.ValidateSave(Request("Nope", 1)));
    }

    [Fact]
    public void ToRow_fills_kind_and_allowed_flags()
    {
        var audit = RolePermissionEndpoints.ToRow("AuditModule", 1, null);
        var student = RolePermissionEndpoints.ToRow("Student", 15, "/x");

        Assert.Equal(1, audit.AllowedFlags);
        Assert.Equal("process", audit.Kind);
        Assert.Equal(15, student.AllowedFlags);
        Assert.Equal("table", student.Kind);
    }
}

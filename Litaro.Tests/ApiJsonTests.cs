using System.Text.Json;
using Litaro.Models;
using Litaro.Services;

namespace Litaro.Tests;

public class ApiJsonTests
{
    private static JsonSerializerOptions Options()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles,
        };
        ApiJson.Configure(options);
        return options;
    }

    private static User SampleUser() => new()
    {
        Id = 7,
        UserName = "ana",
        FirstName = "Ana",
        LastName = "Ruiz",
        Email = "ana@colegio.test",
        PasswordHash = "HASH-SECRETO",
        SecurityStamp = "STAMP-SECRETO",
        ConcurrencyStamp = "CONCURRENCIA",
    };

    [Fact]
    public void User_json_does_not_expose_password_or_stamps()
    {
        var json = JsonSerializer.Serialize(SampleUser(), Options());

        Assert.DoesNotContain("passwordHash", json);
        Assert.DoesNotContain("securityStamp", json);
        Assert.DoesNotContain("concurrencyStamp", json);
        Assert.DoesNotContain("SECRETO", json);
    }

    [Fact]
    public void User_json_keeps_the_rest_of_the_fields()
    {
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(SampleUser(), Options()));
        var root = doc.RootElement;

        Assert.Equal(7, root.GetProperty("id").GetInt32());
        Assert.Equal("Ana", root.GetProperty("firstName").GetString());
        Assert.Equal("Ruiz", root.GetProperty("lastName").GetString());
        Assert.Equal("ana@colegio.test", root.GetProperty("email").GetString());
        Assert.Equal("ana", root.GetProperty("userName").GetString());
    }

    [Fact]
    public void Nested_user_inside_a_student_is_also_cleaned()
    {
        var student = new Student { StudentId = 7, StudentCode = "S-1", User = SampleUser() };

        var json = JsonSerializer.Serialize(new { records = new[] { student } }, Options());

        Assert.DoesNotContain("SECRETO", json);
        Assert.Contains("\"firstName\":\"Ana\"", json);
    }
}

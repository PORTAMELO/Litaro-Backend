using Litaro.Services;

namespace Litaro.Tests;

public class NotificationTemplateTests
{
    [Fact]
    public void Render_replaces_every_placeholder()
    {
        var text = NotificationTemplate.Render(
            "{estudiante} ({curso}) no asistió a {materia} a las {hora}.",
            new Dictionary<string, string>
            {
                ["estudiante"] = "Ana Ruiz",
                ["curso"] = "1001",
                ["materia"] = "Física",
                ["hora"] = "9:00",
            });

        Assert.Equal("Ana Ruiz (1001) no asistió a Física a las 9:00.", text);
    }

    [Fact]
    public void Render_removes_placeholders_without_value()
        => Assert.Equal("Ana  llegó tarde.", NotificationTemplate.Render("Ana {apellido} llegó tarde.", new Dictionary<string, string>()));

    [Fact]
    public void Render_truncates_to_500_characters()
        => Assert.Equal(500, NotificationTemplate.Render("{x}", new Dictionary<string, string> { ["x"] = new string('a', 900) }).Length);
}

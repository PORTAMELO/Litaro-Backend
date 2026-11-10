using Litaro.Services;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Tests;

public class NotificationSettingsServiceTests
{
    [Fact]
    public async Task Update_replaces_recipients_and_template()
    {
        using var db = TestDb.Create();
        var roleA = TestData.AddRole(db, "A");
        var roleB = TestData.AddRole(db, "B");
        var pedro = TestData.AddUser(db, "Pedro", "Cano");
        var type = TestData.AddNotificationType(db, "T1", "Viejo", roleA);
        var svc = new NotificationSettingsService(db);

        await svc.UpdateAsync(type.NotificationTypeId,
            new UpdateNotificationTypeRequest("Nuevo {estudiante}", true, [roleB.Id], [pedro.Id]));

        var saved = (await svc.ListAsync()).Single();

        Assert.Equal("Nuevo {estudiante}", saved.Template);
        Assert.Equal(new[] { "B" }, saved.Roles.Select(r => r.Name));
        Assert.Equal(new[] { "Pedro Cano" }, saved.Users.Select(u => u.Name));
        Assert.Equal(2, await db.NotificationTypeRecipients.CountAsync());
    }

    [Fact]
    public async Task Blank_template_is_rejected()
    {
        using var db = TestDb.Create();
        var type = TestData.AddNotificationType(db, "T1", "Viejo");

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new NotificationSettingsService(db).UpdateAsync(type.NotificationTypeId,
                new UpdateNotificationTypeRequest("  ", true, [], [])));

        Assert.Equal("El mensaje es obligatorio.", error.Message);
    }

    [Fact]
    public async Task Unknown_recipient_is_rejected()
    {
        using var db = TestDb.Create();
        var type = TestData.AddNotificationType(db, "T1", "Viejo");

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new NotificationSettingsService(db).UpdateAsync(type.NotificationTypeId,
                new UpdateNotificationTypeRequest("Texto", true, [9999], [])));

        Assert.Equal("Alguno de los destinatarios no existe.", error.Message);
    }

    [Fact]
    public async Task Unknown_type_is_rejected()
    {
        using var db = TestDb.Create();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new NotificationSettingsService(db).UpdateAsync(77,
                new UpdateNotificationTypeRequest("Texto", true, [], [])));

        Assert.Equal("No existe el tipo de notificación.", error.Message);
    }

    [Fact]
    public async Task User_search_needs_two_characters()
    {
        using var db = TestDb.Create();
        TestData.AddUser(db, "Pedro", "Cano");
        var svc = new NotificationSettingsService(db);

        Assert.Empty(await svc.SearchUsersAsync("p"));
        Assert.Single(await svc.SearchUsersAsync("ped"));
        Assert.Single(await svc.SearchUsersAsync("CANO"));
    }
}

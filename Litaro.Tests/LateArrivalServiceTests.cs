using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Tests;

public class LateArrivalServiceTests
{
    private static readonly DateOnly[] Days =
    [
        new(2026, 11, 10), new(2026, 11, 11), new(2026, 11, 12), new(2026, 11, 13), new(2026, 11, 16),
    ];

    private static async Task<(TestSchool School, TestServices Services)> CreateAsync()
    {
        var db = TestDb.Create();
        var school = await TestSchool.CreateAsync(db);
        return (school, TestServices.Create(db));
    }

    private static async Task RegisterAsync(TestSchool school, TestServices services, int count)
    {
        foreach (var day in Days.Take(count))
            await services.LateArrivals.RegisterAsync(school.Ana.EnrollmentId, day, school.Marta.Id, day);
    }

    private static Task<int> CountAsync(TestSchool school, string code) =>
        school.Db.Notifications.CountAsync(n => n.NotificationType.Code == code);

    [Fact]
    public async Task Third_arrival_in_the_period_notifies_commitment()
    {
        var (school, services) = await CreateAsync();

        await RegisterAsync(school, services, 3);

        var notification = await school.Db.Notifications.SingleAsync();

        Assert.Equal(
            "Ana Ruiz (1001) completó 3 llegadas tarde en el periodo 4. Genere el compromiso para firma del acudiente.",
            notification.Message);
        Assert.Equal(school.Ana.StudentId, notification.StudentId);
        Assert.Equal(school.Campus.CampusId, notification.CampusId);
    }

    [Fact]
    public async Task Two_arrivals_notify_nothing()
    {
        var (school, services) = await CreateAsync();

        await RegisterAsync(school, services, 2);

        Assert.Equal(0, await school.Db.Notifications.CountAsync());
    }

    [Fact]
    public async Task Fourth_arrival_notifies_nothing_and_fifth_notifies_citation()
    {
        var (school, services) = await CreateAsync();

        await RegisterAsync(school, services, 4);
        Assert.Equal(1, await school.Db.Notifications.CountAsync());

        await services.LateArrivals.RegisterAsync(school.Ana.EnrollmentId, Days[4], school.Marta.Id, Days[4]);

        Assert.Equal(1, await CountAsync(school, NotificationCodes.AttLateCommitment));
        Assert.Equal(1, await CountAsync(school, NotificationCodes.AttLateCitation));
    }

    [Fact]
    public async Task Same_day_twice_is_rejected()
    {
        var (school, services) = await CreateAsync();
        await RegisterAsync(school, services, 1);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            services.LateArrivals.RegisterAsync(school.Ana.EnrollmentId, Days[0], school.Marta.Id, Days[0]));

        Assert.Equal("Ya se registró la llegada tarde de hoy para este estudiante.", error.Message);
    }

    [Fact]
    public async Task Undo_and_register_again_does_not_notify_twice()
    {
        var (school, services) = await CreateAsync();
        await RegisterAsync(school, services, 3);
        var third = await school.Db.LateArrivals.SingleAsync(l => l.Date == Days[2]);

        Assert.True(await services.LateArrivals.DeleteAsync(third.LateArrivalId));
        await services.LateArrivals.RegisterAsync(school.Ana.EnrollmentId, Days[3], school.Marta.Id, Days[3]);

        Assert.Equal(1, await CountAsync(school, NotificationCodes.AttLateCommitment));
    }

    [Fact]
    public async Task A_date_outside_every_period_is_rejected()
    {
        var (school, services) = await CreateAsync();
        var december = new DateOnly(2026, 12, 20);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            services.LateArrivals.RegisterAsync(school.Ana.EnrollmentId, december, school.Marta.Id, december));

        Assert.Equal("La fecha no pertenece a ningún periodo académico.", error.Message);
    }

    [Fact]
    public async Task A_future_date_is_rejected()
    {
        var (school, services) = await CreateAsync();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            services.LateArrivals.RegisterAsync(school.Ana.EnrollmentId, Days[1], school.Marta.Id, Days[0]));

        Assert.Equal("No se puede registrar una llegada en una fecha futura.", error.Message);
    }

    [Fact]
    public async Task An_inactive_enrollment_is_rejected()
    {
        var (school, services) = await CreateAsync();
        school.Ana.Status = "RETIRED";
        await school.Db.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            services.LateArrivals.RegisterAsync(school.Ana.EnrollmentId, Days[0], school.Marta.Id, Days[0]));

        Assert.Equal("El estudiante no tiene matrícula activa.", error.Message);
    }

    [Fact]
    public async Task List_shows_the_period_count()
    {
        var (school, services) = await CreateAsync();
        await RegisterAsync(school, services, 2);

        var rows = await services.LateArrivals.ListAsync(Days[1]);

        Assert.Single(rows);
        Assert.Equal("Ana Ruiz", rows[0].StudentName);
        Assert.Equal("1001", rows[0].ClassroomName);
        Assert.Equal(2, rows[0].PeriodCount);
    }

    [Fact]
    public async Task Search_needs_two_characters_and_matches_name_code_or_document()
    {
        var (school, services) = await CreateAsync();
        var ana = await school.Db.Students.Include(s => s.User).SingleAsync(s => s.StudentId == school.Ana.StudentId);

        Assert.Empty(await services.LateArrivals.SearchStudentsAsync("a"));
        Assert.Contains(await services.LateArrivals.SearchStudentsAsync("ruiz"), r => r.EnrollmentId == school.Ana.EnrollmentId);
        Assert.Single(await services.LateArrivals.SearchStudentsAsync(ana.StudentCode));
        Assert.Single(await services.LateArrivals.SearchStudentsAsync(ana.User.DocumentNumber));
    }
}

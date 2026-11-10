using Litaro.Models;
using Litaro.Services;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Tests;

public class AttendanceNightlyServiceTests
{
    private static readonly DateOnly[] FiveTuesdays =
    [
        new(2026, 10, 13), new(2026, 10, 20), new(2026, 10, 27), new(2026, 11, 3), new(2026, 11, 10),
    ];

    private sealed class Scene
    {
        public required TestSchool School { get; init; }
        public required TestServices Services { get; init; }
        public RollAccess Admin => new(School.Marta.Id, false, true);

        public Task SaveAsync(DateOnly date, TimeOnly start, params Enrollment[] absent) =>
            Services.Roll.SaveSheetAsync(
                Admin,
                new SaveSheetRequest(School.C1001.ClassroomId, date, start, absent.Select(e => e.EnrollmentId).ToList(), []),
                date);

        public Task<int> CountAsync(string code) =>
            School.Db.Notifications.CountAsync(n => n.NotificationType.Code == code);
    }

    private static async Task<Scene> CreateAsync()
    {
        var db = TestDb.Create();
        var school = await TestSchool.CreateAsync(db);
        return new Scene { School = school, Services = TestServices.Create(db) };
    }

    [Fact]
    public async Task Five_school_days_fully_absent_notify_once()
    {
        var scene = await CreateAsync();
        foreach (var day in FiveTuesdays)
            await scene.SaveAsync(day, TestSchool.PhysicsStart, scene.School.Beto);

        await scene.Services.Nightly.RunAsync(new DateOnly(2026, 11, 11));
        await scene.Services.Nightly.RunAsync(new DateOnly(2026, 11, 12));

        var notification = await scene.School.Db.Notifications
            .SingleAsync(n => n.NotificationType.Code == NotificationCodes.AttWeekAbsence);

        Assert.Equal(
            "Beto Paz (1001) completó 5 días seguidos de inasistencia injustificada. Contacte a la familia.",
            notification.Message);
        Assert.Equal(scene.School.Beto.StudentId, notification.StudentId);
    }

    [Fact]
    public async Task Four_days_do_not_notify()
    {
        var scene = await CreateAsync();
        foreach (var day in FiveTuesdays.Take(4))
            await scene.SaveAsync(day, TestSchool.PhysicsStart, scene.School.Beto);

        await scene.Services.Nightly.RunAsync(new DateOnly(2026, 11, 11));

        Assert.Equal(0, await scene.CountAsync(NotificationCodes.AttWeekAbsence));
    }

    [Fact]
    public async Task A_justified_day_breaks_the_streak()
    {
        var scene = await CreateAsync();
        foreach (var day in FiveTuesdays)
            await scene.SaveAsync(day, TestSchool.PhysicsStart, scene.School.Beto);

        var justified = FiveTuesdays[2];
        await scene.Services.Excuses.CreateAsync(
            new SaveExcuseRequest(
                scene.School.Beto.StudentId,
                justified.ToDateTime(new TimeOnly(6, 0)),
                justified.ToDateTime(new TimeOnly(12, 30)),
                null),
            scene.School.Marta.Id,
            justified);

        await scene.Services.Nightly.RunAsync(new DateOnly(2026, 11, 11));

        Assert.Equal(0, await scene.CountAsync(NotificationCodes.AttWeekAbsence));
    }

    [Fact]
    public async Task Attending_one_class_of_a_day_breaks_the_streak()
    {
        var scene = await CreateAsync();
        foreach (var day in FiveTuesdays)
            await scene.SaveAsync(day, TestSchool.PhysicsStart, scene.School.Beto);
        await scene.SaveAsync(FiveTuesdays[2], TestSchool.ChemistryStart);

        await scene.Services.Nightly.RunAsync(new DateOnly(2026, 11, 11));

        Assert.Equal(0, await scene.CountAsync(NotificationCodes.AttWeekAbsence));
    }

    [Fact]
    public async Task A_whole_closed_period_absent_notifies_once()
    {
        var scene = await CreateAsync();
        await scene.SaveAsync(new DateOnly(2026, 7, 7), TestSchool.PhysicsStart, scene.School.Beto, scene.School.Caro);
        await scene.SaveAsync(new DateOnly(2026, 7, 14), TestSchool.PhysicsStart, scene.School.Beto);

        await scene.Services.Nightly.RunAsync(scene.School.Tuesday);
        await scene.Services.Nightly.RunAsync(scene.School.Tuesday.AddDays(1));

        var notification = await scene.School.Db.Notifications
            .SingleAsync(n => n.NotificationType.Code == NotificationCodes.AttPeriodAbsence);

        Assert.Equal(scene.School.Beto.StudentId, notification.StudentId);
        Assert.StartsWith("Beto Paz (1001) no asistió durante todo el periodo 3 sin justificación.", notification.Message);
    }

    [Fact]
    public async Task An_open_period_does_not_notify_period_absence()
    {
        var scene = await CreateAsync();
        await scene.SaveAsync(FiveTuesdays[0], TestSchool.PhysicsStart, scene.School.Beto);
        await scene.SaveAsync(FiveTuesdays[1], TestSchool.PhysicsStart, scene.School.Beto);

        await scene.Services.Nightly.RunAsync(new DateOnly(2026, 11, 11));

        Assert.Equal(0, await scene.CountAsync(NotificationCodes.AttPeriodAbsence));
    }

    [Fact]
    public async Task Closed_periods_are_frozen_once()
    {
        var scene = await CreateAsync();

        await scene.Services.Nightly.RunAsync(scene.School.Tuesday);

        var frozen = await scene.School.Db.PeriodPlannedHours
            .Where(p => p.PeriodId == scene.School.ClosedPeriod.PeriodId)
            .ToListAsync();
        var physics = frozen.Single(p =>
            p.ClassroomId == scene.School.C1001.ClassroomId && p.SubjectId == scene.School.Physics.SubjectId);

        Assert.Equal(5, frozen.Count);
        Assert.Equal(12m, physics.PlannedHours);
        Assert.Equal(0, await scene.School.Db.PeriodPlannedHours.CountAsync(p => p.PeriodId == scene.School.Period.PeriodId));

        scene.School.Db.Schedules.Remove(scene.School.PhysicsBlock);
        await scene.School.Db.SaveChangesAsync();
        await scene.Services.Nightly.RunAsync(scene.School.Tuesday.AddDays(1));

        Assert.Equal(5, await scene.School.Db.PeriodPlannedHours.CountAsync());
        Assert.Equal(12m, (await scene.School.Db.PeriodPlannedHours.SingleAsync(p =>
            p.PeriodPlannedHoursId == physics.PeriodPlannedHoursId)).PlannedHours);
    }

    [Fact]
    public async Task The_nightly_run_recalculates_the_loss_flag()
    {
        var scene = await CreateAsync();
        await scene.SaveAsync(FiveTuesdays[3], TestSchool.PhysicsStart, scene.School.Beto);
        await scene.SaveAsync(FiveTuesdays[4], TestSchool.PhysicsStart, scene.School.Beto);
        scene.School.Db.AbsenceLosses.RemoveRange(scene.School.Db.AbsenceLosses);
        await scene.School.Db.SaveChangesAsync();

        await scene.Services.Nightly.RunAsync(new DateOnly(2026, 11, 11));

        Assert.Equal(1, await scene.School.Db.AbsenceLosses.CountAsync());
    }
}

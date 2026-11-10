using Litaro.Services;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Tests;

public class AttendanceSettingsServiceTests
{
    private static AttendanceSettings Sample(int commitment = 2, int citation = 4) =>
        new(25m, 1.5m, 3, commitment, citation, 6, 22, ["Primaria"]);

    [Fact]
    public async Task Defaults_apply_on_an_empty_database()
    {
        using var db = TestDb.Create();

        var settings = await new AttendanceSettingsService(db).GetAsync();

        Assert.Equal(20m, settings.LossPercent);
        Assert.Equal(1.0m, settings.LossGrade);
        Assert.Equal(2, settings.ExcuseDeadlineDays);
        Assert.Equal(3, settings.LateCommitmentCount);
        Assert.Equal(5, settings.LateCitationCount);
        Assert.Equal(5, settings.AbsenceAlertDays);
        Assert.Equal(23, settings.NightlyHour);
        Assert.Empty(settings.DailyLevels);
    }

    [Fact]
    public async Task Save_then_get_round_trips()
    {
        using var db = TestDb.Create();
        var svc = new AttendanceSettingsService(db);

        await svc.SaveAsync(Sample());
        var settings = await svc.GetAsync();

        Assert.Equal(25m, settings.LossPercent);
        Assert.Equal(1.5m, settings.LossGrade);
        Assert.Equal(3, settings.ExcuseDeadlineDays);
        Assert.Equal(2, settings.LateCommitmentCount);
        Assert.Equal(4, settings.LateCitationCount);
        Assert.Equal(6, settings.AbsenceAlertDays);
        Assert.Equal(22, settings.NightlyHour);
        Assert.Equal(new[] { "Primaria" }, settings.DailyLevels);
        Assert.Equal(1, await db.Characteristics.CountAsync(c => c.Name == AttendanceSettingsService.CharacteristicName));
    }

    [Fact]
    public async Task Saving_twice_does_not_duplicate_rows()
    {
        using var db = TestDb.Create();
        var svc = new AttendanceSettingsService(db);

        await svc.SaveAsync(Sample());
        await svc.SaveAsync(Sample());

        Assert.Equal(8, await db.CharacteristicDetails.CountAsync());
    }

    [Fact]
    public async Task Citation_must_exceed_commitment()
    {
        using var db = TestDb.Create();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new AttendanceSettingsService(db).SaveAsync(Sample(commitment: 5, citation: 5)));

        Assert.Equal("Las tardanzas para citación deben ser más que las de compromiso.", error.Message);
    }

    [Fact]
    public async Task Loss_percent_must_be_in_range()
    {
        using var db = TestDb.Create();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new AttendanceSettingsService(db).SaveAsync(Sample() with { LossPercent = 0m }));

        Assert.Equal("El porcentaje de pérdida debe estar entre 1 y 100.", error.Message);
    }

    [Fact]
    public async Task Grade_levels_come_from_the_grades()
    {
        using var db = TestDb.Create();
        db.Grades.AddRange(
            new Litaro.Models.Grade { Name = "Tercero", OrderNum = 3, Level = "Primaria" },
            new Litaro.Models.Grade { Name = "Cuarto", OrderNum = 4, Level = "Primaria" },
            new Litaro.Models.Grade { Name = "Décimo", OrderNum = 10, Level = "Media" });
        await db.SaveChangesAsync();

        Assert.Equal(new[] { "Media", "Primaria" }, await new AttendanceSettingsService(db).GetGradeLevelsAsync());
    }
}

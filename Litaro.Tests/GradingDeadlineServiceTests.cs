using Litaro.Models;
using Litaro.Services;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Tests;

public class GradingDeadlineServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 10, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Save_and_get_planner_and_period_locks()
    {
        var school = await TestSchool.CreateAsync(TestDb.Create());
        var service = new GradingDeadlineService(school.Db);
        var planner = new DateTime(2026, 2, 6, 22, 0, 0, DateTimeKind.Utc);
        var lockAt = new DateTime(2026, 10, 19, 22, 0, 0, DateTimeKind.Utc);

        var initial = await service.GetAsync(TestSchool.YearId);
        var periods = initial.Periods
            .Select(p => p with { DesignLockAt = p.PeriodId == school.Period.PeriodId ? lockAt : null })
            .ToList();

        await service.SaveAsync(initial with { PlannerDueAt = planner, Periods = periods }, school.Marta.Id, Now);

        var saved = await service.GetAsync(TestSchool.YearId);

        Assert.Equal(planner, saved.PlannerDueAt);
        Assert.Equal(lockAt, saved.Periods.Single(p => p.PeriodId == school.Period.PeriodId).DesignLockAt);
        Assert.Null(saved.Periods.Single(p => p.PeriodId == school.ClosedPeriod.PeriodId).DesignLockAt);
        Assert.Equal(planner, await service.PlannerDueAsync(TestSchool.YearId));
        Assert.Equal(lockAt, await service.DesignLockAsync(school.Period.PeriodId));

        await service.SaveAsync(saved with { PlannerDueAt = null }, school.Marta.Id, Now);

        Assert.Null(await service.PlannerDueAsync(TestSchool.YearId));
        Assert.Equal(1, await school.Db.GradingDeadlines.CountAsync());
    }

    [Fact]
    public async Task Rejects_period_of_another_year()
    {
        var school = await TestSchool.CreateAsync(TestDb.Create());
        school.Db.AcademicYears.Add(new AcademicYear { YearId = 2025, StartDate = Now.AddYears(-1), EndDate = Now.AddYears(-1).AddMonths(10), Active = false });
        var foreign = new AcademicPeriod { PeriodNumber = 1, StartDate = Now.AddYears(-1), EndDate = Now.AddYears(-1).AddMonths(3), YearId = 2025 };
        school.Db.AcademicPeriods.Add(foreign);
        await school.Db.SaveChangesAsync();

        var service = new GradingDeadlineService(school.Db);
        var request = new DeadlinesDto(TestSchool.YearId, null, [new PeriodDeadlineDto(foreign.PeriodId, 1, Now)]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync(request, school.Marta.Id, Now));
    }
}

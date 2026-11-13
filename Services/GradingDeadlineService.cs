using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services;

public record PeriodDeadlineDto(short PeriodId, byte PeriodNumber, DateTime? DesignLockAt);

public record DeadlinesDto(short YearId, DateTime? PlannerDueAt, List<PeriodDeadlineDto> Periods);

public class GradingDeadlineService(AppDbContext db)
{
    public async Task<DeadlinesDto> GetAsync(short yearId)
    {
        var periods = await db.AcademicPeriods
            .AsNoTracking()
            .Where(p => p.YearId == yearId)
            .OrderBy(p => p.PeriodNumber)
            .Select(p => new { p.PeriodId, p.PeriodNumber })
            .ToListAsync();

        var rows = await db.GradingDeadlines
            .AsNoTracking()
            .Where(d => d.YearId == yearId)
            .ToListAsync();

        var planner = rows.FirstOrDefault(d => d.Kind == DeadlineKinds.Planner)?.DueAt;
        var locks = rows
            .Where(d => d.Kind == DeadlineKinds.DesignLock && d.PeriodId is not null)
            .ToDictionary(d => d.PeriodId!.Value, d => d.DueAt);

        return new DeadlinesDto(
            yearId,
            Utc(planner),
            periods.Select(p => new PeriodDeadlineDto(
                p.PeriodId,
                p.PeriodNumber,
                locks.TryGetValue(p.PeriodId, out var at) ? Utc(at) : null)).ToList());
    }

    public async Task<DeadlinesDto> SaveAsync(DeadlinesDto request, int userId, DateTime nowUtc)
    {
        if (!await db.AcademicYears.AnyAsync(y => y.YearId == request.YearId))
            throw new InvalidOperationException("El año académico no existe.");

        var periodIds = await db.AcademicPeriods
            .Where(p => p.YearId == request.YearId)
            .Select(p => p.PeriodId)
            .ToListAsync();

        if (request.Periods.Any(p => !periodIds.Contains(p.PeriodId)))
            throw new InvalidOperationException("El periodo no pertenece al año académico.");

        var rows = await db.GradingDeadlines
            .Where(d => d.YearId == request.YearId)
            .ToListAsync();

        Apply(rows, request.YearId, DeadlineKinds.Planner, null, request.PlannerDueAt, userId, nowUtc);

        foreach (var period in request.Periods)
            Apply(rows, request.YearId, DeadlineKinds.DesignLock, period.PeriodId, period.DesignLockAt, userId, nowUtc);

        await db.SaveChangesAsync();

        return await GetAsync(request.YearId);
    }

    public Task<AcademicYear?> CurrentYearAsync(DateTime nowUtc) =>
        db.AcademicYears
            .AsNoTracking()
            .Where(y => y.Active)
            .OrderByDescending(y => y.StartDate <= nowUtc && y.EndDate >= nowUtc)
            .ThenByDescending(y => y.YearId)
            .FirstOrDefaultAsync();

    public async Task<DateTime?> PlannerDueAsync(short yearId) =>
        Utc(await db.GradingDeadlines
            .AsNoTracking()
            .Where(d => d.YearId == yearId && d.Kind == DeadlineKinds.Planner)
            .Select(d => (DateTime?)d.DueAt)
            .FirstOrDefaultAsync());

    public async Task<DateTime?> DesignLockAsync(short periodId) =>
        Utc(await db.GradingDeadlines
            .AsNoTracking()
            .Where(d => d.PeriodId == periodId && d.Kind == DeadlineKinds.DesignLock)
            .Select(d => (DateTime?)d.DueAt)
            .FirstOrDefaultAsync());

    private void Apply(List<GradingDeadline> rows, short yearId, string kind, short? periodId, DateTime? dueAt, int userId, DateTime nowUtc)
    {
        var row = rows.FirstOrDefault(d => d.Kind == kind && d.PeriodId == periodId);

        if (dueAt is null)
        {
            if (row is not null)
                db.GradingDeadlines.Remove(row);
            return;
        }

        if (row is null)
        {
            row = new GradingDeadline { YearId = yearId, PeriodId = periodId, Kind = kind };
            db.GradingDeadlines.Add(row);
        }

        row.DueAt = dueAt.Value.ToUniversalTime();
        row.UpdatedById = userId;
        row.UpdatedAt = nowUtc;
    }

    private static DateTime? Utc(DateTime? value) =>
        value is { } date ? DateTime.SpecifyKind(date, DateTimeKind.Utc) : null;
}

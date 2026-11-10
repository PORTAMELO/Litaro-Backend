using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services;

public class AttendanceNightlyWorker(IServiceScopeFactory scopes, ILogger<AttendanceNightlyWorker> logger) : BackgroundService
{
    private const int LockKey = 4202;

    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan ColombiaOffset = TimeSpan.FromHours(-5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(StartupDelay, stoppingToken);

            using var timer = new PeriodicTimer(CheckInterval);

            do
            {
                await TickAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            return;
        }
    }

    private async Task TickAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var settings = scope.ServiceProvider.GetRequiredService<AttendanceSettingsService>();

            var hour = (await settings.GetAsync()).NightlyHour;
            var nowLocal = DateTime.UtcNow.Add(ColombiaOffset);
            var run = await db.JobRuns.FirstOrDefaultAsync(j => j.JobName == AttendanceNightlyService.JobName, stoppingToken);

            if (!AttendanceRules.ShouldRunNightly(nowLocal, run?.LastRunDate, hour))
                return;

            var locks = db.Database.IsNpgsql();

            if (locks)
            {
                await db.Database.OpenConnectionAsync(stoppingToken);

                var acquired = await db.Database
                    .SqlQuery<bool>($"SELECT pg_try_advisory_lock({LockKey}) AS \"Value\"")
                    .SingleAsync(stoppingToken);

                if (!acquired)
                    return;
            }

            try
            {
                var runDate = AttendanceRules.NightlyRunDate(nowLocal, hour);

                await scope.ServiceProvider.GetRequiredService<AttendanceNightlyService>().RunAsync(runDate);

                if (run is null)
                {
                    run = new JobRun { JobName = AttendanceNightlyService.JobName };
                    db.JobRuns.Add(run);
                }

                run.LastRunDate = runDate;
                await db.SaveChangesAsync(stoppingToken);
            }
            finally
            {
                if (locks)
                    await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_unlock({LockKey})", CancellationToken.None);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "El proceso nocturno de asistencia falló.");
        }
    }
}

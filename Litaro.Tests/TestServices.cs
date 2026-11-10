using Litaro.Data;
using Litaro.Services;

namespace Litaro.Tests;

public sealed class TestServices
{
    public required AttendanceSettingsService Settings { get; init; }
    public required NotificationService Notifications { get; init; }
    public required LateArrivalService LateArrivals { get; init; }
    public required AttendanceRollService Roll { get; init; }
    public required AttendanceExcuseService Excuses { get; init; }
    public required AttendanceCalculationService Calculation { get; init; }
    public required AttendanceNightlyService Nightly { get; init; }

    public static TestServices Create(AppDbContext db)
    {
        var settings = new AttendanceSettingsService(db);
        var notifications = new NotificationService(db);
        var lateArrivals = new LateArrivalService(db, settings, notifications);

        var calculation = new AttendanceCalculationService(db, settings);
        var excuses = new AttendanceExcuseService(db, settings, calculation);
        var roll = new AttendanceRollService(db, settings, notifications, lateArrivals, excuses, calculation);
        var nightly = new AttendanceNightlyService(db, settings, notifications, calculation);

        return new TestServices
        {
            Settings = settings,
            Notifications = notifications,
            LateArrivals = lateArrivals,
            Roll = roll,
            Excuses = excuses,
            Calculation = calculation,
            Nightly = nightly,
        };
    }
}

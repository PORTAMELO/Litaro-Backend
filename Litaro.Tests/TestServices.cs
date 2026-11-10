using Litaro.Data;
using Litaro.Services;

namespace Litaro.Tests;

public sealed class TestServices
{
    public required AttendanceSettingsService Settings { get; init; }
    public required NotificationService Notifications { get; init; }
    public required LateArrivalService LateArrivals { get; init; }

    public static TestServices Create(AppDbContext db)
    {
        var settings = new AttendanceSettingsService(db);
        var notifications = new NotificationService(db);
        var lateArrivals = new LateArrivalService(db, settings, notifications);

        return new TestServices
        {
            Settings = settings,
            Notifications = notifications,
            LateArrivals = lateArrivals,
        };
    }
}

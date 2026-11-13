using Litaro.Data;
using Litaro.Models;
using Litaro.Services;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Tests;

public class SimatNotificationTests
{
    private const string ApprovedTemplate =
        "El Rector aprobó la admisión de {aspirante} ({documento}) a {grado} para {anio}. Regístrelo como estudiante nuevo en el SIMAT.";

    private const string DeclinedTemplate =
        "La familia de {estudiante} ({documento}) no continuará en {anio}. Registre la novedad en el SIMAT.";

    private static AdmissionService Admissions(AppDbContext db) =>
        new(db, new EnrollmentPeriodService(db), new NotificationService(db));

    private static PreEnrollmentService PreEnrollments(AppDbContext db) =>
        new(db, new EnrollmentPeriodService(db), new NotificationService(db));

    private static async Task<(AdmissionRequest Admission, User Rector)> SeedAdmissionAsync(AppDbContext db)
    {
        TestData.AddNotificationType(db, NotificationCodes.SimatAdmissionApproved, ApprovedTemplate);
        var rector = TestData.AddUser(db, "Rita", "Mora");
        var grade = new Grade { Name = "Sexto", OrderNum = 6, Level = "Secundaria" };
        db.Grades.Add(grade);
        db.AcademicYears.Add(new AcademicYear { YearId = 2027, Active = true });
        await db.SaveChangesAsync();

        var admission = new AdmissionRequest
        {
            YearId = 2027,
            GradeId = grade.GradeId,
            FirstName = "Sara",
            LastName = "Vélez",
            DocumentType = "TI",
            DocumentNumber = "1061000001",
            BirthDate = new DateOnly(2015, 3, 1),
            GuardianName = "Luz Vélez",
            GuardianPhone = "3001112233",
            CreatedById = rector.Id,
        };
        db.AdmissionRequests.Add(admission);
        await db.SaveChangesAsync();

        return (admission, rector);
    }

    private static async Task<PreEnrollment> SeedPreEnrollmentAsync(AppDbContext db)
    {
        TestData.AddNotificationType(db, NotificationCodes.SimatCupoDeclined, DeclinedTemplate);
        var user = TestData.AddUser(db, "Juan", "Rey");
        var grade = new Grade { Name = "Séptimo", OrderNum = 7, Level = "Secundaria" };
        db.Grades.Add(grade);
        db.AcademicYears.Add(new AcademicYear { YearId = 2027, Active = true });
        db.Students.Add(new Student { StudentId = user.Id, StudentCode = "INPS-1", Gender = 'M' });
        await db.SaveChangesAsync();

        var item = new PreEnrollment { YearId = 2027, StudentId = user.Id, GradeId = grade.GradeId };
        db.PreEnrollments.Add(item);
        await db.SaveChangesAsync();

        return item;
    }

    [Fact]
    public async Task Approving_an_admission_queues_the_simat_notification()
    {
        using var db = TestDb.Create();
        var (admission, rector) = await SeedAdmissionAsync(db);

        await Admissions(db).DecideAsync(admission.AdmissionRequestId, "APPROVED", null, rector.Id);

        var notification = await db.Notifications.SingleAsync();

        Assert.Equal(
            "El Rector aprobó la admisión de Sara Vélez (TI 1061000001) a Sexto para 2027. Regístrelo como estudiante nuevo en el SIMAT.",
            notification.Message);
        Assert.Equal("AdmissionRequest", notification.SourceTable);
        Assert.Equal(admission.AdmissionRequestId, notification.SourceId);
        Assert.Equal((short)2027, notification.YearId);
    }

    [Fact]
    public async Task Rejecting_an_admission_queues_nothing()
    {
        using var db = TestDb.Create();
        var (admission, rector) = await SeedAdmissionAsync(db);

        await Admissions(db).DecideAsync(admission.AdmissionRequestId, "REJECTED", "Sin cupo", rector.Id);

        Assert.Equal(0, await db.Notifications.CountAsync());
    }

    [Fact]
    public async Task Declining_a_pre_enrollment_queues_cupo_declined()
    {
        using var db = TestDb.Create();
        var item = await SeedPreEnrollmentAsync(db);

        await PreEnrollments(db).RespondAsync(item.PreEnrollmentId, false, null);

        var notification = await db.Notifications.Include(n => n.NotificationType).SingleAsync();

        Assert.Equal(NotificationCodes.SimatCupoDeclined, notification.NotificationType.Code);
        Assert.Equal(item.StudentId, notification.StudentId);
        Assert.Equal((short)2027, notification.YearId);
        Assert.StartsWith("La familia de Juan Rey (", notification.Message);
    }

    private static async Task<(PreEnrollment Item, User Second, EnrollmentPeriod Period)> SeedExpiredAsync(AppDbContext db)
    {
        var item = await SeedPreEnrollmentAsync(db);
        TestData.AddNotificationType(db, NotificationCodes.SimatCupoLost, "{estudiante} ({documento}) perdió el cupo {anio}: la familia no respondió antes del {fecha}.");
        TestData.AddNotificationType(db, NotificationCodes.SimatCupoRestored, "Se restableció el cupo {anio} de {estudiante} ({documento}). Revierta la novedad en el SIMAT.");

        var second = TestData.AddUser(db, "Eva", "Luna");
        db.Students.Add(new Student { StudentId = second.Id, StudentCode = "INPS-2", Gender = 'F' });
        db.PreEnrollments.Add(new PreEnrollment { YearId = 2027, StudentId = second.Id, GradeId = item.GradeId });

        var today = EnrollmentPeriodService.Today();
        var period = new EnrollmentPeriod
        {
            YearId = 2027,
            PreEnrollmentStart = today.AddDays(-30),
            PreEnrollmentEnd = today.AddDays(-1),
            AdmissionStart = today.AddDays(-30),
            AdmissionEnd = today.AddDays(30),
        };
        db.EnrollmentPeriods.Add(period);
        await db.SaveChangesAsync();

        await PreEnrollments(db).ListAsync(2027);

        return (item, second, period);
    }

    [Fact]
    public async Task Expiring_twice_on_the_same_day_does_not_duplicate_notifications()
    {
        using var db = TestDb.Create();
        var item = await SeedPreEnrollmentAsync(db);
        var type = TestData.AddNotificationType(db, NotificationCodes.SimatCupoLost, "{estudiante} perdió el cupo {anio}.");
        var today = EnrollmentPeriodService.Today();
        var key = $"{NotificationCodes.SimatCupoLost}:{item.PreEnrollmentId}:{today:yyyyMMdd}";

        db.EnrollmentPeriods.Add(new EnrollmentPeriod
        {
            YearId = 2027,
            PreEnrollmentStart = today.AddDays(-30),
            PreEnrollmentEnd = today.AddDays(-1),
            AdmissionStart = today.AddDays(-30),
            AdmissionEnd = today.AddDays(30),
        });
        db.Notifications.Add(new Notification
        {
            NotificationTypeId = type.NotificationTypeId,
            Message = "Otra ejecución",
            StudentId = item.StudentId,
            YearId = 2027,
            DedupKey = key,
        });
        await db.SaveChangesAsync();

        await PreEnrollments(db).ListAsync(2027);

        Assert.Equal(PreEnrollmentStatuses.Lost, (await db.PreEnrollments.SingleAsync()).Status);
        Assert.Equal("Otra ejecución", (await db.Notifications.SingleAsync()).Message);
    }

    [Fact]
    public async Task Restoring_lost_cupos_requires_a_future_deadline()
    {
        using var db = TestDb.Create();
        var (_, admin, _) = await SeedExpiredAsync(db);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => PreEnrollments(db).RestoreLostAsync(2027, admin.Id));

        Assert.Equal("Amplíe primero la fecha final de prematrícula.", error.Message);
        Assert.Equal(2, await db.PreEnrollments.CountAsync(p => p.Status == PreEnrollmentStatuses.Lost));
    }

    [Fact]
    public async Task Restoring_lost_cupos_reserves_again_and_handles_simat_notifications()
    {
        using var db = TestDb.Create();
        var (item, second, period) = await SeedExpiredAsync(db);
        var admin = TestData.AddUser(db, "Ana", "Secretaria");

        var processed = await db.Notifications.SingleAsync(n => n.StudentId == second.Id);
        processed.Status = NotificationStatuses.Done;
        processed.DoneById = admin.Id;
        processed.DoneAt = DateTime.UtcNow;
        period.PreEnrollmentEnd = EnrollmentPeriodService.Today().AddDays(10);
        await db.SaveChangesAsync();

        var result = await PreEnrollments(db).RestoreLostAsync(2027, admin.Id);

        Assert.Equal(2, result.Restored);
        Assert.Equal(1, result.SimatReverts);
        Assert.All(await db.PreEnrollments.ToListAsync(), p =>
        {
            Assert.Equal(PreEnrollmentStatuses.Reserved, p.Status);
            Assert.Null(p.ResponseNote);
        });

        var pending = await db.Notifications.SingleAsync(n => n.StudentId == item.StudentId && n.NotificationType.Code == NotificationCodes.SimatCupoLost);

        Assert.Equal(NotificationStatuses.Done, pending.Status);
        Assert.Equal(admin.Id, pending.DoneById);
        Assert.Equal("Cupo restablecido al corregir el plazo de prematrícula.", pending.DoneNote);

        var revert = await db.Notifications.Include(n => n.NotificationType)
            .SingleAsync(n => n.NotificationType.Code == NotificationCodes.SimatCupoRestored);

        Assert.Equal(second.Id, revert.StudentId);
        Assert.StartsWith("Se restableció el cupo 2027 de Eva Luna (", revert.Message);
        Assert.Equal(0, (await PreEnrollments(db).RestoreLostAsync(2027, admin.Id)).Restored);
    }

    [Fact]
    public async Task Confirming_a_pre_enrollment_queues_nothing()
    {
        using var db = TestDb.Create();
        var item = await SeedPreEnrollmentAsync(db);

        await PreEnrollments(db).RespondAsync(item.PreEnrollmentId, true, null);

        Assert.Equal(0, await db.Notifications.CountAsync());
    }
}

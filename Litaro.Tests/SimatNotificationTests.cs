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

    [Fact]
    public async Task Confirming_a_pre_enrollment_queues_nothing()
    {
        using var db = TestDb.Create();
        var item = await SeedPreEnrollmentAsync(db);

        await PreEnrollments(db).RespondAsync(item.PreEnrollmentId, true, null);

        Assert.Equal(0, await db.Notifications.CountAsync());
    }
}

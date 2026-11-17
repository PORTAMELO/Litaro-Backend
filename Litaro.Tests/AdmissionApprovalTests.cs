using Litaro.Data;
using Litaro.Models;
using Litaro.Services;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Tests;

public class AdmissionApprovalTests
{
    private static readonly DateTime Now = new(2026, 11, 17, 15, 0, 0, DateTimeKind.Utc);

    private const string IncompleteTemplate =
        "{estudiante} ({documento}) fue admitido para {anio} con documentación incompleta. Falta: {documentos}.";

    private sealed record Setup(
        AppDbContext Db,
        FakeDocumentStorage Storage,
        AdmissionService Service,
        EnrollmentDocumentService Documents,
        User Rector,
        Campus Campus,
        int GradeId,
        CharacteristicDetail BirthCertificate,
        CharacteristicDetail Vaccines);

    private static async Task<Setup> CreateAsync()
    {
        var db = TestDb.Create();
        var storage = new FakeDocumentStorage();
        var campus = EnrollmentTestData.AddCampus(db);
        TestData.AddRole(db, StudentService.RoleName);
        TestData.AddRole(db, ParentService.RoleName);
        TestData.AddNotificationType(db, NotificationCodes.SimatAdmissionApproved, "Aprobada {aspirante}.");
        TestData.AddNotificationType(db, NotificationCodes.EnrollDocsIncomplete, IncompleteTemplate);
        var types = TestData.AddDocumentTypes(db, ("Registro civil", "OBLIGATORIO"), ("Carné de vacunas", "OBLIGATORIO"), ("Fotografía", "OPCIONAL"));
        var rector = TestData.AddUser(db, "Rita", "Mora");
        var grade = new Grade { Name = "Sexto", OrderNum = 6, Level = "Secundaria" };
        db.Grades.Add(grade);
        db.AcademicYears.Add(new AcademicYear { YearId = 2027, Active = true });
        await db.SaveChangesAsync();

        return new Setup(
            db,
            storage,
            EnrollmentTestData.Admissions(db, storage),
            EnrollmentTestData.Documents(db, storage),
            rector,
            campus,
            grade.GradeId,
            types[0],
            types[1]);
    }

    private static async Task<AdmissionRequest> AddAsync(Setup setup)
    {
        var admission = EnrollmentTestData.FullAdmission(2027, setup.GradeId, setup.Campus.CampusId, setup.Rector.Id);
        setup.Db.AdmissionRequests.Add(admission);
        await setup.Db.SaveChangesAsync();
        return admission;
    }

    private static async Task UploadAsync(Setup setup, AdmissionRequest admission, CharacteristicDetail type, string fileName = "doc.pdf")
    {
        var owner = await setup.Documents.OwnerOfAdmissionAsync(admission.AdmissionRequestId);
        await setup.Documents.UploadAsync(owner, type.CharacteristicDetailId, FakeDocumentStorage.Pdf(fileName), setup.Rector.Id, Now);
    }

    [Fact]
    public async Task Approving_creates_student_guardian_and_link()
    {
        var setup = await CreateAsync();
        var admission = await AddAsync(setup);

        await setup.Service.DecideAsync(admission.AdmissionRequestId, "APPROVED", null, setup.Rector.Id);

        var saved = await setup.Db.AdmissionRequests.AsNoTracking().SingleAsync();
        var student = await setup.Db.Students.SingleAsync();
        var parent = await setup.Db.Parents.SingleAsync();
        var link = await setup.Db.ParentStudents.SingleAsync();

        Assert.Equal(AdmissionStatuses.Approved, saved.Status);
        Assert.Equal(student.StudentId, saved.StudentId);
        Assert.Equal(EnrollmentTestData.StudentNumber, (await setup.Db.Users.SingleAsync(u => u.Id == student.StudentId)).DocumentNumber);
        Assert.Equal(EnrollmentTestData.GuardianNumber, (await setup.Db.Users.SingleAsync(u => u.Id == parent.ParentId)).DocumentNumber);
        Assert.Equal(parent.ParentId, link.ParentId);
        Assert.Equal(student.StudentId, link.StudentId);
        Assert.Equal("00", student.EthnicGroup);
    }

    [Fact]
    public async Task Approving_an_incomplete_legacy_request_is_rejected_without_writes()
    {
        var setup = await CreateAsync();
        var admission = await AddAsync(setup);
        admission.GuardianEmail = null;
        admission.CampusId = null;
        await setup.Db.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            setup.Service.DecideAsync(admission.AdmissionRequestId, "APPROVED", null, setup.Rector.Id));

        Assert.Equal("Complete el formulario antes de aprobar.", error.Message);
        Assert.Equal(AdmissionStatuses.Pending, (await setup.Db.AdmissionRequests.AsNoTracking().SingleAsync()).Status);
        Assert.Empty(await setup.Db.Students.ToListAsync());
        Assert.Empty(await setup.Db.Notifications.ToListAsync());
    }

    [Fact]
    public async Task Approving_with_duplicate_numbers_writes_nothing()
    {
        var setup = await CreateAsync();
        var admission = await AddAsync(setup);
        var first = TestData.AddUser(setup.Db, "Uno", "Igual");
        var second = TestData.AddUser(setup.Db, "Dos", "Igual");
        first.DocumentNumber = EnrollmentTestData.StudentNumber;
        second.DocumentNumber = EnrollmentTestData.StudentNumber;
        await setup.Db.SaveChangesAsync();
        var usersBefore = await setup.Db.Users.CountAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            setup.Service.DecideAsync(admission.AdmissionRequestId, "APPROVED", null, setup.Rector.Id));

        Assert.Equal(usersBefore, await setup.Db.Users.CountAsync());
        Assert.Equal(AdmissionStatuses.Pending, (await setup.Db.AdmissionRequests.AsNoTracking().SingleAsync()).Status);
    }

    [Fact]
    public async Task Approving_moves_documents_to_the_student()
    {
        var setup = await CreateAsync();
        var admission = await AddAsync(setup);
        await UploadAsync(setup, admission, setup.BirthCertificate);

        await setup.Service.DecideAsync(admission.AdmissionRequestId, "APPROVED", null, setup.Rector.Id);

        var studentId = (await setup.Db.AdmissionRequests.AsNoTracking().SingleAsync()).StudentId!.Value;
        var document = await setup.Db.Documents.AsNoTracking().SingleAsync();

        Assert.Equal("Student", document.EntityTable);
        Assert.Equal(studentId, document.EntityId);
        Assert.True(document.IsCurrent);
        Assert.Single(setup.Storage.Files);
    }

    [Fact]
    public async Task Approving_over_an_existing_student_document_keeps_a_single_current_one()
    {
        var setup = await CreateAsync();
        var existing = TestData.AddUser(setup.Db, "Sara", "Vélez");
        existing.DocumentType = "TI";
        existing.DocumentNumber = EnrollmentTestData.StudentNumber;
        setup.Db.Students.Add(new Student { StudentId = existing.Id, StudentCode = "INPS-1", Gender = 'F' });
        await setup.Db.SaveChangesAsync();

        var studentOwner = await setup.Documents.OwnerOfStudentAsync(existing.Id, 2026);
        await setup.Documents.UploadAsync(studentOwner, setup.BirthCertificate.CharacteristicDetailId, FakeDocumentStorage.Pdf("viejo.pdf"), setup.Rector.Id, Now);

        var admission = await AddAsync(setup);
        await UploadAsync(setup, admission, setup.BirthCertificate, "nuevo.pdf");

        await setup.Service.DecideAsync(admission.AdmissionRequestId, "APPROVED", null, setup.Rector.Id);

        var current = await setup.Db.Documents.AsNoTracking().Where(d => d.IsCurrent).ToListAsync();
        var old = await setup.Db.Documents.AsNoTracking().SingleAsync(d => d.FileName == "viejo.pdf");

        Assert.Equal("nuevo.pdf", Assert.Single(current).FileName);
        Assert.Equal(existing.Id, current[0].EntityId);
        Assert.False(old.IsCurrent);
        Assert.NotNull(old.FileRemovedAt);
        Assert.Single(setup.Storage.Files);
        Assert.Single(await setup.Db.Students.ToListAsync());
    }

    [Fact]
    public async Task Approving_with_missing_required_documents_queues_the_alert()
    {
        var setup = await CreateAsync();
        var admission = await AddAsync(setup);
        await UploadAsync(setup, admission, setup.BirthCertificate);

        await setup.Service.DecideAsync(admission.AdmissionRequestId, "APPROVED", null, setup.Rector.Id);

        var studentId = (await setup.Db.AdmissionRequests.AsNoTracking().SingleAsync()).StudentId;
        var notification = await setup.Db.Notifications.Include(n => n.NotificationType)
            .SingleAsync(n => n.NotificationType.Code == NotificationCodes.EnrollDocsIncomplete);

        Assert.Equal("Sara Vélez (TI 1061000001) fue admitido para 2027 con documentación incompleta. Falta: Carné de vacunas.", notification.Message);
        Assert.Equal(studentId, notification.StudentId);
        Assert.Equal((short)2027, notification.YearId);
        Assert.Equal(setup.Campus.CampusId, notification.CampusId);
        Assert.Equal("AdmissionRequest", notification.SourceTable);
    }

    [Fact]
    public async Task Approving_with_every_required_document_queues_no_alert()
    {
        var setup = await CreateAsync();
        var admission = await AddAsync(setup);
        await UploadAsync(setup, admission, setup.BirthCertificate);
        await UploadAsync(setup, admission, setup.Vaccines);

        await setup.Service.DecideAsync(admission.AdmissionRequestId, "APPROVED", null, setup.Rector.Id);

        Assert.False(await setup.Db.Notifications.AnyAsync(n => n.NotificationType.Code == NotificationCodes.EnrollDocsIncomplete));
        Assert.True(await setup.Db.Notifications.AnyAsync(n => n.NotificationType.Code == NotificationCodes.SimatAdmissionApproved));
    }

    [Fact]
    public async Task Rejecting_and_waitlisting_do_not_create_people()
    {
        var setup = await CreateAsync();
        var admission = await AddAsync(setup);

        await setup.Service.DecideAsync(admission.AdmissionRequestId, "WAITLIST", null, setup.Rector.Id);
        await setup.Service.DecideAsync(admission.AdmissionRequestId, "REJECTED", "Sin cupo", setup.Rector.Id);

        Assert.Empty(await setup.Db.Students.ToListAsync());
        Assert.Empty(await setup.Db.Parents.ToListAsync());
        Assert.Null((await setup.Db.AdmissionRequests.AsNoTracking().SingleAsync()).StudentId);
    }
}

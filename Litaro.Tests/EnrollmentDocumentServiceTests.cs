using Litaro.Data;
using Litaro.Models;
using Litaro.Services;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Tests;

public class EnrollmentDocumentServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 20, 15, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly BeforeDeadline = new(2026, 11, 20);
    private static readonly DateOnly AfterDeadline = new(2026, 12, 1);

    private sealed record Setup(
        AppDbContext Db,
        FakeDocumentStorage Storage,
        EnrollmentDocumentService Service,
        User User,
        CharacteristicDetail BirthCertificate,
        CharacteristicDetail Vaccines,
        CharacteristicDetail Photo,
        CharacteristicDetail Hidden,
        int GradeId);

    private static async Task<Setup> CreateAsync()
    {
        var db = TestDb.Create();
        var storage = new FakeDocumentStorage();
        var user = TestData.AddUser(db, "Sonia", "Paz");
        var types = TestData.AddDocumentTypes(db,
            ("Registro civil", "OBLIGATORIO"),
            ("Carné de vacunas", "OBLIGATORIO"),
            ("Fotografía", "OPCIONAL"),
            ("Antiguo", "OCULTO"));

        var grade = new Grade { Name = "Sexto", OrderNum = 6, Level = "Secundaria" };
        db.Grades.Add(grade);
        db.AcademicYears.Add(new AcademicYear { YearId = 2027, Active = true });
        db.EnrollmentPeriods.Add(new EnrollmentPeriod
        {
            YearId = 2027,
            PreEnrollmentStart = new DateOnly(2026, 10, 1),
            PreEnrollmentEnd = new DateOnly(2026, 11, 15),
            AdmissionStart = new DateOnly(2026, 10, 1),
            AdmissionEnd = new DateOnly(2026, 11, 30),
        });
        await db.SaveChangesAsync();

        var service = new EnrollmentDocumentService(db, new DocumentService(db, storage));
        return new Setup(db, storage, service, user, types[0], types[1], types[2], types[3], grade.GradeId);
    }

    private static async Task<AdmissionRequest> AddAdmissionAsync(Setup setup, string documentNumber, int? studentId = null)
    {
        var admission = new AdmissionRequest
        {
            YearId = 2027,
            GradeId = setup.GradeId,
            FirstName = "Sara",
            LastName = "Vélez",
            DocumentType = "TI",
            DocumentNumber = documentNumber,
            BirthDate = new DateOnly(2015, 3, 1),
            GuardianName = "Luz Vélez",
            GuardianPhone = "3001112233",
            CreatedById = setup.User.Id,
            StudentId = studentId,
        };
        setup.Db.AdmissionRequests.Add(admission);
        await setup.Db.SaveChangesAsync();
        return admission;
    }

    private static async Task<Student> AddStudentAsync(Setup setup)
    {
        var user = TestData.AddUser(setup.Db, "Sara", "Vélez");
        var student = new Student { StudentId = user.Id, StudentCode = $"S{user.Id}", Gender = 'F' };
        setup.Db.Students.Add(student);
        await setup.Db.SaveChangesAsync();
        return student;
    }

    [Fact]
    public async Task Checklist_hides_hidden_types_and_counts_only_required()
    {
        var setup = await CreateAsync();
        var admission = await AddAdmissionAsync(setup, "1061000001");
        var owner = await setup.Service.OwnerOfAdmissionAsync(admission.AdmissionRequestId);

        var checklist = await setup.Service.ChecklistAsync(owner, BeforeDeadline);

        Assert.Equal(["Registro civil", "Carné de vacunas", "Fotografía"], checklist.Items.Select(i => i.Name));
        Assert.Equal(2, checklist.RequiredTotal);
        Assert.Equal(0, checklist.RequiredLoaded);
        Assert.Equal(new DateOnly(2026, 11, 30), checklist.DueDate);
        Assert.DoesNotContain(checklist.Items, i => i.Overdue);
        Assert.False(checklist.Items.Single(i => i.Name == "Fotografía").Required);
    }

    [Fact]
    public async Task Required_without_file_is_overdue_after_the_deadline_but_still_uploadable()
    {
        var setup = await CreateAsync();
        var admission = await AddAdmissionAsync(setup, "1061000001");
        var owner = await setup.Service.OwnerOfAdmissionAsync(admission.AdmissionRequestId);

        var late = await setup.Service.ChecklistAsync(owner, AfterDeadline);

        Assert.Equal(["Registro civil", "Carné de vacunas"], late.Items.Where(i => i.Overdue).Select(i => i.Name));

        await setup.Service.UploadAsync(owner, setup.BirthCertificate.CharacteristicDetailId, FakeDocumentStorage.Pdf(), setup.User.Id, Now);
        var after = await setup.Service.ChecklistAsync(owner, AfterDeadline);

        Assert.Equal(1, after.RequiredLoaded);
        Assert.False(after.Items.Single(i => i.Name == "Registro civil").Overdue);
        Assert.True(after.Items.Single(i => i.Name == "Carné de vacunas").Overdue);
    }

    [Fact]
    public async Task A_type_is_complete_only_with_a_current_file()
    {
        var setup = await CreateAsync();
        var admission = await AddAdmissionAsync(setup, "1061000001");
        var owner = await setup.Service.OwnerOfAdmissionAsync(admission.AdmissionRequestId);
        var detailId = setup.BirthCertificate.CharacteristicDetailId;

        await setup.Service.UploadAsync(owner, detailId, FakeDocumentStorage.Jpg(), setup.User.Id, Now);
        var loaded = await setup.Service.ChecklistAsync(owner, BeforeDeadline);

        Assert.Equal("foto.jpg", loaded.Items.Single(i => i.DetailId == detailId).Document!.FileName);

        await setup.Service.RemoveAsync(owner, detailId, setup.User.Id, Now);
        var removed = await setup.Service.ChecklistAsync(owner, BeforeDeadline);

        Assert.Null(removed.Items.Single(i => i.DetailId == detailId).Document);
        Assert.Empty(setup.Storage.Files);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            setup.Service.RemoveAsync(owner, detailId, setup.User.Id, Now));

        Assert.Equal("No hay un archivo cargado para este documento.", error.Message);
    }

    [Fact]
    public async Task Upload_rejects_hidden_or_foreign_types()
    {
        var setup = await CreateAsync();
        var admission = await AddAdmissionAsync(setup, "1061000001");
        var owner = await setup.Service.OwnerOfAdmissionAsync(admission.AdmissionRequestId);
        var foreign = TestData.AddCatalog(setup.Db, Catalogs.Eps, link: true, ("SURA", "SURA", null)).Single();

        foreach (var detailId in new[] { setup.Hidden.CharacteristicDetailId, foreign.CharacteristicDetailId, 9999 })
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                setup.Service.UploadAsync(owner, detailId, FakeDocumentStorage.Pdf(), setup.User.Id, Now));

            Assert.Equal("Tipo de documento no válido.", error.Message);
        }

        Assert.Empty(setup.Storage.Files);
    }

    [Fact]
    public async Task Approved_admission_resolves_to_the_student()
    {
        var setup = await CreateAsync();
        var student = await AddStudentAsync(setup);
        var admission = await AddAdmissionAsync(setup, "1061000001", student.StudentId);

        var owner = await setup.Service.OwnerOfAdmissionAsync(admission.AdmissionRequestId);

        Assert.Equal(new DocumentOwner("Student", student.StudentId, 2027, student.StudentId), owner);
        Assert.Equal(new DocumentOwner("Student", student.StudentId, 2026, student.StudentId),
            await setup.Service.OwnerOfStudentAsync(student.StudentId, 2026));

        var missingAdmission = await Assert.ThrowsAsync<InvalidOperationException>(() => setup.Service.OwnerOfAdmissionAsync(9999));
        var missingStudent = await Assert.ThrowsAsync<InvalidOperationException>(() => setup.Service.OwnerOfStudentAsync(9999, 2027));

        Assert.Equal("No existe la solicitud de admisión.", missingAdmission.Message);
        Assert.Equal("No existe el estudiante.", missingStudent.Message);
    }

    [Fact]
    public async Task MissingCounts_returns_zero_entries_as_absent_and_counts_required_only()
    {
        var setup = await CreateAsync();
        var first = await AddAdmissionAsync(setup, "1061000001");
        var second = await AddAdmissionAsync(setup, "1061000002");
        var third = await AddAdmissionAsync(setup, "1061000003");
        var firstOwner = await setup.Service.OwnerOfAdmissionAsync(first.AdmissionRequestId);
        var thirdOwner = await setup.Service.OwnerOfAdmissionAsync(third.AdmissionRequestId);

        await setup.Service.UploadAsync(firstOwner, setup.BirthCertificate.CharacteristicDetailId, FakeDocumentStorage.Pdf(), setup.User.Id, Now);
        await setup.Service.UploadAsync(firstOwner, setup.Photo.CharacteristicDetailId, FakeDocumentStorage.Png(), setup.User.Id, Now);
        await setup.Service.UploadAsync(thirdOwner, setup.BirthCertificate.CharacteristicDetailId, FakeDocumentStorage.Pdf(), setup.User.Id, Now);
        await setup.Service.UploadAsync(thirdOwner, setup.Vaccines.CharacteristicDetailId, FakeDocumentStorage.Pdf(), setup.User.Id, Now);

        var counts = await setup.Service.MissingCountsAsync(
            DocumentTypes.AdmissionTable, [first.AdmissionRequestId, second.AdmissionRequestId, third.AdmissionRequestId]);

        Assert.Equal(new Dictionary<int, int>
        {
            [first.AdmissionRequestId] = 1,
            [second.AdmissionRequestId] = 2,
        }, counts);
        Assert.Equal(["Carné de vacunas"], await setup.Service.MissingNamesAsync(firstOwner));
    }

    [Fact]
    public async Task Last_required_upload_closes_the_pending_alert()
    {
        var setup = await CreateAsync();
        var student = await AddStudentAsync(setup);
        var type = TestData.AddNotificationType(setup.Db, NotificationCodes.EnrollDocsIncomplete, "Falta: {documentos}.");
        var notification = new Notification
        {
            NotificationTypeId = type.NotificationTypeId,
            Message = "Falta: Registro civil, Carné de vacunas.",
            StudentId = student.StudentId,
            YearId = 2027,
        };
        setup.Db.Notifications.Add(notification);
        await setup.Db.SaveChangesAsync();

        var owner = await setup.Service.OwnerOfStudentAsync(student.StudentId, 2027);

        await setup.Service.UploadAsync(owner, setup.BirthCertificate.CharacteristicDetailId, FakeDocumentStorage.Pdf(), setup.User.Id, Now);
        Assert.Equal(NotificationStatuses.Pending, (await setup.Db.Notifications.AsNoTracking().SingleAsync()).Status);

        await setup.Service.UploadAsync(owner, setup.Vaccines.CharacteristicDetailId, FakeDocumentStorage.Pdf(), setup.User.Id, Now);
        var closed = await setup.Db.Notifications.AsNoTracking().SingleAsync();

        Assert.Equal(NotificationStatuses.Done, closed.Status);
        Assert.Equal("Documentación completa.", closed.DoneNote);
        Assert.Equal(setup.User.Id, closed.DoneById);
        Assert.Equal(Now, closed.DoneAt);
    }

    [Fact]
    public async Task Moving_to_the_student_carries_the_admission_files()
    {
        var setup = await CreateAsync();
        var student = await AddStudentAsync(setup);
        var admission = await AddAdmissionAsync(setup, "1061000001");
        var owner = await setup.Service.OwnerOfAdmissionAsync(admission.AdmissionRequestId);

        await setup.Service.UploadAsync(owner, setup.BirthCertificate.CharacteristicDetailId, FakeDocumentStorage.Pdf(), setup.User.Id, Now);
        await setup.Service.MoveToStudentAsync(admission.AdmissionRequestId, student.StudentId, setup.User.Id, Now);

        var studentOwner = await setup.Service.OwnerOfStudentAsync(student.StudentId, 2027);
        var checklist = await setup.Service.ChecklistAsync(studentOwner, BeforeDeadline);

        Assert.Equal(1, checklist.RequiredLoaded);
        Assert.Equal(0, (await setup.Service.ChecklistAsync(owner, BeforeDeadline)).RequiredLoaded);
    }

    [Fact]
    public async Task Settings_add_rename_hide_and_protect_types_with_files()
    {
        var setup = await CreateAsync();
        var student = await AddStudentAsync(setup);
        var owner = await setup.Service.OwnerOfStudentAsync(student.StudentId, 2027);
        await setup.Service.UploadAsync(owner, setup.Vaccines.CharacteristicDetailId, FakeDocumentStorage.Pdf(), setup.User.Id, Now);

        var current = await setup.Service.GetSettingsAsync();

        Assert.Equal(["Registro civil", "Carné de vacunas", "Fotografía", "Antiguo"], current.Select(s => s.Name));
        Assert.Equal([false, true, false, false], current.Select(s => s.HasFiles));

        var withoutVaccines = current.Where(s => s.Name != "Carné de vacunas").ToList();
        var protectedError = await Assert.ThrowsAsync<InvalidOperationException>(() => setup.Service.SaveSettingsAsync(withoutVaccines));

        Assert.Equal("«Carné de vacunas» tiene archivos cargados; ocúltelo en lugar de quitarlo.", protectedError.Message);

        var duplicate = current.Append(new DocumentTypeSettingDto(null, " registro CIVIL ", "OPCIONAL", false)).ToList();
        var duplicateError = await Assert.ThrowsAsync<InvalidOperationException>(() => setup.Service.SaveSettingsAsync(duplicate));

        Assert.Equal("Ya existe un documento llamado «registro CIVIL».", duplicateError.Message);

        var badState = current.Select(s => s with { State = s.Name == "Fotografía" ? "TAL VEZ" : s.State }).ToList();
        Assert.Equal("Estado no válido.", (await Assert.ThrowsAsync<InvalidOperationException>(() => setup.Service.SaveSettingsAsync(badState))).Message);

        var next = new List<DocumentTypeSettingDto>
        {
            current[0] with { Name = "Registro civil de nacimiento" },
            current[1] with { State = "OCULTO" },
            current[3],
            new(null, "Paz y salvo", "OBLIGATORIO", false),
        };

        var saved = await setup.Service.SaveSettingsAsync(next);

        Assert.Equal(["Registro civil de nacimiento", "Carné de vacunas", "Antiguo", "Paz y salvo"], saved.Select(s => s.Name));
        Assert.Equal(setup.BirthCertificate.CharacteristicDetailId, saved[0].DetailId);
        Assert.Equal("OCULTO", saved[1].State);
        Assert.True(saved[1].HasFiles);
        Assert.False(await setup.Db.CharacteristicDetails.AnyAsync(d => d.Nombre == "Fotografía"));
    }

    [Fact]
    public async Task Settings_create_the_characteristic_when_missing()
    {
        var db = TestDb.Create();
        var service = new EnrollmentDocumentService(db, new DocumentService(db, new FakeDocumentStorage()));

        Assert.Empty(await service.GetSettingsAsync());

        var saved = await service.SaveSettingsAsync([new DocumentTypeSettingDto(null, "Registro civil", "OBLIGATORIO", false)]);

        Assert.Equal("Registro civil", Assert.Single(saved).Name);
        Assert.True(await db.Characteristics.AnyAsync(c => c.Name == Catalogs.EnrollmentDocuments));
    }
}

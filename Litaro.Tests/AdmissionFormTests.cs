using Litaro.Data;
using Litaro.Models;
using Litaro.Services;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Tests;

public class AdmissionFormTests
{
    private static readonly DateTime Now = new(2026, 11, 17, 15, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = new(2026, 11, 17);

    private sealed record Setup(
        AppDbContext Db,
        AdmissionService Service,
        EnrollmentDocumentService Documents,
        User User,
        Campus Campus,
        int GradeId,
        List<CharacteristicDetail> Types);

    private static async Task<Setup> CreateAsync(bool windowOpen = true)
    {
        var db = TestDb.Create();
        var campus = EnrollmentTestData.AddCampus(db);
        EnrollmentTestData.AddCatalogs(db);
        var user = TestData.AddUser(db, "Sonia", "Paz");
        var types = TestData.AddDocumentTypes(db, ("Registro civil", "OBLIGATORIO"), ("Carné de vacunas", "OBLIGATORIO"), ("Fotografía", "OPCIONAL"));
        var grade = new Grade { Name = "Sexto", OrderNum = 6, Level = "Secundaria" };
        var today = EnrollmentPeriodService.Today();

        db.Grades.Add(grade);
        db.AcademicYears.Add(new AcademicYear { YearId = 2027, Active = true });
        db.EnrollmentPeriods.Add(new EnrollmentPeriod
        {
            YearId = 2027,
            PreEnrollmentStart = today.AddDays(-40),
            PreEnrollmentEnd = today.AddDays(-20),
            AdmissionStart = windowOpen ? today.AddDays(-10) : today.AddDays(-30),
            AdmissionEnd = windowOpen ? today.AddDays(10) : today.AddDays(-1),
        });
        await db.SaveChangesAsync();

        return new Setup(db, EnrollmentTestData.Admissions(db), EnrollmentTestData.Documents(db), user, campus, grade.GradeId, types);
    }

    private static async Task<AdmissionRequest> AddAsync(Setup setup, string studentNumber, string status = AdmissionStatuses.Pending)
    {
        var admission = EnrollmentTestData.FullAdmission(2027, setup.GradeId, setup.Campus.CampusId, setup.User.Id, studentNumber);
        admission.Status = status;
        setup.Db.AdmissionRequests.Add(admission);
        await setup.Db.SaveChangesAsync();
        return admission;
    }

    private static SaveAdmissionRequest Form(Setup setup) =>
        EnrollmentTestData.Form(2027, setup.GradeId, setup.Campus.CampusId);

    [Fact]
    public async Task Create_stores_every_field_and_keeps_guardian_name()
    {
        var setup = await CreateAsync();

        var id = await setup.Service.CreateAsync(Form(setup) with { Notes = "  Viene de Bogotá " }, setup.User.Id);
        var saved = await setup.Db.AdmissionRequests.SingleAsync(a => a.AdmissionRequestId == id);

        Assert.Equal("Luz Vélez", saved.GuardianName);
        Assert.Equal("3001112233", saved.GuardianPhone);
        Assert.Equal("00", saved.EthnicGroup);
        Assert.Equal('F', saved.Gender);
        Assert.Equal("SURA", saved.Eps);
        Assert.Equal(setup.Campus.CampusId, saved.CampusId);
        Assert.Equal(EnrollmentTestData.GuardianEmail, saved.GuardianEmail);
        Assert.Equal("MOTHER", saved.GuardianRelationship);
        Assert.Equal("Viene de Bogotá", saved.Notes);
        Assert.Equal(AdmissionStatuses.Pending, saved.Status);
        Assert.Single(await setup.Db.AdmissionRequestEvents.ToListAsync());
    }

    [Fact]
    public async Task Create_still_requires_the_admission_window()
    {
        var setup = await CreateAsync(windowOpen: false);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => setup.Service.CreateAsync(Form(setup), setup.User.Id));

        Assert.StartsWith("Las solicitudes de admisión de 2027 se reciben del", error.Message);
        Assert.Empty(await setup.Db.AdmissionRequests.ToListAsync());
    }

    [Fact]
    public async Task Create_rejects_the_same_document_for_student_and_guardian()
    {
        var setup = await CreateAsync();
        var form = Form(setup);
        var same = form with { Guardian = form.Guardian with { DocumentNumber = form.Student.DocumentNumber } };

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => setup.Service.CreateAsync(same, setup.User.Id));

        Assert.Equal("El estudiante y el acudiente no pueden tener el mismo número de documento.", error.Message);
    }

    [Fact]
    public async Task Create_rejects_the_same_email_for_student_and_guardian()
    {
        var setup = await CreateAsync();
        var form = Form(setup);
        var shared = form with { Student = form.Student with { Email = "LUZ@correo.co" } };

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => setup.Service.CreateAsync(shared, setup.User.Id));

        Assert.Equal("El estudiante y el acudiente no pueden tener el mismo correo.", error.Message);
        Assert.Empty(await setup.Db.AdmissionRequests.ToListAsync());
    }

    [Fact]
    public async Task Update_changes_a_pending_request_and_logs_the_event()
    {
        var setup = await CreateAsync();
        var id = await setup.Service.CreateAsync(Form(setup), setup.User.Id);
        var form = Form(setup);

        await setup.Service.UpdateAsync(id, form with { Student = form.Student with { Eps = "NUEVA EPS" }, YearId = 2030 }, setup.User.Id);

        var saved = await setup.Db.AdmissionRequests.AsNoTracking().SingleAsync();
        var events = await setup.Db.AdmissionRequestEvents.OrderBy(e => e.AdmissionRequestEventId).ToListAsync();

        Assert.Equal("NUEVA EPS", saved.Eps);
        Assert.Equal(2027, saved.YearId);
        Assert.Equal(2, events.Count);
        Assert.Equal(AdmissionStatuses.Pending, events[1].FromStatus);
        Assert.Equal(AdmissionStatuses.Pending, events[1].ToStatus);
        Assert.Equal("Solicitud actualizada.", events[1].Note);
    }

    [Fact]
    public async Task Update_is_allowed_after_the_admission_window_closes()
    {
        var setup = await CreateAsync(windowOpen: false);
        var admission = await AddAsync(setup, "1061000001", AdmissionStatuses.Waitlist);
        var form = Form(setup);

        await setup.Service.UpdateAsync(admission.AdmissionRequestId, form with { Student = form.Student with { FirstName = "Sarah" } }, setup.User.Id);

        Assert.Equal("Sarah", (await setup.Db.AdmissionRequests.AsNoTracking().SingleAsync()).FirstName);
    }

    [Fact]
    public async Task Update_rejects_a_decided_request()
    {
        var setup = await CreateAsync();
        var admission = await AddAsync(setup, "1061000001", AdmissionStatuses.Approved);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            setup.Service.UpdateAsync(admission.AdmissionRequestId, Form(setup), setup.User.Id));

        Assert.Equal("La solicitud ya tiene una decisión final y no puede cambiarse.", error.Message);
    }

    [Fact]
    public async Task Update_rejects_the_number_of_another_open_request()
    {
        var setup = await CreateAsync();
        await AddAsync(setup, "1061000001");
        var second = await AddAsync(setup, "1061000002");
        var form = Form(setup);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            setup.Service.UpdateAsync(second.AdmissionRequestId, form, setup.User.Id));

        Assert.Equal("Ya hay una solicitud de admisión vigente para el documento 1061000001 en 2027.", error.Message);

        await setup.Service.UpdateAsync(second.AdmissionRequestId, form with { Student = form.Student with { DocumentNumber = "1061000002" } }, setup.User.Id);
    }

    [Fact]
    public async Task Get_returns_nulls_for_a_request_created_before_the_change()
    {
        var setup = await CreateAsync();
        var legacy = new AdmissionRequest
        {
            YearId = 2027,
            GradeId = setup.GradeId,
            FirstName = "Sara",
            LastName = "Vélez",
            DocumentType = "TI",
            DocumentNumber = "1061000009",
            BirthDate = new DateOnly(2015, 3, 1),
            GuardianName = "Luz Vélez",
            GuardianPhone = "3001112233",
            CreatedById = setup.User.Id,
        };
        setup.Db.AdmissionRequests.Add(legacy);
        await setup.Db.SaveChangesAsync();

        var detail = await setup.Service.GetAsync(legacy.AdmissionRequestId, Today);

        Assert.False(detail.Complete);
        Assert.False(detail.Summary.Complete);
        Assert.Null(detail.Student.CampusId);
        Assert.Null(detail.Student.Gender);
        Assert.Equal("1061000009", detail.Student.DocumentNumber);
        Assert.Equal("3001112233", detail.Guardian.Phone);
        Assert.Null(detail.Guardian.DocumentNumber);
        Assert.Null(detail.C600.EthnicGroup);
        Assert.Equal(2, detail.Documents.RequiredTotal);
        Assert.Equal(setup.GradeId, detail.GradeId);
    }

    [Fact]
    public async Task List_reports_missing_required_documents_per_request()
    {
        var setup = await CreateAsync();
        var first = await AddAsync(setup, "1061000001");
        var second = await AddAsync(setup, "1061000002");
        var owner = await setup.Documents.OwnerOfAdmissionAsync(first.AdmissionRequestId);
        await setup.Documents.UploadAsync(owner, setup.Types[0].CharacteristicDetailId, FakeDocumentStorage.Pdf(), setup.User.Id, Now);

        var rows = await setup.Service.ListAsync(2027);

        Assert.Equal(1, rows.Single(r => r.AdmissionRequestId == first.AdmissionRequestId).MissingDocuments);
        Assert.Equal(2, rows.Single(r => r.AdmissionRequestId == second.AdmissionRequestId).MissingDocuments);
        Assert.All(rows, r => Assert.True(r.Complete));
    }

    [Fact]
    public async Task Lookup_returns_student_and_guardian_data_or_flags_duplicates()
    {
        var setup = await CreateAsync();
        var people = new EnrollmentPersonService(setup.Db);

        var studentUser = TestData.AddUser(setup.Db, "Sara", "Vélez");
        studentUser.DocumentType = "TI";
        studentUser.DocumentNumber = "1061000001";
        studentUser.Email = "sara@correo.co";
        setup.Db.Students.Add(new Student
        {
            StudentId = studentUser.Id,
            StudentCode = "1061000001",
            BirthDate = new DateTime(2015, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            Gender = 'F',
            Eps = "SURA",
            BloodType = "O+",
            EthnicGroup = "00",
        });

        var guardianUser = TestData.AddUser(setup.Db, "Luz", "Vélez");
        guardianUser.DocumentNumber = "52000111";
        guardianUser.PhoneNumber = "3001112233";
        setup.Db.Parents.Add(new Parent { ParentId = guardianUser.Id, Relationship = "MOTHER", Address = "Calle 1" });

        var twinA = TestData.AddUser(setup.Db, "Uno", "Igual");
        var twinB = TestData.AddUser(setup.Db, "Dos", "Igual");
        twinA.DocumentNumber = "777";
        twinB.DocumentNumber = "777";
        await setup.Db.SaveChangesAsync();

        var student = await people.LookupAsync(" 1061000001 ");
        var guardian = await people.LookupAsync("52000111");
        var missing = await people.LookupAsync("000");
        var twins = await people.LookupAsync("777");

        Assert.True(student.Found);
        Assert.True(student.IsStudent);
        Assert.False(student.IsGuardian);
        Assert.Equal("SURA", student.Student!.Eps);
        Assert.Equal(new DateOnly(2015, 3, 1), student.Student.BirthDate);
        Assert.Equal("F", student.Student.Gender);
        Assert.Equal("sara@correo.co", student.Student.Email);
        Assert.Equal("00", student.C600!.EthnicGroup);

        Assert.True(guardian.IsGuardian);
        Assert.Equal("Calle 1", guardian.Guardian!.Address);
        Assert.Equal("3001112233", guardian.Guardian.Phone);
        Assert.Equal("MOTHER", guardian.Guardian.Relationship);
        Assert.Null(guardian.C600);

        Assert.False(missing.Found);
        Assert.False(missing.Ambiguous);

        Assert.False(twins.Found);
        Assert.True(twins.Ambiguous);
        Assert.Null(twins.Student);
    }
}

using Litaro.Data;
using Litaro.Models;
using Litaro.Services;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Tests;

public class EnrollmentFormServiceTests
{
    private static readonly DateOnly Today = new(2026, 11, 17);
    private static readonly DateTime Now = new(2026, 11, 17, 15, 0, 0, DateTimeKind.Utc);

    private sealed record Setup(AppDbContext Db, EnrollmentFormService Service, EnrollmentDocumentService Documents, Campus Campus, User StudentUser);

    private static async Task<Setup> CreateAsync()
    {
        var db = TestDb.Create();
        var campus = EnrollmentTestData.AddCampus(db);
        EnrollmentTestData.AddCatalogs(db);
        TestData.AddRole(db, StudentService.RoleName);
        TestData.AddRole(db, ParentService.RoleName);
        TestData.AddDocumentTypes(db, ("Registro civil", "OBLIGATORIO"), ("Carné de vacunas", "OBLIGATORIO"));
        db.AcademicYears.Add(new AcademicYear { YearId = 2027, Active = true });

        var user = TestData.AddUser(db, "Sara", "Vélez");
        user.DocumentType = "TI";
        user.DocumentNumber = EnrollmentTestData.StudentNumber;
        user.CampusId = campus.CampusId;
        db.Students.Add(new Student
        {
            StudentId = user.Id,
            StudentCode = "INPS-1",
            Gender = 'F',
            BirthDate = new DateTime(2015, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            Eps = "SALUD VIDA",
            BloodType = "O+",
        });
        await db.SaveChangesAsync();

        var documents = EnrollmentTestData.Documents(db);
        var service = new EnrollmentFormService(
            db,
            new EnrollmentFormValidator(db, new CatalogService(db)),
            new EnrollmentPersonService(db),
            documents);

        return new Setup(db, service, documents, campus, user);
    }

    private static SaveStudentFormRequest Request(Setup setup, GuardianFormData? guardian = null) =>
        new(EnrollmentTestData.Student(setup.Campus.CampusId) with { Eps = "SALUD VIDA" }, guardian, EnrollmentTestData.C600());

    private static async Task<User> AddGuardianAsync(Setup setup, string number, string firstName, bool primary)
    {
        var user = TestData.AddUser(setup.Db, firstName, "Vélez");
        user.DocumentNumber = number;
        user.PhoneNumber = "3000000000";
        setup.Db.Parents.Add(new Parent { ParentId = user.Id, Relationship = "FATHER", Address = "Calle 9" });
        setup.Db.ParentStudents.Add(new ParentStudent { ParentId = user.Id, StudentId = setup.StudentUser.Id, PrimaryContact = primary });
        await setup.Db.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task Get_returns_current_data_primary_guardian_and_checklist()
    {
        var setup = await CreateAsync();
        await AddGuardianAsync(setup, "80000001", "Pedro", primary: false);
        await AddGuardianAsync(setup, "52000111", "Luz", primary: true);

        var form = await setup.Service.GetAsync(setup.StudentUser.Id, 2027, Today);

        Assert.Equal("INPS-1", form.StudentCode);
        Assert.Equal("SALUD VIDA", form.Student.Eps);
        Assert.Equal(new DateOnly(2015, 3, 1), form.Student.BirthDate);
        Assert.Equal("Luz", form.Guardian!.FirstName);
        Assert.Equal("Calle 9", form.Guardian.Address);
        Assert.Null(form.C600.EthnicGroup);
        Assert.Equal(2, form.Documents.RequiredTotal);
    }

    [Fact]
    public async Task Get_returns_null_guardian_for_a_simat_imported_student()
    {
        var setup = await CreateAsync();

        var form = await setup.Service.GetAsync(setup.StudentUser.Id, 2027, Today);

        Assert.Null(form.Guardian);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => setup.Service.GetAsync(9999, 2027, Today));
        Assert.Equal("No existe el estudiante.", error.Message);
    }

    [Fact]
    public async Task Save_updates_student_and_c600_without_touching_the_document_number()
    {
        var setup = await CreateAsync();
        var request = Request(setup);

        await setup.Service.SaveAsync(setup.StudentUser.Id, request with
        {
            Student = request.Student with { DocumentNumber = "999", DocumentType = "CC", FirstName = "Sarita" },
            C600 = request.C600 with { EthnicGroup = "200" },
        }, Today);

        var user = await setup.Db.Users.AsNoTracking().SingleAsync(u => u.Id == setup.StudentUser.Id);
        var student = await setup.Db.Students.AsNoTracking().SingleAsync();

        Assert.Equal(EnrollmentTestData.StudentNumber, user.DocumentNumber);
        Assert.Equal("TI", user.DocumentType);
        Assert.Equal("Sarita", user.FirstName);
        Assert.Equal("200", student.EthnicGroup);
        Assert.Equal("INPS-1", student.StudentCode);
        Assert.Single(await setup.Db.Students.ToListAsync());
    }

    [Fact]
    public async Task Save_accepts_an_unchanged_eps_that_is_not_in_the_catalog()
    {
        var setup = await CreateAsync();

        await setup.Service.SaveAsync(setup.StudentUser.Id, Request(setup), Today);

        var request = Request(setup);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            setup.Service.SaveAsync(setup.StudentUser.Id, request with { Student = request.Student with { Eps = "OTRA" } }, Today));

        Assert.Equal("EPS no es una opción válida.", error.Message);
        Assert.Equal("SALUD VIDA", (await setup.Db.Students.AsNoTracking().SingleAsync()).Eps);
    }

    [Fact]
    public async Task Save_without_guardian_data_is_allowed()
    {
        var setup = await CreateAsync();

        await setup.Service.SaveAsync(setup.StudentUser.Id, Request(setup, new GuardianFormData(null, "", null, null, null, null, null, null)), Today);

        Assert.Empty(await setup.Db.Parents.ToListAsync());
        Assert.Equal("00", (await setup.Db.Students.AsNoTracking().SingleAsync()).EthnicGroup);
    }

    [Fact]
    public async Task Save_with_partial_guardian_data_is_rejected()
    {
        var setup = await CreateAsync();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            setup.Service.SaveAsync(setup.StudentUser.Id, Request(setup, EnrollmentTestData.Guardian() with { Email = null }), Today));

        Assert.Equal("Los datos del acudiente están incompletos.", error.Message);
        Assert.Null((await setup.Db.Students.AsNoTracking().SingleAsync()).EthnicGroup);
    }

    [Fact]
    public async Task Save_with_guardian_creates_and_links_it()
    {
        var setup = await CreateAsync();

        await setup.Service.SaveAsync(setup.StudentUser.Id, Request(setup, EnrollmentTestData.Guardian()), Today);

        var parent = await setup.Db.Parents.SingleAsync();
        var link = await setup.Db.ParentStudents.SingleAsync();
        var guardianUser = await setup.Db.Users.SingleAsync(u => u.Id == parent.ParentId);

        Assert.Equal(EnrollmentTestData.GuardianNumber, guardianUser.DocumentNumber);
        Assert.Equal(setup.Campus.CampusId, guardianUser.CampusId);
        Assert.Equal(setup.StudentUser.Id, link.StudentId);
        Assert.True(link.PrimaryContact);
    }

    [Fact]
    public async Task Replacing_the_guardian_shows_the_new_one()
    {
        var setup = await CreateAsync();
        await AddGuardianAsync(setup, "80000001", "Pedro", primary: true);

        await setup.Service.SaveAsync(setup.StudentUser.Id, Request(setup, EnrollmentTestData.Guardian()), Today);
        var form = await setup.Service.GetAsync(setup.StudentUser.Id, 2027, Today);

        Assert.Equal(EnrollmentTestData.GuardianNumber, form.Guardian!.DocumentNumber);
    }

    [Fact]
    public async Task Save_rejects_a_guardian_with_the_student_number()
    {
        var setup = await CreateAsync();
        var guardian = EnrollmentTestData.Guardian() with { DocumentNumber = EnrollmentTestData.StudentNumber };

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            setup.Service.SaveAsync(setup.StudentUser.Id, Request(setup, guardian), Today));

        Assert.Equal("El estudiante y el acudiente no pueden tener el mismo número de documento.", error.Message);
    }

    [Fact]
    public async Task Pre_enrollment_list_reports_missing_required_documents_per_student()
    {
        var setup = await CreateAsync();
        var other = TestData.AddUser(setup.Db, "Juan", "Rey");
        setup.Db.Students.Add(new Student { StudentId = other.Id, StudentCode = "INPS-2", Gender = 'M' });
        var grade = new Grade { Name = "Séptimo", OrderNum = 7, Level = "Secundaria" };
        setup.Db.Grades.Add(grade);
        await setup.Db.SaveChangesAsync();
        setup.Db.PreEnrollments.AddRange(
            new PreEnrollment { YearId = 2027, StudentId = setup.StudentUser.Id, GradeId = grade.GradeId },
            new PreEnrollment { YearId = 2027, StudentId = other.Id, GradeId = grade.GradeId });
        await setup.Db.SaveChangesAsync();

        var owner = await setup.Documents.OwnerOfStudentAsync(setup.StudentUser.Id, 2027);
        var types = await setup.Documents.GetSettingsAsync();
        await setup.Documents.UploadAsync(owner, types[0].DetailId!.Value, FakeDocumentStorage.Pdf(), other.Id, Now);

        var service = new PreEnrollmentService(setup.Db, new EnrollmentPeriodService(setup.Db), new NotificationService(setup.Db), setup.Documents);
        var rows = await service.ListAsync(2027);

        Assert.Equal(1, rows.Single(r => r.StudentId == setup.StudentUser.Id).MissingDocuments);
        Assert.Equal(2, rows.Single(r => r.StudentId == other.Id).MissingDocuments);
    }
}

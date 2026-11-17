using Litaro.Data;
using Litaro.Models;
using Litaro.Services;

namespace Litaro.Tests;

public class EnrollmentFormValidatorTests
{
    private static readonly DateOnly Today = new(2026, 11, 17);

    private static (AppDbContext Db, EnrollmentFormValidator Validator, Campus Campus) Create()
    {
        var db = TestDb.Create();
        var campus = EnrollmentTestData.AddCampus(db);
        EnrollmentTestData.AddCatalogs(db);
        return (db, new EnrollmentFormValidator(db, new CatalogService(db)), campus);
    }

    private static async Task<string> FailsAsync(Func<Task> action) =>
        (await Assert.ThrowsAsync<InvalidOperationException>(action)).Message;

    [Fact]
    public async Task Student_requires_everything_except_email()
    {
        var (_, validator, campus) = Create();
        var valid = EnrollmentTestData.Student(campus.CampusId);

        var cleaned = await validator.CleanStudentAsync(valid with { FirstName = "  Sara ", DocumentType = "ti" }, Today);

        Assert.Equal("Sara", cleaned.FirstName);
        Assert.Equal("TI", cleaned.DocumentType);
        Assert.Null(cleaned.Email);

        StudentFormData[] incomplete =
        [
            valid with { DocumentType = null },
            valid with { DocumentNumber = " " },
            valid with { FirstName = null },
            valid with { LastName = "" },
            valid with { BirthDate = null },
            valid with { Gender = null },
            valid with { BloodType = null },
            valid with { Eps = null },
            valid with { CampusId = null },
        ];

        foreach (var data in incomplete)
            Assert.Equal("Los datos del estudiante están incompletos.", await FailsAsync(() => validator.CleanStudentAsync(data, Today)));
    }

    [Fact]
    public async Task Student_rejects_future_birth_bad_email_long_text_and_missing_campus()
    {
        var (_, validator, campus) = Create();
        var valid = EnrollmentTestData.Student(campus.CampusId);

        Assert.Equal("La fecha de nacimiento no es válida.",
            await FailsAsync(() => validator.CleanStudentAsync(valid with { BirthDate = Today.AddDays(1) }, Today)));
        Assert.Equal("El correo no es válido.",
            await FailsAsync(() => validator.CleanStudentAsync(valid with { Email = "sara@correo" }, Today)));
        Assert.Equal("Alguno de los textos supera la longitud permitida.",
            await FailsAsync(() => validator.CleanStudentAsync(valid with { FirstName = new string('a', 101) }, Today)));
        Assert.Equal("La sede no existe.",
            await FailsAsync(() => validator.CleanStudentAsync(valid with { CampusId = 9999 }, Today)));
        Assert.Equal("sara@correo.co",
            (await validator.CleanStudentAsync(valid with { Email = " sara@correo.co " }, Today)).Email);
    }

    [Fact]
    public async Task Guardian_requires_every_field_including_email()
    {
        var (_, validator, _) = Create();
        var valid = EnrollmentTestData.Guardian();

        Assert.Equal("Luz", (await validator.CleanGuardianAsync(valid)).FirstName);

        GuardianFormData[] incomplete =
        [
            valid with { DocumentType = null },
            valid with { DocumentNumber = null },
            valid with { FirstName = null },
            valid with { LastName = null },
            valid with { Phone = null },
            valid with { Address = null },
            valid with { Email = null },
            valid with { Relationship = null },
        ];

        foreach (var data in incomplete)
            Assert.Equal("Los datos del acudiente están incompletos.", await FailsAsync(() => validator.CleanGuardianAsync(data)));
    }

    [Fact]
    public async Task C600_requires_every_field()
    {
        var (_, validator, _) = Create();

        Assert.Equal("00", (await validator.CleanC600Async(EnrollmentTestData.C600())).EthnicGroup);
        Assert.Equal("Los datos del formulario C-600 están incompletos.",
            await FailsAsync(() => validator.CleanC600Async(EnrollmentTestData.C600() with { Methodology = null })));
    }

    [Fact]
    public async Task Catalog_values_must_exist()
    {
        var (_, validator, campus) = Create();
        var student = EnrollmentTestData.Student(campus.CampusId);

        Assert.Equal("EPS no es una opción válida.",
            await FailsAsync(() => validator.CleanStudentAsync(student with { Eps = "INVENTADA" }, Today)));
        Assert.Equal("Género no es una opción válida.",
            await FailsAsync(() => validator.CleanStudentAsync(student with { Gender = "X" }, Today)));
        Assert.Equal("Tipo de documento no es una opción válida.",
            await FailsAsync(() => validator.CleanStudentAsync(student with { DocumentType = "PAS" }, Today)));
        Assert.Equal("Parentesco no es una opción válida.",
            await FailsAsync(() => validator.CleanGuardianAsync(EnrollmentTestData.Guardian() with { Relationship = "VECINO" })));
        Assert.Equal("Metodología no es una opción válida.",
            await FailsAsync(() => validator.CleanC600Async(EnrollmentTestData.C600() with { Methodology = "40" })));
    }

    [Fact]
    public async Task Stored_value_outside_the_catalog_is_accepted_while_unchanged()
    {
        var (_, validator, campus) = Create();
        var stored = EnrollmentTestData.Student(campus.CampusId) with { Eps = "SALUD VIDA" };

        var kept = await validator.CleanStudentAsync(stored, Today, stored);

        Assert.Equal("SALUD VIDA", kept.Eps);
        Assert.Equal("EPS no es una opción válida.",
            await FailsAsync(() => validator.CleanStudentAsync(stored with { Eps = "OTRA" }, Today, stored)));
    }

    [Fact]
    public async Task Ethnic_group_keeps_leading_zeros()
    {
        var (_, validator, _) = Create();

        Assert.Equal("00", (await validator.CleanC600Async(EnrollmentTestData.C600() with { EthnicGroup = "00" })).EthnicGroup);
        Assert.Equal("Grupo étnico no es una opción válida.",
            await FailsAsync(() => validator.CleanC600Async(EnrollmentTestData.C600() with { EthnicGroup = "0" })));
    }

    [Fact]
    public void Same_document_for_student_and_guardian_is_rejected()
    {
        var student = EnrollmentTestData.Student(1);
        var guardian = EnrollmentTestData.Guardian() with { DocumentNumber = student.DocumentNumber };

        var error = Assert.Throws<InvalidOperationException>(() => EnrollmentFormValidator.EnsureDistinct(student, guardian));

        Assert.Equal("El estudiante y el acudiente no pueden tener el mismo número de documento.", error.Message);
        EnrollmentFormValidator.EnsureDistinct(student, EnrollmentTestData.Guardian());
    }

    [Fact]
    public async Task Email_of_another_person_is_rejected()
    {
        var (db, validator, _) = Create();
        var other = TestData.AddUser(db, "Otra", "Persona");
        other.Email = "Luz@Correo.co";
        other.NormalizedEmail = "LUZ@CORREO.CO";
        await db.SaveChangesAsync();

        Assert.Equal("El correo luz@correo.co ya pertenece a otra persona.",
            await FailsAsync(() => validator.EnsureEmailFreeAsync("luz@correo.co", EnrollmentTestData.GuardianNumber)));

        await validator.EnsureEmailFreeAsync("luz@correo.co", other.DocumentNumber);
        await validator.EnsureEmailFreeAsync(null, EnrollmentTestData.GuardianNumber);
    }

    [Fact]
    public void Guardian_is_empty_only_without_any_data()
    {
        Assert.True(EnrollmentFormValidator.IsEmpty(null));
        Assert.True(EnrollmentFormValidator.IsEmpty(new GuardianFormData(null, " ", null, null, null, null, null, null)));
        Assert.False(EnrollmentFormValidator.IsEmpty(new GuardianFormData(null, null, null, null, "300", null, null, null)));
    }

    [Fact]
    public void IsComplete_is_false_for_a_request_created_before_the_change()
    {
        var legacy = new AdmissionRequest
        {
            FirstName = "Sara",
            LastName = "Vélez",
            DocumentType = "TI",
            DocumentNumber = "1",
            BirthDate = new DateOnly(2015, 3, 1),
            GuardianName = "Luz Vélez",
            GuardianPhone = "300",
        };

        Assert.False(EnrollmentFormValidator.IsComplete(legacy));
        Assert.True(EnrollmentFormValidator.IsComplete(EnrollmentTestData.FullAdmission(2027, 1, 1, 1)));

        var withoutCampus = EnrollmentTestData.FullAdmission(2027, 1, 1, 1);
        withoutCampus.CampusId = null;

        Assert.False(EnrollmentFormValidator.IsComplete(withoutCampus));
    }
}

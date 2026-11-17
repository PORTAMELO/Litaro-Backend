using Litaro.Data;
using Litaro.Models;
using Litaro.Services;

namespace Litaro.Tests;

public static class EnrollmentTestData
{
    public const string StudentNumber = "1061000001";
    public const string GuardianNumber = "52000111";
    public const string GuardianEmail = "luz@correo.co";

    public static Campus AddCampus(AppDbContext db)
    {
        var campus = new Campus { Name = "Sede Principal", Address = "Calle 4", Dane = "125875000001", SchoolId = 1 };
        db.Campuses.Add(campus);
        db.SaveChanges();
        return campus;
    }

    public static void AddCatalogs(AppDbContext db)
    {
        TestData.AddCatalog(db, Catalogs.DocumentType, true, ("CC", "Cédula de ciudadanía", "1"), ("TI", "Tarjeta de identidad", "2"), ("RC", "Registro civil", "5"));
        TestData.AddCatalog(db, Catalogs.Gender, true, ("F", "Femenino", null), ("M", "Masculino", null));
        TestData.AddCatalog(db, Catalogs.BloodType, true, ("O+", "O+", "1"), ("A+", "A+", "3"));
        TestData.AddCatalog(db, Catalogs.Eps, true, ("SURA", "SURA", null), ("NUEVA EPS", "NUEVA EPS", null));
        TestData.AddCatalog(db, Catalogs.Relationship, true, ("MOTHER", "Madre", null), ("FATHER", "Padre", null));
        TestData.AddCatalog(db, Catalogs.EthnicGroup, true, ("00", "No aplica", null), ("01", "Achagua", null), ("200", "Negritudes", null));
        TestData.AddCatalog(db, Catalogs.DisabilityType, true, ("1", "Sordera profunda", null), ("99", "No aplica", null));
        TestData.AddCatalog(db, Catalogs.ExceptionalCapacity, true, ("1", "Superdotado", null), ("9", "No aplica", null));
        TestData.AddCatalog(db, Catalogs.ConflictVictim, true, ("1", "En situación de desplazamiento", null), ("9", "No aplica", null));
        TestData.AddCatalog(db, Catalogs.Methodology, true, ("1", "Educación Tradicional", null), ("2", "Escuela nueva", null));
    }

    public static AdmissionService Admissions(AppDbContext db, FakeDocumentStorage? storage = null) =>
        new(
            db,
            new EnrollmentPeriodService(db),
            new NotificationService(db),
            new EnrollmentFormValidator(db, new CatalogService(db)),
            Documents(db, storage),
            new EnrollmentPersonService(db));

    public static EnrollmentDocumentService Documents(AppDbContext db, FakeDocumentStorage? storage = null) =>
        new(db, new DocumentService(db, storage ?? new FakeDocumentStorage()));

    public static StudentFormData Student(int campusId) =>
        new("TI", StudentNumber, "Sara", "Vélez", new DateOnly(2015, 3, 1), "F", "O+", "SURA", null, campusId);

    public static GuardianFormData Guardian() =>
        new("CC", GuardianNumber, "Luz", "Vélez", "3001112233", "Calle 1 # 2-3", GuardianEmail, "MOTHER");

    public static C600FormData C600() => new("00", "99", "9", "9", "1");

    public static SaveAdmissionRequest Form(short yearId, int gradeId, int campusId) =>
        new(yearId, gradeId, Student(campusId), Guardian(), C600(), null);

    public static AdmissionRequest FullAdmission(short yearId, int gradeId, int campusId, int createdById, string studentNumber = StudentNumber) =>
        new()
        {
            YearId = yearId,
            GradeId = gradeId,
            FirstName = "Sara",
            LastName = "Vélez",
            DocumentType = "TI",
            DocumentNumber = studentNumber,
            BirthDate = new DateOnly(2015, 3, 1),
            Gender = 'F',
            BloodType = "O+",
            Eps = "SURA",
            CampusId = campusId,
            GuardianName = "Luz Vélez",
            GuardianPhone = "3001112233",
            GuardianDocumentType = "CC",
            GuardianDocumentNumber = GuardianNumber,
            GuardianFirstName = "Luz",
            GuardianLastName = "Vélez",
            GuardianAddress = "Calle 1 # 2-3",
            GuardianEmail = GuardianEmail,
            GuardianRelationship = "MOTHER",
            EthnicGroup = "00",
            DisabilityType = "99",
            ExceptionalCapacity = "9",
            ConflictVictim = "9",
            Methodology = "1",
            CreatedById = createdById,
        };
}

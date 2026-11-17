using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services;

public record StudentFormData(
    string? DocumentType,
    string? DocumentNumber,
    string? FirstName,
    string? LastName,
    DateOnly? BirthDate,
    string? Gender,
    string? BloodType,
    string? Eps,
    string? Email,
    int? CampusId);

public record GuardianFormData(
    string? DocumentType,
    string? DocumentNumber,
    string? FirstName,
    string? LastName,
    string? Phone,
    string? Address,
    string? Email,
    string? Relationship);

public record C600FormData(
    string? EthnicGroup,
    string? DisabilityType,
    string? ExceptionalCapacity,
    string? ConflictVictim,
    string? Methodology);

public record SaveAdmissionRequest(
    short YearId,
    int GradeId,
    StudentFormData Student,
    GuardianFormData Guardian,
    C600FormData C600,
    string? Notes);

public class EnrollmentFormValidator(AppDbContext db, CatalogService catalogs)
{
    public const string StudentIncomplete = "Los datos del estudiante están incompletos.";
    public const string GuardianIncomplete = "Los datos del acudiente están incompletos.";
    public const string C600Incomplete = "Los datos del formulario C-600 están incompletos.";

    private const string TooLong = "Alguno de los textos supera la longitud permitida.";

    public async Task<StudentFormData> CleanStudentAsync(StudentFormData data, DateOnly today, StudentFormData? stored = null)
    {
        var clean = new StudentFormData(
            Upper(data.DocumentType),
            Text(data.DocumentNumber),
            Text(data.FirstName),
            Text(data.LastName),
            data.BirthDate,
            Upper(data.Gender),
            Upper(data.BloodType),
            Text(data.Eps),
            Text(data.Email),
            data.CampusId);

        if (clean.DocumentType is null || clean.DocumentNumber is null || clean.FirstName is null || clean.LastName is null ||
            clean.BirthDate is null || clean.Gender is null || clean.BloodType is null || clean.Eps is null || clean.CampusId is null)
            throw new InvalidOperationException(StudentIncomplete);

        if (clean.DocumentType.Length > 5 || clean.DocumentNumber.Length > 20 || clean.FirstName.Length > 100 ||
            clean.LastName.Length > 100 || clean.BloodType.Length > 5 || clean.Eps.Length > 100 || clean.Email?.Length > 256)
            throw new InvalidOperationException(TooLong);

        if (clean.BirthDate == default || clean.BirthDate > today)
            throw new InvalidOperationException("La fecha de nacimiento no es válida.");

        EnsureEmailFormat(clean.Email);

        await CheckAsync(Catalogs.DocumentType, clean.DocumentType, stored?.DocumentType);
        await CheckAsync(Catalogs.Gender, clean.Gender, stored?.Gender);
        await CheckAsync(Catalogs.BloodType, clean.BloodType, stored?.BloodType);
        await CheckAsync(Catalogs.Eps, clean.Eps, stored?.Eps);

        if (!await db.Campuses.AnyAsync(c => c.CampusId == clean.CampusId))
            throw new InvalidOperationException("La sede no existe.");

        return clean;
    }

    public async Task<GuardianFormData> CleanGuardianAsync(GuardianFormData data, GuardianFormData? stored = null)
    {
        var clean = new GuardianFormData(
            Upper(data.DocumentType),
            Text(data.DocumentNumber),
            Text(data.FirstName),
            Text(data.LastName),
            Text(data.Phone),
            Text(data.Address),
            Text(data.Email),
            Text(data.Relationship));

        if (clean.DocumentType is null || clean.DocumentNumber is null || clean.FirstName is null || clean.LastName is null ||
            clean.Phone is null || clean.Address is null || clean.Email is null || clean.Relationship is null)
            throw new InvalidOperationException(GuardianIncomplete);

        if (clean.DocumentType.Length > 5 || clean.DocumentNumber.Length > 20 || clean.FirstName.Length > 100 ||
            clean.LastName.Length > 100 || clean.Phone.Length > 20 || clean.Address.Length > 200 ||
            clean.Email.Length > 256 || clean.Relationship.Length > 20)
            throw new InvalidOperationException(TooLong);

        EnsureEmailFormat(clean.Email);

        await CheckAsync(Catalogs.DocumentType, clean.DocumentType, stored?.DocumentType);
        await CheckAsync(Catalogs.Relationship, clean.Relationship, stored?.Relationship);

        return clean;
    }

    public async Task<C600FormData> CleanC600Async(C600FormData data, C600FormData? stored = null)
    {
        var clean = new C600FormData(
            Text(data.EthnicGroup),
            Text(data.DisabilityType),
            Text(data.ExceptionalCapacity),
            Text(data.ConflictVictim),
            Text(data.Methodology));

        if (clean.EthnicGroup is null || clean.DisabilityType is null || clean.ExceptionalCapacity is null ||
            clean.ConflictVictim is null || clean.Methodology is null)
            throw new InvalidOperationException(C600Incomplete);

        if (new[] { clean.EthnicGroup, clean.DisabilityType, clean.ExceptionalCapacity, clean.ConflictVictim, clean.Methodology }
            .Any(v => v.Length > 5))
            throw new InvalidOperationException(TooLong);

        await CheckAsync(Catalogs.EthnicGroup, clean.EthnicGroup, stored?.EthnicGroup);
        await CheckAsync(Catalogs.DisabilityType, clean.DisabilityType, stored?.DisabilityType);
        await CheckAsync(Catalogs.ExceptionalCapacity, clean.ExceptionalCapacity, stored?.ExceptionalCapacity);
        await CheckAsync(Catalogs.ConflictVictim, clean.ConflictVictim, stored?.ConflictVictim);
        await CheckAsync(Catalogs.Methodology, clean.Methodology, stored?.Methodology);

        return clean;
    }

    public Task EnsureEmailFreeAsync(string? email, string documentNumber) =>
        EnsureEmailFreeAsync(db, email, documentNumber);

    public static async Task EnsureEmailFreeAsync(AppDbContext db, string? email, string documentNumber)
    {
        if (string.IsNullOrWhiteSpace(email))
            return;

        var normalized = email.Trim().ToUpperInvariant();

        var taken = await db.Users.AnyAsync(u =>
            u.DocumentNumber != documentNumber &&
            (u.NormalizedEmail == normalized || (u.Email != null && u.Email.ToUpper() == normalized)));

        if (taken)
            throw new InvalidOperationException($"El correo {email.Trim()} ya pertenece a otra persona.");
    }

    public static void EnsureDistinct(StudentFormData student, GuardianFormData guardian)
    {
        if (string.Equals(Text(student.DocumentNumber), Text(guardian.DocumentNumber), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("El estudiante y el acudiente no pueden tener el mismo número de documento.");

        if (Text(student.Email) is { } email && string.Equals(email, Text(guardian.Email), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("El estudiante y el acudiente no pueden tener el mismo correo.");
    }

    public static bool IsEmpty(GuardianFormData? guardian) =>
        guardian is null ||
        new[]
        {
            guardian.DocumentType, guardian.DocumentNumber, guardian.FirstName, guardian.LastName,
            guardian.Phone, guardian.Address, guardian.Email, guardian.Relationship,
        }.All(string.IsNullOrWhiteSpace);

    public static bool IsComplete(AdmissionRequest admission) =>
        admission.Gender is not null &&
        admission.CampusId is not null &&
        new[]
        {
            admission.DocumentType, admission.DocumentNumber, admission.FirstName, admission.LastName,
            admission.BloodType, admission.Eps,
            admission.GuardianDocumentType, admission.GuardianDocumentNumber, admission.GuardianFirstName,
            admission.GuardianLastName, admission.GuardianPhone, admission.GuardianAddress, admission.GuardianEmail,
            admission.GuardianRelationship,
            admission.EthnicGroup, admission.DisabilityType, admission.ExceptionalCapacity, admission.ConflictVictim,
            admission.Methodology,
        }.All(v => !string.IsNullOrWhiteSpace(v));

    private async Task CheckAsync(CatalogKey key, string value, string? stored)
    {
        if (value == stored)
            return;

        if (!await catalogs.IsValidAsync(key, value))
            throw new InvalidOperationException($"{key.Label} no es una opción válida.");
    }

    private static void EnsureEmailFormat(string? email)
    {
        if (email is null)
            return;

        var at = email.IndexOf('@');
        var valid = at > 0 && email.IndexOf('@', at + 1) < 0 && email.IndexOf('.', at + 2) > at + 1 &&
                    !email.EndsWith('.') && !email.Contains(' ');

        if (!valid)
            throw new InvalidOperationException("El correo no es válido.");
    }

    private static string? Text(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? Upper(string? value) => Text(value)?.ToUpperInvariant();
}

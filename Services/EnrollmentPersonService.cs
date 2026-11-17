using Litaro.Data;
using Litaro.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services;

public record PersonLookupDto(
    bool Found,
    bool Ambiguous,
    bool IsStudent,
    bool IsGuardian,
    StudentFormData? Student,
    C600FormData? C600,
    GuardianFormData? Guardian);

public class EnrollmentPersonService(AppDbContext db)
{
    private const int CodeLength = 20;

    private static readonly PersonLookupDto NotFound = new(false, false, false, false, null, null, null);
    private static readonly PersonLookupDto Duplicated = new(false, true, false, false, null, null, null);

    public async Task<PersonLookupDto> LookupAsync(string documentNumber)
    {
        var number = (documentNumber ?? string.Empty).Trim();
        if (number.Length == 0)
            return NotFound;

        var users = await db.Users
            .AsNoTracking()
            .Where(u => u.DocumentNumber == number)
            .Take(2)
            .ToListAsync();

        if (users.Count == 0)
            return NotFound;

        if (users.Count > 1)
            return Duplicated;

        var user = users[0];
        var student = await db.Students.AsNoTracking().FirstOrDefaultAsync(s => s.StudentId == user.Id);
        var parent = await db.Parents.AsNoTracking().FirstOrDefaultAsync(p => p.ParentId == user.Id);

        var studentData = new StudentFormData(
            user.DocumentType,
            user.DocumentNumber,
            user.FirstName,
            user.LastName,
            student is null ? null : DateOnly.FromDateTime(student.BirthDate),
            student?.Gender.ToString(),
            student?.BloodType,
            student?.Eps,
            user.Email,
            user.CampusId);

        var c600 = student is null
            ? null
            : new C600FormData(student.EthnicGroup, student.DisabilityType, student.ExceptionalCapacity, student.ConflictVictim, student.Methodology);

        var guardian = new GuardianFormData(
            user.DocumentType,
            user.DocumentNumber,
            user.FirstName,
            user.LastName,
            user.PhoneNumber,
            parent?.Address,
            user.Email,
            parent?.Relationship);

        return new PersonLookupDto(true, false, student is not null, parent is not null, studentData, c600, guardian);
    }

    public async Task EnsureCanSaveAsync(StudentFormData student, GuardianFormData? guardian)
    {
        if (guardian is not null)
            EnrollmentFormValidator.EnsureDistinct(student, guardian);

        await EnsureSingleAsync(student.DocumentNumber);

        if (guardian is not null)
            await EnsureSingleAsync(guardian.DocumentNumber);

        await EnrollmentFormValidator.EnsureEmailFreeAsync(db, student.Email, student.DocumentNumber ?? string.Empty);

        if (guardian is not null)
            await EnrollmentFormValidator.EnsureEmailFreeAsync(db, guardian.Email, guardian.DocumentNumber ?? string.Empty);

        if (await StudentRoleIdAsync() is null)
            throw new InvalidOperationException("No existe el rol de estudiante.");

        if (guardian is not null && await ParentRoleIdAsync() is null)
            throw new InvalidOperationException("No existe el rol de acudiente.");
    }

    public async Task<Student> UpsertStudentAsync(StudentFormData data, C600FormData c600)
    {
        var roleId = await StudentRoleIdAsync() ?? throw new InvalidOperationException("No existe el rol de estudiante.");
        var user = await db.Users.FirstOrDefaultAsync(u => u.DocumentNumber == data.DocumentNumber);

        if (user is null)
        {
            user = await NewUserAsync(data.DocumentType!, data.DocumentNumber!, data.FirstName!, data.LastName!, data.CampusId!.Value);
        }
        else
        {
            user.FirstName = data.FirstName!;
            user.LastName = data.LastName!;
            user.DocumentType = data.DocumentType!;
            user.CampusId = data.CampusId!.Value;
        }

        SetEmail(user, data.Email);
        await db.SaveChangesAsync();

        var student = await db.Students.FirstOrDefaultAsync(s => s.StudentId == user.Id);
        if (student is null)
        {
            student = new Student
            {
                StudentId = user.Id,
                StudentCode = await NextCodeAsync(data.DocumentType!, data.DocumentNumber!),
            };
            db.Students.Add(student);
        }

        student.BirthDate = DateTime.SpecifyKind(data.BirthDate!.Value.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        student.Gender = data.Gender![0];
        student.BloodType = data.BloodType;
        student.Eps = data.Eps;
        student.EthnicGroup = c600.EthnicGroup;
        student.DisabilityType = c600.DisabilityType;
        student.ExceptionalCapacity = c600.ExceptionalCapacity;
        student.ConflictVictim = c600.ConflictVictim;
        student.Methodology = c600.Methodology;
        student.Active = true;

        await EnsureRoleAsync(user.Id, roleId);
        await db.SaveChangesAsync();

        return student;
    }

    public async Task<Parent> UpsertGuardianAsync(GuardianFormData data, int campusId, int studentId)
    {
        var roleId = await ParentRoleIdAsync() ?? throw new InvalidOperationException("No existe el rol de acudiente.");
        var user = await db.Users.FirstOrDefaultAsync(u => u.DocumentNumber == data.DocumentNumber);

        if (user is null)
        {
            user = await NewUserAsync(data.DocumentType!, data.DocumentNumber!, data.FirstName!, data.LastName!, campusId);
        }
        else
        {
            user.FirstName = data.FirstName!;
            user.LastName = data.LastName!;
            user.DocumentType = data.DocumentType!;
        }

        user.PhoneNumber = data.Phone;
        SetEmail(user, data.Email);
        await db.SaveChangesAsync();

        var parent = await db.Parents.FirstOrDefaultAsync(p => p.ParentId == user.Id);
        if (parent is null)
        {
            parent = new Parent { ParentId = user.Id };
            db.Parents.Add(parent);
        }

        parent.Relationship = data.Relationship!;
        parent.Address = data.Address;
        parent.Active = true;

        var links = await db.ParentStudents.Where(ps => ps.StudentId == studentId).ToListAsync();

        foreach (var other in links.Where(l => l.ParentId != user.Id))
            other.PrimaryContact = false;

        var link = links.FirstOrDefault(l => l.ParentId == user.Id);
        if (link is null)
        {
            db.ParentStudents.Add(new ParentStudent
            {
                ParentId = user.Id,
                StudentId = studentId,
                PrimaryContact = true,
            });
        }
        else
        {
            link.PrimaryContact = true;
        }

        await EnsureRoleAsync(user.Id, roleId);
        await db.SaveChangesAsync();

        return parent;
    }

    private async Task EnsureSingleAsync(string? documentNumber)
    {
        if (string.IsNullOrWhiteSpace(documentNumber))
            return;

        var count = await db.Users.Where(u => u.DocumentNumber == documentNumber).Take(2).CountAsync();

        if (count > 1)
            throw new InvalidOperationException(
                $"Hay más de un usuario con el documento {documentNumber}. Resuelva el duplicado antes de continuar.");
    }

    private Task<int?> StudentRoleIdAsync() => RoleIdAsync(StudentService.RoleName);

    private async Task<int?> ParentRoleIdAsync() =>
        await RoleIdAsync(ParentService.RoleName)
        ?? (PermissionCatalog.AliasOf(ParentService.RoleName) is { } alias ? await RoleIdAsync(alias) : null);

    private Task<int?> RoleIdAsync(string name)
    {
        var normalized = name.ToUpperInvariant();

        return db.Roles
            .Where(r => r.NormalizedName == normalized)
            .Select(r => (int?)r.Id)
            .FirstOrDefaultAsync();
    }

    private async Task EnsureRoleAsync(int userId, int roleId)
    {
        if (db.UserRoles.Local.Any(r => r.UserId == userId && r.RoleId == roleId) ||
            await db.UserRoles.AnyAsync(r => r.UserId == userId && r.RoleId == roleId))
            return;

        db.UserRoles.Add(new IdentityUserRole<int> { UserId = userId, RoleId = roleId });
    }

    private async Task<User> NewUserAsync(string documentType, string documentNumber, string firstName, string lastName, int campusId)
    {
        var userName = await NextUserNameAsync(documentType, documentNumber);

        var user = new User
        {
            UserName = userName,
            NormalizedUserName = userName.ToUpperInvariant(),
            DocumentType = documentType,
            DocumentNumber = documentNumber,
            FirstName = firstName,
            LastName = lastName,
            CampusId = campusId,
            Active = true,
            MustChangePassword = true,
            CreationDate = DateTime.UtcNow,
            SecurityStamp = Guid.NewGuid().ToString("N").ToUpperInvariant(),
            ConcurrencyStamp = Guid.NewGuid().ToString(),
            LockoutEnabled = true,
        };

        db.Users.Add(user);
        return user;
    }

    private async Task<string> NextUserNameAsync(string documentType, string documentNumber)
    {
        foreach (var candidate in Candidates(documentType, documentNumber, int.MaxValue))
        {
            var normalized = candidate.ToUpperInvariant();
            if (!await db.Users.AnyAsync(u => u.NormalizedUserName == normalized))
                return candidate;
        }

        throw new InvalidOperationException($"No se pudo generar el usuario para el documento {documentNumber}.");
    }

    private async Task<string> NextCodeAsync(string documentType, string documentNumber)
    {
        foreach (var candidate in Candidates(documentType, documentNumber, CodeLength))
        {
            if (!await db.Students.AnyAsync(s => s.StudentCode == candidate))
                return candidate;
        }

        throw new InvalidOperationException($"No se pudo generar el código de estudiante para el documento {documentNumber}.");
    }

    private static IEnumerable<string> Candidates(string documentType, string documentNumber, int maxLength)
    {
        string[] fixedCandidates = [documentNumber, $"{documentType}{documentNumber}"];

        foreach (var candidate in fixedCandidates.Concat(Enumerable.Range(2, 98).Select(i => $"{documentType}{documentNumber}-{i}")))
        {
            if (candidate.Length <= maxLength)
                yield return candidate;
        }
    }

    private static void SetEmail(User user, string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return;

        user.Email = email.Trim();
        user.NormalizedEmail = user.Email.ToUpperInvariant();
    }
}

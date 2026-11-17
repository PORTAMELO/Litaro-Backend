using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services;

public record StudentFormDto(
    int StudentId,
    string StudentCode,
    StudentFormData Student,
    GuardianFormData? Guardian,
    C600FormData C600,
    ChecklistDto Documents);

public record SaveStudentFormRequest(StudentFormData Student, GuardianFormData? Guardian, C600FormData C600);

public class EnrollmentFormService(
    AppDbContext db,
    EnrollmentFormValidator validator,
    EnrollmentPersonService people,
    EnrollmentDocumentService documents)
{
    private sealed record Current(User User, Student Student, StudentFormData StudentData, C600FormData C600, GuardianFormData? Guardian);

    public async Task<StudentFormDto> GetAsync(int studentId, short yearId, DateOnly today)
    {
        var current = await LoadAsync(studentId);
        var checklist = await documents.ChecklistAsync(await documents.OwnerOfStudentAsync(studentId, yearId), today);

        return new StudentFormDto(
            studentId,
            current.Student.StudentCode,
            current.StudentData,
            current.Guardian,
            current.C600,
            checklist);
    }

    public async Task SaveAsync(int studentId, SaveStudentFormRequest request, DateOnly today)
    {
        if (request.Student is null || request.C600 is null)
            throw new InvalidOperationException(EnrollmentFormValidator.StudentIncomplete);

        var current = await LoadAsync(studentId);

        var studentInput = request.Student with
        {
            DocumentType = current.StudentData.DocumentType,
            DocumentNumber = current.StudentData.DocumentNumber,
        };

        var student = await validator.CleanStudentAsync(studentInput, today, current.StudentData);
        var c600 = await validator.CleanC600Async(request.C600, current.C600);

        GuardianFormData? guardian = null;
        if (!EnrollmentFormValidator.IsEmpty(request.Guardian))
        {
            guardian = await validator.CleanGuardianAsync(request.Guardian!, current.Guardian);
            EnrollmentFormValidator.EnsureDistinct(student, guardian);
        }

        await people.EnsureCanSaveAsync(student, guardian);

        var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync() : null;

        try
        {
            var saved = await people.UpsertStudentAsync(student, c600);

            if (guardian is not null)
                await people.UpsertGuardianAsync(guardian, student.CampusId!.Value, saved.StudentId);

            if (transaction is not null)
                await transaction.CommitAsync();
        }
        catch
        {
            if (transaction is not null)
                await transaction.RollbackAsync();

            throw;
        }
        finally
        {
            if (transaction is not null)
                await transaction.DisposeAsync();
        }
    }

    private async Task<Current> LoadAsync(int studentId)
    {
        var student = await db.Students.AsNoTracking().FirstOrDefaultAsync(s => s.StudentId == studentId)
            ?? throw new InvalidOperationException("No existe el estudiante.");

        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == studentId);

        var studentData = new StudentFormData(
            user.DocumentType,
            user.DocumentNumber,
            user.FirstName,
            user.LastName,
            DateOnly.FromDateTime(student.BirthDate),
            student.Gender.ToString(),
            student.BloodType,
            student.Eps,
            user.Email,
            user.CampusId);

        var c600 = new C600FormData(
            student.EthnicGroup,
            student.DisabilityType,
            student.ExceptionalCapacity,
            student.ConflictVictim,
            student.Methodology);

        var link = await db.ParentStudents
            .AsNoTracking()
            .Where(ps => ps.StudentId == studentId)
            .OrderByDescending(ps => ps.PrimaryContact)
            .ThenBy(ps => ps.ParentStudentId)
            .Select(ps => new { ps.ParentId, ps.Parent.Relationship, ps.Parent.Address })
            .FirstOrDefaultAsync();

        GuardianFormData? guardian = null;
        if (link is not null)
        {
            var guardianUser = await db.Users.AsNoTracking().FirstAsync(u => u.Id == link.ParentId);

            guardian = new GuardianFormData(
                guardianUser.DocumentType,
                guardianUser.DocumentNumber,
                guardianUser.FirstName,
                guardianUser.LastName,
                guardianUser.PhoneNumber,
                link.Address,
                guardianUser.Email,
                link.Relationship);
        }

        return new Current(user, student, studentData, c600, guardian);
    }
}

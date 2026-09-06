using Litaro.Data;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services;

public record LookupOption(string Id, string Label);

public class LookupService(AppDbContext db, ForeignKeyResolverService fkSvc)
{
    public async Task<List<LookupOption>?> GetOptionsAsync(string tableName)
    {
        if (!SchemaService.AllowedTables.Contains(tableName))
            return null;

        return tableName switch
        {
            "Student" => await GetStudentOptionsAsync(),
            "Teacher" => await GetTeacherOptionsAsync(),
            "Parent" => await GetParentOptionsAsync(),
            "User" => await GetUserOptionsAsync(),
            "AcademicYear" => await GetAcademicYearOptionsAsync(),
            "AcademicPeriod" => await GetAcademicPeriodOptionsAsync(),
            "Enrollment" => await GetEnrollmentOptionsAsync(),
            "AcademicAssignment" => await GetAcademicAssignmentOptionsAsync(),
            _ => await GetGenericOptionsAsync(tableName),
        };
    }

    private async Task<List<LookupOption>> GetStudentOptionsAsync()
    {
        var students = await db.Students.Include(s => s.User).ToListAsync();

        return students
            .OrderBy(s => s.StudentCode)
            .Select(s => new LookupOption(
                s.StudentId.ToString(),
                $"{s.StudentCode} — {s.User.FirstName} {s.User.LastName}".Trim()))
            .ToList();
    }

    private async Task<List<LookupOption>> GetTeacherOptionsAsync()
    {
        var teachers = await db.Teachers.Include(t => t.User).ToListAsync();

        return teachers
            .OrderBy(t => t.User.LastName).ThenBy(t => t.User.FirstName)
            .Select(t => new LookupOption(
                t.TeacherId.ToString(),
                $"{t.User.FirstName} {t.User.LastName} — {t.Specialty}".Trim()))
            .ToList();
    }

    private async Task<List<LookupOption>> GetParentOptionsAsync()
    {
        var parents = await db.Parents.Include(p => p.User).ToListAsync();

        return parents
            .OrderBy(p => p.User.LastName).ThenBy(p => p.User.FirstName)
            .Select(p => new LookupOption(
                p.ParentId.ToString(),
                $"{p.User.FirstName} {p.User.LastName} ({p.User.DocumentType} {p.User.DocumentNumber})"))
            .ToList();
    }

    private async Task<List<LookupOption>> GetUserOptionsAsync()
    {
        var users = await db.Users.ToListAsync();

        return users
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
            .Select(u => new LookupOption(
                u.Id.ToString(),
                $"{u.FirstName} {u.LastName} ({u.DocumentType} {u.DocumentNumber})"))
            .ToList();
    }

    private async Task<List<LookupOption>> GetAcademicYearOptionsAsync()
    {
        var years = await db.AcademicYears.ToListAsync();

        return years
            .OrderByDescending(y => y.StartDate)
            .Select(y => new LookupOption(
                y.YearId.ToString(),
                y.StartDate.Year == y.EndDate.Year
                    ? $"Año {y.StartDate:yyyy}"
                    : $"{y.StartDate:yyyy}-{y.EndDate:yyyy}"))
            .ToList();
    }

    private async Task<List<LookupOption>> GetAcademicPeriodOptionsAsync()
    {
        var periods = await db.AcademicPeriods.Include(p => p.AcademicYear).ToListAsync();

        return periods
            .OrderByDescending(p => p.YearId).ThenBy(p => p.PeriodNumber)
            .Select(p => new LookupOption(
                p.PeriodId.ToString(),
                $"Periodo {p.PeriodNumber} ({p.AcademicYear.StartDate:yyyy})"))
            .ToList();
    }

    private async Task<List<LookupOption>> GetEnrollmentOptionsAsync()
    {
        var enrollments = await db.Enrollments
            .Include(e => e.Student).ThenInclude(s => s.User)
            .Include(e => e.Classroom)
            .Include(e => e.AcademicYear)
            .ToListAsync();

        return enrollments
            .OrderByDescending(e => e.EnrollmentDate)
            .Select(e => new LookupOption(
                e.EnrollmentId.ToString(),
                $"{e.Student.StudentCode} — {e.Student.User.FirstName} {e.Student.User.LastName} — {e.Classroom.Name} ({e.AcademicYear.StartDate:yyyy})"))
            .ToList();
    }

    private async Task<List<LookupOption>> GetAcademicAssignmentOptionsAsync()
    {
        var assignments = await db.AcademicAssignments
            .Include(a => a.Subject)
            .Include(a => a.Classroom)
            .Include(a => a.Teacher).ThenInclude(t => t.User)
            .ToListAsync();

        return assignments
            .OrderBy(a => a.Classroom.Name).ThenBy(a => a.Subject.Name)
            .Select(a => new LookupOption(
                a.AssignmentId.ToString(),
                $"{a.Subject.Name} — {a.Classroom.Name} — {a.Teacher.User.FirstName} {a.Teacher.User.LastName}"))
            .ToList();
    }

    private async Task<List<LookupOption>> GetGenericOptionsAsync(string tableName)
    {
        var pkColumn = await GetPrimaryKeyColumnAsync(tableName);
        if (pkColumn is null)
            return new List<LookupOption>();

        var hasName = await fkSvc.HasNameColumnAsync(tableName);
        var labelColumn = hasName ? "Name" : pkColumn;

        var sql = $@"
            SELECT ""{pkColumn}""::text AS ""Id"", ""{labelColumn}""::text AS ""Label""
            FROM ""{tableName}""
            ORDER BY ""{labelColumn}""";

        return await db.Database.SqlQueryRaw<LookupOption>(sql).ToListAsync();
    }

    private async Task<string?> GetPrimaryKeyColumnAsync(string tableName)
    {
        return await db.Database.SqlQuery<string>($@"
            SELECT kcu.column_name AS ""Value""
            FROM information_schema.table_constraints tc
            JOIN information_schema.key_column_usage kcu
                ON tc.constraint_name = kcu.constraint_name AND tc.table_schema = kcu.table_schema
            WHERE tc.constraint_type = 'PRIMARY KEY'
                AND tc.table_schema = 'public'
                AND tc.table_name = {tableName}
            LIMIT 1")
            .FirstOrDefaultAsync();
    }
}
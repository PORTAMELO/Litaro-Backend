using Litaro.Data;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services;

public record SchemaData(
    string ColumnName,
    string DataType,
    int? MaxLength,
    string IsNullable,
    string? ColumnDefault
);

public class SchemaService(AppDbContext db)
{

    private static readonly HashSet<string> AllowedTables = new()
    {
        "AcademicAssignment",
        "AcademicPeriod",
        "AcademicYear",
        "Attendance",
        "Campus",
        "Classroom",
        "Enrollment",
        "Grade",
        "GradeScore",
        "Parent",
        "ParentStudent",
        "Schedule",
        "School",
        "Student",
        "StudentLog",
        "Subject",
        "Teacher",
        "User",
        "WebContent",
        "WebContentConfiguration"
    };

    public async Task<List<SchemaData>?> GetByTableAsync(string tableName)
    {
        if (!AllowedTables.Contains(tableName))
            return null;

        return await db.Database.SqlQuery<SchemaData>($@"
        SELECT 
            column_name AS ""ColumnName"",
            data_type AS ""DataType"",
            character_maximum_length AS ""MaxLength"",
            is_nullable AS ""IsNullable"",
            column_default AS ""ColumnDefault""
        FROM information_schema.columns
        WHERE table_schema = 'public'
        AND table_name = {tableName}
        ORDER BY ordinal_position;")
        .ToListAsync();
    }
}
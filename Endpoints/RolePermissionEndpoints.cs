using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Endpoints;

public static class RolePermissionEndpoints
{

    private static readonly string[] ManagedTables =
    [
        "School", "Campus", "AcademicYear", "AcademicPeriod",
        "Characteristic", "CharacteristicDetail",
        "User", "Student", "Parent", "Teacher",
        "Grade", "Classroom", "Subject", "AcademicAssignment", "Schedule",
        "Enrollment", "GradeScore", "Attendance", "StudentLog",
        "Space", "StudyPlan", "TeacherAvailability",
    ];

    public record RolePermissionRow(string TableName, int Permissions, string? View);
    public record SaveRolePermissionRequest(int RoleId, string TableName, int Permissions, string? View);

    public static void MapRolePermissionEndpoints(this WebApplication app)
    {
        app.MapGet("/role-permissions/{roleId:int}", async (int roleId, AppDbContext db) =>
        {
            var existing = await db.RolePermissions
                .Where(rp => rp.RoleId == roleId)
                .ToDictionaryAsync(rp => rp.TableName);

            var rows = ManagedTables.Select(table =>
                existing.TryGetValue(table, out var rp)
                    ? new RolePermissionRow(table, rp.Permissions, rp.View)
                    : new RolePermissionRow(table, 0, null));

            return Results.Ok(rows);
        }).RequireAuthorization(policy => policy.RequireRole("Administrador"));

        app.MapPut("/role-permissions", async (SaveRolePermissionRequest request, AppDbContext db) =>
        {
            if (!ManagedTables.Contains(request.TableName))
                return Results.BadRequest("Tabla no administrable.");

            if (request.Permissions is < 0 or > 15)
                return Results.BadRequest("Permisos inválidos.");

            var row = await db.RolePermissions
                .FirstOrDefaultAsync(rp => rp.RoleId == request.RoleId && rp.TableName == request.TableName);

            if (row is null)
            {
                row = new RolePermission { RoleId = request.RoleId, TableName = request.TableName };
                db.RolePermissions.Add(row);
            }

            row.Permissions = request.Permissions;
            row.View = string.IsNullOrWhiteSpace(request.View) ? null : request.View.Trim();

            await db.SaveChangesAsync();

            return Results.Ok(new RolePermissionRow(row.TableName, row.Permissions, row.View));
        }).RequireAuthorization(policy => policy.RequireRole("Administrador"));
    }
}
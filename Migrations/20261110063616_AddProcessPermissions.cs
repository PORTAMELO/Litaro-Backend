using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Litaro.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO "RolePermission" ("RoleId", "TableName", "Permissions")
                SELECT r."Id", v.key, v.perm
                FROM "AspNetRoles" r
                JOIN (VALUES
                  ('Administrador','AdministrationModule',15), ('Administrador','EnrollmentProcess',15),
                  ('Administrador','MySchedule',1), ('Administrador','GradesModule',15),
                  ('Administrador','ObservationsModule',15), ('Administrador','StatisticsModule',15),
                  ('Administrador','ReportsModule',15), ('Administrador','CommunicationsModule',15),
                  ('Administrador','ForumModule',15), ('Administrador','SecurityModule',15),
                  ('Administrador','WebpageModule',15),
                  ('Rector','EnrollmentProcess',1), ('Rector','AdmissionDecision',4),
                  ('Secretaría','EnrollmentProcess',7), ('Secretaria','EnrollmentProcess',7)
                ) AS v(role, key, perm) ON r."Name" = v.role
                ON CONFLICT ("RoleId", "TableName") DO NOTHING;

                INSERT INTO "RolePermission" ("RoleId", "TableName", "Permissions")
                SELECT r."Id", k.key, 1
                FROM "AspNetRoles" r
                CROSS JOIN (VALUES ('GradesModule'), ('MySchedule'), ('ObservationsModule'),
                                   ('CommunicationsModule'), ('ForumModule')) AS k(key)
                WHERE r."Name" IN ('Rector','Secretaría','Secretaria','Profesor','Estudiante','Acudiente','Padre')
                ON CONFLICT ("RoleId", "TableName") DO NOTHING;

                INSERT INTO "RolePermission" ("RoleId", "TableName", "Permissions")
                SELECT r."Id", 'ReportsModule', 1
                FROM "AspNetRoles" r
                WHERE r."Name" IN ('Estudiante','Acudiente','Padre')
                ON CONFLICT ("RoleId", "TableName") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "RolePermission"
                WHERE "TableName" IN (
                  'AdministrationModule','EnrollmentProcess','AdmissionDecision','MySchedule','GradesModule',
                  'ObservationsModule','StatisticsModule','ReportsModule','CommunicationsModule','ForumModule',
                  'SecurityModule','WebpageModule');
                """);
        }
    }
}

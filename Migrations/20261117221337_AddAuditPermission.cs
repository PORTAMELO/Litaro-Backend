using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Litaro.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditPermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO "RolePermission" ("RoleId","TableName","Permissions")
                SELECT r."Id", 'AuditModule', 1
                FROM "AspNetRoles" r
                WHERE r."Name" = 'Administrador'
                ON CONFLICT ("RoleId","TableName") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "RolePermission" WHERE "TableName" = 'AuditModule';
                """);
        }
    }
}

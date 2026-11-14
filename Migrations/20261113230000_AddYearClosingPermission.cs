using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Litaro.Migrations
{
    /// <inheritdoc />
    public partial class AddYearClosingPermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO "RolePermission" ("RoleId","TableName","Permissions")
                SELECT r."Id", 'YearClosing', 5
                FROM "AspNetRoles" r
                WHERE r."NormalizedName" IN ('COORDINADOR ACADÉMICO','COORDINADOR ACADEMICO')
                ON CONFLICT ("RoleId","TableName") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "RolePermission" WHERE "TableName" = 'YearClosing';
                """);
        }
    }
}

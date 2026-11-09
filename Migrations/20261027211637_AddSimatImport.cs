using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Litaro.Migrations
{
    /// <inheritdoc />
    public partial class AddSimatImport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BloodType",
                schema: "public",
                table: "Student",
                type: "character varying(5)",
                maxLength: 5,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Eps",
                schema: "public",
                table: "Student",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasPiar",
                schema: "public",
                table: "Student",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Neighborhood",
                schema: "public",
                table: "Student",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "SimatPerId",
                schema: "public",
                table: "Student",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "SimatCode",
                schema: "public",
                table: "Grade",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StatusReason",
                schema: "public",
                table: "Enrollment",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Shift",
                schema: "public",
                table: "Classroom",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Student_SimatPerId",
                schema: "public",
                table: "Student",
                column: "SimatPerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Grade_SimatCode",
                schema: "public",
                table: "Grade",
                column: "SimatCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Student_SimatPerId",
                schema: "public",
                table: "Student");

            migrationBuilder.DropIndex(
                name: "IX_Grade_SimatCode",
                schema: "public",
                table: "Grade");

            migrationBuilder.DropColumn(
                name: "BloodType",
                schema: "public",
                table: "Student");

            migrationBuilder.DropColumn(
                name: "Eps",
                schema: "public",
                table: "Student");

            migrationBuilder.DropColumn(
                name: "HasPiar",
                schema: "public",
                table: "Student");

            migrationBuilder.DropColumn(
                name: "Neighborhood",
                schema: "public",
                table: "Student");

            migrationBuilder.DropColumn(
                name: "SimatPerId",
                schema: "public",
                table: "Student");

            migrationBuilder.DropColumn(
                name: "SimatCode",
                schema: "public",
                table: "Grade");

            migrationBuilder.DropColumn(
                name: "StatusReason",
                schema: "public",
                table: "Enrollment");

            migrationBuilder.DropColumn(
                name: "Shift",
                schema: "public",
                table: "Classroom");
        }
    }
}

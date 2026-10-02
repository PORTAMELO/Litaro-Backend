using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Litaro.Migrations
{
    public partial class ScheduleModule : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Schedule_AssignmentId_Weekday_StartTime",
                schema: "public",
                table: "Schedule");

            migrationBuilder.Sql(@"ALTER TABLE ""Teacher"" ADD COLUMN IF NOT EXISTS ""Active"" boolean NOT NULL DEFAULT TRUE;");

            migrationBuilder.Sql(@"ALTER TABLE ""Student"" ADD COLUMN IF NOT EXISTS ""Active"" boolean NOT NULL DEFAULT TRUE;");

            migrationBuilder.AlterColumn<int>(
                name: "AssignmentId",
                schema: "public",
                table: "Schedule",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "CampusId",
                schema: "public",
                table: "Schedule",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ClassroomId",
                schema: "public",
                table: "Schedule",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SpaceId",
                schema: "public",
                table: "Schedule",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TeacherId",
                schema: "public",
                table: "Schedule",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                schema: "public",
                table: "Schedule",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Type",
                schema: "public",
                table: "Schedule",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "CLASS");

            migrationBuilder.AddColumn<short>(
                name: "YearId",
                schema: "public",
                table: "Schedule",
                type: "smallint",
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE ""Schedule"" s
                SET ""TeacherId""   = a.""TeacherId"",
                    ""ClassroomId"" = a.""ClassroomId"",
                    ""YearId""      = a.""YearId"",
                    ""CampusId""    = c.""CampusId"",
                    ""Type""        = 'CLASS'
                FROM ""AcademicAssignment"" a
                JOIN ""Classroom"" c ON c.""ClassroomId"" = a.""ClassroomId""
                WHERE a.""AssignmentId"" = s.""AssignmentId"";");

            migrationBuilder.AlterColumn<short>(
                name: "YearId",
                schema: "public",
                table: "Schedule",
                type: "smallint",
                nullable: false,
                oldClrType: typeof(short),
                oldType: "smallint",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "CampusId",
                schema: "public",
                table: "Schedule",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "TeacherId",
                schema: "public",
                table: "Schedule",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.Sql(@"ALTER TABLE ""Parent"" ADD COLUMN IF NOT EXISTS ""Active"" boolean NOT NULL DEFAULT TRUE;");

            migrationBuilder.AddColumn<short>(
                name: "ClassMinutes",
                schema: "public",
                table: "Classroom",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DirectorTeacherId",
                schema: "public",
                table: "Classroom",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Space",
                schema: "public",
                columns: table => new
                {
                    SpaceId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CampusId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Capacity = table.Column<short>(type: "smallint", nullable: true),
                    Active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Space", x => x.SpaceId);
                    table.CheckConstraint("CK_Space_Capacity", "\"Capacity\" IS NULL OR \"Capacity\" > 0");
                    table.ForeignKey(
                        name: "FK_Space_Campus_CampusId",
                        column: x => x.CampusId,
                        principalSchema: "public",
                        principalTable: "Campus",
                        principalColumn: "CampusId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudyPlan",
                schema: "public",
                columns: table => new
                {
                    StudyPlanId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    YearId = table.Column<short>(type: "smallint", nullable: false),
                    GradeId = table.Column<int>(type: "integer", nullable: false),
                    SubjectId = table.Column<int>(type: "integer", nullable: false),
                    WeeklyHours = table.Column<byte>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudyPlan", x => x.StudyPlanId);
                    table.CheckConstraint("CK_StudyPlan_WeeklyHours", "\"WeeklyHours\" BETWEEN 1 AND 40");
                    table.ForeignKey(
                        name: "FK_StudyPlan_AcademicYear_YearId",
                        column: x => x.YearId,
                        principalSchema: "public",
                        principalTable: "AcademicYear",
                        principalColumn: "YearId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudyPlan_Grade_GradeId",
                        column: x => x.GradeId,
                        principalSchema: "public",
                        principalTable: "Grade",
                        principalColumn: "GradeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudyPlan_Subject_SubjectId",
                        column: x => x.SubjectId,
                        principalSchema: "public",
                        principalTable: "Subject",
                        principalColumn: "SubjectId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TeacherAvailability",
                schema: "public",
                columns: table => new
                {
                    TeacherAvailabilityId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    YearId = table.Column<short>(type: "smallint", nullable: false),
                    TeacherId = table.Column<int>(type: "integer", nullable: false),
                    Weekday = table.Column<byte>(type: "smallint", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    CampusId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeacherAvailability", x => x.TeacherAvailabilityId);
                    table.CheckConstraint("CK_TeacherAvailability_Time", "\"EndTime\" > \"StartTime\"");
                    table.CheckConstraint("CK_TeacherAvailability_Weekday", "\"Weekday\" BETWEEN 1 AND 6");
                    table.ForeignKey(
                        name: "FK_TeacherAvailability_AcademicYear_YearId",
                        column: x => x.YearId,
                        principalSchema: "public",
                        principalTable: "AcademicYear",
                        principalColumn: "YearId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeacherAvailability_Campus_CampusId",
                        column: x => x.CampusId,
                        principalSchema: "public",
                        principalTable: "Campus",
                        principalColumn: "CampusId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeacherAvailability_Teacher_TeacherId",
                        column: x => x.TeacherId,
                        principalSchema: "public",
                        principalTable: "Teacher",
                        principalColumn: "TeacherId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Schedule_AssignmentId",
                schema: "public",
                table: "Schedule",
                column: "AssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Schedule_CampusId",
                schema: "public",
                table: "Schedule",
                column: "CampusId");

            migrationBuilder.CreateIndex(
                name: "IX_Schedule_ClassroomId",
                schema: "public",
                table: "Schedule",
                column: "ClassroomId");

            migrationBuilder.CreateIndex(
                name: "IX_Schedule_SpaceId",
                schema: "public",
                table: "Schedule",
                column: "SpaceId");

            migrationBuilder.CreateIndex(
                name: "IX_Schedule_TeacherId",
                schema: "public",
                table: "Schedule",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_Schedule_YearId_CampusId",
                schema: "public",
                table: "Schedule",
                columns: new[] { "YearId", "CampusId" });

            migrationBuilder.CreateIndex(
                name: "IX_Schedule_YearId_ClassroomId_Weekday",
                schema: "public",
                table: "Schedule",
                columns: new[] { "YearId", "ClassroomId", "Weekday" });

            migrationBuilder.CreateIndex(
                name: "IX_Schedule_YearId_SpaceId_Weekday",
                schema: "public",
                table: "Schedule",
                columns: new[] { "YearId", "SpaceId", "Weekday" });

            migrationBuilder.CreateIndex(
                name: "IX_Schedule_YearId_TeacherId_Weekday",
                schema: "public",
                table: "Schedule",
                columns: new[] { "YearId", "TeacherId", "Weekday" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Schedule_ClassFields",
                schema: "public",
                table: "Schedule",
                sql: "(\"Type\" = 'CLASS' AND \"AssignmentId\" IS NOT NULL AND \"ClassroomId\" IS NOT NULL) OR (\"Type\" <> 'CLASS' AND \"AssignmentId\" IS NULL AND \"ClassroomId\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Schedule_Time",
                schema: "public",
                table: "Schedule",
                sql: "\"EndTime\" > \"StartTime\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Schedule_Type",
                schema: "public",
                table: "Schedule",
                sql: "\"Type\" IN ('CLASS','PARENT_ATTENTION','ACCOMPANIMENT','MEETING','OTHER')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Schedule_Weekday",
                schema: "public",
                table: "Schedule",
                sql: "\"Weekday\" BETWEEN 1 AND 6");

            migrationBuilder.CreateIndex(
                name: "IX_Classroom_DirectorTeacherId",
                schema: "public",
                table: "Classroom",
                column: "DirectorTeacherId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Classroom_ClassMinutes",
                schema: "public",
                table: "Classroom",
                sql: "\"ClassMinutes\" IS NULL OR \"ClassMinutes\" BETWEEN 20 AND 180");

            migrationBuilder.CreateIndex(
                name: "UX_AcademicAssignment_Assignment_Teacher_Classroom",
                schema: "public",
                table: "AcademicAssignment",
                columns: new[] { "AssignmentId", "TeacherId", "ClassroomId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Space_CampusId_Name",
                schema: "public",
                table: "Space",
                columns: new[] { "CampusId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudyPlan_GradeId",
                schema: "public",
                table: "StudyPlan",
                column: "GradeId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyPlan_SubjectId",
                schema: "public",
                table: "StudyPlan",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyPlan_YearId_GradeId_SubjectId",
                schema: "public",
                table: "StudyPlan",
                columns: new[] { "YearId", "GradeId", "SubjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeacherAvailability_CampusId",
                schema: "public",
                table: "TeacherAvailability",
                column: "CampusId");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherAvailability_TeacherId",
                schema: "public",
                table: "TeacherAvailability",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherAvailability_YearId_TeacherId_Weekday",
                schema: "public",
                table: "TeacherAvailability",
                columns: new[] { "YearId", "TeacherId", "Weekday" });

            migrationBuilder.AddForeignKey(
                name: "FK_Classroom_Teacher_DirectorTeacherId",
                schema: "public",
                table: "Classroom",
                column: "DirectorTeacherId",
                principalSchema: "public",
                principalTable: "Teacher",
                principalColumn: "TeacherId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Schedule_AcademicYear_YearId",
                schema: "public",
                table: "Schedule",
                column: "YearId",
                principalSchema: "public",
                principalTable: "AcademicYear",
                principalColumn: "YearId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Schedule_Campus_CampusId",
                schema: "public",
                table: "Schedule",
                column: "CampusId",
                principalSchema: "public",
                principalTable: "Campus",
                principalColumn: "CampusId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Schedule_Classroom_ClassroomId",
                schema: "public",
                table: "Schedule",
                column: "ClassroomId",
                principalSchema: "public",
                principalTable: "Classroom",
                principalColumn: "ClassroomId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Schedule_Space_SpaceId",
                schema: "public",
                table: "Schedule",
                column: "SpaceId",
                principalSchema: "public",
                principalTable: "Space",
                principalColumn: "SpaceId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Schedule_Teacher_TeacherId",
                schema: "public",
                table: "Schedule",
                column: "TeacherId",
                principalSchema: "public",
                principalTable: "Teacher",
                principalColumn: "TeacherId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(@"
                ALTER TABLE ""Schedule""
                ADD CONSTRAINT ""FK_Schedule_Assignment_Consistent""
                FOREIGN KEY (""AssignmentId"", ""TeacherId"", ""ClassroomId"")
                REFERENCES ""AcademicAssignment"" (""AssignmentId"", ""TeacherId"", ""ClassroomId"")
                ON UPDATE CASCADE ON DELETE RESTRICT;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"ALTER TABLE ""Schedule"" DROP CONSTRAINT IF EXISTS ""FK_Schedule_Assignment_Consistent"";");

            migrationBuilder.DropForeignKey(
                name: "FK_Classroom_Teacher_DirectorTeacherId",
                schema: "public",
                table: "Classroom");

            migrationBuilder.DropForeignKey(
                name: "FK_Schedule_AcademicYear_YearId",
                schema: "public",
                table: "Schedule");

            migrationBuilder.DropForeignKey(
                name: "FK_Schedule_Campus_CampusId",
                schema: "public",
                table: "Schedule");

            migrationBuilder.DropForeignKey(
                name: "FK_Schedule_Classroom_ClassroomId",
                schema: "public",
                table: "Schedule");

            migrationBuilder.DropForeignKey(
                name: "FK_Schedule_Space_SpaceId",
                schema: "public",
                table: "Schedule");

            migrationBuilder.DropForeignKey(
                name: "FK_Schedule_Teacher_TeacherId",
                schema: "public",
                table: "Schedule");

            migrationBuilder.DropTable(
                name: "Space",
                schema: "public");

            migrationBuilder.DropTable(
                name: "StudyPlan",
                schema: "public");

            migrationBuilder.DropTable(
                name: "TeacherAvailability",
                schema: "public");

            migrationBuilder.DropIndex(
                name: "IX_Schedule_AssignmentId",
                schema: "public",
                table: "Schedule");

            migrationBuilder.DropIndex(
                name: "IX_Schedule_CampusId",
                schema: "public",
                table: "Schedule");

            migrationBuilder.DropIndex(
                name: "IX_Schedule_ClassroomId",
                schema: "public",
                table: "Schedule");

            migrationBuilder.DropIndex(
                name: "IX_Schedule_SpaceId",
                schema: "public",
                table: "Schedule");

            migrationBuilder.DropIndex(
                name: "IX_Schedule_TeacherId",
                schema: "public",
                table: "Schedule");

            migrationBuilder.DropIndex(
                name: "IX_Schedule_YearId_CampusId",
                schema: "public",
                table: "Schedule");

            migrationBuilder.DropIndex(
                name: "IX_Schedule_YearId_ClassroomId_Weekday",
                schema: "public",
                table: "Schedule");

            migrationBuilder.DropIndex(
                name: "IX_Schedule_YearId_SpaceId_Weekday",
                schema: "public",
                table: "Schedule");

            migrationBuilder.DropIndex(
                name: "IX_Schedule_YearId_TeacherId_Weekday",
                schema: "public",
                table: "Schedule");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Schedule_ClassFields",
                schema: "public",
                table: "Schedule");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Schedule_Time",
                schema: "public",
                table: "Schedule");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Schedule_Type",
                schema: "public",
                table: "Schedule");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Schedule_Weekday",
                schema: "public",
                table: "Schedule");

            migrationBuilder.DropIndex(
                name: "IX_Classroom_DirectorTeacherId",
                schema: "public",
                table: "Classroom");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Classroom_ClassMinutes",
                schema: "public",
                table: "Classroom");

            migrationBuilder.DropIndex(
                name: "UX_AcademicAssignment_Assignment_Teacher_Classroom",
                schema: "public",
                table: "AcademicAssignment");

            migrationBuilder.DropColumn(
                name: "CampusId",
                schema: "public",
                table: "Schedule");

            migrationBuilder.DropColumn(
                name: "ClassroomId",
                schema: "public",
                table: "Schedule");

            migrationBuilder.DropColumn(
                name: "SpaceId",
                schema: "public",
                table: "Schedule");

            migrationBuilder.DropColumn(
                name: "TeacherId",
                schema: "public",
                table: "Schedule");

            migrationBuilder.DropColumn(
                name: "Title",
                schema: "public",
                table: "Schedule");

            migrationBuilder.DropColumn(
                name: "Type",
                schema: "public",
                table: "Schedule");

            migrationBuilder.DropColumn(
                name: "YearId",
                schema: "public",
                table: "Schedule");

            migrationBuilder.DropColumn(
                name: "ClassMinutes",
                schema: "public",
                table: "Classroom");

            migrationBuilder.DropColumn(
                name: "DirectorTeacherId",
                schema: "public",
                table: "Classroom");

            migrationBuilder.AlterColumn<int>(
                name: "AssignmentId",
                schema: "public",
                table: "Schedule",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Schedule_AssignmentId_Weekday_StartTime",
                schema: "public",
                table: "Schedule",
                columns: new[] { "AssignmentId", "Weekday", "StartTime" },
                unique: true);
        }
    }
}

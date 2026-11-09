using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Litaro.Migrations
{
    /// <inheritdoc />
    public partial class AddEnrollmentProcess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdmissionRequest",
                schema: "public",
                columns: table => new
                {
                    AdmissionRequestId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    YearId = table.Column<short>(type: "smallint", nullable: false),
                    GradeId = table.Column<int>(type: "integer", nullable: false),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DocumentType = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    DocumentNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BirthDate = table.Column<DateOnly>(type: "date", nullable: false),
                    GuardianName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    GuardianPhone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false, defaultValue: "PENDING"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: false),
                    WaitlistedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecidedById = table.Column<int>(type: "integer", nullable: true),
                    DecisionNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdmissionRequest", x => x.AdmissionRequestId);
                    table.CheckConstraint("CK_AdmissionRequest_Status", "\"Status\" IN ('PENDING','APPROVED','REJECTED','WAITLIST')");
                    table.ForeignKey(
                        name: "FK_AdmissionRequest_AcademicYear_YearId",
                        column: x => x.YearId,
                        principalSchema: "public",
                        principalTable: "AcademicYear",
                        principalColumn: "YearId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AdmissionRequest_Grade_GradeId",
                        column: x => x.GradeId,
                        principalSchema: "public",
                        principalTable: "Grade",
                        principalColumn: "GradeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AdmissionRequest_User_CreatedById",
                        column: x => x.CreatedById,
                        principalSchema: "public",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AdmissionRequest_User_DecidedById",
                        column: x => x.DecidedById,
                        principalSchema: "public",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EnrollmentPeriod",
                schema: "public",
                columns: table => new
                {
                    EnrollmentPeriodId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    YearId = table.Column<short>(type: "smallint", nullable: false),
                    PreEnrollmentStart = table.Column<DateOnly>(type: "date", nullable: false),
                    PreEnrollmentEnd = table.Column<DateOnly>(type: "date", nullable: false),
                    AdmissionStart = table.Column<DateOnly>(type: "date", nullable: false),
                    AdmissionEnd = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnrollmentPeriod", x => x.EnrollmentPeriodId);
                    table.CheckConstraint("CK_EnrollmentPeriod_Admission", "\"AdmissionEnd\" >= \"AdmissionStart\"");
                    table.CheckConstraint("CK_EnrollmentPeriod_PreEnrollment", "\"PreEnrollmentEnd\" >= \"PreEnrollmentStart\"");
                    table.ForeignKey(
                        name: "FK_EnrollmentPeriod_AcademicYear_YearId",
                        column: x => x.YearId,
                        principalSchema: "public",
                        principalTable: "AcademicYear",
                        principalColumn: "YearId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PreEnrollment",
                schema: "public",
                columns: table => new
                {
                    PreEnrollmentId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    YearId = table.Column<short>(type: "smallint", nullable: false),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    GradeId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false, defaultValue: "RESERVED"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RespondedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResponseNote = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreEnrollment", x => x.PreEnrollmentId);
                    table.CheckConstraint("CK_PreEnrollment_Status", "\"Status\" IN ('RESERVED','CONFIRMED','DECLINED','LOST')");
                    table.ForeignKey(
                        name: "FK_PreEnrollment_AcademicYear_YearId",
                        column: x => x.YearId,
                        principalSchema: "public",
                        principalTable: "AcademicYear",
                        principalColumn: "YearId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PreEnrollment_Grade_GradeId",
                        column: x => x.GradeId,
                        principalSchema: "public",
                        principalTable: "Grade",
                        principalColumn: "GradeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PreEnrollment_Student_StudentId",
                        column: x => x.StudentId,
                        principalSchema: "public",
                        principalTable: "Student",
                        principalColumn: "StudentId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AdmissionRequestEvent",
                schema: "public",
                columns: table => new
                {
                    AdmissionRequestEventId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AdmissionRequestId = table.Column<int>(type: "integer", nullable: false),
                    FromStatus = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: true),
                    ToStatus = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdmissionRequestEvent", x => x.AdmissionRequestEventId);
                    table.ForeignKey(
                        name: "FK_AdmissionRequestEvent_AdmissionRequest_AdmissionRequestId",
                        column: x => x.AdmissionRequestId,
                        principalSchema: "public",
                        principalTable: "AdmissionRequest",
                        principalColumn: "AdmissionRequestId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AdmissionRequestEvent_User_UserId",
                        column: x => x.UserId,
                        principalSchema: "public",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SimatAlert",
                schema: "public",
                columns: table => new
                {
                    SimatAlertId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    YearId = table.Column<short>(type: "smallint", nullable: true),
                    StudentId = table.Column<int>(type: "integer", nullable: true),
                    AdmissionRequestId = table.Column<int>(type: "integer", nullable: true),
                    Message = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    Status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "PENDING"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolvedById = table.Column<int>(type: "integer", nullable: true),
                    ResolutionNote = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SimatAlert", x => x.SimatAlertId);
                    table.CheckConstraint("CK_SimatAlert_Status", "\"Status\" IN ('PENDING','DONE')");
                    table.ForeignKey(
                        name: "FK_SimatAlert_AcademicYear_YearId",
                        column: x => x.YearId,
                        principalSchema: "public",
                        principalTable: "AcademicYear",
                        principalColumn: "YearId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SimatAlert_AdmissionRequest_AdmissionRequestId",
                        column: x => x.AdmissionRequestId,
                        principalSchema: "public",
                        principalTable: "AdmissionRequest",
                        principalColumn: "AdmissionRequestId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SimatAlert_Student_StudentId",
                        column: x => x.StudentId,
                        principalSchema: "public",
                        principalTable: "Student",
                        principalColumn: "StudentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SimatAlert_User_ResolvedById",
                        column: x => x.ResolvedById,
                        principalSchema: "public",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdmissionRequest_CreatedById",
                schema: "public",
                table: "AdmissionRequest",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_AdmissionRequest_DecidedById",
                schema: "public",
                table: "AdmissionRequest",
                column: "DecidedById");

            migrationBuilder.CreateIndex(
                name: "IX_AdmissionRequest_GradeId",
                schema: "public",
                table: "AdmissionRequest",
                column: "GradeId");

            migrationBuilder.CreateIndex(
                name: "IX_AdmissionRequest_YearId_DocumentNumber",
                schema: "public",
                table: "AdmissionRequest",
                columns: new[] { "YearId", "DocumentNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_AdmissionRequest_YearId_Status",
                schema: "public",
                table: "AdmissionRequest",
                columns: new[] { "YearId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AdmissionRequestEvent_AdmissionRequestId",
                schema: "public",
                table: "AdmissionRequestEvent",
                column: "AdmissionRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_AdmissionRequestEvent_UserId",
                schema: "public",
                table: "AdmissionRequestEvent",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_EnrollmentPeriod_YearId",
                schema: "public",
                table: "EnrollmentPeriod",
                column: "YearId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PreEnrollment_GradeId",
                schema: "public",
                table: "PreEnrollment",
                column: "GradeId");

            migrationBuilder.CreateIndex(
                name: "IX_PreEnrollment_StudentId",
                schema: "public",
                table: "PreEnrollment",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_PreEnrollment_YearId_Status",
                schema: "public",
                table: "PreEnrollment",
                columns: new[] { "YearId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PreEnrollment_YearId_StudentId",
                schema: "public",
                table: "PreEnrollment",
                columns: new[] { "YearId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SimatAlert_AdmissionRequestId",
                schema: "public",
                table: "SimatAlert",
                column: "AdmissionRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_SimatAlert_ResolvedById",
                schema: "public",
                table: "SimatAlert",
                column: "ResolvedById");

            migrationBuilder.CreateIndex(
                name: "IX_SimatAlert_Status_CreatedAt",
                schema: "public",
                table: "SimatAlert",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SimatAlert_StudentId",
                schema: "public",
                table: "SimatAlert",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_SimatAlert_Type_StudentId",
                schema: "public",
                table: "SimatAlert",
                columns: new[] { "Type", "StudentId" });

            migrationBuilder.CreateIndex(
                name: "IX_SimatAlert_YearId",
                schema: "public",
                table: "SimatAlert",
                column: "YearId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdmissionRequestEvent",
                schema: "public");

            migrationBuilder.DropTable(
                name: "EnrollmentPeriod",
                schema: "public");

            migrationBuilder.DropTable(
                name: "PreEnrollment",
                schema: "public");

            migrationBuilder.DropTable(
                name: "SimatAlert",
                schema: "public");

            migrationBuilder.DropTable(
                name: "AdmissionRequest",
                schema: "public");
        }
    }
}

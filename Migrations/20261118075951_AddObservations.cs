using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Litaro.Migrations
{
    /// <inheritdoc />
    public partial class AddObservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Observation",
                schema: "public",
                columns: table => new
                {
                    ObservationId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EnrollmentId = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    IsAcademic = table.Column<bool>(type: "boolean", nullable: false),
                    IsCoexistence = table.Column<bool>(type: "boolean", nullable: false),
                    CoexistenceType = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    SubjectId = table.Column<int>(type: "integer", nullable: true),
                    Statement = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RecordedById = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Observation", x => x.ObservationId);
                    table.CheckConstraint("CK_Observation_CoexistenceType", "(\"IsCoexistence\" AND \"CoexistenceType\" IN ('I','II','III')) OR (NOT \"IsCoexistence\" AND \"CoexistenceType\" IS NULL)");
                    table.CheckConstraint("CK_Observation_Kind", "\"IsAcademic\" OR \"IsCoexistence\"");
                    table.ForeignKey(
                        name: "FK_Observation_Enrollment_EnrollmentId",
                        column: x => x.EnrollmentId,
                        principalSchema: "public",
                        principalTable: "Enrollment",
                        principalColumn: "EnrollmentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Observation_Subject_SubjectId",
                        column: x => x.SubjectId,
                        principalSchema: "public",
                        principalTable: "Subject",
                        principalColumn: "SubjectId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Observation_User_RecordedById",
                        column: x => x.RecordedById,
                        principalSchema: "public",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ObservationCommitment",
                schema: "public",
                columns: table => new
                {
                    ObservationCommitmentId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ObservationId = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false, defaultValue: "PENDING"),
                    FollowUpDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Note = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreatedById = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ObservationCommitment", x => x.ObservationCommitmentId);
                    table.CheckConstraint("CK_ObservationCommitment_Status", "\"Status\" IN ('PENDING','MET','NOT_MET','OBSERVATION')");
                    table.ForeignKey(
                        name: "FK_ObservationCommitment_Observation_ObservationId",
                        column: x => x.ObservationId,
                        principalSchema: "public",
                        principalTable: "Observation",
                        principalColumn: "ObservationId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ObservationCommitment_User_CreatedById",
                        column: x => x.CreatedById,
                        principalSchema: "public",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ObservationEvent",
                schema: "public",
                columns: table => new
                {
                    ObservationEventId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ObservationId = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: false),
                    Detail = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ObservationEvent", x => x.ObservationEventId);
                    table.ForeignKey(
                        name: "FK_ObservationEvent_Observation_ObservationId",
                        column: x => x.ObservationId,
                        principalSchema: "public",
                        principalTable: "Observation",
                        principalColumn: "ObservationId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ObservationEvent_User_UserId",
                        column: x => x.UserId,
                        principalSchema: "public",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Observation_EnrollmentId_Date",
                schema: "public",
                table: "Observation",
                columns: new[] { "EnrollmentId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_Observation_RecordedById",
                schema: "public",
                table: "Observation",
                column: "RecordedById");

            migrationBuilder.CreateIndex(
                name: "IX_Observation_SubjectId",
                schema: "public",
                table: "Observation",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ObservationCommitment_CreatedById",
                schema: "public",
                table: "ObservationCommitment",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_ObservationCommitment_ObservationId",
                schema: "public",
                table: "ObservationCommitment",
                column: "ObservationId");

            migrationBuilder.CreateIndex(
                name: "IX_ObservationEvent_ObservationId_CreatedAt",
                schema: "public",
                table: "ObservationEvent",
                columns: new[] { "ObservationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ObservationEvent_UserId",
                schema: "public",
                table: "ObservationEvent",
                column: "UserId");

            migrationBuilder.Sql("""
                INSERT INTO "RolePermission" ("RoleId","TableName","Permissions")
                SELECT r."Id", k.key, 15
                FROM "AspNetRoles" r
                CROSS JOIN (VALUES ('ObservationRecord'), ('ObservationConsolidated')) AS k(key)
                WHERE r."Name" = 'Administrador'
                ON CONFLICT ("RoleId","TableName") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "RolePermission" WHERE "TableName" IN ('ObservationRecord','ObservationConsolidated');
                """);

            migrationBuilder.DropTable(
                name: "ObservationCommitment",
                schema: "public");

            migrationBuilder.DropTable(
                name: "ObservationEvent",
                schema: "public");

            migrationBuilder.DropTable(
                name: "Observation",
                schema: "public");
        }
    }
}

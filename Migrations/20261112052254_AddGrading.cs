using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Litaro.Migrations
{
    /// <inheritdoc />
    public partial class AddGrading : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Document",
                schema: "public",
                columns: table => new
                {
                    DocumentId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Module = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    DocumentType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    EntityTable = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EntityId = table.Column<int>(type: "integer", nullable: false),
                    YearId = table.Column<short>(type: "smallint", nullable: true),
                    StoragePath = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    FileName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    IsCurrent = table.Column<bool>(type: "boolean", nullable: false),
                    UploadedById = table.Column<int>(type: "integer", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReplacedById = table.Column<int>(type: "integer", nullable: true),
                    ReplacedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Document", x => x.DocumentId);
                    table.ForeignKey(
                        name: "FK_Document_AcademicYear_YearId",
                        column: x => x.YearId,
                        principalSchema: "public",
                        principalTable: "AcademicYear",
                        principalColumn: "YearId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Document_User_ReplacedById",
                        column: x => x.ReplacedById,
                        principalSchema: "public",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Document_User_UploadedById",
                        column: x => x.UploadedById,
                        principalSchema: "public",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GradingDeadline",
                schema: "public",
                columns: table => new
                {
                    GradingDeadlineId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    YearId = table.Column<short>(type: "smallint", nullable: false),
                    PeriodId = table.Column<short>(type: "smallint", nullable: true),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DueAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedById = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GradingDeadline", x => x.GradingDeadlineId);
                    table.CheckConstraint("CK_GradingDeadline_Kind", "\"Kind\" IN ('PLANNER','DESIGN_LOCK')");
                    table.CheckConstraint("CK_GradingDeadline_Period", "(\"Kind\" = 'PLANNER') = (\"PeriodId\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_GradingDeadline_AcademicPeriod_PeriodId",
                        column: x => x.PeriodId,
                        principalSchema: "public",
                        principalTable: "AcademicPeriod",
                        principalColumn: "PeriodId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GradingDeadline_AcademicYear_YearId",
                        column: x => x.YearId,
                        principalSchema: "public",
                        principalTable: "AcademicYear",
                        principalColumn: "YearId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GradingDeadline_User_UpdatedById",
                        column: x => x.UpdatedById,
                        principalSchema: "public",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GradingScheme",
                schema: "public",
                columns: table => new
                {
                    GradingSchemeId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClassroomId = table.Column<int>(type: "integer", nullable: false),
                    SubjectId = table.Column<int>(type: "integer", nullable: false),
                    PeriodId = table.Column<short>(type: "smallint", nullable: false),
                    Mode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    UpdatedById = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GradingScheme", x => x.GradingSchemeId);
                    table.CheckConstraint("CK_GradingScheme_Mode", "\"Mode\" IN ('WEIGHTED','AVERAGE')");
                    table.ForeignKey(
                        name: "FK_GradingScheme_AcademicPeriod_PeriodId",
                        column: x => x.PeriodId,
                        principalSchema: "public",
                        principalTable: "AcademicPeriod",
                        principalColumn: "PeriodId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GradingScheme_Classroom_ClassroomId",
                        column: x => x.ClassroomId,
                        principalSchema: "public",
                        principalTable: "Classroom",
                        principalColumn: "ClassroomId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GradingScheme_Subject_SubjectId",
                        column: x => x.SubjectId,
                        principalSchema: "public",
                        principalTable: "Subject",
                        principalColumn: "SubjectId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GradingScheme_User_UpdatedById",
                        column: x => x.UpdatedById,
                        principalSchema: "public",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GradeActivity",
                schema: "public",
                columns: table => new
                {
                    GradeActivityId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GradingSchemeId = table.Column<int>(type: "integer", nullable: false),
                    Component = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Competencies = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Weight = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GradeActivity", x => x.GradeActivityId);
                    table.CheckConstraint("CK_GradeActivity_Component", "\"Component\" IN ('HETERO','SABER','SELF','PEER')");
                    table.CheckConstraint("CK_GradeActivity_Weight", "\"Weight\" IS NULL OR (\"Weight\" > 0 AND \"Weight\" <= 100)");
                    table.ForeignKey(
                        name: "FK_GradeActivity_GradingScheme_GradingSchemeId",
                        column: x => x.GradingSchemeId,
                        principalSchema: "public",
                        principalTable: "GradingScheme",
                        principalColumn: "GradingSchemeId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GradeActivity_User_CreatedById",
                        column: x => x.CreatedById,
                        principalSchema: "public",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ActivityScore",
                schema: "public",
                columns: table => new
                {
                    ActivityScoreId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GradeActivityId = table.Column<int>(type: "integer", nullable: false),
                    EnrollmentId = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<decimal>(type: "numeric(3,1)", nullable: false),
                    RecordedById = table.Column<int>(type: "integer", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityScore", x => x.ActivityScoreId);
                    table.CheckConstraint("CK_ActivityScore_Value", "\"Value\" BETWEEN 10 AND 50");
                    table.ForeignKey(
                        name: "FK_ActivityScore_Enrollment_EnrollmentId",
                        column: x => x.EnrollmentId,
                        principalSchema: "public",
                        principalTable: "Enrollment",
                        principalColumn: "EnrollmentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ActivityScore_GradeActivity_GradeActivityId",
                        column: x => x.GradeActivityId,
                        principalSchema: "public",
                        principalTable: "GradeActivity",
                        principalColumn: "GradeActivityId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ActivityScore_User_RecordedById",
                        column: x => x.RecordedById,
                        principalSchema: "public",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ActivityScore_User_UpdatedById",
                        column: x => x.UpdatedById,
                        principalSchema: "public",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityScore_EnrollmentId",
                schema: "public",
                table: "ActivityScore",
                column: "EnrollmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityScore_GradeActivityId_EnrollmentId",
                schema: "public",
                table: "ActivityScore",
                columns: new[] { "GradeActivityId", "EnrollmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ActivityScore_RecordedById",
                schema: "public",
                table: "ActivityScore",
                column: "RecordedById");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityScore_UpdatedById",
                schema: "public",
                table: "ActivityScore",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Document_DocumentType_EntityTable_EntityId",
                schema: "public",
                table: "Document",
                columns: new[] { "DocumentType", "EntityTable", "EntityId" },
                unique: true,
                filter: "\"IsCurrent\"");

            migrationBuilder.CreateIndex(
                name: "IX_Document_ReplacedById",
                schema: "public",
                table: "Document",
                column: "ReplacedById");

            migrationBuilder.CreateIndex(
                name: "IX_Document_StoragePath",
                schema: "public",
                table: "Document",
                column: "StoragePath",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Document_UploadedById",
                schema: "public",
                table: "Document",
                column: "UploadedById");

            migrationBuilder.CreateIndex(
                name: "IX_Document_YearId",
                schema: "public",
                table: "Document",
                column: "YearId");

            migrationBuilder.CreateIndex(
                name: "IX_GradeActivity_CreatedById",
                schema: "public",
                table: "GradeActivity",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_GradeActivity_GradingSchemeId",
                schema: "public",
                table: "GradeActivity",
                column: "GradingSchemeId");

            migrationBuilder.CreateIndex(
                name: "IX_GradeActivity_GradingSchemeId_Component",
                schema: "public",
                table: "GradeActivity",
                columns: new[] { "GradingSchemeId", "Component" },
                unique: true,
                filter: "\"Component\" <> 'HETERO'");

            migrationBuilder.CreateIndex(
                name: "IX_GradingDeadline_PeriodId",
                schema: "public",
                table: "GradingDeadline",
                column: "PeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_GradingDeadline_UpdatedById",
                schema: "public",
                table: "GradingDeadline",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_GradingDeadline_YearId_Kind_PeriodId",
                schema: "public",
                table: "GradingDeadline",
                columns: new[] { "YearId", "Kind", "PeriodId" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_GradingScheme_ClassroomId_SubjectId_PeriodId",
                schema: "public",
                table: "GradingScheme",
                columns: new[] { "ClassroomId", "SubjectId", "PeriodId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GradingScheme_PeriodId",
                schema: "public",
                table: "GradingScheme",
                column: "PeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_GradingScheme_SubjectId",
                schema: "public",
                table: "GradingScheme",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_GradingScheme_UpdatedById",
                schema: "public",
                table: "GradingScheme",
                column: "UpdatedById");

            migrationBuilder.Sql("""
                INSERT INTO "NotificationType" ("Code","Name","Category","Template","Active")
                SELECT v.code, v.name, 'Notas', v.template, true
                FROM (VALUES
                    ('PLANNER_OVERDUE','Plan de estudio vencido','{docente} no cargó el plan de estudio de: {asignaciones}. El plazo venció el {fecha}.'),
                    ('PLANNER_REQUEST','Solicitud de plan de estudio','{docente} solicita gestionar la carga del plan de estudio de {materia} · {curso}.')
                ) AS v(code,name,template)
                WHERE NOT EXISTS (SELECT 1 FROM "NotificationType" t WHERE t."Code" = v.code);

                INSERT INTO "NotificationTypeRecipient" ("NotificationTypeId","RoleId")
                SELECT t."NotificationTypeId", r."Id"
                FROM "NotificationType" t CROSS JOIN "AspNetRoles" r
                WHERE t."Code" IN ('PLANNER_OVERDUE','PLANNER_REQUEST') AND r."Name" = 'Administrador'
                  AND NOT EXISTS (SELECT 1 FROM "NotificationTypeRecipient" x
                                  WHERE x."NotificationTypeId" = t."NotificationTypeId" AND x."RoleId" = r."Id");

                INSERT INTO "RolePermission" ("RoleId","TableName","Permissions")
                SELECT r."Id", v.key, v.perm
                FROM "AspNetRoles" r
                JOIN (VALUES ('Administrador','GradeBook',15), ('Administrador','GradeDesign',15),
                             ('Administrador','StudyPlanner',15), ('Administrador','StudyPlannerPanel',15),
                             ('Administrador','GradeSettings',15),
                             ('Profesor','GradeBook',3), ('Profesor','GradeDesign',10),
                             ('Profesor','StudyPlanner',2)) AS v(role,key,perm)
                  ON r."Name" = v.role
                ON CONFLICT ("RoleId","TableName") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "RolePermission"
                WHERE "TableName" IN ('GradeBook','GradeDesign','StudyPlanner','StudyPlannerPanel','GradeSettings');

                DELETE FROM "NotificationRead" WHERE "NotificationId" IN (
                    SELECT n."NotificationId" FROM "Notification" n
                    JOIN "NotificationType" t ON t."NotificationTypeId" = n."NotificationTypeId"
                    WHERE t."Code" IN ('PLANNER_OVERDUE','PLANNER_REQUEST'));

                DELETE FROM "Notification" WHERE "NotificationTypeId" IN (
                    SELECT "NotificationTypeId" FROM "NotificationType" WHERE "Code" IN ('PLANNER_OVERDUE','PLANNER_REQUEST'));

                DELETE FROM "NotificationTypeRecipient" WHERE "NotificationTypeId" IN (
                    SELECT "NotificationTypeId" FROM "NotificationType" WHERE "Code" IN ('PLANNER_OVERDUE','PLANNER_REQUEST'));

                DELETE FROM "NotificationType" WHERE "Code" IN ('PLANNER_OVERDUE','PLANNER_REQUEST');

                DELETE FROM "JobRun" WHERE "JobName" LIKE 'PlannerOverdue:%';
                """);

            migrationBuilder.DropTable(
                name: "ActivityScore",
                schema: "public");

            migrationBuilder.DropTable(
                name: "Document",
                schema: "public");

            migrationBuilder.DropTable(
                name: "GradingDeadline",
                schema: "public");

            migrationBuilder.DropTable(
                name: "GradeActivity",
                schema: "public");

            migrationBuilder.DropTable(
                name: "GradingScheme",
                schema: "public");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Litaro.Migrations
{
    /// <inheritdoc />
    public partial class AddCitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RecipientUserId",
                schema: "public",
                table: "Notification",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Citation",
                schema: "public",
                columns: table => new
                {
                    CitationId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    ParentId = table.Column<int>(type: "integer", nullable: true),
                    ObservationId = table.Column<int>(type: "integer", nullable: true),
                    Origin = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ResponsibleUserId = table.Column<int>(type: "integer", nullable: true),
                    ScheduledDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ScheduledTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    ScheduleId = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false, defaultValue: "TO_SCHEDULE"),
                    StudentInformed = table.Column<bool>(type: "boolean", nullable: false),
                    ParentInformed = table.Column<bool>(type: "boolean", nullable: false),
                    Attended = table.Column<bool>(type: "boolean", nullable: false),
                    RescheduledFromId = table.Column<int>(type: "integer", nullable: true),
                    Note = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreatedById = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Citation", x => x.CitationId);
                    table.CheckConstraint("CK_Citation_Origin", "\"Origin\" IN ('AUTO','MANUAL')");
                    table.CheckConstraint("CK_Citation_Schedule", "(\"ScheduledDate\" IS NULL) = (\"ScheduledTime\" IS NULL)");
                    table.CheckConstraint("CK_Citation_Status", "\"Status\" IN ('TO_SCHEDULE','SCHEDULED','RESCHEDULED','CANCELLED','CLOSED')");
                    table.ForeignKey(
                        name: "FK_Citation_Citation_RescheduledFromId",
                        column: x => x.RescheduledFromId,
                        principalSchema: "public",
                        principalTable: "Citation",
                        principalColumn: "CitationId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Citation_Observation_ObservationId",
                        column: x => x.ObservationId,
                        principalSchema: "public",
                        principalTable: "Observation",
                        principalColumn: "ObservationId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Citation_Parent_ParentId",
                        column: x => x.ParentId,
                        principalSchema: "public",
                        principalTable: "Parent",
                        principalColumn: "ParentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Citation_Schedule_ScheduleId",
                        column: x => x.ScheduleId,
                        principalSchema: "public",
                        principalTable: "Schedule",
                        principalColumn: "ScheduleId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Citation_Student_StudentId",
                        column: x => x.StudentId,
                        principalSchema: "public",
                        principalTable: "Student",
                        principalColumn: "StudentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Citation_User_CreatedById",
                        column: x => x.CreatedById,
                        principalSchema: "public",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Citation_User_ResponsibleUserId",
                        column: x => x.ResponsibleUserId,
                        principalSchema: "public",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notification_RecipientUserId_Status",
                schema: "public",
                table: "Notification",
                columns: new[] { "RecipientUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Citation_CreatedById",
                schema: "public",
                table: "Citation",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Citation_ObservationId",
                schema: "public",
                table: "Citation",
                column: "ObservationId");

            migrationBuilder.CreateIndex(
                name: "IX_Citation_ParentId",
                schema: "public",
                table: "Citation",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_Citation_RescheduledFromId",
                schema: "public",
                table: "Citation",
                column: "RescheduledFromId");

            migrationBuilder.CreateIndex(
                name: "IX_Citation_ResponsibleUserId_ScheduledDate",
                schema: "public",
                table: "Citation",
                columns: new[] { "ResponsibleUserId", "ScheduledDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Citation_ScheduleId",
                schema: "public",
                table: "Citation",
                column: "ScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_Citation_StudentId",
                schema: "public",
                table: "Citation",
                column: "StudentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Notification_User_RecipientUserId",
                schema: "public",
                table: "Notification",
                column: "RecipientUserId",
                principalSchema: "public",
                principalTable: "User",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql("""
                INSERT INTO "NotificationType" ("Code","Name","Category","Template","Active")
                SELECT v.code, v.name, 'Observador', v.template, true
                FROM (VALUES
                    ('CITATION_PARENT', 'Citación al acudiente', 'Tiene una citación el {fecha} a las {hora} con {responsable} para hablar sobre {estudiante}. Motivo: {motivo}'),
                    ('CITATION_COORDINATION', 'Citación para Coordinación', '{estudiante} ({curso}) tiene una observación de convivencia Tipo {tipo} del {fecha}. Agende la citación al acudiente.')
                ) AS v(code, name, template)
                WHERE NOT EXISTS (SELECT 1 FROM "NotificationType" t WHERE t."Code" = v.code);

                INSERT INTO "NotificationTypeRecipient" ("NotificationTypeId","RoleId")
                SELECT t."NotificationTypeId", r."Id"
                FROM "NotificationType" t CROSS JOIN "AspNetRoles" r
                WHERE t."Code" = 'CITATION_COORDINATION' AND r."Name" = 'Administrador'
                  AND NOT EXISTS (SELECT 1 FROM "NotificationTypeRecipient" y
                                  WHERE y."NotificationTypeId" = t."NotificationTypeId" AND y."RoleId" = r."Id");

                INSERT INTO "RolePermission" ("RoleId","TableName","Permissions")
                SELECT r."Id", v.key, v.perm
                FROM "AspNetRoles" r
                JOIN (VALUES ('Administrador','Citation',15), ('Padre','Notification',5), ('Acudiente','Notification',5)) AS v(role,key,perm)
                  ON r."Name" = v.role
                ON CONFLICT ("RoleId","TableName") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "RolePermission" WHERE "TableName" = 'Citation';
                DELETE FROM "Notification" WHERE "NotificationTypeId" IN (
                    SELECT "NotificationTypeId" FROM "NotificationType" WHERE "Code" IN ('CITATION_PARENT','CITATION_COORDINATION'));
                DELETE FROM "NotificationTypeRecipient" WHERE "NotificationTypeId" IN (
                    SELECT "NotificationTypeId" FROM "NotificationType" WHERE "Code" IN ('CITATION_PARENT','CITATION_COORDINATION'));
                DELETE FROM "NotificationType" WHERE "Code" IN ('CITATION_PARENT','CITATION_COORDINATION');
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Notification_User_RecipientUserId",
                schema: "public",
                table: "Notification");

            migrationBuilder.DropTable(
                name: "Citation",
                schema: "public");

            migrationBuilder.DropIndex(
                name: "IX_Notification_RecipientUserId_Status",
                schema: "public",
                table: "Notification");

            migrationBuilder.DropColumn(
                name: "RecipientUserId",
                schema: "public",
                table: "Notification");
        }
    }
}

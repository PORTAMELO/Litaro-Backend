using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Litaro.Migrations
{
    /// <inheritdoc />
    public partial class AddNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NotificationType",
                schema: "public",
                columns: table => new
                {
                    NotificationTypeId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Template = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationType", x => x.NotificationTypeId);
                });

            migrationBuilder.CreateTable(
                name: "Notification",
                schema: "public",
                columns: table => new
                {
                    NotificationId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NotificationTypeId = table.Column<int>(type: "integer", nullable: false),
                    Message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    StudentId = table.Column<int>(type: "integer", nullable: true),
                    YearId = table.Column<short>(type: "smallint", nullable: true),
                    CampusId = table.Column<int>(type: "integer", nullable: true),
                    SourceTable = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SourceId = table.Column<int>(type: "integer", nullable: true),
                    DedupKey = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "PENDING"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DoneAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DoneById = table.Column<int>(type: "integer", nullable: true),
                    DoneNote = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notification", x => x.NotificationId);
                    table.CheckConstraint("CK_Notification_Status", "\"Status\" IN ('PENDING','DONE')");
                    table.ForeignKey(
                        name: "FK_Notification_AcademicYear_YearId",
                        column: x => x.YearId,
                        principalSchema: "public",
                        principalTable: "AcademicYear",
                        principalColumn: "YearId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Notification_Campus_CampusId",
                        column: x => x.CampusId,
                        principalSchema: "public",
                        principalTable: "Campus",
                        principalColumn: "CampusId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Notification_NotificationType_NotificationTypeId",
                        column: x => x.NotificationTypeId,
                        principalSchema: "public",
                        principalTable: "NotificationType",
                        principalColumn: "NotificationTypeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Notification_Student_StudentId",
                        column: x => x.StudentId,
                        principalSchema: "public",
                        principalTable: "Student",
                        principalColumn: "StudentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Notification_User_DoneById",
                        column: x => x.DoneById,
                        principalSchema: "public",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NotificationTypeRecipient",
                schema: "public",
                columns: table => new
                {
                    NotificationTypeRecipientId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NotificationTypeId = table.Column<int>(type: "integer", nullable: false),
                    RoleId = table.Column<int>(type: "integer", nullable: true),
                    UserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationTypeRecipient", x => x.NotificationTypeRecipientId);
                    table.CheckConstraint("CK_NotificationTypeRecipient_Target", "(\"RoleId\" IS NULL) <> (\"UserId\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_NotificationTypeRecipient_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "public",
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NotificationTypeRecipient_NotificationType_NotificationType~",
                        column: x => x.NotificationTypeId,
                        principalSchema: "public",
                        principalTable: "NotificationType",
                        principalColumn: "NotificationTypeId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NotificationTypeRecipient_User_UserId",
                        column: x => x.UserId,
                        principalSchema: "public",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NotificationRead",
                schema: "public",
                columns: table => new
                {
                    NotificationId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationRead", x => new { x.NotificationId, x.UserId });
                    table.ForeignKey(
                        name: "FK_NotificationRead_Notification_NotificationId",
                        column: x => x.NotificationId,
                        principalSchema: "public",
                        principalTable: "Notification",
                        principalColumn: "NotificationId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NotificationRead_User_UserId",
                        column: x => x.UserId,
                        principalSchema: "public",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notification_CampusId",
                schema: "public",
                table: "Notification",
                column: "CampusId");

            migrationBuilder.CreateIndex(
                name: "IX_Notification_DedupKey",
                schema: "public",
                table: "Notification",
                column: "DedupKey",
                unique: true,
                filter: "\"DedupKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Notification_DoneById",
                schema: "public",
                table: "Notification",
                column: "DoneById");

            migrationBuilder.CreateIndex(
                name: "IX_Notification_NotificationTypeId_StudentId_YearId",
                schema: "public",
                table: "Notification",
                columns: new[] { "NotificationTypeId", "StudentId", "YearId" });

            migrationBuilder.CreateIndex(
                name: "IX_Notification_Status_CreatedAt",
                schema: "public",
                table: "Notification",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Notification_StudentId",
                schema: "public",
                table: "Notification",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_Notification_YearId",
                schema: "public",
                table: "Notification",
                column: "YearId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationRead_UserId",
                schema: "public",
                table: "NotificationRead",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationType_Code",
                schema: "public",
                table: "NotificationType",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationTypeRecipient_NotificationTypeId_RoleId",
                schema: "public",
                table: "NotificationTypeRecipient",
                columns: new[] { "NotificationTypeId", "RoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationTypeRecipient_NotificationTypeId_UserId",
                schema: "public",
                table: "NotificationTypeRecipient",
                columns: new[] { "NotificationTypeId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationTypeRecipient_RoleId",
                schema: "public",
                table: "NotificationTypeRecipient",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationTypeRecipient_UserId",
                schema: "public",
                table: "NotificationTypeRecipient",
                column: "UserId");

            migrationBuilder.Sql("""
                INSERT INTO "NotificationType" ("Code","Name","Category","Template","Active") VALUES
                ('SIMAT_ADMISSION_APPROVED','Admisión aprobada','SIMAT','El Rector aprobó la admisión de {aspirante} ({documento}) a {grado} para {anio}. Regístrelo como estudiante nuevo en el SIMAT.',true),
                ('SIMAT_CUPO_DECLINED','Cupo liberado: no continúa','SIMAT','La familia de {estudiante} ({documento}) no continuará en {anio}. Registre la novedad en el SIMAT.',true),
                ('SIMAT_CUPO_LOST','Cupo liberado: sin respuesta','SIMAT','{estudiante} ({documento}) perdió el cupo {anio}: la familia no respondió antes del {fecha}. Registre la novedad en el SIMAT.',true),
                ('SIMAT_NOT_IN_SIMAT','No está en el SIMAT','SIMAT','{estudiante} ({documento}) está matriculado en LITARO {anio} pero no aparece en el archivo del SIMAT. Verifique en el SIMAT y regístrelo o corríjalo a mano.',true);

                INSERT INTO "NotificationTypeRecipient" ("NotificationTypeId","RoleId")
                SELECT t."NotificationTypeId", r."Id"
                FROM "NotificationType" t CROSS JOIN "AspNetRoles" r
                WHERE t."Category" = 'SIMAT' AND r."Name" IN ('Administrador','Secretaría','Secretaria');

                INSERT INTO "RolePermission" ("RoleId","TableName","Permissions")
                SELECT r."Id", v.key, v.perm
                FROM "AspNetRoles" r
                JOIN (VALUES ('Administrador','Notification',15), ('Administrador','NotificationSettings',15),
                             ('Secretaría','Notification',5), ('Secretaria','Notification',5)) AS v(role,key,perm)
                  ON r."Name" = v.role
                ON CONFLICT ("RoleId","TableName") DO NOTHING;

                INSERT INTO "Notification" ("NotificationTypeId","Message","StudentId","YearId","SourceTable","SourceId",
                                            "Status","CreatedAt","DoneAt","DoneById","DoneNote")
                SELECT t."NotificationTypeId", a."Message", a."StudentId", a."YearId",
                       CASE WHEN a."AdmissionRequestId" IS NOT NULL THEN 'AdmissionRequest' END, a."AdmissionRequestId",
                       a."Status", a."CreatedAt", a."ResolvedAt", a."ResolvedById", a."ResolutionNote"
                FROM "SimatAlert" a
                JOIN "NotificationType" t ON t."Code" = CASE
                    WHEN a."Type" = 'ADMISSION_APPROVED' THEN 'SIMAT_ADMISSION_APPROVED'
                    WHEN a."Type" = 'NOT_IN_SIMAT' THEN 'SIMAT_NOT_IN_SIMAT'
                    WHEN a."Message" LIKE '%perdió el cupo%' THEN 'SIMAT_CUPO_LOST'
                    ELSE 'SIMAT_CUPO_DECLINED' END;
                """);

            migrationBuilder.DropTable(
                name: "SimatAlert",
                schema: "public");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "RolePermission" WHERE "TableName" IN ('Notification','NotificationSettings');
                """);

            migrationBuilder.DropTable(
                name: "NotificationRead",
                schema: "public");

            migrationBuilder.DropTable(
                name: "NotificationTypeRecipient",
                schema: "public");

            migrationBuilder.DropTable(
                name: "Notification",
                schema: "public");

            migrationBuilder.DropTable(
                name: "NotificationType",
                schema: "public");

            migrationBuilder.CreateTable(
                name: "SimatAlert",
                schema: "public",
                columns: table => new
                {
                    SimatAlertId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AdmissionRequestId = table.Column<int>(type: "integer", nullable: true),
                    ResolvedById = table.Column<int>(type: "integer", nullable: true),
                    StudentId = table.Column<int>(type: "integer", nullable: true),
                    YearId = table.Column<short>(type: "smallint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Message = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    ResolutionNote = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "PENDING"),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
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
    }
}

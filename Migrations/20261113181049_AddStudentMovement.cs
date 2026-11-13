using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Litaro.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentMovement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EnrollmentEvent",
                schema: "public",
                columns: table => new
                {
                    EnrollmentEventId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EnrollmentId = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    FromClassroomId = table.Column<int>(type: "integer", nullable: false),
                    ToClassroomId = table.Column<int>(type: "integer", nullable: true),
                    Reason = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnrollmentEvent", x => x.EnrollmentEventId);
                    table.ForeignKey(
                        name: "FK_EnrollmentEvent_Classroom_FromClassroomId",
                        column: x => x.FromClassroomId,
                        principalSchema: "public",
                        principalTable: "Classroom",
                        principalColumn: "ClassroomId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EnrollmentEvent_Classroom_ToClassroomId",
                        column: x => x.ToClassroomId,
                        principalSchema: "public",
                        principalTable: "Classroom",
                        principalColumn: "ClassroomId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EnrollmentEvent_Enrollment_EnrollmentId",
                        column: x => x.EnrollmentId,
                        principalSchema: "public",
                        principalTable: "Enrollment",
                        principalColumn: "EnrollmentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EnrollmentEvent_User_UserId",
                        column: x => x.UserId,
                        principalSchema: "public",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EnrollmentEvent_EnrollmentId",
                schema: "public",
                table: "EnrollmentEvent",
                column: "EnrollmentId");

            migrationBuilder.CreateIndex(
                name: "IX_EnrollmentEvent_FromClassroomId",
                schema: "public",
                table: "EnrollmentEvent",
                column: "FromClassroomId");

            migrationBuilder.CreateIndex(
                name: "IX_EnrollmentEvent_ToClassroomId",
                schema: "public",
                table: "EnrollmentEvent",
                column: "ToClassroomId");

            migrationBuilder.CreateIndex(
                name: "IX_EnrollmentEvent_UserId",
                schema: "public",
                table: "EnrollmentEvent",
                column: "UserId");

            migrationBuilder.Sql("""
                INSERT INTO "AspNetRoles" ("Name","NormalizedName","ConcurrencyStamp")
                SELECT 'Coordinador Académico', 'COORDINADOR ACADÉMICO', md5(random()::text)
                WHERE NOT EXISTS (
                    SELECT 1 FROM "AspNetRoles"
                    WHERE "NormalizedName" IN ('COORDINADOR ACADÉMICO','COORDINADOR ACADEMICO'));

                INSERT INTO "RolePermission" ("RoleId","TableName","Permissions")
                SELECT r."Id", 'StudentMovement', 5
                FROM "AspNetRoles" r
                WHERE r."NormalizedName" IN ('COORDINADOR ACADÉMICO','COORDINADOR ACADEMICO')
                ON CONFLICT ("RoleId","TableName") DO NOTHING;

                INSERT INTO "NotificationType" ("Code","Name","Category","Template","Active") VALUES
                ('SIMAT_CLASSROOM_CHANGE','Cambio de curso','SIMAT','{estudiante} ({documento}) pasó del curso {origen} al curso {destino} en {sede}. Actualice el grupo en el SIMAT.',true),
                ('SIMAT_CAMPUS_TRANSFER','Cambio de sede','SIMAT','{estudiante} ({documento}) fue trasladado de {origen} ({sede_origen}) a {destino} ({sede_destino}). Registre el traslado entre sedes en el SIMAT.',true),
                ('SIMAT_WITHDRAWAL','Retiro de estudiante','SIMAT','{estudiante} ({documento}) fue retirado del curso {curso} en {anio}. Motivo: {motivo}. Registre el retiro en el SIMAT.',true);

                INSERT INTO "NotificationTypeRecipient" ("NotificationTypeId","RoleId")
                SELECT t."NotificationTypeId", r."Id"
                FROM "NotificationType" t CROSS JOIN "AspNetRoles" r
                WHERE t."Code" IN ('SIMAT_CLASSROOM_CHANGE','SIMAT_CAMPUS_TRANSFER','SIMAT_WITHDRAWAL')
                  AND r."Name" IN ('Administrador','Secretaría','Secretaria');

                INSERT INTO "Characteristic" ("Name","CreationDate")
                SELECT 'Motivos de retiro SIMAT', now()
                WHERE NOT EXISTS (SELECT 1 FROM "Characteristic" WHERE "Name" = 'Motivos de retiro SIMAT');

                INSERT INTO "CharacteristicDetail" ("CharacteristicId","Nombre","Valor","CreationDate")
                SELECT c."CharacteristicId", 'Motivo', v.valor, now()
                FROM "Characteristic" c
                CROSS JOIN (VALUES ('Deserción'), ('Traslado'), ('Cambio de domicilio')) AS v(valor)
                WHERE c."Name" = 'Motivos de retiro SIMAT'
                  AND NOT EXISTS (
                      SELECT 1 FROM "CharacteristicDetail" d
                      WHERE d."CharacteristicId" = c."CharacteristicId" AND d."Valor" = v.valor);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "RolePermission" WHERE "TableName" = 'StudentMovement';

                DELETE FROM "Notification"
                WHERE "NotificationTypeId" IN (
                    SELECT "NotificationTypeId" FROM "NotificationType"
                    WHERE "Code" IN ('SIMAT_CLASSROOM_CHANGE','SIMAT_CAMPUS_TRANSFER','SIMAT_WITHDRAWAL'));

                DELETE FROM "NotificationType"
                WHERE "Code" IN ('SIMAT_CLASSROOM_CHANGE','SIMAT_CAMPUS_TRANSFER','SIMAT_WITHDRAWAL');

                DELETE FROM "CharacteristicDetail"
                WHERE "CharacteristicId" IN (SELECT "CharacteristicId" FROM "Characteristic" WHERE "Name" = 'Motivos de retiro SIMAT');

                DELETE FROM "Characteristic" WHERE "Name" = 'Motivos de retiro SIMAT';
                """);

            migrationBuilder.DropTable(
                name: "EnrollmentEvent",
                schema: "public");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Litaro.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceRegister : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AbsenceLoss",
                schema: "public",
                columns: table => new
                {
                    AbsenceLossId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EnrollmentId = table.Column<int>(type: "integer", nullable: false),
                    SubjectId = table.Column<int>(type: "integer", nullable: false),
                    PeriodId = table.Column<short>(type: "smallint", nullable: false),
                    UnjustifiedHours = table.Column<decimal>(type: "numeric(7,2)", nullable: false),
                    PlannedHours = table.Column<decimal>(type: "numeric(7,2)", nullable: false),
                    Percent = table.Column<decimal>(type: "numeric(7,2)", nullable: false),
                    FlaggedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AbsenceLoss", x => x.AbsenceLossId);
                    table.ForeignKey(
                        name: "FK_AbsenceLoss_AcademicPeriod_PeriodId",
                        column: x => x.PeriodId,
                        principalSchema: "public",
                        principalTable: "AcademicPeriod",
                        principalColumn: "PeriodId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AbsenceLoss_Enrollment_EnrollmentId",
                        column: x => x.EnrollmentId,
                        principalSchema: "public",
                        principalTable: "Enrollment",
                        principalColumn: "EnrollmentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AbsenceLoss_Subject_SubjectId",
                        column: x => x.SubjectId,
                        principalSchema: "public",
                        principalTable: "Subject",
                        principalColumn: "SubjectId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceExcuse",
                schema: "public",
                columns: table => new
                {
                    AttendanceExcuseId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    StartAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EndAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreatedById = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceExcuse", x => x.AttendanceExcuseId);
                    table.CheckConstraint("CK_AttendanceExcuse_Range", "\"EndAt\" > \"StartAt\"");
                    table.ForeignKey(
                        name: "FK_AttendanceExcuse_Student_StudentId",
                        column: x => x.StudentId,
                        principalSchema: "public",
                        principalTable: "Student",
                        principalColumn: "StudentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceExcuse_User_CreatedById",
                        column: x => x.CreatedById,
                        principalSchema: "public",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceSession",
                schema: "public",
                columns: table => new
                {
                    AttendanceSessionId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    YearId = table.Column<short>(type: "smallint", nullable: false),
                    ClassroomId = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Kind = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    AssignmentId = table.Column<int>(type: "integer", nullable: true),
                    SubjectId = table.Column<int>(type: "integer", nullable: true),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    TakenById = table.Column<int>(type: "integer", nullable: false),
                    TakenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceSession", x => x.AttendanceSessionId);
                    table.CheckConstraint("CK_AttendanceSession_Fields", "(\"Kind\" = 'CLASS' AND \"AssignmentId\" IS NOT NULL AND \"SubjectId\" IS NOT NULL AND \"StartTime\" IS NOT NULL AND \"EndTime\" IS NOT NULL AND \"EndTime\" > \"StartTime\") OR (\"Kind\" = 'DAILY' AND \"AssignmentId\" IS NULL AND \"SubjectId\" IS NULL AND \"StartTime\" IS NULL AND \"EndTime\" IS NULL)");
                    table.CheckConstraint("CK_AttendanceSession_Kind", "\"Kind\" IN ('CLASS','DAILY')");
                    table.ForeignKey(
                        name: "FK_AttendanceSession_AcademicAssignment_AssignmentId",
                        column: x => x.AssignmentId,
                        principalSchema: "public",
                        principalTable: "AcademicAssignment",
                        principalColumn: "AssignmentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceSession_AcademicYear_YearId",
                        column: x => x.YearId,
                        principalSchema: "public",
                        principalTable: "AcademicYear",
                        principalColumn: "YearId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceSession_Classroom_ClassroomId",
                        column: x => x.ClassroomId,
                        principalSchema: "public",
                        principalTable: "Classroom",
                        principalColumn: "ClassroomId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceSession_Subject_SubjectId",
                        column: x => x.SubjectId,
                        principalSchema: "public",
                        principalTable: "Subject",
                        principalColumn: "SubjectId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceSession_User_TakenById",
                        column: x => x.TakenById,
                        principalSchema: "public",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceSession_User_UpdatedById",
                        column: x => x.UpdatedById,
                        principalSchema: "public",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "JobRun",
                schema: "public",
                columns: table => new
                {
                    JobName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    LastRunDate = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobRun", x => x.JobName);
                });

            migrationBuilder.CreateTable(
                name: "LateArrival",
                schema: "public",
                columns: table => new
                {
                    LateArrivalId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EnrollmentId = table.Column<int>(type: "integer", nullable: false),
                    CampusId = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    ArrivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RecordedById = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LateArrival", x => x.LateArrivalId);
                    table.ForeignKey(
                        name: "FK_LateArrival_Campus_CampusId",
                        column: x => x.CampusId,
                        principalSchema: "public",
                        principalTable: "Campus",
                        principalColumn: "CampusId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LateArrival_Enrollment_EnrollmentId",
                        column: x => x.EnrollmentId,
                        principalSchema: "public",
                        principalTable: "Enrollment",
                        principalColumn: "EnrollmentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LateArrival_User_RecordedById",
                        column: x => x.RecordedById,
                        principalSchema: "public",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PeriodPlannedHours",
                schema: "public",
                columns: table => new
                {
                    PeriodPlannedHoursId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PeriodId = table.Column<short>(type: "smallint", nullable: false),
                    ClassroomId = table.Column<int>(type: "integer", nullable: false),
                    SubjectId = table.Column<int>(type: "integer", nullable: false),
                    PlannedHours = table.Column<decimal>(type: "numeric(7,2)", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PeriodPlannedHours", x => x.PeriodPlannedHoursId);
                    table.ForeignKey(
                        name: "FK_PeriodPlannedHours_AcademicPeriod_PeriodId",
                        column: x => x.PeriodId,
                        principalSchema: "public",
                        principalTable: "AcademicPeriod",
                        principalColumn: "PeriodId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PeriodPlannedHours_Classroom_ClassroomId",
                        column: x => x.ClassroomId,
                        principalSchema: "public",
                        principalTable: "Classroom",
                        principalColumn: "ClassroomId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PeriodPlannedHours_Subject_SubjectId",
                        column: x => x.SubjectId,
                        principalSchema: "public",
                        principalTable: "Subject",
                        principalColumn: "SubjectId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceAbsence",
                schema: "public",
                columns: table => new
                {
                    AttendanceAbsenceId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AttendanceSessionId = table.Column<int>(type: "integer", nullable: false),
                    EnrollmentId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    AttendanceExcuseId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceAbsence", x => x.AttendanceAbsenceId);
                    table.CheckConstraint("CK_AttendanceAbsence_Status", "\"Status\" IN ('ABSENT','EVASION')");
                    table.ForeignKey(
                        name: "FK_AttendanceAbsence_AttendanceExcuse_AttendanceExcuseId",
                        column: x => x.AttendanceExcuseId,
                        principalSchema: "public",
                        principalTable: "AttendanceExcuse",
                        principalColumn: "AttendanceExcuseId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AttendanceAbsence_AttendanceSession_AttendanceSessionId",
                        column: x => x.AttendanceSessionId,
                        principalSchema: "public",
                        principalTable: "AttendanceSession",
                        principalColumn: "AttendanceSessionId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AttendanceAbsence_Enrollment_EnrollmentId",
                        column: x => x.EnrollmentId,
                        principalSchema: "public",
                        principalTable: "Enrollment",
                        principalColumn: "EnrollmentId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClassTardiness",
                schema: "public",
                columns: table => new
                {
                    ClassTardinessId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AttendanceSessionId = table.Column<int>(type: "integer", nullable: false),
                    EnrollmentId = table.Column<int>(type: "integer", nullable: false),
                    Observation = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    RecordedById = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassTardiness", x => x.ClassTardinessId);
                    table.ForeignKey(
                        name: "FK_ClassTardiness_AttendanceSession_AttendanceSessionId",
                        column: x => x.AttendanceSessionId,
                        principalSchema: "public",
                        principalTable: "AttendanceSession",
                        principalColumn: "AttendanceSessionId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClassTardiness_Enrollment_EnrollmentId",
                        column: x => x.EnrollmentId,
                        principalSchema: "public",
                        principalTable: "Enrollment",
                        principalColumn: "EnrollmentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClassTardiness_User_RecordedById",
                        column: x => x.RecordedById,
                        principalSchema: "public",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AbsenceLoss_EnrollmentId_SubjectId_PeriodId",
                schema: "public",
                table: "AbsenceLoss",
                columns: new[] { "EnrollmentId", "SubjectId", "PeriodId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AbsenceLoss_PeriodId",
                schema: "public",
                table: "AbsenceLoss",
                column: "PeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_AbsenceLoss_SubjectId",
                schema: "public",
                table: "AbsenceLoss",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceAbsence_AttendanceExcuseId",
                schema: "public",
                table: "AttendanceAbsence",
                column: "AttendanceExcuseId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceAbsence_AttendanceSessionId_EnrollmentId",
                schema: "public",
                table: "AttendanceAbsence",
                columns: new[] { "AttendanceSessionId", "EnrollmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceAbsence_EnrollmentId",
                schema: "public",
                table: "AttendanceAbsence",
                column: "EnrollmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceExcuse_CreatedById",
                schema: "public",
                table: "AttendanceExcuse",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceExcuse_StudentId",
                schema: "public",
                table: "AttendanceExcuse",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceSession_AssignmentId",
                schema: "public",
                table: "AttendanceSession",
                column: "AssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceSession_SubjectId",
                schema: "public",
                table: "AttendanceSession",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceSession_TakenById",
                schema: "public",
                table: "AttendanceSession",
                column: "TakenById");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceSession_UpdatedById",
                schema: "public",
                table: "AttendanceSession",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceSession_YearId_Date",
                schema: "public",
                table: "AttendanceSession",
                columns: new[] { "YearId", "Date" });

            migrationBuilder.CreateIndex(
                name: "UX_AttendanceSession_Class",
                schema: "public",
                table: "AttendanceSession",
                columns: new[] { "ClassroomId", "Date", "StartTime" },
                unique: true,
                filter: "\"Kind\" = 'CLASS'");

            migrationBuilder.CreateIndex(
                name: "UX_AttendanceSession_Daily",
                schema: "public",
                table: "AttendanceSession",
                columns: new[] { "ClassroomId", "Date" },
                unique: true,
                filter: "\"Kind\" = 'DAILY'");

            migrationBuilder.CreateIndex(
                name: "IX_ClassTardiness_AttendanceSessionId_EnrollmentId",
                schema: "public",
                table: "ClassTardiness",
                columns: new[] { "AttendanceSessionId", "EnrollmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClassTardiness_EnrollmentId",
                schema: "public",
                table: "ClassTardiness",
                column: "EnrollmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassTardiness_RecordedById",
                schema: "public",
                table: "ClassTardiness",
                column: "RecordedById");

            migrationBuilder.CreateIndex(
                name: "IX_LateArrival_CampusId",
                schema: "public",
                table: "LateArrival",
                column: "CampusId");

            migrationBuilder.CreateIndex(
                name: "IX_LateArrival_Date",
                schema: "public",
                table: "LateArrival",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_LateArrival_EnrollmentId_Date",
                schema: "public",
                table: "LateArrival",
                columns: new[] { "EnrollmentId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LateArrival_RecordedById",
                schema: "public",
                table: "LateArrival",
                column: "RecordedById");

            migrationBuilder.CreateIndex(
                name: "IX_PeriodPlannedHours_ClassroomId",
                schema: "public",
                table: "PeriodPlannedHours",
                column: "ClassroomId");

            migrationBuilder.CreateIndex(
                name: "IX_PeriodPlannedHours_PeriodId_ClassroomId_SubjectId",
                schema: "public",
                table: "PeriodPlannedHours",
                columns: new[] { "PeriodId", "ClassroomId", "SubjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PeriodPlannedHours_SubjectId",
                schema: "public",
                table: "PeriodPlannedHours",
                column: "SubjectId");

            migrationBuilder.Sql("""
                INSERT INTO "NotificationType" ("Code","Name","Category","Template","Active") VALUES
                ('ATT_EVASION','Evasión','Asistencia','{estudiante} ({curso}) no asistió a {materia} a las {hora}.',true),
                ('ATT_CLASS_TARDY','Tardanza en clase','Asistencia','{estudiante} ({curso}) llegó tarde a {materia}. Sanción pedagógica: {observacion}',true),
                ('ATT_LATE_COMMITMENT','Llegadas tarde: compromiso','Asistencia','{estudiante} ({curso}) completó {cantidad} llegadas tarde en el periodo {periodo}. Genere el compromiso para firma del acudiente.',true),
                ('ATT_LATE_CITATION','Llegadas tarde: citación','Asistencia','{estudiante} ({curso}) completó {cantidad} llegadas tarde en el periodo {periodo}. Cite al acudiente.',true),
                ('ATT_WEEK_ABSENCE','Semana de inasistencia','Asistencia','{estudiante} ({curso}) completó {dias} días seguidos de inasistencia injustificada. Contacte a la familia.',true),
                ('ATT_PERIOD_ABSENCE','Periodo de inasistencia','Asistencia','{estudiante} ({curso}) no asistió durante todo el periodo {periodo} sin justificación. Inicie el proceso ante las entidades competentes y gestione el retiro del SIMAT.',true);

                INSERT INTO "NotificationTypeRecipient" ("NotificationTypeId","RoleId")
                SELECT t."NotificationTypeId", r."Id"
                FROM "NotificationType" t CROSS JOIN "AspNetRoles" r
                WHERE t."Category" = 'Asistencia' AND r."Name" = 'Administrador';

                INSERT INTO "RolePermission" ("RoleId","TableName","Permissions")
                SELECT r."Id", v.key, v.perm
                FROM "AspNetRoles" r
                JOIN (VALUES ('Administrador','AttendanceRoll',15), ('Administrador','AttendanceSummary',15),
                             ('Administrador','LateArrival',15), ('Administrador','AttendanceExcuse',15),
                             ('Administrador','AttendanceSettings',15),
                             ('Profesor','AttendanceRoll',3), ('Profesor','AttendanceSummary',1)) AS v(role,key,perm)
                  ON r."Name" = v.role
                ON CONFLICT ("RoleId","TableName") DO NOTHING;

                INSERT INTO "Characteristic" ("Name","CreationDate")
                SELECT 'Parámetros de asistencia', now()
                WHERE NOT EXISTS (SELECT 1 FROM "Characteristic" WHERE "Name" = 'Parámetros de asistencia');

                INSERT INTO "CharacteristicDetail" ("CharacteristicId","Nombre","Valor","CreationDate")
                SELECT c."CharacteristicId", v.nombre, v.valor, now()
                FROM "Characteristic" c
                CROSS JOIN (VALUES ('Porcentaje de pérdida','20'), ('Nota por pérdida','1.0'),
                                   ('Días para presentar excusa','2'), ('Tardanzas para compromiso','3'),
                                   ('Tardanzas para citación','5'), ('Días de inasistencia para alerta','5'),
                                   ('Hora del proceso nocturno','23')) AS v(nombre, valor)
                WHERE c."Name" = 'Parámetros de asistencia';

                INSERT INTO "CharacteristicDetail" ("CharacteristicId","Nombre","Valor","CreationDate")
                SELECT c."CharacteristicId", 'Nivel con lista diaria', g."Level", now()
                FROM (SELECT DISTINCT "Level" FROM "Grade") g
                CROSS JOIN "Characteristic" c
                WHERE c."Name" = 'Parámetros de asistencia'
                  AND (lower(g."Level") LIKE '%prim%' OR lower(g."Level") LIKE '%pree%'
                       OR lower(g."Level") LIKE '%elem%' OR lower(g."Level") LIKE '%trans%');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "RolePermission"
                WHERE "TableName" IN ('AttendanceRoll','AttendanceSummary','LateArrival','AttendanceExcuse','AttendanceSettings');

                DELETE FROM "Notification"
                WHERE "NotificationTypeId" IN (SELECT "NotificationTypeId" FROM "NotificationType" WHERE "Category" = 'Asistencia');

                DELETE FROM "NotificationType" WHERE "Category" = 'Asistencia';

                DELETE FROM "CharacteristicDetail"
                WHERE "CharacteristicId" IN (SELECT "CharacteristicId" FROM "Characteristic" WHERE "Name" = 'Parámetros de asistencia');

                DELETE FROM "Characteristic" WHERE "Name" = 'Parámetros de asistencia';
                """);

            migrationBuilder.DropTable(
                name: "AbsenceLoss",
                schema: "public");

            migrationBuilder.DropTable(
                name: "AttendanceAbsence",
                schema: "public");

            migrationBuilder.DropTable(
                name: "ClassTardiness",
                schema: "public");

            migrationBuilder.DropTable(
                name: "JobRun",
                schema: "public");

            migrationBuilder.DropTable(
                name: "LateArrival",
                schema: "public");

            migrationBuilder.DropTable(
                name: "PeriodPlannedHours",
                schema: "public");

            migrationBuilder.DropTable(
                name: "AttendanceExcuse",
                schema: "public");

            migrationBuilder.DropTable(
                name: "AttendanceSession",
                schema: "public");
        }
    }
}

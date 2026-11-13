using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Litaro.Migrations
{
    /// <inheritdoc />
    public partial class AddCupoRestoredNotification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO "NotificationType" ("Code","Name","Category","Template","Active")
                SELECT 'SIMAT_CUPO_RESTORED','Cupo restablecido','SIMAT',
                       'Se restableció el cupo {anio} de {estudiante} ({documento}) al corregir el plazo de prematrícula. Si registró la novedad en el SIMAT, reviértala.',
                       true
                WHERE NOT EXISTS (SELECT 1 FROM "NotificationType" WHERE "Code" = 'SIMAT_CUPO_RESTORED');

                INSERT INTO "NotificationTypeRecipient" ("NotificationTypeId","RoleId")
                SELECT t."NotificationTypeId", r."Id"
                FROM "NotificationType" t CROSS JOIN "AspNetRoles" r
                WHERE t."Code" = 'SIMAT_CUPO_RESTORED' AND r."Name" IN ('Administrador','Secretaría','Secretaria')
                  AND NOT EXISTS (SELECT 1 FROM "NotificationTypeRecipient" x
                                  WHERE x."NotificationTypeId" = t."NotificationTypeId" AND x."RoleId" = r."Id");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "NotificationRead" WHERE "NotificationId" IN (
                    SELECT n."NotificationId" FROM "Notification" n
                    JOIN "NotificationType" t ON t."NotificationTypeId" = n."NotificationTypeId"
                    WHERE t."Code" = 'SIMAT_CUPO_RESTORED');

                DELETE FROM "Notification" WHERE "NotificationTypeId" IN (
                    SELECT "NotificationTypeId" FROM "NotificationType" WHERE "Code" = 'SIMAT_CUPO_RESTORED');

                DELETE FROM "NotificationTypeRecipient" WHERE "NotificationTypeId" IN (
                    SELECT "NotificationTypeId" FROM "NotificationType" WHERE "Code" = 'SIMAT_CUPO_RESTORED');

                DELETE FROM "NotificationType" WHERE "Code" = 'SIMAT_CUPO_RESTORED';
                """);
        }
    }
}

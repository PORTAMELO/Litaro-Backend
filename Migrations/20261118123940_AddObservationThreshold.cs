using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Litaro.Migrations
{
    /// <inheritdoc />
    public partial class AddObservationThreshold : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO "NotificationType" ("Code","Name","Category","Template","Active")
                SELECT 'OBS_TYPE1_THRESHOLD', 'Reincidencia Tipo I', 'Observador',
                       '{estudiante} ({curso}) alcanzó {cantidad} situaciones Tipo I en {anio}. Revise su Observador.', true
                WHERE NOT EXISTS (SELECT 1 FROM "NotificationType" t WHERE t."Code" = 'OBS_TYPE1_THRESHOLD');

                INSERT INTO "NotificationTypeRecipient" ("NotificationTypeId","RoleId")
                SELECT t."NotificationTypeId", r."Id"
                FROM "NotificationType" t CROSS JOIN "AspNetRoles" r
                WHERE t."Code" = 'OBS_TYPE1_THRESHOLD' AND r."Name" = 'Administrador'
                  AND NOT EXISTS (SELECT 1 FROM "NotificationTypeRecipient" y
                                  WHERE y."NotificationTypeId" = t."NotificationTypeId" AND y."RoleId" = r."Id");

                INSERT INTO "Characteristic" ("Name","CreationDate")
                SELECT 'Parámetros del observador', now()
                WHERE NOT EXISTS (SELECT 1 FROM "Characteristic" c WHERE c."Name" = 'Parámetros del observador');

                INSERT INTO "CharacteristicDetail" ("CharacteristicId","Nombre","Valor","CreationDate")
                SELECT c."CharacteristicId", 'Situaciones Tipo I para alerta', '3', now()
                FROM "Characteristic" c
                WHERE c."Name" = 'Parámetros del observador'
                  AND NOT EXISTS (SELECT 1 FROM "CharacteristicDetail" d
                                  WHERE d."CharacteristicId" = c."CharacteristicId" AND d."Nombre" = 'Situaciones Tipo I para alerta');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "Notification" WHERE "NotificationTypeId" IN (
                    SELECT "NotificationTypeId" FROM "NotificationType" WHERE "Code" = 'OBS_TYPE1_THRESHOLD');
                DELETE FROM "NotificationTypeRecipient" WHERE "NotificationTypeId" IN (
                    SELECT "NotificationTypeId" FROM "NotificationType" WHERE "Code" = 'OBS_TYPE1_THRESHOLD');
                DELETE FROM "NotificationType" WHERE "Code" = 'OBS_TYPE1_THRESHOLD';
                DELETE FROM "CharacteristicDetail" WHERE "CharacteristicId" IN (
                    SELECT "CharacteristicId" FROM "Characteristic" WHERE "Name" = 'Parámetros del observador');
                DELETE FROM "Characteristic" WHERE "Name" = 'Parámetros del observador';
                """);
        }
    }
}

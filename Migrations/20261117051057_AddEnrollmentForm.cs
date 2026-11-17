using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Litaro.Migrations
{
    /// <inheritdoc />
    public partial class AddEnrollmentForm : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConflictVictim",
                schema: "public",
                table: "Student",
                type: "character varying(5)",
                maxLength: 5,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisabilityType",
                schema: "public",
                table: "Student",
                type: "character varying(5)",
                maxLength: 5,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EthnicGroup",
                schema: "public",
                table: "Student",
                type: "character varying(5)",
                maxLength: 5,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExceptionalCapacity",
                schema: "public",
                table: "Student",
                type: "character varying(5)",
                maxLength: 5,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Methodology",
                schema: "public",
                table: "Student",
                type: "character varying(5)",
                maxLength: 5,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Address",
                schema: "public",
                table: "Parent",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FileRemovedAt",
                schema: "public",
                table: "Document",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Codigo",
                schema: "public",
                table: "CharacteristicDetail",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BloodType",
                schema: "public",
                table: "AdmissionRequest",
                type: "character varying(5)",
                maxLength: 5,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CampusId",
                schema: "public",
                table: "AdmissionRequest",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConflictVictim",
                schema: "public",
                table: "AdmissionRequest",
                type: "character varying(5)",
                maxLength: 5,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisabilityType",
                schema: "public",
                table: "AdmissionRequest",
                type: "character varying(5)",
                maxLength: 5,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                schema: "public",
                table: "AdmissionRequest",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Eps",
                schema: "public",
                table: "AdmissionRequest",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EthnicGroup",
                schema: "public",
                table: "AdmissionRequest",
                type: "character varying(5)",
                maxLength: 5,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExceptionalCapacity",
                schema: "public",
                table: "AdmissionRequest",
                type: "character varying(5)",
                maxLength: 5,
                nullable: true);

            migrationBuilder.AddColumn<char>(
                name: "Gender",
                schema: "public",
                table: "AdmissionRequest",
                type: "character(1)",
                maxLength: 1,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuardianAddress",
                schema: "public",
                table: "AdmissionRequest",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuardianDocumentNumber",
                schema: "public",
                table: "AdmissionRequest",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuardianDocumentType",
                schema: "public",
                table: "AdmissionRequest",
                type: "character varying(5)",
                maxLength: 5,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuardianEmail",
                schema: "public",
                table: "AdmissionRequest",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuardianFirstName",
                schema: "public",
                table: "AdmissionRequest",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuardianLastName",
                schema: "public",
                table: "AdmissionRequest",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuardianRelationship",
                schema: "public",
                table: "AdmissionRequest",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Methodology",
                schema: "public",
                table: "AdmissionRequest",
                type: "character varying(5)",
                maxLength: 5,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StudentId",
                schema: "public",
                table: "AdmissionRequest",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdmissionRequest_CampusId",
                schema: "public",
                table: "AdmissionRequest",
                column: "CampusId");

            migrationBuilder.CreateIndex(
                name: "IX_AdmissionRequest_StudentId",
                schema: "public",
                table: "AdmissionRequest",
                column: "StudentId");

            migrationBuilder.AddForeignKey(
                name: "FK_AdmissionRequest_Campus_CampusId",
                schema: "public",
                table: "AdmissionRequest",
                column: "CampusId",
                principalSchema: "public",
                principalTable: "Campus",
                principalColumn: "CampusId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AdmissionRequest_Student_StudentId",
                schema: "public",
                table: "AdmissionRequest",
                column: "StudentId",
                principalSchema: "public",
                principalTable: "Student",
                principalColumn: "StudentId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql("""
                INSERT INTO "Characteristic" ("Name","CreationDate")
                SELECT v.name, now()
                FROM (VALUES
                    ('Tipo de documento'),
                    ('Género'),
                    ('Tipo de sangre'),
                    ('EPS'),
                    ('Grupo étnico'),
                    ('Tipo de discapacidad'),
                    ('Capacidades excepcionales'),
                    ('Víctima del conflicto'),
                    ('Metodología'),
                    ('Documentos de matrícula')
                ) AS v(name)
                WHERE NOT EXISTS (SELECT 1 FROM "Characteristic" c WHERE c."Name" = v.name);

                INSERT INTO "CharacteristicDetail" ("CharacteristicId","Nombre","Valor","Codigo","CreationDate")
                SELECT c."CharacteristicId", v.nombre, v.valor, v.codigo, now()
                FROM "Characteristic" c
                CROSS JOIN (VALUES
                    (1, 'CC', 'Cédula de ciudadanía', '1'),
                    (2, 'TI', 'Tarjeta de identidad', '2'),
                    (3, 'CE', 'Cédula de extranjería o identificación de extranjería', '3'),
                    (4, 'RC', 'Registro civil de nacimiento', '5'),
                    (5, 'NIP', 'Número de identificación personal (NIP)', '6'),
                    (6, 'NUIP', 'Número único de identificación personal (NUIP)', '7'),
                    (7, 'NES', 'Número de identificación establecido por la Secretaría de Educación', '8'),
                    (8, 'CCB', 'Certificado Cabildo', '9'),
                    (9, 'PPT', 'Permiso de protección temporal', NULL::varchar),
                    (10, 'PEP', 'Permiso especial de permanencia', NULL::varchar)
                ) AS v(ord, valor, nombre, codigo)
                WHERE c."Name" = 'Tipo de documento'
                  AND NOT EXISTS (SELECT 1 FROM "CharacteristicDetail" d
                                  WHERE d."CharacteristicId" = c."CharacteristicId" AND d."Valor" = v.valor)
                ORDER BY v.ord;

                INSERT INTO "CharacteristicDetail" ("CharacteristicId","Nombre","Valor","Codigo","CreationDate")
                SELECT c."CharacteristicId", v.nombre, v.valor, v.codigo, now()
                FROM "Characteristic" c
                CROSS JOIN (VALUES
                    (1, 'F', 'Femenino', NULL::varchar),
                    (2, 'M', 'Masculino', NULL::varchar)
                ) AS v(ord, valor, nombre, codigo)
                WHERE c."Name" = 'Género'
                  AND NOT EXISTS (SELECT 1 FROM "CharacteristicDetail" d
                                  WHERE d."CharacteristicId" = c."CharacteristicId" AND d."Valor" = v.valor)
                ORDER BY v.ord;

                INSERT INTO "CharacteristicDetail" ("CharacteristicId","Nombre","Valor","Codigo","CreationDate")
                SELECT c."CharacteristicId", v.nombre, v.valor, v.codigo, now()
                FROM "Characteristic" c
                CROSS JOIN (VALUES
                    (1, 'O+', 'O+', '1'),
                    (2, 'O-', 'O-', '2'),
                    (3, 'A+', 'A+', '3'),
                    (4, 'A-', 'A-', '4'),
                    (5, 'B+', 'B+', '5'),
                    (6, 'B-', 'B-', '6'),
                    (7, 'AB+', 'AB+', '7'),
                    (8, 'AB-', 'AB-', '8'),
                    (9, 'NS', 'No sabe / No conoce', '9')
                ) AS v(ord, valor, nombre, codigo)
                WHERE c."Name" = 'Tipo de sangre'
                  AND NOT EXISTS (SELECT 1 FROM "CharacteristicDetail" d
                                  WHERE d."CharacteristicId" = c."CharacteristicId" AND d."Valor" = v.valor)
                ORDER BY v.ord;

                INSERT INTO "CharacteristicDetail" ("CharacteristicId","Nombre","Valor","Codigo","CreationDate")
                SELECT c."CharacteristicId", v.nombre, v.valor, v.codigo, now()
                FROM "Characteristic" c
                CROSS JOIN (VALUES
                    (1, 'FAMISANAR LTDA.', 'FAMISANAR LTDA.', NULL::varchar),
                    (2, 'CAFAM - COLSUBSIDIO E.P.S.', 'CAFAM - COLSUBSIDIO E.P.S.', NULL::varchar),
                    (3, 'NUEVA EPS', 'NUEVA EPS', NULL::varchar),
                    (4, 'CAPITAL SALUD EPSS S.A.S.', 'CAPITAL SALUD EPSS S.A.S.', NULL::varchar),
                    (5, 'EPS CONVIDA', 'EPS CONVIDA', NULL::varchar),
                    (6, 'SELVASALUD S.A. E.P.S.', 'SELVASALUD S.A. E.P.S.', NULL::varchar),
                    (7, 'COMPENSAR E.P.S.', 'COMPENSAR E.P.S.', NULL::varchar),
                    (8, 'E.P.S. SANITAS S.A.', 'E.P.S. SANITAS S.A.', NULL::varchar),
                    (9, 'SALUD TOTAL S.A. E.P.S.', 'SALUD TOTAL S.A. E.P.S.', NULL::varchar),
                    (10, 'SOS SERVICIO OCCIDENTAL DE SALUD', 'SOS SERVICIO OCCIDENTAL DE SALUD', NULL::varchar),
                    (11, 'COMFAORIENTE', 'COMFAORIENTE', NULL::varchar),
                    (12, 'DIRECCIÓN DE SANIDAD POLICÍA NACIONAL', 'DIRECCIÓN DE SANIDAD POLICÍA NACIONAL', NULL::varchar),
                    (13, 'CAFESALUD E.P.S. S.A.', 'CAFESALUD E.P.S. S.A.', NULL::varchar),
                    (14, 'COMFACUNDI', 'COMFACUNDI', NULL::varchar),
                    (15, 'ECOOPSOS EPS', 'ECOOPSOS EPS', NULL::varchar),
                    (16, 'SURA', 'SURA', NULL::varchar),
                    (17, 'E.P.S. DE CALDAS S.A.', 'E.P.S. DE CALDAS S.A.', NULL::varchar),
                    (18, 'E.P.S. SALUDCOOP', 'E.P.S. SALUDCOOP', NULL::varchar),
                    (19, 'NUEVA EMPRESA PROMOTORA DE SALUD S.A.', 'NUEVA EMPRESA PROMOTORA DE SALUD S.A.', NULL::varchar),
                    (20, 'COMFENALCO VALLE E.P.S.', 'COMFENALCO VALLE E.P.S.', NULL::varchar),
                    (21, 'COOSALUD EPS', 'COOSALUD EPS', NULL::varchar),
                    (22, 'MUTUALSER EPS', 'MUTUALSER EPS', NULL::varchar),
                    (23, 'SALUD COLPATRIA E.P.S.', 'SALUD COLPATRIA E.P.S.', NULL::varchar),
                    (24, 'MEDIMAS EPS S.A.S.', 'MEDIMAS EPS S.A.S.', NULL::varchar),
                    (25, 'SERVICIO DE SALUD UNIVERSIDAD DEL VALLE', 'SERVICIO DE SALUD UNIVERSIDAD DEL VALLE', NULL::varchar),
                    (26, 'DIRECCIÓN GENERAL DE SANIDAD MILITAR', 'DIRECCIÓN GENERAL DE SANIDAD MILITAR', NULL::varchar),
                    (27, 'COOMEVA E.P.S. S.A.', 'COOMEVA E.P.S. S.A.', NULL::varchar)
                ) AS v(ord, valor, nombre, codigo)
                WHERE c."Name" = 'EPS'
                  AND NOT EXISTS (SELECT 1 FROM "CharacteristicDetail" d
                                  WHERE d."CharacteristicId" = c."CharacteristicId" AND d."Valor" = v.valor)
                ORDER BY v.ord;

                INSERT INTO "CharacteristicDetail" ("CharacteristicId","Nombre","Valor","Codigo","CreationDate")
                SELECT c."CharacteristicId", v.nombre, v.valor, v.codigo, now()
                FROM "Characteristic" c
                CROSS JOIN (VALUES
                    (1, '00', 'No aplica', NULL::varchar),
                    (2, '01', 'Achagua', NULL::varchar),
                    (3, '02', 'Amorúa', NULL::varchar),
                    (4, '03', 'Andoque o Andoke', NULL::varchar),
                    (5, '04', 'Arhuaco (IJKA)', NULL::varchar),
                    (6, '05', 'Awa (CUAIKER)', NULL::varchar),
                    (7, '06', 'Barea', NULL::varchar),
                    (8, '07', 'Barazana', NULL::varchar),
                    (9, '08', 'Barí (Motilón)', NULL::varchar),
                    (10, '09', 'Betoye', NULL::varchar),
                    (11, '10', 'Bora', NULL::varchar),
                    (12, '11', 'Cabiyari o Kawiyarí', NULL::varchar),
                    (13, '12', 'Carapana', NULL::varchar),
                    (14, '13', 'Carijona o Karijona', NULL::varchar),
                    (15, '14', 'Chimila (ETTE E'' NEKA)', NULL::varchar),
                    (16, '15', 'Chiricoa', NULL::varchar),
                    (17, '16', 'Cocama', NULL::varchar),
                    (18, '17', 'Coconuco', NULL::varchar),
                    (19, '18', 'Cofán o Kofán', NULL::varchar),
                    (20, '19', 'Pijaos', NULL::varchar),
                    (21, '20', 'Cubeo o Kubeo', NULL::varchar),
                    (22, '21', 'Cuiba o Kuiba', NULL::varchar),
                    (23, '22', 'Curripaco o Kurripako', NULL::varchar),
                    (24, '23', 'Desano', NULL::varchar),
                    (25, '24', 'Dujos', NULL::varchar),
                    (26, '26', 'Embera Catio o Embera Katío', NULL::varchar),
                    (27, '27', 'Embera Chami', NULL::varchar),
                    (28, '28', 'Eperara Siapidara', NULL::varchar),
                    (29, '29', 'Guambiano', NULL::varchar),
                    (30, '30', 'Guanaca', NULL::varchar),
                    (31, '31', 'Guayabero', NULL::varchar),
                    (32, '33', 'Hitnú', NULL::varchar),
                    (33, '34', 'Inga', NULL::varchar),
                    (34, '35', 'Kamsa o Kamëntsá', NULL::varchar),
                    (35, '36', 'Kogui', NULL::varchar),
                    (36, '37', 'Koreguaje o Coreguaje', NULL::varchar),
                    (37, '38', 'Letuama', NULL::varchar),
                    (38, '39', 'Macaguaje o Makaguaje', NULL::varchar),
                    (39, '40', 'Nukak (Makú)', NULL::varchar),
                    (40, '41', 'Macuna o Makuna (Sara)', NULL::varchar),
                    (41, '42', 'Masiguare', NULL::varchar),
                    (42, '43', 'Matapí', NULL::varchar),
                    (43, '44', 'Miraña', NULL::varchar),
                    (44, '45', 'Muinane', NULL::varchar),
                    (45, '46', 'Muisca', NULL::varchar),
                    (46, '47', 'Nonuya', NULL::varchar),
                    (47, '48', 'Ocaina', NULL::varchar),
                    (48, '49', 'Nasa (Paéz)', NULL::varchar),
                    (49, '50', 'Pastos', NULL::varchar),
                    (50, '51', 'Piapoco (Dzase)', NULL::varchar),
                    (51, '52', 'Piaroa', NULL::varchar),
                    (52, '53', 'Piratapuyo', NULL::varchar),
                    (53, '54', 'Pisamira', NULL::varchar),
                    (54, '55', 'Puinave', NULL::varchar),
                    (55, '56', 'Sáliba', NULL::varchar),
                    (56, '57', 'Sikuani', NULL::varchar),
                    (57, '58', 'Siona', NULL::varchar),
                    (58, '59', 'Siriano', NULL::varchar),
                    (59, '60', 'Siripu o Tsiripu (Mariposo)', NULL::varchar),
                    (60, '61', 'Taiwano (Tajuano)', NULL::varchar),
                    (61, '62', 'TanimuKa', NULL::varchar),
                    (62, '63', 'Tariano', NULL::varchar),
                    (63, '64', 'Tatuyo', NULL::varchar),
                    (64, '65', 'Tikuna', NULL::varchar),
                    (65, '66', 'Totoró', NULL::varchar),
                    (66, '67', 'Tucano (Desea) o Tukano', NULL::varchar),
                    (67, '68', 'Tule (Kuna)', NULL::varchar),
                    (68, '69', 'Tuyuka (Dojkapuara)', NULL::varchar),
                    (69, '70', 'U´wa (Tunebo)', NULL::varchar),
                    (70, '71', 'Wanano', NULL::varchar),
                    (71, '72', 'Wayuu', NULL::varchar),
                    (72, '73', 'Witoto', NULL::varchar),
                    (73, '74', 'Wiwa (Arzario)', NULL::varchar),
                    (74, '75', 'Waunan (Wuanana)', NULL::varchar),
                    (75, '76', 'Yagua', NULL::varchar),
                    (76, '77', 'Yanacona', NULL::varchar),
                    (77, '78', 'Yauna', NULL::varchar),
                    (78, '79', 'Yukuna', NULL::varchar),
                    (79, '80', 'Yuko (Yukpa)', NULL::varchar),
                    (80, '81', 'Yurí (Carabayo)', NULL::varchar),
                    (81, '82', 'Yuruti', NULL::varchar),
                    (82, '83', 'Zenú', NULL::varchar),
                    (83, '84', 'Quillacingas', NULL::varchar),
                    (84, '200', 'Negritudes', NULL::varchar),
                    (85, '400', 'Rom', NULL::varchar)
                ) AS v(ord, valor, nombre, codigo)
                WHERE c."Name" = 'Grupo étnico'
                  AND NOT EXISTS (SELECT 1 FROM "CharacteristicDetail" d
                                  WHERE d."CharacteristicId" = c."CharacteristicId" AND d."Valor" = v.valor)
                ORDER BY v.ord;

                INSERT INTO "CharacteristicDetail" ("CharacteristicId","Nombre","Valor","Codigo","CreationDate")
                SELECT c."CharacteristicId", v.nombre, v.valor, v.codigo, now()
                FROM "Characteristic" c
                CROSS JOIN (VALUES
                    (1, '1', 'Sordera profunda', NULL::varchar),
                    (2, '2', 'Hipoacusia o baja audición', NULL::varchar),
                    (3, '3', 'Baja visión diagnosticada', NULL::varchar),
                    (4, '4', 'Ceguera', NULL::varchar),
                    (5, '5', 'Parálisis cerebral', NULL::varchar),
                    (6, '6', 'Lesión neuromuscular', NULL::varchar),
                    (7, '7', 'Autismo', NULL::varchar),
                    (8, '8', 'Deficiencia cognitiva (retardo mental)', NULL::varchar),
                    (9, '9', 'Síndrome de Down', NULL::varchar),
                    (10, '10', 'Múltiple', NULL::varchar),
                    (11, '99', 'No aplica', NULL::varchar)
                ) AS v(ord, valor, nombre, codigo)
                WHERE c."Name" = 'Tipo de discapacidad'
                  AND NOT EXISTS (SELECT 1 FROM "CharacteristicDetail" d
                                  WHERE d."CharacteristicId" = c."CharacteristicId" AND d."Valor" = v.valor)
                ORDER BY v.ord;

                INSERT INTO "CharacteristicDetail" ("CharacteristicId","Nombre","Valor","Codigo","CreationDate")
                SELECT c."CharacteristicId", v.nombre, v.valor, v.codigo, now()
                FROM "Characteristic" c
                CROSS JOIN (VALUES
                    (1, '1', 'Superdotado', NULL::varchar),
                    (2, '2', 'Con talento científico', NULL::varchar),
                    (3, '3', 'Con talento tecnológico', NULL::varchar),
                    (4, '4', 'Con talento subjetivo', NULL::varchar),
                    (5, '9', 'No aplica', NULL::varchar)
                ) AS v(ord, valor, nombre, codigo)
                WHERE c."Name" = 'Capacidades excepcionales'
                  AND NOT EXISTS (SELECT 1 FROM "CharacteristicDetail" d
                                  WHERE d."CharacteristicId" = c."CharacteristicId" AND d."Valor" = v.valor)
                ORDER BY v.ord;

                INSERT INTO "CharacteristicDetail" ("CharacteristicId","Nombre","Valor","Codigo","CreationDate")
                SELECT c."CharacteristicId", v.nombre, v.valor, v.codigo, now()
                FROM "Characteristic" c
                CROSS JOIN (VALUES
                    (1, '1', 'En situación de desplazamiento', NULL::varchar),
                    (2, '2', 'Desvinculados de grupos armados', NULL::varchar),
                    (3, '3', 'Hijos de adultos desmovilizados', NULL::varchar),
                    (4, '9', 'No aplica', NULL::varchar)
                ) AS v(ord, valor, nombre, codigo)
                WHERE c."Name" = 'Víctima del conflicto'
                  AND NOT EXISTS (SELECT 1 FROM "CharacteristicDetail" d
                                  WHERE d."CharacteristicId" = c."CharacteristicId" AND d."Valor" = v.valor)
                ORDER BY v.ord;

                INSERT INTO "CharacteristicDetail" ("CharacteristicId","Nombre","Valor","Codigo","CreationDate")
                SELECT c."CharacteristicId", v.nombre, v.valor, v.codigo, now()
                FROM "Characteristic" c
                CROSS JOIN (VALUES
                    (1, '1', 'Educación Tradicional', NULL::varchar),
                    (2, '2', 'Escuela nueva', NULL::varchar),
                    (3, '3', 'Post primaria', NULL::varchar),
                    (4, '4', 'Telesecundaria', NULL::varchar),
                    (5, '5', 'SER', NULL::varchar),
                    (6, '6', 'CAFAM', NULL::varchar),
                    (7, '7', 'SAT', NULL::varchar),
                    (8, '8', 'Etnoeducación', NULL::varchar),
                    (9, '9', 'Aceleración del Aprendizaje', NULL::varchar),
                    (10, '10', 'Programa para Jóvenes en extraedad y Adultos (Decreto 3011)', NULL::varchar),
                    (11, '11', 'Preescolar Escolarizado', NULL::varchar),
                    (12, '12', 'Preescolar No Escolarizado/Semiescolarizado', NULL::varchar),
                    (13, '13', 'SAT presencial', NULL::varchar),
                    (14, '14', 'Media Rural', NULL::varchar),
                    (15, '15', 'Entorno Comunidad', NULL::varchar),
                    (16, '16', 'Entorno Institucional', NULL::varchar),
                    (17, '17', 'Entorno Familiar', NULL::varchar),
                    (18, '18', 'Círculos de Aprendizaje', NULL::varchar),
                    (19, '19', 'Transformemos', NULL::varchar),
                    (20, '20', 'Adultos', NULL::varchar),
                    (21, '21', 'Modalidad Virtual Asistida UCN', NULL::varchar),
                    (22, '22', 'Grupos Juveniles Creativos', NULL::varchar),
                    (23, '23', 'Bachillerato Pacicultor', NULL::varchar),
                    (24, '24', 'Crecer', NULL::varchar),
                    (25, '25', 'Niños y Jóvenes', NULL::varchar)
                ) AS v(ord, valor, nombre, codigo)
                WHERE c."Name" = 'Metodología'
                  AND NOT EXISTS (SELECT 1 FROM "CharacteristicDetail" d
                                  WHERE d."CharacteristicId" = c."CharacteristicId" AND d."Valor" = v.valor)
                ORDER BY v.ord;

                INSERT INTO "CharacteristicDetail" ("CharacteristicId","Nombre","Valor","Codigo","CreationDate")
                SELECT c."CharacteristicId", v.nombre, v.valor, v.codigo, now()
                FROM "Characteristic" c
                CROSS JOIN (VALUES
                    (1, 'OBLIGATORIO', 'Registro civil', NULL::varchar),
                    (2, 'OBLIGATORIO', 'Certificado de estudios del año anterior', NULL::varchar),
                    (3, 'OBLIGATORIO', 'Carné de vacunas', NULL::varchar),
                    (4, 'OBLIGATORIO', 'Paz y salvo', NULL::varchar),
                    (5, 'OPCIONAL', 'Certificación de retiro del SIMAT', NULL::varchar)
                ) AS v(ord, valor, nombre, codigo)
                WHERE c."Name" = 'Documentos de matrícula'
                  AND NOT EXISTS (SELECT 1 FROM "CharacteristicDetail" d
                                  WHERE d."CharacteristicId" = c."CharacteristicId" AND d."Nombre" = v.nombre)
                ORDER BY v.ord;

                INSERT INTO "Characteristic" ("Name","CreationDate")
                SELECT 'Parentesco', now()
                WHERE NOT EXISTS (SELECT 1 FROM "ColumnConfigurations" cc
                                  WHERE cc."TableName" = 'Parent' AND cc."ColumnName" = 'Relationship' AND cc."CharacteristicId" IS NOT NULL)
                  AND NOT EXISTS (SELECT 1 FROM "Characteristic" c WHERE c."Name" = 'Parentesco');

                INSERT INTO "CharacteristicDetail" ("CharacteristicId","Nombre","Valor","Codigo","CreationDate")
                SELECT c."CharacteristicId", v.nombre, v.valor, v.codigo, now()
                FROM "Characteristic" c
                CROSS JOIN (VALUES
                    (1, 'FATHER', 'Padre', NULL::varchar),
                    (2, 'MOTHER', 'Madre', NULL::varchar),
                    (3, 'GRANDFATHER', 'Abuelo', NULL::varchar),
                    (4, 'GRANDMOTHER', 'Abuela', NULL::varchar),
                    (5, 'UNCLE', 'Tío', NULL::varchar),
                    (6, 'AUNT', 'Tía', NULL::varchar),
                    (7, 'BROTHER', 'Hermano', NULL::varchar),
                    (8, 'SISTER', 'Hermana', NULL::varchar),
                    (9, 'OTHER', 'Otro', NULL::varchar)
                ) AS v(ord, valor, nombre, codigo)
                WHERE c."Name" = 'Parentesco'
                  AND NOT EXISTS (SELECT 1 FROM "ColumnConfigurations" cc
                                  WHERE cc."TableName" = 'Parent' AND cc."ColumnName" = 'Relationship' AND cc."CharacteristicId" IS NOT NULL)
                  AND NOT EXISTS (SELECT 1 FROM "CharacteristicDetail" d WHERE d."CharacteristicId" = c."CharacteristicId")
                ORDER BY v.ord;

                UPDATE "ColumnConfigurations" cc
                SET "CharacteristicId" = c."CharacteristicId"
                FROM "Characteristic" c
                JOIN (VALUES
                    ('User', 'DocumentType', 'Tipo de documento'),
                    ('Student', 'Gender', 'Género'),
                    ('Student', 'BloodType', 'Tipo de sangre'),
                    ('Student', 'Eps', 'EPS'),
                    ('Student', 'EthnicGroup', 'Grupo étnico'),
                    ('Student', 'DisabilityType', 'Tipo de discapacidad'),
                    ('Student', 'ExceptionalCapacity', 'Capacidades excepcionales'),
                    ('Student', 'ConflictVictim', 'Víctima del conflicto'),
                    ('Student', 'Methodology', 'Metodología'),
                    ('Parent', 'Relationship', 'Parentesco')
                ) AS v(tbl, col, name) ON c."Name" = v.name
                WHERE cc."TableName" = v.tbl AND cc."ColumnName" = v.col AND cc."CharacteristicId" IS NULL;

                INSERT INTO "ColumnConfigurations" ("TableName","ColumnName","Filterable","Visible","Alias","CreationDate","CharacteristicId")
                SELECT v.tbl, v.col, false, true, NULL, now(), c."CharacteristicId"
                FROM (VALUES
                    ('User', 'DocumentType', 'Tipo de documento'),
                    ('Student', 'Gender', 'Género'),
                    ('Student', 'BloodType', 'Tipo de sangre'),
                    ('Student', 'Eps', 'EPS'),
                    ('Student', 'EthnicGroup', 'Grupo étnico'),
                    ('Student', 'DisabilityType', 'Tipo de discapacidad'),
                    ('Student', 'ExceptionalCapacity', 'Capacidades excepcionales'),
                    ('Student', 'ConflictVictim', 'Víctima del conflicto'),
                    ('Student', 'Methodology', 'Metodología'),
                    ('Parent', 'Relationship', 'Parentesco')
                ) AS v(tbl, col, name)
                JOIN "Characteristic" c ON c."Name" = v.name
                WHERE NOT EXISTS (SELECT 1 FROM "ColumnConfigurations" cc
                                  WHERE cc."TableName" = v.tbl AND cc."ColumnName" = v.col);

                INSERT INTO "NotificationType" ("Code","Name","Category","Template","Active")
                SELECT v.code, v.name, v.category, v.template, true
                FROM (VALUES
                    ('ENROLL_DOCS_INCOMPLETE', 'Documentación incompleta', 'Matrícula', '{estudiante} ({documento}) fue admitido para {anio} con documentación incompleta. Falta: {documentos}.'),
                    ('SIMAT_DATA_MISMATCH', 'Diferencias con el SIMAT', 'SIMAT', '{estudiante} ({documento}) tiene datos distintos en LITARO y en el SIMAT de {anio}: {diferencias}. LITARO conserva su valor.')
                ) AS v(code, name, category, template)
                WHERE NOT EXISTS (SELECT 1 FROM "NotificationType" t WHERE t."Code" = v.code);

                INSERT INTO "NotificationTypeRecipient" ("NotificationTypeId","RoleId")
                SELECT t."NotificationTypeId", x."RoleId"
                FROM "NotificationType" t
                CROSS JOIN (SELECT DISTINCT r."RoleId"
                            FROM "NotificationTypeRecipient" r
                            JOIN "NotificationType" s ON s."NotificationTypeId" = r."NotificationTypeId"
                            WHERE s."Category" = 'SIMAT' AND s."Code" NOT IN ('ENROLL_DOCS_INCOMPLETE','SIMAT_DATA_MISMATCH') AND r."RoleId" IS NOT NULL) x
                WHERE t."Code" IN ('ENROLL_DOCS_INCOMPLETE','SIMAT_DATA_MISMATCH')
                  AND NOT EXISTS (SELECT 1 FROM "NotificationTypeRecipient" y
                                  WHERE y."NotificationTypeId" = t."NotificationTypeId" AND y."RoleId" = x."RoleId");

                INSERT INTO "RolePermission" ("RoleId","TableName","Permissions")
                SELECT r."Id", 'EnrollmentSettings', 15
                FROM "AspNetRoles" r
                WHERE r."Name" = 'Administrador'
                ON CONFLICT ("RoleId","TableName") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "RolePermission" WHERE "TableName" = 'EnrollmentSettings';

                DELETE FROM "NotificationRead" WHERE "NotificationId" IN (
                    SELECT n."NotificationId" FROM "Notification" n
                    JOIN "NotificationType" t ON t."NotificationTypeId" = n."NotificationTypeId"
                    WHERE t."Code" IN ('ENROLL_DOCS_INCOMPLETE','SIMAT_DATA_MISMATCH'));

                DELETE FROM "Notification" WHERE "NotificationTypeId" IN (
                    SELECT "NotificationTypeId" FROM "NotificationType" WHERE "Code" IN ('ENROLL_DOCS_INCOMPLETE','SIMAT_DATA_MISMATCH'));

                DELETE FROM "NotificationTypeRecipient" WHERE "NotificationTypeId" IN (
                    SELECT "NotificationTypeId" FROM "NotificationType" WHERE "Code" IN ('ENROLL_DOCS_INCOMPLETE','SIMAT_DATA_MISMATCH'));

                DELETE FROM "NotificationType" WHERE "Code" IN ('ENROLL_DOCS_INCOMPLETE','SIMAT_DATA_MISMATCH');

                UPDATE "ColumnConfigurations" SET "CharacteristicId" = NULL
                WHERE "CharacteristicId" IN (SELECT "CharacteristicId" FROM "Characteristic" WHERE "Name" IN ('Tipo de documento', 'Género', 'Tipo de sangre', 'EPS', 'Grupo étnico', 'Tipo de discapacidad', 'Capacidades excepcionales', 'Víctima del conflicto', 'Metodología', 'Documentos de matrícula'));

                DELETE FROM "CharacteristicDetail"
                WHERE "CharacteristicId" IN (SELECT "CharacteristicId" FROM "Characteristic" WHERE "Name" IN ('Tipo de documento', 'Género', 'Tipo de sangre', 'EPS', 'Grupo étnico', 'Tipo de discapacidad', 'Capacidades excepcionales', 'Víctima del conflicto', 'Metodología', 'Documentos de matrícula'));

                DELETE FROM "Characteristic" WHERE "Name" IN ('Tipo de documento', 'Género', 'Tipo de sangre', 'EPS', 'Grupo étnico', 'Tipo de discapacidad', 'Capacidades excepcionales', 'Víctima del conflicto', 'Metodología', 'Documentos de matrícula');
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_AdmissionRequest_Campus_CampusId",
                schema: "public",
                table: "AdmissionRequest");

            migrationBuilder.DropForeignKey(
                name: "FK_AdmissionRequest_Student_StudentId",
                schema: "public",
                table: "AdmissionRequest");

            migrationBuilder.DropIndex(
                name: "IX_AdmissionRequest_CampusId",
                schema: "public",
                table: "AdmissionRequest");

            migrationBuilder.DropIndex(
                name: "IX_AdmissionRequest_StudentId",
                schema: "public",
                table: "AdmissionRequest");

            migrationBuilder.DropColumn(
                name: "ConflictVictim",
                schema: "public",
                table: "Student");

            migrationBuilder.DropColumn(
                name: "DisabilityType",
                schema: "public",
                table: "Student");

            migrationBuilder.DropColumn(
                name: "EthnicGroup",
                schema: "public",
                table: "Student");

            migrationBuilder.DropColumn(
                name: "ExceptionalCapacity",
                schema: "public",
                table: "Student");

            migrationBuilder.DropColumn(
                name: "Methodology",
                schema: "public",
                table: "Student");

            migrationBuilder.DropColumn(
                name: "Address",
                schema: "public",
                table: "Parent");

            migrationBuilder.DropColumn(
                name: "FileRemovedAt",
                schema: "public",
                table: "Document");

            migrationBuilder.DropColumn(
                name: "Codigo",
                schema: "public",
                table: "CharacteristicDetail");

            migrationBuilder.DropColumn(
                name: "BloodType",
                schema: "public",
                table: "AdmissionRequest");

            migrationBuilder.DropColumn(
                name: "CampusId",
                schema: "public",
                table: "AdmissionRequest");

            migrationBuilder.DropColumn(
                name: "ConflictVictim",
                schema: "public",
                table: "AdmissionRequest");

            migrationBuilder.DropColumn(
                name: "DisabilityType",
                schema: "public",
                table: "AdmissionRequest");

            migrationBuilder.DropColumn(
                name: "Email",
                schema: "public",
                table: "AdmissionRequest");

            migrationBuilder.DropColumn(
                name: "Eps",
                schema: "public",
                table: "AdmissionRequest");

            migrationBuilder.DropColumn(
                name: "EthnicGroup",
                schema: "public",
                table: "AdmissionRequest");

            migrationBuilder.DropColumn(
                name: "ExceptionalCapacity",
                schema: "public",
                table: "AdmissionRequest");

            migrationBuilder.DropColumn(
                name: "Gender",
                schema: "public",
                table: "AdmissionRequest");

            migrationBuilder.DropColumn(
                name: "GuardianAddress",
                schema: "public",
                table: "AdmissionRequest");

            migrationBuilder.DropColumn(
                name: "GuardianDocumentNumber",
                schema: "public",
                table: "AdmissionRequest");

            migrationBuilder.DropColumn(
                name: "GuardianDocumentType",
                schema: "public",
                table: "AdmissionRequest");

            migrationBuilder.DropColumn(
                name: "GuardianEmail",
                schema: "public",
                table: "AdmissionRequest");

            migrationBuilder.DropColumn(
                name: "GuardianFirstName",
                schema: "public",
                table: "AdmissionRequest");

            migrationBuilder.DropColumn(
                name: "GuardianLastName",
                schema: "public",
                table: "AdmissionRequest");

            migrationBuilder.DropColumn(
                name: "GuardianRelationship",
                schema: "public",
                table: "AdmissionRequest");

            migrationBuilder.DropColumn(
                name: "Methodology",
                schema: "public",
                table: "AdmissionRequest");

            migrationBuilder.DropColumn(
                name: "StudentId",
                schema: "public",
                table: "AdmissionRequest");
        }
    }
}

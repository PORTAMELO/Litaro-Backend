using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Litaro.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("SET LOCAL lock_timeout = '5s';");

            migrationBuilder.CreateTable(
                name: "AuditLog",
                schema: "public",
                columns: table => new
                {
                    AuditLogId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "clock_timestamp()"),
                    EventType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TableName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RecordId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    OldData = table.Column<string>(type: "jsonb", nullable: true),
                    NewData = table.Column<string>(type: "jsonb", nullable: true),
                    ChangedColumns = table.Column<string[]>(type: "text[]", nullable: true),
                    ActorKind = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    UserName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DbUser = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValueSql: "current_user"),
                    Source = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Detail = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLog", x => x.AuditLogId);
                    table.CheckConstraint("CK_AuditLog_ActorKind", "\"ActorKind\" IN ('USER','SYSTEM','DIRECT_DB','ANONYMOUS')");
                    table.CheckConstraint("CK_AuditLog_EventType", "\"EventType\" IN ('INSERT','UPDATE','DELETE','LOGIN_OK','LOGIN_FAIL','LOGOUT','DOC_UPLOAD','DOC_UPDATE','DOC_REPLACE','DOC_REPLACED','DOC_REMOVE','DOC_PURGE')");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_EventType_OccurredAt",
                schema: "public",
                table: "AuditLog",
                columns: new[] { "EventType", "OccurredAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_OccurredAt",
                schema: "public",
                table: "AuditLog",
                column: "OccurredAt",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_OperationId",
                schema: "public",
                table: "AuditLog",
                column: "OperationId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_TableName_RecordId_OccurredAt",
                schema: "public",
                table: "AuditLog",
                columns: new[] { "TableName", "RecordId", "OccurredAt" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_UserId_OccurredAt",
                schema: "public",
                table: "AuditLog",
                columns: new[] { "UserId", "OccurredAt" },
                descending: new[] { false, true });

            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION audit_excluded_tables() RETURNS text[]
                LANGUAGE sql IMMUTABLE AS $$
                  SELECT ARRAY['AuditLog','JobRun','NotificationRead','__EFMigrationsHistory','AspNetUserTokens']::text[]
                $$;

                CREATE OR REPLACE FUNCTION audit_row_change() RETURNS trigger
                LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
                DECLARE
                  masked constant text[] := ARRAY['PasswordHash','SecurityStamp','ConcurrencyStamp'];
                  old_row jsonb := CASE WHEN TG_OP <> 'INSERT' THEN to_jsonb(OLD) END;
                  new_row jsonb := CASE WHEN TG_OP <> 'DELETE' THEN to_jsonb(NEW) END;
                  row_for_key jsonb := coalesce(new_row, old_row);
                  changed text[]; record_id text; k text;
                  app text := nullif(current_setting('litaro.app', true), '');
                  actor text := nullif(current_setting('litaro.actor_kind', true), '');
                  src text := nullif(current_setting('litaro.source', true), '');
                BEGIN
                  IF TG_OP = 'UPDATE' THEN
                    SELECT array_agg(n.key ORDER BY n.key) INTO changed
                      FROM jsonb_each(new_row) n
                     WHERE n.key <> 'ConcurrencyStamp' AND n.value IS DISTINCT FROM old_row -> n.key;
                    IF changed IS NULL THEN RETURN NULL; END IF;
                  END IF;
                  FOREACH k IN ARRAY masked LOOP
                    IF jsonb_typeof(old_row -> k) <> 'null' THEN old_row := jsonb_set(old_row, ARRAY[k], '"***"'); END IF;
                    IF jsonb_typeof(new_row -> k) <> 'null' THEN new_row := jsonb_set(new_row, ARRAY[k], '"***"'); END IF;
                  END LOOP;
                  IF TG_NARGS = 1 THEN
                    record_id := row_for_key ->> TG_ARGV[0];
                  ELSE
                    SELECT string_agg(a || '=' || (row_for_key ->> a), ';' ORDER BY i) INTO record_id
                      FROM unnest(TG_ARGV) WITH ORDINALITY AS t(a, i);
                  END IF;
                  IF app IS NULL THEN actor := 'DIRECT_DB'; src := NULL;
                  ELSIF actor IS NULL THEN actor := 'SYSTEM'; src := coalesce(src, 'Litaro (sin contexto)');
                  END IF;
                  INSERT INTO "AuditLog"("EventType","TableName","RecordId","OldData","NewData","ChangedColumns",
                    "ActorKind","UserId","UserName","DbUser","Source","IpAddress","UserAgent","OperationId")
                  VALUES (TG_OP, TG_TABLE_NAME, left(record_id, 200), old_row, new_row, changed, actor,
                    CASE WHEN app IS NULL THEN NULL ELSE nullif(current_setting('litaro.user_id', true), '')::int END,
                    CASE WHEN app IS NULL THEN NULL ELSE nullif(current_setting('litaro.user_name', true), '') END,
                    session_user,
                    src,
                    CASE WHEN app IS NULL THEN NULL ELSE nullif(current_setting('litaro.ip', true), '') END,
                    CASE WHEN app IS NULL THEN NULL ELSE nullif(current_setting('litaro.user_agent', true), '') END,
                    CASE WHEN app IS NULL THEN NULL ELSE nullif(current_setting('litaro.operation_id', true), '')::uuid END);
                  RETURN NULL;
                END $$;

                CREATE OR REPLACE FUNCTION audit_attach(tbl text) RETURNS void
                LANGUAGE plpgsql SET search_path = public, pg_temp AS $$
                DECLARE
                  key_args text;
                BEGIN
                  SELECT string_agg(quote_literal(a.attname), ', ' ORDER BY k.ord) INTO key_args
                    FROM pg_index i
                    CROSS JOIN LATERAL unnest(i.indkey::int2[]) WITH ORDINALITY AS k(attnum, ord)
                    JOIN pg_attribute a ON a.attrelid = i.indrelid AND a.attnum = k.attnum
                   WHERE i.indrelid = format('public.%I', tbl)::regclass AND i.indisprimary;
                  IF key_args IS NULL THEN
                    RAISE EXCEPTION 'La tabla % no tiene clave primaria.', tbl;
                  END IF;
                  IF EXISTS (SELECT 1 FROM pg_trigger
                              WHERE tgrelid = format('public.%I', tbl)::regclass AND tgname = 'audit_row') THEN
                    EXECUTE format('DROP TRIGGER audit_row ON public.%I', tbl);
                  END IF;
                  EXECUTE format('CREATE TRIGGER audit_row AFTER INSERT OR UPDATE OR DELETE ON public.%I FOR EACH ROW EXECUTE FUNCTION audit_row_change(%s)', tbl, key_args);
                END $$;

                CREATE OR REPLACE FUNCTION audit_uncovered_tables() RETURNS SETOF text
                LANGUAGE sql STABLE SET search_path = public, pg_temp AS $$
                  SELECT t.tablename::text
                    FROM pg_tables t
                   WHERE t.schemaname = 'public'
                     AND NOT (t.tablename::text = ANY (audit_excluded_tables()))
                     AND NOT EXISTS (
                       SELECT 1 FROM pg_trigger g
                        WHERE g.tgrelid = format('public.%I', t.tablename)::regclass
                          AND g.tgname = 'audit_row'
                          AND g.tgenabled <> 'D'
                          AND NOT g.tgisinternal)
                   ORDER BY 1
                $$;

                CREATE OR REPLACE FUNCTION audit_block_change() RETURNS trigger
                LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
                BEGIN
                  RAISE EXCEPTION 'La auditoría no se puede modificar ni eliminar.';
                END $$;

                CREATE TRIGGER audit_block_row BEFORE UPDATE OR DELETE ON "AuditLog"
                  FOR EACH ROW EXECUTE FUNCTION audit_block_change();
                CREATE TRIGGER audit_block_truncate BEFORE TRUNCATE ON "AuditLog"
                  FOR EACH STATEMENT EXECUTE FUNCTION audit_block_change();

                ALTER TABLE "AuditLog" ENABLE ROW LEVEL SECURITY;

                DO $$
                DECLARE
                  r text;
                  seq text := pg_get_serial_sequence('public."AuditLog"', 'AuditLogId');
                BEGIN
                  FOR r IN
                    SELECT rolname::text FROM pg_roles
                     WHERE rolname IN ('anon', 'authenticated', 'service_role')
                     ORDER BY rolname
                  LOOP
                    EXECUTE format('REVOKE ALL ON TABLE public."AuditLog" FROM %I', r);
                    EXECUTE format('REVOKE ALL ON SEQUENCE %s FROM %I', seq, r);
                  END LOOP;
                END $$;

                DO $$
                DECLARE
                  t text;
                BEGIN
                  FOR t IN
                    SELECT tablename::text FROM pg_tables
                     WHERE schemaname = 'public' AND NOT (tablename::text = ANY (audit_excluded_tables()))
                     ORDER BY tablename
                  LOOP
                    PERFORM audit_attach(t);
                  END LOOP;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                DECLARE
                  t text;
                BEGIN
                  FOR t IN
                    SELECT c.relname::text
                      FROM pg_trigger g
                      JOIN pg_class c ON c.oid = g.tgrelid
                      JOIN pg_namespace n ON n.oid = c.relnamespace
                     WHERE n.nspname = 'public' AND g.tgname = 'audit_row'
                  LOOP
                    EXECUTE format('DROP TRIGGER audit_row ON public.%I', t);
                  END LOOP;
                END $$;

                DROP TRIGGER IF EXISTS audit_block_row ON "AuditLog";
                DROP TRIGGER IF EXISTS audit_block_truncate ON "AuditLog";
                DROP FUNCTION IF EXISTS audit_uncovered_tables();
                DROP FUNCTION IF EXISTS audit_attach(text);
                DROP FUNCTION IF EXISTS audit_row_change();
                DROP FUNCTION IF EXISTS audit_block_change();
                DROP FUNCTION IF EXISTS audit_excluded_tables();
                """);

            migrationBuilder.DropTable(
                name: "AuditLog",
                schema: "public");
        }
    }
}

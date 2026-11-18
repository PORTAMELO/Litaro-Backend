using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Litaro.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditDocumentEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                SET LOCAL lock_timeout = '5s';

                CREATE OR REPLACE FUNCTION audit_write(event_type text, table_name text, record_id text,
                  old_row jsonb, new_row jsonb, changed text[], detail jsonb) RETURNS void
                LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
                DECLARE
                  app text := nullif(current_setting('litaro.app', true), '');
                  actor text := nullif(current_setting('litaro.actor_kind', true), '');
                  src text := nullif(current_setting('litaro.source', true), '');
                BEGIN
                  IF pg_trigger_depth() = 0 THEN
                    RAISE EXCEPTION 'La auditoría solo se escribe desde un trigger.';
                  END IF;
                  IF app IS NULL THEN actor := 'DIRECT_DB'; src := NULL;
                  ELSIF actor IS NULL THEN actor := 'SYSTEM'; src := coalesce(src, 'Litaro (sin contexto)');
                  END IF;
                  INSERT INTO "AuditLog"("EventType","TableName","RecordId","OldData","NewData","ChangedColumns",
                    "ActorKind","UserId","UserName","DbUser","Source","IpAddress","UserAgent","OperationId","Detail")
                  VALUES (event_type, table_name, left(record_id, 200), old_row, new_row, changed, actor,
                    CASE WHEN app IS NULL THEN NULL ELSE nullif(current_setting('litaro.user_id', true), '')::int END,
                    CASE WHEN app IS NULL THEN NULL ELSE nullif(current_setting('litaro.user_name', true), '') END,
                    session_user,
                    src,
                    CASE WHEN app IS NULL THEN NULL ELSE nullif(current_setting('litaro.ip', true), '') END,
                    CASE WHEN app IS NULL THEN NULL ELSE nullif(current_setting('litaro.user_agent', true), '') END,
                    CASE WHEN app IS NULL THEN NULL ELSE nullif(current_setting('litaro.operation_id', true), '')::uuid END,
                    detail);
                END $$;

                REVOKE ALL ON FUNCTION audit_write(text, text, text, jsonb, jsonb, text[], jsonb) FROM PUBLIC;

                DO $$
                DECLARE
                  r text;
                BEGIN
                  FOR r IN
                    SELECT rolname::text FROM pg_roles
                     WHERE rolname IN ('anon', 'authenticated', 'service_role')
                     ORDER BY rolname
                  LOOP
                    EXECUTE format('REVOKE ALL ON FUNCTION audit_write(text, text, text, jsonb, jsonb, text[], jsonb) FROM %I', r);
                  END LOOP;
                END $$;

                CREATE OR REPLACE FUNCTION audit_row_change() RETURNS trigger
                LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
                DECLARE
                  masked constant text[] := ARRAY['PasswordHash','SecurityStamp','ConcurrencyStamp'];
                  old_row jsonb := CASE WHEN TG_OP <> 'INSERT' THEN to_jsonb(OLD) END;
                  new_row jsonb := CASE WHEN TG_OP <> 'DELETE' THEN to_jsonb(NEW) END;
                  row_for_key jsonb := coalesce(new_row, old_row);
                  changed text[]; record_id text; k text;
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
                  PERFORM audit_write(TG_OP, TG_TABLE_NAME, record_id, old_row, new_row, changed, NULL);
                  RETURN NULL;
                END $$;

                CREATE OR REPLACE FUNCTION audit_document_change() RETURNS trigger
                LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
                DECLARE
                  old_row jsonb := CASE WHEN TG_OP <> 'INSERT' THEN to_jsonb(OLD) END;
                  new_row jsonb := CASE WHEN TG_OP <> 'DELETE' THEN to_jsonb(NEW) END;
                  doc jsonb := coalesce(new_row, old_row);
                  op uuid := CASE WHEN nullif(current_setting('litaro.app', true), '') IS NOT NULL
                                  THEN nullif(current_setting('litaro.operation_id', true), '')::uuid END;
                  changed text[];
                  moved boolean := false;
                  event_type text;
                  detail jsonb;
                BEGIN
                  IF TG_OP = 'UPDATE' THEN
                    SELECT array_agg(n.key ORDER BY n.key) INTO changed
                      FROM jsonb_each(new_row) n
                     WHERE n.value IS DISTINCT FROM old_row -> n.key;
                    IF changed IS NULL THEN RETURN NULL; END IF;
                    moved := changed && ARRAY['EntityTable','EntityId'];
                  END IF;
                  detail := jsonb_build_object(
                    'DocumentType', doc -> 'DocumentType',
                    'FileName', doc -> 'FileName',
                    'EntityTable', doc -> 'EntityTable',
                    'EntityId', doc -> 'EntityId',
                    'Module', doc -> 'Module',
                    'SizeBytes', doc -> 'SizeBytes');
                  IF TG_OP = 'DELETE' THEN
                    event_type := 'DOC_REMOVE';
                  ELSIF TG_OP = 'UPDATE' AND old_row -> 'IsCurrent' = 'true'::jsonb AND new_row -> 'IsCurrent' = 'false'::jsonb THEN
                    IF current_setting('litaro.action', true) = 'DOC_REPLACE' THEN
                      event_type := 'DOC_REPLACED';
                      detail := detail || jsonb_build_object('role', 'previous');
                    ELSE
                      event_type := 'DOC_REMOVE';
                    END IF;
                  ELSIF TG_OP = 'INSERT' OR moved THEN
                    IF op IS NOT NULL AND EXISTS (
                      SELECT 1 FROM "AuditLog" a
                       WHERE a."OperationId" = op
                         AND a."EventType" = 'DOC_REPLACED'
                         AND a."Detail" -> 'DocumentType' = doc -> 'DocumentType'
                         AND a."Detail" -> 'EntityTable' = doc -> 'EntityTable'
                         AND a."Detail" -> 'EntityId' = doc -> 'EntityId') THEN
                      event_type := 'DOC_REPLACE';
                      detail := detail || jsonb_build_object('role', 'current');
                    ELSIF TG_OP = 'INSERT' THEN
                      event_type := 'DOC_UPLOAD';
                    ELSE
                      event_type := 'DOC_UPDATE';
                    END IF;
                    IF moved THEN
                      detail := detail || jsonb_build_object('movedFrom', jsonb_build_object(
                        'EntityTable', old_row -> 'EntityTable',
                        'EntityId', old_row -> 'EntityId'));
                    END IF;
                  ELSIF changed = ARRAY['FileRemovedAt'] AND old_row -> 'FileRemovedAt' = 'null'::jsonb THEN
                    event_type := 'DOC_PURGE';
                  ELSE
                    event_type := 'DOC_UPDATE';
                  END IF;
                  PERFORM audit_write(event_type, TG_TABLE_NAME, doc ->> 'DocumentId', old_row, new_row, changed, detail);
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
                  IF tbl = 'Document' THEN
                    EXECUTE format('CREATE TRIGGER audit_row AFTER INSERT OR UPDATE OR DELETE ON public.%I FOR EACH ROW EXECUTE FUNCTION audit_document_change()', tbl);
                  ELSE
                    EXECUTE format('CREATE TRIGGER audit_row AFTER INSERT OR UPDATE OR DELETE ON public.%I FOR EACH ROW EXECUTE FUNCTION audit_row_change(%s)', tbl, key_args);
                  END IF;
                END $$;

                DROP TRIGGER audit_row ON "Document";
                CREATE TRIGGER audit_row AFTER INSERT OR UPDATE OR DELETE ON "Document"
                  FOR EACH ROW EXECUTE FUNCTION audit_document_change();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
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

                SELECT audit_attach('Document');

                DROP FUNCTION IF EXISTS audit_document_change();
                DROP FUNCTION IF EXISTS audit_write(text, text, text, jsonb, jsonb, text[], jsonb);
                """);
        }
    }
}

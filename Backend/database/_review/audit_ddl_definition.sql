CREATE OR REPLACE FUNCTION ppiq_forensics.audit_ddl()
 RETURNS event_trigger
 LANGUAGE plpgsql
AS $function$
DECLARE r record;
BEGIN
    FOR r IN SELECT * FROM pg_event_trigger_dropped_objects() LOOP
        IF r.object_type IN ('table','schema') AND r.schema_name = 'public' THEN
            INSERT INTO ppiq_forensics.wipe_audit
                (table_name, operation, query, application_name, client_addr, session_user_name, backend_pid)
            VALUES
                (r.object_identity, 'DROP:' || r.object_type, current_query(),
                 current_setting('application_name', true), inet_client_addr()::text,
                 session_user, pg_backend_pid());
        END IF;
    END LOOP;
END;
$function$


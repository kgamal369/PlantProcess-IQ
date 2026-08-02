-- 750_forensics_audit_subsystem.sql
--
-- PRODUCT FIX, per T-006: a schema object that exists only as data belongs
-- in a numbered migration. Generated from the live catalog with
-- pg_get_functiondef, pg_get_constraintdef and pg_get_triggerdef - nothing
-- retyped from memory.
--
-- IDEMPOTENT AND NON-DESTRUCTIVE. At a customer this schema will hold real
-- audit history, so nothing here drops a table or a schema. Re-running it
-- replaces functions and triggers and leaves every recorded row alone.

CREATE SCHEMA IF NOT EXISTS ppiq_forensics;

-- ---- tables ---------------------------------------------------------
CREATE TABLE IF NOT EXISTS public.ppiq_catalog_audit (
    id bigint NOT NULL,
    executed_at_utc timestamp with time zone DEFAULT now() NOT NULL,
    action text NOT NULL,
    detail text NOT NULL
);

CREATE TABLE IF NOT EXISTS public.ppiq_purge_audit (
    id bigint NOT NULL,
    executed_at_utc timestamp with time zone DEFAULT now() NOT NULL,
    action text NOT NULL,
    detail text NOT NULL
);

-- ---- constraints ----------------------------------------------------

-- ---- functions ------------------------------------------------------
CREATE OR REPLACE FUNCTION ppiq_forensics.audit_ddl()
 RETURNS event_trigger
 LANGUAGE plpgsql
AS $function$
;


DECLARE r record;
;


BEGIN
;


    FOR r IN SELECT * FROM pg_event_trigger_dropped_objects() LOOP
;


        IF r.object_type IN ('table','schema') AND r.schema_name = 'public' THEN
;


            INSERT INTO ppiq_forensics.wipe_audit
;


                (table_name, operation, query, application_name, client_addr, session_user_name, backend_pid)
;


            VALUES
;


                (r.object_identity, 'DROP:' || r.object_type, current_query(),
;


                 current_setting('application_name', true), inet_client_addr()::text,
;


                 session_user, pg_backend_pid());
;


        END IF;
;


    END LOOP;
;


END;
;


$function$

;

CREATE OR REPLACE FUNCTION ppiq_forensics.audit_wipe()
 RETURNS trigger
 LANGUAGE plpgsql
 SECURITY DEFINER
AS $function$
;


DECLARE n bigint;
;


BEGIN
;


    BEGIN
;


        EXECUTE format('SELECT count(*) FROM %I.%I', TG_TABLE_SCHEMA, TG_TABLE_NAME) INTO n;
;


    EXCEPTION WHEN OTHERS THEN n := NULL;
;


    END;
;


    INSERT INTO ppiq_forensics.wipe_audit
;


        (table_name, operation, rows_before, query, application_name, client_addr, session_user_name, backend_pid)
;


    VALUES
;


        (TG_TABLE_NAME, TG_OP, n, current_query(),
;


         current_setting('application_name', true), inet_client_addr()::text,
;


         session_user, pg_backend_pid());
;


    RETURN NULL;
;


END;
;


$function$

;

-- ---- table triggers -------------------------------------------------
DROP TRIGGER IF EXISTS ppiq_wipe_trap_genealogy_edges ON public.genealogy_edges;
CREATE TRIGGER ppiq_wipe_trap_genealogy_edges BEFORE DELETE OR TRUNCATE ON public.genealogy_edges FOR EACH STATEMENT EXECUTE FUNCTION ppiq_forensics.audit_wipe();
DROP TRIGGER IF EXISTS ppiq_wipe_trap_import_batches ON public.import_batches;
CREATE TRIGGER ppiq_wipe_trap_import_batches BEFORE DELETE OR TRUNCATE ON public.import_batches FOR EACH STATEMENT EXECUTE FUNCTION ppiq_forensics.audit_wipe();
DROP TRIGGER IF EXISTS ppiq_wipe_trap_material_units ON public.material_units;
CREATE TRIGGER ppiq_wipe_trap_material_units BEFORE DELETE OR TRUNCATE ON public.material_units FOR EACH STATEMENT EXECUTE FUNCTION ppiq_forensics.audit_wipe();
DROP TRIGGER IF EXISTS ppiq_wipe_trap_parameter_observations ON public.parameter_observations;
CREATE TRIGGER ppiq_wipe_trap_parameter_observations BEFORE DELETE OR TRUNCATE ON public.parameter_observations FOR EACH STATEMENT EXECUTE FUNCTION ppiq_forensics.audit_wipe();
DROP TRIGGER IF EXISTS ppiq_wipe_trap_quality_events ON public.quality_events;
CREATE TRIGGER ppiq_wipe_trap_quality_events BEFORE DELETE OR TRUNCATE ON public.quality_events FOR EACH STATEMENT EXECUTE FUNCTION ppiq_forensics.audit_wipe();
DROP TRIGGER IF EXISTS ppiq_wipe_trap_staging_records ON public.staging_records;
CREATE TRIGGER ppiq_wipe_trap_staging_records BEFORE DELETE OR TRUNCATE ON public.staging_records FOR EACH STATEMENT EXECUTE FUNCTION ppiq_forensics.audit_wipe();

-- ---- event triggers -------------------------------------------------
-- These were invisible to every diff run before 02-Aug: the object
-- inventory compared tables, views, indexes, functions and table triggers
-- and never queried pg_event_trigger. A table trigger cannot see DDL, and
-- the 18 recorded events are all DROP:table.

-- PPIQ FORENSICS SUBSYSTEM - CAPTURED FROM ppiq_presentation ON 2026-08-02 21:07:06
--
-- THIS IS A REVIEW FILE, NOT A MIGRATION. It is not in the numbered chain
-- and nothing applies it. Promote it deliberately, after the trigger-level
-- verdict in forensics_trigger_verdict.txt has been acted on.
--
-- Extracted with pg_dump and pg_get_triggerdef. Nothing retyped from memory.

-- ============ schema ppiq_forensics ============
--
-- PostgreSQL database dump
--

\restrict G53bL4wg0myigHQsux1JmhehSjFAkVZh0nCK2HsBEaETx525EMRcEmvco8scb8K

-- Dumped from database version 16.13
-- Dumped by pg_dump version 16.13

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

--
-- Name: ppiq_forensics; Type: SCHEMA; Schema: -; Owner: -
--

CREATE SCHEMA ppiq_forensics;


--
-- Name: audit_ddl(); Type: FUNCTION; Schema: ppiq_forensics; Owner: -
--

CREATE FUNCTION ppiq_forensics.audit_ddl() RETURNS event_trigger
    LANGUAGE plpgsql
    AS $$

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

$$;


--
-- Name: audit_wipe(); Type: FUNCTION; Schema: ppiq_forensics; Owner: -
--

CREATE FUNCTION ppiq_forensics.audit_wipe() RETURNS trigger
    LANGUAGE plpgsql SECURITY DEFINER
    AS $$

DECLARE n bigint;

BEGIN

    BEGIN

        EXECUTE format('SELECT count(*) FROM %I.%I', TG_TABLE_SCHEMA, TG_TABLE_NAME) INTO n;

    EXCEPTION WHEN OTHERS THEN n := NULL;

    END;

    INSERT INTO ppiq_forensics.wipe_audit

        (table_name, operation, rows_before, query, application_name, client_addr, session_user_name, backend_pid)

    VALUES

        (TG_TABLE_NAME, TG_OP, n, current_query(),

         current_setting('application_name', true), inet_client_addr()::text,

         session_user, pg_backend_pid());

    RETURN NULL;

END;

$$;


SET default_tablespace = '';

SET default_table_access_method = heap;

--
-- Name: wipe_audit; Type: TABLE; Schema: ppiq_forensics; Owner: -
--

CREATE TABLE ppiq_forensics.wipe_audit (
    id bigint NOT NULL,
    occurred_at timestamp with time zone DEFAULT now() NOT NULL,
    table_name text,
    operation text,
    rows_before bigint,
    query text,
    application_name text,
    client_addr text,
    session_user_name text,
    backend_pid integer
);


--
-- Name: wipe_audit_id_seq; Type: SEQUENCE; Schema: ppiq_forensics; Owner: -
--

CREATE SEQUENCE ppiq_forensics.wipe_audit_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: wipe_audit_id_seq; Type: SEQUENCE OWNED BY; Schema: ppiq_forensics; Owner: -
--

ALTER SEQUENCE ppiq_forensics.wipe_audit_id_seq OWNED BY ppiq_forensics.wipe_audit.id;


--
-- Name: wipe_audit id; Type: DEFAULT; Schema: ppiq_forensics; Owner: -
--

ALTER TABLE ONLY ppiq_forensics.wipe_audit ALTER COLUMN id SET DEFAULT nextval('ppiq_forensics.wipe_audit_id_seq'::regclass);


--
-- Name: wipe_audit wipe_audit_pkey; Type: CONSTRAINT; Schema: ppiq_forensics; Owner: -
--

ALTER TABLE ONLY ppiq_forensics.wipe_audit
    ADD CONSTRAINT wipe_audit_pkey PRIMARY KEY (id);


--
-- PostgreSQL database dump complete
--

\unrestrict G53bL4wg0myigHQsux1JmhehSjFAkVZh0nCK2HsBEaETx525EMRcEmvco8scb8K


-- ============ public audit tables ============
--
-- PostgreSQL database dump
--

\restrict YbAJ5rAR4ccPhdCCN5HqwLc4lrwLPLRhCZeBfz2dnYOoEwyX8ykXo7WtnsqkiIw

-- Dumped from database version 16.13
-- Dumped by pg_dump version 16.13

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

SET default_tablespace = '';

SET default_table_access_method = heap;

--
-- Name: ppiq_catalog_audit; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.ppiq_catalog_audit (
    id bigint NOT NULL,
    executed_at_utc timestamp with time zone DEFAULT now() NOT NULL,
    action text NOT NULL,
    detail text NOT NULL
);


--
-- Name: ppiq_catalog_audit_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.ppiq_catalog_audit_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: ppiq_catalog_audit_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.ppiq_catalog_audit_id_seq OWNED BY public.ppiq_catalog_audit.id;


--
-- Name: ppiq_purge_audit; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.ppiq_purge_audit (
    id bigint NOT NULL,
    executed_at_utc timestamp with time zone DEFAULT now() NOT NULL,
    action text NOT NULL,
    detail text NOT NULL
);


--
-- Name: ppiq_purge_audit_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.ppiq_purge_audit_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: ppiq_purge_audit_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.ppiq_purge_audit_id_seq OWNED BY public.ppiq_purge_audit.id;


--
-- Name: ppiq_catalog_audit id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.ppiq_catalog_audit ALTER COLUMN id SET DEFAULT nextval('public.ppiq_catalog_audit_id_seq'::regclass);


--
-- Name: ppiq_purge_audit id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.ppiq_purge_audit ALTER COLUMN id SET DEFAULT nextval('public.ppiq_purge_audit_id_seq'::regclass);


--
-- Name: ppiq_catalog_audit ppiq_catalog_audit_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.ppiq_catalog_audit
    ADD CONSTRAINT ppiq_catalog_audit_pkey PRIMARY KEY (id);


--
-- Name: ppiq_purge_audit ppiq_purge_audit_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.ppiq_purge_audit
    ADD CONSTRAINT ppiq_purge_audit_pkey PRIMARY KEY (id);


--
-- PostgreSQL database dump complete
--

\unrestrict YbAJ5rAR4ccPhdCCN5HqwLc4lrwLPLRhCZeBfz2dnYOoEwyX8ykXo7WtnsqkiIw


-- ============ wipe traps ============
-- NOTE: these fire on the projection hot path. Read the verdict file.
CREATE TRIGGER ppiq_wipe_trap_genealogy_edges BEFORE DELETE OR TRUNCATE ON public.genealogy_edges FOR EACH STATEMENT EXECUTE FUNCTION ppiq_forensics.audit_wipe();
CREATE TRIGGER ppiq_wipe_trap_import_batches BEFORE DELETE OR TRUNCATE ON public.import_batches FOR EACH STATEMENT EXECUTE FUNCTION ppiq_forensics.audit_wipe();
CREATE TRIGGER ppiq_wipe_trap_material_units BEFORE DELETE OR TRUNCATE ON public.material_units FOR EACH STATEMENT EXECUTE FUNCTION ppiq_forensics.audit_wipe();
CREATE TRIGGER ppiq_wipe_trap_parameter_observations BEFORE DELETE OR TRUNCATE ON public.parameter_observations FOR EACH STATEMENT EXECUTE FUNCTION ppiq_forensics.audit_wipe();
CREATE TRIGGER ppiq_wipe_trap_quality_events BEFORE DELETE OR TRUNCATE ON public.quality_events FOR EACH STATEMENT EXECUTE FUNCTION ppiq_forensics.audit_wipe();
CREATE TRIGGER ppiq_wipe_trap_staging_records BEFORE DELETE OR TRUNCATE ON public.staging_records FOR EACH STATEMENT EXECUTE FUNCTION ppiq_forensics.audit_wipe();

-- ============ signal-to-noise ============
-- All 18 events trapped so far are one idempotent seed dropping
-- ppiq_p4_demo_features / _outcomes / _truth. A trap that has only ever
-- fired on a known-safe script will be ignored on the day it matters.
-- Add an allowlist for ppiq_p4_demo_% before promoting this.
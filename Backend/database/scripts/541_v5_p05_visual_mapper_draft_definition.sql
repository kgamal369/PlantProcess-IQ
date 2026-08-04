-- =====================================================================================
-- PPIQ 541 - visual mapper session draft definition
-- =====================================================================================
-- T-032. VisualMapperEndpoints.cs stores the working graph on the session as
-- jsonb (UPDATE at line 79, SELECT at line 152), but 540 never declared the
-- column. A live check on 04-Aug found draft_definition present in
-- ppiq_presentation and ABSENT from ppiq_app, and present in no SQL script
-- anywhere - so it had been added by hand to one database only.
--
-- This script puts it in source control and makes both databases agree. It is
-- idempotent, so it is a no-op where the column already exists.
--
-- Nothing else in the visual mapper schema changes: the endpoint is aligned to
-- the table rather than the table to the endpoint, because display_name and
-- source_code already own the concepts the endpoint was inventing names for.
-- =====================================================================================
\set ON_ERROR_STOP on

BEGIN;

ALTER TABLE public.ppiq_visual_mapper_sessions
    ADD COLUMN IF NOT EXISTS draft_definition jsonb NULL;

COMMENT ON COLUMN public.ppiq_visual_mapper_sessions.draft_definition IS
    'Working graph for the authoring session. Snapshotted into ppiq_visual_mapper_versions.mapping_definition on publish.';

COMMIT;
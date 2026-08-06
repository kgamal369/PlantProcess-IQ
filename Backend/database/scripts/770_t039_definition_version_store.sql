-- ============================================================================
-- 770_t039_definition_version_store.sql
-- PPIQ T-039 - the M1 compatibility snapshot store for versioned definitions
--
-- WHAT THIS IS, AND WHAT IT IS NOT.
--
-- Chapter 3 section 4.5.11 specifies a generic definition store with its own
-- identity and dependency tables. THIS IS NOT THAT, and this file names none of
-- those three objects deliberately: the pack that delivers it refuses to run if
-- any of their names appears here, so writing them out even to disclaim them
-- would make this script its own first offender. M2a builds that architecture
-- and replaces this file entirely.
--
-- What this is: the smallest immutable snapshot storage a compatibility adapter
-- needs so that IDefinitionService can answer GetVersion and ListVersions with
-- real rows instead of a fiction. The operational widget definition stays
-- exactly where it is, in dashboard_widget_definitions. This table holds
-- history beside it and never replaces it.
--
-- WHY IT IS HERE AND NOT IN A LIVE STATEMENT. The numbered SQL replay chain is
-- the authoritative rebuild path for this product; a table created by hand
-- against one developer database exists nowhere else and is lost on the next
-- clean build. Everything this adapter depends on is therefore in this file.
--
-- Idempotent by construction: safe to replay over a database that already has
-- it, which is what the replay chain does on every rebuild.
-- ============================================================================

CREATE TABLE IF NOT EXISTS public.ppiq_definition_versions
(
    id               uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    definition_kind  text        NOT NULL,
    definition_id    uuid        NOT NULL,
    version_number   integer     NOT NULL,
    payload_json     jsonb       NOT NULL,
    created_at_utc   timestamptz NOT NULL DEFAULT now(),
    created_by       text        NULL,
    is_published     boolean     NOT NULL DEFAULT false,

    CONSTRAINT ck_ppiq_definition_versions_version_positive
        CHECK (version_number > 0)
);

-- THE DATABASE BACKSTOP. Version numbers are allocated by the server inside the
-- same transaction that writes the current definition, under a row lock on that
-- definition. This constraint is what makes that allocation safe rather than
-- merely likely: two writers that raced past the lock cannot both land the same
-- number, and the loser fails loudly instead of overwriting history.
CREATE UNIQUE INDEX IF NOT EXISTS ux_ppiq_definition_versions_kind_id_version
    ON public.ppiq_definition_versions (definition_kind, definition_id, version_number);

-- Reading a definition's history is the common query, newest first.
CREATE INDEX IF NOT EXISTS ix_ppiq_definition_versions_kind_id_desc
    ON public.ppiq_definition_versions (definition_kind, definition_id, version_number DESC);

COMMENT ON TABLE public.ppiq_definition_versions IS
    'PPIQ T-039. M1 compatibility snapshot store behind IDefinitionService. Immutable version rows only; the current definition lives in its own operational table. Replaced by the M2a definition store.';

-- Grants, guarded so the script replays on a database where a role is absent.
-- 999 grants across the whole schema, but a table that only works because a
-- later script happened to run is a table with an undeclared dependency.
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'plantprocess_app') THEN
        GRANT SELECT, INSERT, UPDATE ON public.ppiq_definition_versions TO plantprocess_app;
    END IF;

    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'plantprocess_readonly_preview') THEN
        GRANT SELECT ON public.ppiq_definition_versions TO plantprocess_readonly_preview;
    END IF;
END $$;

SELECT 'T-039 definition version store applied' AS status;
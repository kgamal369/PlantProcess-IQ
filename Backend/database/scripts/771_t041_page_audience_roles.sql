-- ============================================================================
-- PPIQ T-041 - PAGE AUDIENCE ROLES
-- Adds the audience-role contract to the existing page_definitions seam.
-- Idempotent and backward compatible. Safe to rerun.
--
-- AUDIENCE IS NOT VISIBILITY. Visibility answers who may open the page and is
-- untouched here. Audience answers which roles the page was authored FOR, and
-- T-042 reads it when Publish puts the page into navigation.
--
-- A page that predates this column reads as an EMPTY audience, never as every
-- role: inventing an audience for a row that never declared one would put a
-- page in front of people nobody chose.
--
-- page_definition_shares is deliberately NOT used. That table is individual
-- user sharing, which is a different question from role audience.
-- ============================================================================

SET client_min_messages TO WARNING;

ALTER TABLE page_definitions
    ADD COLUMN IF NOT EXISTS audience_roles jsonb NOT NULL DEFAULT '[]'::jsonb;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'ck_page_definitions_audience_roles_array'
    ) THEN
        ALTER TABLE page_definitions
            ADD CONSTRAINT ck_page_definitions_audience_roles_array
            CHECK (jsonb_typeof(audience_roles) = 'array');
    END IF;
END $$;

COMMENT ON COLUMN page_definitions.audience_roles IS
    'PPIQ T-041. Roles the page was authored for, from the API role authority: Admin, DataManager, Engineer, Viewer. Empty means none declared.';
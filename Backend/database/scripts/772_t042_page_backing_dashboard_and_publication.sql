-- ============================================================================
-- PPIQ T-042 - THE PAGE TO WORKSPACE BRIDGE, AND PUBLICATION
-- Idempotent and backward compatible. Safe to rerun. Drops nothing.
--
-- backing_dashboard_definition_id: the TYPED link from an authored page to the
-- operational dashboard that carries its widgets and its /workspace/:code
-- surface. An id, not a naming convention: a code can be renamed and a
-- convention can be broken, and the page would then point at nothing while
-- still looking correct.
--
-- published_at_utc: NULL is a draft. Publication is not visibility and not
-- audience. It answers only whether this authored page is eligible to appear as
-- a Workspace. It is deliberately NOT the backing dashboard's active flag,
-- because that dashboard must be active for widgets to be saved into it long
-- before the page is ready to be seen by anyone.
-- ============================================================================

SET client_min_messages TO WARNING;

ALTER TABLE page_definitions
    ADD COLUMN IF NOT EXISTS backing_dashboard_definition_id uuid NULL;

ALTER TABLE page_definitions
    ADD COLUMN IF NOT EXISTS published_at_utc timestamptz NULL;

CREATE INDEX IF NOT EXISTS ix_page_definitions_backing_dashboard
    ON page_definitions (backing_dashboard_definition_id)
    WHERE is_deleted = false AND backing_dashboard_definition_id IS NOT NULL;

COMMENT ON COLUMN page_definitions.backing_dashboard_definition_id IS
    'PPIQ T-042. Typed link to the DashboardDefinition that carries this page''s widgets and its /workspace/:code surface. Null means the page has no operational backing yet.';

COMMENT ON COLUMN page_definitions.published_at_utc IS
    'PPIQ T-042. Null is a draft. Set means the page is eligible to appear as a Workspace. Not visibility, not audience, and not the dashboard active flag.';
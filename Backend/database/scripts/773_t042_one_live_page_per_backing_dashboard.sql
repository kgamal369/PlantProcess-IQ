-- ============================================================================
-- PPIQ T-042 S6 - ONE PAGE, ONE BACKING WORKSPACE
-- Idempotent. Drops nothing.
--
-- Two live pages claiming the same backing dashboard would give one workspace
-- two competing authored labels in navigation, and the client would have to
-- pick one - which is a guess dressed as a projection. The database refuses it
-- instead. Deleted rows are excluded so a slug can be reused after a delete.
-- ============================================================================

SET client_min_messages TO WARNING;

CREATE UNIQUE INDEX IF NOT EXISTS ux_page_definitions_backing_dashboard_live
    ON page_definitions (backing_dashboard_definition_id)
    WHERE is_deleted = false AND backing_dashboard_definition_id IS NOT NULL;

COMMENT ON INDEX ux_page_definitions_backing_dashboard_live IS
    'PPIQ T-042. One live page per backing dashboard, so a workspace can never carry two authored labels.';
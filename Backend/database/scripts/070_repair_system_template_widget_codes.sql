-- ============================================================================
-- SUPERSEDED. This repair script no longer rewrites widget definitions.
--
-- It existed only to reconcile two disagreeing system-template authorities, by
-- UPDATE-ing dimension_code and measure_code on rows another mechanism had just
-- written. With a single runtime authority in place it has nothing to repair,
-- and leaving it active would mean it keeps fighting that authority on every
-- replay.
--
-- Its historical effect is still visible in existing databases and is reconciled
-- by Backend/database/scripts/830_system_template_single_authority.sql.
--
-- Kept as a deliberate no-op so replay lists that name it continue to succeed.
-- ============================================================================

SELECT 'system template widget-code repair is superseded by the runtime authority' AS notice;

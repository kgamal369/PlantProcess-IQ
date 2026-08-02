-- ============================================================
-- TOMBSTONE - M1-07, 29 July 2026
--
-- This file was named 070_fix_system_template_widget_codes.sql and it WROTE
-- THE DEFECT it claimed to fix: PascalCase dimension and measure codes, where
-- the canonical codes in DashboardMetadataCodes are camelCase. A clean rebuild
-- reinstalled the broken values every time.
--
-- WHY IT STAYED HIDDEN. DashboardWidgetQuerySafetyRegistry validates codes with
-- OrdinalIgnoreCase, so a PascalCase code PASSED validation; the measure
-- executor resolves with Ordinal, so it then FAILED at execution. The widget
-- looked configured and returned nothing.
--
-- The corrected statements now live in:
--     070_repair_system_template_widget_codes.sql
--
-- The name is kept as an empty transaction rather than deleted, because
-- database.apply-order.manifest.csv references this path and deploy scripts
-- glob this folder. An empty transaction is harmless; a missing file that
-- something still expects is not.
--
-- Do not add statements here. Add them to the repair script above.
-- ============================================================

BEGIN;
COMMIT;
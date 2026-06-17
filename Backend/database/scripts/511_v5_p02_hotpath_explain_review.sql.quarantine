-- ============================================================
-- PlantProcess IQ Doctrine v5
-- P02-T04 · Hot-path query-plan review helper
--
-- Run manually after applying 510_v5_p02_rls_tenant_isolation_and_secret_vault.sql.
-- This script does not mutate data; it records EXPLAIN plans for review.
-- ============================================================

SET client_min_messages TO WARNING;
SELECT set_config('app.current_tenant', '00000000-0000-0000-0000-000000000001', false);

EXPLAIN (ANALYZE, BUFFERS)
SELECT *
FROM public.genealogy_edges
WHERE tenant_id = public.ppiq_current_tenant()
  AND is_deleted = false
ORDER BY created_at_utc DESC
LIMIT 100;

EXPLAIN (ANALYZE, BUFFERS)
SELECT *
FROM public.material_units
WHERE tenant_id = public.ppiq_current_tenant()
  AND is_deleted = false
ORDER BY production_start_utc DESC
LIMIT 100;

EXPLAIN (ANALYZE, BUFFERS)
SELECT *
FROM public.quality_events
WHERE tenant_id = public.ppiq_current_tenant()
  AND is_deleted = false
ORDER BY event_at_utc DESC
LIMIT 100;

EXPLAIN (ANALYZE, BUFFERS)
SELECT *
FROM public.parameter_observations
WHERE tenant_id = public.ppiq_current_tenant()
  AND is_deleted = false
ORDER BY observed_at_utc DESC
LIMIT 100;
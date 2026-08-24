-- ============================================================================
-- System templates: one authority.
--
-- Retires legacy product-generated system-template rows so the runtime
-- authority owns the family alone. Scoped strictly by product provenance:
-- only rows whose source_system is the product template marker are touched.
-- Customer-authored dashboards carry a different source_system and are never
-- matched by any statement below.
--
-- Idempotent: every statement filters on is_deleted = FALSE, so a second run
-- affects zero rows.
--
-- Nothing is hard-deleted. Rows are soft-retired so the history stays readable
-- and so the widget_code uniqueness index, which applies only to rows that are
-- not deleted, releases the code for the runtime authority.
-- ============================================================================

BEGIN;

-- 1. Widgets of legacy, unprefixed product template dashboards.
UPDATE dashboard_widget_definitions w
SET    is_deleted     = TRUE,
       deleted_at_utc = NOW() AT TIME ZONE 'UTC',
       deleted_reason = 'superseded by the runtime system-template authority',
       is_active      = FALSE,
       updated_at_utc = NOW() AT TIME ZONE 'UTC'
FROM   dashboard_definitions d
WHERE  d.id = w.dashboard_definition_id
  AND  w.is_deleted = FALSE
  AND  w.source_system = 'PlantProcessIQ.SystemTemplates'
  AND  d.source_system = 'PlantProcessIQ.SystemTemplates'
  AND  d.dashboard_code NOT LIKE 'SYSTEM\_%';

-- 2. The legacy dashboards themselves.
UPDATE dashboard_definitions
SET    is_deleted     = TRUE,
       deleted_at_utc = NOW() AT TIME ZONE 'UTC',
       deleted_reason = 'superseded by the runtime system-template authority',
       is_active      = FALSE,
       updated_at_utc = NOW() AT TIME ZONE 'UTC'
WHERE  is_deleted = FALSE
  AND  source_system = 'PlantProcessIQ.SystemTemplates'
  AND  dashboard_code NOT LIKE 'SYSTEM\_%';

-- 3. A product template cannot declare an average-parameter measure without a
--    parameter, and cannot choose one without embedding plant vocabulary.
--    Retired wherever it persists, including inside the runtime family.
UPDATE dashboard_widget_definitions
SET    is_deleted     = TRUE,
       deleted_at_utc = NOW() AT TIME ZONE 'UTC',
       deleted_reason = 'average-parameter measure cannot be seeded without a parameter selection',
       is_active      = FALSE,
       updated_at_utc = NOW() AT TIME ZONE 'UTC'
WHERE  is_deleted = FALSE
  AND  source_system = 'PlantProcessIQ.SystemTemplates'
  AND  widget_code = 'CORR_PARAMETER_AVG_BY_EQUIPMENT';

-- 4. Legacy widget code for a definition the runtime authority already publishes.
--    The repair script rewrote this widget to materialUnitType, which is exactly the
--    runtime widget DQ_BY_MATERIAL_TYPE under an older code. It is a duplicate under a
--    legacy name, not a broken definition, and retiring it leaves the question itself
--    answered by the runtime family.
UPDATE dashboard_widget_definitions
SET    is_deleted     = TRUE,
       deleted_at_utc = NOW() AT TIME ZONE 'UTC',
       deleted_reason = 'duplicate of the runtime widget under a legacy code',
       is_active      = FALSE,
       updated_at_utc = NOW() AT TIME ZONE 'UTC'
WHERE  is_deleted = FALSE
  AND  source_system = 'PlantProcessIQ.SystemTemplates'
  AND  widget_code = 'DQ_BY_TYPE';

COMMIT;


-- ============================================================
-- PHASE 0 / PHASE 1 HOTFIX
-- FILE: Backend/database/scripts/070_fix_system_template_widget_codes.sql
--
-- PURPOSE:
--   Repairs old system-template widget definitions that were seeded
--   with dimension_code / measure_code values not accepted by
--   DashboardWidgetQuerySafetyRegistry.
--
-- SAFE TO RUN:
--   Yes. Idempotent. It only updates known system template widgets.
-- ============================================================

BEGIN;

UPDATE dashboard_widget_definitions
SET    dimension_code = 'day',
       measure_code   = 'defectRate',
       updated_at_utc = NOW() AT TIME ZONE 'UTC'
WHERE  widget_code    = 'DEFECT_TREND'
  AND  source_system  = 'PlantProcessIQ.SystemTemplates'
  AND  is_deleted     = FALSE
  AND  (dimension_code <> 'day' OR measure_code <> 'defectRate');

UPDATE dashboard_widget_definitions
SET    dimension_code = 'defectType',
       measure_code   = 'defectCount',
       updated_at_utc = NOW() AT TIME ZONE 'UTC'
WHERE  widget_code    = 'DEFECT_BREAKDOWN'
  AND  source_system  = 'PlantProcessIQ.SystemTemplates'
  AND  is_deleted     = FALSE
  AND  (dimension_code <> 'defectType' OR measure_code <> 'defectCount');

UPDATE dashboard_widget_definitions
SET    dimension_code = 'materialUnitType',
       measure_code   = 'materialCount',
       updated_at_utc = NOW() AT TIME ZONE 'UTC'
WHERE  widget_code IN ('MATERIAL_EXPLORER', 'MATERIAL_BY_TYPE', 'INV_MATERIAL_BY_TYPE')
  AND  source_system  = 'PlantProcessIQ.SystemTemplates'
  AND  is_deleted     = FALSE
  AND  (dimension_code <> 'materialUnitType' OR measure_code <> 'materialCount');

UPDATE dashboard_widget_definitions
SET    dimension_code = 'riskClass',
       measure_code   = 'riskScore',
       updated_at_utc = NOW() AT TIME ZONE 'UTC'
WHERE  widget_code    = 'RISK_BY_CLASS'
  AND  source_system  = 'PlantProcessIQ.SystemTemplates'
  AND  is_deleted     = FALSE
  AND  (dimension_code <> 'riskClass' OR measure_code <> 'riskScore');

UPDATE dashboard_widget_definitions
SET    dimension_code = 'equipment',
       measure_code   = 'riskScore',
       updated_at_utc = NOW() AT TIME ZONE 'UTC'
WHERE  widget_code IN ('TOP_CONTRIBUTORS', 'RISK_BY_EQUIPMENT')
  AND  source_system  = 'PlantProcessIQ.SystemTemplates'
  AND  is_deleted     = FALSE
  AND  (dimension_code <> 'equipment' OR measure_code <> 'riskScore');

UPDATE dashboard_widget_definitions
SET    dimension_code = 'sourceSystem',
       measure_code   = 'dataQualityIssueCount',
       updated_at_utc = NOW() AT TIME ZONE 'UTC'
WHERE  widget_code IN ('DQ_BY_SEVERITY', 'DQ_BY_SOURCE')
  AND  source_system  = 'PlantProcessIQ.SystemTemplates'
  AND  is_deleted     = FALSE
  AND  (dimension_code <> 'sourceSystem' OR measure_code <> 'dataQualityIssueCount');

UPDATE dashboard_widget_definitions
SET    dimension_code = 'materialUnitType',
       measure_code   = 'dataQualityIssueCount',
       updated_at_utc = NOW() AT TIME ZONE 'UTC'
WHERE  widget_code IN ('DQ_BY_TYPE', 'DQ_BY_MATERIAL_TYPE')
  AND  source_system  = 'PlantProcessIQ.SystemTemplates'
  AND  is_deleted     = FALSE
  AND  (dimension_code <> 'materialUnitType' OR measure_code <> 'dataQualityIssueCount');

COMMIT;

SELECT
    d.dashboard_code,
    w.widget_code,
    w.widget_title,
    w.chart_type,
    w.dimension_code,
    w.measure_code,
    w.updated_at_utc
FROM dashboard_widget_definitions w
JOIN dashboard_definitions d
    ON d.id = w.dashboard_definition_id
WHERE w.source_system = 'PlantProcessIQ.SystemTemplates'
  AND w.is_deleted = FALSE
ORDER BY d.dashboard_code, w.sort_order, w.widget_code;
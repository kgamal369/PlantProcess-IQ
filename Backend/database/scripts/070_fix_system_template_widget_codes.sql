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
SET    dimension_code = 'Day',
       measure_code   = 'DefectRate',
       updated_at_utc = NOW() AT TIME ZONE 'UTC'
WHERE  widget_code    = 'DEFECT_TREND'
  AND  source_system  = 'PlantProcessIQ.SystemTemplates'
  AND  is_deleted     = FALSE
  AND  (dimension_code <> 'Day' OR measure_code <> 'DefectRate');

UPDATE dashboard_widget_definitions
SET    dimension_code = 'DefectType',
       measure_code   = 'DefectCount',
       updated_at_utc = NOW() AT TIME ZONE 'UTC'
WHERE  widget_code    = 'DEFECT_BREAKDOWN'
  AND  source_system  = 'PlantProcessIQ.SystemTemplates'
  AND  is_deleted     = FALSE
  AND  (dimension_code <> 'DefectType' OR measure_code <> 'DefectCount');

UPDATE dashboard_widget_definitions
SET    dimension_code = 'MaterialUnitType',
       measure_code   = 'MaterialCount',
       updated_at_utc = NOW() AT TIME ZONE 'UTC'
WHERE  widget_code IN ('MATERIAL_EXPLORER', 'MATERIAL_BY_TYPE', 'INV_MATERIAL_BY_TYPE')
  AND  source_system  = 'PlantProcessIQ.SystemTemplates'
  AND  is_deleted     = FALSE
  AND  (dimension_code <> 'MaterialUnitType' OR measure_code <> 'MaterialCount');

UPDATE dashboard_widget_definitions
SET    dimension_code = 'RiskClass',
       measure_code   = 'RiskScore',
       updated_at_utc = NOW() AT TIME ZONE 'UTC'
WHERE  widget_code    = 'RISK_BY_CLASS'
  AND  source_system  = 'PlantProcessIQ.SystemTemplates'
  AND  is_deleted     = FALSE
  AND  (dimension_code <> 'RiskClass' OR measure_code <> 'RiskScore');

UPDATE dashboard_widget_definitions
SET    dimension_code = 'Equipment',
       measure_code   = 'RiskScore',
       updated_at_utc = NOW() AT TIME ZONE 'UTC'
WHERE  widget_code IN ('TOP_CONTRIBUTORS', 'RISK_BY_EQUIPMENT')
  AND  source_system  = 'PlantProcessIQ.SystemTemplates'
  AND  is_deleted     = FALSE
  AND  (dimension_code <> 'Equipment' OR measure_code <> 'RiskScore');

UPDATE dashboard_widget_definitions
SET    dimension_code = 'SourceSystem',
       measure_code   = 'DataQualityIssueCount',
       updated_at_utc = NOW() AT TIME ZONE 'UTC'
WHERE  widget_code IN ('DQ_BY_SEVERITY', 'DQ_BY_SOURCE')
  AND  source_system  = 'PlantProcessIQ.SystemTemplates'
  AND  is_deleted     = FALSE
  AND  (dimension_code <> 'SourceSystem' OR measure_code <> 'DataQualityIssueCount');

UPDATE dashboard_widget_definitions
SET    dimension_code = 'MaterialUnitType',
       measure_code   = 'DataQualityIssueCount',
       updated_at_utc = NOW() AT TIME ZONE 'UTC'
WHERE  widget_code IN ('DQ_BY_TYPE', 'DQ_BY_MATERIAL_TYPE')
  AND  source_system  = 'PlantProcessIQ.SystemTemplates'
  AND  is_deleted     = FALSE
  AND  (dimension_code <> 'MaterialUnitType' OR measure_code <> 'DataQualityIssueCount');

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
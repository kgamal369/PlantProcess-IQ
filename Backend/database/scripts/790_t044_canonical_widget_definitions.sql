-- ============================================================================
-- 790_t044_canonical_widget_definitions.sql
--
-- T-044. CONVERGENCE: source truth = rebuild truth = live truth.
--
-- WHY THIS FILE EXISTS. ppiq_presentation carried a QM_SEV definition that NO
-- source artifact in this repository produces. Every seeder writes
-- 'Severity Distribution' on the dimension 'severity'; the live database held
-- 'Defects by Equipment' on 'equipment'. It was a manual mutation, present
-- since at least the 27 July widget census, and a clean rebuild could not
-- reproduce it. A live database is never the authority, so this script exists
-- to converge an EXISTING database onto the corrected seed truth.
--
-- A fresh rebuild does not need this script: the corrected seeders already
-- write the canonical rows. This is for databases that were never rebuilt.
--
-- THE TWO RULINGS IT CARRIES.
--
--   QM_SEV was 'Defects by Equipment' on the equipment dimension. Quality
--   events carry NO equipment relationship: QualityEvent has MaterialUnitId
--   only, no source or landing table holds an equipment reference for a
--   quality event, and inspection_jobs has zero rows with an equipment_id.
--   The widget was asking a question the data cannot answer, and its single
--   category was the null-attribution bucket. It becomes Quality Events by
--   Type on the registered defectType dimension, which has fifteen real
--   classifications. The title says "Quality Events" and not "Defects"
--   deliberately: the largest bucket is Disposition, which is not a defect,
--   and a title of "Defects by Type" would be semantically false.
--
--   EO_EQDEF was 'Quality Events by Equipment', the same invalid question on
--   an equipment dashboard. It becomes Downtime Minutes by Equipment: 630
--   downtime events, 630 of them attributed to one of nine equipment, no
--   unattributed bucket, largest share 26.8 percent. That relationship is
--   real, so the question is defensible.
--
-- IDEMPOTENT. Keyed on widget_code, safe to replay, and it reports what it
-- changed rather than changing silently.
-- ============================================================================

BEGIN;

UPDATE dashboard_widget_definitions
SET widget_title   = 'Quality Events by Type',
    chart_type     = 'bar',
    dimension_code = 'defectType',
    measure_code   = 'defectCount',
    updated_at_utc = NOW() AT TIME ZONE 'UTC'
WHERE widget_code = 'QM_SEV'
  AND is_deleted = FALSE
  AND (widget_title <> 'Quality Events by Type'
    OR chart_type <> 'bar'
    OR dimension_code <> 'defectType'
    OR measure_code <> 'defectCount');

UPDATE dashboard_widget_definitions
SET widget_title   = 'Downtime Minutes by Equipment',
    chart_type     = 'bar',
    dimension_code = 'equipment',
    measure_code   = 'downtimeMinutes',
    updated_at_utc = NOW() AT TIME ZONE 'UTC'
WHERE widget_code = 'EO_EQDEF'
  AND is_deleted = FALSE
  AND (widget_title <> 'Downtime Minutes by Equipment'
    OR chart_type <> 'bar'
    OR dimension_code <> 'equipment'
    OR measure_code <> 'downtimeMinutes');

COMMIT;

-- The state after replay, printed so the operator sees convergence rather than
-- being told it happened.
SELECT widget_code, widget_title, chart_type, dimension_code, measure_code
FROM dashboard_widget_definitions
WHERE widget_code IN ('QM_SEV', 'EO_EQDEF')
  AND is_deleted = FALSE
ORDER BY widget_code;
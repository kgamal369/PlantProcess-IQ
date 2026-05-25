-- ============================================================================
-- PlantProcess IQ
-- Phase 1 Demo Mapping Views
--
-- Purpose:
-- Provide safe source-shaped views that prove:
-- 1. material genealogy join
-- 2. surface defect join
-- 3. KPI computed view
-- ============================================================================

SET client_min_messages TO WARNING;
SET TIME ZONE 'UTC';

BEGIN;

CREATE OR REPLACE VIEW public.v_phase1_material_genealogy_join AS
SELECT
    h.heat_no,
    cp.piece_id AS slab_id,
    hc.coil_id,
    h.steel_grade,
    h.furnace_code,
    cp.caster_id,
    cp.strand_no,
    hc.mill_line,
    h.tap_start_utc,
    cp.cut_time AS caster_cut_time_utc,
    hc.rolling_start_time,
    hc.rolling_end_time,
    cp.width_mm AS slab_width_mm,
    cp.thickness_mm AS slab_thickness_mm,
    hc.actual_width_mm AS coil_width_mm,
    hc.actual_thickness_mm AS coil_thickness_mm,
    hc.actual_fdt_c,
    hc.actual_ct_c
FROM src_meltshop_pg.heats h
JOIN src_caster_oracle_shape.cast_pieces cp
    ON cp.heat_no = h.heat_no
LEFT JOIN src_hsm_oracle_shape.hsm_coils hc
    ON hc.input_piece_id = cp.piece_id;

CREATE OR REPLACE VIEW public.v_phase1_surface_defect_join AS
SELECT
    hc.coil_id,
    hc.input_piece_id AS slab_id,
    hc.heat_no,
    hc.mill_line,
    psd.defect_code,
    psd.defect_name,
    psd.defect_class,
    psd.defect_severity,
    psd.side_code,
    psd.position_start_m,
    psd.position_end_m,
    psd.confidence_pct,
    psd.event_time_utc,
    hc.actual_fdt_c,
    hc.actual_ct_c,
    hc.actual_thickness_mm,
    hc.actual_width_mm
FROM src_hsm_oracle_shape.hsm_coils hc
JOIN src_inspection_mysql_shape.parsytec_surface_defects psd
    ON psd.coil_id = hc.coil_id;

CREATE OR REPLACE VIEW public.v_phase1_kpi_quality_temperature_window AS
SELECT
    hc.mill_line,
    date_trunc('day', hc.rolling_start_time) AS production_day_utc,
    COUNT(*) AS coil_count,
    AVG(hc.actual_fdt_c) AS avg_fdt_c,
    AVG(hc.actual_ct_c) AS avg_ct_c,
    COUNT(psd.defect_row_id) AS defect_count,
    CASE
        WHEN COUNT(*) = 0 THEN 0
        ELSE ROUND(COUNT(psd.defect_row_id)::numeric / COUNT(*)::numeric * 100, 4)
    END AS defects_per_100_coils,
    MIN(hc.rolling_start_time) AS first_rolling_time_utc,
    MAX(hc.rolling_end_time) AS last_rolling_time_utc
FROM src_hsm_oracle_shape.hsm_coils hc
LEFT JOIN src_inspection_mysql_shape.parsytec_surface_defects psd
    ON psd.coil_id = hc.coil_id
GROUP BY
    hc.mill_line,
    date_trunc('day', hc.rolling_start_time);

COMMIT;

SELECT 'Phase 1 demo mapping views created' AS status;
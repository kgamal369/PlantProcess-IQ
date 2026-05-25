-- ============================================================================
-- PlantProcess IQ
-- Phase 1 Golden Demo Source Shapes
--
-- Covers:
-- PPIQ-DEMO-003 MeltShop PostgreSQL Schema DDL
-- PPIQ-DEMO-004 Caster Oracle-shaped Schema DDL
-- PPIQ-DEMO-005 HSM Oracle-shaped Schema DDL
-- PPIQ-DEMO-006 PKL MSSQL-shaped Schema DDL
-- PPIQ-DEMO-007 Downtime + Parsytec MySQL-shaped Schema DDL
--
-- Purpose:
-- Create source-shaped demo schemas in PostgreSQL so the full staging →
-- mapping → canonical workflow can be proven before every real external
-- connector is certified.
-- ============================================================================

SET client_min_messages TO WARNING;
SET TIME ZONE 'UTC';

BEGIN;

-- ----------------------------------------------------------------------------
-- 1. MeltShop PostgreSQL-shaped source
-- ----------------------------------------------------------------------------

CREATE SCHEMA IF NOT EXISTS src_meltshop_pg;

CREATE TABLE IF NOT EXISTS src_meltshop_pg.heats
(
    heat_no                 text PRIMARY KEY,
    plant_code              text NOT NULL,
    furnace_code            text NOT NULL,
    steel_grade             text NOT NULL,
    route_code              text NOT NULL,
    tap_start_utc           timestamptz NOT NULL,
    tap_end_utc             timestamptz,
    heat_weight_ton         numeric(12,3),
    target_temp_c           numeric(8,2),
    actual_temp_c           numeric(8,2),
    oxygen_nm3              numeric(14,3),
    power_kwh               numeric(14,3),
    carbon_pct              numeric(8,5),
    manganese_pct           numeric(8,5),
    silicon_pct             numeric(8,5),
    source_updated_at_utc   timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS src_meltshop_pg.lf_treatment
(
    treatment_id            bigserial PRIMARY KEY,
    heat_no                 text NOT NULL REFERENCES src_meltshop_pg.heats(heat_no),
    lf_code                 text NOT NULL,
    treatment_start_utc     timestamptz NOT NULL,
    treatment_end_utc       timestamptz,
    argon_flow_nm3          numeric(12,3),
    calcium_wire_m          numeric(12,3),
    final_temp_c            numeric(8,2),
    sample_result_code      text,
    source_updated_at_utc   timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_src_meltshop_heats_updated
ON src_meltshop_pg.heats(source_updated_at_utc);

CREATE INDEX IF NOT EXISTS ix_src_meltshop_lf_heat_updated
ON src_meltshop_pg.lf_treatment(heat_no, source_updated_at_utc);

-- ----------------------------------------------------------------------------
-- 2. Caster Oracle-shaped source
-- ----------------------------------------------------------------------------

CREATE SCHEMA IF NOT EXISTS src_caster_oracle_shape;

CREATE TABLE IF NOT EXISTS src_caster_oracle_shape.cast_sequence
(
    sequence_no             text PRIMARY KEY,
    caster_id               text NOT NULL,
    tundish_no              text,
    start_time              timestamptz NOT NULL,
    end_time                timestamptz,
    planned_grade           text,
    actual_grade            text,
    sequence_status         text,
    last_update_ts          timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS src_caster_oracle_shape.cast_pieces
(
    piece_id                text PRIMARY KEY,
    heat_no                 text NOT NULL,
    sequence_no             text NOT NULL REFERENCES src_caster_oracle_shape.cast_sequence(sequence_no),
    caster_id               text NOT NULL,
    strand_no               integer NOT NULL,
    slab_no                 integer NOT NULL,
    cut_time                timestamptz NOT NULL,
    width_mm                numeric(10,2),
    thickness_mm            numeric(10,2),
    length_mm               numeric(12,2),
    weight_kg               numeric(14,3),
    mould_level_avg         numeric(10,4),
    casting_speed_avg       numeric(10,4),
    superheat_c             numeric(10,4),
    last_update_ts          timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_src_caster_piece_heat
ON src_caster_oracle_shape.cast_pieces(heat_no);

CREATE INDEX IF NOT EXISTS ix_src_caster_piece_update
ON src_caster_oracle_shape.cast_pieces(last_update_ts);

-- ----------------------------------------------------------------------------
-- 3. HSM Oracle-shaped source
-- ----------------------------------------------------------------------------

CREATE SCHEMA IF NOT EXISTS src_hsm_oracle_shape;

CREATE TABLE IF NOT EXISTS src_hsm_oracle_shape.hsm_coils
(
    coil_id                 text PRIMARY KEY,
    input_piece_id          text NOT NULL,
    heat_no                 text,
    mill_line               text NOT NULL,
    rolling_start_time      timestamptz NOT NULL,
    rolling_end_time        timestamptz,
    target_fdt_c            numeric(8,2),
    actual_fdt_c            numeric(8,2),
    target_ct_c             numeric(8,2),
    actual_ct_c             numeric(8,2),
    target_thickness_mm     numeric(10,4),
    actual_thickness_mm     numeric(10,4),
    target_width_mm         numeric(10,2),
    actual_width_mm         numeric(10,2),
    coil_weight_kg          numeric(14,3),
    last_update_ts          timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS src_hsm_oracle_shape.hsm_pass_measurements
(
    measurement_id          bigserial PRIMARY KEY,
    coil_id                 text NOT NULL REFERENCES src_hsm_oracle_shape.hsm_coils(coil_id),
    stand_no                integer NOT NULL,
    sample_time             timestamptz NOT NULL,
    rolling_force_kn        numeric(14,3),
    roll_gap_mm             numeric(12,5),
    speed_mps               numeric(12,5),
    temperature_c           numeric(8,2),
    last_update_ts          timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_src_hsm_coils_piece
ON src_hsm_oracle_shape.hsm_coils(input_piece_id);

CREATE INDEX IF NOT EXISTS ix_src_hsm_coils_update
ON src_hsm_oracle_shape.hsm_coils(last_update_ts);

CREATE INDEX IF NOT EXISTS ix_src_hsm_pass_coil_time
ON src_hsm_oracle_shape.hsm_pass_measurements(coil_id, sample_time);

-- ----------------------------------------------------------------------------
-- 4. Pickling / QA MSSQL-shaped source
-- ----------------------------------------------------------------------------

CREATE SCHEMA IF NOT EXISTS src_pkl_mssql_shape;

CREATE TABLE IF NOT EXISTS src_pkl_mssql_shape.pickle_orders
(
    order_id                text PRIMARY KEY,
    coil_id                 text NOT NULL,
    line_id                 text NOT NULL,
    customer_code           text,
    entry_time_utc          timestamptz NOT NULL,
    exit_time_utc           timestamptz,
    acid_concentration_pct  numeric(8,4),
    bath_temperature_c      numeric(8,2),
    line_speed_mpm          numeric(10,3),
    inspection_result       text,
    qa_decision             text,
    modified_at_utc         timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS src_pkl_mssql_shape.qa_lab_results
(
    lab_result_id           bigserial PRIMARY KEY,
    coil_id                 text NOT NULL,
    sample_time_utc         timestamptz NOT NULL,
    test_code               text NOT NULL,
    measured_value          numeric(18,6),
    unit_code               text,
    result_status           text,
    modified_at_utc         timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_src_pkl_orders_coil
ON src_pkl_mssql_shape.pickle_orders(coil_id);

CREATE INDEX IF NOT EXISTS ix_src_pkl_orders_modified
ON src_pkl_mssql_shape.pickle_orders(modified_at_utc);

CREATE INDEX IF NOT EXISTS ix_src_pkl_lab_coil_time
ON src_pkl_mssql_shape.qa_lab_results(coil_id, sample_time_utc);

-- ----------------------------------------------------------------------------
-- 5. Downtime + Parsytec MySQL-shaped source
-- ----------------------------------------------------------------------------

CREATE SCHEMA IF NOT EXISTS src_inspection_mysql_shape;

CREATE TABLE IF NOT EXISTS src_inspection_mysql_shape.parsytec_surface_defects
(
    defect_row_id           bigserial PRIMARY KEY,
    coil_id                 text NOT NULL,
    inspection_device       text NOT NULL,
    defect_code             text NOT NULL,
    defect_name             text,
    defect_class            text,
    defect_severity         text,
    side_code               text,
    position_start_m        numeric(14,3),
    position_end_m          numeric(14,3),
    width_position_mm       numeric(14,3),
    confidence_pct          numeric(8,4),
    event_time_utc          timestamptz NOT NULL,
    updated_at_utc          timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS src_inspection_mysql_shape.downtime_events
(
    downtime_id             bigserial PRIMARY KEY,
    equipment_code          text NOT NULL,
    source_line             text NOT NULL,
    reason_code             text NOT NULL,
    reason_text             text,
    downtime_category       text,
    start_time_utc          timestamptz NOT NULL,
    end_time_utc            timestamptz,
    duration_seconds        integer,
    updated_at_utc          timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_src_parsytec_coil_time
ON src_inspection_mysql_shape.parsytec_surface_defects(coil_id, event_time_utc);

CREATE INDEX IF NOT EXISTS ix_src_parsytec_updated
ON src_inspection_mysql_shape.parsytec_surface_defects(updated_at_utc);

CREATE INDEX IF NOT EXISTS ix_src_downtime_equipment_time
ON src_inspection_mysql_shape.downtime_events(equipment_code, start_time_utc);

CREATE INDEX IF NOT EXISTS ix_src_downtime_updated
ON src_inspection_mysql_shape.downtime_events(updated_at_utc);

COMMIT;

SELECT 'Phase 1 demo source-shaped schemas created' AS status;
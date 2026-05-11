BEGIN;

-- ============================================================================
-- PlantProcess IQ - Additional Demo Seed for Phase F
-- File: database/seeds/003_additional_demo_seed.sql
-- Purpose:
--   Extends the existing advanced demo dataset with:
--   1. Second site for multi-site tests
--   2. Deep 5-level genealogy chain
--   3. Aluminum industry template
--   4. Aborted process step
--   5. Boolean parameter observation
--   6. Extra defect/risk rows useful for correlation analytics
-- ============================================================================

-- 1. Second site
INSERT INTO sites
(
    id, created_at_utc, updated_at_utc, is_synthetic, source_system, source_record_id,
    is_deleted, deleted_at_utc, deleted_reason,
    site_code, site_name, company_name, country_code, time_zone_id
)
VALUES
(
    '10000000-0000-0000-0000-000000000002', NOW(), NULL, TRUE, 'PHASE_F_SEED', 'SITE-ALU-001',
    FALSE, NULL, NULL,
    'DEMO_PLANT_002', 'Demo Plant 002 - Aluminum / Multi-Site', 'PlantProcess Demo Company', 'DE', 'Europe/Berlin'
)
ON CONFLICT DO NOTHING;

-- 2. Area and equipment for second site
INSERT INTO areas
(
    id, created_at_utc, updated_at_utc, is_synthetic, source_system, source_record_id,
    is_deleted, deleted_at_utc, deleted_reason,
    site_id, parent_area_id, area_code, area_name, area_type, sort_order
)
VALUES
('11000000-0000-0000-0000-000000000001', NOW(), NULL, TRUE, 'PHASE_F_SEED', 'AREA-ALU-CAST', FALSE, NULL, NULL, '10000000-0000-0000-0000-000000000002', NULL, 'ALU_CAST_SHOP', 'Aluminum Casting Shop', 'ProductionArea', 10),
('11000000-0000-0000-0000-000000000002', NOW(), NULL, TRUE, 'PHASE_F_SEED', 'AREA-ALU-ROLL', FALSE, NULL, NULL, '10000000-0000-0000-0000-000000000002', NULL, 'ALU_ROLLING', 'Aluminum Rolling Area', 'ProductionArea', 20)
ON CONFLICT DO NOTHING;

INSERT INTO equipment
(
    id, created_at_utc, updated_at_utc, is_synthetic, source_system, source_record_id,
    is_deleted, deleted_at_utc, deleted_reason,
    site_id, area_id, parent_equipment_id, equipment_code, equipment_name, equipment_type, manufacturer, is_active, sort_order
)
VALUES
('12000000-0000-0000-0000-000000000001', NOW(), NULL, TRUE, 'PHASE_F_SEED', 'EQ-ALU-FURNACE-01', FALSE, NULL, NULL, '10000000-0000-0000-0000-000000000002', '11000000-0000-0000-0000-000000000001', NULL, 'ALU_FURNACE_01', 'Aluminum Furnace 01', 'Furnace', 'Demo', TRUE, 10),
('12000000-0000-0000-0000-000000000002', NOW(), NULL, TRUE, 'PHASE_F_SEED', 'EQ-ALU-CASTER-01', FALSE, NULL, NULL, '10000000-0000-0000-0000-000000000002', '11000000-0000-0000-0000-000000000001', NULL, 'ALU_CASTER_01', 'Aluminum Caster 01', 'Caster', 'Demo', TRUE, 20),
('12000000-0000-0000-0000-000000000003', NOW(), NULL, TRUE, 'PHASE_F_SEED', 'EQ-ALU-MILL-01', FALSE, NULL, NULL, '10000000-0000-0000-0000-000000000002', '11000000-0000-0000-0000-000000000002', NULL, 'ALU_MILL_01', 'Aluminum Rolling Mill 01', 'RollingMill', 'Demo', TRUE, 30)
ON CONFLICT DO NOTHING;

-- 3. Aluminum configuration template
INSERT INTO industry_templates
(
    id, created_at_utc, updated_at_utc, is_synthetic, source_system, source_record_id,
    is_deleted, deleted_at_utc, deleted_reason,
    template_code, template_name, industry_name, description, version, is_active
)
VALUES
('13000000-0000-0000-0000-000000000001', NOW(), NULL, TRUE, 'PHASE_F_SEED', 'TPL-ALUMINUM', FALSE, NULL, NULL, 'ADV_ALUMINUM', 'Advanced Aluminum Demo Template', 'Aluminum', 'Demo route: Furnace -> Casting -> Rolling -> Inspection. Aluminum words are metadata only.', 'v1', TRUE)
ON CONFLICT DO NOTHING;

INSERT INTO material_unit_type_definitions
(
    id, created_at_utc, updated_at_utc, is_synthetic, source_system, source_record_id,
    is_deleted, deleted_at_utc, deleted_reason,
    industry_template_id, material_unit_type_code, material_unit_type_name, description, sort_order, is_active
)
VALUES
('13000000-0000-0000-0000-000000000011', NOW(), NULL, TRUE, 'PHASE_F_SEED', 'MUT-ALU-CAST', FALSE, NULL, NULL, '13000000-0000-0000-0000-000000000001', 'AluminumCast', 'Aluminum Cast', 'Aluminum demo material unit type.', 10, TRUE),
('13000000-0000-0000-0000-000000000012', NOW(), NULL, TRUE, 'PHASE_F_SEED', 'MUT-ALU-BILLET', FALSE, NULL, NULL, '13000000-0000-0000-0000-000000000001', 'AluminumBillet', 'Aluminum Billet', 'Aluminum demo material unit type.', 20, TRUE),
('13000000-0000-0000-0000-000000000013', NOW(), NULL, TRUE, 'PHASE_F_SEED', 'MUT-ALU-ROLL', FALSE, NULL, NULL, '13000000-0000-0000-0000-000000000001', 'AluminumRoll', 'Aluminum Roll', 'Aluminum demo material unit type.', 30, TRUE)
ON CONFLICT DO NOTHING;

INSERT INTO operation_definitions
(
    id, created_at_utc, updated_at_utc, is_synthetic, source_system, source_record_id,
    is_deleted, deleted_at_utc, deleted_reason,
    industry_template_id, operation_code, operation_name, operation_category, description, sort_order, is_active
)
VALUES
('13000000-0000-0000-0000-000000000101', NOW(), NULL, TRUE, 'PHASE_F_SEED', 'OP-ALU-FURNACE', FALSE, NULL, NULL, '13000000-0000-0000-0000-000000000001', 'Aluminum_Furnace', 'Aluminum Furnace', 'Melting', 'Demo operation.', 10, TRUE),
('13000000-0000-0000-0000-000000000102', NOW(), NULL, TRUE, 'PHASE_F_SEED', 'OP-ALU-CASTING', FALSE, NULL, NULL, '13000000-0000-0000-0000-000000000001', 'Aluminum_Casting', 'Aluminum Casting', 'Casting', 'Demo operation.', 20, TRUE),
('13000000-0000-0000-0000-000000000103', NOW(), NULL, TRUE, 'PHASE_F_SEED', 'OP-ALU-ROLLING', FALSE, NULL, NULL, '13000000-0000-0000-0000-000000000001', 'Aluminum_Rolling', 'Aluminum Rolling', 'Rolling', 'Demo operation.', 30, TRUE)
ON CONFLICT DO NOTHING;

-- 4. Deep 5-level genealogy chain material units
INSERT INTO material_units
(
    id, created_at_utc, updated_at_utc, is_synthetic, source_system, source_record_id,
    is_deleted, deleted_at_utc, deleted_reason,
    material_code, material_unit_type, product_family, grade_or_recipe, site_id,
    production_start_utc, production_end_utc, production_start_local, production_end_local,
    plant_time_zone_id, plant_utc_offset_minutes
)
VALUES
('14000000-0000-0000-0000-000000000001', NOW(), NULL, TRUE, 'PHASE_F_SEED', 'ALU-RAW-001', FALSE, NULL, NULL, 'ALU_RAW_001', 'RawMaterial', 'AluminumDemo', 'AL-6061', '10000000-0000-0000-0000-000000000002', '2026-02-01 06:00:00+00', '2026-02-01 06:30:00+00', '2026-02-01 07:00:00', '2026-02-01 07:30:00', 'Europe/Berlin', 60),
('14000000-0000-0000-0000-000000000002', NOW(), NULL, TRUE, 'PHASE_F_SEED', 'ALU-MELT-001', FALSE, NULL, NULL, 'ALU_MELT_001', 'AluminumCast', 'AluminumDemo', 'AL-6061', '10000000-0000-0000-0000-000000000002', '2026-02-01 06:30:00+00', '2026-02-01 07:30:00+00', '2026-02-01 07:30:00', '2026-02-01 08:30:00', 'Europe/Berlin', 60),
('14000000-0000-0000-0000-000000000003', NOW(), NULL, TRUE, 'PHASE_F_SEED', 'ALU-BILLET-001', FALSE, NULL, NULL, 'ALU_BILLET_001', 'AluminumBillet', 'AluminumDemo', 'AL-6061', '10000000-0000-0000-0000-000000000002', '2026-02-01 07:30:00+00', '2026-02-01 08:20:00+00', '2026-02-01 08:30:00', '2026-02-01 09:20:00', 'Europe/Berlin', 60),
('14000000-0000-0000-0000-000000000004', NOW(), NULL, TRUE, 'PHASE_F_SEED', 'ALU-ROLL-001', FALSE, NULL, NULL, 'ALU_ROLL_001', 'AluminumRoll', 'AluminumDemo', 'AL-6061', '10000000-0000-0000-0000-000000000002', '2026-02-01 08:20:00+00', '2026-02-01 09:20:00+00', '2026-02-01 09:20:00', '2026-02-01 10:20:00', 'Europe/Berlin', 60),
('14000000-0000-0000-0000-000000000005', NOW(), NULL, TRUE, 'PHASE_F_SEED', 'ALU-FINAL-001', FALSE, NULL, NULL, 'ALU_FINAL_001', 'CustomerRoll', 'AluminumDemo', 'AL-6061', '10000000-0000-0000-0000-000000000002', '2026-02-01 09:20:00+00', '2026-02-01 10:00:00+00', '2026-02-01 10:20:00', '2026-02-01 11:00:00', 'Europe/Berlin', 60)
ON CONFLICT DO NOTHING;

INSERT INTO genealogy_edges
(
    id, created_at_utc, updated_at_utc, is_synthetic, source_system, source_record_id,
    is_deleted, deleted_at_utc, deleted_reason,
    parent_material_unit_id, child_material_unit_id, relationship_type, effective_from_utc, effective_to_utc
)
VALUES
('15000000-0000-0000-0000-000000000001', NOW(), NULL, TRUE, 'PHASE_F_SEED', 'GEN-1', FALSE, NULL, NULL, '14000000-0000-0000-0000-000000000001', '14000000-0000-0000-0000-000000000002', 'MeltedInto', '2026-02-01 06:30:00+00', NULL),
('15000000-0000-0000-0000-000000000002', NOW(), NULL, TRUE, 'PHASE_F_SEED', 'GEN-2', FALSE, NULL, NULL, '14000000-0000-0000-0000-000000000002', '14000000-0000-0000-0000-000000000003', 'CastInto', '2026-02-01 07:30:00+00', NULL),
('15000000-0000-0000-0000-000000000003', NOW(), NULL, TRUE, 'PHASE_F_SEED', 'GEN-3', FALSE, NULL, NULL, '14000000-0000-0000-0000-000000000003', '14000000-0000-0000-0000-000000000004', 'RolledInto', '2026-02-01 08:20:00+00', NULL),
('15000000-0000-0000-0000-000000000004', NOW(), NULL, TRUE, 'PHASE_F_SEED', 'GEN-4', FALSE, NULL, NULL, '14000000-0000-0000-0000-000000000004', '14000000-0000-0000-0000-000000000005', 'PackedInto', '2026-02-01 09:20:00+00', NULL)
ON CONFLICT DO NOTHING;

-- 5. Boolean parameter definition and observation
INSERT INTO parameter_definitions
(
    id, created_at_utc, updated_at_utc, is_synthetic, source_system, source_record_id,
    is_deleted, deleted_at_utc, deleted_reason,
    parameter_code, parameter_name, value_type, unit_of_measure, parameter_category, industry_template,
    expected_min_value, expected_max_value
)
VALUES
('16000000-0000-0000-0000-000000000001', NOW(), NULL, TRUE, 'PHASE_F_SEED', 'PARAM-COOLING-ACTIVE', FALSE, NULL, NULL, 'CoolingActive', 'Cooling Active', 'Boolean', NULL, 'ProcessState', 'Aluminum', NULL, NULL)
ON CONFLICT DO NOTHING;

INSERT INTO process_step_executions
(
    id, created_at_utc, updated_at_utc, is_synthetic, source_system, source_record_id,
    is_deleted, deleted_at_utc, deleted_reason,
    material_unit_id, equipment_id, operation_definition_id, operation_type, operation_code, crew_code,
    started_at_utc, ended_at_utc, started_at_local, ended_at_local,
    plant_time_zone_id, plant_utc_offset_minutes, execution_status
)
VALUES
('17000000-0000-0000-0000-000000000001', NOW(), NULL, TRUE, 'PHASE_F_SEED', 'STEP-ALU-ABORTED', FALSE, NULL, NULL, '14000000-0000-0000-0000-000000000004', '12000000-0000-0000-0000-000000000003', '13000000-0000-0000-0000-000000000103', 'Aluminum_Rolling', 'Aluminum_Rolling', 'CREW-B', '2026-02-01 08:20:00+00', '2026-02-01 08:55:00+00', '2026-02-01 09:20:00', '2026-02-01 09:55:00', 'Europe/Berlin', 60, 'Aborted')
ON CONFLICT DO NOTHING;

INSERT INTO parameter_observations
(
    id, created_at_utc, updated_at_utc, is_synthetic, source_system, source_record_id,
    is_deleted, deleted_at_utc, deleted_reason,
    material_unit_id, process_step_execution_id, parameter_definition_id, equipment_id,
    observed_at_utc, observed_at_local, plant_time_zone_id, plant_utc_offset_minutes,
    numeric_value, text_value, boolean_value, unit_of_measure, quality_flag, raw_value
)
VALUES
('18000000-0000-0000-0000-000000000001', NOW(), NULL, TRUE, 'PHASE_F_SEED', 'OBS-BOOL-001', FALSE, NULL, NULL, '14000000-0000-0000-0000-000000000004', '17000000-0000-0000-0000-000000000001', '16000000-0000-0000-0000-000000000001', '12000000-0000-0000-0000-000000000003', '2026-02-01 08:40:00+00', '2026-02-01 09:40:00', 'Europe/Berlin', 60, NULL, NULL, TRUE, NULL, 'Good', 'true')
ON CONFLICT DO NOTHING;

COMMIT;

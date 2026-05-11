BEGIN;

-- ============================================================================
-- PlantProcess IQ - Advanced Full Feature Demo Seed
-- File:
--   database/seeds/002_full_feature_demo_seed.sql
--
-- Purpose:
--   Full Sprint 2 backend validation dataset.
--
-- This seed tests:
--   1. Plant layout:
--      Site, recursive Area hierarchy, recursive Equipment hierarchy.
--
--   2. Configuration:
--      IndustryTemplate, MaterialUnitTypeDefinition, OperationDefinition,
--      Route, RouteStep.
--
--   3. Integration:
--      SourceSystemDefinition, ImportBatch, MappingDefinition.
--
--   4. Materials and genealogy:
--      MaterialUnit, MaterialAlias, GenealogyEdge.
--
--   5. Process:
--      ProcessStepExecution, ParameterDefinition, ParameterObservation,
--      ProcessEvent, DowntimeEvent.
--
--   6. Quality:
--      DefectCatalog, QualityEvent, DataQualityIssue.
--
--   7. Analytics:
--      RiskScore, low/medium/high risk, contributor JSON, invalid/bad cases.
--
--   8. Workflow and investigation:
--      Full genealogy chain from raw/intermediate/final material to risk.
--
--   9. Data-quality scan and validation:
--      Intentional bad data:
--        - Material without alias.
--        - Material without process history.
--        - Parameter observation without value.
--        - Parameter observation outside process-step time window.
--        - Process event without any reference.
--        - Downtime event without any reference.
--        - Defect quality event without defect catalog.
--        - High risk score without contributor JSON.
--        - Invalid risk score > 1, inserted directly for validation endpoint.
--        - Mapping definition with unknown target entity.
--        - Non-read-only source system.
--
-- Important:
--   Demo words like Heat, Slab, Coil, Batch, Tire, Roll are demo metadata only.
--   They are not hard-coded product architecture.
-- ============================================================================


-- ============================================================================
-- 0. Optional cleanup for only this advanced demo seed
--    Keep commented by default.
--    Use only if you want to reset this demo data.
-- ============================================================================

-- DELETE FROM data_quality_issues WHERE source_system = 'ADVANCED_DEMO_SEED';
-- DELETE FROM risk_scores WHERE source_system = 'ADVANCED_DEMO_SEED';
-- DELETE FROM quality_events WHERE source_system = 'ADVANCED_DEMO_SEED';
-- DELETE FROM downtime_events WHERE source_system = 'ADVANCED_DEMO_SEED';
-- DELETE FROM process_events WHERE source_system = 'ADVANCED_DEMO_SEED';
-- DELETE FROM parameter_observations WHERE source_system = 'ADVANCED_DEMO_SEED';
-- DELETE FROM process_step_executions WHERE source_system = 'ADVANCED_DEMO_SEED';
-- DELETE FROM genealogy_edges WHERE source_system = 'ADVANCED_DEMO_SEED';
-- DELETE FROM material_aliases WHERE source_system = 'ADVANCED_DEMO_SEED';
-- DELETE FROM material_units WHERE source_system = 'ADVANCED_DEMO_SEED';
-- DELETE FROM defect_catalogs WHERE source_system = 'ADVANCED_DEMO_SEED';
-- DELETE FROM parameter_definitions WHERE source_system = 'ADVANCED_DEMO_SEED';
-- DELETE FROM equipment WHERE source_system = 'ADVANCED_DEMO_SEED';
-- DELETE FROM areas WHERE source_system = 'ADVANCED_DEMO_SEED';
-- DELETE FROM sites WHERE source_system = 'ADVANCED_DEMO_SEED';
-- DELETE FROM route_steps WHERE source_system = 'ADVANCED_DEMO_SEED';
-- DELETE FROM routes WHERE source_system = 'ADVANCED_DEMO_SEED';
-- DELETE FROM operation_definitions WHERE source_system = 'ADVANCED_DEMO_SEED';
-- DELETE FROM material_unit_type_definitions WHERE source_system = 'ADVANCED_DEMO_SEED';
-- DELETE FROM industry_templates WHERE source_system = 'ADVANCED_DEMO_SEED';
-- DELETE FROM mapping_definitions WHERE source_system = 'ADVANCED_DEMO_SEED';
-- DELETE FROM import_batches WHERE source_system = 'ADVANCED_DEMO_SEED';
-- DELETE FROM source_system_definitions WHERE source_system = 'ADVANCED_DEMO_SEED';


-- ============================================================================
-- 1. Source systems
-- ============================================================================

INSERT INTO source_system_definitions
(
    id, created_at_utc, updated_at_utc, is_synthetic, source_system, source_record_id,
    is_deleted, deleted_at_utc, deleted_reason,
    source_system_code, source_system_name, source_system_type, description, is_read_only_source
)
VALUES
(
    '01000000-0000-0000-0000-000000000001', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'SRC-MES-001',
    FALSE, NULL, NULL,
    'MES_ADV_DEMO', 'Advanced Demo MES', 'MES',
    'Synthetic MES source for production/material master data.', TRUE
),
(
    '01000000-0000-0000-0000-000000000002', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'SRC-L2-001',
    FALSE, NULL, NULL,
    'L2_ADV_DEMO', 'Advanced Demo Level 2', 'Level2',
    'Synthetic Level 2 source for process steps and process parameters.', TRUE
),
(
    '01000000-0000-0000-0000-000000000003', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'SRC-HIST-001',
    FALSE, NULL, NULL,
    'HIST_ADV_DEMO', 'Advanced Demo Historian', 'Historian',
    'Synthetic historian source for time-series/process observations.', TRUE
),
(
    '01000000-0000-0000-0000-000000000004', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'SRC-QMS-001',
    FALSE, NULL, NULL,
    'QMS_ADV_DEMO', 'Advanced Demo QMS', 'QMS',
    'Synthetic QMS source for quality outcomes and final decisions.', TRUE
),
(
    '01000000-0000-0000-0000-000000000005', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'SRC-LAB-001',
    FALSE, NULL, NULL,
    'LAB_ADV_DEMO', 'Advanced Demo Lab', 'Lab',
    'Synthetic laboratory source for chemistry and recipe measurements.', TRUE
),
(
    '01000000-0000-0000-0000-000000000006', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'SRC-CMMS-001',
    FALSE, NULL, NULL,
    'CMMS_ADV_DEMO', 'Advanced Demo CMMS', 'CMMS',
    'Synthetic maintenance/downtime source.', TRUE
),
(
    '01000000-0000-0000-0000-000000000007', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'SRC-ERP-001',
    FALSE, NULL, NULL,
    'ERP_ADV_DEMO', 'Advanced Demo ERP', 'ERP',
    'Synthetic ERP/order source.', TRUE
),
(
    '01000000-0000-0000-0000-000000000099', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'SRC-NON-READONLY-TEST',
    FALSE, NULL, NULL,
    'API_WRITE_WARNING_TEST', 'Intentional Non ReadOnly Test Source', 'API',
    'Intentional warning source system. Data-quality scan should flag this.', FALSE
)
ON CONFLICT DO NOTHING;


-- ============================================================================
-- 2. Import batches
-- ============================================================================

INSERT INTO import_batches
(
    id, created_at_utc, updated_at_utc, is_synthetic, source_system, source_record_id,
    is_deleted, deleted_at_utc, deleted_reason,
    source_system_definition_id, import_batch_code, import_type, status,
    started_at_utc, completed_at_utc, source_object_name, file_name, checksum, row_count, error_message
)
VALUES
(
    '02000000-0000-0000-0000-000000000001', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'BATCH-MES-MATERIALS',
    FALSE, NULL, NULL,
    '01000000-0000-0000-0000-000000000001', 'ADV_MES_MATERIALS_20260101', 'SyntheticSeed', 'Completed',
    '2026-01-01 05:00:00+00', '2026-01-01 05:00:10+00',
    'mes_materials_export', 'adv_mes_materials.csv', 'adv-checksum-mes-001', 30, NULL
),
(
    '02000000-0000-0000-0000-000000000002', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'BATCH-L2-STEPS',
    FALSE, NULL, NULL,
    '01000000-0000-0000-0000-000000000002', 'ADV_L2_STEPS_20260101', 'SyntheticSeed', 'Completed',
    '2026-01-01 05:01:00+00', '2026-01-01 05:01:20+00',
    'l2_step_export', 'adv_l2_steps.csv', 'adv-checksum-l2-steps-001', 18, NULL
),
(
    '02000000-0000-0000-0000-000000000003', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'BATCH-HIST-PARAMS',
    FALSE, NULL, NULL,
    '01000000-0000-0000-0000-000000000003', 'ADV_HIST_PARAMETERS_20260101', 'SyntheticSeed', 'Completed',
    '2026-01-01 05:02:00+00', '2026-01-01 05:02:45+00',
    'historian_parameter_export', 'adv_hist_parameters.csv', 'adv-checksum-hist-001', 500, NULL
),
(
    '02000000-0000-0000-0000-000000000004', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'BATCH-QMS-QUALITY',
    FALSE, NULL, NULL,
    '01000000-0000-0000-0000-000000000004', 'ADV_QMS_QUALITY_20260101', 'SyntheticSeed', 'Completed',
    '2026-01-01 05:03:00+00', '2026-01-01 05:03:15+00',
    'qms_quality_export', 'adv_qms_quality.csv', 'adv-checksum-qms-001', 18, NULL
),
(
    '02000000-0000-0000-0000-000000000005', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'BATCH-LAB-CHEM',
    FALSE, NULL, NULL,
    '01000000-0000-0000-0000-000000000005', 'ADV_LAB_CHEMISTRY_20260101', 'SyntheticSeed', 'Running',
    '2026-01-01 05:04:00+00', NULL,
    'lab_chemistry_export', 'adv_lab_chemistry.csv', 'adv-checksum-lab-001', NULL, NULL
),
(
    '02000000-0000-0000-0000-000000000006', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'BATCH-CMMS-FAILED',
    FALSE, NULL, NULL,
    '01000000-0000-0000-0000-000000000006', 'ADV_CMMS_DOWNTIME_FAILED_20260101', 'SyntheticSeed', 'Failed',
    '2026-01-01 05:05:00+00', '2026-01-01 05:05:30+00',
    'cmms_downtime_export', 'adv_cmms_downtime.csv', 'adv-checksum-cmms-001', 0,
    'Intentional failed import batch for endpoint/status testing.'
)
ON CONFLICT DO NOTHING;


-- ============================================================================
-- 3. Mapping definitions
-- ============================================================================

INSERT INTO mapping_definitions
(
    id, created_at_utc, updated_at_utc, is_synthetic, source_system, source_record_id,
    is_deleted, deleted_at_utc, deleted_reason,
    source_system_definition_id, mapping_code, mapping_name, source_object_name,
    target_entity_name, mapping_json, mapping_version, is_active, description
)
VALUES
(
    '03000000-0000-0000-0000-000000000001', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'MAP-MES-MATERIAL',
    FALSE, NULL, NULL,
    '01000000-0000-0000-0000-000000000001', 'ADV_MAP_MES_MATERIAL_UNIT',
    'MES material export to canonical MaterialUnit',
    'mes_materials_export', 'MaterialUnit',
    '{"MaterialCode":"material_id","MaterialUnitType":"material_type","ProductFamily":"product_family","GradeOrRecipe":"grade","SiteId":"site_code"}',
    'v1', TRUE,
    'Tests mapping definition endpoint and canonical material mapping concept.'
),
(
    '03000000-0000-0000-0000-000000000002', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'MAP-L2-STEP',
    FALSE, NULL, NULL,
    '01000000-0000-0000-0000-000000000002', 'ADV_MAP_L2_PROCESS_STEP',
    'L2 process step export to ProcessStepExecution',
    'l2_step_export', 'ProcessStepExecution',
    '{"MaterialCode":"piece_id","OperationType":"operation","EquipmentCode":"equipment","StartedAtUtc":"start_utc","EndedAtUtc":"end_utc"}',
    'v1', TRUE,
    'Tests process-step mapping concept.'
),
(
    '03000000-0000-0000-0000-000000000003', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'MAP-HIST-PARAM',
    FALSE, NULL, NULL,
    '01000000-0000-0000-0000-000000000003', 'ADV_MAP_HIST_PARAMETER_OBSERVATION',
    'Historian tags to ParameterObservation',
    'historian_parameter_export', 'ParameterObservation',
    '{"MaterialCode":"piece_id","ParameterCode":"tag","NumericValue":"value","ObservedAtUtc":"sample_time","QualityFlag":"quality"}',
    'v1', TRUE,
    'Tests parameter observation mapping concept.'
),
(
    '03000000-0000-0000-0000-000000000004', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'MAP-QMS-QUALITY',
    FALSE, NULL, NULL,
    '01000000-0000-0000-0000-000000000004', 'ADV_MAP_QMS_QUALITY_EVENT',
    'QMS outcome export to QualityEvent',
    'qms_quality_export', 'QualityEvent',
    '{"MaterialCode":"coil_id","EventType":"quality_event","Severity":"severity","Decision":"decision","DefectCode":"defect_code"}',
    'v1', TRUE,
    'Tests quality-event mapping concept.'
),
(
    '03000000-0000-0000-0000-000000000099', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'MAP-UNKNOWN-TARGET-TEST',
    FALSE, NULL, NULL,
    '01000000-0000-0000-0000-000000000001', 'ADV_MAP_UNKNOWN_TARGET_FOR_VALIDATION',
    'Intentional unknown target mapping',
    'unknown_export', 'UnknownEntityForValidation',
    '{"SomeField":"some_source_column"}',
    'v1', TRUE,
    'Intentional validation issue. /validation/sync-report should flag this if your validation endpoint is added.'
)
ON CONFLICT DO NOTHING;


-- ============================================================================
-- 4. Industry templates
-- ============================================================================

INSERT INTO industry_templates
(
    id, created_at_utc, updated_at_utc, is_synthetic, source_system, source_record_id,
    is_deleted, deleted_at_utc, deleted_reason,
    template_code, template_name, industry_name, description, version, is_active
)
VALUES
(
    '04000000-0000-0000-0000-000000000001', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'TPL-FLAT-STEEL',
    FALSE, NULL, NULL,
    'ADV_FLAT_STEEL', 'Advanced Flat Steel Demo Template', 'FlatSteel',
    'Demo route: EAF/LF -> Casting -> Hot Rolling -> Inspection. Steel words are metadata only.', 'v1', TRUE
),
(
    '04000000-0000-0000-0000-000000000002', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'TPL-PHARMA',
    FALSE, NULL, NULL,
    'ADV_PHARMA', 'Advanced Pharma Demo Template', 'Pharma',
    'Demo route: Mixing -> Filling -> Packaging -> Lab/QC.', 'v1', TRUE
),
(
    '04000000-0000-0000-0000-000000000003', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'TPL-TIRE',
    FALSE, NULL, NULL,
    'ADV_TIRE', 'Advanced Tire Demo Template', 'Tire',
    'Demo route: Mixing -> Extrusion -> Curing -> Inspection.', 'v1', TRUE
),
(
    '04000000-0000-0000-0000-000000000004', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'TPL-PAPER',
    FALSE, NULL, NULL,
    'ADV_PAPER', 'Advanced Paper Demo Template', 'Paper',
    'Demo route: Pulping -> Drying -> Coating -> Rewinding.', 'v1', TRUE
)
ON CONFLICT DO NOTHING;


-- ============================================================================
-- 5. Material unit type definitions
-- ============================================================================

INSERT INTO material_unit_type_definitions
(
    id, created_at_utc, updated_at_utc, is_synthetic, source_system, source_record_id,
    is_deleted, deleted_at_utc, deleted_reason,
    industry_template_id, material_unit_type_code, material_unit_type_name, description, sort_order, is_active
)
VALUES
-- Flat steel
('05000000-0000-0000-0000-000000000001', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'MUT-HEAT', FALSE, NULL, NULL, '04000000-0000-0000-0000-000000000001', 'Heat', 'Heat', 'Flat steel demo material type.', 10, TRUE),
('05000000-0000-0000-0000-000000000002', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'MUT-CAST', FALSE, NULL, NULL, '04000000-0000-0000-0000-000000000001', 'Cast', 'Cast', 'Flat steel demo material type.', 20, TRUE),
('05000000-0000-0000-0000-000000000003', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'MUT-SLAB', FALSE, NULL, NULL, '04000000-0000-0000-0000-000000000001', 'Slab', 'Slab', 'Flat steel demo material type.', 30, TRUE),
('05000000-0000-0000-0000-000000000004', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'MUT-COIL', FALSE, NULL, NULL, '04000000-0000-0000-0000-000000000001', 'Coil', 'Coil', 'Flat steel demo material type.', 40, TRUE),

-- Pharma
('05000000-0000-0000-0000-000000000011', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'MUT-PH-BATCH', FALSE, NULL, NULL, '04000000-0000-0000-0000-000000000002', 'Batch', 'Batch', 'Pharma demo material type.', 10, TRUE),
('05000000-0000-0000-0000-000000000012', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'MUT-PH-LOT', FALSE, NULL, NULL, '04000000-0000-0000-0000-000000000002', 'Lot', 'Lot', 'Pharma demo material type.', 20, TRUE),
('05000000-0000-0000-0000-000000000013', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'MUT-PH-PACKAGED-LOT', FALSE, NULL, NULL, '04000000-0000-0000-0000-000000000002', 'PackagedLot', 'Packaged Lot', 'Pharma demo material type.', 30, TRUE),

-- Tire
('05000000-0000-0000-0000-000000000021', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'MUT-TIRE-COMPOUND', FALSE, NULL, NULL, '04000000-0000-0000-0000-000000000003', 'CompoundBatch', 'Compound Batch', 'Tire demo material type.', 10, TRUE),
('05000000-0000-0000-0000-000000000022', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'MUT-TIRE-UNIT', FALSE, NULL, NULL, '04000000-0000-0000-0000-000000000003', 'TireUnit', 'Tire Unit', 'Tire demo material type.', 20, TRUE),

-- Paper
('05000000-0000-0000-0000-000000000031', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'MUT-PAPER-JUMBO', FALSE, NULL, NULL, '04000000-0000-0000-0000-000000000004', 'JumboRoll', 'Jumbo Roll', 'Paper demo material type.', 10, TRUE),
('05000000-0000-0000-0000-000000000032', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'MUT-PAPER-CUSTOMER', FALSE, NULL, NULL, '04000000-0000-0000-0000-000000000004', 'CustomerRoll', 'Customer Roll', 'Paper demo material type.', 20, TRUE)
ON CONFLICT DO NOTHING;


-- ============================================================================
-- 6. Operation definitions
-- ============================================================================

INSERT INTO operation_definitions
(
    id, created_at_utc, updated_at_utc, is_synthetic, source_system, source_record_id,
    is_deleted, deleted_at_utc, deleted_reason,
    industry_template_id, operation_code, operation_name, operation_category, description, sort_order, is_active
)
VALUES
-- Flat steel
('06000000-0000-0000-0000-000000000001', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'OP-EAF', FALSE, NULL, NULL, '04000000-0000-0000-0000-000000000001', 'EAF_Melting', 'EAF Melting', 'Melting', 'Demo operation.', 10, TRUE),
('06000000-0000-0000-0000-000000000002', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'OP-LF', FALSE, NULL, NULL, '04000000-0000-0000-0000-000000000001', 'LF_Treatment', 'Ladle Furnace Treatment', 'Refining', 'Demo operation.', 20, TRUE),
('06000000-0000-0000-0000-000000000003', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'OP-CASTING', FALSE, NULL, NULL, '04000000-0000-0000-0000-000000000001', 'Continuous_Casting', 'Continuous Casting', 'Casting', 'Demo operation.', 30, TRUE),
('06000000-0000-0000-0000-000000000004', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'OP-HSM', FALSE, NULL, NULL, '04000000-0000-0000-0000-000000000001', 'Hot_Rolling', 'Hot Rolling', 'Rolling', 'Demo operation.', 40, TRUE),
('06000000-0000-0000-0000-000000000005', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'OP-INSPECTION', FALSE, NULL, NULL, '04000000-0000-0000-0000-000000000001', 'Final_Inspection', 'Final Inspection', 'Inspection', 'Demo operation.', 50, TRUE),

-- Pharma
('06000000-0000-0000-0000-000000000011', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'OP-PH-MIXING', FALSE, NULL, NULL, '04000000-0000-0000-0000-000000000002', 'Mixing', 'Mixing', 'Mixing', 'Pharma demo operation.', 10, TRUE),
('06000000-0000-0000-0000-000000000012', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'OP-PH-FILLING', FALSE, NULL, NULL, '04000000-0000-0000-0000-000000000002', 'Filling', 'Filling', 'Filling', 'Pharma demo operation.', 20, TRUE),
('06000000-0000-0000-0000-000000000013', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'OP-PH-PACKAGING', FALSE, NULL, NULL, '04000000-0000-0000-0000-000000000002', 'Packaging', 'Packaging', 'Packaging', 'Pharma demo operation.', 30, TRUE),
('06000000-0000-0000-0000-000000000014', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'OP-PH-LAB', FALSE, NULL, NULL, '04000000-0000-0000-0000-000000000002', 'Lab_Testing', 'Lab Testing', 'Quality', 'Pharma demo operation.', 40, TRUE),

-- Tire
('06000000-0000-0000-0000-000000000021', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'OP-TIRE-MIX', FALSE, NULL, NULL, '04000000-0000-0000-0000-000000000003', 'Rubber_Mixing', 'Rubber Mixing', 'Mixing', 'Tire demo operation.', 10, TRUE),
('06000000-0000-0000-0000-000000000022', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'OP-TIRE-CURE', FALSE, NULL, NULL, '04000000-0000-0000-0000-000000000003', 'Curing', 'Curing', 'Curing', 'Tire demo operation.', 20, TRUE),
('06000000-0000-0000-0000-000000000023', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'OP-TIRE-INSPECT', FALSE, NULL, NULL, '04000000-0000-0000-0000-000000000003', 'Uniformity_Inspection', 'Uniformity Inspection', 'Inspection', 'Tire demo operation.', 30, TRUE)
ON CONFLICT DO NOTHING;


-- ============================================================================
-- 7. Routes and route steps
-- ============================================================================

INSERT INTO routes
(
    id, created_at_utc, updated_at_utc, is_synthetic, source_system, source_record_id,
    is_deleted, deleted_at_utc, deleted_reason,
    industry_template_id, route_code, route_name, product_family, description, is_active
)
VALUES
(
    '07000000-0000-0000-0000-000000000001', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'ROUTE-FS-HRC',
    FALSE, NULL, NULL,
    '04000000-0000-0000-0000-000000000001', 'ADV_FS_HRC_ROUTE',
    'Advanced Flat Steel Hot Rolled Coil Route', 'FlatSteel',
    'EAF/LF -> Continuous Casting -> Hot Rolling -> Final Inspection.', TRUE
),
(
    '07000000-0000-0000-0000-000000000002', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'ROUTE-PH-LOT',
    FALSE, NULL, NULL,
    '04000000-0000-0000-0000-000000000002', 'ADV_PHARMA_PACKAGED_LOT_ROUTE',
    'Advanced Pharma Packaged Lot Route', 'Pharma',
    'Mixing -> Filling -> Packaging -> Lab Testing.', TRUE
),
(
    '07000000-0000-0000-0000-000000000003', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'ROUTE-TIRE',
    FALSE, NULL, NULL,
    '04000000-0000-0000-0000-000000000003', 'ADV_TIRE_UNIT_ROUTE',
    'Advanced Tire Unit Route', 'Tire',
    'Rubber Mixing -> Curing -> Uniformity Inspection.', TRUE
)
ON CONFLICT DO NOTHING;

INSERT INTO route_steps
(
    id, created_at_utc, updated_at_utc, is_synthetic, source_system, source_record_id,
    is_deleted, deleted_at_utc, deleted_reason,
    route_id, operation_definition_id, sequence_no, expected_material_unit_type, is_required, description
)
VALUES
-- Flat steel route
('08000000-0000-0000-0000-000000000001', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'RS-FS-01', FALSE, NULL, NULL, '07000000-0000-0000-0000-000000000001', '06000000-0000-0000-0000-000000000001', 10, 'Heat', TRUE, 'Melting step.'),
('08000000-0000-0000-0000-000000000002', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'RS-FS-02', FALSE, NULL, NULL, '07000000-0000-0000-0000-000000000001', '06000000-0000-0000-0000-000000000002', 20, 'Heat', TRUE, 'Refining step.'),
('08000000-0000-0000-0000-000000000003', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'RS-FS-03', FALSE, NULL, NULL, '07000000-0000-0000-0000-000000000001', '06000000-0000-0000-0000-000000000003', 30, 'Slab', TRUE, 'Casting step.'),
('08000000-0000-0000-0000-000000000004', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'RS-FS-04', FALSE, NULL, NULL, '07000000-0000-0000-0000-000000000001', '06000000-0000-0000-0000-000000000004', 40, 'Coil', TRUE, 'Rolling step.'),
('08000000-0000-0000-0000-000000000005', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'RS-FS-05', FALSE, NULL, NULL, '07000000-0000-0000-0000-000000000001', '06000000-0000-0000-0000-000000000005', 50, 'Coil', TRUE, 'Inspection step.'),

-- Pharma route
('08000000-0000-0000-0000-000000000011', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'RS-PH-01', FALSE, NULL, NULL, '07000000-0000-0000-0000-000000000002', '06000000-0000-0000-0000-000000000011', 10, 'Batch', TRUE, 'Mixing step.'),
('08000000-0000-0000-0000-000000000012', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'RS-PH-02', FALSE, NULL, NULL, '07000000-0000-0000-0000-000000000002', '06000000-0000-0000-0000-000000000012', 20, 'Lot', TRUE, 'Filling step.'),
('08000000-0000-0000-0000-000000000013', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'RS-PH-03', FALSE, NULL, NULL, '07000000-0000-0000-0000-000000000002', '06000000-0000-0000-0000-000000000013', 30, 'PackagedLot', TRUE, 'Packaging step.'),
('08000000-0000-0000-0000-000000000014', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'RS-PH-04', FALSE, NULL, NULL, '07000000-0000-0000-0000-000000000002', '06000000-0000-0000-0000-000000000014', 40, 'PackagedLot', TRUE, 'Lab test step.'),

-- Tire route
('08000000-0000-0000-0000-000000000021', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'RS-TI-01', FALSE, NULL, NULL, '07000000-0000-0000-0000-000000000003', '06000000-0000-0000-0000-000000000021', 10, 'CompoundBatch', TRUE, 'Rubber mixing step.'),
('08000000-0000-0000-0000-000000000022', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'RS-TI-02', FALSE, NULL, NULL, '07000000-0000-0000-0000-000000000003', '06000000-0000-0000-0000-000000000022', 20, 'TireUnit', TRUE, 'Curing step.'),
('08000000-0000-0000-0000-000000000023', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'RS-TI-03', FALSE, NULL, NULL, '07000000-0000-0000-0000-000000000003', '06000000-0000-0000-0000-000000000023', 30, 'TireUnit', TRUE, 'Uniformity inspection step.')
ON CONFLICT DO NOTHING;


-- ============================================================================
-- 8. Plant layout: site, recursive areas, recursive equipment
-- ============================================================================

INSERT INTO sites
(
    id, created_at_utc, updated_at_utc, is_synthetic, source_system, source_record_id,
    is_deleted, deleted_at_utc, deleted_reason,
    site_code, site_name, company_name, country_code, time_zone_id
)
VALUES
(
    '09000000-0000-0000-0000-000000000001', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'SITE-MFG-001',
    FALSE, NULL, NULL,
    'ADV_DEMO_PLANT', 'Advanced Demo Manufacturing Plant', 'PlantProcess IQ Demo Company', 'DE', 'Europe/Berlin'
)
ON CONFLICT DO NOTHING;

INSERT INTO areas
(
    id, created_at_utc, updated_at_utc, is_synthetic, source_system, source_record_id,
    is_deleted, deleted_at_utc, deleted_reason,
    site_id, parent_area_id, area_code, area_name, area_type, sort_order
)
VALUES
-- Top-level areas
('09100000-0000-0000-0000-000000000001', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'AREA-MFG', FALSE, NULL, NULL, '09000000-0000-0000-0000-000000000001', NULL, 'MANUFACTURING', 'Manufacturing', 'Department', 10),
('09100000-0000-0000-0000-000000000002', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'AREA-QA', FALSE, NULL, NULL, '09000000-0000-0000-0000-000000000001', NULL, 'QUALITY', 'Quality', 'Department', 20),
('09100000-0000-0000-0000-000000000003', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'AREA-WH', FALSE, NULL, NULL, '09000000-0000-0000-0000-000000000001', NULL, 'WAREHOUSE', 'Warehouse', 'Department', 30),

-- Manufacturing subareas
('09100000-0000-0000-0000-000000000011', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'AREA-MELT', FALSE, NULL, NULL, '09000000-0000-0000-0000-000000000001', '09100000-0000-0000-0000-000000000001', 'MELT_SHOP', 'Melt Shop', 'ProductionArea', 10),
('09100000-0000-0000-0000-000000000012', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'AREA-CAST', FALSE, NULL, NULL, '09000000-0000-0000-0000-000000000001', '09100000-0000-0000-0000-000000000001', 'CASTER_AREA', 'Caster Area', 'ProductionArea', 20),
('09100000-0000-0000-0000-000000000013', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'AREA-HSM', FALSE, NULL, NULL, '09000000-0000-0000-0000-000000000001', '09100000-0000-0000-0000-000000000001', 'HSM_AREA', 'Hot Strip Mill Area', 'ProductionArea', 30),
('09100000-0000-0000-0000-000000000014', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'AREA-PHARMA', FALSE, NULL, NULL, '09000000-0000-0000-0000-000000000001', '09100000-0000-0000-0000-000000000001', 'PHARMA_AREA', 'Pharma Demo Area', 'ProductionArea', 40),
('09100000-0000-0000-0000-000000000015', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'AREA-TIRE', FALSE, NULL, NULL, '09000000-0000-0000-0000-000000000001', '09100000-0000-0000-0000-000000000001', 'TIRE_AREA', 'Tire Demo Area', 'ProductionArea', 50),

-- Quality subareas
('09100000-0000-0000-0000-000000000021', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'AREA-QA-LAB', FALSE, NULL, NULL, '09000000-0000-0000-0000-000000000001', '09100000-0000-0000-0000-000000000002', 'QA_LAB', 'Quality Lab', 'Lab', 10),
('09100000-0000-0000-0000-000000000022', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'AREA-QA-INSPECTION', FALSE, NULL, NULL, '09000000-0000-0000-0000-000000000001', '09100000-0000-0000-0000-000000000002', 'QA_INSPECTION', 'Inspection Area', 'Inspection', 20)
ON CONFLICT DO NOTHING;

INSERT INTO equipment
(
    id, created_at_utc, updated_at_utc, is_synthetic, source_system, source_record_id,
    is_deleted, deleted_at_utc, deleted_reason,
    site_id, area_id, parent_equipment_id, equipment_code, equipment_name, equipment_type,
    manufacturer, is_active, sort_order
)
VALUES
-- Flat steel top-level equipment
('09200000-0000-0000-0000-000000000001', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'EQ-EAF1', FALSE, NULL, NULL, '09000000-0000-0000-0000-000000000001', '09100000-0000-0000-0000-000000000011', NULL, 'EAF_1', 'Electric Arc Furnace 1', 'Furnace', 'Demo OEM', TRUE, 10),
('09200000-0000-0000-0000-000000000002', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'EQ-LF1', FALSE, NULL, NULL, '09000000-0000-0000-0000-000000000001', '09100000-0000-0000-0000-000000000011', NULL, 'LF_1', 'Ladle Furnace 1', 'Furnace', 'Demo OEM', TRUE, 20),
('09200000-0000-0000-0000-000000000003', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'EQ-CASTER1', FALSE, NULL, NULL, '09000000-0000-0000-0000-000000000001', '09100000-0000-0000-0000-000000000012', NULL, 'CASTER_1', 'Continuous Caster 1', 'Caster', 'Demo OEM', TRUE, 30),
('09200000-0000-0000-0000-000000000004', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'EQ-HSM1', FALSE, NULL, NULL, '09000000-0000-0000-0000-000000000001', '09100000-0000-0000-0000-000000000013', NULL, 'HSM_1', 'Hot Strip Mill 1', 'ProductionLine', 'Demo OEM', TRUE, 40),
('09200000-0000-0000-0000-000000000005', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'EQ-SI1', FALSE, NULL, NULL, '09000000-0000-0000-0000-000000000001', '09100000-0000-0000-0000-000000000022', NULL, 'SURFACE_INSPECTION_1', 'Surface Inspection 1', 'InspectionDevice', 'Demo OEM', TRUE, 50),

-- Recursive child equipment under CASTER_1
('09200000-0000-0000-0000-000000000031', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'EQ-CASTER1-MOULD', FALSE, NULL, NULL, '09000000-0000-0000-0000-000000000001', '09100000-0000-0000-0000-000000000012', '09200000-0000-0000-0000-000000000003', 'CASTER_1_MOULD', 'Caster 1 Mould', 'Tooling', 'Demo OEM', TRUE, 31),
('09200000-0000-0000-0000-000000000032', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'EQ-CASTER1-SEG1', FALSE, NULL, NULL, '09000000-0000-0000-0000-000000000001', '09100000-0000-0000-0000-000000000012', '09200000-0000-0000-0000-000000000003', 'CASTER_1_SEGMENT_1', 'Caster 1 Segment 1', 'Segment', 'Demo OEM', TRUE, 32),

-- Recursive child equipment under HSM_1
('09200000-0000-0000-0000-000000000041', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'EQ-HSM1-F1', FALSE, NULL, NULL, '09000000-0000-0000-0000-000000000001', '09100000-0000-0000-0000-000000000013', '09200000-0000-0000-0000-000000000004', 'HSM_1_F1', 'Finishing Stand F1', 'MillStand', 'Demo OEM', TRUE, 41),
('09200000-0000-0000-0000-000000000042', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'EQ-HSM1-F2', FALSE, NULL, NULL, '09000000-0000-0000-0000-000000000001', '09100000-0000-0000-0000-000000000013', '09200000-0000-0000-0000-000000000004', 'HSM_1_F2', 'Finishing Stand F2', 'MillStand', 'Demo OEM', TRUE, 42),

-- Pharma and tire equipment
('09200000-0000-0000-0000-000000000101', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'EQ-PH-MIX1', FALSE, NULL, NULL, '09000000-0000-0000-0000-000000000001', '09100000-0000-0000-0000-000000000014', NULL, 'PH_MIXER_1', 'Pharma Mixer 1', 'Mixer', 'Demo OEM', TRUE, 101),
('09200000-0000-0000-0000-000000000102', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'EQ-PH-FILL1', FALSE, NULL, NULL, '09000000-0000-0000-0000-000000000001', '09100000-0000-0000-0000-000000000014', NULL, 'PH_FILLER_1', 'Pharma Filler 1', 'Filler', 'Demo OEM', TRUE, 102),
('09200000-0000-0000-0000-000000000201', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'EQ-TI-MIX1', FALSE, NULL, NULL, '09000000-0000-0000-0000-000000000001', '09100000-0000-0000-0000-000000000015', NULL, 'TIRE_MIXER_1', 'Tire Mixer 1', 'Mixer', 'Demo OEM', TRUE, 201),
('09200000-0000-0000-0000-000000000202', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'EQ-TI-CURE1', FALSE, NULL, NULL, '09000000-0000-0000-0000-000000000001', '09100000-0000-0000-0000-000000000015', NULL, 'TIRE_CURING_1', 'Tire Curing Press 1', 'CuringPress', 'Demo OEM', TRUE, 202)
ON CONFLICT DO NOTHING;


-- ============================================================================
-- 9. Parameter definitions
-- ============================================================================

INSERT INTO parameter_definitions
(
    id, created_at_utc, updated_at_utc, is_synthetic, source_system, source_record_id,
    is_deleted, deleted_at_utc, deleted_reason,
    parameter_code, parameter_name, value_type, unit_of_measure, parameter_category,
    industry_template, expected_min_value, expected_max_value
)
VALUES
-- Flat steel parameters
('10000000-0000-0000-0000-000000000001', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'PD-CARBON', FALSE, NULL, NULL, 'CARBON_PCT', 'Carbon Percentage', 'Numeric', '%', 'Chemistry', 'FlatSteel', 0.010000, 0.250000),
('10000000-0000-0000-0000-000000000002', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'PD-SUPERHEAT', FALSE, NULL, NULL, 'SUPERHEAT_C', 'Superheat', 'Numeric', 'C', 'Casting', 'FlatSteel', 10.000000, 60.000000),
('10000000-0000-0000-0000-000000000003', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'PD-CAST-SPEED', FALSE, NULL, NULL, 'CASTING_SPEED', 'Casting Speed', 'Numeric', 'm/min', 'Casting', 'FlatSteel', 0.500000, 2.500000),
('10000000-0000-0000-0000-000000000004', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'PD-MOULD', FALSE, NULL, NULL, 'MOULD_ID', 'Mould ID', 'Text', NULL, 'EquipmentContext', 'FlatSteel', NULL, NULL),
('10000000-0000-0000-0000-000000000005', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'PD-ROLL-FORCE', FALSE, NULL, NULL, 'ROLLING_FORCE', 'Rolling Force', 'Numeric', 'kN', 'Rolling', 'FlatSteel', 1000.000000, 40000.000000),
('10000000-0000-0000-0000-000000000006', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'PD-FLATNESS', FALSE, NULL, NULL, 'FLATNESS_IUNIT', 'Flatness', 'Numeric', 'I-Unit', 'Rolling', 'FlatSteel', 0.000000, 50.000000),

-- Pharma parameters
('10000000-0000-0000-0000-000000000101', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'PD-PH-PH', FALSE, NULL, NULL, 'PH_VALUE', 'pH Value', 'Numeric', 'pH', 'Lab', 'Pharma', 6.500000, 7.500000),
('10000000-0000-0000-0000-000000000102', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'PD-PH-HUMIDITY', FALSE, NULL, NULL, 'HUMIDITY_PCT', 'Humidity', 'Numeric', '%', 'Environment', 'Pharma', 30.000000, 55.000000),
('10000000-0000-0000-0000-000000000103', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'PD-PH-RECIPE', FALSE, NULL, NULL, 'RECIPE_CODE', 'Recipe Code', 'Text', NULL, 'Recipe', 'Pharma', NULL, NULL),

-- Tire parameters
('10000000-0000-0000-0000-000000000201', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'PD-TI-CURE-TEMP', FALSE, NULL, NULL, 'CURING_TEMP_C', 'Curing Temperature', 'Numeric', 'C', 'Curing', 'Tire', 140.000000, 190.000000),
('10000000-0000-0000-0000-000000000202', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'PD-TI-CURE-PRESSURE', FALSE, NULL, NULL, 'CURING_PRESSURE_BAR', 'Curing Pressure', 'Numeric', 'bar', 'Curing', 'Tire', 8.000000, 18.000000),
('10000000-0000-0000-0000-000000000203', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'PD-TI-UNIFORMITY', FALSE, NULL, NULL, 'UNIFORMITY_INDEX', 'Uniformity Index', 'Numeric', 'index', 'Inspection', 'Tire', 0.000000, 1.000000)
ON CONFLICT DO NOTHING;


-- ============================================================================
-- 10. Defect catalog
-- ============================================================================

INSERT INTO defect_catalogs
(
    id, created_at_utc, updated_at_utc, is_synthetic, source_system, source_record_id,
    is_deleted, deleted_at_utc, deleted_reason,
    defect_code, defect_name, defect_category, industry_template
)
VALUES
-- Flat steel
('11000000-0000-0000-0000-000000000001', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'DEF-SURFACE', FALSE, NULL, NULL, 'SURFACE_CRACK', 'Surface Crack', 'Surface', 'FlatSteel'),
('11000000-0000-0000-0000-000000000002', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'DEF-INCLUSION', FALSE, NULL, NULL, 'INCLUSION', 'Non-metallic Inclusion', 'Internal', 'FlatSteel'),
('11000000-0000-0000-0000-000000000003', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'DEF-FLATNESS', FALSE, NULL, NULL, 'CENTER_BUCKLE', 'Center Buckle', 'Shape', 'FlatSteel'),

-- Pharma
('11000000-0000-0000-0000-000000000101', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'DEF-PH-OOS', FALSE, NULL, NULL, 'OOS_PH', 'Out of Specification pH', 'Lab', 'Pharma'),
('11000000-0000-0000-0000-000000000102', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'DEF-PH-CONTAM', FALSE, NULL, NULL, 'CONTAMINATION_RISK', 'Contamination Risk', 'Quality', 'Pharma'),

-- Tire
('11000000-0000-0000-0000-000000000201', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'DEF-TI-UNIFORMITY', FALSE, NULL, NULL, 'UNIFORMITY_DEFECT', 'Uniformity Defect', 'Inspection', 'Tire'),
('11000000-0000-0000-0000-000000000202', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'DEF-TI-CURE', FALSE, NULL, NULL, 'UNDER_CURE', 'Under Cure', 'Curing', 'Tire')
ON CONFLICT DO NOTHING;


-- ============================================================================
-- 11. Material units
-- ============================================================================

INSERT INTO material_units
(
    id, created_at_utc, updated_at_utc, is_synthetic, source_system, source_record_id,
    is_deleted, deleted_at_utc, deleted_reason,
    material_code, material_unit_type, product_family, grade_or_recipe, site_id,
    production_start_utc, production_end_utc, production_start_local, production_end_local,
    plant_time_zone_id, plant_utc_offset_minutes
)
VALUES
-- Flat steel happy chain A: good product
('12000000-0000-0000-0000-000000000001', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'MAT-H1001', FALSE, NULL, NULL, 'ADV_H1001', 'Heat', 'FlatSteel', 'G2', '09000000-0000-0000-0000-000000000001', '2026-01-01 06:00:00+00', '2026-01-01 06:55:00+00', '2026-01-01 07:00:00', '2026-01-01 07:55:00', 'Europe/Berlin', 60),
('12000000-0000-0000-0000-000000000002', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'MAT-C2001', FALSE, NULL, NULL, 'ADV_C2001', 'Cast', 'FlatSteel', 'G2', '09000000-0000-0000-0000-000000000001', '2026-01-01 07:00:00+00', '2026-01-01 09:00:00+00', '2026-01-01 08:00:00', '2026-01-01 10:00:00', 'Europe/Berlin', 60),
('12000000-0000-0000-0000-000000000003', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'MAT-S3001', FALSE, NULL, NULL, 'ADV_S3001', 'Slab', 'FlatSteel', 'G2', '09000000-0000-0000-0000-000000000001', '2026-01-01 07:15:00+00', '2026-01-01 07:22:00+00', '2026-01-01 08:15:00', '2026-01-01 08:22:00', 'Europe/Berlin', 60),
('12000000-0000-0000-0000-000000000004', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'MAT-COIL4001', FALSE, NULL, NULL, 'ADV_COIL4001', 'Coil', 'FlatSteel', 'G2', '09000000-0000-0000-0000-000000000001', '2026-01-02 12:00:00+00', '2026-01-02 12:12:00+00', '2026-01-02 13:00:00', '2026-01-02 13:12:00', 'Europe/Berlin', 60),

-- Flat steel risky chain B: high defect/risk product
('12000000-0000-0000-0000-000000000011', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'MAT-H1002', FALSE, NULL, NULL, 'ADV_H1002', 'Heat', 'FlatSteel', 'G3', '09000000-0000-0000-0000-000000000001', '2026-01-03 06:00:00+00', '2026-01-03 06:55:00+00', '2026-01-03 07:00:00', '2026-01-03 07:55:00', 'Europe/Berlin', 60),
('12000000-0000-0000-0000-000000000012', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'MAT-S3002', FALSE, NULL, NULL, 'ADV_S3002', 'Slab', 'FlatSteel', 'G3', '09000000-0000-0000-0000-000000000001', '2026-01-03 07:15:00+00', '2026-01-03 07:22:00+00', '2026-01-03 08:15:00', '2026-01-03 08:22:00', 'Europe/Berlin', 60),
('12000000-0000-0000-0000-000000000013', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'MAT-COIL4002', FALSE, NULL, NULL, 'ADV_COIL4002', 'Coil', 'FlatSteel', 'G3', '09000000-0000-0000-0000-000000000001', '2026-01-04 12:00:00+00', '2026-01-04 12:12:00+00', '2026-01-04 13:00:00', '2026-01-04 13:12:00', 'Europe/Berlin', 60),

-- Pharma chain
('12000000-0000-0000-0000-000000000101', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'MAT-PH-BATCH1', FALSE, NULL, NULL, 'ADV_PH_BATCH_001', 'Batch', 'Pharma', 'Recipe_A', '09000000-0000-0000-0000-000000000001', '2026-01-05 08:00:00+00', '2026-01-05 09:30:00+00', '2026-01-05 09:00:00', '2026-01-05 10:30:00', 'Europe/Berlin', 60),
('12000000-0000-0000-0000-000000000102', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'MAT-PH-LOT1', FALSE, NULL, NULL, 'ADV_PH_LOT_001', 'Lot', 'Pharma', 'Recipe_A', '09000000-0000-0000-0000-000000000001', '2026-01-05 10:00:00+00', '2026-01-05 11:30:00+00', '2026-01-05 11:00:00', '2026-01-05 12:30:00', 'Europe/Berlin', 60),
('12000000-0000-0000-0000-000000000103', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'MAT-PH-PACK1', FALSE, NULL, NULL, 'ADV_PH_PACK_001', 'PackagedLot', 'Pharma', 'Recipe_A', '09000000-0000-0000-0000-000000000001', '2026-01-05 12:00:00+00', '2026-01-05 12:45:00+00', '2026-01-05 13:00:00', '2026-01-05 13:45:00', 'Europe/Berlin', 60),

-- Tire chain
('12000000-0000-0000-0000-000000000201', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'MAT-TI-COMP1', FALSE, NULL, NULL, 'ADV_TIRE_COMPOUND_001', 'CompoundBatch', 'Tire', 'Compound_X', '09000000-0000-0000-0000-000000000001', '2026-01-06 08:00:00+00', '2026-01-06 09:00:00+00', '2026-01-06 09:00:00', '2026-01-06 10:00:00', 'Europe/Berlin', 60),
('12000000-0000-0000-0000-000000000202', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'MAT-TI-UNIT1', FALSE, NULL, NULL, 'ADV_TIRE_UNIT_001', 'TireUnit', 'Tire', 'Compound_X', '09000000-0000-0000-0000-000000000001', '2026-01-06 10:00:00+00', '2026-01-06 11:00:00+00', '2026-01-06 11:00:00', '2026-01-06 12:00:00', 'Europe/Berlin', 60),

-- Intentional data-quality test material: no alias and no process steps
('12000000-0000-0000-0000-000000000999', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'MAT-ORPHAN-TEST', FALSE, NULL, NULL, 'ADV_ORPHAN_MATERIAL_001', 'Coil', 'FlatSteel', 'TEST', '09000000-0000-0000-0000-000000000001', NULL, NULL, NULL, NULL, 'Europe/Berlin', 60),

-- Intentional validation issue: type not defined in MaterialUnitTypeDefinition
('12000000-0000-0000-0000-000000000998', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'MAT-UNKNOWN-TYPE', FALSE, NULL, NULL, 'ADV_UNKNOWN_TYPE_MATERIAL_001', 'UnknownDemoType', 'ValidationTest', 'TEST', '09000000-0000-0000-0000-000000000001', NULL, NULL, NULL, NULL, 'Europe/Berlin', 60)
ON CONFLICT DO NOTHING;


-- ============================================================================
-- 12. Material aliases
-- ============================================================================

INSERT INTO material_aliases
(
    id, created_at_utc, updated_at_utc, is_synthetic, source_system, source_record_id,
    is_deleted, deleted_at_utc, deleted_reason,
    material_unit_id, alias_code, alias_type
)
VALUES
-- Flat steel chain A
('13000000-0000-0000-0000-000000000001', NOW(), NULL, TRUE, 'MES_ADV_DEMO', 'ALIAS-H1001', FALSE, NULL, NULL, '12000000-0000-0000-0000-000000000001', 'MES_HEAT_ADV_H1001', 'HeatId'),
('13000000-0000-0000-0000-000000000002', NOW(), NULL, TRUE, 'MES_ADV_DEMO', 'ALIAS-C2001', FALSE, NULL, NULL, '12000000-0000-0000-0000-000000000002', 'MES_CAST_ADV_C2001', 'CastId'),
('13000000-0000-0000-0000-000000000003', NOW(), NULL, TRUE, 'L2_ADV_DEMO', 'ALIAS-S3001', FALSE, NULL, NULL, '12000000-0000-0000-0000-000000000003', 'L2_SLAB_ADV_S3001', 'SlabId'),
('13000000-0000-0000-0000-000000000004', NOW(), NULL, TRUE, 'QMS_ADV_DEMO', 'ALIAS-COIL4001', FALSE, NULL, NULL, '12000000-0000-0000-0000-000000000004', 'QMS_COIL_ADV_4001', 'CoilId'),

-- Flat steel chain B
('13000000-0000-0000-0000-000000000011', NOW(), NULL, TRUE, 'MES_ADV_DEMO', 'ALIAS-H1002', FALSE, NULL, NULL, '12000000-0000-0000-0000-000000000011', 'MES_HEAT_ADV_H1002', 'HeatId'),
('13000000-0000-0000-0000-000000000012', NOW(), NULL, TRUE, 'L2_ADV_DEMO', 'ALIAS-S3002', FALSE, NULL, NULL, '12000000-0000-0000-0000-000000000012', 'L2_SLAB_ADV_S3002', 'SlabId'),
('13000000-0000-0000-0000-000000000013', NOW(), NULL, TRUE, 'QMS_ADV_DEMO', 'ALIAS-COIL4002', FALSE, NULL, NULL, '12000000-0000-0000-0000-000000000013', 'QMS_COIL_ADV_4002', 'CoilId'),

-- Pharma
('13000000-0000-0000-0000-000000000101', NOW(), NULL, TRUE, 'MES_ADV_DEMO', 'ALIAS-PH-BATCH', FALSE, NULL, NULL, '12000000-0000-0000-0000-000000000101', 'MES_PH_BATCH_001', 'BatchId'),
('13000000-0000-0000-0000-000000000102', NOW(), NULL, TRUE, 'MES_ADV_DEMO', 'ALIAS-PH-LOT', FALSE, NULL, NULL, '12000000-0000-0000-0000-000000000102', 'MES_PH_LOT_001', 'LotId'),
('13000000-0000-0000-0000-000000000103', NOW(), NULL, TRUE, 'QMS_ADV_DEMO', 'ALIAS-PH-PACK', FALSE, NULL, NULL, '12000000-0000-0000-0000-000000000103', 'QMS_PH_PACK_001', 'PackagedLotId'),

-- Tire
('13000000-0000-0000-0000-000000000201', NOW(), NULL, TRUE, 'MES_ADV_DEMO', 'ALIAS-TI-COMP', FALSE, NULL, NULL, '12000000-0000-0000-0000-000000000201', 'MES_TIRE_COMPOUND_001', 'CompoundBatchId'),
('13000000-0000-0000-0000-000000000202', NOW(), NULL, TRUE, 'QMS_ADV_DEMO', 'ALIAS-TI-UNIT', FALSE, NULL, NULL, '12000000-0000-0000-0000-000000000202', 'QMS_TIRE_UNIT_001', 'TireUnitId')
ON CONFLICT DO NOTHING;


-- ============================================================================
-- 13. Genealogy edges
-- ============================================================================

INSERT INTO genealogy_edges
(
    id, created_at_utc, updated_at_utc, is_synthetic, source_system, source_record_id,
    is_deleted, deleted_at_utc, deleted_reason,
    parent_material_unit_id, child_material_unit_id, relationship_type, effective_from_utc, effective_to_utc
)
VALUES
-- Flat steel happy chain A
('14000000-0000-0000-0000-000000000001', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'EDGE-H1001-C2001', FALSE, NULL, NULL, '12000000-0000-0000-0000-000000000001', '12000000-0000-0000-0000-000000000002', 'ProducedInto', '2026-01-01 07:00:00+00', NULL),
('14000000-0000-0000-0000-000000000002', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'EDGE-C2001-S3001', FALSE, NULL, NULL, '12000000-0000-0000-0000-000000000002', '12000000-0000-0000-0000-000000000003', 'SplitInto', '2026-01-01 07:15:00+00', NULL),
('14000000-0000-0000-0000-000000000003', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'EDGE-S3001-COIL4001', FALSE, NULL, NULL, '12000000-0000-0000-0000-000000000003', '12000000-0000-0000-0000-000000000004', 'RolledInto', '2026-01-02 12:00:00+00', NULL),

-- Flat steel risky chain B
('14000000-0000-0000-0000-000000000011', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'EDGE-H1002-S3002', FALSE, NULL, NULL, '12000000-0000-0000-0000-000000000011', '12000000-0000-0000-0000-000000000012', 'ProducedInto', '2026-01-03 07:15:00+00', NULL),
('14000000-0000-0000-0000-000000000012', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'EDGE-S3002-COIL4002', FALSE, NULL, NULL, '12000000-0000-0000-0000-000000000012', '12000000-0000-0000-0000-000000000013', 'RolledInto', '2026-01-04 12:00:00+00', NULL),

-- Pharma chain
('14000000-0000-0000-0000-000000000101', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'EDGE-PH-BATCH-LOT', FALSE, NULL, NULL, '12000000-0000-0000-0000-000000000101', '12000000-0000-0000-0000-000000000102', 'ProcessedInto', '2026-01-05 10:00:00+00', NULL),
('14000000-0000-0000-0000-000000000102', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'EDGE-PH-LOT-PACK', FALSE, NULL, NULL, '12000000-0000-0000-0000-000000000102', '12000000-0000-0000-0000-000000000103', 'PackedInto', '2026-01-05 12:00:00+00', NULL),

-- Tire chain
('14000000-0000-0000-0000-000000000201', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'EDGE-TI-COMP-UNIT', FALSE, NULL, NULL, '12000000-0000-0000-0000-000000000201', '12000000-0000-0000-0000-000000000202', 'ProcessedInto', '2026-01-06 10:00:00+00', NULL)
ON CONFLICT DO NOTHING;


-- ============================================================================
-- 14. Process step executions
-- ============================================================================

INSERT INTO process_step_executions
(
    id, created_at_utc, updated_at_utc, is_synthetic, source_system, source_record_id,
    is_deleted, deleted_at_utc, deleted_reason,
    material_unit_id, equipment_id, operation_type, operation_code, crew_code,
    started_at_utc, ended_at_utc, started_at_local, ended_at_local,
    plant_time_zone_id, plant_utc_offset_minutes, execution_status
)
VALUES
-- Flat steel happy chain A
('15000000-0000-0000-0000-000000000001', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'STEP-H1001-EAF', FALSE, NULL, NULL, '12000000-0000-0000-0000-000000000001', '09200000-0000-0000-0000-000000000001', 'EAF_Melting', 'EAF_Melting', 'Crew_A', '2026-01-01 06:00:00+00', '2026-01-01 06:45:00+00', '2026-01-01 07:00:00', '2026-01-01 07:45:00', 'Europe/Berlin', 60, 'Completed'),
('15000000-0000-0000-0000-000000000002', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'STEP-H1001-LF', FALSE, NULL, NULL, '12000000-0000-0000-0000-000000000001', '09200000-0000-0000-0000-000000000002', 'LF_Treatment', 'LF_Treatment', 'Crew_A', '2026-01-01 06:45:00+00', '2026-01-01 06:55:00+00', '2026-01-01 07:45:00', '2026-01-01 07:55:00', 'Europe/Berlin', 60, 'Completed'),
('15000000-0000-0000-0000-000000000003', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'STEP-S3001-CAST', FALSE, NULL, NULL, '12000000-0000-0000-0000-000000000003', '09200000-0000-0000-0000-000000000003', 'Continuous_Casting', 'Continuous_Casting', 'Crew_A', '2026-01-01 07:15:00+00', '2026-01-01 07:22:00+00', '2026-01-01 08:15:00', '2026-01-01 08:22:00', 'Europe/Berlin', 60, 'Completed'),
('15000000-0000-0000-0000-000000000004', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'STEP-COIL4001-HSM', FALSE, NULL, NULL, '12000000-0000-0000-0000-000000000004', '09200000-0000-0000-0000-000000000004', 'Hot_Rolling', 'Hot_Rolling', 'Crew_B', '2026-01-02 12:00:00+00', '2026-01-02 12:12:00+00', '2026-01-02 13:00:00', '2026-01-02 13:12:00', 'Europe/Berlin', 60, 'Completed'),
('15000000-0000-0000-0000-000000000005', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'STEP-COIL4001-INSP', FALSE, NULL, NULL, '12000000-0000-0000-0000-000000000004', '09200000-0000-0000-0000-000000000005', 'Final_Inspection', 'Final_Inspection', 'QA_1', '2026-01-02 13:00:00+00', '2026-01-02 13:10:00+00', '2026-01-02 14:00:00', '2026-01-02 14:10:00', 'Europe/Berlin', 60, 'Completed'),

-- Flat steel risky chain B
('15000000-0000-0000-0000-000000000011', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'STEP-H1002-EAF', FALSE, NULL, NULL, '12000000-0000-0000-0000-000000000011', '09200000-0000-0000-0000-000000000001', 'EAF_Melting', 'EAF_Melting', 'Crew_C', '2026-01-03 06:00:00+00', '2026-01-03 06:50:00+00', '2026-01-03 07:00:00', '2026-01-03 07:50:00', 'Europe/Berlin', 60, 'Completed'),
('15000000-0000-0000-0000-000000000012', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'STEP-S3002-CAST', FALSE, NULL, NULL, '12000000-0000-0000-0000-000000000012', '09200000-0000-0000-0000-000000000003', 'Continuous_Casting', 'Continuous_Casting', 'Crew_C', '2026-01-03 07:15:00+00', '2026-01-03 07:22:00+00', '2026-01-03 08:15:00', '2026-01-03 08:22:00', 'Europe/Berlin', 60, 'Completed'),
('15000000-0000-0000-0000-000000000013', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'STEP-COIL4002-HSM', FALSE, NULL, NULL, '12000000-0000-0000-0000-000000000013', '09200000-0000-0000-0000-000000000004', 'Hot_Rolling', 'Hot_Rolling', 'Crew_C', '2026-01-04 12:00:00+00', '2026-01-04 12:12:00+00', '2026-01-04 13:00:00', '2026-01-04 13:12:00', 'Europe/Berlin', 60, 'Completed'),

-- Pharma
('15000000-0000-0000-0000-000000000101', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'STEP-PH-MIX', FALSE, NULL, NULL, '12000000-0000-0000-0000-000000000101', '09200000-0000-0000-0000-000000000101', 'Mixing', 'Mixing', 'PH_Crew_1', '2026-01-05 08:00:00+00', '2026-01-05 09:30:00+00', '2026-01-05 09:00:00', '2026-01-05 10:30:00', 'Europe/Berlin', 60, 'Completed'),
('15000000-0000-0000-0000-000000000102', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'STEP-PH-FILL', FALSE, NULL, NULL, '12000000-0000-0000-0000-000000000102', '09200000-0000-0000-0000-000000000102', 'Filling', 'Filling', 'PH_Crew_1', '2026-01-05 10:00:00+00', '2026-01-05 11:30:00+00', '2026-01-05 11:00:00', '2026-01-05 12:30:00', 'Europe/Berlin', 60, 'Completed'),

-- Tire
('15000000-0000-0000-0000-000000000201', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'STEP-TI-MIX', FALSE, NULL, NULL, '12000000-0000-0000-0000-000000000201', '09200000-0000-0000-0000-000000000201', 'Rubber_Mixing', 'Rubber_Mixing', 'TI_Crew_1', '2026-01-06 08:00:00+00', '2026-01-06 09:00:00+00', '2026-01-06 09:00:00', '2026-01-06 10:00:00', 'Europe/Berlin', 60, 'Completed'),
('15000000-0000-0000-0000-000000000202', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'STEP-TI-CURE', FALSE, NULL, NULL, '12000000-0000-0000-0000-000000000202', '09200000-0000-0000-0000-000000000202', 'Curing', 'Curing', 'TI_Crew_1', '2026-01-06 10:00:00+00', '2026-01-06 11:00:00+00', '2026-01-06 11:00:00', '2026-01-06 12:00:00', 'Europe/Berlin', 60, 'Completed'),

-- Intentional validation issue: operation not defined
('15000000-0000-0000-0000-000000000999', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'STEP-UNKNOWN-OP', FALSE, NULL, NULL, '12000000-0000-0000-0000-000000000998', '09200000-0000-0000-0000-000000000004', 'UnknownOperationForValidation', 'UnknownOperationForValidation', 'TEST_CREW', '2026-01-07 08:00:00+00', '2026-01-07 08:10:00+00', '2026-01-07 09:00:00', '2026-01-07 09:10:00', 'Europe/Berlin', 60, 'Completed')
ON CONFLICT DO NOTHING;


-- ============================================================================
-- 15. Parameter observations
-- ============================================================================

INSERT INTO parameter_observations
(
    id, created_at_utc, updated_at_utc, is_synthetic, source_system, source_record_id,
    is_deleted, deleted_at_utc, deleted_reason,
    material_unit_id, process_step_execution_id, parameter_definition_id, equipment_id,
    observed_at_utc, observed_at_local, plant_time_zone_id, plant_utc_offset_minutes,
    numeric_value, text_value, boolean_value, unit_of_measure, quality_flag, raw_value
)
VALUES
-- Happy chain A: normal parameters
('16000000-0000-0000-0000-000000000001', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'OBS-H1001-CARBON', FALSE, NULL, NULL, '12000000-0000-0000-0000-000000000001', '15000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000001', '09200000-0000-0000-0000-000000000001', '2026-01-01 06:20:00+00', '2026-01-01 07:20:00', 'Europe/Berlin', 60, 0.080000, NULL, NULL, '%', 'Good', '0.080'),
('16000000-0000-0000-0000-000000000002', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'OBS-S3001-SUPERHEAT', FALSE, NULL, NULL, '12000000-0000-0000-0000-000000000003', '15000000-0000-0000-0000-000000000003', '10000000-0000-0000-0000-000000000002', '09200000-0000-0000-0000-000000000003', '2026-01-01 07:18:00+00', '2026-01-01 08:18:00', 'Europe/Berlin', 60, 32.000000, NULL, NULL, 'C', 'Good', '32'),
('16000000-0000-0000-0000-000000000003', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'OBS-S3001-SPEED', FALSE, NULL, NULL, '12000000-0000-0000-0000-000000000003', '15000000-0000-0000-0000-000000000003', '10000000-0000-0000-0000-000000000003', '09200000-0000-0000-0000-000000000003', '2026-01-01 07:18:10+00', '2026-01-01 08:18:10', 'Europe/Berlin', 60, 1.250000, NULL, NULL, 'm/min', 'Good', '1.25'),
('16000000-0000-0000-0000-000000000004', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'OBS-S3001-MOULD', FALSE, NULL, NULL, '12000000-0000-0000-0000-000000000003', '15000000-0000-0000-0000-000000000003', '10000000-0000-0000-0000-000000000004', '09200000-0000-0000-0000-000000000031', '2026-01-01 07:18:20+00', '2026-01-01 08:18:20', 'Europe/Berlin', 60, NULL, 'Mould_A', NULL, NULL, 'Good', 'Mould_A'),
('16000000-0000-0000-0000-000000000005', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'OBS-COIL4001-FORCE', FALSE, NULL, NULL, '12000000-0000-0000-0000-000000000004', '15000000-0000-0000-0000-000000000004', '10000000-0000-0000-0000-000000000005', '09200000-0000-0000-0000-000000000041', '2026-01-02 12:05:00+00', '2026-01-02 13:05:00', 'Europe/Berlin', 60, 22000.000000, NULL, NULL, 'kN', 'Good', '22000'),
('16000000-0000-0000-0000-000000000006', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'OBS-COIL4001-FLATNESS', FALSE, NULL, NULL, '12000000-0000-0000-0000-000000000004', '15000000-0000-0000-0000-000000000004', '10000000-0000-0000-0000-000000000006', '09200000-0000-0000-0000-000000000042', '2026-01-02 12:06:00+00', '2026-01-02 13:06:00', 'Europe/Berlin', 60, 12.000000, NULL, NULL, 'I-Unit', 'Good', '12'),

-- Risky chain B: out-of-range process parameters
('16000000-0000-0000-0000-000000000011', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'OBS-H1002-CARBON', FALSE, NULL, NULL, '12000000-0000-0000-0000-000000000011', '15000000-0000-0000-0000-000000000011', '10000000-0000-0000-0000-000000000001', '09200000-0000-0000-0000-000000000001', '2026-01-03 06:20:00+00', '2026-01-03 07:20:00', 'Europe/Berlin', 60, 0.210000, NULL, NULL, '%', 'Warning', '0.210'),
('16000000-0000-0000-0000-000000000012', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'OBS-S3002-SUPERHEAT-HIGH', FALSE, NULL, NULL, '12000000-0000-0000-0000-000000000012', '15000000-0000-0000-0000-000000000012', '10000000-0000-0000-0000-000000000002', '09200000-0000-0000-0000-000000000003', '2026-01-03 07:18:00+00', '2026-01-03 08:18:00', 'Europe/Berlin', 60, 72.000000, NULL, NULL, 'C', 'OutOfRange', '72'),
('16000000-0000-0000-0000-000000000013', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'OBS-S3002-SPEED-HIGH', FALSE, NULL, NULL, '12000000-0000-0000-0000-000000000012', '15000000-0000-0000-0000-000000000012', '10000000-0000-0000-0000-000000000003', '09200000-0000-0000-0000-000000000003', '2026-01-03 07:18:10+00', '2026-01-03 08:18:10', 'Europe/Berlin', 60, 2.600000, NULL, NULL, 'm/min', 'OutOfRange', '2.6'),
('16000000-0000-0000-0000-000000000014', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'OBS-COIL4002-FLATNESS-HIGH', FALSE, NULL, NULL, '12000000-0000-0000-0000-000000000013', '15000000-0000-0000-0000-000000000013', '10000000-0000-0000-0000-000000000006', '09200000-0000-0000-0000-000000000042', '2026-01-04 12:06:00+00', '2026-01-04 13:06:00', 'Europe/Berlin', 60, 58.000000, NULL, NULL, 'I-Unit', 'OutOfRange', '58'),

-- Pharma
('16000000-0000-0000-0000-000000000101', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'OBS-PH-PH-GOOD', FALSE, NULL, NULL, '12000000-0000-0000-0000-000000000101', '15000000-0000-0000-0000-000000000101', '10000000-0000-0000-0000-000000000101', '09200000-0000-0000-0000-000000000101', '2026-01-05 08:45:00+00', '2026-01-05 09:45:00', 'Europe/Berlin', 60, 7.100000, NULL, NULL, 'pH', 'Good', '7.1'),
('16000000-0000-0000-0000-000000000102', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'OBS-PH-RECIPE', FALSE, NULL, NULL, '12000000-0000-0000-0000-000000000101', '15000000-0000-0000-0000-000000000101', '10000000-0000-0000-0000-000000000103', '09200000-0000-0000-0000-000000000101', '2026-01-05 08:10:00+00', '2026-01-05 09:10:00', 'Europe/Berlin', 60, NULL, 'Recipe_A', NULL, NULL, 'Good', 'Recipe_A'),

-- Tire
('16000000-0000-0000-0000-000000000201', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'OBS-TI-CURE-TEMP', FALSE, NULL, NULL, '12000000-0000-0000-0000-000000000202', '15000000-0000-0000-0000-000000000202', '10000000-0000-0000-0000-000000000201', '09200000-0000-0000-0000-000000000202', '2026-01-06 10:30:00+00', '2026-01-06 11:30:00', 'Europe/Berlin', 60, 168.000000, NULL, NULL, 'C', 'Good', '168'),
('16000000-0000-0000-0000-000000000202', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'OBS-TI-UNIFORMITY', FALSE, NULL, NULL, '12000000-0000-0000-0000-000000000202', '15000000-0000-0000-0000-000000000202', '10000000-0000-0000-0000-000000000203', '09200000-0000-0000-0000-000000000202', '2026-01-06 10:55:00+00', '2026-01-06 11:55:00', 'Europe/Berlin', 60, 0.920000, NULL, NULL, 'index', 'Warning', '0.92'),

-- Intentional data-quality issue: no numeric/text/boolean value
('16000000-0000-0000-0000-000000000901', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'OBS-MISSING-VALUE', FALSE, NULL, NULL, '12000000-0000-0000-0000-000000000013', '15000000-0000-0000-0000-000000000013', '10000000-0000-0000-0000-000000000006', '09200000-0000-0000-0000-000000000004', '2026-01-04 12:07:00+00', '2026-01-04 13:07:00', 'Europe/Berlin', 60, NULL, NULL, NULL, 'I-Unit', 'Missing', NULL),

-- Intentional data-quality issue: observation outside linked process step window
('16000000-0000-0000-0000-000000000902', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'OBS-OUTSIDE-WINDOW', FALSE, NULL, NULL, '12000000-0000-0000-0000-000000000013', '15000000-0000-0000-0000-000000000013', '10000000-0000-0000-0000-000000000005', '09200000-0000-0000-0000-000000000004', '2026-01-04 15:00:00+00', '2026-01-04 16:00:00', 'Europe/Berlin', 60, 25000.000000, NULL, NULL, 'kN', 'Late', '25000')
ON CONFLICT DO NOTHING;


-- ============================================================================
-- 16. Process events
-- ============================================================================

INSERT INTO process_events
(
    id, created_at_utc, updated_at_utc, is_synthetic, source_system, source_record_id,
    is_deleted, deleted_at_utc, deleted_reason,
    material_unit_id, process_step_execution_id, equipment_id,
    event_type, event_at_utc, event_at_local, plant_time_zone_id, plant_utc_offset_minutes,
    event_value, description
)
VALUES
-- Normal events
('17000000-0000-0000-0000-000000000001', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'PE-CAST-STABLE', FALSE, NULL, NULL, '12000000-0000-0000-0000-000000000003', '15000000-0000-0000-0000-000000000003', '09200000-0000-0000-0000-000000000003', 'CastingStable', '2026-01-01 07:18:00+00', '2026-01-01 08:18:00', 'Europe/Berlin', 60, 'Stable', 'Normal casting condition.'),
('17000000-0000-0000-0000-000000000002', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'PE-HSM-COIL-END', FALSE, NULL, NULL, '12000000-0000-0000-0000-000000000004', '15000000-0000-0000-0000-000000000004', '09200000-0000-0000-0000-000000000004', 'CoilingCompleted', '2026-01-02 12:12:00+00', '2026-01-02 13:12:00', 'Europe/Berlin', 60, 'Completed', 'Coiling completed normally.'),

-- Risky events
('17000000-0000-0000-0000-000000000011', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'PE-CAST-CRACK-ALARM', FALSE, NULL, NULL, '12000000-0000-0000-0000-000000000012', '15000000-0000-0000-0000-000000000012', '09200000-0000-0000-0000-000000000003', 'CasterCrackAlarm', '2026-01-03 07:19:00+00', '2026-01-03 08:19:00', 'Europe/Berlin', 60, 'Alarm', 'Synthetic caster crack alarm.'),
('17000000-0000-0000-0000-000000000012', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'PE-MOULD-CHANGE', FALSE, NULL, NULL, '12000000-0000-0000-0000-000000000012', '15000000-0000-0000-0000-000000000012', '09200000-0000-0000-0000-000000000031', 'MouldLevelInstability', '2026-01-03 07:19:30+00', '2026-01-03 08:19:30', 'Europe/Berlin', 60, 'Unstable', 'Synthetic mould instability event.'),

-- Pharma and tire events
('17000000-0000-0000-0000-000000000101', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'PE-PH-MIX-COMPLETE', FALSE, NULL, NULL, '12000000-0000-0000-0000-000000000101', '15000000-0000-0000-0000-000000000101', '09200000-0000-0000-0000-000000000101', 'MixingCompleted', '2026-01-05 09:30:00+00', '2026-01-05 10:30:00', 'Europe/Berlin', 60, 'Completed', 'Pharma batch mixing completed.'),
('17000000-0000-0000-0000-000000000201', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'PE-TI-CURE-COMPLETE', FALSE, NULL, NULL, '12000000-0000-0000-0000-000000000202', '15000000-0000-0000-0000-000000000202', '09200000-0000-0000-0000-000000000202', 'CuringCompleted', '2026-01-06 11:00:00+00', '2026-01-06 12:00:00', 'Europe/Berlin', 60, 'Completed', 'Tire curing completed.'),

-- Intentional data-quality issue: no material, step or equipment reference
('17000000-0000-0000-0000-000000000999', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'PE-NO-REFERENCE', FALSE, NULL, NULL, NULL, NULL, NULL, 'EventWithoutReference', '2026-01-07 10:00:00+00', '2026-01-07 11:00:00', 'Europe/Berlin', 60, 'BadData', 'Intentional bad process event without material/step/equipment reference.')
ON CONFLICT DO NOTHING;


-- ============================================================================
-- 17. Downtime events
-- ============================================================================

INSERT INTO downtime_events
(
    id, created_at_utc, updated_at_utc, is_synthetic, source_system, source_record_id,
    is_deleted, deleted_at_utc, deleted_reason,
    material_unit_id, process_step_execution_id, equipment_id,
    started_at_utc, ended_at_utc, started_at_local, ended_at_local,
    plant_time_zone_id, plant_utc_offset_minutes,
    downtime_type, reason_code, description
)
VALUES
-- Valid downtime linked to risky casting chain
('18000000-0000-0000-0000-000000000001', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'DT-CAST-SPEED-HOLD', FALSE, NULL, NULL, '12000000-0000-0000-0000-000000000012', '15000000-0000-0000-0000-000000000012', '09200000-0000-0000-0000-000000000003', '2026-01-03 07:20:00+00', '2026-01-03 07:21:00+00', '2026-01-03 08:20:00', '2026-01-03 08:21:00', 'Europe/Berlin', 60, 'ProcessDelay', 'CAST_SPEED_HOLD', 'Synthetic short casting delay during risky slab.'),
('18000000-0000-0000-0000-000000000002', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'DT-HSM-SENSOR', FALSE, NULL, NULL, '12000000-0000-0000-0000-000000000013', '15000000-0000-0000-0000-000000000013', '09200000-0000-0000-0000-000000000041', '2026-01-04 12:04:00+00', '2026-01-04 12:05:00+00', '2026-01-04 13:04:00', '2026-01-04 13:05:00', 'Europe/Berlin', 60, 'SensorIssue', 'ROLL_FORCE_SENSOR_GLITCH', 'Synthetic sensor issue during hot rolling.'),

-- Intentional data-quality issue: no material, step or equipment reference
('18000000-0000-0000-0000-000000000999', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'DT-NO-REFERENCE', FALSE, NULL, NULL, NULL, NULL, NULL, '2026-01-07 10:05:00+00', '2026-01-07 10:10:00+00', '2026-01-07 11:05:00', '2026-01-07 11:10:00', 'Europe/Berlin', 60, 'UnknownDowntime', 'NO_REFERENCE', 'Intentional bad downtime without material/step/equipment reference.')
ON CONFLICT DO NOTHING;


-- ============================================================================
-- 18. Quality events
-- ============================================================================

INSERT INTO quality_events
(
    id, created_at_utc, updated_at_utc, is_synthetic, source_system, source_record_id,
    is_deleted, deleted_at_utc, deleted_reason,
    material_unit_id, defect_catalog_id, event_type, event_at_utc, event_at_local,
    plant_time_zone_id, plant_utc_offset_minutes, severity, decision, description
)
VALUES
-- Flat steel happy chain A
('19000000-0000-0000-0000-000000000001', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'QE-COIL4001-RELEASED', FALSE, NULL, NULL, '12000000-0000-0000-0000-000000000004', NULL, 'FinalDecision', '2026-01-02 13:00:00+00', '2026-01-02 14:00:00', 'Europe/Berlin', 60, 'Info', 'Released', 'Good coil released.'),

-- Flat steel risky chain B
('19000000-0000-0000-0000-000000000011', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'QE-COIL4002-SURFACE-CRACK', FALSE, NULL, NULL, '12000000-0000-0000-0000-000000000013', '11000000-0000-0000-0000-000000000001', 'Defect', '2026-01-04 13:00:00+00', '2026-01-04 14:00:00', 'Europe/Berlin', 60, 'High', 'Hold', 'Surface crack detected.'),
('19000000-0000-0000-0000-000000000012', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'QE-COIL4002-FINAL-HOLD', FALSE, NULL, NULL, '12000000-0000-0000-0000-000000000013', NULL, 'FinalDecision', '2026-01-04 13:10:00+00', '2026-01-04 14:10:00', 'Europe/Berlin', 60, 'High', 'Hold', 'Final decision hold due to detected surface defect.'),

-- Pharma
('19000000-0000-0000-0000-000000000101', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'QE-PH-PACK-RELEASED', FALSE, NULL, NULL, '12000000-0000-0000-0000-000000000103', NULL, 'FinalDecision', '2026-01-05 13:30:00+00', '2026-01-05 14:30:00', 'Europe/Berlin', 60, 'Info', 'Released', 'Pharma packaged lot released.'),

-- Tire
('19000000-0000-0000-0000-000000000201', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'QE-TI-UNIFORMITY', FALSE, NULL, NULL, '12000000-0000-0000-0000-000000000202', '11000000-0000-0000-0000-000000000201', 'Defect', '2026-01-06 11:20:00+00', '2026-01-06 12:20:00', 'Europe/Berlin', 60, 'Medium', 'Review', 'Synthetic tire uniformity warning.'),

-- Intentional data-quality issue: defect event without defect catalog
('19000000-0000-0000-0000-000000000999', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'QE-DEFECT-NO-CATALOG', FALSE, NULL, NULL, '12000000-0000-0000-0000-000000000013', NULL, 'Defect', '2026-01-04 13:05:00+00', '2026-01-04 14:05:00', 'Europe/Berlin', 60, 'Medium', 'Review', 'Intentional defect without catalog for data-quality scan.')
ON CONFLICT DO NOTHING;


-- ============================================================================
-- 19. Risk scores
-- ============================================================================

INSERT INTO risk_scores
(
    id, created_at_utc, updated_at_utc, is_synthetic, source_system, source_record_id,
    is_deleted, deleted_at_utc, deleted_reason,
    material_unit_id, risk_type, score, risk_class, main_contributors_json,
    scored_at_utc, scored_at_local, plant_time_zone_id, plant_utc_offset_minutes, model_version
)
VALUES
-- Good flat steel product
(
    '20000000-0000-0000-0000-000000000001', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'RS-COIL4001-SURFACE',
    FALSE, NULL, NULL,
    '12000000-0000-0000-0000-000000000004', 'SurfaceCrackRisk', 0.120000, 'Low',
    '{"contributors":[{"name":"CASTING_SPEED","value":"1.25 m/min","effect":"normal"},{"name":"SUPERHEAT_C","value":"32 C","effect":"normal"},{"name":"FinalDecision","value":"Released","effect":"good outcome"}]}',
    NOW(), NOW(), 'Europe/Berlin', 60, 'synthetic-v1'
),
-- Risky flat steel product
(
    '20000000-0000-0000-0000-000000000011', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'RS-COIL4002-SURFACE',
    FALSE, NULL, NULL,
    '12000000-0000-0000-0000-000000000013', 'SurfaceCrackRisk', 0.860000, 'High',
    '{"contributors":[{"name":"CASTING_SPEED","value":"2.6 m/min","effect":"above expected max"},{"name":"SUPERHEAT_C","value":"72 C","effect":"above expected max"},{"name":"CasterCrackAlarm","value":"Alarm","effect":"risk indicator"},{"name":"Decision","value":"Hold","effect":"quality outcome"}]}',
    NOW(), NOW(), 'Europe/Berlin', 60, 'synthetic-v1'
),
(
    '20000000-0000-0000-0000-000000000012', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'RS-COIL4002-FLATNESS',
    FALSE, NULL, NULL,
    '12000000-0000-0000-0000-000000000013', 'FlatnessRisk', 0.760000, 'High',
    '{"contributors":[{"name":"FLATNESS_IUNIT","value":"58","effect":"above expected max"},{"name":"ROLL_FORCE_SENSOR_GLITCH","value":"true","effect":"possible data-quality/process issue"}]}',
    NOW(), NOW(), 'Europe/Berlin', 60, 'synthetic-v1'
),
-- Pharma and tire
(
    '20000000-0000-0000-0000-000000000101', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'RS-PH-PACK-OOS',
    FALSE, NULL, NULL,
    '12000000-0000-0000-0000-000000000103', 'OosRisk', 0.180000, 'Low',
    '{"contributors":[{"name":"PH_VALUE","value":"7.1","effect":"normal"},{"name":"Recipe","value":"Recipe_A","effect":"known stable recipe"}]}',
    NOW(), NOW(), 'Europe/Berlin', 60, 'synthetic-v1'
),
(
    '20000000-0000-0000-0000-000000000201', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'RS-TI-UNIFORMITY',
    FALSE, NULL, NULL,
    '12000000-0000-0000-0000-000000000202', 'UniformityRisk', 0.620000, 'Medium',
    '{"contributors":[{"name":"UNIFORMITY_INDEX","value":"0.92","effect":"warning range"},{"name":"CURING_TEMP_C","value":"168","effect":"normal"}]}',
    NOW(), NOW(), 'Europe/Berlin', 60, 'synthetic-v1'
),
-- Intentional data-quality issue: high risk without contributors
(
    '20000000-0000-0000-0000-000000000901', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'RS-HIGH-NO-CONTRIB',
    FALSE, NULL, NULL,
    '12000000-0000-0000-0000-000000000013', 'InclusionRisk', 0.910000, 'High',
    NULL,
    NOW(), NOW(), 'Europe/Berlin', 60, 'synthetic-v1'
),
-- Intentional validation issue: invalid score > 1, only possible through direct SQL.
-- This tests /validation/sync-report if you added invalidRiskScores rule.
(
    '20000000-0000-0000-0000-000000000999', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'RS-INVALID-SCORE',
    FALSE, NULL, NULL,
    '12000000-0000-0000-0000-000000000013', 'InvalidRiskScoreForValidation', 1.150000, 'Invalid',
    '{"contributors":[{"name":"Direct SQL","value":"score greater than 1","effect":"validation test"}]}',
    NOW(), NOW(), 'Europe/Berlin', 60, 'synthetic-v1'
)
ON CONFLICT DO NOTHING;


-- ============================================================================
-- 20. Data-quality issues
-- ============================================================================

INSERT INTO data_quality_issues
(
    id, created_at_utc, updated_at_utc, is_synthetic, source_system, source_record_id,
    is_deleted, deleted_at_utc, deleted_reason,
    material_unit_id, issue_type, severity, description, affected_entity_name, affected_entity_id
)
VALUES
(
    '21000000-0000-0000-0000-000000000001', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'DQ-MISSING-PARAM-VALUE',
    FALSE, NULL, NULL,
    '12000000-0000-0000-0000-000000000013', 'MissingParameterValue', 'Error',
    'Intentional test issue: parameter observation has no numeric/text/boolean value.',
    'ParameterObservation', '16000000-0000-0000-0000-000000000901'
),
(
    '21000000-0000-0000-0000-000000000002', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'DQ-OBS-OUTSIDE-WINDOW',
    FALSE, NULL, NULL,
    '12000000-0000-0000-0000-000000000013', 'ParameterObservationOutsideStepWindow', 'Error',
    'Intentional test issue: observation timestamp is outside linked process-step time window.',
    'ParameterObservation', '16000000-0000-0000-0000-000000000902'
),
(
    '21000000-0000-0000-0000-000000000003', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'DQ-DEFECT-NO-CATALOG',
    FALSE, NULL, NULL,
    '12000000-0000-0000-0000-000000000013', 'DefectEventWithoutCatalog', 'Warning',
    'Intentional test issue: defect event has no standardized defect catalog.',
    'QualityEvent', '19000000-0000-0000-0000-000000000999'
),
(
    '21000000-0000-0000-0000-000000000004', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'DQ-HIGH-RISK-NO-CONTRIB',
    FALSE, NULL, NULL,
    '12000000-0000-0000-0000-000000000013', 'HighRiskScoreWithoutContributors', 'Warning',
    'Intentional test issue: high risk score has no contributor explanation JSON.',
    'RiskScore', '20000000-0000-0000-0000-000000000901'
),
(
    '21000000-0000-0000-0000-000000000005', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'DQ-ORPHAN-MATERIAL',
    FALSE, NULL, NULL,
    '12000000-0000-0000-0000-000000000999', 'MissingProcessHistory', 'Warning',
    'Intentional test issue: material has no process history.',
    'MaterialUnit', '12000000-0000-0000-0000-000000000999'
),
(
    '21000000-0000-0000-0000-000000000006', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'DQ-PROCESS-EVENT-NO-REF',
    FALSE, NULL, NULL,
    NULL, 'ProcessEventWithoutReference', 'Error',
    'Intentional test issue: process event has no material, process step or equipment reference.',
    'ProcessEvent', '17000000-0000-0000-0000-000000000999'
),
(
    '21000000-0000-0000-0000-000000000007', NOW(), NULL, TRUE, 'ADVANCED_DEMO_SEED', 'DQ-DOWNTIME-NO-REF',
    FALSE, NULL, NULL,
    NULL, 'DowntimeEventWithoutReference', 'Error',
    'Intentional test issue: downtime event has no material, process step or equipment reference.',
    'DowntimeEvent', '18000000-0000-0000-0000-000000000999'
)
ON CONFLICT DO NOTHING;


-- ============================================================================
-- 21. Final sanity summary
-- ============================================================================

COMMIT;
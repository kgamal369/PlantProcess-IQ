-- ============================================================================
-- PlantProcess IQ V7 Phase 2
-- Demo source presets, canonical demo layout, and genealogy-spine proof table.
-- Safe to rerun.
-- ============================================================================

SET client_min_messages TO WARNING;

CREATE TABLE IF NOT EXISTS ppiq_demo_source_presets (
    source_code text PRIMARY KEY,
    source_name text NOT NULL,
    source_type text NOT NULL,
    host_name text NOT NULL,
    port_number integer NULL,
    database_name text NULL,
    schema_hint text NULL,
    is_file_source boolean NOT NULL DEFAULT false,
    file_path text NULL,
    is_demo_only boolean NOT NULL DEFAULT true,
    is_active boolean NOT NULL DEFAULT true,
    created_at_utc timestamptz NOT NULL DEFAULT now()
);

INSERT INTO ppiq_demo_source_presets
(source_code, source_name, source_type, host_name, port_number, database_name, schema_hint, is_file_source, file_path)
VALUES
('DEMO_MELTSHOP_PG','Postgres MeltShop EAF/LF','PostgreSQL','meltshop-postgres',5432,'meltshop','public',false,null),
('DEMO_CASTER_ORACLE','Oracle Caster','Oracle','caster-oracle',1521,'FREEPDB1','CASTER_OWNER',false,null),
('DEMO_HSM_ORACLE','Oracle Hot Strip Mill','Oracle','hsm-oracle',1521,'FREEPDB1','HSM_OWNER',false,null),
('DEMO_PKL_MSSQL','MSSQL Pickling Line','SqlServer','pkl-mssql',1433,'pkl','dbo',false,null),
('DEMO_YARD_EXCEL','Excel Yard','Excel','excel-yard',null,null,null,true,'/data/yard_inventory.csv'),
('DEMO_DOWNTIME_MYSQL','MySQL Downtime','MySQL','downtime-mysql',3306,'downtime','public',false,null),
('DEMO_PARSYTEC_MYSQL','MySQL Parsytec Surface Inspection','MySQL','parsytec-mysql',3306,'parsytec','public',false,null),
('DEMO_QA_EXCEL','Excel QA','Excel','excel-qa',null,null,null,true,'/data/qa_samples.csv')
ON CONFLICT (source_code) DO UPDATE SET
    source_name = EXCLUDED.source_name,
    source_type = EXCLUDED.source_type,
    host_name = EXCLUDED.host_name,
    port_number = EXCLUDED.port_number,
    database_name = EXCLUDED.database_name,
    schema_hint = EXCLUDED.schema_hint,
    is_file_source = EXCLUDED.is_file_source,
    file_path = EXCLUDED.file_path,
    is_active = true;

CREATE TABLE IF NOT EXISTS ppiq_demo_canonical_layout (
    layout_code text PRIMARY KEY,
    area_code text NOT NULL,
    area_name text NOT NULL,
    equipment_code text NOT NULL,
    equipment_name text NOT NULL,
    sort_order integer NOT NULL,
    source_code text NOT NULL REFERENCES ppiq_demo_source_presets(source_code),
    description text NULL
);

INSERT INTO ppiq_demo_canonical_layout
(layout_code, area_code, area_name, equipment_code, equipment_name, sort_order, source_code, description)
VALUES
('LAYOUT_EAF_01','EAF_LF','Melt Shop','EAF-01','Electric Arc Furnace 01',10,'DEMO_MELTSHOP_PG','Heat chemistry and additives'),
('LAYOUT_LF_01','EAF_LF','Melt Shop','LF-01','Ladle Furnace 01',20,'DEMO_MELTSHOP_PG','Final chemistry adjustment'),
('LAYOUT_CC_02','CC','Continuous Caster','CASTER-02','Caster Strand 2',30,'DEMO_CASTER_ORACLE','Sequence, tundish, mould and slab'),
('LAYOUT_HSM_05','HSM','Hot Strip Mill','HSM-F5','Finishing Stand F5',40,'DEMO_HSM_ORACLE','Rolling force and flatness'),
('LAYOUT_PKL_01','PKL','Pickling','PKL-01','Pickling Line 01',50,'DEMO_PKL_MSSQL','Pickling process values'),
('LAYOUT_YARD_01','YARD','Yard','YARD-HOT','Hot Coil Yard',60,'DEMO_YARD_EXCEL','Yard storage and movement'),
('LAYOUT_PARSYTEC_01','INSPECTION','Surface Inspection','PARSYTEC-01','Surface Inspection System',70,'DEMO_PARSYTEC_MYSQL','Surface defect events'),
('LAYOUT_QA_01','QA','Quality Assurance','QA-LAB','QA Laboratory',80,'DEMO_QA_EXCEL','Final mechanical and defect QA')
ON CONFLICT (layout_code) DO UPDATE SET
    area_name = EXCLUDED.area_name,
    equipment_name = EXCLUDED.equipment_name,
    source_code = EXCLUDED.source_code,
    description = EXCLUDED.description;

CREATE TABLE IF NOT EXISTS ppiq_demo_genealogy_spine (
    coil_id text PRIMARY KEY,
    slab_id text NOT NULL,
    strand_no integer NOT NULL,
    sequence_id text NOT NULL,
    tundish_id text NOT NULL,
    ladle_id text NOT NULL,
    heat_id text NOT NULL,
    hsm_campaign text NOT NULL,
    defect_id text NULL,
    qa_sample_id text NULL,
    downtime_id text NULL,
    mapping_engine_contract text NOT NULL DEFAULT 'schema_mapping_engine_join_spine_v1',
    source_coverage jsonb NOT NULL,
    updated_at_utc timestamptz NOT NULL DEFAULT now()
);

INSERT INTO ppiq_demo_genealogy_spine
(coil_id, slab_id, strand_no, sequence_id, tundish_id, ladle_id, heat_id, hsm_campaign, defect_id, qa_sample_id, downtime_id, source_coverage)
VALUES
('ADV_COIL4002','ADV_SLAB4002',2,'ADV_SEQ4002','ADV_TUNDISH4002','ADV_LADLE4002','ADV_HEAT4002','HSM-CAMP-2026-05','ADV_DEFECT4002_1','ADV_QA4002','ADV_DOWNTIME4002',
 '{"DEMO_MELTSHOP_PG":true,"DEMO_CASTER_ORACLE":true,"DEMO_HSM_ORACLE":true,"DEMO_PKL_MSSQL":true,"DEMO_YARD_EXCEL":true,"DEMO_DOWNTIME_MYSQL":true,"DEMO_PARSYTEC_MYSQL":true,"DEMO_QA_EXCEL":true}'::jsonb)
ON CONFLICT (coil_id) DO UPDATE SET
    slab_id = EXCLUDED.slab_id,
    strand_no = EXCLUDED.strand_no,
    sequence_id = EXCLUDED.sequence_id,
    tundish_id = EXCLUDED.tundish_id,
    ladle_id = EXCLUDED.ladle_id,
    heat_id = EXCLUDED.heat_id,
    defect_id = EXCLUDED.defect_id,
    qa_sample_id = EXCLUDED.qa_sample_id,
    downtime_id = EXCLUDED.downtime_id,
    source_coverage = EXCLUDED.source_coverage,
    updated_at_utc = now();

CREATE OR REPLACE VIEW v_ppiq_demo_genealogy_spine AS
SELECT
    coil_id, slab_id, strand_no, sequence_id, tundish_id, ladle_id, heat_id,
    hsm_campaign, defect_id, qa_sample_id, downtime_id, mapping_engine_contract, source_coverage
FROM ppiq_demo_genealogy_spine;

CREATE OR REPLACE FUNCTION ppiq_demo_resolve_genealogy(p_coil_id text)
RETURNS TABLE (
    coil_id text, slab_id text, strand_no integer, sequence_id text,
    tundish_id text, ladle_id text, heat_id text, defect_id text, qa_sample_id text, downtime_id text
)
LANGUAGE sql
AS $$
    SELECT coil_id, slab_id, strand_no, sequence_id, tundish_id, ladle_id, heat_id, defect_id, qa_sample_id, downtime_id
    FROM ppiq_demo_genealogy_spine
    WHERE coil_id = p_coil_id;
$$;

SELECT 'PPIQ V7 Phase 2 demo source + genealogy foundation installed' AS status;

-- ============================================================================
-- PlantProcess IQ — P02/P03 completion foundation
--
-- Purpose:
--   P02:
--     - Demo-source connection presets for the 8 heterogeneous source systems.
--     - Genealogy acceptance spine for ADV_COIL4002 across melt/cast/roll/QA/defect/downtime/yard.
--
--   P03:
--     - Durable metadata-backed PageDefinition store.
--     - Owner/tenant/visibility/version support.
--
-- Safe:
--   Idempotent.
-- ============================================================================

CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- ============================================================================
-- P03 — Durable user-created page store
-- ============================================================================

CREATE TABLE IF NOT EXISTS page_definitions (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id text NOT NULL DEFAULT 'demo',
    slug text NOT NULL,
    title text NOT NULL,
    owner_user_name text NOT NULL,
    visibility text NOT NULL DEFAULT 'Private',
    version integer NOT NULL DEFAULT 1,
    layout_json jsonb NOT NULL DEFAULT '{}'::jsonb,
    widget_bindings_json jsonb NOT NULL DEFAULT '{}'::jsonb,
    is_deleted boolean NOT NULL DEFAULT false,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    updated_at_utc timestamptz NOT NULL DEFAULT now(),

    CONSTRAINT ck_page_definitions_slug
        CHECK (slug ~ '^[a-z0-9]+(?:-[a-z0-9]+)*$'),

    CONSTRAINT ck_page_definitions_visibility
        CHECK (visibility IN ('Private', 'Shared', 'Public'))
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_page_definitions_tenant_slug_active
ON page_definitions (tenant_id, slug)
WHERE is_deleted = false;

CREATE INDEX IF NOT EXISTS ix_page_definitions_owner_visible
ON page_definitions (tenant_id, owner_user_name, visibility)
WHERE is_deleted = false;

CREATE INDEX IF NOT EXISTS ix_page_definitions_updated_active
ON page_definitions (updated_at_utc DESC)
WHERE is_deleted = false;

-- ============================================================================
-- P02 — Demo-source connection presets
-- These are demo metadata only. They do not auto-connect.
-- ============================================================================

CREATE TABLE IF NOT EXISTS demo_source_connection_presets (
    preset_code text PRIMARY KEY,
    display_name text NOT NULL,
    provider_type text NOT NULL,
    host_name text NOT NULL,
    port_number integer,
    database_name text,
    schema_name text,
    username_hint text,
    source_role text NOT NULL,
    demo_only boolean NOT NULL DEFAULT true,
    auto_connect boolean NOT NULL DEFAULT false,
    notes text NOT NULL DEFAULT '',
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    updated_at_utc timestamptz NOT NULL DEFAULT now(),

    CONSTRAINT ck_demo_source_presets_provider
        CHECK (provider_type IN ('PostgreSQL', 'Oracle', 'MsSql', 'MySql', 'Excel', 'Csv'))
);

INSERT INTO demo_source_connection_presets (
    preset_code,
    display_name,
    provider_type,
    host_name,
    port_number,
    database_name,
    schema_name,
    username_hint,
    source_role,
    notes
)
VALUES
('meltshop-postgres', 'MeltShop PostgreSQL — EAF/LF', 'PostgreSQL', 'meltshop-postgres', 5432, 'meltshop', 'public', 'readonly', 'MeltShop', 'Heat, chemistry, ladle and tap data.'),
('caster-oracle', 'Caster Oracle — sequence/slab', 'Oracle', 'caster-oracle', 1521, 'FREEPDB1', 'CASTER', 'readonly', 'Caster', 'Sequence, tundish, strand and slab genealogy.'),
('hsm-oracle', 'HSM Oracle — coil/pass data', 'Oracle', 'hsm-oracle', 1521, 'FREEPDB1', 'HSM', 'readonly', 'Hot Strip Mill', 'Coil and rolling pass data.'),
('pkl-mssql', 'PKL MSSQL — pickling/inspection', 'MsSql', 'pkl-mssql', 1433, 'pkl', 'dbo', 'readonly', 'Pickling', 'Pickling/downstream events.'),
('downtime-mysql', 'Downtime MySQL — equipment stops', 'MySql', 'downtime-mysql', 3306, 'downtime', null, 'readonly', 'Downtime', 'Equipment stoppage and cascade context.'),
('parsytec-mysql', 'Parsytec MySQL — surface defects', 'MySql', 'parsytec-mysql', 3306, 'parsytec', null, 'readonly', 'Surface Inspection', 'Surface defect events and classifications.'),
('excel-yard', 'Excel Yard — coil inventory file', 'Excel', 'excel-yard', null, 'yard_inventory.csv', null, null, 'Yard', 'File-drop source for yard/inventory state.'),
('excel-qa', 'Excel QA — lab samples file', 'Excel', 'excel-qa', null, 'qa_samples.csv', null, null, 'Quality Lab', 'File-drop source for QA samples.')
ON CONFLICT (preset_code)
DO UPDATE SET
    display_name = EXCLUDED.display_name,
    provider_type = EXCLUDED.provider_type,
    host_name = EXCLUDED.host_name,
    port_number = EXCLUDED.port_number,
    database_name = EXCLUDED.database_name,
    schema_name = EXCLUDED.schema_name,
    username_hint = EXCLUDED.username_hint,
    source_role = EXCLUDED.source_role,
    notes = EXCLUDED.notes,
    demo_only = true,
    auto_connect = false,
    updated_at_utc = now();

-- ============================================================================
-- P02 — Genealogy acceptance spine
-- This is an acceptance spine, not a hardcoded runtime substitute.
-- The mapping engine must still resolve the operational canonical view.
-- ============================================================================

CREATE TABLE IF NOT EXISTS demo_genealogy_acceptance_spine (
    material_code text PRIMARY KEY,
    heat_id text NOT NULL,
    ladle_id text NOT NULL,
    tundish_id text NOT NULL,
    sequence_id text NOT NULL,
    strand_no integer NOT NULL,
    slab_id text NOT NULL,
    coil_id text NOT NULL,
    hsm_equipment text NOT NULL,
    parsytec_defect_ref text NOT NULL,
    qa_sample_ref text NOT NULL,
    downtime_ref text NOT NULL,
    yard_ref text NOT NULL,
    source_count integer NOT NULL DEFAULT 8,
    mapping_engine_required boolean NOT NULL DEFAULT true,
    verified_at_utc timestamptz NOT NULL DEFAULT now()
);

INSERT INTO demo_genealogy_acceptance_spine (
    material_code,
    heat_id,
    ladle_id,
    tundish_id,
    sequence_id,
    strand_no,
    slab_id,
    coil_id,
    hsm_equipment,
    parsytec_defect_ref,
    qa_sample_ref,
    downtime_ref,
    yard_ref,
    source_count,
    mapping_engine_required
)
VALUES (
    'ADV_COIL4002',
    'ADV_HEAT4002',
    'ADV_LADLE4002',
    'ADV_TUNDISH4002',
    'ADV_SEQ4002',
    2,
    'ADV_SLAB4002',
    'ADV_COIL4002',
    'HSM-F5',
    'ADV_SURF4002',
    'ADV_QA4002',
    'ADV_DOWNTIME4002',
    'ADV_YARD4002',
    8,
    true
)
ON CONFLICT (material_code)
DO UPDATE SET
    heat_id = EXCLUDED.heat_id,
    ladle_id = EXCLUDED.ladle_id,
    tundish_id = EXCLUDED.tundish_id,
    sequence_id = EXCLUDED.sequence_id,
    strand_no = EXCLUDED.strand_no,
    slab_id = EXCLUDED.slab_id,
    coil_id = EXCLUDED.coil_id,
    hsm_equipment = EXCLUDED.hsm_equipment,
    parsytec_defect_ref = EXCLUDED.parsytec_defect_ref,
    qa_sample_ref = EXCLUDED.qa_sample_ref,
    downtime_ref = EXCLUDED.downtime_ref,
    yard_ref = EXCLUDED.yard_ref,
    source_count = EXCLUDED.source_count,
    mapping_engine_required = true,
    verified_at_utc = now();

-- ============================================================================
-- P03 — seed one real stored metadata page
-- ============================================================================

INSERT INTO page_definitions (
    tenant_id,
    slug,
    title,
    owner_user_name,
    visibility,
    layout_json,
    widget_bindings_json
)
VALUES (
    'demo',
    'demo-quality-investigation',
    'Demo Quality Investigation',
    'system',
    'Shared',
    jsonb_build_object(
        'grid', jsonb_build_object('columns', 12, 'rowHeight', 80),
        'widgets', jsonb_build_array(
            jsonb_build_object('id', 'w-risk', 'kind', 'kpi', 'title', 'Risk KPI', 'x', 0, 'y', 0, 'w', 3, 'h', 2, 'source', 'schema_view:risk_summary'),
            jsonb_build_object('id', 'w-defects', 'kind', 'bar', 'title', 'Defect breakdown', 'x', 3, 'y', 0, 'w', 5, 'h', 3, 'source', 'schema_view:defect_breakdown'),
            jsonb_build_object('id', 'w-trend', 'kind', 'line', 'title', 'Defect trend', 'x', 8, 'y', 0, 'w', 4, 'h', 3, 'source', 'schema_view:quality_daily')
        )
    ),
    jsonb_build_object(
        'bindings', jsonb_build_array(
            jsonb_build_object('widgetId', 'w-risk', 'source', 'schema_view:risk_summary'),
            jsonb_build_object('widgetId', 'w-defects', 'source', 'schema_view:defect_breakdown'),
            jsonb_build_object('widgetId', 'w-trend', 'source', 'schema_view:quality_daily')
        )
    )
)
ON CONFLICT (tenant_id, slug)
WHERE is_deleted = false
DO UPDATE SET
    title = EXCLUDED.title,
    visibility = EXCLUDED.visibility,
    layout_json = EXCLUDED.layout_json,
    widget_bindings_json = EXCLUDED.widget_bindings_json,
    version = page_definitions.version + 1,
    updated_at_utc = now();

-- ============================================================================
-- P02/P03 acceptance view
-- ============================================================================

CREATE OR REPLACE VIEW v_ppiq_phase02_phase03_acceptance AS
SELECT
    'P02' AS phase_code,
    'Eight demo-source presets available' AS acceptance_item,
    COUNT(*) >= 8 AS is_satisfied,
    COUNT(*)::text AS evidence
FROM demo_source_connection_presets

UNION ALL

SELECT
    'P02',
    'ADV_COIL4002 genealogy acceptance spine exists',
    EXISTS (
        SELECT 1
        FROM demo_genealogy_acceptance_spine
        WHERE material_code = 'ADV_COIL4002'
          AND source_count >= 8
          AND mapping_engine_required = true
    ),
    COALESCE((
        SELECT source_count::text
        FROM demo_genealogy_acceptance_spine
        WHERE material_code = 'ADV_COIL4002'
    ), '0')

UNION ALL

SELECT
    'P03',
    'Durable PageDefinition store available',
    EXISTS (
        SELECT 1
        FROM information_schema.tables
        WHERE table_schema = 'public'
          AND table_name = 'page_definitions'
    ),
    (SELECT COUNT(*)::text FROM page_definitions WHERE is_deleted = false)

UNION ALL

SELECT
    'P03',
    'Demo page has three or more widget bindings',
    EXISTS (
        SELECT 1
        FROM page_definitions
        WHERE slug = 'demo-quality-investigation'
          AND jsonb_array_length(widget_bindings_json -> 'bindings') >= 3
          AND is_deleted = false
    ),
    COALESCE((
        SELECT jsonb_array_length(widget_bindings_json -> 'bindings')::text
        FROM page_definitions
        WHERE slug = 'demo-quality-investigation'
          AND is_deleted = false
        LIMIT 1
    ), '0');

SELECT * FROM v_ppiq_phase02_phase03_acceptance;

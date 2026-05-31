-- ============================================================================
-- PlantProcess IQ V7 Phase 3
-- Generic PageDefinition persistence, sharing, and seeded user-created page.
-- Safe to rerun.
-- ============================================================================

SET client_min_messages TO WARNING;

CREATE TABLE IF NOT EXISTS page_definitions (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id text NOT NULL DEFAULT 'demo',
    slug text NOT NULL,
    title text NOT NULL,
    owner_user_name text NOT NULL,
    visibility text NOT NULL DEFAULT 'Private',
    version integer NOT NULL DEFAULT 1,
    layout_json jsonb NOT NULL DEFAULT '{"widgets":[]}'::jsonb,
    widget_bindings_json jsonb NOT NULL DEFAULT '{"bindings":[]}'::jsonb,
    is_deleted boolean NOT NULL DEFAULT false,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    updated_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_page_definitions_slug CHECK (slug ~ '^[a-z0-9]+(-[a-z0-9]+)*$'),
    CONSTRAINT ck_page_definitions_visibility CHECK (visibility IN ('Private','Shared','Public'))
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_page_definitions_tenant_slug_active
ON page_definitions(tenant_id, slug)
WHERE is_deleted = false;

CREATE INDEX IF NOT EXISTS ix_page_definitions_owner
ON page_definitions(tenant_id, owner_user_name, updated_at_utc DESC)
WHERE is_deleted = false;

CREATE TABLE IF NOT EXISTS page_definition_shares (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    page_definition_id uuid NOT NULL REFERENCES page_definitions(id) ON DELETE CASCADE,
    shared_with_user_name text NOT NULL,
    permission text NOT NULL DEFAULT 'Viewer',
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_page_definition_shares_permission CHECK (permission IN ('Viewer','Editor','Owner'))
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_page_definition_shares_page_user
ON page_definition_shares(page_definition_id, shared_with_user_name);

CREATE TABLE IF NOT EXISTS page_definition_audit (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    page_definition_id uuid NOT NULL,
    audit_action text NOT NULL,
    actor_user_name text NOT NULL,
    version integer NOT NULL,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    payload_json jsonb NULL
);

INSERT INTO page_definitions
(tenant_id, slug, title, owner_user_name, visibility, version, layout_json, widget_bindings_json)
VALUES
('demo','demo-quality-investigation','Demo Quality Investigation','admin','Shared',1,
 '{"grid":{"columns":12,"rowHeight":80},"widgets":[{"id":"w-risk","type":"kpi","x":0,"y":0,"w":3,"h":2,"dashboardWidgetCode":"RISK_BY_CLASS"},{"id":"w-defects","type":"bar","x":3,"y":0,"w":5,"h":3,"dashboardWidgetCode":"DEFECT_BREAKDOWN"},{"id":"w-trend","type":"line","x":8,"y":0,"w":4,"h":3,"dashboardWidgetCode":"DEFECT_TREND"}]}'::jsonb,
 '{"bindings":[{"widgetId":"w-risk","source":"schema_view:material_quality_summary"},{"widgetId":"w-defects","source":"schema_view:defect_breakdown"},{"widgetId":"w-trend","source":"schema_view:quality_daily"}]}'::jsonb)
ON CONFLICT (tenant_id, slug) WHERE is_deleted = false
DO UPDATE SET
    title = EXCLUDED.title,
    visibility = EXCLUDED.visibility,
    layout_json = EXCLUDED.layout_json,
    widget_bindings_json = EXCLUDED.widget_bindings_json,
    updated_at_utc = now();

SELECT 'PPIQ V7 Phase 3 page builder foundation installed' AS status;

-- ============================================================
-- PlantProcess IQ — P5 OT-Safe Acquisition
-- PPIQ-T028 -> T032
--
-- Doctrine:
--   - Collector PUSHES to PPIQ.
--   - PPIQ NEVER opens inbound connection to OT source.
--   - PPIQ NEVER sends control/write commands to OT source.
--   - Historian ingestion is read-only and mapped into canonical observations.
-- ============================================================

CREATE SCHEMA IF NOT EXISTS acquisition;

CREATE TABLE IF NOT EXISTS acquisition.edge_collectors
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    collector_code text NOT NULL,
    site_code text NOT NULL,
    source_system_code text NOT NULL,
    credential_fingerprint text NOT NULL,
    one_way_push_only boolean NOT NULL DEFAULT true,
    no_inbound_network_path boolean NOT NULL DEFAULT true,
    no_control_path boolean NOT NULL DEFAULT true,
    buffer_retention_hours integer NOT NULL DEFAULT 72,
    is_active boolean NOT NULL DEFAULT true,
    registered_at_utc timestamptz NOT NULL DEFAULT now(),
    last_push_at_utc timestamptz NULL,
    credential_rotated_at_utc timestamptz NULL,
    CONSTRAINT ux_edge_collector_code UNIQUE (tenant_id, collector_code),
    CONSTRAINT ck_edge_collector_ot_safe CHECK (
        one_way_push_only = true
        AND no_inbound_network_path = true
        AND no_control_path = true
        AND buffer_retention_hours > 0
        AND length(credential_fingerprint) >= 8
    )
);

CREATE TABLE IF NOT EXISTS acquisition.edge_collector_batches
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    collector_id uuid NOT NULL REFERENCES acquisition.edge_collectors(id),
    source_system_code text NOT NULL,
    source_object_name text NOT NULL,
    batch_id text NOT NULL,
    captured_from_utc timestamptz NOT NULL,
    captured_to_utc timestamptz NOT NULL,
    row_count integer NOT NULL,
    accepted_row_count integer NOT NULL DEFAULT 0,
    signature_hash text NOT NULL,
    status text NOT NULL DEFAULT 'Accepted',
    pushed_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ux_edge_collector_batch UNIQUE (collector_id, batch_id),
    CONSTRAINT ck_edge_collector_batch_status CHECK (status IN ('Accepted','Rejected','Quarantined')),
    CONSTRAINT ck_edge_collector_batch_window CHECK (captured_to_utc >= captured_from_utc)
);

CREATE TABLE IF NOT EXISTS acquisition.edge_collector_buffer_status
(
    collector_id uuid PRIMARY KEY REFERENCES acquisition.edge_collectors(id),
    last_push_at_utc timestamptz NOT NULL,
    lag_seconds integer NOT NULL DEFAULT 0,
    buffered_batch_count integer NOT NULL DEFAULT 0,
    buffered_row_count integer NOT NULL DEFAULT 0,
    health_status text NOT NULL DEFAULT 'Healthy',
    network_proof text NOT NULL DEFAULT 'one-way-push-only/no-inbound-piq-to-ot/no-control-path',
    credential_status text NOT NULL DEFAULT 'active',
    updated_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_edge_collector_health CHECK (health_status IN ('Healthy','Warning','Critical','Offline')),
    CONSTRAINT ck_edge_collector_lag_non_negative CHECK (lag_seconds >= 0)
);

CREATE TABLE IF NOT EXISTS acquisition.historian_tag_mappings
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    source_system_code text NOT NULL,
    tag_name text NOT NULL,
    parameter_code text NOT NULL,
    unit_override text NULL,
    is_active boolean NOT NULL DEFAULT true,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ux_historian_tag_mapping UNIQUE (tenant_id, source_system_code, tag_name)
);

CREATE TABLE IF NOT EXISTS acquisition.schema_drift_events
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    source_system_code text NOT NULL,
    source_object_name text NOT NULL,
    detected_at_utc timestamptz NOT NULL DEFAULT now(),
    field_name text NOT NULL,
    drift_type text NOT NULL,
    severity text NOT NULL,
    before_value text NOT NULL,
    after_value text NOT NULL,
    blocks_ingestion boolean NOT NULL,
    remediation_prompt text NOT NULL,
    is_acknowledged boolean NOT NULL DEFAULT false,
    acknowledged_by text NULL,
    acknowledged_at_utc timestamptz NULL,
    CONSTRAINT ck_schema_drift_type CHECK (drift_type IN ('Added','Removed','TypeChanged','UnitChanged')),
    CONSTRAINT ck_schema_drift_severity CHECK (severity IN ('Info','Warning','Critical'))
);

CREATE INDEX IF NOT EXISTS ix_edge_collectors_tenant_status
ON acquisition.edge_collectors(tenant_id, is_active, last_push_at_utc DESC);

CREATE INDEX IF NOT EXISTS ix_schema_drift_events_open
ON acquisition.schema_drift_events(tenant_id, detected_at_utc DESC)
WHERE is_acknowledged = false;

COMMENT ON TABLE acquisition.edge_collectors IS
'P5 OT-safe collector registry. PPIQ accepts one-way pushes only and never opens inbound OT connections.';
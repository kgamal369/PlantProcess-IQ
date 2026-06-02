-- PPIQ Phase 6 — assistant retrieval chunks (tenant + permission partitioned) + index runs. Idempotent.
CREATE TABLE IF NOT EXISTS canon.assistant_chunk (
    id            uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id     uuid NOT NULL,
    scope_role    text NULL,                  -- minimum role required to see this chunk (NULL = any authenticated)
    source_kind   text NOT NULL,              -- 'doc' | 'analysis' | 'report' | 'finding'
    source_ref    text NOT NULL,
    content       text NOT NULL,
    embedding_json jsonb NOT NULL DEFAULT '[]'::jsonb,
    content_hash  text NOT NULL,
    is_synthetic  boolean NOT NULL DEFAULT false,
    is_stale      boolean NOT NULL DEFAULT false,
    indexed_at    timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_assistant_chunk UNIQUE (tenant_id, source_kind, source_ref)
);

CREATE TABLE IF NOT EXISTS canon.assistant_index_run (
    id             uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id      uuid NOT NULL,
    chunk_count    integer NOT NULL DEFAULT 0,
    replaced_count integer NOT NULL DEFAULT 0,
    correlation_id text NOT NULL,
    started_at     timestamptz NOT NULL DEFAULT now(),
    finished_at    timestamptz NULL
);

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname='canon' AND indexname='ix_assistant_chunk_tenant') THEN
        EXECUTE 'CREATE INDEX ix_assistant_chunk_tenant ON canon.assistant_chunk (tenant_id, is_stale, is_synthetic)';
    END IF;
END $$;
-- PPIQ Phase 5 — suggestion dedup key + workflow audit/comments. Idempotent; canon.* convention.
ALTER TABLE canon.suggestion ADD COLUMN IF NOT EXISTS suggestion_key text NULL;
ALTER TABLE canon.suggestion ADD COLUMN IF NOT EXISTS source_findings jsonb NOT NULL DEFAULT '[]'::jsonb;
ALTER TABLE canon.suggestion ADD COLUMN IF NOT EXISTS assigned_role text NULL;

-- One ACTIVE suggestion per (tenant, key); dismissed/closed rows don't block a fresh one.
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname='canon' AND indexname='ux_suggestion_active_key') THEN
        EXECUTE 'CREATE UNIQUE INDEX ux_suggestion_active_key ON canon.suggestion (tenant_id, suggestion_key) ' ||
                'WHERE suggestion_key IS NOT NULL AND status NOT IN (''dismissed'',''closed'',''rejected'')';
    END IF;
END $$;

CREATE TABLE IF NOT EXISTS canon.suggestion_audit (
    id            uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id     uuid NOT NULL,
    suggestion_id uuid NOT NULL,
    actor         text NOT NULL DEFAULT 'system',
    from_status   text NULL,
    to_status     text NOT NULL,
    note          text NULL,
    changed_at    timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS canon.suggestion_comment (
    id            uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id     uuid NOT NULL,
    suggestion_id uuid NOT NULL REFERENCES canon.suggestion(id) ON DELETE CASCADE,
    author        text NOT NULL,
    body          text NOT NULL,
    created_at    timestamptz NOT NULL DEFAULT now()
);

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname='canon' AND indexname='ix_suggestion_audit_sug') THEN
        EXECUTE 'CREATE INDEX ix_suggestion_audit_sug ON canon.suggestion_audit (tenant_id, suggestion_id, changed_at DESC)';
    END IF;
END $$;
-- 320_phase2_canonical_completion.sql
-- PlantProcess IQ - Phase 2 / T-020: complete & index the canonical model.
-- Idempotent and drift-safe. Apply with: psql "<conn>" -v ON_ERROR_STOP=1 -f 320_phase2_canonical_completion.sql
-- Order: after the canonical core scripts, before the value/suggestion engine scripts. Renumber to fit your chain.

BEGIN;

CREATE SCHEMA IF NOT EXISTS canon;

-- ---- missing canonical tables (engines arrive in P4/P5); tenant-scoped + audited ----
CREATE TABLE IF NOT EXISTS canon.value_impact (
    id                 uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id          uuid NOT NULL,
    finding_ref        text NOT NULL,
    coil_id            text NULL,
    defect_code        text NULL,
    currency           text NOT NULL DEFAULT 'EUR',
    impact_eur_low     numeric(18,2) NOT NULL,
    impact_eur_mid     numeric(18,2) NOT NULL,
    impact_eur_high    numeric(18,2) NOT NULL,
    assumption_version integer NOT NULL,
    is_abstained       boolean NOT NULL DEFAULT false,
    evidence           jsonb NOT NULL DEFAULT '{}'::jsonb,
    computed_at        timestamptz NOT NULL DEFAULT now(),
    created_at         timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_value_impact_band CHECK (impact_eur_low <= impact_eur_mid AND impact_eur_mid <= impact_eur_high),
    CONSTRAINT ck_value_impact_ccy  CHECK (char_length(currency) = 3)
);

CREATE TABLE IF NOT EXISTS canon.suggestion (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       uuid NOT NULL,
    title           text NOT NULL,
    action_type     text NOT NULL,
    status          text NOT NULL DEFAULT 'open',
    confidence      numeric(5,4) NOT NULL,
    impact_eur_low  numeric(18,2) NULL,
    impact_eur_high numeric(18,2) NULL,
    owner_user_id   uuid NULL,
    evidence        jsonb NOT NULL DEFAULT '[]'::jsonb,
    honesty_text    text NOT NULL,
    superseded_by   uuid NULL REFERENCES canon.suggestion(id) ON DELETE SET NULL,  -- self-reference proves out
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_suggestion_status CHECK (status IN ('open','assigned','accepted','rejected','closed','dismissed')),
    CONSTRAINT ck_suggestion_conf   CHECK (confidence >= 0 AND confidence <= 1)
);

-- ---- FK wiring to existing parents, added only if the parent exists (never fails on drift) ----
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.columns
               WHERE table_schema='canon' AND table_name='coil' AND column_name='coil_id')
       AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname='fk_value_impact_coil') THEN
        IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname='canon' AND indexname='ux_coil_coil_id') THEN
            EXECUTE 'CREATE UNIQUE INDEX ux_coil_coil_id ON canon.coil(coil_id)';
        END IF;
        EXECUTE 'ALTER TABLE canon.value_impact
                 ADD CONSTRAINT fk_value_impact_coil FOREIGN KEY (coil_id)
                 REFERENCES canon.coil(coil_id) ON DELETE CASCADE';
        RAISE NOTICE 'added fk_value_impact_coil';
    END IF;
END $$;

-- ---- covering indexes for genealogy traversal, time-window queries, tenant scoping ----
-- helper only creates an index when every referenced column actually exists in the table.
CREATE OR REPLACE FUNCTION canon._ensure_index(p_table text, p_index text, p_cols text)
RETURNS void LANGUAGE plpgsql AS $fn$
DECLARE col text; ok boolean := true;
BEGIN
    FOR col IN SELECT btrim(unnest(string_to_array(regexp_replace(p_cols, '[() ]', '', 'g'), ','))) LOOP
        IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                       WHERE table_schema='canon' AND table_name=p_table AND column_name=col) THEN
            ok := false;
        END IF;
    END LOOP;
    IF ok AND NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname='canon' AND indexname=p_index) THEN
        EXECUTE format('CREATE INDEX %I ON canon.%I %s', p_index, p_table, p_cols);
        RAISE NOTICE 'created index %', p_index;
    ELSIF NOT ok THEN
        RAISE NOTICE 'skipped index % (missing column in canon.%)', p_index, p_table;
    END IF;
END $fn$;

-- genealogy spine (both-direction parent lookups)
SELECT canon._ensure_index('coil',       'ix_coil_heat_id',        '(heat_id)');
SELECT canon._ensure_index('slab',       'ix_slab_heat_id',        '(heat_id)');
SELECT canon._ensure_index('slab',       'ix_slab_coil_id',        '(coil_id)');
SELECT canon._ensure_index('defect',     'ix_defect_coil_id',      '(coil_id)');
SELECT canon._ensure_index('lab_sample', 'ix_lab_heat_id',         '(heat_id)');
-- recursive area / equipment
SELECT canon._ensure_index('area',       'ix_area_parent',         '(parent_area_id)');
SELECT canon._ensure_index('equipment',  'ix_equip_parent',        '(parent_equipment_id)');
SELECT canon._ensure_index('equipment',  'ix_equip_area',          '(area_id)');
-- time-window
SELECT canon._ensure_index('parameter_observation', 'ix_paramobs_time',       '(event_time)');
SELECT canon._ensure_index('parameter_observation', 'ix_paramobs_equip_time', '(equipment_id, event_time)');
SELECT canon._ensure_index('quality_event',         'ix_qevent_time',         '(detected_at)');
SELECT canon._ensure_index('defect',                'ix_defect_time',         '(detected_at)');
-- new tables
SELECT canon._ensure_index('value_impact', 'ix_value_impact_tenant',     '(tenant_id)');
SELECT canon._ensure_index('value_impact', 'ix_value_impact_coil',       '(coil_id)');
SELECT canon._ensure_index('suggestion',   'ix_suggestion_tenant_status','(tenant_id, status)');

DROP FUNCTION canon._ensure_index(text, text, text);

-- ---- recursive self-reference report + post-apply assertion ----
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.columns
               WHERE table_schema='canon' AND table_name='area' AND column_name='parent_area_id') THEN
        RAISE NOTICE 'canon.area recursive self-reference present';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema='canon' AND table_name='value_impact') THEN
        RAISE EXCEPTION 'canon.value_impact missing after apply';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema='canon' AND table_name='suggestion') THEN
        RAISE EXCEPTION 'canon.suggestion missing after apply';
    END IF;
    RAISE NOTICE 'Phase 2 canonical completion: OK';
END $$;

COMMIT;
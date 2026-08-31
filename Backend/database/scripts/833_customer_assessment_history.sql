-- ============================================================================
-- 833_customer_assessment_history.sql
--
-- Customer data intake and capability assessment history.
--
-- AUTHORITY
--   This script is the single DDL authority for the assessment lineage and
--   assessment version tables. EF maps both with ExcludeFromMigrations() so a
--   second DDL authority cannot exist.
--
-- POSITION
--   831 definition store
--   -> 000_storage_topology_convergence
--   -> 832 definition contract convergence
--   -> 833 customer assessment history      (this file)
--   -> canonical views
--
-- SEMANTIC BOUNDARY
--   An assessment version records what was known about a customer input
--   structure at version N. It is evidence, not a semantic definition
--   authority. Nothing here participates in definition resolution, dimension
--   or measure registration, schema mapping or publication.
--
-- IMMUTABILITY RULING
--   UPDATE on an assessment version is refused by trigger. DELETE is NOT
--   refused: tenant erasure, retention policy and integration-test teardown
--   are legitimate row removals, and T-089's blanket refusal on the definition
--   version table produced a teardown failure that masked a passing test body.
--   Immutability here means an existing version is never rewritten in place;
--   a changed assessment truth creates version N+1.
--
-- DRIFT REFUSAL
--   CREATE TABLE IF NOT EXISTS is never treated as proof that the physical
--   contract is correct. Every owned object is verified after creation and the
--   script raises rather than reshaping a contradictory pre-existing table.
-- ============================================================================

\qecho PPIQ_PSQL_EXECUTED 833_customer_assessment_history

BEGIN;

-- ---------------------------------------------------------------------------
-- Predecessor assertion. 833 does not run against a database that has not
-- reached the 832 contract convergence.
-- ---------------------------------------------------------------------------
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.tables
        WHERE table_schema = 'ppiq_meta' AND table_name = 'definition_store'
    ) THEN
        RAISE EXCEPTION
            'PPIQ_833_PREDECESSOR_MISSING: ppiq_meta.definition_store not present. 831 and 832 must precede 833.';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.tables
        WHERE table_schema = 'ppiq_meta' AND table_name = 'definition_versions'
    ) THEN
        RAISE EXCEPTION
            'PPIQ_833_PREDECESSOR_MISSING: ppiq_meta.definition_versions not present. 831 and 832 must precede 833.';
    END IF;
END
$$;

-- ---------------------------------------------------------------------------
-- Assessment lineage. Stable tenant-scoped identity for one customer input
-- structure under continuing assessment.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS ppiq_meta.customer_assessments
(
    assessment_id       uuid         NOT NULL,
    tenant_id           uuid         NOT NULL,
    lineage_code        varchar(128) NOT NULL,
    display_name        varchar(256) NULL,
    created_at_utc      timestamptz  NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    CONSTRAINT pk_customer_assessments PRIMARY KEY (assessment_id),
    CONSTRAINT ux_customer_assessments_tenant_lineage UNIQUE (tenant_id, lineage_code)
);

-- ---------------------------------------------------------------------------
-- Assessment version. Immutable assessment report history.
--
-- semantic_fingerprint covers normalised intake + contract version + rule
-- version. Its uniqueness is scoped to the lineage: two different customers
-- may legitimately produce an identical fingerprint and must not collide.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS ppiq_meta.customer_assessment_versions
(
    assessment_version_id uuid        NOT NULL,
    assessment_id         uuid        NOT NULL,
    version_number        integer     NOT NULL,
    contract_version      varchar(32) NOT NULL,
    rule_version          varchar(32) NOT NULL,
    semantic_fingerprint  char(64)    NOT NULL,
    intake_json           jsonb       NOT NULL,
    report_json           jsonb       NOT NULL,
    created_at_utc        timestamptz NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    CONSTRAINT pk_customer_assessment_versions PRIMARY KEY (assessment_version_id),
    CONSTRAINT fk_customer_assessment_versions_assessment
        FOREIGN KEY (assessment_id)
        REFERENCES ppiq_meta.customer_assessments (assessment_id),
    CONSTRAINT ux_customer_assessment_versions_number
        UNIQUE (assessment_id, version_number),
    CONSTRAINT ux_customer_assessment_versions_fingerprint
        UNIQUE (assessment_id, semantic_fingerprint),
    CONSTRAINT ck_customer_assessment_versions_positive
        CHECK (version_number > 0)
);

-- ---------------------------------------------------------------------------
-- PHYSICAL POSTCONDITIONS
--
-- Everything below proves the physical contract rather than assuming that
-- CREATE TABLE IF NOT EXISTS produced it. A pre-existing table that
-- contradicts the contract stops the script by name; it is never reshaped.
-- ---------------------------------------------------------------------------
DO $$
DECLARE
    expected  record;
    actual    record;
    missing   text;
BEGIN
    -- --- column contract -------------------------------------------------
    FOR expected IN
        SELECT * FROM (VALUES
            ('customer_assessments',         'assessment_id',        'uuid',                        'NO'),
            ('customer_assessments',         'tenant_id',            'uuid',                        'NO'),
            ('customer_assessments',         'lineage_code',         'character varying',           'NO'),
            ('customer_assessments',         'display_name',         'character varying',           'YES'),
            ('customer_assessments',         'created_at_utc',       'timestamp with time zone',    'NO'),
            ('customer_assessment_versions', 'assessment_version_id','uuid',                        'NO'),
            ('customer_assessment_versions', 'assessment_id',        'uuid',                        'NO'),
            ('customer_assessment_versions', 'version_number',       'integer',                     'NO'),
            ('customer_assessment_versions', 'contract_version',     'character varying',           'NO'),
            ('customer_assessment_versions', 'rule_version',         'character varying',           'NO'),
            ('customer_assessment_versions', 'semantic_fingerprint', 'character',                   'NO'),
            ('customer_assessment_versions', 'intake_json',          'jsonb',                       'NO'),
            ('customer_assessment_versions', 'report_json',          'jsonb',                       'NO'),
            ('customer_assessment_versions', 'created_at_utc',       'timestamp with time zone',    'NO')
        ) AS t(tbl, col, typ, nullable)
    LOOP
        SELECT data_type, is_nullable INTO actual
        FROM information_schema.columns
        WHERE table_schema = 'ppiq_meta'
          AND table_name = expected.tbl
          AND column_name = expected.col;

        IF NOT FOUND THEN
            RAISE EXCEPTION
                'PPIQ_833_POSTCONDITION_COLUMN_MISSING: ppiq_meta.%.% is absent.',
                expected.tbl, expected.col;
        END IF;

        IF actual.data_type <> expected.typ THEN
            RAISE EXCEPTION
                'PPIQ_833_POSTCONDITION_COLUMN_TYPE: ppiq_meta.%.% is % but the contract requires %.',
                expected.tbl, expected.col, actual.data_type, expected.typ;
        END IF;

        IF actual.is_nullable <> expected.nullable THEN
            RAISE EXCEPTION
                'PPIQ_833_POSTCONDITION_COLUMN_NULLABILITY: ppiq_meta.%.% is_nullable=% but the contract requires %.',
                expected.tbl, expected.col, actual.is_nullable, expected.nullable;
        END IF;
    END LOOP;

    -- --- no unexpected owned columns -------------------------------------
    SELECT string_agg(column_name, ', ') INTO missing
    FROM information_schema.columns
    WHERE table_schema = 'ppiq_meta'
      AND table_name = 'customer_assessment_versions'
      AND column_name NOT IN (
            'assessment_version_id','assessment_id','version_number',
            'contract_version','rule_version','semantic_fingerprint',
            'intake_json','report_json','created_at_utc');

    IF missing IS NOT NULL THEN
        RAISE EXCEPTION
            'PPIQ_833_POSTCONDITION_UNEXPECTED_COLUMNS: ppiq_meta.customer_assessment_versions carries columns outside the contract: %.',
            missing;
    END IF;

    -- --- constraint contract ---------------------------------------------
    FOR expected IN
        SELECT * FROM (VALUES
            ('pk_customer_assessments'),
            ('ux_customer_assessments_tenant_lineage'),
            ('pk_customer_assessment_versions'),
            ('fk_customer_assessment_versions_assessment'),
            ('ux_customer_assessment_versions_number'),
            ('ux_customer_assessment_versions_fingerprint'),
            ('ck_customer_assessment_versions_positive')
        ) AS t(name)
    LOOP
        IF NOT EXISTS (
            SELECT 1
            FROM pg_constraint c
            JOIN pg_namespace n ON n.oid = c.connamespace
            WHERE n.nspname = 'ppiq_meta' AND c.conname = expected.name
        ) THEN
            RAISE EXCEPTION
                'PPIQ_833_POSTCONDITION_CONSTRAINT_MISSING: constraint % is absent in ppiq_meta. A contradictory pre-existing table is refused, never reshaped.',
                expected.name;
        END IF;
    END LOOP;

    RAISE NOTICE 'PPIQ_833_STRUCTURE_CONTRACT_PROVEN';
END
$$;

-- ---------------------------------------------------------------------------
-- Indexes and the in-place rewrite refusal are created only after the column
-- and constraint contract has been proven, so a contradictory pre-existing
-- table stops the script by name rather than through an incidental
-- CREATE INDEX failure.
-- ---------------------------------------------------------------------------
CREATE INDEX IF NOT EXISTS ix_customer_assessments_tenant
    ON ppiq_meta.customer_assessments (tenant_id);

CREATE INDEX IF NOT EXISTS ix_customer_assessment_versions_latest
    ON ppiq_meta.customer_assessment_versions (assessment_id, version_number DESC);

-- ---------------------------------------------------------------------------
-- In-place rewrite refusal.
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION ppiq_meta.fn_customer_assessment_version_immutable()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    RAISE EXCEPTION
        'PPIQ_833_ASSESSMENT_VERSION_IMMUTABLE: assessment version % of assessment % cannot be updated. Create a new version instead.',
        OLD.version_number, OLD.assessment_id;
END
$$;

DROP TRIGGER IF EXISTS trg_customer_assessment_version_immutable
    ON ppiq_meta.customer_assessment_versions;

CREATE TRIGGER trg_customer_assessment_version_immutable
    BEFORE UPDATE ON ppiq_meta.customer_assessment_versions
    FOR EACH ROW
    EXECUTE FUNCTION ppiq_meta.fn_customer_assessment_version_immutable();

DO $$
DECLARE
    expected record;
BEGIN
    -- --- index contract ---------------------------------------------------
    FOR expected IN
        SELECT * FROM (VALUES
            ('ix_customer_assessments_tenant'),
            ('ix_customer_assessment_versions_latest')
        ) AS t(name)
    LOOP
        IF NOT EXISTS (
            SELECT 1 FROM pg_indexes
            WHERE schemaname = 'ppiq_meta' AND indexname = expected.name
        ) THEN
            RAISE EXCEPTION
                'PPIQ_833_POSTCONDITION_INDEX_MISSING: index % is absent in ppiq_meta.',
                expected.name;
        END IF;
    END LOOP;

    -- --- trigger contract -------------------------------------------------
    IF NOT EXISTS (
        SELECT 1
        FROM pg_trigger t
        JOIN pg_class c ON c.oid = t.tgrelid
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = 'ppiq_meta'
          AND c.relname = 'customer_assessment_versions'
          AND t.tgname = 'trg_customer_assessment_version_immutable'
          AND NOT t.tgisinternal
    ) THEN
        RAISE EXCEPTION
            'PPIQ_833_POSTCONDITION_TRIGGER_MISSING: trg_customer_assessment_version_immutable is absent.';
    END IF;

    RAISE NOTICE 'PPIQ_833_POSTCONDITIONS_PROVEN';
END
$$;

COMMIT;

\qecho PPIQ_PSQL_EXECUTED 833_complete

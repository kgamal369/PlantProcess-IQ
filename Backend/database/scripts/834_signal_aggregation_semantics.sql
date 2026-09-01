-- =============================================================================
-- PPIQ T-210 - Canonical signal and aggregation semantics authority.
--
-- WHAT THIS SCRIPT DECLARES. A database type says how a value is stored; it
-- says nothing about what the value MEANS. A numeric column can be a sampled
-- analogue, a counter, a rate, a state code or a duration, and "average" is
-- mathematically false for most of those. This script gives every parameter a
-- governed semantic declaration and every KPI binding a governed override, and
-- it defines the ONLY resolution order consumers may use:
--
--     published KPI binding override
--     -> published parameter aggregation_kind
--     -> AG01 aggregation_semantics_undeclared
--
-- then validates the resolved or requested method against the signal kind:
--
--     incompatible -> AG02 invalid_aggregation_for_signal
--
-- There is no fallback to Average and no inference from data type.
--
-- WHERE IT LIVES. On the existing parameter and KPI-binding authorities, not in
-- a new registry: ppiq_meta.parameter_definitions carries signal semantics,
-- ppiq_meta.kpi_parameter_bindings carries the KPI override. One authority.
--
-- GRAMMAR IS PRODUCT-OWNED. The values below are the product's vocabulary
-- (SampleMean, TimeWeightedMean, ...). Customer definitions SELECT from it;
-- they never extend it and it never contains an industry noun.
--
-- IDEMPOTENT. Every statement is re-runnable against a database where it has
-- already run; the fresh-replay and idempotence gates prove it.
-- =============================================================================

-- ------------------------------------------------------------ grammar ------
CREATE TABLE IF NOT EXISTS ppiq_meta.signal_kinds (
    signal_kind     varchar(32) PRIMARY KEY,
    description     text        NOT NULL
);

CREATE TABLE IF NOT EXISTS ppiq_meta.aggregation_kinds (
    aggregation_kind varchar(32) PRIMARY KEY,
    description      text        NOT NULL,
    requires_time_basis boolean  NOT NULL DEFAULT false,
    executes_in_m2   boolean     NOT NULL DEFAULT true
);

CREATE TABLE IF NOT EXISTS ppiq_meta.sampling_bases (
    sampling_basis  varchar(32) PRIMARY KEY,
    description     text        NOT NULL
);

-- The compatibility matrix: which methods are defensible for which signal
-- kinds. This is the product's mathematical position, versioned with the
-- schema, and the thing AG02 is decided against.
CREATE TABLE IF NOT EXISTS ppiq_meta.signal_aggregation_compatibility (
    signal_kind      varchar(32) NOT NULL REFERENCES ppiq_meta.signal_kinds(signal_kind),
    aggregation_kind varchar(32) NOT NULL REFERENCES ppiq_meta.aggregation_kinds(aggregation_kind),
    requires_sampling_basis varchar(32) NULL REFERENCES ppiq_meta.sampling_bases(sampling_basis),
    PRIMARY KEY (signal_kind, aggregation_kind)
);

INSERT INTO ppiq_meta.signal_kinds (signal_kind, description) VALUES
    ('Analog',      'sampled continuous value'),
    ('State',       'discrete state code held between transitions'),
    ('Counter',     'monotonic cumulative count subject to resets'),
    ('Event',       'discrete occurrence with a timestamp'),
    ('LabSample',   'sparse measured value with a sampling timestamp'),
    ('Composition', 'fraction or proportion of a whole'),
    ('Level',       'inventory-like quantity at an instant'),
    ('Rate',        'instantaneous rate of change or flow'),
    ('Derived',     'computed from other signals; semantics declared by its derivation'),
    ('Unknown',     'no semantic declaration; every aggregation refuses')
ON CONFLICT (signal_kind) DO NOTHING;

INSERT INTO ppiq_meta.aggregation_kinds (aggregation_kind, description, requires_time_basis, executes_in_m2) VALUES
    ('SampleMean',       'arithmetic mean of samples',                       false, true),
    ('TimeWeightedMean', 'mean weighted by the time each value held',         true,  true),
    ('Integral',         'time integral of a rate',                           true,  true),
    ('Delta',            'end minus start, reset-aware for counters',         true,  true),
    ('StateDuration',    'time spent in each state',                          true,  true),
    ('Count',            'number of observations or events',                  false, true),
    ('Min',              'minimum observed value',                            false, true),
    ('Max',              'maximum observed value',                            false, true),
    ('Last',             'most recent value in the window',                   false, true),
    ('Percentile',       'value at a declared percentile; declared, not executed in M2', false, false),
    ('WeightedMean',     'mean weighted by a declared basis; declared, not executed in M2', false, false)
ON CONFLICT (aggregation_kind) DO NOTHING;

INSERT INTO ppiq_meta.sampling_bases (sampling_basis, description) VALUES
    ('FixedCadence', 'samples arrive on a fixed interval'),
    ('Irregular',    'samples arrive at irregular instants'),
    ('OnChange',     'a value is recorded only when it changes (deadband, event)'),
    ('Batch',        'a value is recorded once per batch or lot')
ON CONFLICT (sampling_basis) DO NOTHING;

INSERT INTO ppiq_meta.signal_aggregation_compatibility (signal_kind, aggregation_kind, requires_sampling_basis) VALUES
    -- Analog: SampleMean is lawful only on fixed cadence; on irregular or
    -- on-change data the defensible mean is time-weighted.
    ('Analog', 'SampleMean', 'FixedCadence'),
    ('Analog', 'TimeWeightedMean', NULL),
    ('Analog', 'Min', NULL), ('Analog', 'Max', NULL), ('Analog', 'Last', NULL), ('Analog', 'Count', NULL),
    ('Analog', 'Percentile', NULL), ('Analog', 'WeightedMean', NULL),
    -- State: duration in state is the meaning; a mean of state codes is not.
    ('State', 'StateDuration', NULL), ('State', 'Last', NULL), ('State', 'Count', NULL),
    -- Counter: the delta is the meaning; a mean of a running total is not.
    ('Counter', 'Delta', NULL), ('Counter', 'Last', NULL), ('Counter', 'Max', NULL), ('Counter', 'Count', NULL),
    -- Event: counting is the meaning.
    ('Event', 'Count', NULL), ('Event', 'Last', NULL),
    -- LabSample: sparse values; a plain mean of samples is defensible, a
    -- time-weighted one is not (there is no held value between samples).
    ('LabSample', 'SampleMean', NULL), ('LabSample', 'Min', NULL), ('LabSample', 'Max', NULL),
    ('LabSample', 'Last', NULL), ('LabSample', 'Count', NULL), ('LabSample', 'Percentile', NULL),
    -- Composition: fractions average by weighted basis; plain mean lawful on fixed cadence.
    ('Composition', 'SampleMean', 'FixedCadence'), ('Composition', 'TimeWeightedMean', NULL),
    ('Composition', 'WeightedMean', NULL), ('Composition', 'Last', NULL), ('Composition', 'Min', NULL), ('Composition', 'Max', NULL),
    -- Level: an instantaneous quantity; held-value semantics apply.
    ('Level', 'TimeWeightedMean', NULL), ('Level', 'Last', NULL), ('Level', 'Min', NULL), ('Level', 'Max', NULL), ('Level', 'Delta', NULL),
    -- Rate: integrate over time to get a quantity; time-weighted mean of the rate is lawful.
    ('Rate', 'Integral', NULL), ('Rate', 'TimeWeightedMean', NULL), ('Rate', 'Min', NULL), ('Rate', 'Max', NULL), ('Rate', 'Last', NULL),
    -- Derived: whatever its derivation declares; the declaration is the parameter's own aggregation_kind.
    ('Derived', 'SampleMean', NULL), ('Derived', 'TimeWeightedMean', NULL), ('Derived', 'Last', NULL),
    ('Derived', 'Min', NULL), ('Derived', 'Max', NULL), ('Derived', 'Count', NULL)
    -- Unknown: deliberately no rows. Nothing is lawful until someone declares.
ON CONFLICT (signal_kind, aggregation_kind) DO NOTHING;

-- ------------------------------------------------- tenancy of semantics ------
-- The parameter and binding authorities carry no tenant column of their own.
-- A semantic declaration is customer-authored, so the tenant that declared it
-- is a real fact this authority owns: nullable, so existing unscoped rows are
-- untouched, and strictly matched at resolution, so an unscoped row resolves
-- for nobody until a tenant declares it. (W1-T210-TENANT-01)
ALTER TABLE ppiq_meta.parameter_definitions  ADD COLUMN IF NOT EXISTS tenant_id uuid NULL;
ALTER TABLE ppiq_meta.kpi_parameter_bindings ADD COLUMN IF NOT EXISTS tenant_id uuid NULL;

DO $$
BEGIN
    -- The tenant authority is on the canonical path before this script. Its
    -- absence is an architecture failure, not a reason to skip the key.
    IF to_regclass('ppiq_meta.tenants') IS NULL THEN
        RAISE EXCEPTION 'T-210: ppiq_meta.tenants is absent; tenant ownership of signal semantics cannot be bound to authority';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_parameter_definitions_tenant') THEN
        ALTER TABLE ppiq_meta.parameter_definitions
            ADD CONSTRAINT fk_parameter_definitions_tenant FOREIGN KEY (tenant_id) REFERENCES ppiq_meta.tenants(id);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_kpi_parameter_bindings_tenant') THEN
        ALTER TABLE ppiq_meta.kpi_parameter_bindings
            ADD CONSTRAINT fk_kpi_parameter_bindings_tenant FOREIGN KEY (tenant_id) REFERENCES ppiq_meta.tenants(id);
    END IF;
END $$;

-- Lookup indexes for tenant-first access. Legacy uniqueness constraints are
-- deliberately untouched here: the complete tenant-first uniqueness and RLS
-- audit belongs to the security convergence task, not to T-210.
CREATE INDEX IF NOT EXISTS ix_parameter_definitions_tenant_code ON ppiq_meta.parameter_definitions (tenant_id, parameter_code);
CREATE INDEX IF NOT EXISTS ix_kpi_parameter_bindings_tenant     ON ppiq_meta.kpi_parameter_bindings (tenant_id);

-- ------------------------------------------ parameter semantic columns ------
ALTER TABLE ppiq_meta.parameter_definitions
    ADD COLUMN IF NOT EXISTS signal_kind          varchar(32) NULL,
    ADD COLUMN IF NOT EXISTS sampling_basis       varchar(32) NULL,
    ADD COLUMN IF NOT EXISTS aggregation_kind     varchar(32) NULL,
    ADD COLUMN IF NOT EXISTS interpolation_kind   varchar(32) NULL,
    ADD COLUMN IF NOT EXISTS weight_basis         varchar(32) NULL,
    ADD COLUMN IF NOT EXISTS maximum_gap_seconds  integer     NULL,
    ADD COLUMN IF NOT EXISTS counter_reset_policy varchar(32) NULL,
    ADD COLUMN IF NOT EXISTS quality_policy       varchar(32) NULL,
    ADD COLUMN IF NOT EXISTS time_basis           varchar(32) NULL,
    ADD COLUMN IF NOT EXISTS semantics_version    integer     NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS semantics_declared_at_utc timestamptz NULL;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_parameter_definitions_signal_kind') THEN
        ALTER TABLE ppiq_meta.parameter_definitions
            ADD CONSTRAINT fk_parameter_definitions_signal_kind
            FOREIGN KEY (signal_kind) REFERENCES ppiq_meta.signal_kinds(signal_kind);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_parameter_definitions_aggregation_kind') THEN
        ALTER TABLE ppiq_meta.parameter_definitions
            ADD CONSTRAINT fk_parameter_definitions_aggregation_kind
            FOREIGN KEY (aggregation_kind) REFERENCES ppiq_meta.aggregation_kinds(aggregation_kind);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_parameter_definitions_sampling_basis') THEN
        ALTER TABLE ppiq_meta.parameter_definitions
            ADD CONSTRAINT fk_parameter_definitions_sampling_basis
            FOREIGN KEY (sampling_basis) REFERENCES ppiq_meta.sampling_bases(sampling_basis);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_parameter_definitions_interpolation') THEN
        ALTER TABLE ppiq_meta.parameter_definitions
            ADD CONSTRAINT ck_parameter_definitions_interpolation
            CHECK (interpolation_kind IS NULL OR interpolation_kind IN ('None', 'HoldLast', 'Linear', 'Step'));
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_parameter_definitions_weight_basis') THEN
        ALTER TABLE ppiq_meta.parameter_definitions
            ADD CONSTRAINT ck_parameter_definitions_weight_basis
            CHECK (weight_basis IS NULL OR weight_basis IN ('Time', 'Sample', 'Quantity', 'Declared'));
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_parameter_definitions_counter_reset') THEN
        ALTER TABLE ppiq_meta.parameter_definitions
            ADD CONSTRAINT ck_parameter_definitions_counter_reset
            CHECK (counter_reset_policy IS NULL OR counter_reset_policy IN ('None', 'ResetToZero', 'Rollover', 'RefuseOnReset'));
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_parameter_definitions_quality_policy') THEN
        ALTER TABLE ppiq_meta.parameter_definitions
            ADD CONSTRAINT ck_parameter_definitions_quality_policy
            CHECK (quality_policy IS NULL OR quality_policy IN ('GoodOnly', 'GoodAndUncertain', 'All', 'RefuseOnBad'));
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_parameter_definitions_time_basis') THEN
        ALTER TABLE ppiq_meta.parameter_definitions
            ADD CONSTRAINT ck_parameter_definitions_time_basis
            CHECK (time_basis IS NULL OR time_basis IN ('ObservationTime', 'ArrivalTime', 'ProcessTime'));
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_parameter_definitions_maximum_gap') THEN
        ALTER TABLE ppiq_meta.parameter_definitions
            ADD CONSTRAINT ck_parameter_definitions_maximum_gap
            CHECK (maximum_gap_seconds IS NULL OR maximum_gap_seconds > 0);
    END IF;
END $$;

-- ---------------------------------------------- KPI binding overrides ------
ALTER TABLE ppiq_meta.kpi_parameter_bindings
    ADD COLUMN IF NOT EXISTS aggregation_kind_override varchar(32) NULL,
    ADD COLUMN IF NOT EXISTS weight_basis_override     varchar(32) NULL,
    ADD COLUMN IF NOT EXISTS window_semantics_override jsonb       NULL;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_kpi_parameter_bindings_aggregation_override') THEN
        ALTER TABLE ppiq_meta.kpi_parameter_bindings
            ADD CONSTRAINT fk_kpi_parameter_bindings_aggregation_override
            FOREIGN KEY (aggregation_kind_override) REFERENCES ppiq_meta.aggregation_kinds(aggregation_kind);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_kpi_parameter_bindings_weight_override') THEN
        ALTER TABLE ppiq_meta.kpi_parameter_bindings
            ADD CONSTRAINT ck_kpi_parameter_bindings_weight_override
            CHECK (weight_basis_override IS NULL OR weight_basis_override IN ('Time', 'Sample', 'Quantity', 'Declared'));
    END IF;
END $$;

-- ------------------------------------------------ governed history ------
-- A semantic change is a semantic change. The prior declaration is appended
-- here before the row changes, so an analysis that ran under version N can
-- always be explained against version N. Append-only by trigger.
CREATE TABLE IF NOT EXISTS ppiq_meta.parameter_signal_semantics_history (
    id                       uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    parameter_definition_id  uuid        NOT NULL,
    tenant_id                uuid        NULL,
    semantics_version        integer     NOT NULL,
    signal_kind              varchar(32) NULL,
    sampling_basis           varchar(32) NULL,
    aggregation_kind         varchar(32) NULL,
    interpolation_kind       varchar(32) NULL,
    weight_basis             varchar(32) NULL,
    maximum_gap_seconds      integer     NULL,
    counter_reset_policy     varchar(32) NULL,
    quality_policy           varchar(32) NULL,
    time_basis               varchar(32) NULL,
    effective_from_utc       timestamptz NULL,
    superseded_at_utc        timestamptz NOT NULL DEFAULT now(),
    UNIQUE (parameter_definition_id, semantics_version)
);

CREATE OR REPLACE FUNCTION ppiq_meta.parameter_signal_semantics_version()
RETURNS trigger
LANGUAGE plpgsql
AS $fn$
DECLARE
    changed boolean;
BEGIN
    -- Ownership is claimed once and never transferred through this path.
    IF OLD.tenant_id IS NOT NULL AND NEW.tenant_id IS DISTINCT FROM OLD.tenant_id THEN
        RAISE EXCEPTION 'T-210: parameter % is owned by tenant % and ownership is immutable', OLD.id, OLD.tenant_id
            USING ERRCODE = '23514';
    END IF;

    changed :=
        NEW.signal_kind          IS DISTINCT FROM OLD.signal_kind OR
        NEW.sampling_basis       IS DISTINCT FROM OLD.sampling_basis OR
        NEW.aggregation_kind     IS DISTINCT FROM OLD.aggregation_kind OR
        NEW.interpolation_kind   IS DISTINCT FROM OLD.interpolation_kind OR
        NEW.weight_basis         IS DISTINCT FROM OLD.weight_basis OR
        NEW.maximum_gap_seconds  IS DISTINCT FROM OLD.maximum_gap_seconds OR
        NEW.counter_reset_policy IS DISTINCT FROM OLD.counter_reset_policy OR
        NEW.quality_policy       IS DISTINCT FROM OLD.quality_policy OR
        NEW.time_basis           IS DISTINCT FROM OLD.time_basis;

    -- IDEMPOTENT REDECLARATION. Identical semantics leave the version alone:
    -- re-saving what is already true is not a change and must not look like one.
    IF NOT changed THEN
        NEW.semantics_version := OLD.semantics_version;
        NEW.semantics_declared_at_utc := OLD.semantics_declared_at_utc;
        RETURN NEW;
    END IF;

    IF OLD.semantics_version > 0 THEN
        INSERT INTO ppiq_meta.parameter_signal_semantics_history
            (parameter_definition_id, tenant_id, semantics_version, signal_kind, sampling_basis,
             aggregation_kind, interpolation_kind, weight_basis, maximum_gap_seconds,
             counter_reset_policy, quality_policy, time_basis, effective_from_utc)
        VALUES
            (OLD.id, OLD.tenant_id, OLD.semantics_version, OLD.signal_kind, OLD.sampling_basis,
             OLD.aggregation_kind, OLD.interpolation_kind, OLD.weight_basis, OLD.maximum_gap_seconds,
             OLD.counter_reset_policy, OLD.quality_policy, OLD.time_basis, OLD.semantics_declared_at_utc)
        ON CONFLICT (parameter_definition_id, semantics_version) DO NOTHING;
    END IF;

    NEW.semantics_version := OLD.semantics_version + 1;
    NEW.semantics_declared_at_utc := now();
    RETURN NEW;
END;
$fn$;

DROP TRIGGER IF EXISTS trg_parameter_signal_semantics_version ON ppiq_meta.parameter_definitions;
CREATE TRIGGER trg_parameter_signal_semantics_version
    BEFORE UPDATE ON ppiq_meta.parameter_definitions
    FOR EACH ROW EXECUTE FUNCTION ppiq_meta.parameter_signal_semantics_version();

-- A declared default must itself be lawful for the declared signal kind, and
-- SampleMean on an Analog signal requires a fixed cadence to be declared.
CREATE OR REPLACE FUNCTION ppiq_meta.parameter_signal_semantics_validate()
RETURNS trigger
LANGUAGE plpgsql
AS $fn$
DECLARE
    required_basis varchar(32);
BEGIN
    IF NEW.aggregation_kind IS NULL THEN
        RETURN NEW;
    END IF;

    IF NEW.signal_kind IS NULL THEN
        RAISE EXCEPTION 'AG02: aggregation % declared without a signal kind', NEW.aggregation_kind
            USING ERRCODE = '23514';
    END IF;

    SELECT c.requires_sampling_basis INTO required_basis
      FROM ppiq_meta.signal_aggregation_compatibility c
     WHERE c.signal_kind = NEW.signal_kind AND c.aggregation_kind = NEW.aggregation_kind;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'AG02: aggregation % is not defensible for signal kind %', NEW.aggregation_kind, NEW.signal_kind
            USING ERRCODE = '23514';
    END IF;

    IF required_basis IS NOT NULL AND NEW.sampling_basis IS DISTINCT FROM required_basis THEN
        RAISE EXCEPTION 'AG02: aggregation % on signal kind % requires sampling basis % (declared: %)',
            NEW.aggregation_kind, NEW.signal_kind, required_basis, COALESCE(NEW.sampling_basis, 'none')
            USING ERRCODE = '23514';
    END IF;

    RETURN NEW;
END;
$fn$;

DROP TRIGGER IF EXISTS trg_parameter_signal_semantics_validate ON ppiq_meta.parameter_definitions;
CREATE TRIGGER trg_parameter_signal_semantics_validate
    BEFORE INSERT OR UPDATE ON ppiq_meta.parameter_definitions
    FOR EACH ROW EXECUTE FUNCTION ppiq_meta.parameter_signal_semantics_validate();

-- ---------------------------------------------- the resolver, in SQL ------
-- One function, one order, consumed by every C# caller. Returns exactly one
-- row. refusal_code is NULL on success, 'AG01' when nothing is declared,
-- 'AG02' when the resolved or requested method is not defensible for the
-- signal. resolution_source names where the answer came from so a caller can
-- show WHY it got that method.
CREATE OR REPLACE FUNCTION ppiq_meta.resolve_aggregation_semantics(
    p_tenant_id      uuid,
    p_parameter_id   uuid,
    p_binding_id     uuid,
    p_requested_kind varchar
)
RETURNS TABLE (
    resolved_kind      varchar,
    resolution_source  varchar,
    refusal_code       varchar,
    refusal_message    text,
    signal_kind        varchar,
    sampling_basis     varchar,
    weight_basis       varchar,
    semantics_version  integer
)
LANGUAGE plpgsql
STABLE
AS $fn$
DECLARE
    p       record;
    b       record;
    kind    varchar;
    source  varchar;
    weight  varchar;
    req_basis varchar;
BEGIN
    SELECT pd.id, pd.tenant_id, pd.signal_kind, pd.sampling_basis, pd.aggregation_kind,
           pd.weight_basis, pd.semantics_version
      INTO p
      FROM ppiq_meta.parameter_definitions pd
     WHERE pd.id = p_parameter_id AND pd.tenant_id = p_tenant_id;

    IF NOT FOUND THEN
        RETURN QUERY SELECT NULL::varchar, 'none'::varchar, 'AG01'::varchar,
            'aggregation_semantics_undeclared: no parameter with that identity in this tenant'::text,
            NULL::varchar, NULL::varchar, NULL::varchar, 0;
        RETURN;
    END IF;

    kind := NULL; source := 'none'; weight := p.weight_basis;

    IF p_binding_id IS NOT NULL THEN
        SELECT kb.aggregation_kind_override, kb.weight_basis_override
          INTO b
          FROM ppiq_meta.kpi_parameter_bindings kb
         WHERE kb.id = p_binding_id AND kb.tenant_id = p_tenant_id;

        IF FOUND AND b.aggregation_kind_override IS NOT NULL THEN
            kind := b.aggregation_kind_override; source := 'kpi_binding';
            weight := COALESCE(b.weight_basis_override, weight);
        END IF;
    END IF;

    IF kind IS NULL AND p.aggregation_kind IS NOT NULL THEN
        kind := p.aggregation_kind; source := 'parameter';
    END IF;

    -- An explicit request wins for validation purposes but never manufactures
    -- a declaration: if nothing is declared and nothing is requested, AG01.
    IF p_requested_kind IS NOT NULL THEN
        kind := p_requested_kind; source := 'requested';
    END IF;

    IF kind IS NULL THEN
        RETURN QUERY SELECT NULL::varchar, 'none'::varchar, 'AG01'::varchar,
            'aggregation_semantics_undeclared: neither the parameter nor a KPI binding declares an aggregation, and none was requested'::text,
            p.signal_kind, p.sampling_basis, weight, p.semantics_version;
        RETURN;
    END IF;

    IF p.signal_kind IS NULL OR p.signal_kind = 'Unknown' THEN
        RETURN QUERY SELECT NULL::varchar, source, 'AG01'::varchar,
            ('aggregation_semantics_undeclared: parameter has no signal kind, so ' || kind || ' cannot be validated')::text,
            p.signal_kind, p.sampling_basis, weight, p.semantics_version;
        RETURN;
    END IF;

    SELECT c.requires_sampling_basis INTO req_basis
      FROM ppiq_meta.signal_aggregation_compatibility c
     WHERE c.signal_kind = p.signal_kind AND c.aggregation_kind = kind;

    IF NOT FOUND THEN
        RETURN QUERY SELECT NULL::varchar, source, 'AG02'::varchar,
            ('invalid_aggregation_for_signal: ' || kind || ' is not defensible for signal kind ' || p.signal_kind)::text,
            p.signal_kind, p.sampling_basis, weight, p.semantics_version;
        RETURN;
    END IF;

    IF req_basis IS NOT NULL AND p.sampling_basis IS DISTINCT FROM req_basis THEN
        RETURN QUERY SELECT NULL::varchar, source, 'AG02'::varchar,
            ('invalid_aggregation_for_signal: ' || kind || ' on ' || p.signal_kind || ' requires sampling basis ' ||
             req_basis || ' but the parameter declares ' || COALESCE(p.sampling_basis, 'none'))::text,
            p.signal_kind, p.sampling_basis, weight, p.semantics_version;
        RETURN;
    END IF;

    RETURN QUERY SELECT kind, source, NULL::varchar, NULL::text,
        p.signal_kind, p.sampling_basis, weight, p.semantics_version;
END;
$fn$;

COMMENT ON FUNCTION ppiq_meta.resolve_aggregation_semantics(uuid, uuid, uuid, varchar) IS
    'PPIQ T-210. Single resolution authority: KPI binding override -> parameter aggregation_kind -> AG01; '
    'then compatibility with the signal kind -> AG02. No default Average, no inference from storage type.';

-- =============================================================================
-- 320_p3_business_key_reconciliation.sql            (PPIQ-301, idempotent)
-- Business-key NORMALIZATION RULES + conflict-rejecting reconciliation (B1.3 / G2).
-- NOTE: the rules table is ppiq_business_key_rules. It is NOT the business-key
-- dictionary created by 310 (ppiq_business_key_definitions, key_code/entity_scope),
-- which 312 and 313 read. The two are unrelated; they collided on one name and
-- CREATE TABLE IF NOT EXISTS silently hid it until the INSERT below failed.
-- Normalizes 'C-0044170' == '44170', rejects one-key->two-units with a typed
-- 'AmbiguousJoinKey' (transaction rolls back), admin-editable rule table.
-- =============================================================================

CREATE TABLE IF NOT EXISTS public.ppiq_business_key_rules (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id           uuid NULL,
    key_type            text NOT NULL,
    description         text NULL,
    strip_alpha_prefix  boolean NOT NULL DEFAULT true,
    strip_leading_zeros boolean NOT NULL DEFAULT true,
    case_insensitive    boolean NOT NULL DEFAULT true,
    is_active           boolean NOT NULL DEFAULT true,
    created_at_utc      timestamptz NOT NULL DEFAULT now()
);

-- Default rule for the coil/heat business key family (admin can add/disable rows).
INSERT INTO public.ppiq_business_key_rules (key_type, description)
SELECT 'coil', 'Default coil/heat/slab key normalization (strip alpha prefix + leading zeros).'
WHERE NOT EXISTS (
    SELECT 1 FROM public.ppiq_business_key_rules WHERE key_type = 'coil' AND tenant_id IS NULL
);

-- Deterministic, definition-driven normalizer. Marked STABLE (reads the rule row).
CREATE OR REPLACE FUNCTION public.ppiq_normalize_business_key(p_key_type text, p_raw text)
RETURNS text
LANGUAGE plpgsql
STABLE
AS $fn$
DECLARE
    v_rule public.ppiq_business_key_rules;
    v      text;
BEGIN
    IF p_raw IS NULL THEN RETURN NULL; END IF;
    v := trim(p_raw);

    SELECT * INTO v_rule
    FROM public.ppiq_business_key_rules
    WHERE key_type = p_key_type AND is_active
    ORDER BY tenant_id NULLS LAST
    LIMIT 1;

    IF COALESCE(v_rule.case_insensitive, true) THEN v := upper(v); END IF;
    v := regexp_replace(v, '[^A-Za-z0-9]', '', 'g');                 -- drop separators
    IF COALESCE(v_rule.strip_alpha_prefix, true) THEN
        v := regexp_replace(v, '^[A-Za-z]+', '');                    -- drop leading C/H/S/COIL
    END IF;
    IF COALESCE(v_rule.strip_leading_zeros, true) THEN
        v := regexp_replace(v, '^0+', '');                           -- 0044170 -> 44170
    END IF;
    IF v = '' THEN v := '0'; END IF;
    RETURN v;
END;
$fn$;

-- Maintain a normalized projection column on the alias table.
ALTER TABLE IF EXISTS public.material_aliases
    ADD COLUMN IF NOT EXISTS normalized_alias_code text;

DO $do$
BEGIN
    IF to_regclass('public.material_aliases') IS NOT NULL THEN
        UPDATE public.material_aliases
           SET normalized_alias_code = public.ppiq_normalize_business_key('coil', alias_code)
         WHERE normalized_alias_code IS NULL;
    END IF;
END;
$do$;

CREATE INDEX IF NOT EXISTS ix_material_aliases_normalized
    ON public.material_aliases (normalized_alias_code);

-- BEFORE INSERT/UPDATE guard: the SAME normalized key must not map to two
-- different material units (across any source). Raises a typed error -> rollback.
CREATE OR REPLACE FUNCTION public.ppiq_material_alias_conflict_guard()
RETURNS trigger
LANGUAGE plpgsql
AS $tg$
DECLARE
    v_norm  text;
    v_other uuid;
BEGIN
    v_norm := public.ppiq_normalize_business_key('coil', NEW.alias_code);
    NEW.normalized_alias_code := v_norm;

    SELECT a.material_unit_id INTO v_other
    FROM public.material_aliases a
    WHERE a.normalized_alias_code = v_norm
      AND a.material_unit_id <> NEW.material_unit_id
      AND COALESCE(a.is_deleted, false) = false
    LIMIT 1;

    IF v_other IS NOT NULL THEN
        RAISE EXCEPTION
            'AmbiguousJoinKey: business key % (normalized %) already maps to material unit % - refusing to silently merge two entities.',
            NEW.alias_code, v_norm, v_other
            USING ERRCODE = '23P01';   -- exclusion_violation
    END IF;

    RETURN NEW;
END;
$tg$;

DO $do$
BEGIN
    IF to_regclass('public.material_aliases') IS NOT NULL THEN
        DROP TRIGGER IF EXISTS trg_material_alias_conflict_guard ON public.material_aliases;
        CREATE TRIGGER trg_material_alias_conflict_guard
            BEFORE INSERT OR UPDATE ON public.material_aliases
            FOR EACH ROW EXECUTE FUNCTION public.ppiq_material_alias_conflict_guard();
    END IF;
END;
$do$;

-- Resolve a raw business key to exactly ONE material unit, or raise AmbiguousJoinKey.
CREATE OR REPLACE FUNCTION public.ppiq_resolve_material_by_business_key(p_raw text)
RETURNS uuid
LANGUAGE plpgsql
STABLE
AS $fn$
DECLARE
    v_norm text;
    v_ids  uuid[];
BEGIN
    v_norm := public.ppiq_normalize_business_key('coil', p_raw);
    SELECT array_agg(DISTINCT a.material_unit_id) INTO v_ids
    FROM public.material_aliases a
    WHERE a.normalized_alias_code = v_norm
      AND COALESCE(a.is_deleted, false) = false;

    IF v_ids IS NULL OR array_length(v_ids, 1) = 0 THEN RETURN NULL; END IF;
    IF array_length(v_ids, 1) > 1 THEN
        RAISE EXCEPTION 'AmbiguousJoinKey: business key % resolves to % distinct material units.',
            p_raw, array_length(v_ids, 1) USING ERRCODE = '23P01';
    END IF;
    RETURN v_ids[1];
END;
$fn$;
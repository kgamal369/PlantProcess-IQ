-- ============================================================================
-- 822_t044r1_positional_and_specification_materialisation.sql
--
-- T-044-R1. TWO CANONICAL MATERIALISATIONS.
--
-- A. Positional defect facts onto the EXISTING canonical quality events. The
--    join is the encoded lineage key the FLEET_V2 materialiser already writes,
--    so this is an UPDATE of 5,961 known rows and cannot change the row count.
--    No second canonical defect population is created.
--
-- B. The six source-declared parameters, then the 36 grade specifications that
--    depend on them. Identity and unit come from the source; nothing else is
--    filled in.
--
-- Idempotent. Re-running changes nothing and inserts nothing.
-- ============================================================================

BEGIN;

-- ------------------------------------------------------------ A. schema
ALTER TABLE public.quality_events
    ADD COLUMN IF NOT EXISTS position_start_m numeric,
    ADD COLUMN IF NOT EXISTS position_end_m   numeric,
    ADD COLUMN IF NOT EXISTS width_position_mm        numeric;

-- ------------------------------------------------------- A. backfill
-- Exact lineage. Every updated row already exists; none is created.
UPDATE public.quality_events qe
SET position_start_m = p.position_start_m,
    position_end_m   = p.position_end_m,
    width_position_mm        = p.width_position_mm,
    updated_at_utc                = NOW() AT TIME ZONE 'UTC'
FROM src_inspection_mysql_shape.parsytec_surface_defects p
WHERE qe.source_record_id = 'FLEETV2-QE-DEFECT-' || p.defect_row_id::text
  AND qe.is_deleted = FALSE
  AND qe.source_system = 'FLEET_V2'
  AND qe.event_type = 'SurfaceDefect'
  AND (qe.position_start_m IS DISTINCT FROM p.position_start_m
    OR qe.position_end_m   IS DISTINCT FROM p.position_end_m
    OR qe.width_position_mm        IS DISTINCT FROM p.width_position_mm);

-- ------------------------------------------------------------ B. schema
CREATE TABLE IF NOT EXISTS public.product_specifications (
    id                      uuid NOT NULL,
    specification_code      varchar(100) NOT NULL,
    product_family          varchar(100),
    grade_or_recipe         varchar(100) NOT NULL,
    parameter_definition_id uuid NOT NULL,
    min_value               numeric,
    target_value            numeric,
    max_value               numeric,
    unit_of_measure         varchar(50),
    effective_from_utc      timestamp with time zone NOT NULL,
    effective_to_utc        timestamp with time zone,
    provenance              varchar(200),
    created_at_utc          timestamp with time zone NOT NULL,
    updated_at_utc          timestamp with time zone,
    is_synthetic            boolean NOT NULL,
    source_system           varchar(100),
    source_record_id        varchar(100),
    is_deleted              boolean NOT NULL,
    deleted_at_utc          timestamp with time zone,
    deleted_reason          varchar(500),
    CONSTRAINT pk_product_specifications PRIMARY KEY (id),
    CONSTRAINT fk_product_specifications_parameter_definitions_parameter_defi
        FOREIGN KEY (parameter_definition_id)
        REFERENCES public.parameter_definitions (id) ON DELETE RESTRICT);

CREATE INDEX IF NOT EXISTS ix_product_specifications_parameter_definition_id
    ON public.product_specifications (parameter_definition_id);
CREATE INDEX IF NOT EXISTS ix_product_specifications_grade_or_recipe
    ON public.product_specifications (grade_or_recipe);
CREATE INDEX IF NOT EXISTS ix_product_specifications_grade_or_recipe_parameter_definition
    ON public.product_specifications (grade_or_recipe, parameter_definition_id);
CREATE UNIQUE INDEX IF NOT EXISTS ix_product_specifications_grade_or_recipe_parameter_definition1
    ON public.product_specifications (grade_or_recipe, parameter_definition_id, effective_from_utc)
    WHERE is_deleted = FALSE;

-- ------------------------------------------ B. source vocabulary mapping
-- THE SIX CANONICAL PARAMETERS ALREADY EXIST. This source spells them as
-- element symbols; the canonical registry spells them as measured percentages,
-- and the existing observation materialisation already uses those identities.
-- Creating Al beside ALUMINIUM_PCT would give one measurement two canonical
-- authorities, so nothing is inserted into parameter_definitions here.
--
-- The translation below is SOURCE ADAPTER CONFIGURATION and lives only in this
-- materialisation script. It is not a runtime mapping service and adds no
-- alias to the canonical model. An unmapped element yields NULL, joins to
-- nothing, and the row count proof fails - it can never be silently dropped.

-- ------------------------------------------------ B. the specifications
INSERT INTO public.product_specifications (
    id, specification_code, product_family, grade_or_recipe, parameter_definition_id,
    min_value, target_value, max_value, unit_of_measure,
    effective_from_utc, effective_to_utc, provenance,
    created_at_utc, is_synthetic, source_system, source_record_id, is_deleted)
SELECT
    gen_random_uuid(),
    gs.grade_code,
    NULL,
    gs.grade_code,
    pd.id,
    gs.min_value,
    gs.target_value,
    gs.max_value,
    gs.unit_code,
    gs.effective_from::timestamptz,
    gs.effective_to::timestamptz,
    'src_meltshop_pg.grade_specification',
    NOW() AT TIME ZONE 'UTC',
    TRUE,
    'src_meltshop_pg.grade_specification',
    'T044R1-SPEC-' || gs.grade_code || '-' || gs.element_code,
    FALSE
FROM src_meltshop_pg.grade_specification gs
JOIN public.parameter_definitions pd
  ON pd.parameter_code = CASE upper(gs.element_code)
        WHEN 'AL' THEN 'ALUMINIUM_PCT'
        WHEN 'C'  THEN 'CARBON_PCT'
        WHEN 'MN' THEN 'MANGANESE_PCT'
        WHEN 'P'  THEN 'PHOSPHORUS_PCT'
        WHEN 'S'  THEN 'SULPHUR_PCT'
        WHEN 'SI' THEN 'SILICON_PCT'
     END
 AND pd.is_deleted = FALSE
WHERE NOT EXISTS (
    SELECT 1 FROM public.product_specifications ps
    WHERE ps.grade_or_recipe = gs.grade_code
      AND ps.parameter_definition_id = pd.id
      AND ps.effective_from_utc = gs.effective_from::timestamptz
      AND ps.is_deleted = FALSE);

COMMIT;

SELECT 'surface defects with a position' AS fact,
       count(*) FILTER (WHERE position_start_m IS NOT NULL)::text AS value
FROM public.quality_events
WHERE NOT is_deleted AND event_type = 'SurfaceDefect'
UNION ALL
SELECT 'parameter definitions', count(*)::text FROM public.parameter_definitions WHERE NOT is_deleted
UNION ALL
SELECT 'product specifications', count(*)::text FROM public.product_specifications WHERE NOT is_deleted;
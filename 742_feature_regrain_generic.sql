-- 742_feature_regrain_generic.sql
--
-- WALL 7 (named 20-Jul 22:15, probe2): the readiness gate blocks on Required
-- Completeness - 12,149 coils carry features vs 38,351 coils carrying outcomes
-- (31.7% < 85%). Meanwhile 40,146 feature rows sit at grain='generic' with a
-- material_unit_id pointing at real coil/slab/heat units - written by an older
-- feature writer that never classified grain. The coil-grain loader
-- (WHERE grain=@g) is structurally blind to them.
--
-- FIX (no threshold touched, no data invented): re-classify those rows by their
-- unit's actual type, fill coil_id/slab_id/heat_id (heat via the 740 lineage),
-- and normalise effective_sample_key to the unit's material_code so the
-- in-memory join matches. Idempotent: only rows still at grain='generic' with a
-- resolvable unit are touched; re-running is a no-op.
--
-- NOTE (recorded debt): rows re-grained here whose source_system is
-- 'PPIQ-ML-Refresh' would be re-created as 'generic' by a future refresh if any
-- unit types still fall through the function's CASE. The permanent home for
-- this classification is the refresh function / M2-36 job-config policy; until
-- then re-run this migration after any manual full refresh (it is cheap and
-- idempotent), and it is folded into the rebuild path by M1-31.

UPDATE public.ml_feature_values fv
SET grain = CASE
        WHEN lower(COALESCE(mu.material_unit_type, '')) LIKE '%coil%' THEN 'coil'
        WHEN lower(COALESCE(mu.material_unit_type, '')) LIKE '%slab%' THEN 'slab'
        WHEN lower(COALESCE(mu.material_unit_type, '')) LIKE '%heat%' THEN 'heat'
        ELSE fv.grain
    END,
    coil_id = CASE WHEN lower(COALESCE(mu.material_unit_type, '')) LIKE '%coil%' THEN mu.material_code ELSE fv.coil_id END,
    slab_id = CASE WHEN lower(COALESCE(mu.material_unit_type, '')) LIKE '%slab%' THEN mu.material_code ELSE fv.slab_id END,
    heat_id = CASE
        WHEN lower(COALESCE(mu.material_unit_type, '')) LIKE '%heat%' THEN mu.material_code
        ELSE COALESCE(fv.heat_id, lin.heat_code)
    END,
    effective_sample_key = COALESCE(NULLIF(fv.effective_sample_key, ''), mu.material_code)
FROM public.material_units mu
LEFT JOIN public.ppiq_ml_unit_heat_lineage lin ON lin.unit_id = mu.id
WHERE fv.material_unit_id = mu.id
  AND fv.grain = 'generic'
  AND mu.is_deleted = false
  AND lower(COALESCE(mu.material_unit_type, '')) SIMILAR TO '%(coil|slab|heat)%';

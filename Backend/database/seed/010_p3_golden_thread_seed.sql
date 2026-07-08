-- =============================================================================
-- seed/010_p3_golden_thread_seed.sql                (PPIQ-301/302/303 fixtures)
-- Deterministic golden-thread + transition entities. Idempotent. The canonical
-- walk-layer block is wrapped so an absent/odd canonical schema can never abort
-- the core (material_units / genealogy_edges / material_aliases) seed.
-- =============================================================================
DO $seed$
DECLARE
    v_site  uuid;
    v_h1    uuid := '3a000000-0000-0000-0000-000000003361';  -- Heat H-3361
    v_h2    uuid := '3a000000-0000-0000-0000-000000003362';  -- Heat H-3362
    v_slab  uuid := '3a000000-0000-0000-0000-000000044100';  -- Slab S-0044170
    v_coilT uuid := '3a000000-0000-0000-0000-000000044170';  -- Coil C-0044170 (transition)
    v_coilN uuid := '3a000000-0000-0000-0000-000000044171';  -- Coil C-0044171 (normal)
BEGIN
    IF to_regclass('public.material_units') IS NULL THEN
        RAISE NOTICE 'PPIQ P3 seed skipped: public.material_units not present.';
        RETURN;
    END IF;

    SELECT id INTO v_site FROM public.sites ORDER BY created_at_utc LIMIT 1;
    IF v_site IS NULL THEN
        RAISE NOTICE 'PPIQ P3 seed skipped: no site row to attach material units to.';
        RETURN;
    END IF;

    INSERT INTO public.material_units
        (id, created_at_utc, is_synthetic, source_system, is_deleted,
         material_code, material_unit_type, product_family, grade_or_recipe, site_id)
    VALUES
        (v_h1,   now(), true, 'PPIQ_P3_SEED', false, 'H-3361',    'Heat', 'FlatSteel', 'S235JR', v_site),
        (v_h2,   now(), true, 'PPIQ_P3_SEED', false, 'H-3362',    'Heat', 'FlatSteel', 'S235JR', v_site),
        (v_slab, now(), true, 'PPIQ_P3_SEED', false, 'S-0044170', 'Slab', 'FlatSteel', 'S235JR', v_site),
        (v_coilT,now(), true, 'PPIQ_P3_SEED', false, 'C-0044170', 'Coil', 'FlatSteel', 'S235JR', v_site),
        (v_coilN,now(), true, 'PPIQ_P3_SEED', false, 'C-0044171', 'Coil', 'FlatSteel', 'S235JR', v_site)
    ON CONFLICT (id) DO NOTHING;

    INSERT INTO public.genealogy_edges
        (id, created_at_utc, is_synthetic, source_system, is_deleted,
         parent_material_unit_id, child_material_unit_id, relationship_type,
         contribution_weight, is_transition)
    VALUES
        ('3a000000-0000-0000-0000-0000000ed001', now(), true, 'PPIQ_P3_SEED', false, v_h1,   v_slab,  'HeatToSlab', 1.00, false),
        ('3a000000-0000-0000-0000-0000000ed002', now(), true, 'PPIQ_P3_SEED', false, v_slab, v_coilT, 'SlabToCoil', 1.00, false),
        ('3a000000-0000-0000-0000-0000000ed003', now(), true, 'PPIQ_P3_SEED', false, v_h1,   v_coilT, 'HeatToCoil', 0.70, true),
        ('3a000000-0000-0000-0000-0000000ed004', now(), true, 'PPIQ_P3_SEED', false, v_h2,   v_coilT, 'HeatToCoil', 0.30, true),
        ('3a000000-0000-0000-0000-0000000ed005', now(), true, 'PPIQ_P3_SEED', false, v_h1,   v_coilN, 'HeatToCoil', 1.00, false)
    ON CONFLICT (id) DO NOTHING;

    INSERT INTO public.material_aliases
        (id, created_at_utc, is_synthetic, source_system, is_deleted,
         material_unit_id, alias_code, alias_type)
    VALUES
        ('3a000000-0000-0000-0000-00000a110001', now(), true, 'HSM_ORACLE',     false, v_coilT, 'C-0044170', 'SourceSystemId'),
        ('3a000000-0000-0000-0000-00000a110002', now(), true, 'PARSYTEC_MYSQL', false, v_coilT, '44170',     'SourceSystemId')
    ON CONFLICT (id) DO NOTHING;

    -- Canonical walk layer (302) - isolated so a shape mismatch only NOTICEs.
    BEGIN
        IF to_regclass('public.canonical_material_units') IS NOT NULL
           AND to_regclass('public.canonical_genealogy_edges') IS NOT NULL
           AND EXISTS (SELECT 1 FROM information_schema.columns
                       WHERE table_schema='public' AND table_name='canonical_material_units'
                         AND column_name='tenant_id') THEN

            INSERT INTO public.canonical_material_units (id, tenant_id, material_key, material_type)
            SELECT v.id, t.id, v.k, v.ty
            FROM (VALUES
                    (v_h1,   'H-3361',    'Heat'),
                    (v_h2,   'H-3362',    'Heat'),
                    (v_slab, 'S-0044170', 'Slab'),
                    (v_coilT,'C-0044170', 'Coil'),
                    (v_coilN,'C-0044171', 'Coil')
                 ) AS v(id, k, ty)
            CROSS JOIN LATERAL (SELECT id FROM public.ppiq_tenants ORDER BY created_at_utc LIMIT 1) AS t
            ON CONFLICT (id) DO NOTHING;

            INSERT INTO public.canonical_genealogy_edges
                (id, tenant_id, parent_material_unit_id, child_material_unit_id)
            SELECT e.id, t.id, e.p, e.c
            FROM (VALUES
                    ('3a000000-0000-0000-0000-00000ce0001'::uuid, v_h1,   v_slab),
                    ('3a000000-0000-0000-0000-00000ce0002'::uuid, v_slab, v_coilT),
                    ('3a000000-0000-0000-0000-00000ce0003'::uuid, v_h2,   v_coilT),
                    ('3a000000-0000-0000-0000-00000ce0004'::uuid, v_h1,   v_coilN)
                 ) AS e(id, p, c)
            CROSS JOIN LATERAL (SELECT id FROM public.ppiq_tenants ORDER BY created_at_utc LIMIT 1) AS t
            ON CONFLICT (id) DO NOTHING;

            RAISE NOTICE 'PPIQ P3 seed: canonical walk layer seeded.';
        ELSE
            RAISE NOTICE 'PPIQ P3 seed: canonical layer absent/odd - 302 walk test self-skips.';
        END IF;
    EXCEPTION WHEN others THEN
        RAISE NOTICE 'PPIQ P3 seed: canonical walk seed skipped (%).', SQLERRM;
    END;

    RAISE NOTICE 'PPIQ P3 seed applied (H-3361/H-3362/C-0044170/C-0044171).';
END;
$seed$;
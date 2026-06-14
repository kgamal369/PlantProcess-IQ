DO $seed$
DECLARE
    v_site  uuid;
    v_h1    uuid := '3a000000-0000-0000-0000-000000003361';
    v_h2    uuid := '3a000000-0000-0000-0000-000000003362';
    v_coilT uuid := '3a000000-0000-0000-0000-000000044170';
    v_coilN uuid := '3a000000-0000-0000-0000-000000044171';
BEGIN
    IF to_regclass('public.material_units') IS NULL THEN RAISE NOTICE 'no material_units'; RETURN; END IF;
    IF to_regclass('public.ppiq_tenants') IS NOT NULL THEN
        INSERT INTO public.ppiq_tenants (tenant_code, display_name, license_tier, is_active)
        SELECT 'default','Default Tenant','Enterprise',true WHERE NOT EXISTS (SELECT 1 FROM public.ppiq_tenants);
    END IF;
    SELECT id INTO v_site FROM public.sites ORDER BY created_at_utc LIMIT 1;
    IF v_site IS NULL THEN
        v_site := '3a000000-0000-0000-0000-0000005140e0';
        INSERT INTO public.sites (id,created_at_utc,is_synthetic,source_system,source_record_id,is_deleted,site_code,site_name,company_name,country_code,time_zone_id)
        VALUES (v_site,now(),true,'PPIQ_P3_SEED','PPIQ-P3-SITE',false,'PPIQ_P3_SITE','PlantProcess IQ P3 Site','SOU Industrial Software','DE','Europe/Berlin')
        ON CONFLICT (id) DO NOTHING;
    END IF;
    INSERT INTO public.material_units (id,created_at_utc,is_synthetic,source_system,is_deleted,material_code,material_unit_type,product_family,grade_or_recipe,site_id,plant_time_zone_id,plant_utc_offset_minutes)
    VALUES
        (v_h1,now(),true,'PPIQ_P3_SEED',false,'H-3361','Heat','FlatSteel','S235JR',v_site,'Europe/Berlin',60),
        (v_h2,now(),true,'PPIQ_P3_SEED',false,'H-3362','Heat','FlatSteel','S235JR',v_site,'Europe/Berlin',60),
        (v_coilT,now(),true,'PPIQ_P3_SEED',false,'C-0044170','Coil','FlatSteel','S235JR',v_site,'Europe/Berlin',60),
        (v_coilN,now(),true,'PPIQ_P3_SEED',false,'C-0044171','Coil','FlatSteel','S235JR',v_site,'Europe/Berlin',60)
    ON CONFLICT (id) DO NOTHING;
    INSERT INTO public.genealogy_edges (id,created_at_utc,is_synthetic,source_system,is_deleted,parent_material_unit_id,child_material_unit_id,relationship_type,contribution_weight,is_transition)
    VALUES
        ('3a000000-0000-0000-0000-0000000ed003',now(),true,'PPIQ_P3_SEED',false,v_h1,v_coilT,'HeatToCoil',0.70,true),
        ('3a000000-0000-0000-0000-0000000ed004',now(),true,'PPIQ_P3_SEED',false,v_h2,v_coilT,'HeatToCoil',0.30,true),
        ('3a000000-0000-0000-0000-0000000ed005',now(),true,'PPIQ_P3_SEED',false,v_h1,v_coilN,'HeatToCoil',1.00,false)
    ON CONFLICT (id) DO NOTHING;
    INSERT INTO public.material_aliases (id,created_at_utc,is_synthetic,source_system,is_deleted,material_unit_id,alias_code,alias_type)
    VALUES
        ('3a000000-0000-0000-0000-00000a110001',now(),true,'HSM_ORACLE',false,v_coilT,'C-0044170','SourceSystemId'),
        ('3a000000-0000-0000-0000-00000a110002',now(),true,'PARSYTEC_MYSQL',false,v_coilT,'44170','SourceSystemId')
    ON CONFLICT (id) DO NOTHING;
    BEGIN
        IF to_regclass('public.canonical_material_units') IS NOT NULL AND to_regclass('public.canonical_genealogy_edges') IS NOT NULL
           AND EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='canonical_material_units' AND column_name='tenant_id') THEN
            INSERT INTO public.canonical_material_units (id,tenant_id,material_key,material_type)
            SELECT v.id,t.id,v.k,v.ty FROM (VALUES (v_h1,'H-3361','Heat'),(v_h2,'H-3362','Heat'),(v_coilT,'C-0044170','Coil'),(v_coilN,'C-0044171','Coil')) AS v(id,k,ty)
            CROSS JOIN LATERAL (SELECT id FROM public.ppiq_tenants ORDER BY created_at_utc LIMIT 1) AS t ON CONFLICT (id) DO NOTHING;
            INSERT INTO public.canonical_genealogy_edges (id,tenant_id,parent_material_unit_id,child_material_unit_id)
            SELECT e.id,t.id,e.p,e.c FROM (VALUES
                ('3a000000-0000-0000-0000-0000ce000003'::uuid,v_h1,v_coilT),
                ('3a000000-0000-0000-0000-0000ce000005'::uuid,v_h2,v_coilT),
                ('3a000000-0000-0000-0000-0000ce000004'::uuid,v_h1,v_coilN)) AS e(id,p,c)
            CROSS JOIN LATERAL (SELECT id FROM public.ppiq_tenants ORDER BY created_at_utc LIMIT 1) AS t ON CONFLICT (id) DO NOTHING;
        END IF;
    EXCEPTION WHEN others THEN RAISE NOTICE 'canonical skipped (%)', SQLERRM;
    END;
    RAISE NOTICE 'PPIQ P3 seed applied site=%', v_site;
END;$seed$;
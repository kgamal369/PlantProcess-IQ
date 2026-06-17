-- PPIQ_REALIZATION_T023_SECOND_TENANT_SEED

CREATE TABLE IF NOT EXISTS public.ppiq_tenant_test_seed
(
    tenant_id uuid PRIMARY KEY,
    tenant_code text NOT NULL UNIQUE,
    display_name text NOT NULL,
    created_at_utc timestamptz NOT NULL DEFAULT now()
);

INSERT INTO public.ppiq_tenant_test_seed
(
    tenant_id,
    tenant_code,
    display_name
)
VALUES
('00000000-0000-0000-0000-000000000001', 'TENANT_A', 'PlantProcess IQ Tenant A'),
('00000000-0000-0000-0000-000000000002', 'TENANT_B', 'PlantProcess IQ Tenant B')
ON CONFLICT (tenant_id) DO UPDATE
SET
    tenant_code = EXCLUDED.tenant_code,
    display_name = EXCLUDED.display_name;

CREATE OR REPLACE FUNCTION public.ppiq_validate_second_tenant_seed()
RETURNS TABLE
(
    gate_code text,
    is_green boolean,
    evidence text
)
LANGUAGE sql
AS $$
    SELECT
        'PPIQ-T023-SECOND-TENANT-SEED',
        (SELECT count(*) FROM public.ppiq_tenant_test_seed) >= 2,
        'Tenant A and Tenant B deterministic seed rows exist.';
$$;

-- DEMO ONLY — v4 demo cost-assumption defaults for the 'default' tenant. Do NOT apply in Production.
-- These are the same numbers behind the §7.5 DX51D worked example (mid ~EUR 42k/mo; band ~28.4k-55.6k).
INSERT INTO canon.cost_assumption (
    tenant_id, version, currency,
    cost_per_ton_low, cost_per_ton_mid, cost_per_ton_high,
    downgrade_delta_low, downgrade_delta_mid, downgrade_delta_high,
    scrap_cost_low, scrap_cost_mid, scrap_cost_high,
    downtime_cost_min_low, downtime_cost_min_mid, downtime_cost_min_high,
    grade_premium_low, grade_premium_mid, grade_premium_high,
    energy_price_low, energy_price_mid, energy_price_high, created_by)
SELECT t.id, 1, 'EUR',
       600, 700, 820,
       80, 120, 160,
       240, 300, 360,
       100, 150, 200,
       110, 155, 200,
       60, 85, 120, 'demo-seed'
FROM public.ppiq_tenants t
WHERE lower(t.tenant_code) = 'default'
  AND NOT EXISTS (SELECT 1 FROM canon.cost_assumption c WHERE c.tenant_id = t.id);
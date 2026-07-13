#requires -Version 5.1
<#
  Run-M1-08-EngineDiscovery.ps1  (M1-08)
  --------------------------------------
  Runs the Engine on the imported canonical data and confirms it ORGANICALLY rediscovers the
  planted pattern - the software was never told about it. Steps:
    1. readiness: parameter_observations count is now above the gate threshold
    2. refresh: ppiq_ml_refresh_feature_store_v6 (features from parameter_observations,
       outcomes from quality_events)
    3. run: ppiq_ml_run_learning_job_governed_v1('ML_PROCESS_VS_DEFECT', 365, 20, false)
    4. read back ml_correlation_results_v2 - superheat should rank as a CRACK_LONG driver with a
       q-value; SCRATCH should show no significant superheat driver (the honest control)

  READ + COMPUTE only against ppiq_app (no source writes, no code changes).
  Launch: powershell -NoProfile -ExecutionPolicy Bypass -File .\Run-M1-08-EngineDiscovery.ps1
#>

[CmdletBinding()]
param(
    [string]$DbHost='127.0.0.1', [int]$Port=5432, [string]$Database='ppiq_app',
    [string]$User='ppiq_dev', [string]$Pass='ppiq_dev_local_only',
    [int]$WindowDays=365
)
$ErrorActionPreference='Stop'
function Section($t){ Write-Host ""; Write-Host "== $t ==" -ForegroundColor Cyan }
function Bad($t){ Write-Host "FAIL: $t" -ForegroundColor Red }

$psql=(Get-Command psql -ErrorAction SilentlyContinue).Source
if(-not $psql){ $c=Get-ChildItem 'C:\Program Files\PostgreSQL\*\bin\psql.exe' -EA SilentlyContinue | Sort-Object FullName -Descending | Select-Object -First 1; if($c){$psql=$c.FullName} }
if(-not $psql){ Bad "psql.exe not found."; exit 1 }
Write-Host "using psql: $psql" -ForegroundColor Gray
$env:PGPASSWORD=$Pass
function Sql([string]$label,[string]$sql){
    Section $label
    $eap=$ErrorActionPreference; $ErrorActionPreference='Continue'
    $out=& $psql -h $DbHost -p $Port -d $Database -U $User -v ON_ERROR_STOP=on -c $sql 2>&1
    $code=$LASTEXITCODE; $ErrorActionPreference=$eap
    $out | ForEach-Object { Write-Host "     $_" }
    if($code -ne 0){ Bad "step failed (see above)"; }
    return $code
}

Sql "1. Readiness - observations now present per parameter" @"
SELECT pd.parameter_code, count(*) AS observations
FROM parameter_observations po JOIN parameter_definitions pd ON pd.id = po.parameter_definition_id
WHERE pd.parameter_code LIKE '%.%'
GROUP BY pd.parameter_code ORDER BY observations DESC;
"@ | Out-Null

Sql "2. Refresh the feature + outcome store (ppiq_ml_refresh_feature_store_v6)" @"
SELECT * FROM public.ppiq_ml_refresh_feature_store_v6($WindowDays);
"@ | Out-Null

Sql "3. Run the governed learning job (ML_PROCESS_VS_DEFECT)" @"
SELECT * FROM public.ppiq_ml_run_learning_job_governed_v1('ML_PROCESS_VS_DEFECT', $WindowDays, 20, false);
"@ | Out-Null

Sql "4a. What the Engine discovered - top correlations (feature vs outcome)" @"
SELECT feature_key, outcome_key,
       round(coefficient::numeric,3)  AS coefficient,
       round(effect_size::numeric,3)  AS effect_size,
       round(q_value::numeric,4)      AS q_value,
       sample_size
FROM ml_correlation_results_v2
ORDER BY abs(coefficient) DESC NULLS LAST
LIMIT 15;
"@ | Out-Null

Sql "4b. The headline: superheat as a CRACK_LONG driver (vs SCRATCH control)" @"
SELECT feature_key, outcome_key, round(effect_size::numeric,3) AS effect_size, round(q_value::numeric,4) AS q_value
FROM ml_correlation_results_v2
WHERE feature_key ILIKE '%superheat%'
ORDER BY q_value NULLS LAST;
"@ | Out-Null

Section "Read"
Write-Host "     PASS if superheat shows a significant effect on the defect outcome (low q_value) and the" -ForegroundColor Gray
Write-Host "     control (SCRATCH, or a non-driver feature) does not. The Engine was never told the pattern." -ForegroundColor Gray
Write-Host "     If ml_correlation_results_v2 is empty, paste blocks 1-3 - the readiness gate or an outcome" -ForegroundColor Gray
Write-Host "     definition (defect.rate_per_m2 grain) likely needs one adjustment, which I'll pinpoint." -ForegroundColor Gray

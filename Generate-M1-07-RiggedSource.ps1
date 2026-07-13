#requires -Version 5.1
<#
  Generate-M1-07-RiggedSource.ps1  (M1-07 Phase 1 of 2)
  -----------------------------------------------------
  Plants the "rigged" raw data INSIDE the emulated Meltshop source DB (the external customer
  database), keyed to the heat_ids already present in meltshop_heats. Two new source tables:
    - meltshop_param_readings : one row per heat per process parameter (the drivers + context)
    - meltshop_defect_events  : CRACK_LONG (driver-linked) and SCRATCH (control) defect events
  The pattern is embedded MATHEMATICALLY: high-superheat heats get CRACK_LONG at ~32.8%, normal
  heats at ~5% -> odds ratio ~9.3. SCRATCH is ~10% independent of superheat (should show NO driver).
  The software is untouched: it will discover this only after importing through the DB-Link.

  This writes ONLY to the emulated source (port 15432), never to ppiq_app. It ends by computing
  the REALIZED odds ratio in the generated data so we confirm it landed on 9.3x before import.

  Prereq: the source stack is up (docker compose -f deploy\compose\docker-compose.sources.yml up -d).
  Launch (immune to execution policy / mark-of-the-web):
    powershell -NoProfile -ExecutionPolicy Bypass -File .\Generate-M1-07-RiggedSource.ps1
#>

[CmdletBinding()]
param(
    [string]$SrcHost='127.0.0.1', [int]$SrcPort=15432, [string]$SrcDb='meltshop',
    [string]$SrcUser='ppiq_src', [string]$SrcPass='ppiq_src_local_only',
    [int]$HighPct=30,            # share of heats in the high-superheat group
    [int]$P1PerMille=328,        # CRACK_LONG probability for high group (0.328)
    [int]$P0PerMille=50,         # CRACK_LONG probability for normal group (0.05)
    [int]$ScratchPerMille=100    # SCRATCH probability (control, ~0.10)
)

$ErrorActionPreference='Stop'
function Section($t){ Write-Host ""; Write-Host "== $t ==" -ForegroundColor Cyan }
function Bad($t){ Write-Host "FAIL: $t" -ForegroundColor Red }

$psql=(Get-Command psql -ErrorAction SilentlyContinue).Source
if(-not $psql){ $c=Get-ChildItem 'C:\Program Files\PostgreSQL\*\bin\psql.exe' -EA SilentlyContinue | Sort-Object FullName -Descending | Select-Object -First 1; if($c){$psql=$c.FullName} }
if(-not $psql){ Bad "psql.exe not found."; exit 1 }
Write-Host "using psql: $psql" -ForegroundColor Gray
$env:PGPASSWORD=$SrcPass

function Run-Sql([string]$label,[string]$sql){
    Section $label
    $eap = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
    $out = & $psql -h $SrcHost -p $SrcPort -d $SrcDb -U $SrcUser -v ON_ERROR_STOP=on -c $sql 2>&1
    $code = $LASTEXITCODE
    $ErrorActionPreference = $eap
    if($code -ne 0){ Bad "query failed:"; $out | ForEach-Object { Write-Host "     $_" -ForegroundColor DarkYellow }; throw "sql failed" }
    $out | ForEach-Object { Write-Host "     $_" }
}

# --- connectivity check ---
Section "0. Source reachable + meltshop_heats present"
Run-Sql "heat count in the emulated source" "SELECT count(*) AS heats FROM meltshop_heats;"

# --- generate the rigged tables (deterministic via hashtext, re-runnable) ---
$gen = @"
SET client_min_messages TO WARNING;
DROP TABLE IF EXISTS meltshop_param_readings;
CREATE TABLE meltshop_param_readings (
    reading_id      bigserial PRIMARY KEY,
    heat_id         text NOT NULL,
    param_code      text NOT NULL,
    observed_at_utc timestamptz NOT NULL,
    numeric_value   double precision NOT NULL
);
DROP TABLE IF EXISTS meltshop_defect_events;
CREATE TABLE meltshop_defect_events (
    event_id     bigserial PRIMARY KEY,
    heat_id      text NOT NULL,
    event_at_utc timestamptz NOT NULL,
    defect_code  text NOT NULL,
    event_type   text NOT NULL DEFAULT 'Defect',
    severity     text
);

CREATE TEMP TABLE _scored AS
WITH latent AS (
    SELECT h.heat_id,
           COALESCE(h.tap_start_utc, now() - interval '45 days') AS t0,
           (abs(hashtext(h.heat_id))            % 100)  < $HighPct AS is_high,
           (abs(hashtext(h.heat_id || 'sh'))    % 1000)           AS r_sh,
           (abs(hashtext(h.heat_id || 'crack')) % 1000)           AS r_crack,
           (abs(hashtext(h.heat_id || 'scr'))   % 1000)           AS r_scr,
           (abs(hashtext(h.heat_id || 'cev'))   % 1000)           AS r_cev
    FROM meltshop_heats h
)
SELECT heat_id, t0, is_high,
    CASE WHEN is_high THEN 32.0 + (r_sh % 80)::double precision/10.0
         ELSE 18.0 + (r_sh % 120)::double precision/10.0 END AS superheat_c,
    CASE WHEN is_high THEN (r_crack < $P1PerMille)
         ELSE (r_crack < $P0PerMille) END AS has_crack,
    (r_scr < $ScratchPerMille) AS has_scratch,
    CASE WHEN is_high THEN 0.45 + (r_cev % 25)::double precision/100.0
         ELSE 0.38 + (r_cev % 25)::double precision/100.0 END AS cev
FROM latent;

INSERT INTO meltshop_param_readings (heat_id, param_code, observed_at_utc, numeric_value)
SELECT heat_id, 'thermal.true_superheat',   t0, superheat_c FROM _scored
UNION ALL SELECT heat_id, 'chemistry.cev',            t0, cev FROM _scored
UNION ALL SELECT heat_id, 'casting.speed_mean',       t0, 1.0  + (abs(hashtext(heat_id||'cs')) % 60)::double precision/100.0 FROM _scored
UNION ALL SELECT heat_id, 'rolling.reduction_ratio',  t0, 3.0  + (abs(hashtext(heat_id||'rr')) % 200)::double precision/100.0 FROM _scored
UNION ALL SELECT heat_id, 'rolling.cooling_rate',     t0, 10.0 + (abs(hashtext(heat_id||'cr')) % 150)::double precision/10.0 FROM _scored
UNION ALL SELECT heat_id, 'kpi.energy_per_ton',       t0, 380.0 + (abs(hashtext(heat_id||'en')) % 120)::double precision FROM _scored
UNION ALL SELECT heat_id, 'kpi.prime_yield',          t0, 0.90 + (abs(hashtext(heat_id||'py')) % 90)::double precision/1000.0 FROM _scored
UNION ALL SELECT heat_id, 'downtime.cascade_minutes', t0, (abs(hashtext(heat_id||'dt')) % 45)::double precision FROM _scored;

INSERT INTO meltshop_defect_events (heat_id, event_at_utc, defect_code, event_type, severity)
SELECT heat_id, t0 + interval '2 days', 'CRACK_LONG', 'Defect',
       CASE WHEN superheat_c >= 36 THEN 'High' ELSE 'Medium' END
FROM _scored WHERE has_crack
UNION ALL
SELECT heat_id, t0 + interval '2 days', 'SCRATCH', 'Defect', 'Low'
FROM _scored WHERE has_scratch;
"@
Run-Sql "1. Generate rigged readings + defect events (deterministic, re-runnable)" $gen

Run-Sql "2. Row counts" @"
SELECT 'param_readings' AS tbl, count(*) AS rows, count(DISTINCT param_code) AS params FROM meltshop_param_readings
UNION ALL SELECT 'defect_events', count(*), count(DISTINCT defect_code) FROM meltshop_defect_events;
"@

Run-Sql "3. REALIZED odds ratio: CRACK_LONG vs high-superheat (target ~9.3)" @"
WITH j AS (
    SELECT r.heat_id,
           (r.numeric_value >= 31.0) AS high,
           EXISTS (SELECT 1 FROM meltshop_defect_events e
                   WHERE e.heat_id = r.heat_id AND e.defect_code = 'CRACK_LONG') AS crack
    FROM meltshop_param_readings r
    WHERE r.param_code = 'thermal.true_superheat'
),
agg AS (
    SELECT count(*) FILTER (WHERE high AND crack)          AS a,
           count(*) FILTER (WHERE high AND NOT crack)      AS b,
           count(*) FILTER (WHERE NOT high AND crack)      AS c,
           count(*) FILTER (WHERE NOT high AND NOT crack)  AS d
    FROM j
)
SELECT a AS high_crack, b AS high_ok, c AS norm_crack, d AS norm_ok,
       round((a::numeric * d) / NULLIF(b::numeric * c, 0), 2) AS odds_ratio_crack_long
FROM agg;
"@

Run-Sql "4. Control check: SCRATCH vs high-superheat (should be ~1.0 = no driver)" @"
WITH j AS (
    SELECT r.heat_id, (r.numeric_value >= 31.0) AS high,
           EXISTS (SELECT 1 FROM meltshop_defect_events e
                   WHERE e.heat_id = r.heat_id AND e.defect_code = 'SCRATCH') AS scr
    FROM meltshop_param_readings r WHERE r.param_code = 'thermal.true_superheat'
), agg AS (
    SELECT count(*) FILTER (WHERE high AND scr) a, count(*) FILTER (WHERE high AND NOT scr) b,
           count(*) FILTER (WHERE NOT high AND scr) c, count(*) FILTER (WHERE NOT high AND NOT scr) d FROM j
)
SELECT round((a::numeric*d)/NULLIF(b::numeric*c,0),2) AS odds_ratio_scratch_control FROM agg;
"@

Section "Read"
Write-Host "     Block 3 odds_ratio_crack_long should be ~9.3 (tune -P1PerMille/-P0PerMille/-HighPct if needed)." -ForegroundColor Gray
Write-Host "     Block 4 should be ~1.0 (SCRATCH is a genuine control with no superheat driver)." -ForegroundColor Gray
Write-Host "     When the ratio is right, Phase 2 imports meltshop_param_readings -> ParameterObservation and" -ForegroundColor Gray
Write-Host "     meltshop_defect_events -> QualityEvent through the DB-Link, then runs the Engine to rediscover it." -ForegroundColor Gray

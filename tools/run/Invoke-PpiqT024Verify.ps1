#requires -Version 5.1
<#
.SYNOPSIS
    PPIQ T-024 - re-run the closure verification only. READ-ONLY apart from
    refreshing materialized views. It does not delete, insert or replace.

.DESCRIPTION
    WHY THIS EXISTS. The step B runner performs the replacement and then verifies
    it in one pass. Its API smoke defaulted to port 5000 while the presentation
    profile listens on 5063, so the smoke fails on a database that is actually
    correct. Re-running step B to fix a port would repeat a 113 MB destructive
    transaction for no reason.

    This runs the same verification, with the right port, and touches no data.

    Checks:
      1  no operational row outside the Fleet v2 provenance label
      2  genealogy complete and conserved - no orphan, no self edge, every coil
         resolving to a slab and every slab to a heat
      3  the downtime two-quantity contract populated from a real source field
      4  no surface defect without a catalogue row
      5  the identity convention still shared with the donor
      6  population profile, so the shape can be read rather than assumed
      7  materialized views that read the canonical tables, refreshed
      8  API smoke, because row counts do not prove a dashboard renders
      9  the mixed-industry vocabulary still present, reported as OUTSTANDING

.EXAMPLE
    .\tools\run\Invoke-PpiqT024Verify.ps1
    .\tools\run\Invoke-PpiqT024Verify.ps1 -ApiBaseUrl http://localhost:5063
#>

[CmdletBinding()]
param(
    [string]$PgHost     = "127.0.0.1",
    [int]   $PgPort     = 5432,
    [string]$PgUser     = "ppiq_dev",
    [string]$PgPassword = "ppiq_dev_local_only",
    [string]$Database   = "ppiq_presentation",
    [string]$PsqlPath   = "",
    [string]$ApiBaseUrl = "http://localhost:5063",
    [switch]$SkipRefresh
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Continue"

function Say  { param([string]$T) Write-Host $T }
function Rule { param([string]$T) Write-Host ""; Write-Host ("=" * 78); Write-Host $T; Write-Host ("=" * 78) }

function Read-IfExists {
    param([string]$Path)
    if (Test-Path -LiteralPath $Path) { return [System.IO.File]::ReadAllText($Path) }
    return ""
}

function Resolve-Psql {
    param([string]$Explicit)
    if (-not [string]::IsNullOrWhiteSpace($Explicit)) {
        if (Test-Path -LiteralPath $Explicit) { return $Explicit }
        return $null
    }
    $c = Get-Command psql.exe -ErrorAction SilentlyContinue
    if ($null -ne $c) { return $c.Source }
    foreach ($p in @("C:\Program Files\PostgreSQL\16\bin\psql.exe",
                     "C:\Program Files\PostgreSQL\17\bin\psql.exe",
                     "C:\Program Files\PostgreSQL\15\bin\psql.exe")) {
        if (Test-Path -LiteralPath $p) { return $p }
    }
    return $null
}

function Invoke-Sql {
    param([string]$Sql, [string]$Tag)
    $f = Join-Path $script:tmp ($Tag + ".sql")
    $o = Join-Path $script:tmp ($Tag + ".out")
    $e = Join-Path $script:tmp ($Tag + ".err")
    [System.IO.File]::WriteAllText($f, $Sql, (New-Object System.Text.UTF8Encoding($false)))
    $a = @("-X", "-w", "-h", $PgHost, "-p", "$PgPort", "-U", $PgUser, "-d", $Database,
           "-v", "ON_ERROR_STOP=1", "-f", $f, "-o", $o)
    $p = Start-Process -FilePath $script:psql -ArgumentList $a -NoNewWindow -Wait `
                       -PassThru -RedirectStandardError $e
    $r = New-Object psobject
    Add-Member -InputObject $r -MemberType NoteProperty -Name ExitCode -Value $p.ExitCode
    Add-Member -InputObject $r -MemberType NoteProperty -Name Output   -Value (Read-IfExists $o)
    Add-Member -InputObject $r -MemberType NoteProperty -Name Error    -Value (Read-IfExists $e)
    return $r
}

function Check-Table {
    param([string]$Output, [string]$Label)
    $bad = 0
    foreach ($rawLine in ($Output -split "`n")) {
        $line = $rawLine.Trim()
        if ($line -match "^\|\s*(.+?)\s*\|\s*(-?\d+)\s*\|\s*(-?\d+)\s*\|") {
            if ([int]$Matches[2] -ne [int]$Matches[3]) {
                Say ("[FAIL] " + $Label + " - " + $Matches[1] + ": found " +
                     $Matches[2] + ", required " + $Matches[3])
                $bad = $bad + 1
            }
        }
    }
    return $bad
}

Rule "PPIQ T-024 - CLOSURE VERIFICATION (READ-ONLY)"
$script:psql = Resolve-Psql -Explicit $PsqlPath
if ($null -eq $script:psql) { Say "[FAIL] psql.exe not found."; exit 2 }
Say ("Database : " + $Database)
Say ("API      : " + $ApiBaseUrl)

$env:PGPASSWORD = $PgPassword
$env:PGCLIENTENCODING = "UTF8"
$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$script:tmp = Join-Path $env:TEMP ("ppiq_t024v_" + $stamp)
New-Item -ItemType Directory -Path $script:tmp -Force | Out-Null
$bad = 0

try {
    Rule "1 - POPULATION PROFILE"
    $prof = Invoke-Sql -Tag "profile" -Sql @'
\pset border 2
SELECT 'material_units' AS entity, count(*) AS rows,
       count(*) FILTER (WHERE source_system = 'FLEET_V2') AS fleet_v2
FROM public.material_units
UNION ALL SELECT 'genealogy_edges', count(*), count(*) FILTER (WHERE source_system='FLEET_V2') FROM public.genealogy_edges
UNION ALL SELECT 'process_step_executions', count(*), count(*) FILTER (WHERE source_system='FLEET_V2') FROM public.process_step_executions
UNION ALL SELECT 'parameter_observations', count(*), count(*) FILTER (WHERE source_system='FLEET_V2') FROM public.parameter_observations
UNION ALL SELECT 'quality_events', count(*), count(*) FILTER (WHERE source_system='FLEET_V2') FROM public.quality_events
UNION ALL SELECT 'downtime_events', count(*), count(*) FILTER (WHERE source_system='FLEET_V2') FROM public.downtime_events
ORDER BY 1;
'@
    if ($prof.ExitCode -ne 0) { Say $prof.Error; throw "profile" }
    Say $prof.Output

    Say "material unit types now present:"
    $types = Invoke-Sql -Tag "types" -Sql @'
\pset border 2
SELECT material_unit_type, count(*) AS units FROM public.material_units
GROUP BY 1 ORDER BY 2 DESC;
'@
    Say $types.Output

    Rule "2 - CLOSURE CONDITIONS"
    $ver = Invoke-Sql -Tag "verify" -Sql @'
\pset border 2
SELECT 'operational rows outside FLEET_V2' AS check_name, count(*) AS found, 0 AS required
FROM (
  SELECT 1 FROM public.material_units WHERE coalesce(source_system,'') <> 'FLEET_V2'
  UNION ALL SELECT 1 FROM public.parameter_observations WHERE coalesce(source_system,'') <> 'FLEET_V2'
  UNION ALL SELECT 1 FROM public.quality_events WHERE coalesce(source_system,'') <> 'FLEET_V2'
  UNION ALL SELECT 1 FROM public.genealogy_edges WHERE coalesce(source_system,'') <> 'FLEET_V2'
  UNION ALL SELECT 1 FROM public.downtime_events WHERE coalesce(source_system,'') <> 'FLEET_V2'
  UNION ALL SELECT 1 FROM public.process_step_executions WHERE coalesce(source_system,'') <> 'FLEET_V2'
) x
UNION ALL
SELECT 'genealogy orphan endpoints', count(*), 0 FROM (
  SELECT 1 FROM public.genealogy_edges g LEFT JOIN public.material_units m
    ON m.id = g.parent_material_unit_id WHERE m.id IS NULL
  UNION ALL
  SELECT 1 FROM public.genealogy_edges g LEFT JOIN public.material_units m
    ON m.id = g.child_material_unit_id WHERE m.id IS NULL) y
UNION ALL
SELECT 'self edges', count(*), 0 FROM public.genealogy_edges
WHERE parent_material_unit_id = child_material_unit_id
UNION ALL
SELECT 'coils with no slab parent', count(*), 0 FROM public.material_units c
WHERE c.material_unit_type = 'Coil' AND NOT EXISTS
  (SELECT 1 FROM public.genealogy_edges g WHERE g.child_material_unit_id = c.id)
UNION ALL
SELECT 'slabs with no heat parent', count(*), 0 FROM public.material_units s
WHERE s.material_unit_type = 'Slab' AND NOT EXISTS
  (SELECT 1 FROM public.genealogy_edges g WHERE g.child_material_unit_id = s.id)
UNION ALL
SELECT 'downtime rows with zero stopped minutes', count(*), 0
FROM public.downtime_events WHERE stopped_minutes <= 0
UNION ALL
SELECT 'surface defects with no catalogue row', count(*), 0
FROM public.quality_events WHERE event_type = 'SurfaceDefect' AND defect_catalog_id IS NULL
UNION ALL
SELECT 'observations with no parameter definition', count(*), 0
FROM public.parameter_observations WHERE parameter_definition_id IS NULL;
'@
    Say $ver.Output
    $bad = $bad + (Check-Table -Output $ver.Output -Label "closure")

    Rule "3 - THE DOWNTIME TWO-QUANTITY CONTRACT"
    Say "T-009 closed the contract; this is the first population of it from a real"
    Say "source field rather than a default. Reported, not asserted against a"
    Say "fixed count, because the split is a property of the buffer posture."
    $dt = Invoke-Sql -Tag "downtime" -Sql @'
\pset border 2
SELECT count(*) AS events,
       count(*) FILTER (WHERE stopped_minutes > 0) AS stopped_above_zero,
       count(*) FILTER (WHERE production_impact_minutes > 0) AS impact_above_zero,
       count(*) FILTER (WHERE production_impact_minutes = 0) AS fully_absorbed,
       count(*) FILTER (WHERE production_impact_minutes > 2 * stopped_minutes) AS cascaded,
       round(min(stopped_minutes),2) AS min_stopped,
       round(max(stopped_minutes),2) AS max_stopped,
       round(max(production_impact_minutes),2) AS max_impact
FROM public.downtime_events;
'@
    Say $dt.Output

    Rule "4 - IDENTITY CONVENTION SHARED WITH THE DONOR"
    Say "The donor sits at 1x and canonical at 3x, so these prove the CONVENTION"
    Say "holds on the overlapping range, not that the two are the same size."
    $idr = Invoke-Sql -Tag "identity" -Sql @'
\pset border 2
-- Heat and coil identifiers are SEQUENTIAL, so the donor range is a clean prefix
-- of canonical's and the overlap is total.
--
-- THE SLAB IDENTIFIER IS COMPOSITE: SLB + heat + slab index. T-022 made
-- pieces-per-heat vary from 7 to 11, so a heat that now carries 8 slabs no longer
-- has SLB...09. The overlap is sum(min(9, slabs_per_heat)) over heats 1 to 630 -
-- arithmetically exact, not approximate. An earlier version asserted 5,670 by
-- copying the heat and coil pattern without noticing that one of the three
-- identifiers encodes cardinality.
--
-- What must hold is the CONVENTION, so that is what is checked.
SELECT 'heat codes shared' AS check_name, count(*) AS found, 630 AS required
FROM public.material_units m JOIN src_meltshop_pg.heats h ON h.heat_no = m.material_code
UNION ALL
SELECT 'coil codes shared', count(*), 5670
FROM public.material_units m JOIN src_hsm_oracle_shape.hsm_coils c ON c.coil_id = m.material_code
UNION ALL
SELECT 'slab overlap equals sum(min(9, slabs per heat))', (
  SELECT count(*) FROM public.material_units m
  JOIN src_caster_oracle_shape.cast_pieces p ON p.piece_id = m.material_code), (
  SELECT coalesce(sum(least(9, n)), 0)::bigint FROM (
    SELECT count(*) AS n FROM public.material_units
    WHERE material_unit_type = 'Slab'
      AND substring(material_code from 4 for 5) ~ '^[0-9]+$'
      AND substring(material_code from 4 for 5)::int <= 630
    GROUP BY substring(material_code from 4 for 5)) q)
UNION ALL
SELECT 'slab identifiers off-convention', (
  SELECT count(*) FROM public.material_units
  WHERE material_unit_type = 'Slab' AND material_code !~ '^SLB[0-9]{7}$'), 0
UNION ALL
SELECT 'heat identifiers off-convention', (
  SELECT count(*) FROM public.material_units
  WHERE material_unit_type = 'Heat' AND material_code !~ '^H2026[0-9]{5}$'), 0
UNION ALL
SELECT 'coil identifiers off-convention', (
  SELECT count(*) FROM public.material_units
  WHERE material_unit_type = 'Coil' AND material_code !~ '^C[0-9]{7}$'), 0;
'@
    Say $idr.Output
    $bad = $bad + (Check-Table -Output $idr.Output -Label "identity")

    Rule "5 - DEFECT PARETO AS THE CUSTOMER WOULD SEE IT"
    $par = Invoke-Sql -Tag "pareto" -Sql @'
\pset border 2
SELECT d.defect_code, count(*) AS events,
       round(100.0 * count(*) / sum(count(*)) OVER (), 2) AS pct
FROM public.quality_events q
JOIN public.defect_catalogs d ON d.id = q.defect_catalog_id
WHERE q.event_type = 'SurfaceDefect'
GROUP BY 1 ORDER BY 2 DESC;
'@
    Say $par.Output

    Rule "6 - READ MODELS"
    $mv = Invoke-Sql -Tag "matviews" -Sql @'
\pset border 2
SELECT DISTINCT dependent.relname AS matview
FROM pg_depend d
JOIN pg_rewrite r ON r.oid = d.objid
JOIN pg_class dependent ON dependent.oid = r.ev_class
JOIN pg_class source ON source.oid = d.refobjid
JOIN pg_namespace n ON n.oid = source.relnamespace AND n.nspname = 'public'
WHERE dependent.relkind = 'm'
  AND source.relname IN ('material_units','parameter_observations','quality_events',
                         'downtime_events','genealogy_edges','process_step_executions')
  AND dependent.relname <> source.relname
ORDER BY 1;
'@
    Say $mv.Output
    # THE CARRIAGE RETURN AGAIN. Splitting psql output on `n leaves the \r at the
    # end of every line, so a $ anchor never matches and the parse silently found
    # nothing - while the table above plainly listed nine views. This is the third
    # time this exact fault has cost something today, so the line is TRIMMED before
    # matching rather than relying on an anchor.
    $names = @()
    foreach ($rawLine in ($mv.Output -split "`n")) {
        $line = $rawLine.Trim()
        if ($line -match "^\|\s*([a-z0-9_]+)\s*\|$") {
            if ($Matches[1] -ne "matview") { $names += $Matches[1] }
        }
    }
    Say ("parsed " + $names.Count + " materialized view name(s) from the table above")
    if ($names.Count -eq 0 -and ($mv.Output -match "\(\s*[1-9]\d*\s+rows?\s*\)")) {
        Say "[FAIL] the table reported rows but none parsed - refusing to claim"
        Say "       there is nothing to refresh when the query says otherwise."
        $bad = $bad + 1
    }
    if ($names.Count -eq 0) {
        Say "No materialized view reads the canonical tables."
    } elseif ($SkipRefresh) {
        Say "[WARN] -SkipRefresh given; views NOT refreshed."
    } else {
        foreach ($n in $names) {
            $rf = Invoke-Sql -Tag ("refresh_" + $n) `
                             -Sql ("REFRESH MATERIALIZED VIEW public." + $n + ";")
            if ($rf.ExitCode -ne 0) { Say ("[FAIL] refresh " + $n); $bad = $bad + 1 }
            else { Say ("[OK] refreshed " + $n) }
        }
    }

    Rule "7 - API SMOKE"
    $endpoints = @("/health", "/healthz", "/api/health")
    $apiOk = $false
    foreach ($ep in $endpoints) {
        try {
            $resp = Invoke-WebRequest -Uri ($ApiBaseUrl + $ep) -TimeoutSec 8 `
                                      -UseBasicParsing -ErrorAction Stop
            Say ("  " + $ep.PadRight(14) + " -> " + $resp.StatusCode)
            if ($resp.StatusCode -eq 200) { $apiOk = $true; break }
        } catch {
            Say ("  " + $ep.PadRight(14) + " -> unreachable")
        }
    }
    if (-not $apiOk) {
        Say ""
        Say "[WARN] no health endpoint answered. The API smoke is NOT green, and"
        Say "       T-024 closure requires it."
        $bad = $bad + 1
    } else {
        Say "[OK] API responded"
    }

    Rule "8 - OUTSTANDING: MIXED-INDUSTRY VOCABULARY"
    Say "Still present and still customer-visible. Reported, not removed - it needs"
    Say "dependency checks against dashboards and widgets before anything retires."
    $mix = Invoke-Sql -Tag "mixed" -Sql @'
\pset border 2
SELECT 'defect_catalogs not in the fleet v2 set' AS surface, count(*) AS rows
FROM public.defect_catalogs
WHERE defect_code NOT IN ('SCALE','EDGE_CRACK','ROLLED_IN_SCALE','SLIVER','INCLUSION',
  'PINHOLE','SCRATCH','WAVINESS','CENTRE_BUCKLE','EDGE_WAVE','ROLL_MARK','LAMINATION',
  'OIL_SPOT','SENSOR_ARTEFACT')
UNION ALL
SELECT 'parameter_definitions not in the fleet v2 set', count(*)
FROM public.parameter_definitions
WHERE parameter_code NOT IN ('CARBON_PCT','MANGANESE_PCT','SILICON_PCT','SULPHUR_PCT',
  'PHOSPHORUS_PCT','ALUMINIUM_PCT','TAP_TEMP_C','OXYGEN_NM3','POWER_KWH','LF_ARGON_NM3',
  'LF_CALCIUM_M','LF_FINAL_TEMP_C','CASTING_SPEED_MPM','SUPERHEAT_C','MOULD_LEVEL_AVG',
  'FDT_C','CT_C','THICKNESS_MM','WIDTH_MM','ROLL_FORCE_KN','ROLL_GAP_MM','ROLL_SPEED_MPS',
  'ROLL_TEMP_C','ACID_CONC_PCT','BATH_TEMP_C','LINE_SPEED_MPM','QA_WIDTH_MM','QA_THK_MM',
  'QA_ROUGHNESS_UM')
UNION ALL
SELECT 'equipment not in the fleet v2 set', count(*)
FROM public.equipment
WHERE equipment_code NOT IN ('EAF-01','EAF-02','LF-01','LF-02','CCM-01','CCM-02','HSM-01',
  'PKL-01','PKL-02','PARSYTEC-01','PARSYTEC-02','HSM-01-F1','HSM-01-F2','HSM-01-F3',
  'HSM-01-F4','HSM-01-F5','HSM-01-F6','HSM-01-F7')
UNION ALL
SELECT 'material unit type definitions outside flat steel', count(*)
FROM public.material_unit_type_definitions
WHERE material_unit_type_code NOT IN ('Heat','Cast','Slab','Coil');
'@
    Say $mix.Output
}
catch {
    $bad = $bad + 1
}
finally {
    Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue
}

Rule "RESULT"
if ($bad -gt 0) {
    Say ("[FAIL] " + $bad + " check(s) not satisfied. Nothing was changed by this")
    Say "       script apart from any materialized view refresh."
    exit 1
}
Say "[OK] every closure condition in this script is satisfied."
Say ""
Say "STILL OUTSTANDING FOR T-024:"
Say "  - the mixed-industry vocabulary in section 8, to be dependency-checked"
Say "    then retired or proven filtered from every customer-visible selector"
Say "  - a BROWSER check; an API 200 is not a rendered dashboard"
Say ""
Say "AND THE STATE IS NOT PRESENTATION-READY UNTIL T-025: canonical operational"
Say "truth is new while the analytical and ML results still describe the old"
Say "population."
exit 0

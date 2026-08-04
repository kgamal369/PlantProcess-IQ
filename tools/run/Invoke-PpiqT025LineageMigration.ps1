#requires -Version 5.1
<#
.SYNOPSIS
    PPIQ T-025 lineage migration - make the feature-store engines persist the run
    identity they already create. ReportOnly by default; -Apply migrates.

.DESCRIPTION
    THE GAP. ppiq_ml_refresh_feature_store creates an authoritative run row in
    ml_feature_store_refresh_runs and returns its id, but no feature or outcome
    row records it. 346,973 rows exist with no traceable lineage.

    THE FIX, engine-owned as ruled. Each producing function stamps its OWN rows
    inside its OWN body, before completing its run row:

        producing function
          -> inserts its values
          -> stamps those values with its authoritative v_run_id
          -> completes the authoritative run row
          -> transaction commits

    Not driver backfill. Every caller - the API, a scheduler, an M2a rebuild -
    receives the invariant automatically.

    WHY NOT EDIT THE SIX INSERT LISTS. Adding the column to six INSERT lists means
    matching six SELECT lists across 222 lines of subqueries, CASE expressions and
    jsonb_build_object calls. Reconstructing that risks silently corrupting a
    working engine, which is the failure the preflight existed to prevent. The
    stamping UPDATE costs one extra write per refresh and cannot corrupt anything.

    THE TRANSFORMATION IS APPLIED TO THE LIVE BODY read at runtime with
    pg_get_functiondef, never to a repository copy - four scripts define this
    function and the live one is whichever ran last.

    GUARDS, all required before mutating:
      G1  every anchor the transformation needs is present in the live body
      G2  TAG OWNERSHIP - only the base function writes 'PPIQ-ML-Refresh' and only
          v6 writes 'PPIQ.V6.FeatureStore'. If another producer writes either tag,
          the stamping approach is unsafe and this FAILS CLOSED.
      G3  SINGLE WRITER - whether a guard already serialises refreshes. If none
          exists, a transaction-scoped advisory lock is added to the base path.

    V6 OWNERSHIP. Base creates the run and stamps its rows; v6 stamps its own rows
    with the SAME run id, promotes that run to engine_version 'v6', and recomputes
    counts across both tag sets. One run owns everything a v6 refresh produced.

.EXAMPLE
    .\tools\run\Invoke-PpiqT025LineageMigration.ps1
    .\tools\run\Invoke-PpiqT025LineageMigration.ps1 -Apply
#>

[CmdletBinding()]
param(
    [string]$PgHost     = "127.0.0.1",
    [int]   $PgPort     = 5432,
    [string]$PgUser     = "ppiq_dev",
    [string]$PgPassword = "ppiq_dev_local_only",
    [string]$Database   = "ppiq_presentation",
    [string]$PsqlPath   = "",
    [switch]$Apply
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
    param([string]$Sql, [string]$Tag, [switch]$Raw)
    $f = Join-Path $script:tmp ($Tag + ".sql")
    $o = Join-Path $script:tmp ($Tag + ".out")
    $e = Join-Path $script:tmp ($Tag + ".err")
    [System.IO.File]::WriteAllText($f, $Sql, (New-Object System.Text.UTF8Encoding($false)))
    $a = @("-X", "-w", "-h", $PgHost, "-p", "$PgPort", "-U", $PgUser, "-d", $Database,
           "-v", "ON_ERROR_STOP=1")
    if ($Raw) { $a += @("-A", "-t") }
    $a += @("-f", $f, "-o", $o)
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
    foreach ($raw in ($Output -split "`n")) {
        $line = $raw.Trim()
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

Rule "PPIQ T-025 LINEAGE MIGRATION - ENGINE-OWNED RUN STAMPING"
$script:psql = Resolve-Psql -Explicit $PsqlPath
if ($null -eq $script:psql) { Say "[FAIL] psql.exe not found."; exit 2 }
Say ("Database : " + $Database)
$modeLabel = "REPORT ONLY"
if ($Apply) { $modeLabel = "APPLY" }
Say ("Mode     : " + $modeLabel)

$env:PGPASSWORD = $PgPassword
$env:PGCLIENTENCODING = "UTF8"
$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$script:tmp = Join-Path $env:TEMP ("ppiq_t025mig_" + $stamp)
New-Item -ItemType Directory -Path $script:tmp -Force | Out-Null
$bad = 0

try {
    Rule "G2 - TAG OWNERSHIP. FAIL CLOSED IF ANOTHER PRODUCER WRITES EITHER TAG."
    Say "The stamping approach is only safe while each tag has exactly one writer."
    $own = Invoke-Sql -Tag "ownership" -Sql @'
\pset border 2
SELECT 'functions writing PPIQ-ML-Refresh' AS check_name,
       count(*) AS found, 1 AS required
FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
WHERE n.nspname = 'public' AND p.prokind = 'f'
  AND pg_get_functiondef(p.oid) LIKE '%INSERT INTO public.ml_feature_values%'
  AND pg_get_functiondef(p.oid) LIKE '%PPIQ-ML-Refresh%'
UNION ALL
SELECT 'functions writing PPIQ.V6.FeatureStore', count(*), 1
FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
WHERE n.nspname = 'public' AND p.prokind = 'f'
  AND pg_get_functiondef(p.oid) LIKE '%INSERT INTO public.ml_feature_values%'
  AND pg_get_functiondef(p.oid) LIKE '%PPIQ.V6.FeatureStore%'
UNION ALL
SELECT 'value rows carrying an unexpected tag', count(*), 0
FROM (
  SELECT 1 FROM public.ml_feature_values
   WHERE coalesce(source_system,'') NOT IN ('PPIQ-ML-Refresh','PPIQ.V6.FeatureStore')
  UNION ALL
  SELECT 1 FROM public.ml_outcome_values
   WHERE coalesce(source_system,'') NOT IN ('PPIQ-ML-Refresh','PPIQ.V6.FeatureStore')
) x;
'@
    if ($own.ExitCode -ne 0) { Say $own.Error; throw "ownership" }
    Say $own.Output
    $bad = $bad + (Check-Table -Output $own.Output -Label "tag ownership")
    Say "--- which functions those are ---"
    $whos = Invoke-Sql -Tag "whos" -Sql @'
\pset border 2
SELECT p.proname AS function_name,
       pg_get_functiondef(p.oid) LIKE '%PPIQ-ML-Refresh%' AS writes_base_tag,
       pg_get_functiondef(p.oid) LIKE '%PPIQ.V6.FeatureStore%' AS writes_v6_tag
FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
WHERE n.nspname='public' AND p.prokind='f'
  AND pg_get_functiondef(p.oid) LIKE '%INSERT INTO public.ml_feature_values%'
ORDER BY 1;
'@
    Say $whos.Output

    Rule "G3 - SINGLE WRITER"
    $live = Invoke-Sql -Tag "livebase" -Raw -Sql @'
SELECT pg_get_functiondef(p.oid)
FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
WHERE n.nspname='public' AND p.proname='ppiq_ml_refresh_feature_store' AND p.prokind='f';
'@
    if ($live.ExitCode -ne 0 -or [string]::IsNullOrWhiteSpace($live.Output)) {
        Say "[FAIL] could not read the live base function."
        throw "live"
    }
    $baseDef = $live.Output
    $liveV6 = Invoke-Sql -Tag "livev6" -Raw -Sql @'
SELECT pg_get_functiondef(p.oid)
FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
WHERE n.nspname='public' AND p.proname='ppiq_ml_refresh_feature_store_v6' AND p.prokind='f';
'@
    if ($liveV6.ExitCode -ne 0 -or [string]::IsNullOrWhiteSpace($liveV6.Output)) {
        Say "[FAIL] could not read the live v6 function."
        throw "live"
    }
    $v6Def = $liveV6.Output
    Say ("live base definition : " + $baseDef.Length + " chars")
    Say ("live v6 definition   : " + $v6Def.Length + " chars")
    $hasLock = ($baseDef -match "pg_advisory")
    if ($hasLock) {
        Say "[OK] the base path already serialises refreshes with an advisory lock"
    } else {
        Say "[INFO] no existing serialisation. A TRANSACTION-SCOPED advisory lock"
        Say "       will be added to the base path - pg_advisory_xact_lock, released"
        Say "       automatically at commit, so a failed refresh cannot strand it."
    }

    Rule "G1 - EVERY TRANSFORMATION ANCHOR MUST BE PRESENT"
    $anchors = @(
        @{ n = "base: seed catalog call";      t = $baseDef; s = "PERFORM public.ppiq_ml_seed_foundation_catalog();" },
        @{ n = "base: run row insert";         t = $baseDef; s = "INSERT INTO public.ml_feature_store_refresh_runs(id, status, window_days)" },
        @{ n = "base: run completion update";  t = $baseDef; s = "UPDATE public.ml_feature_store_refresh_runs" },
        @{ n = "base: run variable";           t = $baseDef; s = "v_run_id" },
        @{ n = "v6: base call";                t = $v6Def;   s = "FROM public.ppiq_ml_refresh_feature_store(p_window_days)" },
        @{ n = "v6: return";                   t = $v6Def;   s = "RETURN QUERY SELECT v_base.run_id" }
    )
    foreach ($a in $anchors) {
        $c = ([regex]::Matches($a.t, [regex]::Escape($a.s))).Count
        if ($c -ge 1) { Say ("[OK]      " + $a.n.PadRight(30) + $c + " occurrence(s)") }
        else { Say ("[MISSING] " + $a.n); $bad = $bad + 1 }
    }
    if ($bad -gt 0) {
        Say ""
        Say "[STOP] the live body does not match what the transformation expects."
        throw "anchors"
    }

    # ---------------------------------------------------------------- transform
    $newBase = $baseDef
    if (-not $hasLock) {
        $newBase = $newBase.Replace(
            "PERFORM public.ppiq_ml_seed_foundation_catalog();",
            "-- T-025 single-flight: transaction-scoped, released at commit." + "`n" +
            "    PERFORM pg_advisory_xact_lock(hashtext('ppiq_ml_feature_store_refresh'));" + "`n`n" +
            "    PERFORM public.ppiq_ml_seed_foundation_catalog();")
    }
    $stampBase = @"
-- T-025 ENGINE-OWNED LINEAGE. The run this function created is stamped onto the
    -- rows this function produced, inside this function, before the run completes.
    -- Only unowned rows carrying THIS producer's tag are touched.
    UPDATE public.ml_feature_values
       SET refresh_run_id = v_run_id
     WHERE source_system = 'PPIQ-ML-Refresh' AND refresh_run_id IS NULL;

    UPDATE public.ml_outcome_values
       SET refresh_run_id = v_run_id
     WHERE source_system = 'PPIQ-ML-Refresh' AND refresh_run_id IS NULL;

    UPDATE public.ml_feature_store_refresh_runs
"@
    $idx = $newBase.IndexOf("UPDATE public.ml_feature_store_refresh_runs")
    $newBase = $newBase.Substring(0, $idx) + $stampBase.Substring($stampBase.IndexOf("-- T-025")) `
               + $newBase.Substring($idx + "UPDATE public.ml_feature_store_refresh_runs".Length)
    $newBase = [regex]::Replace($newBase,
        "(UPDATE public\.ml_feature_store_refresh_runs\s*\r?\n\s*)SET ",
        "`$1SET engine_key = 'postgres-feature-store', engine_version = 'base', ")

    $v6Stamp = @"
-- T-025 ENGINE-OWNED LINEAGE, v6 path. ONE run owns everything this refresh
    -- produced: the base rows the base function stamped, and the v6 rows below.
    -- The run is then PROMOTED to v6 identity - the same pattern
    -- ppiq_ml_compute_correlations_v6 already applies to a correlation run.
    UPDATE public.ml_feature_values
       SET refresh_run_id = v_base.run_id
     WHERE source_system = 'PPIQ.V6.FeatureStore' AND refresh_run_id IS NULL;

    UPDATE public.ml_outcome_values
       SET refresh_run_id = v_base.run_id
     WHERE source_system = 'PPIQ.V6.FeatureStore' AND refresh_run_id IS NULL;

    UPDATE public.ml_feature_store_refresh_runs
       SET engine_key = 'postgres-feature-store',
           engine_version = 'v6',
           feature_row_count = (SELECT count(*) FROM public.ml_feature_values
                                 WHERE refresh_run_id = v_base.run_id),
           outcome_row_count = (SELECT count(*) FROM public.ml_outcome_values
                                 WHERE refresh_run_id = v_base.run_id)
     WHERE id = v_base.run_id;

    RETURN QUERY SELECT v_base.run_id
"@
    $newV6 = $v6Def.Replace("RETURN QUERY SELECT v_base.run_id",
                            $v6Stamp.Substring($v6Stamp.IndexOf("-- T-025")))

    Rule "THE MIGRATION"
    $ddl = @'
BEGIN;

-- lineage columns. NULLABLE for now: the 346,973 stale rows are about to be
-- cleared and must NOT be given manufactured lineage. NOT NULL is enforced only
-- after the real engine path has produced a traceable population.
ALTER TABLE public.ml_feature_values
  ADD COLUMN IF NOT EXISTS refresh_run_id uuid NULL;
ALTER TABLE public.ml_outcome_values
  ADD COLUMN IF NOT EXISTS refresh_run_id uuid NULL;

-- engine identity on the AUTHORITATIVE run record. No second run concept.
ALTER TABLE public.ml_feature_store_refresh_runs
  ADD COLUMN IF NOT EXISTS engine_key text NULL;
ALTER TABLE public.ml_feature_store_refresh_runs
  ADD COLUMN IF NOT EXISTS engine_version text NULL;

DO $ppiq$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint
                  WHERE conname = 'fk_ml_feature_values_refresh_run') THEN
    ALTER TABLE public.ml_feature_values
      ADD CONSTRAINT fk_ml_feature_values_refresh_run
      FOREIGN KEY (refresh_run_id)
      REFERENCES public.ml_feature_store_refresh_runs(id);
  END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_constraint
                  WHERE conname = 'fk_ml_outcome_values_refresh_run') THEN
    ALTER TABLE public.ml_outcome_values
      ADD CONSTRAINT fk_ml_outcome_values_refresh_run
      FOREIGN KEY (refresh_run_id)
      REFERENCES public.ml_feature_store_refresh_runs(id);
  END IF;
END
$ppiq$;

CREATE INDEX IF NOT EXISTS ix_ml_feature_values_refresh_run_id
  ON public.ml_feature_values (refresh_run_id);
CREATE INDEX IF NOT EXISTS ix_ml_outcome_values_refresh_run_id
  ON public.ml_outcome_values (refresh_run_id);

'@
    $full = $ddl + $newBase + ";`n`n" + $newV6 + ";`n`nCOMMIT;`n"
    $migFile = Join-Path $script:tmp "migration.sql"
    [System.IO.File]::WriteAllText($migFile, $full, (New-Object System.Text.UTF8Encoding($false)))
    Say ("migration written : " + $migFile)
    Say ("bytes             : " + $full.Length)
    Say ""
    Say "self-check on the generated SQL:"
    $checks = @(
        @{ n = "base stamps feature values"; s = "SET refresh_run_id = v_run_id" },
        @{ n = "v6 stamps with the base run"; s = "SET refresh_run_id = v_base.run_id" },
        @{ n = "only unowned rows stamped";   s = "refresh_run_id IS NULL" },
        @{ n = "base engine identity";        s = "engine_version = 'base'" },
        @{ n = "v6 engine identity";          s = "engine_version = 'v6'" },
        @{ n = "single-flight lock";          s = "pg_advisory_xact_lock" }
    )
    foreach ($c in $checks) {
        $n = ([regex]::Matches($full, [regex]::Escape($c.s))).Count
        if ($n -ge 1) { Say ("  [OK]   " + $c.n.PadRight(30) + $n) }
        else {
            if ($c.s -eq "pg_advisory_xact_lock" -and $hasLock) {
                Say ("  [OK]   " + $c.n.PadRight(30) + "already present in the live body")
            } else {
                Say ("  [FAIL] " + $c.n); $bad = $bad + 1
            }
        }
    }
    if ($bad -gt 0) { throw "selfcheck" }

    if (-not $Apply) {
        Rule "REPORT ONLY - NOTHING MIGRATED"
        Say "The generated migration is at the path above. Inspect it, then re-run"
        Say "with -Apply."
        Rule "RESULT"
        Say "[OK] guards pass and the migration generated cleanly. Nothing changed."
        exit 0
    }

    Rule "APPLY - ONE TRANSACTION"
    $ap = Invoke-Sql -Tag "apply" -Sql $full
    if ($ap.ExitCode -ne 0 -or $ap.Error -match "(?i)(ERROR|FATAL):") {
        Say ("[FAIL] migration exited " + $ap.ExitCode)
        Say $ap.Error
        Say "It was one transaction, so nothing changed."
        throw "apply"
    }
    Say "[OK] migrated inside one transaction"

    Rule "VERIFY THE SCHEMA AND THE ENGINE BODIES"
    $ver = Invoke-Sql -Tag "verify" -Sql @'
\pset border 2
SELECT 'refresh_run_id on ml_feature_values' AS check_name, count(*) AS found, 1 AS required
FROM information_schema.columns
WHERE table_schema='public' AND table_name='ml_feature_values' AND column_name='refresh_run_id'
UNION ALL
SELECT 'refresh_run_id on ml_outcome_values', count(*), 1
FROM information_schema.columns
WHERE table_schema='public' AND table_name='ml_outcome_values' AND column_name='refresh_run_id'
UNION ALL
SELECT 'engine_key and engine_version on the run table', count(*), 2
FROM information_schema.columns
WHERE table_schema='public' AND table_name='ml_feature_store_refresh_runs'
  AND column_name IN ('engine_key','engine_version')
UNION ALL
SELECT 'foreign keys created', count(*), 2
FROM pg_constraint
WHERE conname IN ('fk_ml_feature_values_refresh_run','fk_ml_outcome_values_refresh_run')
UNION ALL
SELECT 'base function stamps its run', count(*), 1
FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace
WHERE n.nspname='public' AND p.proname='ppiq_ml_refresh_feature_store' AND p.prokind='f'
  AND pg_get_functiondef(p.oid) LIKE '%SET refresh_run_id = v_run_id%'
UNION ALL
SELECT 'v6 function stamps the base run', count(*), 1
FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace
WHERE n.nspname='public' AND p.proname='ppiq_ml_refresh_feature_store_v6' AND p.prokind='f'
  AND pg_get_functiondef(p.oid) LIKE '%SET refresh_run_id = v_base.run_id%'
UNION ALL
SELECT 'stale rows given manufactured lineage', count(*), 0
FROM public.ml_feature_values WHERE refresh_run_id IS NOT NULL;
'@
    Say $ver.Output
    $bad = $bad + (Check-Table -Output $ver.Output -Label "migration")
}
catch {
    $bad = $bad + 1
}
finally {
    Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue
}

Rule "RESULT"
if ($bad -gt 0) {
    Say ("[FAIL] " + $bad + " problem(s). If the failure was in a guard, nothing was")
    Say "       touched. If it was in the migration, it was one transaction."
    exit 1
}
Say "[OK] lineage migration complete."
Say ""
Say "The 346,973 stale rows still have NULL lineage, deliberately: they describe"
Say "the pre-T024 plant and must not be given manufactured provenance. They are"
Say "cleared next, and the authenticated refresh produces their replacement WITH"
Say "lineage attached by the engine itself."
Say ""
Say "NOT NULL is enforced only after that path is proven."
exit 0

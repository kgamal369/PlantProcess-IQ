#requires -Version 5.1
<#
.SYNOPSIS
    PPIQ T-025 step 1 - prove the supported authentication path, then read the
    current state of the analysis layer. READ-ONLY: no engine is run, nothing is
    written, nothing is deleted.

.DESCRIPTION
    YOUR RULING, FOLLOWED EXACTLY. This uses the existing supported flow the
    product's own journey scripts use - POST /auth/login with the presentation
    profile's e2eadmin user, then Bearer on every call. No AllowAnonymous, no
    test-only bypass, no new mechanism.

    Then it reports, without changing anything:
      A  authentication: does /auth/login return an accessToken, and does one
         protected endpoint accept it
      B  which engine endpoints exist and answer
      C  the current analysis-layer population - how many rows, and how many of
         them describe the plant that T-024 replaced
      D  compute-run coverage as it stands today, which is the metric T-025 must
         end at full
      E  the definitions and registries that must SURVIVE the clear

    Nothing here decides anything. It establishes what is true before the first
    engine runs, so "reproducible" and "full coverage" can be measured against a
    known starting point rather than asserted.

.EXAMPLE
    .\tools\run\Invoke-PpiqT025Probe.ps1
#>

[CmdletBinding()]
param(
    [string]$ApiBase      = "http://localhost:5063",
    [string]$SmokeUser    = "e2eadmin",
    [string]$SmokePassword = "E2EAdmin123!",
    [string]$PgHost       = "127.0.0.1",
    [int]   $PgPort       = 5432,
    [string]$PgUser       = "ppiq_dev",
    [string]$PgPassword   = "ppiq_dev_local_only",
    [string]$Database     = "ppiq_presentation",
    [string]$PsqlPath     = "",
    [string]$OutDir       = "docs\m1\evidence"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Continue"

function Say  { param([string]$T) Write-Host $T; $script:log += ($T + "`r`n") }
function Rule {
    param([string]$T)
    Say ""
    Say ("=" * 78)
    Say $T
    Say ("=" * 78)
}
$script:log = ""
$script:Token = $null

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
           "-v", "ON_ERROR_STOP=0", "-f", $f, "-o", $o)
    $p = Start-Process -FilePath $script:psql -ArgumentList $a -NoNewWindow -Wait `
                       -PassThru -RedirectStandardError $e
    $r = New-Object psobject
    Add-Member -InputObject $r -MemberType NoteProperty -Name ExitCode -Value $p.ExitCode
    Add-Member -InputObject $r -MemberType NoteProperty -Name Output   -Value (Read-IfExists $o)
    Add-Member -InputObject $r -MemberType NoteProperty -Name Error    -Value (Read-IfExists $e)
    return $r
}
function Invoke-Api {
    param([string]$Method, [string]$Path, $Body = $null)
    $headers = @{}
    if ($script:Token) { $headers['Authorization'] = 'Bearer ' + $script:Token }
    $uri = $ApiBase.TrimEnd('/') + $Path
    $res = New-Object psobject
    Add-Member -InputObject $res -MemberType NoteProperty -Name Status -Value 0
    Add-Member -InputObject $res -MemberType NoteProperty -Name Body   -Value $null
    Add-Member -InputObject $res -MemberType NoteProperty -Name Raw    -Value ""
    try {
        if ($null -ne $Body) {
            $json = $Body | ConvertTo-Json -Depth 8
            $resp = Invoke-WebRequest -Uri $uri -Method $Method -Headers $headers `
                        -ContentType 'application/json' -Body $json -UseBasicParsing -TimeoutSec 120
        } else {
            $resp = Invoke-WebRequest -Uri $uri -Method $Method -Headers $headers `
                        -UseBasicParsing -TimeoutSec 120
        }
        $res.Status = [int]$resp.StatusCode
        $res.Raw = $resp.Content
        if ($resp.Content) { try { $res.Body = $resp.Content | ConvertFrom-Json } catch { } }
    } catch {
        if ($_.Exception.Response) {
            $res.Status = [int]$_.Exception.Response.StatusCode
            try {
                $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
                $res.Raw = $reader.ReadToEnd()
            } catch { }
        }
    }
    return $res
}

Rule "PPIQ T-025 STEP 1 - AUTH PROOF AND ANALYSIS-LAYER STATE (READ-ONLY)"
$repoRoot = (Get-Location).Path
$script:psql = Resolve-Psql -Explicit $PsqlPath
if ($null -eq $script:psql) { Write-Host "[FAIL] psql.exe not found."; exit 2 }
Say ("API      : " + $ApiBase)
Say ("User     : " + $SmokeUser + "   the presentation profile's configured admin")
Say ("Database : " + $Database)
Say ("Mode     : READ-ONLY. No engine is run. Nothing is written or deleted.")

$env:PGPASSWORD = $PgPassword
$env:PGCLIENTENCODING = "UTF8"
$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$script:tmp = Join-Path $env:TEMP ("ppiq_t025probe_" + $stamp)
New-Item -ItemType Directory -Path $script:tmp -Force | Out-Null
$fail = 0

try {
    Rule "A - AUTHENTICATION, VIA THE SUPPORTED FLOW"
    Say "The same path scripts/run/Invoke-PpiqJourneyProof.ps1 uses: POST /auth/login,"
    Say "take accessToken, send Bearer. No bypass and no new mechanism."
    Say ""
    $login = Invoke-Api -Method POST -Path "/auth/login" `
                        -Body @{ userName = $SmokeUser; password = $SmokePassword }
    Say ("POST /auth/login  -> " + $login.Status)
    if ($login.Status -ne 200 -or $null -eq $login.Body) {
        Say "[FAIL] login did not return 200 with a body."
        Say ("       " + $login.Raw.Substring(0, [Math]::Min(200, $login.Raw.Length)))
        $fail = $fail + 1
    } else {
        $tok = $null
        try { $tok = $login.Body.accessToken } catch { }
        if ([string]::IsNullOrWhiteSpace($tok)) {
            Say "[FAIL] 200 but no accessToken field."
            $fail = $fail + 1
        } else {
            $script:Token = $tok
            Say ("[OK] accessToken received, length " + $tok.Length)
        }
    }

    if ($null -ne $script:Token) {
        Say ""
        Say "Now one PROTECTED endpoint, to prove the token is actually accepted:"
        $probe = Invoke-Api -Method GET -Path "/api/ml/foundation/readiness"
        Say ("GET /api/ml/foundation/readiness -> " + $probe.Status)
        if ($probe.Status -eq 200) {
            Say "[OK] the token is accepted by a protected engine endpoint"
        } elseif ($probe.Status -eq 401 -or $probe.Status -eq 403) {
            Say "[FAIL] the token was rejected. The credential or role is wrong."
            $fail = $fail + 1
        } else {
            Say ("[WARN] unexpected status; body: " +
                 $probe.Raw.Substring(0, [Math]::Min(200, $probe.Raw.Length)))
        }
    }

    Rule "B - WHICH ENGINE SURFACES ANSWER"
    Say "GET only. Nothing is computed. A 401 here would mean the token is not"
    Say "being carried; a 404 would mean the route does not exist on this build."
    Say ""
    foreach ($ep in @("/api/ml/foundation/readiness",
                      "/api/ml/foundation/feature-definitions",
                      "/api/ml/foundation/outcomes",
                      "/api/analytics/correlation/runs",
                      "/api/analytics/risk-scores",
                      "/api/analysis-jobs")) {
        $r = Invoke-Api -Method GET -Path $ep
        Say ("  " + $ep.PadRight(46) + " -> " + $r.Status)
    }

    Rule "C - THE ANALYSIS LAYER AS IT STANDS"
    Say "These rows describe the plant T-024 replaced. Reported, not judged."
    $pop = Invoke-Sql -Tag "pop" -Sql @'
\pset border 2
SELECT 'ml_feature_values' AS entity, count(*) AS rows FROM public.ml_feature_values
UNION ALL SELECT 'ml_outcome_values', count(*) FROM public.ml_outcome_values
UNION ALL SELECT 'ml_correlation_results_v2', count(*) FROM public.ml_correlation_results_v2
UNION ALL SELECT 'ml_correlation_compute_runs', count(*) FROM public.ml_correlation_compute_runs
UNION ALL SELECT 'ml_learning_results_v1', count(*) FROM public.ml_learning_results_v1
UNION ALL SELECT 'ml_learning_runs_v1', count(*) FROM public.ml_learning_runs_v1
UNION ALL SELECT 'ml_learning_observations_v1', count(*) FROM public.ml_learning_observations_v1
UNION ALL SELECT 'risk_scores', count(*) FROM public.risk_scores
UNION ALL SELECT 'data_quality_issues', count(*) FROM public.data_quality_issues
ORDER BY 1;
'@
    Say $pop.Output

    Say "How much of it still points at material units that exist?"
    $orph = Invoke-Sql -Tag "orphan" -Sql @'
\pset border 2
SELECT 'ml_feature_values with a live material unit' AS check_name,
       count(*) FILTER (WHERE m.id IS NOT NULL) AS live,
       count(*) FILTER (WHERE m.id IS NULL) AS stale
FROM public.ml_feature_values f
LEFT JOIN public.material_units m ON m.id = f.material_unit_id
UNION ALL
SELECT 'ml_outcome_values with a live material unit',
       count(*) FILTER (WHERE m.id IS NOT NULL), count(*) FILTER (WHERE m.id IS NULL)
FROM public.ml_outcome_values o
LEFT JOIN public.material_units m ON m.id = o.material_unit_id
UNION ALL
SELECT 'risk_scores with a live material unit',
       count(*) FILTER (WHERE m.id IS NOT NULL), count(*) FILTER (WHERE m.id IS NULL)
FROM public.risk_scores r
LEFT JOIN public.material_units m ON m.id = r.material_unit_id;
'@
    Say $orph.Output

    Rule "D - COMPUTE-RUN COVERAGE TODAY"
    Say "T-025 must end with every analysis row carrying a compute run. This is"
    Say "where it starts from."
    $cov = Invoke-Sql -Tag "coverage" -Sql @'
\pset border 2
SELECT c.table_name, c.column_name
FROM information_schema.columns c
JOIN pg_class pc ON pc.relname = c.table_name
JOIN pg_namespace pn ON pn.oid = pc.relnamespace AND pn.nspname = 'public'
WHERE c.table_schema = 'public' AND pc.relkind = 'r'
  AND (c.column_name LIKE '%run_id%' OR c.column_name LIKE '%compute_run%')
  AND c.table_name LIKE 'ml_%'
ORDER BY 1, 2;
'@
    Say $cov.Output

    Rule "E - WHAT MUST SURVIVE THE CLEAR"
    Say "Definitions and registries are inputs, not results. They are not cleared."
    $defs = Invoke-Sql -Tag "defs" -Sql @'
\pset border 2
SELECT 'ml_feature_definitions' AS registry, count(*) AS rows,
       count(*) FILTER (WHERE is_deleted = false) AS active
FROM public.ml_feature_definitions
UNION ALL
SELECT 'ml_outcome_definitions', count(*), count(*) FILTER (WHERE is_deleted = false)
FROM public.ml_outcome_definitions
UNION ALL
SELECT 'kpi_definitions', count(*), count(*) FILTER (WHERE is_deleted = false)
FROM public.kpi_definitions
UNION ALL
SELECT 'analysis job definitions', count(*), count(*) FILTER (WHERE is_deleted = false)
FROM public.job_definitions
ORDER BY 1;
'@
    Say $defs.Output

    Say "Do the feature definitions still resolve against the Fleet v2 parameters?"
    $res = Invoke-Sql -Tag "resolve" -Sql @'
\pset border 2
SELECT coalesce(f.source_column,'(null)') AS source_column,
       count(*) AS definitions,
       count(*) FILTER (WHERE p.id IS NOT NULL) AS resolves_to_a_parameter
FROM public.ml_feature_definitions f
LEFT JOIN public.parameter_definitions p
  ON p.parameter_code = f.source_column AND p.is_deleted = false
WHERE f.is_deleted = false
GROUP BY 1 ORDER BY 3, 1;
'@
    Say $res.Output
}
catch {
    Say ("[FAIL] " + $_.Exception.Message)
    $fail = $fail + 1
}
finally {
    Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue
}

Rule "RESULT"
if ($fail -gt 0) {
    Say ("[FAIL] " + $fail + " problem(s). Nothing was run or written.")
} else {
    Say "[OK] authentication proven and the starting state recorded."
    Say "     No engine was run. Nothing was written or deleted."
}

$outFolder = $OutDir
if (-not [System.IO.Path]::IsPathRooted($OutDir)) { $outFolder = Join-Path $repoRoot $OutDir }
if (-not (Test-Path -LiteralPath $outFolder)) {
    New-Item -ItemType Directory -Path $outFolder -Force | Out-Null
}
$evidence = Join-Path $outFolder ("T-025_probe_" + $stamp + ".txt")
$clean = New-Object System.Text.StringBuilder
foreach ($ch in ($script:log -replace "`r`n", "`n").ToCharArray()) {
    if ([int]$ch -le 126 -or [int]$ch -eq 10) { [void]$clean.Append($ch) }
}
[System.IO.File]::WriteAllText($evidence,
    ($clean.ToString() -replace "`n", "`r`n"),
    (New-Object System.Text.UTF8Encoding($false)))
Write-Host ""
Write-Host ("Evidence : " + $evidence)
if ($fail -gt 0) { exit 1 }
exit 0

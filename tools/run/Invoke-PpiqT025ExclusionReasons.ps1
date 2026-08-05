#requires -Version 5.1
<#
.SYNOPSIS
    PPIQ T-025 exclusion-reason readout. READ ONLY, credentials in the script,
    nothing to type. Confirms from the database why every parameter was excluded.

.DESCRIPTION
    I asked you to run a bare psql -c last turn and it prompted for a password.
    That was my rule to follow: credentials go IN the script, diagnostics ship as
    a runner, never as an ad-hoc command. "It is only one query" is exactly how
    the exception gets in. This is the same query, done properly.

    WHAT IT CONFIRMS. The engine persists one row per EXCLUDED feature carrying
    the reason, so the answer is already in the database and needs no refresh and
    no engine call. The prediction from the source is:

      MethodSelector.Select(Numeric, Categorical) -> NotApplicable
      because the matrix only covers Numeric/Numeric (Spearman or MutualInformation),
      Binary/Numeric (PointBiserial) and Categorical/Categorical (CramersV).

    defect.severity is 'ordinal' and MapOutcome sends ordinal to Categorical.
    defect.class is 'multinomial', also Categorical. All 26 features are numeric.
    So no pairing available in the current canonical population has a method.

    REFUTED IF the reasons come back as anything else - insufficient pairs, zero
    variance, or collinearity. Then the method-matrix reading is wrong and the
    cause is elsewhere. The script prints whatever is there.

.EXAMPLE
    .\tools\run\Invoke-PpiqT025ExclusionReasons.ps1
#>

[CmdletBinding()]
param(
    [string]$PgHost     = "127.0.0.1",
    [int]   $PgPort     = 5432,
    [string]$PgUser     = "ppiq_dev",
    [string]$PgPassword = "ppiq_dev_local_only",
    [string]$Database   = "ppiq_presentation",
    [string]$PsqlPath   = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Continue"

$script:log = ""
function Say  { param([string]$T) Write-Host $T; $script:log += ($T + "`r`n") }
function Rule { param([string]$T) Say ""; Say ("=" * 78); Say $T; Say ("=" * 78) }

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
    # -w forbids a prompt outright. A script meant to run bare must never be able
    # to sit waiting for input.
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

Rule "PPIQ T-025 EXCLUSION REASONS - READ ONLY"
$repoRoot = (Get-Location).Path
$script:psql = Resolve-Psql -Explicit $PsqlPath
if ($null -eq $script:psql) { Write-Host "[FAIL] psql.exe not found."; exit 2 }
$env:PGPASSWORD = $PgPassword
$env:PGCLIENTENCODING = "UTF8"
$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$script:tmp = Join-Path $env:TEMP ("ppiq_t025ex_" + $stamp)
New-Item -ItemType Directory -Path $script:tmp -Force | Out-Null
Say ("Database : " + $Database)
Say "No engine call, no write, no refresh."

try {
    Rule "1 - WHY EVERY PARAMETER WAS EXCLUDED, BY OUTCOME"
    $r = Invoke-Sql -Tag "reasons" -Sql @"
SELECT c.target_outcome_key AS outcome,
       c.status,
       r.method,
       COALESCE(r.evidence_json->>'reason', '(no reason recorded)') AS reason,
       count(*) AS features
FROM public.ml_correlation_results_v2 r
JOIN public.ml_correlation_compute_runs c ON c.id = r.compute_run_id
GROUP BY 1, 2, 3, 4
ORDER BY 1, 5 DESC;
"@
    if ($r.ExitCode -ne 0) { Say ("[FAIL] " + $r.Error.Trim()) }
    Say $r.Output

    Rule "2 - THE SHAPE THAT DECIDES THE METHOD"
    Say "MethodSelector covers Numeric/Numeric, Binary/Numeric and"
    Say "Categorical/Categorical. Everything else returns NotApplicable."
    Say "MapOutcome sends 'multinomial' AND 'ordinal' to Categorical."
    $r2 = Invoke-Sql -Tag "shapes" -Sql @"
SELECT d.outcome_key,
       d.outcome_type,
       CASE WHEN lower(d.outcome_type) IN ('multinomial','ordinal') THEN 'Categorical'
            WHEN lower(d.outcome_type) = 'binary' THEN 'Binary'
            ELSE 'Numeric' END AS engine_outcome_type,
       COALESCE(v.rows, 0) AS materialised_rows
FROM public.ml_outcome_definitions d
LEFT JOIN (SELECT lower(outcome_key) AS k, count(*) AS rows
           FROM public.ml_outcome_values GROUP BY 1) v
       ON v.k = lower(d.outcome_key)
WHERE d.is_deleted = false AND d.status = 'Active'
ORDER BY d.outcome_key;
"@
    Say $r2.Output

    $r3 = Invoke-Sql -Tag "feattypes" -Sql @"
SELECT fd.value_type,
       count(DISTINCT fv.feature_key) AS feature_keys
FROM public.ml_feature_values fv
JOIN public.ml_feature_definitions fd ON lower(fd.feature_key) = lower(fv.feature_key)
                                     AND fd.is_deleted = false
WHERE fv.grain = 'coil'
GROUP BY fd.value_type
ORDER BY fd.value_type;
"@
    Say "Feature value types at grain coil - the left side of every pairing:"
    Say $r3.Output

    Rule "3 - THE VERDICT"
    Say "If section 1 shows NotApplicable with an undefined-statistic or"
    Say "unsupported-shape reason across every feature, the cause is the method"
    Say "matrix and NOT the data: a numeric parameter against a categorical"
    Say "outcome has no selectable test. Every materialised outcome is"
    Say "categorical and every feature is numeric, so no pairing available in"
    Say "the current canonical population can produce a finding."
    Say ""
    Say "If instead the reasons say insufficient paired samples, zero variance or"
    Say "collinearity, the method-matrix reading is WRONG and the cause is"
    Say "elsewhere. Report what is printed, not what was expected."
}
finally {
    Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue
}

$outFolder = Join-Path $repoRoot "docs\m1\evidence"
if (-not (Test-Path -LiteralPath $outFolder)) {
    New-Item -ItemType Directory -Path $outFolder -Force | Out-Null
}
$ev = Join-Path $outFolder ("T-025_exclusion_reasons_" + $stamp + ".txt")
$clean = New-Object System.Text.StringBuilder
foreach ($ch in ($script:log -replace "`r`n", "`n").ToCharArray()) {
    if ([int]$ch -le 126 -or [int]$ch -eq 10) { [void]$clean.Append($ch) }
}
[System.IO.File]::WriteAllText($ev, ($clean.ToString() -replace "`n", "`r`n"),
    (New-Object System.Text.UTF8Encoding($false)))
Write-Host ""
Write-Host ("Evidence : " + $ev)
exit 0

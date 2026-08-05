#requires -Version 5.1
<#
.SYNOPSIS
    PPIQ T-029 supplement - name the parameters behind the physical layer's 1,167
    out-of-range observations. READ ONLY, one query.

.DESCRIPTION
    The v2 audit reported 1,167 observations breaching their own declared
    expected_min_value / expected_max_value, and reported it as a bare total. A
    total nobody can act on is not a finding, so this names each offender with the
    declared range it breached and how far outside it went.

    WHY THIS IS A SEPARATE RUNNER. The breakdown was written into the v2 patch and
    silently did not apply - the insertion was anchored on a two-line string using
    LF against a CRLF file, so it matched nothing and failed without an error. A
    small separate query is cheaper and safer than re-patching a working audit.

    NO RANGE IS INVENTED HERE. Every bound comes from the parameter's own
    definition row.

.EXAMPLE
    .\tools\run\Invoke-PpiqT029RangeBreakdown.ps1
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
    [System.IO.File]::WriteAllText($f, $Sql, (New-Object System.Text.UTF8Encoding($false)))
    $o = Join-Path $script:tmp ($Tag + ".out")
    $e = Join-Path $script:tmp ($Tag + ".err")
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

Rule "PPIQ T-029 SUPPLEMENT - WHO BREACHES THEIR OWN DECLARED RANGE"
$repoRoot = (Get-Location).Path
$script:psql = Resolve-Psql -Explicit $PsqlPath
if ($null -eq $script:psql) { Write-Host "[FAIL] psql.exe not found."; exit 2 }
$env:PGPASSWORD = $PgPassword
$env:PGCLIENTENCODING = "UTF8"
$env:PGOPTIONS = "-c default_transaction_read_only=on"
$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$script:tmp = Join-Path $env:TEMP ("ppiq_t029s_" + $stamp)
New-Item -ItemType Directory -Path $script:tmp -Force | Out-Null
Say ("Database : " + $Database)

try {
    $ro = Invoke-Sql -Tag "ro" -Sql "\pset border 2`nSHOW transaction_read_only;"
    Say $ro.Output

    Rule "OFFENDERS, WITH THEIR OWN DECLARED BOUNDS"
    Say "Every bound below is the parameter's own expected_min_value and"
    Say "expected_max_value. Nothing is invented."
    $r = Invoke-Sql -Tag "break" -Sql @"
\pset border 2
WITH v AS (
  SELECT pd.parameter_code, pd.unit_of_measure,
         pd.expected_min_value AS lo, pd.expected_max_value AS hi,
         po.numeric_value AS val
  FROM public.parameter_observations po
  JOIN public.parameter_definitions pd ON pd.id = po.parameter_definition_id AND pd.is_deleted = false
  WHERE po.is_deleted = false AND po.numeric_value IS NOT NULL
    AND pd.expected_min_value IS NOT NULL AND pd.expected_max_value IS NOT NULL
), t AS (
  SELECT parameter_code, unit_of_measure, lo, hi, count(*) AS total,
         count(*) FILTER (WHERE val < lo OR val > hi) AS offending,
         count(*) FILTER (WHERE val < lo) AS below,
         count(*) FILTER (WHERE val > hi) AS above,
         min(val) AS obs_min, max(val) AS obs_max
  FROM v GROUP BY parameter_code, unit_of_measure, lo, hi
)
SELECT parameter_code, unit_of_measure,
       round(lo::numeric, 3) AS declared_min, round(hi::numeric, 3) AS declared_max,
       round(obs_min::numeric, 3) AS observed_min, round(obs_max::numeric, 3) AS observed_max,
       total, offending, below, above,
       round((100.0 * offending / nullif(total, 0))::numeric, 3) AS pct_offending
FROM t WHERE offending > 0
ORDER BY offending DESC;
"@
    if ($r.ExitCode -ne 0) { Say ("[FAIL] " + $r.Error.Trim()) }
    Say $r.Output

    Rule "HOW FAR OUTSIDE - THE WORST BREACHES"
    Say "A breach of a fraction of a percent is a tail; a breach of many percent is"
    Say "a wrong range or a wrong generator. The distinction decides who owns it."
    $r2 = Invoke-Sql -Tag "extent" -Sql @"
\pset border 2
WITH v AS (
  SELECT pd.parameter_code, pd.expected_min_value AS lo, pd.expected_max_value AS hi,
         po.numeric_value AS val
  FROM public.parameter_observations po
  JOIN public.parameter_definitions pd ON pd.id = po.parameter_definition_id AND pd.is_deleted = false
  WHERE po.is_deleted = false AND po.numeric_value IS NOT NULL
    AND pd.expected_min_value IS NOT NULL AND pd.expected_max_value IS NOT NULL
    AND (po.numeric_value < pd.expected_min_value OR po.numeric_value > pd.expected_max_value)
)
SELECT parameter_code,
       count(*) AS offending,
       round(max(CASE WHEN val < lo THEN (lo - val) / nullif(hi - lo, 0) * 100.0
                      ELSE (val - hi) / nullif(hi - lo, 0) * 100.0 END)::numeric, 2)
         AS worst_overshoot_pct_of_range
FROM v GROUP BY parameter_code ORDER BY 3 DESC;
"@
    Say $r2.Output

    Rule "TOTAL, RECONCILED AGAINST THE AUDIT"
    $r3 = Invoke-Sql -Tag "total" -Sql @"
\pset border 2
SELECT count(*) AS total_offending
FROM public.parameter_observations po
JOIN public.parameter_definitions pd ON pd.id = po.parameter_definition_id AND pd.is_deleted = false
WHERE po.is_deleted = false AND po.numeric_value IS NOT NULL
  AND pd.expected_min_value IS NOT NULL AND pd.expected_max_value IS NOT NULL
  AND (po.numeric_value < pd.expected_min_value OR po.numeric_value > pd.expected_max_value);
"@
    Say $r3.Output
    Say "This must equal the 1,167 the v2 audit reported. If it does not, one of the"
    Say "two queries is wrong and neither number should be quoted."
}
finally {
    Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue
    Remove-Item Env:\PGOPTIONS -ErrorAction SilentlyContinue
}

$outFolder = Join-Path $repoRoot "docs\m1\evidence"
if (-not (Test-Path -LiteralPath $outFolder)) {
    New-Item -ItemType Directory -Path $outFolder -Force | Out-Null
}
$ev = Join-Path $outFolder ("T-029_range_breakdown_" + $stamp + ".txt")
$clean = New-Object System.Text.StringBuilder
foreach ($ch in ($script:log -replace "`r`n", "`n").ToCharArray()) {
    if ([int]$ch -le 126 -or [int]$ch -eq 10) { [void]$clean.Append($ch) }
}
[System.IO.File]::WriteAllText($ev, ($clean.ToString() -replace "`n", "`r`n"),
    (New-Object System.Text.UTF8Encoding($false)))
Write-Host ""
Write-Host ("Evidence : " + $ev)
exit 0

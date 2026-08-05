#requires -Version 5.1
<#
.SYNOPSIS
    PPIQ T-027 coverage ledger runner. READ ONLY. Classifies all 104 historical
    phenomena against what the generated plant actually contains.

.DESCRIPTION
    T-027 requires measured effects to be tested against targets PREDECLARED in
    T-008 and refined in T-015, before the data existed. Its own text says
    "Writing the band from the data and then testing the data against it is a
    self-fulfilling test and proves nothing."

    So no number here is invented. This runner reads the identifiers the plant
    ACTUALLY has - every parameter_code in parameter_definitions plus every column
    of the canonical tables - and hands them to the classifier. The available-set
    is queried, never typed, because a hand-written list that omits a real column
    would report a false SOURCE_VARIABLE_NOT_MATERIALISED and the ledger would be
    a lie in the safe-looking direction.

    THE CLASSIFIER REFUSES to run on an empty available-set for the same reason.

    NO ANALYTICAL EXECUTION. No feature store refresh, no correlation, no risk, no
    learning. The only database access is two catalogue reads.

    WHAT IT PRODUCES
      docs/m1/phenomena/T-027_coverage_ledger.csv   one row per phenomenon,
          carrying the original id, assertion, matrix status, required variables,
          required statistic, variable availability, classification and reason
      docs/m1/evidence/T-027_coverage_report_<stamp>.txt   the totals and the two
          execution clauses of the T-027 validation, measured

.EXAMPLE
    .\tools\run\Invoke-PpiqT027Ledger.ps1
#>

[CmdletBinding()]
param(
    [string]$Matrix     = "docs\m1\evidence\phenomena_widget_matrix.csv",
    [string]$Classifier = "Backend\tools\t027_coverage_ledger.py",
    [string]$PgHost     = "127.0.0.1",
    [int]   $PgPort     = 5432,
    [string]$PgUser     = "ppiq_dev",
    [string]$PgPassword = "ppiq_dev_local_only",
    [string]$Database   = "ppiq_presentation",
    [string]$PsqlPath   = "",
    [string]$PythonPath = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Continue"

$script:log = ""
$code = 3
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
function Resolve-Python {
    param([string]$Explicit)
    if (-not [string]::IsNullOrWhiteSpace($Explicit)) {
        if (Test-Path -LiteralPath $Explicit) { return $Explicit }
        return $null
    }
    foreach ($n in @("python.exe", "python3.exe", "py.exe")) {
        $c = Get-Command $n -ErrorAction SilentlyContinue
        if ($null -ne $c) { return $c.Source }
    }
    return $null
}
function Invoke-Sql {
    param([string]$Sql, [string]$Tag, [string]$OutFile)
    $f = Join-Path $script:tmp ($Tag + ".sql")
    [System.IO.File]::WriteAllText($f, $Sql, (New-Object System.Text.UTF8Encoding($false)))
    $e = Join-Path $script:tmp ($Tag + ".err")
    $a = @("-X", "-w", "-A", "-t", "-h", $PgHost, "-p", "$PgPort", "-U", $PgUser,
           "-d", $Database, "-v", "ON_ERROR_STOP=1", "-f", $f, "-o", $OutFile)
    $p = Start-Process -FilePath $script:psql -ArgumentList $a -NoNewWindow -Wait `
                       -PassThru -RedirectStandardError $e
    $r = New-Object psobject
    Add-Member -InputObject $r -MemberType NoteProperty -Name ExitCode -Value $p.ExitCode
    Add-Member -InputObject $r -MemberType NoteProperty -Name Error    -Value (Read-IfExists $e)
    return $r
}

Rule "PPIQ T-027 COVERAGE LEDGER - READ ONLY"
$repoRoot = (Get-Location).Path
$script:psql = Resolve-Psql -Explicit $PsqlPath
if ($null -eq $script:psql) { Write-Host "[FAIL] psql.exe not found."; exit 2 }
$python = Resolve-Python -Explicit $PythonPath
if ($null -eq $python) { Write-Host "[FAIL] python not found on PATH."; exit 2 }
$env:PGPASSWORD = $PgPassword
$env:PGCLIENTENCODING = "UTF8"
$env:PGOPTIONS = "-c default_transaction_read_only=on"
$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$script:tmp = Join-Path $env:TEMP ("ppiq_t027_" + $stamp)
New-Item -ItemType Directory -Path $script:tmp -Force | Out-Null
Say ("Database : " + $Database)

try {
    Rule "1 - INPUTS"
    if (-not (Test-Path -LiteralPath $Matrix))     { Say ("[FAIL] matrix not found: " + $Matrix); exit 2 }
    if (-not (Test-Path -LiteralPath $Classifier)) { Say ("[FAIL] classifier not found: " + $Classifier); exit 2 }
    Say ("  matrix     : " + $Matrix)
    Say ("  classifier : " + $Classifier)

    # PROVEN, NOT ASSERTED - the same lesson as the phenomenon harness.
    $roOut = Join-Path $script:tmp "readonly.txt"
    $ro = Invoke-Sql -Tag "ro" -OutFile $roOut -Sql "SHOW transaction_read_only;"
    $roValue = (Read-IfExists $roOut).Trim()
    Say ("  server reports transaction_read_only = " + $roValue + " (required on)")
    if ($roValue -ne "on") { Say "[STOP] the connection is not read-only."; exit 2 }

    Rule "2 - WHAT THE PLANT ACTUALLY CONTAINS"
    Say "Queried, never typed. A hand-written list that omitted a real column would"
    Say "report a false SOURCE_VARIABLE_NOT_MATERIALISED and the ledger would be"
    Say "wrong in the direction that looks safe."
    $availFile = Join-Path $script:tmp "available.txt"
    $r = Invoke-Sql -Tag "available" -OutFile $availFile -Sql @"
SELECT lower(parameter_code) FROM public.parameter_definitions WHERE is_deleted = false
UNION
SELECT lower(column_name) FROM information_schema.columns
WHERE table_schema = 'public'
  AND table_name IN ('material_units','quality_events','defect_catalogs',
                     'parameter_observations','parameter_definitions',
                     'downtime_events','equipment','sites','process_events',
                     'process_step_executions','genealogy_edges','routes','route_steps')
UNION SELECT 'defect_rate' UNION SELECT 'defect_count'
ORDER BY 1;
"@
    if ($r.ExitCode -ne 0) { Say ("[FAIL] " + $r.Error.Trim()); exit 2 }
    $availLines = @(Get-Content -LiteralPath $availFile | Where-Object { $_.Trim() -ne "" })
    Say ("  identifiers available to a population_query : " + $availLines.Count)
    $codeOut = Join-Path $script:tmp "codes.txt"
    Invoke-Sql -Tag "codes" -OutFile $codeOut -Sql @"
SELECT count(*) FROM public.parameter_definitions WHERE is_deleted = false;
"@ | Out-Null
    Say ("  of which parameter_definitions codes        : " + (Read-IfExists $codeOut).Trim())

    Rule "3 - CLASSIFY"
    $phenDir = Join-Path $repoRoot "docs\m1\phenomena"
    $evDir   = Join-Path $repoRoot "docs\m1\evidence"
    foreach ($d in @($phenDir, $evDir)) {
        if (-not (Test-Path -LiteralPath $d)) { New-Item -ItemType Directory -Path $d -Force | Out-Null }
    }
    $ledger = Join-Path $phenDir "T-027_coverage_ledger.csv"
    $report = Join-Path $evDir ("T-027_coverage_report_" + $stamp + ".txt")
    & $python $Classifier "--matrix" $Matrix "--available" $availFile `
              "--ledger-out" $ledger "--report-out" $report
    $code = $LASTEXITCODE
    Say ""
    Say ("  classifier exit code : " + $code)
    Say ("  ledger : " + $ledger)
    Say ("  report : " + $report)
}
finally {
    Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue
    Remove-Item Env:\PGOPTIONS -ErrorAction SilentlyContinue
}

$ev = Join-Path $repoRoot ("docs\m1\evidence\T-027_ledger_run_" + $stamp + ".txt")
$clean = New-Object System.Text.StringBuilder
foreach ($ch in ($script:log -replace "`r`n", "`n").ToCharArray()) {
    if ([int]$ch -le 126 -or [int]$ch -eq 10) { [void]$clean.Append($ch) }
}
[System.IO.File]::WriteAllText($ev, ($clean.ToString() -replace "`n", "`r`n"),
    (New-Object System.Text.UTF8Encoding($false)))
Write-Host ""
Write-Host ("Evidence : " + $ev)
exit $code

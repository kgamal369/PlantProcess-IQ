# ============================================================================
# Inspect-JobLog.ps1   -   what did the HMI actually just do?
# Phase A produced job_log +4 and nothing else: jobs ran, rows did not arrive.
# Verify-ImportChain's job-log section printed nothing because its column probe
# missed - so this dumps the REAL schema and the newest entries verbatim, plus
# the dataset/job registry, so the next move is based on the product's own
# words rather than a guess.
# Run from repo root:
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\Inspect-JobLog.ps1
# ============================================================================
[CmdletBinding()]
param(
    [string]$TargetDb = 'ppiq_presentation',
    [int]$Last = 12
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Continue'

$RepoRoot = (Get-Location).Path
$Stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$Out = Join-Path $RepoRoot ('JobLog_' + $Stamp + '.txt')
$sb = New-Object System.Text.StringBuilder
function W([string]$s) { [void]$sb.AppendLine($s); Write-Host $s }
function Save { [System.IO.File]::WriteAllText($Out, $sb.ToString(), (New-Object System.Text.UTF8Encoding($false))) }

$Psql = $null
$cmd = Get-Command psql.exe -ErrorAction SilentlyContinue
if ($cmd) { $Psql = $cmd.Source } else {
    $c = @(Get-ChildItem 'C:\Program Files\PostgreSQL\*\bin\psql.exe' -ErrorAction SilentlyContinue | Sort-Object FullName -Descending)
    if ($c.Count -gt 0) { $Psql = $c[0].FullName }
}
if (-not $Psql) { Write-Host "[FAIL] psql not found." -ForegroundColor Red; exit 1 }
$env:PGPASSWORD = 'ppiq_dev_local_only'

function Rows([string]$q) {
    return @(& $Psql -h 127.0.0.1 -p 5432 -U ppiq_dev -d $TargetDb -w -X -A -t -F ' | ' -c $q 2>&1 | Where-Object { $_ -and ($_.ToString().Trim() -ne '') })
}
# self-check first (the 17-Jul lesson: prove the instrument)
$probe = @(Rows "SELECT 40148;")
if (@($probe).Count -eq 0 -or "$($probe[0])".Trim() -ne '40148') {
    Write-Host "[SELF-CHECK FAILED] query layer broken - refusing to report." -ForegroundColor Red; exit 1
}

W ("JOB LOG INSPECTION - " + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss') + "   DB: " + $TargetDb)
W ("=" * 78)
W ""

# ---- 1. the real schema ----------------------------------------------------
W "[1] job_log columns (so the probe can never miss again):"
$cols = @(Rows "SELECT column_name || '  ' || data_type FROM information_schema.columns WHERE table_schema='public' AND table_name='job_log' ORDER BY ordinal_position;")
if (@($cols).Count -eq 0) { W "    job_log DOES NOT EXIST under public." }
foreach ($c in $cols) { W ("    " + $c) }
W ""

# ---- 2. the newest entries, verbatim ---------------------------------------
W ("[2] newest " + $Last + " job_log rows (THE EVIDENCE - these 4 are your Phase-A attempt):")
$colNames = @(Rows "SELECT column_name FROM information_schema.columns WHERE table_schema='public' AND table_name='job_log' ORDER BY ordinal_position;")
$orderCol = $null
foreach ($cand in @('created_at_utc', 'logged_at_utc', 'created_at', 'logged_at', 'timestamp_utc', 'occurred_at_utc', 'id')) {
    foreach ($c in $colNames) { if ($c.ToString().Trim() -eq $cand) { $orderCol = $cand; break } }
    if ($orderCol) { break }
}
if (-not $orderCol -and @($colNames).Count -gt 0) { $orderCol = $colNames[0].ToString().Trim() }
W ("    (ordered by " + $orderCol + " DESC)")
W ""
$dump = @(Rows ("SELECT row_to_json(t)::text FROM (SELECT * FROM job_log ORDER BY " + $orderCol + " DESC LIMIT " + $Last + ") t;"))
if (@($dump).Count -eq 0) { W "    (no rows)" }
$i = 0
foreach ($d in $dump) {
    $i++
    $s = $d.ToString()
    W ("    --- row " + $i + " ---")
    # pretty-print the json one field per line, truncated
    foreach ($m in [regex]::Matches($s, '"([^"]+)":(("(?:[^"\\]|\\.)*")|([^,}]+))')) {
        $k = $m.Groups[1].Value
        $v = $m.Groups[2].Value.Trim('"')
        if ($v.Length -gt 220) { $v = $v.Substring(0, 220) + ' ...' }
        if ($v -eq 'null' -or $v -eq '') { continue }
        W ("      " + $k.PadRight(24) + " " + $v)
    }
}
W ""

# ---- 3. registry state -----------------------------------------------------
W "[3] what is registered (source_dataset_definitions):"
$dsCols = @(Rows "SELECT column_name FROM information_schema.columns WHERE table_schema='public' AND table_name='source_dataset_definitions' ORDER BY ordinal_position;")
W ("    columns: " + ((@($dsCols) | ForEach-Object { $_.ToString().Trim() }) -join ', '))
W ""
$ds = @(Rows "SELECT row_to_json(t)::text FROM (SELECT * FROM source_dataset_definitions) t;")
if (@($ds).Count -eq 0) { W "    (none registered)" }
foreach ($d in $ds) {
    $s = $d.ToString()
    if ($s.Length -gt 400) { $s = $s.Substring(0, 400) + ' ...' }
    W ("    " + $s)
}
W ""

# ---- 4. job definitions / import jobs --------------------------------------
W "[4] job-related tables present:"
$tabs = @(Rows "SELECT table_name FROM information_schema.tables WHERE table_schema='public' AND (table_name ~* 'job' OR table_name ~* 'batch') ORDER BY 1;")
foreach ($t in $tabs) {
    $tn = $t.ToString().Trim()
    $n = @(Rows ("SELECT COUNT(*) FROM " + $tn + ";"))
    $cv = '?'
    if (@($n).Count -gt 0) { $cv = "$($n[0])".Trim() }
    W ("    " + $tn.PadRight(38) + " " + $cv + " row(s)")
}
W ""

# ---- 5. the newest batch, whatever its state -------------------------------
W "[5] import_batches - newest 5 rows verbatim:"
$bCols = @(Rows "SELECT column_name FROM information_schema.columns WHERE table_schema='public' AND table_name='import_batches' ORDER BY ordinal_position;")
$bOrder = $null
foreach ($cand in @('created_at_utc', 'started_at_utc', 'created_at', 'id')) {
    foreach ($c in $bCols) { if ($c.ToString().Trim() -eq $cand) { $bOrder = $cand; break } }
    if ($bOrder) { break }
}
if (-not $bOrder -and @($bCols).Count -gt 0) { $bOrder = $bCols[0].ToString().Trim() }
$bd = @(Rows ("SELECT row_to_json(t)::text FROM (SELECT * FROM import_batches ORDER BY " + $bOrder + " DESC LIMIT 5) t;"))
foreach ($d in $bd) {
    $s = $d.ToString()
    if ($s.Length -gt 420) { $s = $s.Substring(0, 420) + ' ...' }
    W ("    " + $s)
}
W ""
W "=" * 78
W "READ SECTION 2 FIRST. Four entries appeared during your HMI attempt and no"
W "rows arrived - those messages say why, in the product's own words."
Save
Write-Host ""
Write-Host ("[DONE] -> " + $Out) -ForegroundColor Green
exit 0

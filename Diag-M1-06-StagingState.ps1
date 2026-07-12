#requires -Version 5.1
<#
  Diag-M1-06-StagingState.ps1
  ---------------------------
  READ-ONLY. Reports what the projection did to the meltshop_heats staging rows:
  how many are Mapped vs Failed vs Pending, and the distinct failure reasons.
  No writes, no state change.

  Launch (immune to execution policy / mark-of-the-web):
    powershell -NoProfile -ExecutionPolicy Bypass -File .\Diag-M1-06-StagingState.ps1
#>

[CmdletBinding()]
param(
    [string]$BaseUrl      = 'http://localhost:5063',
    [string]$UserName     = 'e2eadmin',
    [string]$Password     = 'E2EAdmin123!',
    [string]$SourceObject = 'meltshop_heats'
)

$ErrorActionPreference = 'Stop'
function Section($t){ Write-Host ""; Write-Host "== $t ==" -ForegroundColor Cyan }
function Info($t){ Write-Host "     $t" -ForegroundColor Gray }

function Invoke-Api {
    param([string]$Method,[string]$Path,$Body,[hashtable]$Headers)
    $args = @{ Method=$Method; Uri="$BaseUrl$Path"; Headers=$Headers; ContentType='application/json' }
    if ($null -ne $Body) { $args.Body = ($Body | ConvertTo-Json -Depth 8) }
    try { return Invoke-RestMethod @args }
    catch {
        $resp = $_.Exception.Response; $status = if ($resp) { [int]$resp.StatusCode } else { 0 }
        $bt=''; if ($resp) { try { $bt=(New-Object System.IO.StreamReader($resp.GetResponseStream())).ReadToEnd() } catch {} }
        Write-Host ("FAIL: {0} {1} -> HTTP {2}  {3}" -f $Method,$Path,$status,$bt) -ForegroundColor Red; throw
    }
}

# pull the list out of any response shape: bare array, or a wrapper whose one array-valued
# property holds the rows (rows/items/data/results/value/batches/... all handled generically)
function AsList($r){
    if ($null -eq $r) { return @() }
    if ($r -is [System.Array]) { return $r }
    foreach ($p in $r.PSObject.Properties) { if ($p.Value -is [System.Array]) { return @($p.Value) } }
    return @($r)
}

# 1. login
Section "Login"
$login = Invoke-Api -Method Post -Path '/auth/login' -Body @{ userName=$UserName; password=$Password }
$H = @{ Authorization = "Bearer $($login.accessToken)" }
Info "authenticated as $($login.userName)"

# 2. batch
Section "Batch"
$batches = AsList (Invoke-Api -Method Get -Path '/integration/import-batches' -Headers $H)
$batch = $batches | Where-Object { $_.sourceObjectName -eq $SourceObject -and [int]($_.rowCount) -gt 0 } |
         Sort-Object { [int]$_.rowCount } -Descending | Select-Object -First 1
if (-not $batch) { Write-Host "No $SourceObject batch found." -ForegroundColor Red; exit 1 }
$batchId = $batch.id
Info "batch $batchId | rowCount=$($batch.rowCount)"

# 3. status split (take capped at 1000 server-side; count is 'at least this many')
Section "Processing-status split for this batch"
$total = 0
foreach ($st in 'Mapped','Failed','Pending') {
    $r = Invoke-Api -Method Get -Path "/integration/staging-records?importBatchId=$batchId&processingStatus=$st&take=1000" -Headers $H
    $n = [int]$r.count
    $total += $n
    $cap = if ($n -ge 1000) { ' (capped at 1000 - there may be more)' } else { '' }
    Write-Host ("     {0,-8} = {1}{2}" -f $st, $n, $cap)
    if ($st -eq 'Failed' -and $n -gt 0) {
        Section "Distinct failure reasons (Failed rows)"
        $r.rows | Group-Object processingError | Sort-Object Count -Descending | Select-Object -First 10 |
            ForEach-Object { Write-Host ("     [{0,4}x] {1}" -f $_.Count, $_.Name) -ForegroundColor Yellow }
        Section "Sample failed rows (first 3, with raw source)"
        $r.rows | Select-Object -First 3 | ForEach-Object {
            Write-Host ("     row {0}: {1}" -f $_.rowNumber, $_.processingError) -ForegroundColor DarkYellow
            Write-Host ("        raw: {0}" -f ($_.rawJson.Substring(0, [Math]::Min(220, $_.rawJson.Length)))) -ForegroundColor DarkGray
        }
    }
    if ($st -eq 'Mapped' -and $n -gt 0) {
        $one = $r.rows | Select-Object -First 1
        Info "sample Mapped row $($one.rowNumber): canonicalEntityId=$($one.canonicalEntityId) ($($one.canonicalEntityName))"
    }
}

Section "Summary"
Write-Host "     batch rowCount = $($batch.rowCount) | classified (>=) $total" -ForegroundColor Gray
Write-Host "     If Failed dominates with 'Required mapped field ...MaterialUnitType...', the running API" -ForegroundColor Gray
Write-Host "     is NOT the freshly-built binary with constant support -> restart it from a clean build." -ForegroundColor Gray
Write-Host "     If Mapped dominates, Stage A is done -> next is Stage C (canonical refresh)." -ForegroundColor Gray

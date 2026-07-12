#requires -Version 5.1
<#
  Run-M1-06-StageA-Bridge.ps1
  ---------------------------
  M1-06 Stage A (2 of 2). Bridges the MELTSHOP_HEATS staging rows (Architecture A output)
  into canonical MaterialUnits via the existing generic projector (MappingExecutionService).

  Prerequisite: Apply-MappingConstantValues.ps1 applied AND the API restarted (the field map
  below uses "const:" values, which only resolve after that pack).

  Flow:
    1. login
    2. find the meltshop_heats import batch (id + sourceSystemDefinitionId + rowCount)
    3. find a configured Site (its SiteCode anchors the material units)
    4. reuse or create the MELTSHOP_HEATS -> MaterialUnit mapping definition
    5. preview a few rows (dry), then execute across the whole batch
    6. report mapped / failed / skipped and confirm staging rows are now Mapped

  Usage:  .\Run-M1-06-StageA-Bridge.ps1
          .\Run-M1-06-StageA-Bridge.ps1 -BaseUrl http://localhost:5000
#>

[CmdletBinding()]
param(
    [string]$BaseUrl        = 'http://localhost:5063',
    [string]$UserName       = 'e2eadmin',
    [string]$Password       = 'E2EAdmin123!',
    [string]$SourceObject   = 'meltshop_heats',
    [string]$TargetEntity   = 'MaterialUnit'
)

$ErrorActionPreference = 'Stop'
function Section($t){ Write-Host ""; Write-Host "== $t ==" -ForegroundColor Cyan }
function Ok($t){ Write-Host "PASS: $t" -ForegroundColor Green }
function Info($t){ Write-Host "     $t" -ForegroundColor Gray }
function Bad($t){ Write-Host "FAIL: $t" -ForegroundColor Red }

function Invoke-Api {
    param([string]$Method,[string]$Path,$Body,[hashtable]$Headers,[switch]$Soft)
    $args = @{ Method=$Method; Uri="$BaseUrl$Path"; Headers=$Headers; ContentType='application/json' }
    if ($null -ne $Body) { $args.Body = ($Body | ConvertTo-Json -Depth 8) }
    try { return Invoke-RestMethod @args }
    catch {
        $resp = $_.Exception.Response
        $status = if ($resp) { [int]$resp.StatusCode } else { 0 }
        $bt = ''
        if ($resp) { try { $bt = (New-Object System.IO.StreamReader($resp.GetResponseStream())).ReadToEnd() } catch {} }
        if ($Soft) { Write-Host ("     (soft) {0} {1} -> HTTP {2} {3}" -f $Method,$Path,$status,$bt) -ForegroundColor DarkYellow; return $null }
        Bad ("{0} {1} -> HTTP {2}  {3}" -f $Method,$Path,$status,$bt); throw
    }
}
# list endpoints may return a bare array or a wrapper ({items|data|results:[...]})
function AsList($r){
    if ($null -eq $r) { return @() }
    foreach ($k in 'items','data','results','value') { if ($r.PSObject.Properties.Name -contains $k) { return @($r.$k) } }
    return @($r)
}

# --- 1. login ---
Section "1. Login"
$login = Invoke-Api -Method Post -Path '/auth/login' -Body @{ userName=$UserName; password=$Password }
$H = @{ Authorization = "Bearer $($login.accessToken)" }
Ok "authenticated as $($login.userName)"

# --- 2. find the import batch ---
Section "2. Locate the $SourceObject import batch"
$batches = AsList (Invoke-Api -Method Get -Path '/integration/import-batches' -Headers $H)
$batch = $batches |
    Where-Object { $_.sourceObjectName -eq $SourceObject -and [int]($_.rowCount) -gt 0 } |
    Sort-Object { [int]$_.rowCount } -Descending | Select-Object -First 1
if (-not $batch) { Bad "No completed import batch for '$SourceObject' with rows. Run the import close-out first."; exit 1 }
$batchId = $batch.id
$ssdId   = $batch.sourceSystemDefinitionId
Ok "batch $batchId | rowCount=$($batch.rowCount) | sourceSystemDefinitionId=$ssdId"

# --- 3. find a configured Site ---
Section "3. Resolve a Site to anchor the material units"
$sites = AsList (Invoke-Api -Method Get -Path '/plant-layout/sites' -Headers $H -Soft)
$site = $sites | Where-Object { $_.siteCode } | Select-Object -First 1
if (-not $site) {
    Bad "No Site is configured in this database. Material units must belong to a site."
    Info "Configure one site (plant identity), then re-run. Paste 'SELECT id, site_code FROM sites;' if you want me to wire the site-create step."
    exit 1
}
$siteCode = $site.siteCode
Ok "using SiteCode '$siteCode'"

# --- 4. reuse or create the mapping definition ---
Section "4. Mapping definition ($SourceObject -> $TargetEntity)"
$maps = AsList (Invoke-Api -Method Get -Path '/integration/mapping-definitions' -Headers $H)
$map = $maps | Where-Object { $_.sourceObjectName -eq $SourceObject -and $_.targetEntityName -eq $TargetEntity -and $_.isActive } | Select-Object -First 1
if ($map) {
    $mapId = $map.id
    Ok "reusing existing mapping $mapId ($($map.mappingCode))"
} else {
    $fieldMap = [ordered]@{
        MaterialCode       = 'heat_id'
        MaterialUnitType   = 'const:Heat'
        SiteCode           = "const:$siteCode"
        GradeOrRecipe      = 'steel_grade'
        ProductionStartUtc = 'tap_start_utc'
        ProductionEndUtc   = 'tap_end_utc'
        SourceRecordId     = 'heat_id'
    }
    $mappingJson = ($fieldMap | ConvertTo-Json -Compress)
    $createBody = @{
        sourceSystemDefinitionId = $ssdId
        mappingCode              = 'MELTSHOP_HEATS_TO_MATERIALUNIT'
        mappingName              = 'Meltshop Heats to Material Unit'
        sourceObjectName         = $SourceObject
        targetEntityName         = $TargetEntity
        mappingJson              = $mappingJson
        mappingVersion           = 'v1'
        description              = 'Generic projection of meltshop heats into canonical material units.'
        isSynthetic              = $false
        sourceSystem             = 'meltshop'
        sourceRecordId           = $null
    }
    $created = Invoke-Api -Method Post -Path '/integration/mapping-definitions' -Headers $H -Body $createBody
    $mapId = $created.id
    Ok "created mapping $mapId"
    Info "field map: $mappingJson"
}

# --- 5. preview (dry) ---
Section "5. Preview (dry run, 5 rows)"
$prev = Invoke-Api -Method Post -Path "/integration/mapping-definitions/$mapId/preview?importBatchId=$batchId&take=5" -Headers $H -Soft
if ($prev) {
    Write-Host ("     mapped={0} failed={1} skipped={2}" -f $prev.mapped, $prev.failed, $prev.skipped)
    if ([int]$prev.failed -gt 0) {
        $prev.rowResults | Where-Object { $_.status -ne 'Mapped' } | Select-Object -First 3 |
            ForEach-Object { Write-Host ("       row {0}: {1}" -f $_.rowNumber, $_.error) -ForegroundColor DarkYellow }
        Bad "preview shows failures - fix the field map before executing. Not executing."
        exit 1
    }
    Ok "preview clean"
} else { Info "preview endpoint not conclusive; proceeding to execute (it is transactional per row)." }

# --- 6. execute across the batch ---
Section "6. Execute projection (whole batch)"
$exec = Invoke-Api -Method Post -Path "/integration/mapping-definitions/$mapId/execute?importBatchId=$batchId&take=5000&stopOnFirstError=false" -Headers $H
Write-Host ("     processed={0} mapped={1} failed={2} skipped={3}" -f $exec.processed, $exec.mapped, $exec.failed, $exec.skipped)
if ([int]$exec.failed -gt 0) {
    $exec.rowResults | Where-Object { $_.status -eq 'Failed' } | Select-Object -First 5 |
        ForEach-Object { Write-Host ("       row {0}: {1}" -f $_.rowNumber, $_.error) -ForegroundColor DarkYellow }
}
if ([int]$exec.mapped -gt 0 -and [int]$exec.failed -eq 0) {
    Ok "$($exec.mapped) heats projected into canonical MaterialUnits, zero failures -> STAGING BRIDGED"
} elseif ([int]$exec.mapped -gt 0) {
    Ok "$($exec.mapped) mapped, but $($exec.failed) failed - inspect the errors above"
} else {
    Bad "nothing mapped - inspect errors above"
}

# --- 7. confirm staging now Mapped ---
Section "7. Confirm staging rows flipped to Mapped"
$mappedRows = AsList (Invoke-Api -Method Get -Path "/integration/staging-records?importBatchId=$batchId&processingStatus=Mapped&take=1" -Headers $H -Soft)
if ($mappedRows.Count -ge 1) { Ok "staging records now show ProcessingStatus=Mapped" } else { Info "could not confirm via staging-records filter; the execute summary above is authoritative." }

Section "DONE"
Write-Host "If step 6 mapped ~$($batch.rowCount) with zero failures, Stage A is complete." -ForegroundColor Green
Write-Host "Next (Stage B/C): auto-run projection as a journey job after import, then ppiq_run_stage2_canonical_refresh." -ForegroundColor DarkGray

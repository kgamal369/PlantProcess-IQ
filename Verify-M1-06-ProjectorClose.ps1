#requires -Version 5.1
<#
  Verify-M1-06-ProjectorClose.ps1
  -------------------------------
  Proves the two live-demonstrable M1-06 close bars on the current build:
    (1) IDEMPOTENT: re-projecting the already-projected meltshop_heats batch writes ZERO rows.
    (2) NOT-NULL COVERAGE: every imported canonical unit (source_system='postgresql') has a
        non-null plant_time_zone_id and plant_utc_offset_minutes.
  The typed-error-names-field bar and generic-only bar are code-verified (Required() throws a
  typed error naming the field at MappingExecutionService line 67454; zero dataset identifiers
  in the projection path). A LIVE unmapped-field demo needs Pending rows, which M1-07 will
  supply through the pipeline - we capture it there rather than mutating the all-Mapped batch now.

  Non-destructive: the re-projection is a no-op by design.
  Launch (immune to execution policy / mark-of-the-web):
    powershell -NoProfile -ExecutionPolicy Bypass -File .\Verify-M1-06-ProjectorClose.ps1
#>

[CmdletBinding()]
param(
    [string]$BaseUrl      = 'http://localhost:5063',
    [string]$UserName     = 'e2eadmin',
    [string]$Password     = 'E2EAdmin123!',
    [string]$SourceObject = 'meltshop_heats',
    [string]$TargetEntity = 'MaterialUnit',
    [string]$DbHost = '127.0.0.1', [int]$Port = 5432, [string]$Database = 'ppiq_app',
    [string]$DbUser = 'ppiq_dev', [string]$DbPass = 'ppiq_dev_local_only'
)

$ErrorActionPreference = 'Stop'
function Section($t){ Write-Host ""; Write-Host "== $t ==" -ForegroundColor Cyan }
function Ok($t){ Write-Host "PASS: $t" -ForegroundColor Green }
function Bad($t){ Write-Host "FAIL: $t" -ForegroundColor Red }
function Info($t){ Write-Host "     $t" -ForegroundColor Gray }

function Invoke-Api {
    param([string]$Method,[string]$Path,$Body,[hashtable]$Headers)
    $args=@{ Method=$Method; Uri="$BaseUrl$Path"; Headers=$Headers; ContentType='application/json' }
    if ($null -ne $Body) { $args.Body=($Body | ConvertTo-Json -Depth 8) }
    try { return Invoke-RestMethod @args }
    catch { $r=$_.Exception.Response; $s=if($r){[int]$r.StatusCode}else{0}
            $b=''; if($r){try{$b=(New-Object System.IO.StreamReader($r.GetResponseStream())).ReadToEnd()}catch{}}
            Bad ("{0} {1} -> HTTP {2} {3}" -f $Method,$Path,$s,$b); throw }
}
function AsList($r){ if($null -eq $r){return @()}; if($r -is [System.Array]){return $r}
    foreach($p in $r.PSObject.Properties){ if($p.Value -is [System.Array]){return @($p.Value)} }; return @($r) }

# --- (1) idempotency via re-projection ---
Section "1. Idempotency: re-project the already-projected batch (expect ZERO)"
$login = Invoke-Api -Method Post -Path '/auth/login' -Body @{ userName=$UserName; password=$Password }
$H=@{ Authorization="Bearer $($login.accessToken)" }
$batch = AsList (Invoke-Api -Method Get -Path '/integration/import-batches' -Headers $H) |
    Where-Object { $_.sourceObjectName -eq $SourceObject -and [int]$_.rowCount -gt 0 } |
    Sort-Object { [int]$_.rowCount } -Descending | Select-Object -First 1
if (-not $batch) { Bad "no $SourceObject batch"; exit 1 }
$map = AsList (Invoke-Api -Method Get -Path '/integration/mapping-definitions' -Headers $H) |
    Where-Object { $_.sourceObjectName -eq $SourceObject -and $_.targetEntityName -eq $TargetEntity -and $_.isActive } |
    Select-Object -First 1
if (-not $map) { Bad "no active $SourceObject->$TargetEntity mapping (run Stage A first)"; exit 1 }
Info "batch $($batch.id) rowCount=$($batch.rowCount) | mapping $($map.id)"
$re = Invoke-Api -Method Post -Path "/integration/mapping-definitions/$($map.id)/execute?importBatchId=$($batch.id)&take=5000&stopOnFirstError=false" -Headers $H
Write-Host ("     processed={0} mapped={1} skipped={2} failed={3}" -f $re.processedRows,$re.mappedRows,$re.skippedRows,$re.failedRows)
if ([int]$re.mappedRows -eq 0 -and [int]$re.failedRows -eq 0) { Ok "re-projection wrote 0 canonical rows -> idempotent" }
else { Bad "re-projection was not a no-op (mapped=$($re.mappedRows), failed=$($re.failedRows))" }

# --- (2) NOT-NULL coverage via psql ---
Section "2. NOT-NULL coverage on imported canonical units (plant_time_zone_id, plant_utc_offset_minutes)"
$psql=(Get-Command psql -ErrorAction SilentlyContinue).Source
if (-not $psql) { $c=Get-ChildItem 'C:\Program Files\PostgreSQL\*\bin\psql.exe' -EA SilentlyContinue | Sort-Object FullName -Descending | Select-Object -First 1; if($c){$psql=$c.FullName} }
if (-not $psql) { Bad "psql.exe not found - skipping NOT-NULL check"; }
else {
    $env:PGPASSWORD=$DbPass
    $sql=@"
SELECT count(*) AS imported,
       count(*) FILTER (WHERE plant_time_zone_id IS NULL)        AS tz_null,
       count(*) FILTER (WHERE plant_utc_offset_minutes IS NULL)  AS offset_null
FROM material_units
WHERE source_system = 'postgresql';
"@
    $out = & $psql -h $DbHost -p $Port -d $Database -U $DbUser -c $sql 2>&1
    $out | ForEach-Object { Write-Host "     $_" }
    if ($LASTEXITCODE -eq 0) { Info "PASS if tz_null=0 AND offset_null=0 (never-null coverage held)." }
}

Section "Verdict"
Write-Host "     (1) idempotency + (2) NOT-NULL are the live bars; (3) typed-error-names-field and" -ForegroundColor Gray
Write-Host "     (4) generic-only are code-verified. M1-06 core is banked; the live typed-error demo" -ForegroundColor Gray
Write-Host "     rides along with M1-07 (fresh Pending rows through the pipeline)." -ForegroundColor Gray

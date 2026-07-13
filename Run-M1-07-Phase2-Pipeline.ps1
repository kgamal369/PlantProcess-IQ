#requires -Version 5.1
<#
  Run-M1-07-Phase2-Pipeline.ps1  (M1-07 Phase 2a of 2 - completes M1-07)
  ----------------------------------------------------------------------
  Brings the rigged source tables (planted in Phase 1) into the canonical schema entirely through
  the clean DB-Link pipeline - software untouched. Steps:
    1. config: ensure CRACK_LONG + SCRATCH exist in defect_catalogs (the defect taxonomy the
       QualityEvent mapper resolves by code) - reference config, not plant data
    2. discover the Meltshop connection profile (reused from the existing meltshop_heats dataset)
    3. register meltshop_param_readings + meltshop_defect_events through the M1-05 Register route
    4. author two mappings: ParameterObservation and QualityEvent
    5. run-due -> import both to staging
    6. project each batch into canonical
    7. verify canonical parameter_observations (per parameter) + quality_events (CRACK_LONG/SCRATCH)

  The Engine run + 9.5x rediscovery is M1-08 (next). This script only lands the data.
  Launch (immune to execution policy / mark-of-the-web):
    powershell -NoProfile -ExecutionPolicy Bypass -File .\Run-M1-07-Phase2-Pipeline.ps1
#>

[CmdletBinding()]
param(
    [string]$BaseUrl='http://localhost:5063', [string]$UserName='e2eadmin', [string]$Password='E2EAdmin123!',
    [string]$AppHost='127.0.0.1', [int]$AppPort=5432, [string]$AppDb='ppiq_app',
    [string]$AppUser='ppiq_dev', [string]$AppPass='ppiq_dev_local_only'
)
$ErrorActionPreference='Stop'
function Section($t){ Write-Host ""; Write-Host "== $t ==" -ForegroundColor Cyan }
function Ok($t){ Write-Host "PASS: $t" -ForegroundColor Green }
function Info($t){ Write-Host "     $t" -ForegroundColor Gray }
function Bad($t){ Write-Host "FAIL: $t" -ForegroundColor Red }

function Invoke-Api {
    param([string]$Method,[string]$Path,$Body,[hashtable]$Headers,[switch]$Soft)
    $a=@{ Method=$Method; Uri="$BaseUrl$Path"; Headers=$Headers; ContentType='application/json' }
    if($null -ne $Body){ $a.Body=($Body | ConvertTo-Json -Depth 8) }
    try { return Invoke-RestMethod @a }
    catch { $r=$_.Exception.Response; $s=if($r){[int]$r.StatusCode}else{0}
            $b=''; if($r){try{$b=(New-Object System.IO.StreamReader($r.GetResponseStream())).ReadToEnd()}catch{}}
            if($Soft){ Write-Host "     (soft) $Method $Path -> HTTP $s $b" -ForegroundColor DarkYellow; return $null }
            Bad "$Method $Path -> HTTP $s  $b"; throw }
}
function AsList($r){ if($null -eq $r){return @()}; if($r -is [System.Array]){return $r}
    foreach($p in $r.PSObject.Properties){ if($p.Value -is [System.Array]){return @($p.Value)} }; return @($r) }

# psql locate
$psql=(Get-Command psql -ErrorAction SilentlyContinue).Source
if(-not $psql){ $c=Get-ChildItem 'C:\Program Files\PostgreSQL\*\bin\psql.exe' -EA SilentlyContinue | Sort-Object FullName -Descending | Select-Object -First 1; if($c){$psql=$c.FullName} }
if(-not $psql){ Bad "psql.exe not found."; exit 1 }
function App-Sql([string]$sql){
    $env:PGPASSWORD=$AppPass; $eap=$ErrorActionPreference; $ErrorActionPreference='Continue'
    $out=& $psql -h $AppHost -p $AppPort -d $AppDb -U $AppUser -v ON_ERROR_STOP=on -c $sql 2>&1
    $code=$LASTEXITCODE; $ErrorActionPreference=$eap
    if($code -ne 0){ Bad "app sql failed:"; $out | ForEach-Object { Write-Host "     $_" -ForegroundColor DarkYellow }; throw "sql failed" }
    return $out
}

# --- 1. defect catalog config ---
Section "1. Ensure CRACK_LONG + SCRATCH in defect_catalogs (config)"
$cfg=@"
SET client_min_messages TO WARNING;
INSERT INTO defect_catalogs (id, created_at_utc, is_synthetic, source_system, source_record_id, is_deleted, defect_code, defect_name, defect_category, industry_template)
SELECT gen_random_uuid(), now(), false, 'PPIQ_CONFIG', 'CFG-CRACK-LONG', false, 'CRACK_LONG', 'Longitudinal Crack', 'Surface', 'FlatSteel'
WHERE NOT EXISTS (SELECT 1 FROM defect_catalogs WHERE defect_code='CRACK_LONG' AND is_deleted=false);
INSERT INTO defect_catalogs (id, created_at_utc, is_synthetic, source_system, source_record_id, is_deleted, defect_code, defect_name, defect_category, industry_template)
SELECT gen_random_uuid(), now(), false, 'PPIQ_CONFIG', 'CFG-SCRATCH', false, 'SCRATCH', 'Surface Scratch', 'Surface', 'FlatSteel'
WHERE NOT EXISTS (SELECT 1 FROM defect_catalogs WHERE defect_code='SCRATCH' AND is_deleted=false);
"@
App-Sql $cfg | Out-Null
(App-Sql "SELECT defect_code FROM defect_catalogs WHERE defect_code IN ('CRACK_LONG','SCRATCH') AND is_deleted=false ORDER BY 1;") | ForEach-Object { Write-Host "     $_" }
Ok "defect catalog ready"

# --- 1b. parameter_definitions taxonomy (config: the 8 dotted codes the readings use) ---
Section "1b. Ensure the parameter_definitions taxonomy (config)"
$pdcfg=@"
SET client_min_messages TO WARNING;
INSERT INTO parameter_definitions (id, created_at_utc, is_synthetic, source_system, source_record_id, is_deleted,
       parameter_code, parameter_name, value_type, unit_of_measure, parameter_category, industry_template, expected_min_value, expected_max_value)
SELECT gen_random_uuid(), now(), false, 'PPIQ_CONFIG', 'PD-'||v.code, false,
       v.code, v.name, 'Numeric', v.unit, v.cat, 'FlatSteel', v.mn, v.mx
FROM (VALUES
  ('thermal.true_superheat','True Superheat','C','Thermal',10.0,60.0),
  ('chemistry.cev','Carbon Equivalent',NULL,'Chemistry',0.30,0.60),
  ('casting.speed_mean','Casting Speed Mean','m/min','Casting',0.5,2.5),
  ('rolling.reduction_ratio','Reduction Ratio',NULL,'Rolling',1.0,6.0),
  ('rolling.cooling_rate','Cooling Rate','C/s','Rolling',5.0,30.0),
  ('kpi.energy_per_ton','Energy per Ton','kWh/t','KPI',300.0,600.0),
  ('kpi.prime_yield','Prime Yield','ratio','KPI',0.80,1.0),
  ('downtime.cascade_minutes','Cascade Downtime','min','Downtime',0.0,60.0)
) AS v(code,name,unit,cat,mn,mx)
WHERE NOT EXISTS (SELECT 1 FROM parameter_definitions pd WHERE pd.parameter_code = v.code AND pd.is_deleted = false);
"@
App-Sql $pdcfg | Out-Null
(App-Sql "SELECT count(*) AS dotted_param_defs FROM parameter_definitions WHERE parameter_code LIKE '%.%' AND is_deleted=false;") | ForEach-Object { Write-Host "     $_" }
Ok "parameter_definitions ready"

# --- 2. login + discover Meltshop connection profile ---
Section "2. Login + discover the Meltshop connection profile"
$login=Invoke-Api -Method Post -Path '/auth/login' -Body @{ userName=$UserName; password=$Password }
$H=@{ Authorization="Bearer $($login.accessToken)" }
$profiles=AsList (Invoke-Api -Method Get -Path '/admin/connectors/connection-profiles' -Headers $H)
$profile=$profiles | Where-Object { "$($_.hostName):$($_.port) $($_.connectionProfileCode) $($_.displayName) $($_.schemaName)" -match 'meltshop|15432' } | Select-Object -First 1
if(-not $profile){ Bad "could not find a Meltshop connection profile. Profiles seen: $($profiles | ForEach-Object { $_.connectionProfileCode })"; exit 1 }
$profileId=$profile.id
Ok "connection profile $profileId ($($profile.connectionProfileCode))"

# --- 3. register both source tables via the M1-05 Register route ---
Section "3. Register the two rigged tables through the DB-Link Register route"
function Register-Table($schema,$table,$pk,$watermark){
    $body=@{ schemaName=$schema; tableName=$table; primaryKeyColumns=@($pk); watermarkColumn=$watermark; selectedColumns=$null; rowFilter=$null }
    $res=Invoke-Api -Method Post -Path "/admin/connectors/connection-profiles/$profileId/register" -Headers $H -Body $body
    Info "registered $table : $($res.message)"
}
Register-Table 'public' 'meltshop_param_readings' 'reading_id' 'observed_at_utc'
Register-Table 'public' 'meltshop_defect_events'  'event_id'   'event_at_utc'
Ok "both datasets registered"

# --- 4. seed a first import (so sourceSystemDefinitionId exists) + author mappings ---
Section "4. Seed import + author mappings"
$seed=Invoke-Api -Method Post -Path '/admin/workflow-foundation/run-due-source-imports' -Headers $H -Body @{ maxDatasetsPerRun=25; maxRowsPerDataset=50000 }
Write-Host ("     seed run: imported={0} processed={1} failed={2}" -f $seed.totalRowsImported,$seed.datasetsProcessed,$seed.datasetsFailedCount)

function Ensure-Mapping($obj,$target,$fieldMap,$code,$name){
    $existing = AsList (Invoke-Api -Method Get -Path '/integration/mapping-definitions' -Headers $H) |
        Where-Object { $_.sourceObjectName -eq $obj -and $_.targetEntityName -eq $target -and $_.isActive } | Select-Object -First 1
    if($existing){ Info "reusing mapping $($existing.id) for $obj->$target"; return $existing.id }
    $batch = AsList (Invoke-Api -Method Get -Path '/integration/import-batches' -Headers $H) | Where-Object { $_.sourceObjectName -eq $obj } | Select-Object -First 1
    $ssdId = if($batch){ $batch.sourceSystemDefinitionId } else { $null }
    $json = ($fieldMap | ConvertTo-Json -Compress)
    $body=@{ sourceSystemDefinitionId=$ssdId; mappingCode=$code; mappingName=$name; sourceObjectName=$obj;
             targetEntityName=$target; mappingJson=$json; mappingVersion='v1'; description="M1-07 $target"; isSynthetic=$false; sourceSystem='meltshop' }
    $res=Invoke-Api -Method Post -Path '/integration/mapping-definitions' -Headers $H -Body $body
    Info "created mapping $($res.id) for $obj->$target"; return $res.id
}
$pmMap = Ensure-Mapping 'meltshop_param_readings' 'ParameterObservation' ([ordered]@{
    MaterialCode='heat_id'; ParameterCode='param_code'; NumericValue='numeric_value'; ObservedAtUtc='observed_at_utc' } ) 'MELTSHOP_PARAM_READINGS_TO_PARAMOBS' 'Meltshop Readings to Parameter Observation'
$qeMap = Ensure-Mapping 'meltshop_defect_events' 'QualityEvent' ([ordered]@{
    MaterialCode='heat_id'; DefectCode='defect_code'; EventType='event_type'; EventAtUtc='event_at_utc'; Severity='severity' } ) 'MELTSHOP_DEFECT_EVENTS_TO_QE' 'Meltshop Defects to Quality Event'

# --- 4b. reset any previously-failed staging rows to Pending (taxonomy now exists) ---
Section "4b. Reset previously-failed staging rows (config now present)"
App-Sql "UPDATE staging_records SET is_processed=false, processing_status='Pending', processing_error=NULL WHERE source_object_name IN ('meltshop_param_readings','meltshop_defect_events') AND processing_status='Failed';" | Out-Null
Ok "failed staging reset to Pending"

# --- 5-6. drain import + project EVERY batch (readings arrive in ~5k chunks) ---
Section "5. Drain import + project all batches into canonical"
function ProjectAll($obj,$mapId){
    $batches = AsList (Invoke-Api -Method Get -Path '/integration/import-batches' -Headers $H) |
        Where-Object { $_.sourceObjectName -eq $obj -and [int]$_.rowCount -gt 0 }
    $mapped=0; $fail=0
    foreach($b in $batches){
        $ex=Invoke-Api -Method Post -Path "/integration/mapping-definitions/$mapId/execute?importBatchId=$($b.id)&take=50000&stopOnFirstError=false" -Headers $H
        $mapped+=[int]$ex.mappedRows; $fail+=[int]$ex.failedRows
        if([int]$ex.failedRows -gt 0){ $ex.rows | Where-Object { $_.status -eq 'Failed' } | Select-Object -First 3 | ForEach-Object { Write-Host "       $obj row $($_.rowNumber): $($_.message)" -ForegroundColor DarkYellow } }
    }
    Write-Host ("     {0}: mapped(this pass)={1} failed={2} over {3} batch(es)" -f $obj,$mapped,$fail,$batches.Count)
}
for($i=1; $i -le 6; $i++){
    ProjectAll 'meltshop_param_readings' $pmMap
    ProjectAll 'meltshop_defect_events'  $qeMap
    $more=Invoke-Api -Method Post -Path '/admin/workflow-foundation/run-due-source-imports' -Headers $H -Body @{ maxDatasetsPerRun=25; maxRowsPerDataset=50000 }
    Write-Host ("     pass $i run-due imported={0}" -f $more.totalRowsImported)
    if([int]$more.totalRowsImported -eq 0){ break }
}
ProjectAll 'meltshop_param_readings' $pmMap
ProjectAll 'meltshop_defect_events'  $qeMap

# --- 7. verify canonical ---
Section "7. Verify canonical parameter_observations + quality_events"
(App-Sql @"
SELECT 'parameter_observations' AS canonical, count(*) AS rows FROM parameter_observations
UNION ALL SELECT 'quality_events (CRACK_LONG)', count(*) FROM quality_events qe JOIN defect_catalogs dc ON dc.id=qe.defect_catalog_id WHERE dc.defect_code='CRACK_LONG'
UNION ALL SELECT 'quality_events (SCRATCH)', count(*) FROM quality_events qe JOIN defect_catalogs dc ON dc.id=qe.defect_catalog_id WHERE dc.defect_code='SCRATCH';
"@) | ForEach-Object { Write-Host "     $_" }

Section "DONE"
Write-Host "     If parameter_observations grew and CRACK_LONG/SCRATCH quality_events landed, M1-07 is complete." -ForegroundColor Green
Write-Host "     Next: M1-08 - run ppiq_ml_run_learning_job_governed_v1('ML_PROCESS_VS_DEFECT',365,20,false) and confirm the ~9.5x rediscovery." -ForegroundColor DarkGray

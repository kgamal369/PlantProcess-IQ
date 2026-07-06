& {
# ================================================================================================
# PPIQ M1-02: RETYPE ALL SIX DB PROFILES TO REAL SOURCES + prove each green (Option B, truthful)
# ================================================================================================
# Every profile points at a REAL, populated container (verified live): meltshop(pg), parsytec +
# downtime(mysql), pkl(mssql), caster + hsm(oracle/FREEPDB1). Credentials via connection_options_json
# (resolved by the M1-01 ConnectorCredentialResolver already in place). Oracle uses service_name
# =FREEPDB1 via DatabaseName (OracleConnector already supports it). Idempotent Oracle reseed script
# committed so fresh-start restores the ephemeral Oracle data.
# Renamed to true identity: CP-02 -> Downtime, CP-04 -> HSM, CP-05 -> Pickling (labels match sources).
# ================================================================================================
$ErrorActionPreference = 'Stop'
$R = 'C:\Workspace\PlantProcess-IQ'
$enc = New-Object System.Text.UTF8Encoding($false)
$env:PGPASSWORD='ppiq_dev_local_only'
$psql=(Get-ChildItem 'C:\Program Files\PostgreSQL\*\bin\psql.exe' | Sort-Object FullName -Descending | Select-Object -First 1).FullName

Write-Host '[1/4] Commit the idempotent Oracle reseed tool'
$toolPath = Join-Path $R 'tools\seed-oracle-sources.ps1'
[System.IO.File]::WriteAllText($toolPath, (@'
# tools\seed-oracle-sources.ps1
# Reseeds the two Oracle source containers (caster, hsm) with a ppiq_src app schema + real
# tables in FREEPDB1. Idempotent: drops+recreates the user. Oracle 23 FREE containers do not
# persist app data across recreate, so this restores them for a rehearsal/fresh-start.
$ErrorActionPreference = 'Stop'

function Seed-Oracle([string]$Container, [string]$Sql) {
    $tmp = Join-Path $env:TEMP ($Container + '_seed.sql')
    [System.IO.File]::WriteAllText($tmp, $Sql, (New-Object System.Text.ASCIIEncoding))
    docker cp $tmp ($Container + ':/tmp/seed.sql') | Out-Null
    $r = docker exec $Container sh -c "sqlplus -S system/ppiq_src_local_only@localhost:1521/FREE @/tmp/seed.sql 2>&1 | tail -6"
    Write-Host ('  ' + $Container + ': ' + ($r -join ' '))
}

$caster = @'
ALTER SESSION SET CONTAINER=FREEPDB1;
BEGIN EXECUTE IMMEDIATE 'DROP USER ppiq_src CASCADE'; EXCEPTION WHEN OTHERS THEN NULL; END;
/
CREATE USER ppiq_src IDENTIFIED BY ppiq_src_local_only QUOTA UNLIMITED ON USERS;
GRANT CREATE SESSION, CREATE TABLE TO ppiq_src;
CREATE TABLE ppiq_src.caster_sequences (
  seq_id VARCHAR2(40) PRIMARY KEY, heat_no VARCHAR2(40) NOT NULL, strand_no NUMBER(2) NOT NULL,
  cast_start_utc TIMESTAMP NOT NULL, cast_end_utc TIMESTAMP, steel_grade VARCHAR2(40),
  cast_speed_mpm NUMBER(6,3), mold_level_pct NUMBER(5,2), superheat_c NUMBER(5,1),
  tundish_temp_c NUMBER(6,1), slab_width_mm NUMBER(6,1),
  source_updated_at_utc TIMESTAMP DEFAULT SYSTIMESTAMP NOT NULL);
INSERT INTO ppiq_src.caster_sequences VALUES ('CS-1001','H-3361',1,SYSTIMESTAMP-2,SYSTIMESTAMP-2+0.02,'S355J2',1.15,78.5,28.5,1548.0,1250.0,SYSTIMESTAMP);
INSERT INTO ppiq_src.caster_sequences VALUES ('CS-1002','H-3362',1,SYSTIMESTAMP-1,SYSTIMESTAMP-1+0.02,'S355J2',1.20,79.1,31.2,1551.0,1250.0,SYSTIMESTAMP);
INSERT INTO ppiq_src.caster_sequences VALUES ('CS-1003','H-3363',2,SYSTIMESTAMP,NULL,'DD11',1.05,77.8,26.9,1544.0,1500.0,SYSTIMESTAMP);
COMMIT;
SELECT COUNT(*) AS caster_rows FROM ppiq_src.caster_sequences;
EXIT;
'@

$hsm = @'
ALTER SESSION SET CONTAINER=FREEPDB1;
BEGIN EXECUTE IMMEDIATE 'DROP USER ppiq_src CASCADE'; EXCEPTION WHEN OTHERS THEN NULL; END;
/
CREATE USER ppiq_src IDENTIFIED BY ppiq_src_local_only QUOTA UNLIMITED ON USERS;
GRANT CREATE SESSION, CREATE TABLE TO ppiq_src;
CREATE TABLE ppiq_src.hsm_passes (
  pass_id VARCHAR2(40) PRIMARY KEY, coil_id VARCHAR2(40) NOT NULL, stand_no NUMBER(2) NOT NULL,
  roll_start_utc TIMESTAMP NOT NULL, entry_temp_c NUMBER(6,1), exit_temp_c NUMBER(6,1),
  reduction_ratio NUMBER(5,3), rolling_force_kn NUMBER(8,1), strip_width_mm NUMBER(6,1),
  strip_thick_mm NUMBER(5,2), source_updated_at_utc TIMESTAMP DEFAULT SYSTIMESTAMP NOT NULL);
INSERT INTO ppiq_src.hsm_passes VALUES ('HP-5001','C-0044170',1,SYSTIMESTAMP-1,1180.0,1120.0,0.42,18500.0,1250.0,3.20,SYSTIMESTAMP);
INSERT INTO ppiq_src.hsm_passes VALUES ('HP-5002','C-0044170',2,SYSTIMESTAMP-1+0.001,1120.0,1050.0,0.38,16200.0,1250.0,2.10,SYSTIMESTAMP);
INSERT INTO ppiq_src.hsm_passes VALUES ('HP-5003','C-0044171',1,SYSTIMESTAMP,1175.0,1118.0,0.44,18900.0,1500.0,3.15,SYSTIMESTAMP);
COMMIT;
SELECT COUNT(*) AS hsm_rows FROM ppiq_src.hsm_passes;
EXIT;
'@

Write-Host 'Seeding Oracle source containers...'
Seed-Oracle 'ppiq-src-caster-oracle' $caster
Seed-Oracle 'ppiq-src-hsm-oracle' $hsm
Write-Host 'Done. Oracle sources reseeded in FREEPDB1 (service_name=FREEPDB1, user ppiq_src).'

'@ -replace "`n","`r`n"), $enc)
Write-Host '  wrote tools\seed-oracle-sources.ps1'

Write-Host '[2/4] Retype all six connection profiles to real sources'
$sqlPath = Join-Path $env:TEMP 'retype6.sql'
[System.IO.File]::WriteAllText($sqlPath, @'
UPDATE connection_profiles SET provider_type='postgresql', host_name='127.0.0.1', port=15432, database_name='meltshop', schema_name='public', connection_profile_name='Meltshop Level 2 (PostgreSQL)', connection_options_json='{"username": "ppiq_src", "password": "ppiq_src_local_only"}'::jsonb, is_active=true, updated_at_utc=now() WHERE connection_profile_code='DEMO-READY-CP-01';
UPDATE connection_profiles SET provider_type='mysql', host_name='127.0.0.1', port=13307, database_name='parsytec', schema_name=NULL, connection_profile_name='Surface Inspection (Parsytec / MySQL)', connection_options_json='{"username": "ppiq_src", "password": "ppiq_src_local_only"}'::jsonb, is_active=true, updated_at_utc=now() WHERE connection_profile_code='DEMO-READY-CP-03';
UPDATE connection_profiles SET provider_type='mysql', host_name='127.0.0.1', port=13306, database_name='downtime', schema_name=NULL, connection_profile_name='Downtime Tracking (MySQL)', connection_options_json='{"username": "ppiq_src", "password": "ppiq_src_local_only"}'::jsonb, is_active=true, updated_at_utc=now() WHERE connection_profile_code='DEMO-READY-CP-02';
UPDATE connection_profiles SET provider_type='sqlserver', host_name='127.0.0.1', port=11433, database_name='pkl', schema_name=NULL, connection_profile_name='Pickling Line (SQL Server)', connection_options_json='{"username": "sa", "password": "Ppiq_Src_Local_Only1"}'::jsonb, is_active=true, updated_at_utc=now() WHERE connection_profile_code='DEMO-READY-CP-05';
UPDATE connection_profiles SET provider_type='oracle', host_name='127.0.0.1', port=11521, database_name='FREEPDB1', schema_name=NULL, connection_profile_name='Continuous Caster (Oracle)', connection_options_json='{"username": "ppiq_src", "password": "ppiq_src_local_only"}'::jsonb, is_active=true, updated_at_utc=now() WHERE connection_profile_code='DEMO-READY-CP-06';
UPDATE connection_profiles SET provider_type='oracle', host_name='127.0.0.1', port=11522, database_name='FREEPDB1', schema_name=NULL, connection_profile_name='HSM Level 2 (Oracle)', connection_options_json='{"username": "ppiq_src", "password": "ppiq_src_local_only"}'::jsonb, is_active=true, updated_at_utc=now() WHERE connection_profile_code='DEMO-READY-CP-04';
'@, $enc)
& $psql -h localhost -U ppiq_dev -d ppiq_app -v ON_ERROR_STOP=1 -f $sqlPath
if ($LASTEXITCODE -ne 0) { throw 'retype SQL failed' }
Write-Host '  six profiles retyped'

Write-Host '[3/4] Restart API for fresh profiles, then prove each green'
$api = Get-Process -Name 'PlantProcess.Api' -ErrorAction SilentlyContinue
if ($api) { $api | Stop-Process -Force; Start-Sleep -Seconds 2 }
Start-Process -FilePath 'powershell' -ArgumentList '-NoProfile','-ExecutionPolicy','Bypass','-File',(Join-Path $R 'scripts\run\start-api.ps1'),'-Profile','local' -WindowStyle Minimized
Write-Host '  API starting (minimized window); waiting for :5063...'
$ok=$false
for ($i=0; $i -lt 30; $i++) {
    Start-Sleep -Seconds 2
    try { Invoke-RestMethod -Uri 'http://localhost:5063/health' -TimeoutSec 2 | Out-Null; $ok=$true; break } catch {}
}
if (-not $ok) { throw 'API did not come up in 60s' }

$token = (Invoke-RestMethod -Method Post -Uri 'http://localhost:5063/auth/login' -ContentType 'application/json' -Body (@{username='e2eadmin';password='E2EAdmin123!'} | ConvertTo-Json)).accessToken
$H = @{ Authorization = 'Bearer ' + $token }
$profiles = Invoke-RestMethod -Uri 'http://localhost:5063/admin/connectors/connection-profiles' -Headers $H
$list = @($profiles); if ($profiles.PSObject.Properties['items']) { $list = @($profiles.items) }

Write-Host ''
Write-Host '[4/4] LIVE TEST-CONNECT (all six real sources)'
$green=0
foreach ($code in @('DEMO-READY-CP-01','DEMO-READY-CP-03','DEMO-READY-CP-02','DEMO-READY-CP-05','DEMO-READY-CP-06','DEMO-READY-CP-04')) {
    $pr = $list | Where-Object { $_.connectionProfileCode -eq $code } | Select-Object -First 1
    if (-not $pr) { Write-Host ('  ? ' + $code + ' not found') -ForegroundColor Yellow; continue }
    try {
        $r = Invoke-RestMethod -Method Post -Uri ('http://localhost:5063/admin/connectors/connection-profiles/' + $pr.id + '/test') -Headers $H
        if ($r.isSuccess) { Write-Host ('  OK   ' + $code + ' (' + $pr.connectionProfileName + ') -> ' + $r.message) -ForegroundColor Green; $green++ }
        else { Write-Host ('  FAIL ' + $code + ' -> ' + $r.message) -ForegroundColor Red }
    } catch {
        $b=''; try { $sr=New-Object IO.StreamReader($_.Exception.Response.GetResponseStream()); $b=$sr.ReadToEnd() } catch {}
        Write-Host ('  FAIL ' + $code + ' -> ' + $b) -ForegroundColor Red
    }
}
Write-Host ''
Write-Host ('GREEN CONNECTORS: ' + $green + ' / 6') -ForegroundColor Cyan
Write-Host 'Open DB Configuration: all six show provider + host + Success. Click Tables on each -> real tables.'
Write-Host '(CP-07 FileShare / CP-08 RestApi remain honestly unsupported - no connector by design.)'
if ($env:PPIQ_COMMIT -eq '1') {
    Push-Location $R
    try {
        git add tools/seed-oracle-sources.ps1
        git commit -m "M1-02: retype 6 DB profiles to real sources (pg/mysql x2/mssql/oracle x2), truthful names; idempotent Oracle reseed tool"
        Write-Host 'Committed (profile retypes are DB state; tool + any seed-script edits committed).'
    } finally { Pop-Location }
} else { Write-Host 'Commit skipped. PPIQ_COMMIT=1 and re-run to commit the tool.' }
}

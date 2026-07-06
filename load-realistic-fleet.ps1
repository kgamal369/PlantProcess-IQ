# tools\load-realistic-fleet.ps1
# Loads the 3-month simulated plant dataset (ppiq-fleet-3months.zip) into all six source
# containers. Fixes vs previous loader: (1) Oracle drops TABLES not the user (no ORA-01920,
# no session kill needed); (2) mysql stderr warning no longer treated as failure.
param([string]$Zip = 'C:\Workspace\PlantProcess-IQ\ppiq-fleet-3months.zip')
$ErrorActionPreference = 'Stop'
$dir = Join-Path $env:TEMP 'ppiq-fleet-3mo'
if (Test-Path $dir) { Remove-Item $dir -Recurse -Force }
Expand-Archive -Path $Zip -DestinationPath $dir
Write-Host ('Extracted to ' + $dir)

Write-Host '[1/6] meltshop (postgres, ~4.7MB)...'
docker cp (Join-Path $dir 'meltshop.sql') ppiq-src-meltshop-postgres:/tmp/fleet.sql | Out-Null
docker exec ppiq-src-meltshop-postgres psql -U ppiq_src -d meltshop -v ON_ERROR_STOP=1 -q -f /tmp/fleet.sql
if ($LASTEXITCODE -ne 0) { throw 'meltshop failed' }

Write-Host '[2/6] caster (oracle)...'
docker cp (Join-Path $dir 'caster.sql') ppiq-src-caster-oracle:/tmp/fleet.sql | Out-Null
docker exec ppiq-src-caster-oracle sh -c "sqlplus -S system/ppiq_src_local_only@localhost:1521/FREE @/tmp/fleet.sql 2>&1 | tail -4"

Write-Host '[3/6] hsm (oracle, ~20MB - takes a few minutes)...'
docker cp (Join-Path $dir 'hsm.sql') ppiq-src-hsm-oracle:/tmp/fleet.sql | Out-Null
docker exec ppiq-src-hsm-oracle sh -c "sqlplus -S system/ppiq_src_local_only@localhost:1521/FREE @/tmp/fleet.sql 2>&1 | tail -4"

Write-Host '[4/6] parsytec defects (mysql)...'
docker cp (Join-Path $dir 'parsytec.sql') ppiq-src-parsytec-mysql:/tmp/fleet.sql | Out-Null
$out = docker exec ppiq-src-parsytec-mysql sh -c "MYSQL_PWD=ppiq_src_local_only mysql -uppiq_src parsytec < /tmp/fleet.sql 2>&1"
if ($out -match 'ERROR') { throw ('parsytec failed: ' + $out) }

Write-Host '[5/6] downtime (mysql)...'
docker cp (Join-Path $dir 'downtime.sql') ppiq-src-downtime-mysql:/tmp/fleet.sql | Out-Null
$out = docker exec ppiq-src-downtime-mysql sh -c "MYSQL_PWD=ppiq_src_local_only mysql -uppiq_src downtime < /tmp/fleet.sql 2>&1"
if ($out -match 'ERROR') { throw ('downtime failed: ' + $out) }

Write-Host '[6/6] pkl (mssql)...'
docker cp (Join-Path $dir 'pkl.sql') ppiq-src-pkl-mssql:/tmp/fleet.sql | Out-Null
docker exec ppiq-src-pkl-mssql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Ppiq_Src_Local_Only1' -C -d pkl -i /tmp/fleet.sql

Write-Host ''
Write-Host '=== ROW-COUNT PROOF ===' -ForegroundColor Cyan
docker exec ppiq-src-meltshop-postgres psql -U ppiq_src -d meltshop -t -c "SELECT 'heats='||count(*) FROM meltshop_heats UNION ALL SELECT 'samples='||count(*) FROM ms_samples UNION ALL SELECT 'sample_results='||count(*) FROM ms_sample_results UNION ALL SELECT 'steps='||count(*) FROM ms_eaf_steps UNION ALL SELECT 'additives='||count(*) FROM ms_additives;"
docker exec ppiq-src-parsytec-mysql MYSQL_PWD=ppiq_src_local_only mysql -uppiq_src -N -e "SELECT CONCAT('defects=',COUNT(*)) FROM parsytec.parsytec_surface_defects; SELECT CONCAT('codes=',COUNT(*)) FROM parsytec.parsytec_defect_catalog;" 2>$null
docker exec ppiq-src-downtime-mysql MYSQL_PWD=ppiq_src_local_only mysql -uppiq_src -N -e "SELECT CONCAT('downtime=',COUNT(*)) FROM downtime.downtime_events;" 2>$null
Write-Host 'QA_MechanicalTests.xlsx + YardManagement.xlsx are in the zip - place them where the Excel connector will read.'
Write-Host 'DONE. See FLEET_RELATIONS.md (in zip) for the planted-relations catalog + validated effect sizes.'

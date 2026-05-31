$ErrorActionPreference = "Stop"

function Invoke-Check {
    param(
        [string]$Name,
        [scriptblock]$Command,
        [string]$ExpectedPattern = "1"
    )

    Write-Host ""
    Write-Host "Checking $Name..." -ForegroundColor Cyan

    $previousPreference = $ErrorActionPreference
    $ErrorActionPreference = "Continue"

    try {
        $output = & $Command 2>&1
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousPreference
    }

    $text = ($output | Out-String).Trim()

    if ($text) {
        Write-Host $text
    }

    if ($exitCode -ne 0) {
        throw "FAILED: $Name exited with code $exitCode. Output: $text"
    }

    if ($text -notmatch $ExpectedPattern) {
        throw "FAILED: $Name did not return expected pattern '$ExpectedPattern'. Output: $text"
    }

    Write-Host "OK: $Name" -ForegroundColor Green
}

function Invoke-OracleCheck {
    param(
        [string]$Name,
        [string]$Container,
        [string]$User,
        [string]$Password,
        [string]$Sql
    )

    $localDir = ".\tools\phase23\.runtime"
    New-Item -ItemType Directory -Force -Path $localDir | Out-Null

    $safeName = $Name.ToLowerInvariant() -replace '[^a-z0-9]+', '-'
    $localSqlPath = Join-Path $localDir "$safeName.sql"
    $containerSqlPath = "/tmp/$safeName.sql"

@"
SET HEADING OFF
SET FEEDBACK OFF
SET VERIFY OFF
SET ECHO OFF
SET PAGESIZE 0
SET LINESIZE 200
WHENEVER SQLERROR EXIT SQL.SQLCODE
$Sql
EXIT;
"@ | Set-Content -Path $localSqlPath -Encoding ASCII

    docker cp $localSqlPath "${Container}:$containerSqlPath" | Out-Null

    Invoke-Check `
        -Name $Name `
        -Command {
            docker exec $Container sqlplus -s "$User/$Password@localhost/FREEPDB1" "@$containerSqlPath"
        } `
        -ExpectedPattern "1"
}

Write-Host "PlantProcess IQ V7 Phase 2 demo-source runtime smoke V3" -ForegroundColor Cyan

# --------------------------------------------------------------------
# 1. No public host port mappings
# --------------------------------------------------------------------
$containerIds = docker compose -f .\docker-compose.demo-sources.yml ps -q

foreach ($id in $containerIds) {
    $name = docker inspect --format '{{.Name}}' $id
    $ports = docker port $id

    if ($ports) {
        Write-Host "FAIL: $name has published host ports:" -ForegroundColor Red
        Write-Host $ports
        throw "Public host port mapping detected for $name"
    }

    Write-Host "OK: $name has no published host ports" -ForegroundColor Green
}

# --------------------------------------------------------------------
# 2. Source-system row-level smoke checks
# --------------------------------------------------------------------

Invoke-Check `
    -Name "Postgres MeltShop heat ADV_HEAT4002" `
    -Command {
        docker exec ppiq-source-meltshop-postgres `
            psql -U meltshop_owner -d meltshop -tAc "SELECT COUNT(*) FROM heats WHERE heat_id='ADV_HEAT4002';"
    }

Invoke-Check `
    -Name "MySQL Downtime event for ADV_COIL4002" `
    -Command {
        docker exec -e MYSQL_PWD=downtime_demo_pwd ppiq-source-downtime-mysql `
            mysql -u downtime_owner -N -B downtime -e "SELECT COUNT(*) FROM equipment_stoppages WHERE affected_material_code='ADV_COIL4002';"
    }

Invoke-Check `
    -Name "MySQL Parsytec defect for ADV_COIL4002" `
    -Command {
        docker exec -e MYSQL_PWD=parsytec_demo_pwd ppiq-source-parsytec-mysql `
            mysql -u parsytec_owner -N -B parsytec -e "SELECT COUNT(*) FROM surface_defects WHERE coil_id='ADV_COIL4002';"
    }

Invoke-Check `
    -Name "Excel Yard file contains ADV_COIL4002" `
    -Command {
        docker exec ppiq-source-excel-yard sh -lc "grep -c ADV_COIL4002 /data/yard_inventory.csv"
    }

Invoke-Check `
    -Name "Excel QA file contains ADV_COIL4002" `
    -Command {
        docker exec ppiq-source-excel-qa sh -lc "grep -c ADV_COIL4002 /data/qa_samples.csv"
    }

Invoke-OracleCheck `
    -Name "Oracle Caster sequence ADV_SEQ4002" `
    -Container "ppiq-source-caster-oracle" `
    -User "caster_owner" `
    -Password "caster_demo_pwd" `
    -Sql "SELECT COUNT(*) FROM caster_sequences WHERE sequence_id = 'ADV_SEQ4002';"

Invoke-OracleCheck `
    -Name "Oracle HSM coil ADV_COIL4002" `
    -Container "ppiq-source-hsm-oracle" `
    -User "hsm_owner" `
    -Password "hsm_demo_pwd" `
    -Sql "SELECT COUNT(*) FROM hsm_coils WHERE coil_id = 'ADV_COIL4002';"

Invoke-Check `
    -Name "MSSQL PKL coil ADV_COIL4002" `
    -Command {
        docker exec ppiq-source-pkl-mssql /opt/mssql-tools18/bin/sqlcmd `
            -S localhost `
            -U sa `
            -P "Ppiq_Demo_2026_Strong!" `
            -C `
            -h -1 `
            -W `
            -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM pkl.dbo.pkl_coils WHERE coil_id = N'ADV_COIL4002';"
    }

Write-Host ""
Write-Host "PPIQ Phase 2 demo-source runtime smoke PASSED." -ForegroundColor Green

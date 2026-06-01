param(
    [string]$HostName = "127.0.0.1",
    [string]$Port = "5432",
    [string]$Database = "plantprocessiq",
    [string]$User = "plantprocess",
    [string]$PsqlPath = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$Root = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$ScriptDir = Join-Path $Root "Backend\database\scripts"

if ([string]::IsNullOrWhiteSpace($PsqlPath)) {
    $candidate = "C:\Program Files\PostgreSQL\16\bin\psql.exe"

    if (Test-Path $candidate) {
        $PsqlPath = $candidate
    }
    else {
        $cmd = Get-Command psql -ErrorAction SilentlyContinue
        if ($null -eq $cmd) {
            throw "psql.exe was not found. Pass -PsqlPath 'C:\Program Files\PostgreSQL\16\bin\psql.exe'"
        }

        $PsqlPath = $cmd.Source
    }
}

Write-Host "Using psql: $PsqlPath" -ForegroundColor Cyan
Write-Host "Target DB : ${HostName}:$Port / $Database / $User" -ForegroundColor Cyan

$scripts = @(
    "050_dashboard_phase8_9_10_indexes.sql",
    "060_phase_8_9_dashboard_materialized_views.sql",
    "070_fix_system_template_widget_codes.sql",
    "071_validate_dashboard_performance.sql",
    "080_phase_3_4_connector_schema_foundation.sql",
    "095_create_runtime_app_role_admin_only.sql",
    "096_harden_audit_log_immutability.sql",
    "110_phase1_demo_source_shapes.sql",
    "111_phase1_demo_mapping_views.sql",
    "113_phase1_widget_script_layer.sql",
    "115_phase2_integrity_audit.sql",
    "116_phase2_operation_analytics_pilot_foundation.sql",
    "117_phase8_widget_script_layer_entity_mapping.sql",
    "120_phase02_canonical_schema_mapping_engine.sql",
    "121_phase01_bootstrap_token_sweep.sql",
    "130_phase03_two_stage_delta_import_architecture.sql",
    "140_phase02_demo_sources_genealogy_spine.sql",
    "141_phase03_page_builder_foundation.sql",
    "142_phase02_phase03_page_definition_and_demo_source_completion.sql",
    "200_phase02_ml_foundation_feature_store_pgvector.sql",
    "201_phase02_ml_feature_store_v6_completion.sql",
    "202_phase02_ml_compute_basic_correlations_hotfix.sql",
    "203_phase02_ml_compute_v6_wrapper_hotfix.sql",
    "204_phase04_phase05_ml_learning_core.sql",
    "205_phase04_phase05_completion_governance_jobs_tests.sql",
    "206_fix_dashboard_widget_definition_schema_drift.sql",
    "207_fix_dashboard_widget_expression_smallint_schema_drift.sql",
    "300_p01_p02_security_access_control_spine.sql"
)

$temp = Join-Path $env:TEMP "ppiq_sql_apply_$([guid]::NewGuid()).sql"

try {
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($temp, "", $utf8NoBom)

    foreach ($name in $scripts) {
        $path = Join-Path $ScriptDir $name

        if (-not (Test-Path $path)) {
            Write-Host "Skipping missing optional script: $name" -ForegroundColor Yellow
            continue
        }

        $raw = [System.IO.File]::ReadAllText($path)
        $raw = $raw.TrimStart([char]0xFEFF)

        [System.IO.File]::AppendAllText($temp, "`r`n-- >>> $name`r`n", $utf8NoBom)
        [System.IO.File]::AppendAllText($temp, $raw, $utf8NoBom)
        [System.IO.File]::AppendAllText($temp, "`r`n-- <<< $name`r`n", $utf8NoBom)
    }

    Write-Host "Applying SQL scripts first pass..." -ForegroundColor Cyan
    & $PsqlPath -v ON_ERROR_STOP=1 -h $HostName -p $Port -U $User -d $Database -f $temp

    if ($LASTEXITCODE -ne 0) {
        throw "First SQL apply failed with exit code $LASTEXITCODE"
    }

    Write-Host "Applying SQL scripts second pass for idempotency proof..." -ForegroundColor Cyan
    & $PsqlPath -v ON_ERROR_STOP=1 -h $HostName -p $Port -U $User -d $Database -f $temp

    if ($LASTEXITCODE -ne 0) {
        throw "Second SQL idempotency apply failed with exit code $LASTEXITCODE"
    }

    Write-Host "Ordered SQL apply passed twice: clean + idempotency." -ForegroundColor Green
}
finally {
    if (Test-Path $temp) {
        Remove-Item $temp -Force
    }
}


$ErrorActionPreference = "Stop"

param(
    [string]$OutDir = ".\open-export",
    [string]$DbHost = "127.0.0.1",
    [string]$DbPort = "5432",
    [string]$DbName = "plantprocessiq",
    [string]$DbUser = "plantprocess"
)

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

$tables = @(
    "ppiq_i18n_locales",
    "ppiq_i18n_string_keys",
    "ppiq_i18n_translations",
    "ppiq_control_evidence_matrix",
    "ppiq_deployment_airgap_bundles",
    "ppiq_open_format_export_bundles"
)

foreach ($table in $tables) {
    $file = Join-Path $OutDir "$table.csv"
    psql -h $DbHost -p $DbPort -U $DbUser -d $DbName -c "\copy public.$table TO '$file' WITH CSV HEADER"
}

$manifest = @{
    schemaVersion = "open-export-v1"
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    tables = $tables | ForEach-Object {
        @{
            name = $_
            format = "csv"
            path = "$_.csv"
            description = "PlantProcess IQ open export table"
        }
    }
    files = @()
} | ConvertTo-Json -Depth 10

$manifest | Set-Content -Path (Join-Path $OutDir "manifest.json") -Encoding utf8

Write-Host "Open-format export written to $OutDir"
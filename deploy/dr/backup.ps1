$ErrorActionPreference = "Stop"

param(
    [string]$BackupRoot = ".\backups",
    [string]$DbHost = "127.0.0.1",
    [string]$DbPort = "5432",
    [string]$DbName = "plantprocessiq",
    [string]$DbUser = "plantprocess"
)

$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$out = Join-Path $BackupRoot $stamp
New-Item -ItemType Directory -Force -Path $out | Out-Null

$dump = Join-Path $out "plantprocessiq.dump"
pg_dump -h $DbHost -p $DbPort -U $DbUser -d $DbName -Fc -f $dump

$manifest = @{
    backup_time_utc = (Get-Date).ToUniversalTime().ToString("o")
    database = $DbName
    format = "pg_dump_custom"
    file = $dump
    rpo_minutes = 60
} | ConvertTo-Json -Depth 5

$manifest | Set-Content -Path (Join-Path $out "backup-manifest.json") -Encoding utf8

Write-Host "Backup written to $out"
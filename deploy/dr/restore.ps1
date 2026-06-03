$ErrorActionPreference = "Stop"

param(
    [Parameter(Mandatory=$true)][string]$DumpFile,
    [string]$DbHost = "127.0.0.1",
    [string]$DbPort = "5432",
    [string]$DbName = "plantprocessiq_restore",
    [string]$DbUser = "plantprocess"
)

createdb -h $DbHost -p $DbPort -U $DbUser $DbName
pg_restore -h $DbHost -p $DbPort -U $DbUser -d $DbName --clean --if-exists $DumpFile

Write-Host "Restore completed into $DbName"
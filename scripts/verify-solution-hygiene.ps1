#requires -Version 5.1
# PPIQ Phase-3 solution/structure hygiene check. Exit 1 on any failure.
$ErrorActionPreference = "Stop"
$repo = (& git rev-parse --show-toplevel 2>$null); if (-not $repo) { $repo = (Get-Location).Path }
$fail = 0
$sln = Join-Path $repo "Backend\PlantProcessIQ.sln"
if (Test-Path $sln) {
    $listed = (& dotnet sln $sln list) | Where-Object { $_ -match "\.csproj" }
    $onDisk = Get-ChildItem -Path (Join-Path $repo "Backend") -Recurse -Filter *.csproj | Measure-Object
    Write-Host ("csproj in sln: {0}  |  on disk: {1}" -f $listed.Count, $onDisk.Count)
    if ($listed.Count -ne $onDisk.Count) { Write-Host "  [XX] orphan/unregistered csproj" -ForegroundColor Red; $fail++ }
    else { Write-Host "  [OK] every csproj is registered" -ForegroundColor Green }
} else { Write-Host "  [!!] sln not found at $sln" -ForegroundColor Yellow }
$jf = Get-ChildItem -Path $repo -Recurse -Filter Jenkinsfile -ErrorAction SilentlyContinue | Where-Object { $_.FullName -notmatch "\\node_modules\\|\\.ppiq-script-backups\\" }
Write-Host ("Jenkinsfiles: {0}" -f $jf.Count)
if ($jf.Count -ne 1) { Write-Host "  [XX] expected exactly one Jenkinsfile" -ForegroundColor Red; $fail++ } else { Write-Host "  [OK] single Jenkinsfile" -ForegroundColor Green }
$stray = Join-Path $repo "Backend\docker-compose.yml"
if (Test-Path $stray) { Write-Host "  [XX] stray Backend\docker-compose.yml present" -ForegroundColor Red; $fail++ } else { Write-Host "  [OK] no stray Backend/docker-compose.yml" -ForegroundColor Green }
if ($fail -gt 0) { exit 1 }
Write-Host "Solution/structure hygiene OK." -ForegroundColor Green
$ErrorActionPreference = "Stop"

$RepoRoot = "C:\Workspace\PlantProcess-IQ"
$Findings = New-Object System.Collections.Generic.List[string]

$GeneratedPaths = @(
    "Frontend\PlantProcess.Web\storybook-static"
)

foreach ($Relative in $GeneratedPaths) {
    if (Test-Path (Join-Path $RepoRoot $Relative)) {
        $Findings.Add("Generated artifact still exists: $Relative")
    }
}

Get-ChildItem -Path (Join-Path $RepoRoot "tools") -Directory -Recurse -Force -ErrorAction SilentlyContinue |
    Where-Object {
        $_.Name -match "^_pack_.*backup" -or
        $_.Name -eq "purged-artifacts"
    } |
    ForEach-Object {
        $Findings.Add("Implementation backup still exists: " + $_.FullName.Substring($RepoRoot.Length).TrimStart("\"))
    }

Get-ChildItem -Path $RepoRoot -File -Recurse -Force -ErrorAction SilentlyContinue |
    Where-Object {
        $_.FullName -notmatch "\\node_modules\\" -and
        $_.Name -match "\.bak"
    } |
    ForEach-Object {
        $Findings.Add("Backup file still exists: " + $_.FullName.Substring($RepoRoot.Length).TrimStart("\"))
    }

if ($Findings.Count -gt 0) {
    Write-Host "S2B senior-hygiene check found remaining items:" -ForegroundColor Yellow
    foreach ($Finding in $Findings | Select-Object -First 100) {
        Write-Host " - $Finding" -ForegroundColor Yellow
    }

    Write-Host ""
    Write-Host "This is expected if you only ran generated-artifact cleanup. We handle backups in S2B-1B." -ForegroundColor Cyan
    exit 0
}

Write-Host "[GREEN] Senior hygiene quick-check passed: generated artifacts and old backups are gone." -ForegroundColor Green

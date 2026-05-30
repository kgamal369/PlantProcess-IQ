# ============================================================
# FILE: tools/phase1/Fix-PPIQ-Frontend-Json-Encoding.ps1
#
# Fixes frontend JSON/config files saved with BOM or bad encoding.
# Uses Node.js JSON.parse instead of PowerShell ConvertFrom-Json
# because package-lock.json may contain a valid empty key "".
#
# Run from repo root:
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\phase1\Fix-PPIQ-Frontend-Json-Encoding.ps1
# ============================================================

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Get-Location
$frontendRoot = Join-Path $repoRoot "Frontend\PlantProcess.Web"

if (-not (Test-Path $frontendRoot)) {
    throw "Frontend folder not found: $frontendRoot"
}

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)

$files = @(
    "package.json",
    "package-lock.json",
    "postcss.config.json",
    ".postcssrc",
    ".postcssrc.json",
    "tsconfig.json",
    "tsconfig.app.json",
    "tsconfig.node.json"
)

foreach ($relativeFile in $files) {
    $path = Join-Path $frontendRoot $relativeFile

    if (-not (Test-Path $path)) {
        continue
    }

    $text = [System.IO.File]::ReadAllText($path)
    $text = $text.TrimStart([char]0xFEFF)

    [System.IO.File]::WriteAllText($path, $text, $utf8NoBom)
    Write-Host "Re-saved UTF-8 without BOM: Frontend\PlantProcess.Web\$relativeFile"
}

# Validate JSON with Node, not PowerShell.
Push-Location $frontendRoot
try {
    node -e "const fs=require('fs'); for (const f of ['package.json','package-lock.json','tsconfig.json','tsconfig.app.json','tsconfig.node.json']) { if (fs.existsSync(f)) JSON.parse(fs.readFileSync(f,'utf8')); } console.log('Frontend JSON files parsed successfully with Node.js.');"
}
finally {
    Pop-Location
}

Write-Host "Frontend JSON encoding cleanup completed."
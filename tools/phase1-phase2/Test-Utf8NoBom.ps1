[CmdletBinding()]
param(
    [string]$ProjectRoot = (Resolve-Path ".").Path
)

$ErrorActionPreference = "Stop"

function Should-SkipPath([string]$FullPath) {
    $relative = $FullPath.Substring($ProjectRoot.Length).TrimStart("\", "/").Replace("/", "\")

    # Runtime/build/generated/backup folders.
    # Important: supports folder at root level, e.g. ".phase1_phase2_backup\..."
    if ($relative -match '(^|\\)\.git\\') { return $true }
    if ($relative -match '(^|\\)node_modules\\') { return $true }
    if ($relative -match '(^|\\)bin\\') { return $true }
    if ($relative -match '(^|\\)obj\\') { return $true }
    if ($relative -match '(^|\\)dist\\') { return $true }
    if ($relative -match '(^|\\)\.phase1_phase2_backup\\') { return $true }

    # Historical/generated audit outputs should not block active product validation.
    if ($relative -match '^Documentation\\UltimateAudit_') { return $true }
    if ($relative -match '^Documentation\\hygiene\\') { return $true }
    if ($relative -match '^Documentation\\.*\\manifest_.*\.json$') { return $true }

    return $false
}

$extensions = @(".json", ".sql")
$bad = @()

Get-ChildItem -Path $ProjectRoot -Recurse -File |
    Where-Object {
        $extensions -contains $_.Extension.ToLowerInvariant() -and
        -not (Should-SkipPath $_.FullName)
    } |
    ForEach-Object {
        $bytes = [IO.File]::ReadAllBytes($_.FullName)

        if ($bytes.Length -ge 3 -and
            $bytes[0] -eq 0xEF -and
            $bytes[1] -eq 0xBB -and
            $bytes[2] -eq 0xBF) {
            $bad += $_.FullName.Substring($ProjectRoot.Length).TrimStart("\", "/")
        }
    }

if ($bad.Count -gt 0) {
    $bad | Sort-Object | ForEach-Object {
        Write-Host "BOM found: $_" -ForegroundColor Red
    }

    throw "UTF-8 BOM found in active .json/.sql files. Save as UTF-8 without BOM."
}

Write-Host "No UTF-8 BOM found in active .json/.sql files." -ForegroundColor Green
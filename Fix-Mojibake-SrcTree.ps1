# ============================================================================
# Fix-Mojibake-SrcTree.ps1
# Purpose : Eliminate every UTF-8-as-cp1252 mojibake line under
#           Frontend\PlantProcess.Web\src, using the EXACT detection regex of
#           src\test\architecture\noMojibake.test.ts, so the gate goes green
#           for the whole tree, not one file.
# Method  : Line-scoped. Only lines that match the gate regex are touched.
#           Within a matching line, every maximal run of non-ASCII characters
#           is replaced by a single ASCII '-'. Inserts no quotes or braces,
#           cannot break JSX/CSS syntax (lesson L6). Multi-round-trip
#           corruption (AppLayout.css line 2) is not recoverable by decoding;
#           replacement is the deterministic fix.
# Contract: preflight -> byte-safe backup -> fix -> self-check -> vitest gate
#           -> auto-revert on any failure. Backups/restores are Copy-Item
#           (raw bytes) - the L4 encoding-on-restore bug is impossible here.
# Run from repo root:
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\Fix-Mojibake-SrcTree.ps1
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\Fix-Mojibake-SrcTree.ps1 -SkipVitest
# ============================================================================
[CmdletBinding()]
param(
    [switch]$SkipVitest
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$RepoRoot = (Get-Location).Path
$SrcRoot  = Join-Path $RepoRoot 'Frontend\PlantProcess.Web\src'
$Stamp    = Get-Date -Format 'yyyyMMdd_HHmmss'
$BackupDir = Join-Path $RepoRoot ("deploy\.ppiq-backups\mojibake-srctree-" + $Stamp)

# EXACT regex from noMojibake.test.ts
$GateRegex = New-Object System.Text.RegularExpressions.Regex `
    '[\u00C3\u00C2][\u0080-\u00FF\u2000-\u20FF\u0152-\u0178]|\u00E2[\u0080-\u30FF]'
# Same exclusions as the gate's walk()
$ExcludeRegex = New-Object System.Text.RegularExpressions.Regex `
    'node_modules|dist|_phase9_standardbutton_dedupe_backup'
$NonAsciiRun = New-Object System.Text.RegularExpressions.Regex '[^\u0000-\u007F]+'

function Fail([string]$msg) {
    Write-Host ("[FAIL] " + $msg) -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $SrcRoot)) { Fail ("src root not found: " + $SrcRoot + " (run from repo root)") }

# ---------------------------------------------------------------- preflight
Write-Host "[1/5] Preflight scan (gate regex, gate scope: *.ts *.tsx *.css)..."
$files = Get-ChildItem -Path $SrcRoot -Recurse -File -Include *.ts, *.tsx, *.css |
    Where-Object { -not $ExcludeRegex.IsMatch($_.FullName) }

$offenders = @()
foreach ($f in $files) {
    $text = [System.IO.File]::ReadAllText($f.FullName, [System.Text.Encoding]::UTF8)
    if ($GateRegex.IsMatch($text)) { $offenders += $f }
}

if (@($offenders).Count -eq 0) {
    Write-Host "[OK] Tree is already clean. Nothing to do." -ForegroundColor Green
    exit 0
}

Write-Host ("      offender files: " + @($offenders).Count)
foreach ($f in $offenders) {
    $lines = [System.IO.File]::ReadAllLines($f.FullName, [System.Text.Encoding]::UTF8)
    for ($i = 0; $i -lt $lines.Length; $i++) {
        if ($GateRegex.IsMatch($lines[$i])) {
            $snippet = $lines[$i].Trim()
            if ($snippet.Length -gt 70) { $snippet = $snippet.Substring(0, 70) }
            Write-Host ("      " + $f.FullName.Substring($RepoRoot.Length + 1) + ":" + ($i + 1) + "  " + $snippet)
        }
    }
}

# ------------------------------------------------------------------ backup
Write-Host ("[2/5] Byte-safe backup -> " + $BackupDir)
New-Item -ItemType Directory -Path $BackupDir -Force | Out-Null
$backupMap = @{}
foreach ($f in $offenders) {
    $rel = $f.FullName.Substring($SrcRoot.Length + 1)
    $dest = Join-Path $BackupDir $rel
    New-Item -ItemType Directory -Path (Split-Path $dest -Parent) -Force | Out-Null
    Copy-Item -LiteralPath $f.FullName -Destination $dest -Force
    $backupMap[$f.FullName] = $dest
}

function Restore-All {
    Write-Host "[REVERT] Restoring original bytes from backup..." -ForegroundColor Yellow
    foreach ($k in $backupMap.Keys) {
        Copy-Item -LiteralPath $backupMap[$k] -Destination $k -Force
    }
    Write-Host ("[REVERT] Done. Backup kept at " + $BackupDir) -ForegroundColor Yellow
}

# --------------------------------------------------------------------- fix
Write-Host "[3/5] Fixing (line-scoped, non-ASCII runs -> '-')..."
$totalLinesFixed = 0
try {
    foreach ($f in $offenders) {
        $text = [System.IO.File]::ReadAllText($f.FullName, [System.Text.Encoding]::UTF8)
        # Split keeping the separators so original line endings are preserved.
        $parts = [System.Text.RegularExpressions.Regex]::Split($text, '(\r\n|\n)')
        for ($i = 0; $i -lt $parts.Length; $i++) {
            if (($parts[$i] -ne "`r`n") -and ($parts[$i] -ne "`n") -and $GateRegex.IsMatch($parts[$i])) {
                $parts[$i] = $NonAsciiRun.Replace($parts[$i], '-')
                $totalLinesFixed++
            }
        }
        $newText = [string]::Join('', $parts)
        [System.IO.File]::WriteAllText($f.FullName, $newText, (New-Object System.Text.UTF8Encoding($false)))
    }
    Write-Host ("      lines fixed: " + $totalLinesFixed)
} catch {
    Write-Host ("[ERROR] " + $_.Exception.Message) -ForegroundColor Red
    Restore-All
    exit 1
}

# -------------------------------------------------------------- self-check
Write-Host "[4/5] Self-check: full-tree rescan with the gate regex..."
$still = @()
$recheck = Get-ChildItem -Path $SrcRoot -Recurse -File -Include *.ts, *.tsx, *.css |
    Where-Object { -not $ExcludeRegex.IsMatch($_.FullName) }
foreach ($f in $recheck) {
    $text = [System.IO.File]::ReadAllText($f.FullName, [System.Text.Encoding]::UTF8)
    if ($GateRegex.IsMatch($text)) { $still += $f.FullName }
}
if (@($still).Count -gt 0) {
    Write-Host "[ERROR] Residual mojibake after fix:" -ForegroundColor Red
    $still | ForEach-Object { Write-Host ("      " + $_) }
    Restore-All
    exit 1
}
Write-Host "      clean." -ForegroundColor Green

# ------------------------------------------------------------- vitest gate
if ($SkipVitest) {
    Write-Host "[5/5] Vitest gate SKIPPED by switch. Run manually:"
    Write-Host "      npx vitest run src/test/architecture/noMojibake.test.ts"
} else {
    Write-Host "[5/5] Vitest gate: noMojibake.test.ts ..."
    Push-Location (Join-Path $RepoRoot 'Frontend\PlantProcess.Web')
    try {
        & npx vitest run src/test/architecture/noMojibake.test.ts
        $code = $LASTEXITCODE
    } finally {
        Pop-Location
    }
    if ($code -ne 0) {
        Write-Host ("[ERROR] Gate red (exit " + $code + ").") -ForegroundColor Red
        Restore-All
        exit 1
    }
}

Write-Host ""
Write-Host ("[DONE] Mojibake eradicated tree-wide. Files touched: " + @($offenders).Count + ", lines: " + $totalLinesFixed) -ForegroundColor Green
Write-Host ("       Backup: " + $BackupDir)
exit 0

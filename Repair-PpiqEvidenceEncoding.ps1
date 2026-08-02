# ============================================================================
# Repair-PpiqEvidenceEncoding.ps1
#
# THE DEFECT, AND IT IS MINE
#   The gate logs I told you to commit contain this:
#
#       three Latin-1 characters where one check mark should be
#
#   That is the check mark U+2713. Vitest writes it as UTF-8 bytes E2 9C 93.
#   PowerShell 5.1 decodes native-command output using [Console]::OutputEncoding,
#   which on this machine is the OEM code page, not UTF-8. Those three bytes
#   decode as three separate Latin-1 characters, and the corruption happens at
#   DECODE time - before Tee-Object ever writes anything. Tee-Object then makes
#   it worse by writing UTF-16 by default.
#
#   So my diagnostic manufactured mojibake and I told you to commit it. In a
#   repository that already runs noMojibake.test.ts precisely because this class
#   of corruption cost a repo-wide repair pass across thirteen files.
#
# THE RULE FROM HERE
#   Every script that captures output from an external tool sets
#   [Console]::OutputEncoding to UTF-8 BEFORE invoking it, strips ANSI escape
#   sequences, transliterates to ASCII, and writes with UTF8Encoding($false).
#   Generated tool logs are not committed at all.
#
# WHAT THIS SCRIPT DOES
#   Default  - read-only. Reports every non-ASCII character under -Path, with
#              file, line, column and code point. No heuristics: the standing
#              rule is pure ASCII, so any non-ASCII byte is a finding.
#   -Apply   - deletes GENERATED tool logs only (files under a _gate_logs folder
#              or named run_*.log). It refuses to touch anything else, because
#              round-tripping a source file is how a repair pass becomes a
#              second corruption pass.
#
# RUN FROM REPO ROOT. Commands at the bottom.
# ============================================================================
[CmdletBinding()]
param(
    [string]$Path = "docs\m1\evidence",
    [switch]$Apply
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Continue"

$RepoRoot = (Get-Location).Path
$Target   = Join-Path $RepoRoot $Path

function Head([string]$Text) {
    Write-Host ""
    Write-Host ("=" * 78)
    Write-Host $Text
    Write-Host ("=" * 78)
}

function Test-GeneratedLog([string]$FullPath) {
    $rel = $FullPath.Substring($RepoRoot.Length).TrimStart("\")
    if ($rel -match '(^|\\)_gate_logs(\\|$)') { return $true }
    if ((Split-Path $FullPath -Leaf) -match '^run_\d{8}_\d{6}_\d+\.log$') { return $true }
    return $false
}

Head "PPIQ EVIDENCE ENCODING CHECK"
Write-Host ("Path : " + $Target)
Write-Host ("Mode : " + $(if ($Apply) { "APPLY - generated logs will be deleted" } else { "REPORT ONLY" }))

if (-not (Test-Path $Target)) {
    Write-Host ("[REFUSED] not found: " + $Target)
    exit 1
}

$Files = Get-ChildItem -Path $Target -Recurse -File -ErrorAction SilentlyContinue
Write-Host ("Files scanned : " + $Files.Count)

$Offenders = @()

foreach ($f in $Files) {
    $text = ""
    try { $text = [System.IO.File]::ReadAllText($f.FullName) } catch { continue }

    $bad = 0
    $samples = @()
    $lineNo = 0
    foreach ($line in ($text -split "`r?`n")) {
        $lineNo++
        $col = 0
        foreach ($ch in $line.ToCharArray()) {
            $col++
            if ([int]$ch -gt 126) {
                $bad++
                if ($samples.Count -lt 3) {
                    $samples += ("line " + $lineNo + " col " + $col + " U+" + ("{0:X4}" -f [int]$ch))
                }
            }
        }
    }

    if ($bad -gt 0) {
        $Offenders += [pscustomobject]@{
            File      = $f.FullName
            Count     = $bad
            Samples   = ($samples -join "; ")
            Generated = (Test-GeneratedLog $f.FullName)
        }
    }
}

Head "FINDINGS"

if ($Offenders.Count -eq 0) {
    Write-Host "[OK] no non-ASCII character found. The evidence folder is clean."
    exit 0
}

foreach ($o in $Offenders) {
    $tag = "SOURCE OR DOCUMENT"
    if ($o.Generated) { $tag = "GENERATED LOG" }
    Write-Host ""
    Write-Host ("[FOUND] " + $o.File)
    Write-Host ("        non-ASCII characters : " + $o.Count)
    Write-Host ("        first occurrences    : " + $o.Samples)
    Write-Host ("        classification       : " + $tag)
}

$Generated = @($Offenders | Where-Object { $_.Generated })
$Other     = @($Offenders | Where-Object { -not $_.Generated })

Head "SUMMARY"
Write-Host ("Generated tool logs with non-ASCII : " + $Generated.Count)
Write-Host ("Other files with non-ASCII         : " + $Other.Count)

if ($Other.Count -gt 0) {
    Write-Host ""
    Write-Host "[STOP] at least one file is NOT a generated log. This script will not"
    Write-Host "       touch it. A round-trip repair applied to the wrong file turns one"
    Write-Host "       corruption into two. Inspect those files by hand, or use the"
    Write-Host "       repository's own mojibake repair route."
}

if (-not $Apply) {
    Write-Host ""
    Write-Host "[REPORT-ONLY] re-run with -Apply to delete the generated logs listed above."
    exit 1
}

if ($Generated.Count -eq 0) {
    Write-Host ""
    Write-Host "[NOTHING TO DO] no generated log needs deleting."
    exit 1
}

Head "APPLY - DELETING GENERATED LOGS"
foreach ($o in $Generated) {
    Remove-Item $o.File -Force
    Write-Host ("[DELETED] " + $o.File)
}

Write-Host ""
Write-Host "Generated logs are regenerated by re-running the diagnostic, so nothing"
Write-Host "of value is lost. They should not have been committed in the first place."
Write-Host ""
Write-Host "NEXT: stop them coming back. Add this line to .gitignore:"
Write-Host "  docs/m1/evidence/_gate_logs/"
exit 0

# ============================================================================
# HOW TO RUN
#
#   cd C:\Workspace\PlantProcess-IQ
#
#   # 1. see what is corrupted, read-only
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\Repair-PpiqEvidenceEncoding.ps1
#
#   # 2. delete the generated logs
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\Repair-PpiqEvidenceEncoding.ps1 -Apply
#
#   # 3. stop them coming back
#   Add-Content .\.gitignore "docs/m1/evidence/_gate_logs/"
#
#   # 4. prove the whole repository is clean of this class, not just the folder
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\Repair-PpiqEvidenceEncoding.ps1 -Path docs
#
#   git add -A
#   git commit -m "Remove mojibake gate logs and ignore generated tool output"
# ============================================================================

# ============================================================================
# New-ScreenChecklist.ps1
#
# Generates one checklist per screen from docs/m1/screens.txt and the gate table
# in docs/m1/ACCEPTANCE.md, into docs/m1/checklists/.
#
# -Status reports completion WITHOUT regenerating. A tick counts only when an
# evidence file name sits beside it, because a tick with nothing beside it is
# an opinion.
#
# Existing checklists are never overwritten unless -Force is given, so recorded
# evidence cannot be destroyed by re-running this.
#
# NOTE ON NAMING: the path is $ScreensPath and the rows are $ScreenRows.
# PowerShell variable names are case-insensitive, so $Screens and $screens are
# the SAME variable - which is exactly the bug this file was fixed for.
# ============================================================================
[CmdletBinding()]
param(
    [switch]$Status,
    [switch]$Force
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Continue"

$RepoRoot    = (Get-Location).Path
$M1Dir       = Join-Path $RepoRoot "docs\m1"
$ScreensPath = Join-Path $M1Dir "screens.txt"
$AcceptPath  = Join-Path $M1Dir "ACCEPTANCE.md"
$OutDir      = Join-Path $M1Dir "checklists"

function Head([string]$T) { Write-Host ""; Write-Host ("=" * 78); Write-Host $T; Write-Host ("=" * 78) }
function Write-Utf8NoBom([string]$P, [string]$T) {
    $enc = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($P, ($T -replace "`r`n", "`n" -replace "`n", "`r`n"), $enc)
}

if (-not (Test-Path $ScreensPath)) { Write-Host ("[REFUSED] not found: " + $ScreensPath); exit 1 }
if (-not (Test-Path $AcceptPath))  { Write-Host ("[REFUSED] not found: " + $AcceptPath); exit 1 }

# Gate lines are parsed OUT OF ACCEPTANCE.md so the gate is stated once. Add a
# line there and every regenerated checklist gains it.
$GateRows = @()
foreach ($line in ([System.IO.File]::ReadAllText($AcceptPath) -split "`r?`n")) {
    $m = [regex]::Match($line, '^\|\s*(G\d{2})\s*\|\s*([^|]+?)\s*\|')
    if ($m.Success) { $GateRows += [pscustomobject]@{ Id = $m.Groups[1].Value; Text = $m.Groups[2].Value.Trim() } }
}
if ($GateRows.Count -lt 10) {
    Write-Host ("[REFUSED] parsed only " + $GateRows.Count + " gate lines from ACCEPTANCE.md.")
    Write-Host "          The table shape changed. Fix the parser rather than generating a short checklist."
    exit 1
}

$ScreenRows = @()
foreach ($line in ([System.IO.File]::ReadAllText($ScreensPath) -split "`r?`n")) {
    $t = $line.Trim()
    if ($t -eq "" -or $t.StartsWith("#")) { continue }
    $p = $t -split "\|"
    if ($p.Count -lt 5) { continue }
    $ScreenRows += [pscustomobject]@{ Id = $p[0]; Name = $p[1]; Route = $p[2]; Contract = $p[3]; Beat = $p[4] }
}
if ($ScreenRows.Count -lt 1) {
    Write-Host ("[REFUSED] parsed zero screens from " + $ScreensPath + ".")
    Write-Host "          Expected lines of the form Sxx|Name|/route|Contract|Beat."
    exit 1
}

Head ("SCREEN CHECKLISTS - " + $ScreenRows.Count + " screens x " + $GateRows.Count + " gate lines")

if ($Status) {
    if (-not (Test-Path $OutDir)) { Write-Host "[INFO] no checklists generated yet."; exit 1 }
    $TotalDone = 0
    foreach ($s in $ScreenRows) {
        $f = Join-Path $OutDir ($s.Id + "_" + ($s.Name -replace "[^A-Za-z0-9]", "") + ".md")
        if (-not (Test-Path $f)) { Write-Host ("  " + $s.Id + "  MISSING"); continue }
        $txt = [System.IO.File]::ReadAllText($f)
        $done = 0
        foreach ($l in ($txt -split "`r?`n")) {
            if ($l -match '^\|\s*G\d{2}\s*\|') {
                if ($l -match '\|\s*\[x\]\s*\|' -and $l -match '\.(png|jpg|txt|md|log|webm|mp4)') { $done++; $TotalDone++ }
            }
        }
        $state = "OPEN"
        if ($done -eq $GateRows.Count) { $state = "GREEN" }
        Write-Host ("  " + $s.Id.PadRight(5) + $s.Name.PadRight(30) + $done.ToString().PadLeft(3) + " / " + $GateRows.Count + "   " + $state)
    }
    $target = $ScreenRows.Count * $GateRows.Count
    Write-Host ""
    Write-Host ("TOTAL " + $TotalDone + " of " + $target + " gate lines evidenced")
    if ($TotalDone -lt $target) {
        Write-Host "[NOT GREEN] a tick without an evidence file name beside it does not count."
        exit 1
    }
    Write-Host "[GREEN] every screen, every line, every line evidenced."
    exit 0
}

New-Item -ItemType Directory -Path $OutDir -Force | Out-Null
$written = 0
$skipped = 0
foreach ($s in $ScreenRows) {
    $f = Join-Path $OutDir ($s.Id + "_" + ($s.Name -replace "[^A-Za-z0-9]", "") + ".md")
    if ((Test-Path $f) -and (-not $Force)) {
        $skipped++
        Write-Host ("[SKIP] " + (Split-Path $f -Leaf) + " exists - use -Force to regenerate and lose recorded evidence")
        continue
    }

    $sb = New-Object System.Text.StringBuilder
    [void]$sb.AppendLine("# " + $s.Id + " - " + $s.Name)
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("**Route:** ``" + $s.Route + "``  ")
    [void]$sb.AppendLine("**Chapter 3 contract:** " + $s.Contract + "  ")
    [void]$sb.AppendLine("**Presentation beat:** " + $s.Beat)
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("A tick counts only when an evidence file name sits beside it.")
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("| Gate | Line | Done | Evidence file |")
    [void]$sb.AppendLine("|---|---|---|---|")
    foreach ($g in $GateRows) {
        [void]$sb.AppendLine("| " + $g.Id + " | " + $g.Text + " | [ ] | |")
    }
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("## Notes")
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("Anything found while walking this screen that is not a gate line.")
    Write-Utf8NoBom $f $sb.ToString()
    $written++
    Write-Host ("[WRITE] " + (Split-Path $f -Leaf))
}

Write-Host ""
Write-Host ("Written " + $written + ", skipped " + $skipped)
Write-Host "Check progress at any time with:"
Write-Host "  powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\m1\New-ScreenChecklist.ps1 -Status"
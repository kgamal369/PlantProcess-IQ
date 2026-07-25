<#
    Invoke-PpiqControlAudit.ps1
    M1-02  EXHAUSTIVE CONTROL AUDIT OF EVERY DEMO SURFACE

    WHAT THIS IS
      Earlier reviews tested whether the rehearsed path worked. This classifies
      EVERY interactive element on every surface the customer can reach.

    WHY IT DOES NOT JUST READ THE EXISTING INVENTORY
      Frontend\PlantProcess.Web\docs\ui-standards\button-inventory.csv exists and
      carries 256 control rows. It was last modified 2026-06-24, a month before
      this audit. Trusting it would audit June's product. So this script performs
      a LIVE SCAN of the current source and uses that CSV only as a baseline to
      report drift against. That also makes it genuinely re-runnable, which the
      task requires, because this becomes the standing audit method.

    WHAT IT EMITS
      One row per interactive control:
        page, route, onDemoPath, controlType, label, file, line, handler,
        classification

      CLASSIFICATION, one of four:
        HAS HANDLER    a handler expression is present and non-trivial
        NO HANDLER     interactive element with no handler, or a no-op handler.
                       This is a dead control.
        LICENCE-GATED  the control sits inside a LicenseGate or an entitlement
                       branch, so at the demo tier it may render a refusal card
                       instead of the control
        UNMOUNTED      the component exists and is imported by nothing, so the
                       control is unreachable from any page

    READ-ONLY. Writes two report files and changes nothing in the repository.

    RUN FROM REPO ROOT
      powershell -NoProfile -ExecutionPolicy Bypass -File .\Invoke-PpiqControlAudit.ps1
      powershell -NoProfile -ExecutionPolicy Bypass -File .\Invoke-PpiqControlAudit.ps1 -DemoPathOnly
      powershell -NoProfile -ExecutionPolicy Bypass -File .\Invoke-PpiqControlAudit.ps1 -ShowDead
#>

[CmdletBinding()]
param(
    [switch]$DemoPathOnly,
    [switch]$ShowDead,
    [string]$OutDir = "_ppiq_audit"
)

$ErrorActionPreference = "Continue"

$RepoRoot = (Get-Location).Path
$SrcRoot  = Join-Path $RepoRoot "Frontend\PlantProcess.Web\src"
$Baseline = Join-Path $RepoRoot "Frontend\PlantProcess.Web\docs\ui-standards\button-inventory.csv"

function Write-Section { param([string]$T) Write-Host ""; Write-Host ("=" * 100); Write-Host $T; Write-Host ("=" * 100) }
function Read-Text { param([string]$P) return [System.IO.File]::ReadAllText($P) }

# ---------------------------------------------------------------- DEMO PATH
# Routes the customer walks. Everything else is off-path: a dead control there
# is a finding, but a lower-priority one.
$DemoRoutes = @(
    "/", "/overview", "/workspace", "/dashboard", "/dashboards",
    "/connections", "/sources", "/import", "/jobs",
    "/prep", "/prep/canvas", "/mapping",
    "/analysis", "/analysis/toolbox", "/investigate", "/investigate/analysis-jobs",
    "/genealogy", "/materials",
    "/findings", "/analytics", "/analytics/advanced",
    "/assistant", "/supervisor", "/engine", "/alerting"
)

if (-not (Test-Path $SrcRoot)) {
    Write-Host "FATAL: $SrcRoot not found. Run from the repository root."
    exit 1
}

Write-Section "M1-02  EXHAUSTIVE CONTROL AUDIT"
Write-Host ("Repo root : " + $RepoRoot)
Write-Host ("Run at    : " + (Get-Date -Format "yyyy-MM-dd HH:mm:ss"))
Write-Host ("Source    : " + $SrcRoot)

# ------------------------------------------------------------ COLLECT FILES

$files = Get-ChildItem -Path $SrcRoot -Recurse -File -Include *.tsx, *.jsx |
         Where-Object { $_.FullName -notmatch "\\node_modules\\" -and $_.Name -notmatch "\.test\.|\.spec\." }
Write-Host ("Files     : " + $files.Count + " tsx/jsx")

# ------------------------------------------------------------- ROUTE TABLE

Write-Section "STEP 1 - ROUTE TABLE"

$routeMap = @{}   # componentName -> route path
$routeRx  = [regex]'<Route\s+[^>]*?path\s*=\s*"([^"]+)"[^>]*?element\s*=\s*\{\s*<\s*([A-Za-z0-9_]+)'
$routeRx2 = [regex]'path\s*=\s*"([^"]+)"[\s\S]{0,200}?element\s*=\s*\{\s*<\s*([A-Za-z0-9_]+)'

foreach ($f in $files) {
    $t = Read-Text $f.FullName
    if ($t -notmatch "<Route") { continue }
    foreach ($rx in @($routeRx, $routeRx2)) {
        foreach ($m in $rx.Matches($t)) {
            $p = $m.Groups[1].Value; $c = $m.Groups[2].Value
            if (-not $routeMap.ContainsKey($c)) { $routeMap[$c] = $p }
        }
    }
}
Write-Host ("  routes discovered: " + $routeMap.Count)

# --------------------------------------------------- IMPORT GRAPH (mounting)

Write-Section "STEP 2 - IMPORT GRAPH"

$importedNames = New-Object 'System.Collections.Generic.HashSet[string]'
$usedTags      = New-Object 'System.Collections.Generic.HashSet[string]'
$impRx = [regex]'import\s+(?:type\s+)?(?:\{([^}]*)\}|([A-Za-z0-9_]+))\s+from'
$tagRx = [regex]'<\s*([A-Z][A-Za-z0-9_]*)'

foreach ($f in $files) {
    $t = Read-Text $f.FullName
    foreach ($m in $impRx.Matches($t)) {
        if ($m.Groups[1].Success) {
            foreach ($n in ($m.Groups[1].Value -split ",")) {
                $n = ($n -replace "\s+as\s+.*","").Trim()
                if ($n) { [void]$importedNames.Add($n) }
            }
        }
        if ($m.Groups[2].Success) { [void]$importedNames.Add($m.Groups[2].Value.Trim()) }
    }
    foreach ($m in $tagRx.Matches($t)) { [void]$usedTags.Add($m.Groups[1].Value) }
}
Write-Host ("  imported symbols: " + $importedNames.Count + "   rendered tags: " + $usedTags.Count)

# resolve each file's exported component names
$expRx = [regex]'export\s+(?:default\s+)?(?:function|const)\s+([A-Za-z0-9_]+)'
$fileExports = @{}
foreach ($f in $files) {
    $t = Read-Text $f.FullName
    $names = @()
    foreach ($m in $expRx.Matches($t)) {
        $n = $m.Groups[1].Value
        if ($n -cmatch '^[A-Z]') { $names += $n }
    }
    if ($names.Count -eq 0) { $names = @([System.IO.Path]::GetFileNameWithoutExtension($f.Name)) }
    $fileExports[$f.FullName] = ($names | Select-Object -Unique)
}

function Test-Unmounted {
    param([string]$FullName)
    foreach ($n in $fileExports[$FullName]) {
        if ($importedNames.Contains($n) -or $routeMap.ContainsKey($n)) { return $false }
    }
    return $true
}

# --------------------------------------------- FILE -> PAGE / ROUTE MAPPING

function Resolve-Page {
    param([string]$FullName)
    $rel = $FullName.Substring($RepoRoot.Length + 1)
    foreach ($n in $fileExports[$FullName]) {
        if ($routeMap.ContainsKey($n)) { return @{ Page = $n; Route = $routeMap[$n] } }
    }
    # not a routed page: attribute by folder so the reader can still locate it
    if ($rel -match "\\src\\pages\\([^\\]+)\\") { return @{ Page = "pages/" + $Matches[1]; Route = "(component)" } }
    if ($rel -match "\\src\\components\\([^\\]+)\\") { return @{ Page = "components/" + $Matches[1]; Route = "(component)" } }
    return @{ Page = [System.IO.Path]::GetFileNameWithoutExtension($FullName); Route = "(component)" }
}

# ----------------------------------------------------------- CONTROL SCAN

Write-Section "STEP 3 - CONTROL SCAN"

# Interactive element opening tags. The chunk captured runs to the tag's close
# so multi-line JSX props are seen.
$ctrlPatterns = @(
    @{ Type = "StandardButton";     Rx = '<\s*StandardButton\b'      ; Ev = "onClick" },
    @{ Type = "StandardP2Button";   Rx = '<\s*StandardP2Button\b'    ; Ev = "onClick" },
    @{ Type = "button";             Rx = '<\s*button\b'              ; Ev = "onClick" },
    @{ Type = "StandardP2Select";   Rx = '<\s*StandardP2Select\b'    ; Ev = "onChange" },
    @{ Type = "select";             Rx = '<\s*select\b'              ; Ev = "onChange" },
    @{ Type = "StandardP2Input";    Rx = '<\s*StandardP2Input\b'     ; Ev = "onChange" },
    @{ Type = "StandardP2TextArea"; Rx = '<\s*StandardP2TextArea\b'  ; Ev = "onChange" },
    @{ Type = "input";              Rx = '<\s*input\b'               ; Ev = "onChange" },
    @{ Type = "textarea";           Rx = '<\s*textarea\b'            ; Ev = "onChange" },
    @{ Type = "form";               Rx = '<\s*form\b'                ; Ev = "onSubmit" },
    @{ Type = "anchor";             Rx = '<\s*a\s'                   ; Ev = "href" }
)

$rows = New-Object System.Collections.ArrayList
$scanned = 0

foreach ($f in $files) {
    $text = Read-Text $f.FullName
    $rel  = $f.FullName.Substring($RepoRoot.Length + 1)
    $pg   = Resolve-Page $f.FullName
    $unmounted = Test-Unmounted $f.FullName
    $gated = ($text -match 'LicenseGate|useEntitlement|hasFeature\(|licence|license') -and ($text -match 'LicenseGate|hasFeature\(')

    foreach ($p in $ctrlPatterns) {
        foreach ($m in ([regex]$p.Rx).Matches($text)) {
            $start = $m.Index
            # walk forward to the end of this opening tag, ignoring > inside braces
            $depth = 0; $end = -1
            for ($i = $start; $i -lt [Math]::Min($text.Length, $start + 3000); $i++) {
                $ch = $text[$i]
                if ($ch -eq '{') { $depth++ }
                elseif ($ch -eq '}') { if ($depth -gt 0) { $depth-- } }
                elseif ($ch -eq '>' -and $depth -eq 0) { $end = $i; break }
            }
            if ($end -lt 0) { continue }
            $chunk = $text.Substring($start, $end - $start + 1)
            $scanned++

            $line = ($text.Substring(0, $start) -split "`n").Count

            # handler
            $handler = ""
            $hm = [regex]::Match($chunk, ($p.Ev + '\s*=\s*\{([^}]{0,160})'))
            if ($hm.Success) { $handler = $hm.Groups[1].Value.Trim() }
            elseif ($p.Ev -eq "href") {
                $hm2 = [regex]::Match($chunk, 'href\s*=\s*[{"]([^}"]{0,120})')
                if ($hm2.Success) { $handler = "href:" + $hm2.Groups[1].Value.Trim() }
            }
            $handler = ($handler -replace "\s+", " ")

            # label: text between the opening tag close and the next tag
            $label = ""
            $after = $text.Substring($end + 1, [Math]::Min(120, $text.Length - $end - 1))
            $lm = [regex]::Match($after, '^\s*([^<{\r\n]{2,60})')
            if ($lm.Success) { $label = $lm.Groups[1].Value.Trim() }
            if (-not $label) {
                $am = [regex]::Match($chunk, '(?:aria-label|title|placeholder)\s*=\s*"([^"]{2,60})"')
                if ($am.Success) { $label = $am.Groups[1].Value }
            }
            if (-not $label) { $label = "(no label)" }

            # classification
            $cls = "HAS HANDLER"
            $noop = ($handler -eq "" ) -or
                    ($handler -match '^\(\)\s*=>\s*\{\s*\}$') -or
                    ($handler -match '^\(\)\s*=>\s*(null|undefined|void 0)\s*$') -or
                    ($handler -match '^undefined$')
            if ($unmounted) { $cls = "UNMOUNTED" }
            elseif ($noop)  { $cls = "NO HANDLER" }
            elseif ($gated) { $cls = "LICENCE-GATED" }

            $onPath = $DemoRoutes -contains $pg.Route

            [void]$rows.Add([pscustomobject]@{
                onDemoPath     = $(if ($onPath) { "YES" } else { "no" })
                page           = $pg.Page
                route          = $pg.Route
                controlType    = $p.Type
                label          = $label
                classification = $cls
                handler        = $handler
                file           = $rel
                line           = $line
            })
        }
    }
}

Write-Host ("  interactive controls found: " + $rows.Count)

# ------------------------------------------------------------------ OUTPUT

$outFull = Join-Path $RepoRoot $OutDir
New-Item -Path $outFull -ItemType Directory -Force | Out-Null
$stamp = Get-Date -Format "yyyyMMdd_HHmmss"

$sorted = $rows | Sort-Object `
    @{ Expression = { if ($_.onDemoPath -eq "YES") { 0 } else { 1 } } },
    @{ Expression = { switch ($_.classification) { "NO HANDLER" {0} "UNMOUNTED" {1} "LICENCE-GATED" {2} default {3} } } },
    page, controlType, line

$csvPath = Join-Path $outFull ("control-audit-" + $stamp + ".csv")
$sorted | Export-Csv -Path $csvPath -NoTypeInformation -Encoding ASCII

Write-Section "STEP 4 - CLASSIFICATION SUMMARY"

$byCls = $sorted | Group-Object classification | Sort-Object Name
Write-Host ""
Write-Host ("  {0,-16} {1,8} {2,12}" -f "CLASSIFICATION", "TOTAL", "ON DEMO PATH")
Write-Host ("  " + ("-" * 40))
foreach ($g in $byCls) {
    $onPath = ($g.Group | Where-Object { $_.onDemoPath -eq "YES" }).Count
    Write-Host ("  {0,-16} {1,8} {2,12}" -f $g.Name, $g.Count, $onPath)
}

$deadOnPath = @($sorted | Where-Object { $_.classification -eq "NO HANDLER" -and $_.onDemoPath -eq "YES" })
$unmounted  = @($sorted | Where-Object { $_.classification -eq "UNMOUNTED" })

Write-Section "STEP 5 - THE ACCEPTANCE NUMBER"
Write-Host ""
Write-Host ("  Dead controls on the demo path: " + $deadOnPath.Count)
Write-Host "  M1-02 acceptance requires this to be ZERO, by fixing them in M1-07"
Write-Host "  or by removing them from the demo path and recording that in M1-06."
if ($deadOnPath.Count -gt 0) {
    Write-Host ""
    Write-Host "  Every one of them:"
    Write-Host ""
    Write-Host ("  {0,-26} {1,-20} {2,-34} {3}" -f "PAGE", "TYPE", "LABEL", "FILE:LINE")
    Write-Host ("  " + ("-" * 118))
    foreach ($d in $deadOnPath) {
        $lbl = $d.label; if ($lbl.Length -gt 32) { $lbl = $lbl.Substring(0,31) + "." }
        $pgx = $d.page;  if ($pgx.Length -gt 24) { $pgx = $pgx.Substring(0,23) + "." }
        Write-Host ("  {0,-26} {1,-20} {2,-34} {3}" -f $pgx, $d.controlType, $lbl, ($d.file + ":" + $d.line))
    }
}

if ($ShowDead -and -not $DemoPathOnly) {
    $deadOff = @($sorted | Where-Object { $_.classification -eq "NO HANDLER" -and $_.onDemoPath -ne "YES" })
    Write-Section "OFF-PATH DEAD CONTROLS (lower priority, still findings)"
    Write-Host ("  count: " + $deadOff.Count)
    foreach ($d in ($deadOff | Select-Object -First 40)) {
        Write-Host ("    " + $d.page.PadRight(26) + $d.controlType.PadRight(20) + $d.file + ":" + $d.line)
    }
    if ($deadOff.Count -gt 40) { Write-Host ("    ... and " + ($deadOff.Count - 40) + " more, see the CSV") }
}

Write-Section "STEP 6 - UNMOUNTED COMPONENTS"
Write-Host ""
$unmountedFiles = $unmounted | Select-Object -ExpandProperty file -Unique
Write-Host ("  Components carrying controls and imported by NOTHING: " + $unmountedFiles.Count)
Write-Host "  These are built, committed, and reachable from no page."
Write-Host ""
foreach ($u in $unmountedFiles) { Write-Host ("    " + $u) }

# ------------------------------------------------------- BASELINE DRIFT

Write-Section "STEP 7 - DRIFT AGAINST THE COMMITTED INVENTORY"
if (-not (Test-Path $Baseline)) {
    Write-Host "  Baseline CSV not found. Skipping drift check."
} else {
    $bl = Import-Csv $Baseline
    $blDate = (Get-Item $Baseline).LastWriteTime.ToString("yyyy-MM-dd")
    Write-Host ("  Baseline: " + $bl.Count + " rows, last modified " + $blDate)
    Write-Host ("  Live scan: " + $rows.Count + " rows, today")
    Write-Host ""
    $blKeys = @{}
    foreach ($b in $bl) { $blKeys[($b.file + "|" + $b.line)] = $b }
    $newOnes = @($sorted | Where-Object { -not $blKeys.ContainsKey((($_.file -replace '\\','/') -replace '^Frontend/PlantProcess.Web/','') + "|" + $_.line) })
    Write-Host ("  Controls present live but absent from the baseline: " + $newOnes.Count)
    Write-Host "  A large number here simply means the baseline is stale, which is expected."
    Write-Host "  The live scan is the truth; the baseline is a historical reference."
}

# ------------------------------------------------------------- MARKDOWN

$mdPath = Join-Path $outFull ("control-audit-" + $stamp + ".md")
$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine("# PPIQ Control Audit - M1-02")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("Generated " + (Get-Date -Format "yyyy-MM-dd HH:mm") + " by a live scan of the current source.")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("| Classification | Total | On demo path |")
[void]$sb.AppendLine("|---|---|---|")
foreach ($g in $byCls) {
    $onPath = ($g.Group | Where-Object { $_.onDemoPath -eq "YES" }).Count
    [void]$sb.AppendLine("| " + $g.Name + " | " + $g.Count + " | " + $onPath + " |")
}
[void]$sb.AppendLine("")
[void]$sb.AppendLine("**Dead controls on the demo path: " + $deadOnPath.Count + "**. M1-02 acceptance requires zero.")
[void]$sb.AppendLine("")
if ($deadOnPath.Count -gt 0) {
    [void]$sb.AppendLine("## Dead controls on the demo path")
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("| Page | Route | Type | Label | File:line | Decision |")
    [void]$sb.AppendLine("|---|---|---|---|---|---|")
    foreach ($d in $deadOnPath) {
        [void]$sb.AppendLine("| " + $d.page + " | " + $d.route + " | " + $d.controlType + " | " + $d.label + " | " + $d.file + ":" + $d.line + " | FIX (M1-07) / CUT (M1-06) |")
    }
    [void]$sb.AppendLine("")
}
[void]$sb.AppendLine("## Unmounted components carrying controls")
[void]$sb.AppendLine("")
foreach ($u in $unmountedFiles) { [void]$sb.AppendLine("- " + $u) }
[System.IO.File]::WriteAllText($mdPath, $sb.ToString(), (New-Object System.Text.UTF8Encoding($false)))

Write-Section "DONE"
Write-Host ""
Write-Host "  Full matrix : $csvPath"
Write-Host "  Summary     : $mdPath"
Write-Host ""
Write-Host "  HOW TO CLOSE M1-02"
Write-Host "    1. Open the CSV. Every row is classified; none is blank."
Write-Host "    2. Work the demo-path NO HANDLER rows first. Each gets one of two"
Write-Host "       decisions written into the markdown: FIX in M1-07, or CUT in M1-06"
Write-Host "       by removing it from the demo path."
Write-Host "    3. Re-run this script. The demo-path dead count must read 0."
Write-Host ""
Write-Host "  READ THE CLASSIFIER'S LIMITS BEFORE TRUSTING A ROW"
Write-Host "    HAS HANDLER means a handler expression exists. It does NOT mean the"
Write-Host "    handler does the right thing, that a dropdown is populated from live"
Write-Host "    data, or that a save reaches the correct place. Those are lens two and"
Write-Host "    lens three of the audit concept, and they are M1-03, which is a human"
Write-Host "    walk. This script closes lens one only: does the control exist and is"
Write-Host "    it wired. Do not read a green summary here as a working product."
Write-Host ""

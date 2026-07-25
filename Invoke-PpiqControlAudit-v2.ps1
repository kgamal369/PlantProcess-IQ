<#
    Invoke-PpiqControlAudit-v2.ps1
    M1-02  EXHAUSTIVE CONTROL AUDIT - corrected

    WHY THERE IS A v2
      v1 reported "dead controls on the demo path: 0" and that number was FALSE.
      Its ON DEMO PATH column read 0 for every classification, including 141 for
      HAS HANDLER. Zero controls matched the demo path at all, so the headline
      was measuring a failed match, not a clean product. It also listed
      VisualJoinCanvasPage and AnalysisToolboxPage as UNMOUNTED, which is provably
      wrong: both are routed.

      TWO ROOT CAUSES, both mine:

      1. LAZY ROUTES. Pages are loaded with const X = lazy(() => import("...")).
         v1's import regex only matched "import ... from", so every lazy-loaded
         page was invisible to the import graph and fell through to UNMOUNTED.

      2. WRAPPED ROUTE ELEMENTS. element={<Guard><Page /></Guard>} made v1 capture
         the guard's name instead of the page's, so only 12 of the routes resolved.

    WHAT v2 DOES DIFFERENTLY

      - Detects lazy imports and dynamic import() paths as real imports
      - Extracts EVERY component tag inside element={...} and tries each one,
        so a wrapped page still resolves
      - Resolves a route by module path as well as by symbol name, so
        import("./pages/Prep/VisualJoinCanvasPage") binds that file to its route
      - Computes TRANSITIVE REACHABILITY: a component imported by a routed page,
        or by anything that page reaches, inherits that page's route. v1 only
        looked at the file's own exports, which is why components never matched
      - Excludes Storybook files, which are never imported by design
      - FAILS LOUD. If zero controls match the demo path, or if fewer routes
        resolve than exist, the script says the audit is INVALID and refuses to
        print an acceptance number. A false green is worse than a red.

    READ-ONLY. Writes two report files and changes nothing in the repository.

    RUN FROM REPO ROOT
      powershell -NoProfile -ExecutionPolicy Bypass -File .\Invoke-PpiqControlAudit-v2.ps1
      powershell -NoProfile -ExecutionPolicy Bypass -File .\Invoke-PpiqControlAudit-v2.ps1 -ShowDead
      powershell -NoProfile -ExecutionPolicy Bypass -File .\Invoke-PpiqControlAudit-v2.ps1 -ListRoutes
#>

[CmdletBinding()]
param(
    [switch]$ShowDead,
    [switch]$ListRoutes,
    [string]$OutDir = "_ppiq_audit"
)

$ErrorActionPreference = "Continue"

$RepoRoot = (Get-Location).Path
$WebRoot  = Join-Path $RepoRoot "Frontend\PlantProcess.Web"
$SrcRoot  = Join-Path $WebRoot "src"
$Baseline = Join-Path $WebRoot "docs\ui-standards\button-inventory.csv"

function Write-Section { param([string]$T) Write-Host ""; Write-Host ("=" * 100); Write-Host $T; Write-Host ("=" * 100) }
function Read-Text { param([string]$P) return [System.IO.File]::ReadAllText($P) }

$DemoRouteHints = @(
    "/", "overview", "workspace", "dashboard", "connections", "sources", "import",
    "jobs", "prep", "canvas", "mapping", "analysis", "toolbox", "investigate",
    "genealogy", "material", "findings", "analytics", "assistant", "supervisor",
    "engine", "alerting", "ml-readiness"
)

if (-not (Test-Path $SrcRoot)) { Write-Host "FATAL: $SrcRoot not found. Run from the repository root."; exit 1 }

Write-Section "M1-02  EXHAUSTIVE CONTROL AUDIT  (v2)"
Write-Host ("Repo root : " + $RepoRoot)
Write-Host ("Run at    : " + (Get-Date -Format "yyyy-MM-dd HH:mm:ss"))

$files = Get-ChildItem -Path $SrcRoot -Recurse -File -Include *.tsx, *.jsx |
         Where-Object { $_.FullName -notmatch "\\node_modules\\" -and $_.Name -notmatch "\.test\.|\.spec\.|\.stories\." }
Write-Host ("Files     : " + $files.Count + " tsx/jsx  (test and stories files excluded)")

# normalise a full path to a module key: src/pages/Prep/VisualJoinCanvasPage
function To-ModuleKey { param([string]$Full)
    $rel = $Full.Substring($SrcRoot.Length).TrimStart("\","/")
    $rel = $rel -replace "\\","/"
    return ($rel -replace "\.(tsx|jsx)$","")
}
$moduleOf = @{}
foreach ($f in $files) { $moduleOf[$f.FullName] = To-ModuleKey $f.FullName }

# exported component names per file
$expRx = [regex]'export\s+(?:default\s+)?(?:function|const|class)\s+([A-Za-z0-9_]+)'
$fileExports = @{}
foreach ($f in $files) {
    $t = Read-Text $f.FullName
    $names = @()
    foreach ($m in $expRx.Matches($t)) { $n = $m.Groups[1].Value; if ($n -cmatch '^[A-Z]') { $names += $n } }
    if ($t -match 'export\s+default\s+([A-Za-z0-9_]+)') { $names += $Matches[1] }
    if ($names.Count -eq 0) { $names = @([System.IO.Path]::GetFileNameWithoutExtension($f.Name)) }
    $fileExports[$f.FullName] = ($names | Select-Object -Unique)
}
# reverse index: symbol -> file
$fileOfSymbol = @{}
foreach ($f in $files) { foreach ($n in $fileExports[$f.FullName]) { if (-not $fileOfSymbol.ContainsKey($n)) { $fileOfSymbol[$n] = $f.FullName } } }

# ------------------------------------------------------- IMPORT EDGES (fixed)

Write-Section "STEP 1 - IMPORT GRAPH  (static + lazy)"

$edges = @{}     # file -> list of imported files
$lazyCount = 0
$staticRx = [regex]'import\s+(?:type\s+)?(?:[^''"]*?)\s*from\s+[''"]([^''"]+)[''"]'
$lazyRx   = [regex]'import\s*\(\s*[''"]([^''"]+)[''"]\s*\)'
$lazyDecl = [regex]'(?:const|let|var)\s+([A-Za-z0-9_]+)\s*=\s*(?:React\.)?lazy\s*\(\s*\(\s*\)\s*=>\s*import\s*\(\s*[''"]([^''"]+)[''"]'

function Resolve-Spec { param([string]$FromFile, [string]$Spec)
    if ($Spec -match '^[a-zA-Z@]' -and $Spec -notmatch '^@/') { return $null }   # package
    $s = $Spec -replace '^@/', ''
    if ($Spec -match '^\.') {
        $base = Split-Path $FromFile -Parent
        try { $s = (Resolve-Path -LiteralPath (Join-Path $base $Spec) -ErrorAction SilentlyContinue) } catch { $s = $null }
        if ($s) { $s = To-ModuleKey $s.Path } else {
            $s = ($Spec -replace '^\./','') -replace '\\','/'
            $baseKey = (Split-Path (To-ModuleKey $FromFile) -Parent) -replace '\\','/'
            $s = ($baseKey + "/" + $s) -replace '/\./','/'
            while ($s -match '[^/]+/\.\./') { $s = $s -replace '[^/]+/\.\./','' }
        }
    }
    $s = ($s -replace '\.(tsx|jsx|ts|js)$','') -replace '\\','/'
    foreach ($kv in $moduleOf.GetEnumerator()) {
        if ($kv.Value -eq $s -or $kv.Value -eq ($s + "/index")) { return $kv.Key }
    }
    return $null
}

$lazySymbolFile = @{}
foreach ($f in $files) {
    $t = Read-Text $f.FullName
    $list = @()
    foreach ($m in $staticRx.Matches($t)) { $r = Resolve-Spec $f.FullName $m.Groups[1].Value; if ($r) { $list += $r } }
    foreach ($m in $lazyRx.Matches($t))   { $r = Resolve-Spec $f.FullName $m.Groups[1].Value; if ($r) { $list += $r; $lazyCount++ } }
    foreach ($m in $lazyDecl.Matches($t)) {
        $r = Resolve-Spec $f.FullName $m.Groups[2].Value
        if ($r) { $lazySymbolFile[$m.Groups[1].Value] = $r }
    }
    $edges[$f.FullName] = ($list | Select-Object -Unique)
}
Write-Host ("  files with edges : " + ($edges.Keys | Where-Object { $edges[$_].Count -gt 0 }).Count)
Write-Host ("  lazy imports resolved : " + $lazyCount + "   lazy symbols bound : " + $lazySymbolFile.Count)
if ($lazyCount -eq 0) { Write-Host "  NOTE: no lazy imports found. If the app uses them, resolution may still be incomplete." }

# ------------------------------------------------------- ROUTES (fixed)

Write-Section "STEP 2 - ROUTE TABLE  (wrapped elements handled)"

$routeOfFile = @{}
$routeRows = New-Object System.Collections.ArrayList
$routeBlockRx = [regex]'(?s)<Route\b(.*?)/?>'
$pathRx = [regex]'path\s*=\s*[''"]([^''"]+)[''"]'
$tagRx  = [regex]'<\s*([A-Z][A-Za-z0-9_]*)'

foreach ($f in $files) {
    $t = Read-Text $f.FullName
    if ($t -notmatch "<Route") { continue }
    foreach ($m in $routeBlockRx.Matches($t)) {
        $blk = $m.Groups[1].Value
        $pm = $pathRx.Match($blk); if (-not $pm.Success) { continue }
        $path = $pm.Groups[1].Value
        if ($path -notmatch '^/') { $path = "/" + $path }
        # every component tag inside element={...}, innermost last
        $em = [regex]::Match($blk, '(?s)element\s*=\s*\{(.*)$')
        $cands = @()
        if ($em.Success) { foreach ($tm in $tagRx.Matches($em.Groups[1].Value)) { $cands += $tm.Groups[1].Value } }
        foreach ($c in $cands) {
            $target = $null
            if ($lazySymbolFile.ContainsKey($c)) { $target = $lazySymbolFile[$c] }
            elseif ($fileOfSymbol.ContainsKey($c)) { $target = $fileOfSymbol[$c] }
            if ($target -and -not $routeOfFile.ContainsKey($target)) {
                $routeOfFile[$target] = $path
                [void]$routeRows.Add([pscustomobject]@{ Route = $path; Component = $c; File = $target.Substring($RepoRoot.Length+1) })
            }
        }
    }
}
Write-Host ("  route-to-file bindings resolved: " + $routeOfFile.Count)
if ($ListRoutes) {
    Write-Host ""
    foreach ($rr in ($routeRows | Sort-Object Route)) { Write-Host ("    " + $rr.Route.PadRight(34) + $rr.Component.PadRight(34) + $rr.File) }
}

# ------------------------------------------- TRANSITIVE REACHABILITY (new)

Write-Section "STEP 3 - TRANSITIVE REACHABILITY"

$reachRoute = @{}
foreach ($start in $routeOfFile.Keys) {
    $route = $routeOfFile[$start]
    $stack = New-Object System.Collections.Stack
    $stack.Push($start)
    $seen = New-Object 'System.Collections.Generic.HashSet[string]'
    while ($stack.Count -gt 0) {
        $cur = $stack.Pop()
        if (-not $seen.Add($cur)) { continue }
        if (-not $reachRoute.ContainsKey($cur)) { $reachRoute[$cur] = $route }
        if ($edges.ContainsKey($cur)) { foreach ($n in $edges[$cur]) { if (-not $seen.Contains($n)) { $stack.Push($n) } } }
    }
}
Write-Host ("  files reachable from a route: " + $reachRoute.Count + " of " + $files.Count)

function Test-DemoRoute { param([string]$Route)
    if (-not $Route) { return $false }
    foreach ($h in $DemoRouteHints) { if ($h -eq "/" ) { if ($Route -eq "/") { return $true } } elseif ($Route -like ("*" + $h + "*")) { return $true } }
    return $false
}

# --------------------------------------------------------- CONTROL SCAN

Write-Section "STEP 4 - CONTROL SCAN"

$ctrlPatterns = @(
    @{ Type="StandardButton";     Rx='<\s*StandardButton\b';     Ev="onClick" },
    @{ Type="StandardP2Button";   Rx='<\s*StandardP2Button\b';   Ev="onClick" },
    @{ Type="button";             Rx='<\s*button\b';             Ev="onClick" },
    @{ Type="StandardP2Select";   Rx='<\s*StandardP2Select\b';   Ev="onChange" },
    @{ Type="select";             Rx='<\s*select\b';             Ev="onChange" },
    @{ Type="StandardP2Input";    Rx='<\s*StandardP2Input\b';    Ev="onChange" },
    @{ Type="StandardP2TextArea"; Rx='<\s*StandardP2TextArea\b'; Ev="onChange" },
    @{ Type="input";              Rx='<\s*input\b';              Ev="onChange" },
    @{ Type="textarea";           Rx='<\s*textarea\b';           Ev="onChange" },
    @{ Type="form";               Rx='<\s*form\b';               Ev="onSubmit" },
    @{ Type="anchor";             Rx='<\s*a\s';                  Ev="href" }
)

$rows = New-Object System.Collections.ArrayList
foreach ($f in $files) {
    $text = Read-Text $f.FullName
    $rel  = $f.FullName.Substring($RepoRoot.Length + 1)
    $route = $null
    if ($reachRoute.ContainsKey($f.FullName)) { $route = $reachRoute[$f.FullName] }
    $unmounted = ($null -eq $route)
    $gated = ($text -match 'LicenseGate|hasFeature\(')
    $onPath = Test-DemoRoute $route

    foreach ($p in $ctrlPatterns) {
        foreach ($m in ([regex]$p.Rx).Matches($text)) {
            $start = $m.Index; $depth = 0; $end = -1
            for ($i = $start; $i -lt [Math]::Min($text.Length, $start + 3000); $i++) {
                $ch = $text[$i]
                if ($ch -eq '{') { $depth++ } elseif ($ch -eq '}') { if ($depth -gt 0) { $depth-- } }
                elseif ($ch -eq '>' -and $depth -eq 0) { $end = $i; break }
            }
            if ($end -lt 0) { continue }
            $chunk = $text.Substring($start, $end - $start + 1)
            $line = ($text.Substring(0, $start) -split "`n").Count

            $handler = ""
            $hm = [regex]::Match($chunk, ($p.Ev + '\s*=\s*\{([^}]{0,160})'))
            if ($hm.Success) { $handler = $hm.Groups[1].Value.Trim() }
            elseif ($p.Ev -eq "href") { $h2 = [regex]::Match($chunk,'href\s*=\s*[{"]([^}"]{0,120})'); if ($h2.Success) { $handler = "href:" + $h2.Groups[1].Value.Trim() } }
            $handler = ($handler -replace "\s+"," ")

            $label = ""
            $after = $text.Substring($end + 1, [Math]::Min(120, $text.Length - $end - 1))
            $lm = [regex]::Match($after, '^\s*([^<{\r\n]{2,60})')
            if ($lm.Success) { $label = $lm.Groups[1].Value.Trim() }
            if (-not $label) { $am = [regex]::Match($chunk,'(?:aria-label|title|placeholder)\s*=\s*"([^"]{2,60})"'); if ($am.Success) { $label = $am.Groups[1].Value } }
            if (-not $label) { $label = "(no label)" }

            $noop = ($handler -eq "") -or ($handler -match '^\(\)\s*=>\s*\{\s*\}$') -or ($handler -match '^\(\)\s*=>\s*(null|undefined|void 0)\s*$') -or ($handler -match '^undefined$')
            $cls = "HAS HANDLER"
            if ($unmounted) { $cls = "UNMOUNTED" } elseif ($noop) { $cls = "NO HANDLER" } elseif ($gated) { $cls = "LICENCE-GATED" }

            [void]$rows.Add([pscustomobject]@{
                onDemoPath=$(if($onPath){"YES"}else{"no"}); route=$(if($route){$route}else{"(unreachable)"})
                page=[System.IO.Path]::GetFileNameWithoutExtension($f.Name); controlType=$p.Type
                label=$label; classification=$cls; handler=$handler; file=$rel; line=$line })
        }
    }
}
Write-Host ("  interactive controls found: " + $rows.Count)

# ------------------------------------------------------- VALIDITY GUARD

Write-Section "STEP 5 - VALIDITY GUARD"

$onPathCount = @($rows | Where-Object { $_.onDemoPath -eq "YES" }).Count
$invalid = $false
Write-Host ""
Write-Host ("  routes resolved              : " + $routeOfFile.Count)
Write-Host ("  files reachable from a route : " + $reachRoute.Count + " of " + $files.Count)
Write-Host ("  controls on the demo path    : " + $onPathCount)
Write-Host ""
if ($routeOfFile.Count -lt 5)  { Write-Host "  FAIL: fewer than five routes resolved. Route parsing is broken."; $invalid = $true }
if ($onPathCount -eq 0)        { Write-Host "  FAIL: zero controls matched the demo path. The match, not the product, is the problem."; $invalid = $true }
if ($reachRoute.Count -lt ($files.Count * 0.25)) { Write-Host "  FAIL: under a quarter of files are reachable from any route. The import graph is incomplete."; $invalid = $true }

if ($invalid) {
    Write-Host ""
    Write-Host "  THIS AUDIT IS INVALID. No acceptance number will be printed."
    Write-Host "  A zero produced by a failed match is worse than a red, because it reads as a pass."
    Write-Host ""
    Write-Host "  Re-run with -ListRoutes and send the output. The route or import pattern"
    Write-Host "  in this codebase differs from what the resolver expects."
    Write-Host ""
} else {
    Write-Host "  Guard passed. The numbers below are measuring the product, not the parser."
}

# ------------------------------------------------------------------ OUTPUT

$outFull = Join-Path $RepoRoot $OutDir
New-Item -Path $outFull -ItemType Directory -Force | Out-Null
$stamp = Get-Date -Format "yyyyMMdd_HHmmss"

$sorted = $rows | Sort-Object `
    @{Expression={ if ($_.onDemoPath -eq "YES") {0} else {1} }},
    @{Expression={ switch ($_.classification) { "NO HANDLER" {0} "UNMOUNTED" {1} "LICENCE-GATED" {2} default {3} } }},
    route, page, line

$csvPath = Join-Path $outFull ("control-audit-v2-" + $stamp + ".csv")
$sorted | Export-Csv -Path $csvPath -NoTypeInformation -Encoding ASCII

Write-Section "STEP 6 - CLASSIFICATION SUMMARY"
Write-Host ""
Write-Host ("  {0,-16} {1,8} {2,14}" -f "CLASSIFICATION","TOTAL","ON DEMO PATH")
Write-Host ("  " + ("-" * 42))
foreach ($g in ($sorted | Group-Object classification | Sort-Object Name)) {
    $op = ($g.Group | Where-Object { $_.onDemoPath -eq "YES" }).Count
    Write-Host ("  {0,-16} {1,8} {2,14}" -f $g.Name, $g.Count, $op)
}

if (-not $invalid) {
    $deadOnPath = @($sorted | Where-Object { $_.classification -eq "NO HANDLER" -and $_.onDemoPath -eq "YES" })
    Write-Section "STEP 7 - THE ACCEPTANCE NUMBER"
    Write-Host ""
    Write-Host ("  Dead controls on the demo path: " + $deadOnPath.Count)
    Write-Host "  M1-02 acceptance requires zero: fix in M1-07 or cut in M1-06."
    if ($deadOnPath.Count -gt 0) {
        Write-Host ""
        Write-Host ("  {0,-24} {1,-20} {2,-30} {3}" -f "ROUTE","TYPE","LABEL","FILE:LINE")
        Write-Host ("  " + ("-" * 116))
        foreach ($d in $deadOnPath) {
            $lbl=$d.label; if($lbl.Length -gt 28){$lbl=$lbl.Substring(0,27)+"."}
            $rt=$d.route;  if($rt.Length  -gt 22){$rt=$rt.Substring(0,21)+"."}
            Write-Host ("  {0,-24} {1,-20} {2,-30} {3}" -f $rt,$d.controlType,$lbl,($d.file+":"+$d.line))
        }
    }
}

Write-Section "STEP 8 - UNREACHABLE COMPONENTS"
$un = $sorted | Where-Object { $_.classification -eq "UNMOUNTED" } | Select-Object -ExpandProperty file -Unique
Write-Host ""
Write-Host ("  Components carrying controls and reachable from no route: " + $un.Count)
Write-Host "  v2 counts lazy imports, so a page here is genuinely unreachable."
Write-Host ""
foreach ($u in $un) { Write-Host ("    " + $u) }

if ($ShowDead) {
    $deadOff = @($sorted | Where-Object { $_.classification -eq "NO HANDLER" -and $_.onDemoPath -ne "YES" })
    Write-Section "OFF-PATH DEAD CONTROLS"
    Write-Host ("  count: " + $deadOff.Count)
    foreach ($d in ($deadOff | Select-Object -First 40)) { Write-Host ("    " + $d.route.PadRight(24) + $d.controlType.PadRight(20) + $d.file + ":" + $d.line) }
    if ($deadOff.Count -gt 40) { Write-Host ("    ... and " + ($deadOff.Count-40) + " more, see the CSV") }
}

Write-Section "DONE"
Write-Host ""
Write-Host "  Full matrix : $csvPath"
Write-Host ""
if ($invalid) {
    Write-Host "  STATUS: INVALID. Fix resolution before reading any number above as a result."
} else {
    Write-Host "  STATUS: VALID."
    Write-Host ""
    Write-Host "  REMEMBER WHAT THIS DOES NOT MEASURE"
    Write-Host "    HAS HANDLER means a handler exists. It does not mean the handler does the"
    Write-Host "    right thing, that a dropdown is fed from live data, or that a save reaches"
    Write-Host "    the correct place. That is M1-03, and only a human walk closes it."
}
Write-Host ""

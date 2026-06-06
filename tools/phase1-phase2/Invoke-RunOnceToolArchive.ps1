[CmdletBinding()]
param(
    [string]$ProjectRoot = (Resolve-Path ".").Path,
    [switch]$Apply
)

$ErrorActionPreference = "Stop"

$tools = Join-Path $ProjectRoot "tools"
$archive = Join-Path $tools "archive\run-once"
$report = Join-Path $ProjectRoot "docs\phase1-phase2\RUN_ONCE_TOOLING_ARCHIVE_REPORT.md"

if (-not (Test-Path $tools)) {
    Write-Host "No tools folder found."
    exit 0
}

if (-not (Test-Path $archive)) {
    New-Item -ItemType Directory -Force -Path $archive | Out-Null
}

$references = ""
foreach ($f in @("Jenkinsfile", "package.json", "Frontend\PlantProcess.Web\package.json", "Website\PlantProcess.Website\package.json")) {
    $p = Join-Path $ProjectRoot $f
    if (Test-Path $p) { $references += "`n" + [IO.File]::ReadAllText($p) }
}

$candidates = Get-ChildItem -Path $tools -Recurse -File |
    Where-Object {
        $_.FullName -notmatch "\\tools\\archive\\" -and
        $_.Name -match "^(fix-|apply-|scaffold-|run-once-|patch-|migrate-|phase\d+|Apply-)" -and
        $_.Extension -in @(".ps1", ".cjs", ".mjs", ".js", ".txt")
    }

$rows = New-Object System.Collections.Generic.List[string]
$rows.Add("# Run-once tooling archive report")
$rows.Add("")
$rows.Add("| File | Action | Reason |")
$rows.Add("|---|---|---|")

foreach ($c in $candidates) {
    $rel = $c.FullName.Substring($ProjectRoot.Length).TrimStart("\","/")
    if ($references -match [Regex]::Escape($c.Name)) {
        $rows.Add("| $rel | kept | referenced by CI/package config |")
        continue
    }

    if ($Apply) {
        $dest = Join-Path $archive ($rel.Replace("\","__"))
        Move-Item $c.FullName $dest -Force
        $rows.Add("| $rel | archived | run-once pattern and no active reference found |")
    } else {
        $rows.Add("| $rel | would archive | run with -Apply to move |")
    }
}

[IO.File]::WriteAllText($report, ($rows -join "`r`n") + "`r`n", (New-Object Text.UTF8Encoding($false)))
Write-Host "Archive report: $report" -ForegroundColor Green
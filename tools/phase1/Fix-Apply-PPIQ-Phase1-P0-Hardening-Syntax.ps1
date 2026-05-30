# ============================================================
# FILE: tools/phase1/Fix-Apply-PPIQ-Phase1-P0-Hardening-Syntax.ps1
#
# Fixes the broken/truncated Apply-PPIQ-Phase1-P0-Hardening.ps1
# by replacing the validate-forbidden-copy.mjs block with a safe
# variable-based single-quoted here-string.
#
# Run from repo root:
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\phase1\Fix-Apply-PPIQ-Phase1-P0-Hardening-Syntax.ps1
# ============================================================

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptPath = Join-Path (Get-Location) "tools\phase1\Apply-PPIQ-Phase1-P0-Hardening.ps1"

if (-not (Test-Path $scriptPath)) {
    throw "Could not find $scriptPath"
}

Copy-Item $scriptPath "$scriptPath.before-syntax-fix.bak" -Force

$text = Get-Content $scriptPath -Raw

$startMarker = 'Write-Utf8File -RelativePath "Frontend\PlantProcess.Web\scripts\validate-forbidden-copy.mjs" -Content @'''
$endMarker = 'if (-not $SkipPackageJsonPatch) {'

$start = $text.IndexOf($startMarker)
$end = $text.IndexOf($endMarker, $start)

if ($start -lt 0 -or $end -lt 0) {
    throw "Could not locate validate-forbidden-copy.mjs block safely. Replace the whole Apply script instead."
}

$fixedLines = @(
"`$validateForbiddenCopyMjs = @'",
'import { readdirSync, readFileSync, statSync } from "node:fs";',
'import { join, relative } from "node:path";',
'',
'const root = process.cwd();',
'const srcRoot = join(root, "src");',
'',
'const forbidden = [',
'  /could\s+not\s+be\s+loaded/i,',
'  /could\s+not\s+load/i,',
'];',
'',
'const allowedExtensions = new Set([".ts", ".tsx", ".js", ".jsx"]);',
'const ignoredDirectories = new Set([',
'  "node_modules",',
'  "dist",',
'  "build",',
'  "coverage",',
'  "playwright-report",',
'  "test-results",',
']);',
'',
'function extensionOf(filePath) {',
'  const idx = filePath.lastIndexOf(".");',
'  return idx >= 0 ? filePath.slice(idx) : "";',
'}',
'',
'function walk(dir, results = []) {',
'  for (const entry of readdirSync(dir)) {',
'    if (ignoredDirectories.has(entry)) continue;',
'',
'    const full = join(dir, entry);',
'    const stat = statSync(full);',
'',
'    if (stat.isDirectory()) {',
'      walk(full, results);',
'      continue;',
'    }',
'',
'    if (allowedExtensions.has(extensionOf(full))) {',
'      results.push(full);',
'    }',
'  }',
'',
'  return results;',
'}',
'',
'const failures = [];',
'',
'for (const file of walk(srcRoot)) {',
'  const text = readFileSync(file, "utf8");',
'',
'  for (const pattern of forbidden) {',
'    if (pattern.test(text)) {',
'      failures.push(relative(root, file));',
'      break;',
'    }',
'  }',
'}',
'',
'if (failures.length > 0) {',
'  console.error("");',
'  console.error("PPIQ-T001 failed: forbidden customer-visible failure copy is still present.");',
'  console.error("");',
'',
'  for (const file of failures) {',
'    console.error(` - ${file}`);',
'  }',
'',
'  console.error("");',
'  console.error("Use the Refreshing pattern instead.");',
'  process.exit(1);',
'}',
'',
'console.log("PPIQ-T001 passed: forbidden frontend copy is absent.");',
"'@",
'',
'Write-Utf8File -RelativePath "Frontend\PlantProcess.Web\scripts\validate-forbidden-copy.mjs" -Content $validateForbiddenCopyMjs',
''
)

$fixedBlock = $fixedLines -join [Environment]::NewLine

$text = $text.Substring(0, $start) + $fixedBlock + $text.Substring($end)

# If the file was pasted incompletely, remove an unfinished README here-string tail.
$unfinishedReadme = 'Write-Utf8File -RelativePath "Infrastructure\deploy\README.md" -Content @'''
$readmeStart = $text.IndexOf($unfinishedReadme)

if ($readmeStart -ge 0) {
    $tail = $text.Substring($readmeStart)

    if (-not $tail.Contains("'@")) {
        $text = $text.Substring(0, $readmeStart) + @'

Write-Host "Stopped before truncated README block. Re-run with the full v2 script for PPIQ-T002 to PPIQ-T008."
'@
    }
}

Set-Content -Path $scriptPath -Value $text -Encoding UTF8

Write-Host "Syntax wrapper fixed."
Write-Host "Now re-run:"
Write-Host "powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\phase1\Apply-PPIQ-Phase1-P0-Hardening.ps1"
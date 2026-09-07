<#
================================================================================
PlantProcess IQ - T-204 POST-CLOSE PROOF (clear the DLL lock, re-prove the state)
================================================================================
Backlog task : T-204   Release: M2   Owner: Worker 2 (Release Truth)
File         : tools\run\Run-T204-PostCloseProof.ps1

WHAT HAPPENED

  The 222250 closure run went 9/9 GREEN and landed both exact commits:
    fda861fd  Restore governed associative filter propagation
    c6491e2d  Certify associative cross-filter behavior
  Git rollback was intentionally suppressed by the closure script, so T-204's
  certification is committed and stays committed.

  The ONLY red after that was the post-commit T-088 cross-task proof: its
  dotnet build failed with MSB3027 because the API process the closure script
  left running (PlantProcess.Api) still held the output DLLs. That is an
  environmental file lock, not a product or test defect.

WHAT THIS RUNNER DOES

  Verify   report only: the two commits, the six-file cleanliness, and which
           processes currently hold port 5063 / the Api output DLLs.
  Apply    stop the stale PlantProcess.Api process(es), rebuild the Api,
           re-run the Domain and Application suites, and compare their
           fingerprints to the frozen baselines the closure run recorded:
             Domain       failed=1, and the one failure IS the pre-existing
                          DefinitionVersion BaseEntity test (foreign to T-204)
             Application  failed=0
  It edits no source file and touches no commit.

EXIT  0 = proof GREEN   2 = could not run   4 = proof failed
================================================================================
#>

[CmdletBinding()]
param(
    [ValidateSet('Verify', 'Apply')]
    [string]$Mode = 'Verify',
    [string]$RepoRoot = 'C:\Workspace\PlantProcess-IQ',
    [int]$ApiPort = 5063
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
$script:Failures = New-Object System.Collections.ArrayList

function Write-Head([string]$t) { Write-Host ''; Write-Host ('=' * 78) -ForegroundColor DarkCyan; Write-Host $t -ForegroundColor Cyan; Write-Host ('=' * 78) -ForegroundColor DarkCyan }
function Write-Ok  ([string]$t) { Write-Host "  [OK]   $t" -ForegroundColor Green }
function Write-Warn([string]$t) { Write-Host "  [WARN] $t" -ForegroundColor Yellow }
function Write-Bad ([string]$t) { Write-Host "  [FAIL] $t" -ForegroundColor Red; [void]$script:Failures.Add($t) }
function Write-Inf ([string]$t) { Write-Host "  [INFO] $t" -ForegroundColor Gray }

# PS 5.1: a native command writing to stderr under stream redirection with
# EAP=Stop becomes a TERMINATING NativeCommandError. dotnet test writes its
# [FAIL] lines to stderr, so the EXPECTED pre-existing Domain failure killed the
# first revision of this runner at the call site. Native commands therefore run
# through cmd, which merges the streams itself, with EAP relaxed for the call.
function Invoke-Native([string]$CommandLine) {
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $output = & cmd /c ($CommandLine + ' 2>&1')
        return @{ Exit = $LASTEXITCODE; Output = $output }
    } finally {
        $ErrorActionPreference = $previous
    }
}

if (-not (Test-Path -LiteralPath $RepoRoot)) { Write-Bad "Repo not found: $RepoRoot"; exit 2 }
$RepoRootFull = [System.IO.Path]::GetFullPath((Resolve-Path -LiteralPath $RepoRoot).Path).TrimEnd('\')

Write-Head "T-204 POST-CLOSE PROOF  -  MODE: $Mode"

# ============================================================ GIT STATE =======
Write-Head 'STEP 1 - THE TWO CLOSURE COMMITS'
Push-Location -LiteralPath $RepoRootFull
try {
    $headSubject = (& git log -1 --pretty=%s).Trim()
    $headHash = (& git rev-parse --short HEAD).Trim()
    $parentSubject = (& git log -1 --pretty=%s 'HEAD~1').Trim()
    $parentHash = (& git rev-parse --short 'HEAD~1').Trim()

    if ($headSubject -eq 'Certify associative cross-filter behavior') { Write-Ok "HEAD $headHash : $headSubject" }
    else { Write-Bad "HEAD $headHash subject is '$headSubject', expected the T-204 certification commit" }

    if ($parentSubject -eq 'Restore governed associative filter propagation') { Write-Ok "HEAD~1 $parentHash : $parentSubject" }
    else { Write-Bad "HEAD~1 $parentHash subject is '$parentSubject', expected the backend prerequisite commit" }

    $sixDirty = @(& git status --porcelain -- 'Frontend/PlantProcess.Web/e2e/release-truth/' 'Frontend/PlantProcess.Web/release-truth.globalSetup.ts' 'Frontend/PlantProcess.Web/playwright.release-truth.config.ts')
    if ($sixDirty.Count -eq 0) { Write-Ok 'all six T-204 files are committed clean' }
    else { Write-Bad ('T-204 files still dirty after the closure commits: ' + ($sixDirty -join ', ')) }
} finally { Pop-Location }

# ============================================================ LOCK OWNERS =====
Write-Head 'STEP 2 - WHO HOLDS THE LOCK'
$lockOwners = @()

$byName = @(Get-Process -Name 'PlantProcess.Api' -ErrorAction SilentlyContinue)
foreach ($p in $byName) { $lockOwners += $p }

try {
    $conn = Get-NetTCPConnection -LocalPort $ApiPort -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -ne $conn) {
        $portOwner = Get-Process -Id $conn.OwningProcess -ErrorAction SilentlyContinue
        if ($null -ne $portOwner -and -not (@($lockOwners | Where-Object { $_.Id -eq $portOwner.Id }).Count -gt 0)) {
            $lockOwners += $portOwner
        }
    }
} catch { }

if ($lockOwners.Count -eq 0) {
    Write-Ok 'no PlantProcess.Api process and no listener on the API port - the lock is already gone'
} else {
    foreach ($p in $lockOwners) { Write-Warn "lock candidate: $($p.ProcessName) (pid $($p.Id))" }
}

if ($Mode -eq 'Verify') {
    Write-Head 'RESULT'
    if ($script:Failures.Count -eq 0) {
        Write-Ok 'Verify SUCCEEDED. Run -Mode Apply to clear the lock and re-prove the build and test fingerprints.'
        exit 0
    }
    foreach ($f in $script:Failures) { Write-Host "  - $f" -ForegroundColor Red }
    Write-Bad 'Verify FAILED.'
    exit 4
}

# ============================================================ APPLY ===========
Write-Head 'STEP 3 - STOP THE STALE API'
foreach ($p in $lockOwners) {
    try {
        Stop-Process -Id $p.Id -Force -ErrorAction Stop
        Write-Ok "stopped $($p.ProcessName) (pid $($p.Id))"
    } catch {
        Write-Warn "could not stop pid $($p.Id): $_"
    }
}
if ($lockOwners.Count -gt 0) { Start-Sleep -Seconds 3 }

Write-Head 'STEP 4 - REBUILD THE API (this is exactly what MSB3027 blocked)'
Push-Location -LiteralPath $RepoRootFull
try { $build = Invoke-Native 'dotnet build Backend\PlantProcess.Api\PlantProcess.Api.csproj -c Debug --nologo' } finally { Pop-Location }
if ($build.Exit -ne 0) {
    Write-Bad 'the Api build still fails with the lock cleared:'
    $build.Output | Select-Object -Last 20 | ForEach-Object { Write-Host "         $_" -ForegroundColor Red }
} else {
    Write-Ok 'Api build GREEN'
}

Write-Head 'STEP 5 - DOMAIN FINGERPRINT (frozen baseline: failed=1, the pre-existing DefinitionVersion test)'
Push-Location -LiteralPath $RepoRootFull
try { $domain = Invoke-Native 'dotnet test Backend\tests\PlantProcess.Domain.Tests\PlantProcess.Domain.Tests.csproj -c Debug --nologo' } finally { Pop-Location }
$domainText = ($domain.Output | Out-String)
$domainCounts = [regex]::Match($domainText, 'Failed:\s+(\d+),\s+Passed:\s+(\d+),\s+Skipped:\s+(\d+),\s+Total:\s+(\d+)')
if (-not $domainCounts.Success) {
    Write-Bad 'could not read the Domain test counters'
} else {
    $dFailed = [int]$domainCounts.Groups[1].Value
    $dTotal = [int]$domainCounts.Groups[4].Value
    $knownForeign = $domainText.Contains('All_real_domain_entities_should_inherit_base_entity')
    if ($dFailed -eq 1 -and $knownForeign) {
        Write-Ok "Domain fingerprint unchanged: failed=1/$dTotal and the one failure is the pre-existing DefinitionVersion BaseEntity test (foreign to T-204, tracked outside it)"
    } elseif ($dFailed -eq 0) {
        Write-Ok "Domain now fully GREEN: 0/$dTotal - better than the frozen baseline"
    } else {
        Write-Bad "Domain fingerprint moved: failed=$dFailed/$dTotal - this is a regression against the closure baseline"
        $domain.Output | Select-Object -Last 25 | ForEach-Object { Write-Host "         $_" -ForegroundColor Red }
    }
}

Write-Head 'STEP 6 - APPLICATION FINGERPRINT (frozen baseline: failed=0)'
Push-Location -LiteralPath $RepoRootFull
try { $app = Invoke-Native 'dotnet test Backend\tests\PlantProcess.Application.UnitTests\PlantProcess.Application.UnitTests.csproj -c Debug --nologo' } finally { Pop-Location }
$appText = ($app.Output | Out-String)
$appCounts = [regex]::Match($appText, 'Failed:\s+(\d+),\s+Passed:\s+(\d+),\s+Skipped:\s+(\d+),\s+Total:\s+(\d+)')
if (-not $appCounts.Success) {
    Write-Bad 'could not read the Application test counters'
} else {
    $aFailed = [int]$appCounts.Groups[1].Value
    $aTotal = [int]$appCounts.Groups[4].Value
    if ($aFailed -eq 0) { Write-Ok "Application GREEN: 0/$aTotal" }
    else {
        Write-Bad "Application fingerprint moved: failed=$aFailed/$aTotal"
        $app.Output | Select-Object -Last 25 | ForEach-Object { Write-Host "         $_" -ForegroundColor Red }
    }
}

Write-Head 'RESULT'
if ($script:Failures.Count -eq 0) {
    Write-Ok 'POST-CLOSE PROOF GREEN. The MSB3027 red was the stale API process holding the DLLs, nothing else.'
    Write-Ok 'T-204 stands closed at commit c6491e2d (parent fda861fd), 9/9 behavioral gates GREEN.'
    exit 0
}
foreach ($f in $script:Failures) { Write-Host "  - $f" -ForegroundColor Red }
Write-Bad 'POST-CLOSE PROOF FAILED - see the lines above.'
exit 4

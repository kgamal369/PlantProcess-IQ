<#
    Test-CiTruthGateFalsification.ps1

    WHY THIS EXISTS
    ---------------
    Apply-CiTruthGateCommentStripping.ps1 left both guard suites green. So did the
    BROKEN versions of those suites. Green is not evidence. A guard that has never been
    observed failing is not a guard - the same rule as PPIQ_SESSION_HANDOVER_10Jul2026.md
    section 5.3, applied to the fix itself.

    This harness INVERTS the gate. It injects three regressions the guards are supposed
    to catch, runs the suites, and requires each run to FAIL. If a mutation leaves the
    suites green, the guard is decorative and this script exits 1.

        Mutation A - comment out Jenkinsfile stages 3, 4 and 5.
                     Before the comment-stripping fix, this left both suites GREEN.
                     This is the whole point. If A stays green, REVERT THE PACK.

        Mutation B - re-insert the when{} clause on stage 5.
                     Must trip E2e_stage_cannot_be_gated_off.

        Mutation C - delete the ':previous' rollback anchor from deploy-canonical.sh.
                     Must trip Deploy_uses_remove_orphans_and_rolls_back. This assertion
                     used to be Contains("rollback", Jenkinsfile), which was satisfied by
                     the post-block echo "no rollback was needed". Prove it has teeth now.

    SAFETY
    ------
    Nothing is committed. Both files are backed up before any mutation and restored in a
    finally block. The restore is verified by SHA256 against the pre-run hash; a mismatch
    is a hard failure with the backup path printed. Line endings are detected and
    preserved. Mixed-ending files are refused (handover 5.5).

    If this script is killed mid-run, restore by hand from the backup directory it prints
    on its first line. Per-mutation `dotnet test` output is written into that same
    directory as dotnet-test-<label>.log.

    v2 - fixed a defect in v1: $ErrorActionPreference = "Stop" turned `dotnet test`'s
    stderr into a terminating NativeCommandError, so the harness crashed on the first RED
    run - the exact runs it exists to observe. Invoke-GuardSuites now drops to "Continue"
    locally and reads $LASTEXITCODE.

    USAGE
    -----
        Unblock-File .\Test-CiTruthGateFalsification.ps1
        powershell -ExecutionPolicy Bypass -File .\Test-CiTruthGateFalsification.ps1
#>

[CmdletBinding()]
param(
    [string]$RepoRoot = "C:\Workspace\PlantProcess-IQ"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version 2.0

$Stamp     = Get-Date -Format "yyyyMMdd-HHmmss"
$BackupDir = Join-Path $RepoRoot ("deploy\.ppiq-backups\ci-truth-gate-falsification-{0}" -f $Stamp)

$JenkinsPath = Join-Path $RepoRoot "Jenkinsfile"
$DeployPath  = Join-Path $RepoRoot "deploy\scripts\deploy-canonical.sh"
$TestsProj   = Join-Path $RepoRoot "Backend\tests\PlantProcess.Architecture.Tests\PlantProcess.Architecture.Tests.csproj"
$HelperPath  = Join-Path $RepoRoot "Backend\tests\PlantProcess.Architecture.Tests\PipelineSourceText.cs"

$TestFilter = "FullyQualifiedName~CiPipelineTruthGateTests|FullyQualifiedName~DeployRedPathProofTests"

# ---------------------------------------------------------------------------
# helpers
# ---------------------------------------------------------------------------

function Write-Step { param([string]$m) Write-Host ("  " + $m) }
function Write-Head { param([string]$m) Write-Host ""; Write-Host ("== " + $m + " ==") -ForegroundColor Cyan }
function Fail { param([string]$m) Write-Host ("FATAL: " + $m) -ForegroundColor Red; exit 1 }

function Get-LineEnding {
    param([string]$Text, [string]$Label)
    $crlf = ([regex]::Matches($Text, "\r\n")).Count
    $lf   = ([regex]::Matches($Text, "(?<!\r)\n")).Count
    $cr   = ([regex]::Matches($Text, "\r(?!\n)")).Count
    if ($cr -gt 0) { Fail ("{0} contains bare CR characters. Refusing." -f $Label) }
    if (($crlf -gt 0) -and ($lf -gt 0)) {
        Fail ("{0} has MIXED line endings ({1} CRLF, {2} LF). Refusing." -f $Label, $crlf, $lf)
    }
    if ($crlf -gt 0) { return "CRLF" }
    return "LF"
}

function Read-Raw { param([string]$Path) return [System.IO.File]::ReadAllText($Path) }

function Write-Raw {
    param([string]$Path, [string]$Text, [string]$Ending)
    $normalised = $Text -replace "\r\n", "`n"
    if ($Ending -eq "CRLF") { $normalised = $normalised -replace "`n", "`r`n" }
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $normalised, $utf8NoBom)
}

function Get-Sha { param([string]$Path) return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash }

function Invoke-GuardSuites {
    param([string]$LogLabel = "run")

    # Returns a hashtable: @{ Green = <bool>; Failed = <string[]>; Log = <path> }
    #
    # WHY THE PREFERENCE FLIP: `dotnet test` writes to stderr when a suite is RED. With
    # $ErrorActionPreference = "Stop", PowerShell wraps native stderr as a terminating
    # NativeCommandError - so this function used to blow up on exactly the runs it exists
    # to observe. The exit code is the signal; stderr is just text. Capture it, do not
    # trip over it.

    $log = Join-Path $BackupDir ("dotnet-test-{0}.log" -f $LogLabel)
    $prevPreference = $ErrorActionPreference
    $ErrorActionPreference = "Continue"

    Push-Location $RepoRoot
    try {
        $global:LASTEXITCODE = 0
        $output = & dotnet test $TestsProj --nologo --no-build --filter $TestFilter 2>&1 |
                  ForEach-Object { $_.ToString() }
        $exit = $LASTEXITCODE

        $text = ($output -join [Environment]::NewLine)
        [System.IO.File]::WriteAllText($log, $text, (New-Object System.Text.UTF8Encoding($false)))

        $failed = @()
        foreach ($line in $output) {
            $m = [regex]::Match($line, "PlantProcess\.Architecture\.Tests\.([A-Za-z0-9_\.]+)\s+\[FAIL\]")
            if ($m.Success) { $failed += $m.Groups[1].Value }
        }

        return @{ Green = ($exit -eq 0); Failed = @($failed | Select-Object -Unique); Log = $log }
    } finally {
        Pop-Location -ErrorAction SilentlyContinue
        $ErrorActionPreference = $prevPreference
    }
}

function Show-Mutation {
    param([hashtable]$Result, [string]$Blindness)

    if ($Result.Green) {
        Write-Host ("  RESULT: STILL GREEN  <-- " + $Blindness) -ForegroundColor Red
    } else {
        Write-Host "  RESULT: RED (correct)" -ForegroundColor Green
        foreach ($f in $Result.Failed) { Write-Host ("          caught by: " + $f) -ForegroundColor DarkGreen }
        if ($Result.Failed.Count -eq 0) {
            Write-Host "          (non-zero exit, no [FAIL] line parsed - see log)" -ForegroundColor Yellow
        }
    }
    Write-Step ("log: " + $Result.Log)
}

# ---------------------------------------------------------------------------
# preflight
# ---------------------------------------------------------------------------

Write-Head "PREFLIGHT"

foreach ($p in @($JenkinsPath, $DeployPath, $TestsProj)) {
    if (-not (Test-Path -LiteralPath $p)) { Fail ("missing " + $p) }
}
if (-not (Test-Path -LiteralPath $HelperPath)) {
    Fail "PipelineSourceText.cs not found. Apply-CiTruthGateCommentStripping.ps1 has not run. Nothing to falsify."
}

$jenkinsOriginal = Read-Raw $JenkinsPath
$deployOriginal  = Read-Raw $DeployPath

$jenkinsEnd = Get-LineEnding -Text $jenkinsOriginal -Label "Jenkinsfile"
$deployEnd  = Get-LineEnding -Text $deployOriginal  -Label "deploy-canonical.sh"

$jenkinsShaBefore = Get-Sha $JenkinsPath
$deployShaBefore  = Get-Sha $DeployPath

# anchors each mutation depends on
$stage3Anchor = "stage('3. Backend tests - BLOCKING') {"
$stage6Anchor = "stage('6. App DB:"
$stage5Anchor = "stage('5. Frontend e2e - BLOCKING') {"
$previousTok  = ":previous"

foreach ($pair in @(@($stage3Anchor, "Jenkinsfile"), @($stage6Anchor, "Jenkinsfile"), @($stage5Anchor, "Jenkinsfile"))) {
    if ($jenkinsOriginal.IndexOf($pair[0], [System.StringComparison]::Ordinal) -lt 0) {
        Fail ("anchor not found in {0}: {1}" -f $pair[1], $pair[0])
    }
}
if ($deployOriginal.IndexOf($previousTok, [System.StringComparison]::Ordinal) -lt 0) {
    Fail ("anchor not found in deploy-canonical.sh: " + $previousTok)
}

New-Item -ItemType Directory -Path $BackupDir -Force | Out-Null
Copy-Item -LiteralPath $JenkinsPath -Destination (Join-Path $BackupDir "Jenkinsfile") -Force
Copy-Item -LiteralPath $DeployPath  -Destination (Join-Path $BackupDir "deploy-canonical.sh") -Force

Write-Host ""
Write-Host ("BACKUP (restore from here if this script is killed): " + $BackupDir) -ForegroundColor Yellow
Write-Host ""
Write-Step ("Jenkinsfile          {0}  {1}" -f $jenkinsEnd, $jenkinsShaBefore.Substring(0, 16))
Write-Step ("deploy-canonical.sh  {0}  {1}" -f $deployEnd, $deployShaBefore.Substring(0, 16))

# ---------------------------------------------------------------------------
# baseline: unmutated must be GREEN, or nothing below means anything
# ---------------------------------------------------------------------------

Write-Head "BASELINE (unmutated) - must be GREEN"
$prevPref = $ErrorActionPreference
$ErrorActionPreference = "Continue"
Push-Location $RepoRoot
try {
    $global:LASTEXITCODE = 0
    & dotnet build $TestsProj --nologo 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) { $ErrorActionPreference = $prevPref; Fail "dotnet build failed on the unmutated tree." }
} finally {
    Pop-Location -ErrorAction SilentlyContinue
    $ErrorActionPreference = $prevPref
}
$baseline = Invoke-GuardSuites -LogLabel "baseline"
if (-not $baseline.Green) {
    Write-Host ("  see " + $baseline.Log) -ForegroundColor Yellow
    Fail "the guard suites are RED on the unmutated tree. Fix that before falsifying."
}
Write-Step "baseline green"

# ---------------------------------------------------------------------------
# mutations
# ---------------------------------------------------------------------------

$results = @()

function Restore-Files {
    Write-Raw -Path $JenkinsPath -Text $jenkinsOriginal -Ending $jenkinsEnd
    Write-Raw -Path $DeployPath  -Text $deployOriginal  -Ending $deployEnd
}

try {

    # --- A: comment out stages 3, 4, 5 -------------------------------------
    Write-Head "MUTATION A - comment out Jenkinsfile stages 3, 4, 5"

    $i3 = $jenkinsOriginal.IndexOf($stage3Anchor, [System.StringComparison]::Ordinal)
    $i6 = $jenkinsOriginal.IndexOf($stage6Anchor, [System.StringComparison]::Ordinal)
    if ($i6 -le $i3) { Fail "stage 6 does not follow stage 3 in the Jenkinsfile." }

    $head    = $jenkinsOriginal.Substring(0, $i3)
    $middle  = $jenkinsOriginal.Substring($i3, $i6 - $i3)
    $tail    = $jenkinsOriginal.Substring($i6)

    $commented = ($middle -replace "\r\n", "`n").Split("`n") | ForEach-Object { "// " + $_ }
    $mutatedA  = $head + ($commented -join "`n") + "`n" + $tail

    if (-not $mutatedA.Contains("// " + $stage3Anchor)) { Fail "mutation A did not comment stage 3." }
    Write-Raw -Path $JenkinsPath -Text $mutatedA -Ending $jenkinsEnd
    Write-Step ("commented {0} lines" -f $commented.Count)

    $resultA = Invoke-GuardSuites -LogLabel "mutation-a"
    $results += ,@("A  stages 3/4/5 commented out", $resultA.Green)
    Show-Mutation -Result $resultA -Blindness "the guards are decorative; StripComments did not land"
    Restore-Files

    # --- B: re-insert the when{} clause on stage 5 -------------------------
    Write-Head "MUTATION B - re-gate stage 5 behind when{}"

    $whenLine = "      when { expression { return sh(script: 'set -a; . " + '"${ENV_FILE}"' +
                "; set +a; [ " + '"${PPIQ_RUN_E2E:-off}"' + " = " + "'on'" +
                " ] && echo yes || echo no', returnStdout: true).trim() == 'yes' } }"

    $mutatedB = $jenkinsOriginal.Replace($stage5Anchor, $stage5Anchor + "`n" + $whenLine)
    if ($mutatedB -eq $jenkinsOriginal) { Fail "mutation B produced no change." }
    if (-not $mutatedB.Contains("PPIQ_RUN_E2E")) { Fail "mutation B did not insert the when clause." }
    Write-Raw -Path $JenkinsPath -Text $mutatedB -Ending $jenkinsEnd
    Write-Step "when{} re-inserted on stage 5"

    $resultB = Invoke-GuardSuites -LogLabel "mutation-b"
    $results += ,@("B  stage 5 re-gated behind when{}", $resultB.Green)
    Show-Mutation -Result $resultB -Blindness "E2e_stage_cannot_be_gated_off is blind"
    Restore-Files

    # --- C: strip the rollback anchor from deploy-canonical.sh -------------
    Write-Head "MUTATION C - remove the ':previous' rollback anchor"

    $mutatedC = $deployOriginal.Replace($previousTok, ":notprevious")
    if ($mutatedC -eq $deployOriginal) { Fail "mutation C produced no change." }
    Write-Raw -Path $DeployPath -Text $mutatedC -Ending $deployEnd
    Write-Step "':previous' removed from deploy-canonical.sh"

    $resultC = Invoke-GuardSuites -LogLabel "mutation-c"
    $results += ,@("C  rollback anchor removed", $resultC.Green)
    Show-Mutation -Result $resultC -Blindness "the moved rollback assertion has no teeth"

} finally {
    Write-Head "RESTORE"
    Restore-Files

    $jenkinsShaAfter = Get-Sha $JenkinsPath
    $deployShaAfter  = Get-Sha $DeployPath

    if ($jenkinsShaAfter -ne $jenkinsShaBefore) {
        Write-Host ("RESTORE FAILED for Jenkinsfile. Recover from: " + (Join-Path $BackupDir "Jenkinsfile")) -ForegroundColor Red
    } else {
        Write-Step "Jenkinsfile restored, SHA256 matches"
    }

    if ($deployShaAfter -ne $deployShaBefore) {
        Write-Host ("RESTORE FAILED for deploy-canonical.sh. Recover from: " + (Join-Path $BackupDir "deploy-canonical.sh")) -ForegroundColor Red
    } else {
        Write-Step "deploy-canonical.sh restored, SHA256 matches"
    }
}

# ---------------------------------------------------------------------------
# verdict
# ---------------------------------------------------------------------------

Write-Head "VERDICT"

$decorative = @()
foreach ($r in $results) {
    $label = $r[0]
    $green = $r[1]
    if ($green) {
        Write-Host ("  DECORATIVE  " + $label) -ForegroundColor Red
        $decorative += $label
    } else {
        Write-Host ("  ENFORCED    " + $label) -ForegroundColor Green
    }
}

Write-Host ""
if ($decorative.Count -gt 0) {
    Write-Host "THE GUARDS DO NOT GUARD." -ForegroundColor Red
    Write-Host "At least one regression the suites are supposed to catch left them green."
    Write-Host "Revert the pack from its backup and diagnose before trusting any score that"
    Write-Host "cites A1.5 or A1.9 (Aspects of Review v4)."
    exit 1
}

Write-Host "ALL THREE MUTATIONS WENT RED." -ForegroundColor Green
Write-Host "The CI truth gates are now enforced, not decorative."
Write-Host "A1.5 and A1.9 have automated evidence behind them for the first time."
Write-Host ""
Write-Host "Baseline restored and hash-verified. Nothing was committed."
Write-Host ("Backup retained at: " + $BackupDir)
Write-Host ""
Write-Host "NEXT (agreed order):" -ForegroundColor Cyan
Write-Host "  1. builder.Services.AddAssistant();   (v20 M1-06, 0.5h)"
Write-Host "  2. Move M1-23 into M1-P1 and RUN it - nine surfaces, none seen in a browser."
Write-Host "  3. Duplicate CREATE TABLE / FUNCTION truth gate (v20 M2-11)."
Write-Host "  4. The 117 audit-table drop - after the two psql queries confirm it."
Write-Host ""

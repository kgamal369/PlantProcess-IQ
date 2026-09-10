# ============================================================================
# Test-ReleaseEvidenceEnvelope.ps1
#
# Executable semantic authority for the Release Evidence Envelope v1.
#
# The JSON Schema beside the fixtures states the portable structure. This states
# the arithmetic and the verdict prerequisites, which is the part a schema cannot
# prove: that the counts add up, that every skip is accounted for by a reason,
# and that no producer can call itself GREEN while carrying a failure or an
# unapproved skip.
#
# It never edits the envelope. A producer that reported wrong counts has a defect
# worth seeing, and a validator that quietly corrects it hides exactly the thing
# release evidence exists to expose.
#
#   powershell -NoProfile -ExecutionPolicy Bypass `
#     -File tools/validation/Test-ReleaseEvidenceEnvelope.ps1 -Path <evidence.json>
#
# -RepositoryRoot additionally proves every evidence_paths entry exists on disk.
# Without it, validation is structural and needs no repository, no database and
# no product runtime, so any producer can call it anywhere.
#
# EXIT 0 valid - EXIT 1 invalid - EXIT 2 could not read the file
# ============================================================================
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string] $Path,
    [string] $RepositoryRoot = "",
    [switch] $Quiet
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Continue"

$SupportedContractVersion = "1.0"
$AllowedVerdicts = @("GREEN", "RED")
$RequiredFields = @(
    "contract_version", "task_id", "gate_id", "suite_id", "environment", "profile",
    "started_at", "completed_at", "total_count", "executed_count", "passed_count",
    "failed_count", "skipped_count", "unapproved_skip_count", "skip_reasons",
    "commit_sha", "evidence_paths", "verdict"
)
$RequiredSkipFields = @("reason_code", "count", "approved", "reason")

$script:Problems = New-Object System.Collections.ArrayList
function Reject([string]$code, [string]$detail) { [void]$script:Problems.Add($code + ": " + $detail) }
function Note([string]$t) { if (-not $Quiet) { Write-Host $t } }

if (-not (Test-Path -LiteralPath $Path)) { Write-Host ("[FAIL]   evidence file not found: " + $Path); exit 2 }

$raw = Get-Content -LiteralPath $Path -Raw
try { $e = $raw | ConvertFrom-Json } catch {
    Write-Host ("[FAIL]   " + $Path + " is not valid JSON: " + $_.Exception.Message)
    exit 2
}
if ($null -eq $e -or $e -is [array]) { Write-Host ("[FAIL]   " + $Path + " is not a JSON object"); exit 2 }

$present = @($e.PSObject.Properties.Name)

foreach ($f in $RequiredFields) { if ($present -notcontains $f) { Reject "MISSING_FIELD" $f } }
foreach ($f in $present) { if ($RequiredFields -notcontains $f) { Reject "UNKNOWN_FIELD" ($f + " is not part of contract 1.0; a new common field is a deliberate contract revision, not an ad-hoc extension") } }
if ($script:Problems.Count -gt 0) {
    Write-Host ("[FAIL]   " + $Path)
    foreach ($p in $script:Problems) { Write-Host ("           " + $p) }
    exit 1
}

# ---- 1. supported contract version -----------------------------------------
if (("" + $e.contract_version) -cne $SupportedContractVersion) {
    Reject "UNSUPPORTED_CONTRACT_VERSION" ("expected '" + $SupportedContractVersion + "', found '" + $e.contract_version + "'")
}

# ---- identity ---------------------------------------------------------------
if ([string]::IsNullOrWhiteSpace("" + $e.task_id)) { Reject "EMPTY_TASK_ID" "task_id must name the producing task" }

# ---- 2. at least one of gate_id / suite_id ---------------------------------
$hasGate  = -not [string]::IsNullOrWhiteSpace("" + $e.gate_id)
$hasSuite = -not [string]::IsNullOrWhiteSpace("" + $e.suite_id)
if (-not $hasGate -and -not $hasSuite) {
    Reject "NO_PRODUCER_IDENTITY" "at least one of gate_id or suite_id must be populated; evidence that names neither cannot be traced to a producer"
}

foreach ($f in @("environment", "profile")) {
    if ([string]::IsNullOrWhiteSpace("" + $e.$f)) { Reject "EMPTY_FIELD" $f }
}

# ---- 3. completed_at >= started_at -----------------------------------------
$startedAt = [datetime]::MinValue
$completedAt = [datetime]::MinValue
$styles = [System.Globalization.DateTimeStyles]::RoundtripKind
if (-not [datetime]::TryParse(("" + $e.started_at), [System.Globalization.CultureInfo]::InvariantCulture, $styles, [ref]$startedAt)) {
    Reject "UNPARSEABLE_TIMESTAMP" ("started_at '" + $e.started_at + "'")
}
if (-not [datetime]::TryParse(("" + $e.completed_at), [System.Globalization.CultureInfo]::InvariantCulture, $styles, [ref]$completedAt)) {
    Reject "UNPARSEABLE_TIMESTAMP" ("completed_at '" + $e.completed_at + "'")
}
if ($startedAt -ne [datetime]::MinValue -and $completedAt -ne [datetime]::MinValue) {
    if ($completedAt.ToUniversalTime() -lt $startedAt.ToUniversalTime()) {
        Reject "COMPLETED_BEFORE_STARTED" ("completed_at " + $e.completed_at + " precedes started_at " + $e.started_at)
    }
}

# ---- counts are non-negative integers, never strings or floats --------------
$counts = @{}
foreach ($f in @("total_count", "executed_count", "passed_count", "failed_count", "skipped_count", "unapproved_skip_count")) {
    $v = $e.$f
    if ($v -isnot [int] -and $v -isnot [long]) { Reject "NON_INTEGER_COUNT" ($f + " must be an integer, found " + $(if ($null -eq $v) { "null" } else { $v.GetType().Name })) ; continue }
    if ([int64]$v -lt 0) { Reject "NEGATIVE_COUNT" ($f + " = " + $v) ; continue }
    $counts[$f] = [int64]$v
}

if ($counts.Count -eq 6) {
    # ---- 4. total = executed + skipped -------------------------------------
    if ($counts["total_count"] -ne ($counts["executed_count"] + $counts["skipped_count"])) {
        Reject "TOTAL_COUNT_MISMATCH" ("total_count " + $counts["total_count"] + " <> executed_count " + $counts["executed_count"] + " + skipped_count " + $counts["skipped_count"] + "; a skipped test is not an executed test")
    }
    # ---- 5. executed = passed + failed -------------------------------------
    if ($counts["executed_count"] -ne ($counts["passed_count"] + $counts["failed_count"])) {
        Reject "EXECUTED_COUNT_MISMATCH" ("executed_count " + $counts["executed_count"] + " <> passed_count " + $counts["passed_count"] + " + failed_count " + $counts["failed_count"])
    }
    # ---- 6. unapproved <= skipped ------------------------------------------
    if ($counts["unapproved_skip_count"] -gt $counts["skipped_count"]) {
        Reject "UNAPPROVED_EXCEEDS_SKIPPED" ("unapproved_skip_count " + $counts["unapproved_skip_count"] + " > skipped_count " + $counts["skipped_count"])
    }
}

# ---- skip reasons ------------------------------------------------------------
$reasons = @($e.skip_reasons)
$reasonSum = [int64]0
$unapprovedSum = [int64]0
$i = 0
foreach ($r in $reasons) {
    $i = $i + 1
    if ($null -eq $r) { Reject "MALFORMED_SKIP_REASON" ("entry " + $i + " is null"); continue }
    $rp = @($r.PSObject.Properties.Name)
    foreach ($f in $RequiredSkipFields) { if ($rp -notcontains $f) { Reject "MALFORMED_SKIP_REASON" ("entry " + $i + " is missing " + $f) } }
    foreach ($f in $rp) { if ($RequiredSkipFields -notcontains $f) { Reject "MALFORMED_SKIP_REASON" ("entry " + $i + " carries unknown field " + $f) } }
    if ($rp -contains "reason_code" -and [string]::IsNullOrWhiteSpace("" + $r.reason_code)) { Reject "MALFORMED_SKIP_REASON" ("entry " + $i + " has an empty reason_code") }
    if ($rp -contains "reason" -and [string]::IsNullOrWhiteSpace("" + $r.reason)) { Reject "MALFORMED_SKIP_REASON" ("entry " + $i + " has an empty reason") }
    if ($rp -contains "approved" -and $r.approved -isnot [bool]) { Reject "MALFORMED_SKIP_REASON" ("entry " + $i + " approved must be a boolean") }
    if ($rp -contains "count") {
        if ($r.count -isnot [int] -and $r.count -isnot [long]) { Reject "MALFORMED_SKIP_REASON" ("entry " + $i + " count must be an integer") }
        elseif ([int64]$r.count -lt 1) { Reject "MALFORMED_SKIP_REASON" ("entry " + $i + " count must be at least 1") }
        else {
            $reasonSum = $reasonSum + [int64]$r.count
            if ($rp -contains "approved" -and $r.approved -is [bool] -and -not $r.approved) { $unapprovedSum = $unapprovedSum + [int64]$r.count }
        }
    }
}

if ($counts.ContainsKey("skipped_count")) {
    # ---- 7. reasons account for every skip ---------------------------------
    if ($reasonSum -ne $counts["skipped_count"]) {
        Reject "SKIP_REASON_TOTAL_MISMATCH" ("skip_reasons sum to " + $reasonSum + " but skipped_count is " + $counts["skipped_count"] + "; there are no mystery skips")
    }
    # ---- 9. no skips means no reasons --------------------------------------
    if ($counts["skipped_count"] -eq 0 -and $reasons.Count -ne 0) {
        Reject "SKIP_REASONS_WITHOUT_SKIPS" ("skipped_count is 0 but " + $reasons.Count + " skip reason(s) are present")
    }
}
if ($counts.ContainsKey("unapproved_skip_count")) {
    # ---- 8. unapproved reasons account for the unapproved count ------------
    if ($unapprovedSum -ne $counts["unapproved_skip_count"]) {
        Reject "UNAPPROVED_SKIP_MISMATCH" ("unapproved skip reasons sum to " + $unapprovedSum + " but unapproved_skip_count is " + $counts["unapproved_skip_count"])
    }
}

# ---- 13. full commit SHA -----------------------------------------------------
if (("" + $e.commit_sha) -cnotmatch "^[0-9a-fA-F]{40}$") {
    Reject "INVALID_COMMIT_SHA" ("'" + $e.commit_sha + "' is not a full 40-character Git SHA; a branch name or abbreviation cannot identify what was certified")
}

# ---- 12. evidence paths ------------------------------------------------------
$paths = @($e.evidence_paths)
if ($paths.Count -lt 1) { Reject "NO_EVIDENCE_PATHS" "evidence_paths must name at least one artifact" }
foreach ($p in $paths) {
    $s = "" + $p
    if ([string]::IsNullOrWhiteSpace($s)) { Reject "EMPTY_EVIDENCE_PATH" "an evidence_paths entry is empty" ; continue }
    if ($s -match "^[A-Za-z]:[\\/]" -or $s.StartsWith("\\\\") -or $s.StartsWith("/")) {
        Reject "ABSOLUTE_EVIDENCE_PATH" ("'" + $s + "' is absolute; the envelope must stay portable between environments")
    }
}

# ---- verdict -----------------------------------------------------------------
$verdict = "" + $e.verdict
if ($AllowedVerdicts -notcontains $verdict) {
    Reject "INVALID_VERDICT" ("'" + $verdict + "' is not one of " + ($AllowedVerdicts -join ", "))
} elseif ($verdict -eq "GREEN") {
    # ---- 10 and 11. GREEN has prerequisites --------------------------------
    if ($counts.ContainsKey("failed_count") -and $counts["failed_count"] -gt 0) {
        Reject "GREEN_WITH_FAILURE" ("verdict is GREEN but failed_count is " + $counts["failed_count"])
    }
    if ($counts.ContainsKey("unapproved_skip_count") -and $counts["unapproved_skip_count"] -gt 0) {
        Reject "GREEN_WITH_UNAPPROVED_SKIP" ("verdict is GREEN but unapproved_skip_count is " + $counts["unapproved_skip_count"])
    }
}
# RED is never rejected for being RED. A producer may fail for a domain reason
# this envelope cannot see - a hash mismatch, a forbidden artifact - and the
# prerequisites above are necessary for GREEN, not sufficient for it.

# ---- optional: prove the evidence actually exists ---------------------------
if ($RepositoryRoot -ne "") {
    if (-not (Test-Path -LiteralPath $RepositoryRoot)) { Reject "REPOSITORY_ROOT_NOT_FOUND" $RepositoryRoot }
    else {
        foreach ($p in $paths) {
            $s = "" + $p
            if ([string]::IsNullOrWhiteSpace($s)) { continue }
            $full = Join-Path $RepositoryRoot ($s -replace "/", [System.IO.Path]::DirectorySeparatorChar)
            if (-not (Test-Path -LiteralPath $full)) { Reject "EVIDENCE_PATH_MISSING" ($s + " does not exist under " + $RepositoryRoot) }
        }
    }
}

if ($script:Problems.Count -gt 0) {
    Write-Host ("[FAIL]   " + $Path)
    foreach ($p in $script:Problems) { Write-Host ("           " + $p) }
    exit 1
}

Note ("[OK]     " + (Split-Path -Leaf $Path) + "   " + $e.task_id + "  " + $verdict + "  total " + $e.total_count + " = executed " + $e.executed_count + " + skipped " + $e.skipped_count)
exit 0

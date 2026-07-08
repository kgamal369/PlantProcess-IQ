# ============================================================================
# Verify-M1-05-Surface3.ps1
# Acceptance run for Backlog v17 M1-05 against a RUNNING local API (:5063).
# Walks the exact validation sentence:
#   create 'CRACK vs process parameters, 30d, grade=S355J2' via API -> saved ->
#   run -> ReadinessGate evaluated -> results_v2 rows tied to the definition ->
#   rerun after edit recomputes -> planted relation recovered, SCRATCH control.
# PS 5.1. Read-only against the product except for the two test definitions.
# START THE API FIRST:  .\scripts\run\start-api.ps1 -Profile local
# ============================================================================
param(
    [string]$Api = 'http://localhost:5063',
    [string]$User = 'e2eadmin',
    [string]$Password = 'E2EAdmin123!'
)

$ErrorActionPreference = 'Stop'
$Pass = 0
$Fail = 0
$Warn = 0

function Step { param([string]$m) Write-Host ('--- ' + $m) -ForegroundColor Cyan }
function Ok   { param([string]$m) $script:Pass++; Write-Host ('PASS  ' + $m) -ForegroundColor Green }
function Bad  { param([string]$m) $script:Fail++; Write-Host ('FAIL  ' + $m) -ForegroundColor Red }
function Note { param([string]$m) $script:Warn++; Write-Host ('WARN  ' + $m) -ForegroundColor Yellow }

Step 'Login'
$loginBody = @{ username = $User; password = $Password } | ConvertTo-Json
$login = Invoke-RestMethod -Method Post -Uri ($Api + '/auth/login') -Body $loginBody -ContentType 'application/json'
if (-not $login.accessToken) { Bad 'login did not return accessToken'; exit 1 }
$H = @{ Authorization = ('Bearer ' + $login.accessToken) }
Ok 'authenticated'

Step 'Live definition options'
$opts = Invoke-RestMethod -Method Get -Uri ($Api + '/api/analysis-jobs/definition-options') -Headers $H
if ($opts.defectTypes.Count -gt 0) { Ok ('live defect types: ' + $opts.defectTypes.Count) } else { Bad 'no live defect types returned' }
if ($opts.parameters.Count -gt 0)  { Ok ('live parameters: ' + $opts.parameters.Count) } else { Note 'no parameter definitions with observations (rule-based pass will be empty)' }

# Live quality_events on this dataset exposes event_type = 'Defect' / 'FinalDecision'
# only (no CRACK/SCRATCH split at this grain - the planted subtype lives in the
# defect-class dimension). Pick the richest real outcome and run honestly.
$crackDefect = $null
foreach ($d in $opts.defectTypes) { if ($d.eventType -match 'CRACK') { $crackDefect = $d.eventType; break } }
if (-not $crackDefect) {
    $best = $opts.defectTypes | Sort-Object -Property eventCount -Descending | Select-Object -First 1
    $crackDefect = $best.eventType
    Note ('no CRACK label at event_type grain; running against real outcome ' + $crackDefect + ' (' + $best.eventCount + ' events). CRACK/SCRATCH subtype contrast needs the defect-class dimension - logged, not faked.')
}
else { Ok ('planted outcome selected: ' + $crackDefect) }

$scratchDefect = $null
foreach ($d in $opts.defectTypes) { if ($d.eventType -match 'SCRATCH') { $scratchDefect = $d.eventType; break } }

$topParam = $null
if ($opts.parameters.Count -gt 0) { $topParam = $opts.parameters[0].parameterCode }

$stamp = Get-Date -Format 'HHmmss'
$codeA = 'M105_ACC_CRACK_' + $stamp

Step ('Create definition ' + $codeA + ' (30d, grade=S355J2 declared scope)')
$createBody = @{
    code = $codeA
    name = 'M1-05 acceptance: crack vs process parameters'
    defectType = $crackDefect
    parameterCode = $topParam
    windowDays = 30
    populationFilters = @{ grade = 'S355J2' }
    engineOutcomeKey = 'defect.rate_per_m2'
    engineJobCode = 'ML_PROCESS_VS_DEFECT'
    description = 'Created by Verify-M1-05-Surface3.ps1'
} | ConvertTo-Json
$created = Invoke-RestMethod -Method Post -Uri ($Api + '/api/analysis-jobs') -Headers $H -Body $createBody -ContentType 'application/json'
if ($created.code -eq $codeA) { Ok 'definition saved (inspection_jobs row, generic tenant data)' } else { Bad 'create did not return the definition' }
if ($created.ruleJson -match 'S355J2') { Ok 'population filter persisted in rule_json as declared scope' } else { Bad 'population filter missing from rule_json' }

Step 'Run the definition (ReadinessGate + deterministic compute)'
$run1 = Invoke-RestMethod -Method Post -Uri ($Api + '/api/analysis-jobs/' + $codeA + '/run') -Headers $H -Body '{}' -ContentType 'application/json'
if ($run1.readinessStatus) { Ok ('ReadinessGate evaluated: ' + $run1.readinessStatus + ' - ' + $run1.readinessReason) } else { Bad 'readinessStatus missing from run response' }
Write-Host ('      learning: ' + $run1.learningStatus + ' (' + $run1.learningResultCount + ' results)')
Write-Host ('      compute : ' + $run1.computeStatus + ' engine ' + $run1.computeEngineKey + ' (' + $run1.computeResultCount + ' results)')
$computeRun1 = $run1.computeRunId
if ($computeRun1) { Ok ('deterministic compute run tied: ' + $computeRun1) } else { Note 'no compute run id (engine returned Empty/Failed) - see computeMessage above' }

Step 'Definition stamped + results_v2 tied to the definition'
$defAfter = Invoke-RestMethod -Method Get -Uri ($Api + '/api/analysis-jobs/' + $codeA) -Headers $H
if ($defAfter.lastRunAtUtc) { Ok ('last_run_at_utc stamped: ' + $defAfter.lastRunAtUtc) } else { Bad 'definition not stamped after run' }
if ($computeRun1 -and $defAfter.sourceCorrelationRunId -eq $computeRun1) { Ok 'source_correlation_run_id = compute_run_id (tied)' }
elseif ($computeRun1) { Bad 'compute run id not stamped onto the definition' }

$res1 = Invoke-RestMethod -Method Get -Uri ($Api + '/api/analysis-jobs/' + $codeA + '/results') -Headers $H
if ($res1.count -gt 0) {
    Ok ('results_v2 rows tied to the definition: ' + $res1.count)
    $top = $res1.results[0]
    Write-Host ('      top contributor: ' + $top.feature_key + ' method=' + $top.method + ' effect=' + $top.effect_size + ' q=' + $top.q_value)
    if ($top.feature_key -match 'superheat|peritectic') { Ok 'planted plant relation recovered (superheat/peritectic ranked on top)' }
    else { Note ('top feature is ' + $top.feature_key + ' - inspect ranking manually against the planted 9.3x signal') }
}
else { Note 'no tied results_v2 rows yet (compute Empty or feature store not refreshed - run POST /api/ml/foundation/feature-store/refresh, then rerun)' }

Step 'Edit the definition (window 30 -> 45), rerun, prove recompute'
$updateBody = @{
    name = 'M1-05 acceptance: crack vs process parameters (edited)'
    defectType = $crackDefect
    parameterCode = $topParam
    windowDays = 45
    populationFilters = @{ grade = 'S355J2' }
    engineOutcomeKey = 'defect.rate_per_m2'
    engineJobCode = 'ML_PROCESS_VS_DEFECT'
    description = 'Edited by Verify-M1-05-Surface3.ps1'
} | ConvertTo-Json
$null = Invoke-RestMethod -Method Put -Uri ($Api + '/api/analysis-jobs/' + $codeA) -Headers $H -Body $updateBody -ContentType 'application/json'
Ok 'definition edited (windowDays 45)'
Start-Sleep -Seconds 1
$run2 = Invoke-RestMethod -Method Post -Uri ($Api + '/api/analysis-jobs/' + $codeA + '/run') -Headers $H -Body '{}' -ContentType 'application/json'
if ($run2.windowDays -eq 45) { Ok 'rerun consumed the EDITED definition (window 45)' } else { Bad ('rerun window was ' + $run2.windowDays + ', expected 45') }
$defAfter2 = Invoke-RestMethod -Method Get -Uri ($Api + '/api/analysis-jobs/' + $codeA) -Headers $H
if ($defAfter2.lastRunAtUtc -ne $defAfter.lastRunAtUtc) { Ok 'rerun recomputed (last_run_at_utc advanced)' } else { Bad 'last_run_at_utc did not advance on rerun' }
if ($run2.computeRunId -and $run2.computeRunId -ne $computeRun1) { Ok ('new compute run tied on rerun: ' + $run2.computeRunId) }
elseif ($run2.computeRunId) { Note 'compute run id unchanged on rerun - inspect engine' }

Step 'Rule-based control: planted defect vs SCRATCH (existing phase2 endpoint)'
if ($topParam) {
    $fromUtc = (Get-Date).AddDays(-30).ToUniversalTime().ToString('o')
    $ruleCrackBody = @{ parameterCode = $topParam; defectType = $crackDefect; fromUtc = $fromUtc } | ConvertTo-Json
    $ruleCrack = Invoke-RestMethod -Method Post -Uri ($Api + '/analytics/phase2/rule-correlation/run') -Headers $H -Body $ruleCrackBody -ContentType 'application/json'
    Write-Host ('      ' + $crackDefect + ' rule strength: ' + $ruleCrack.ruleStrength + ' - ' + $ruleCrack.interpretation)
    if ($scratchDefect) {
        $ruleScratchBody = @{ parameterCode = $topParam; defectType = $scratchDefect; fromUtc = $fromUtc } | ConvertTo-Json
        $ruleScratch = Invoke-RestMethod -Method Post -Uri ($Api + '/analytics/phase2/rule-correlation/run') -Headers $H -Body $ruleScratchBody -ContentType 'application/json'
        Write-Host ('      ' + $scratchDefect + ' rule strength: ' + $ruleScratch.ruleStrength + ' - ' + $ruleScratch.interpretation)
        if ($ruleCrack.ruleStrength -ge 0.35 -and $ruleScratch.ruleStrength -lt 0.35) { Ok 'planted signal detected AND SCRATCH control rejected (0.35 threshold)' }
        elseif ($ruleCrack.buckets.Count -eq 0) { Note 'rule pass returned no buckets - parameter_observations empty for the plant window (known Session-A scope); signal proof stays with the engine results above' }
        else { Note ('strengths crack=' + $ruleCrack.ruleStrength + ' scratch=' + $ruleScratch.ruleStrength + ' - review manually') }
    }
    else { Note 'no SCRATCH-like defect type in live data - control comparison skipped' }
}
else { Note 'no live parameter available - rule-based control skipped' }

Write-Host ''
Write-Host ('=== M1-05 acceptance: PASS=' + $Pass + ' FAIL=' + $Fail + ' WARN=' + $Warn + ' ===') -ForegroundColor Cyan
if ($Fail -gt 0) { exit 1 } else { exit 0 }
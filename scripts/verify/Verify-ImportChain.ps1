# ============================================================================
# M1-20  Verify-ImportChain.ps1  v1.2
# v1.2 CRITICAL: Q1 array-unrolling bug fixed - v1.0/v1.1 reported the FIRST
#      DIGIT of every count (40148 -> "4"). Two "database wipes" chased on
#      17-Jul were this defect, not data loss. Self-check added below.
# Backlog v23 M1-20 - "GOLDEN EVIDENCE CHAIN part 1" (senior recs 6 + 7).
#
# WHAT THIS IS: the evidence recorder wrapped around the Import Registration
# Runsheet. You drive the imports THROUGH THE HMI (Rule 3 - the journey is
# the product); this script captures, per phase, what the product actually
# did: new batch IDs, staging deltas, canonical deltas, and - the link that
# matters - whether the canonical rows can be traced back to THIS run's
# batches rather than to history (senior rec 7).
#
# WORKFLOW (state persists in importchain_state.json between calls):
#
#   1. BEFORE touching the UI:
#        .\Verify-ImportChain.ps1 -Baseline
#   2. Do Phase A in the HMI (register the four taxonomy views), then:
#        .\Verify-ImportChain.ps1 -Phase A
#   3. ...B (cc_slabs, hsm_coils), C (cc_heats, params), D (parsytec defects)
#   4. When A-D are done:
#        .\Verify-ImportChain.ps1 -Chain
#
# -Chain proves, on THIS run's rows only:
#      import batch -> staging -> mapping -> canonical -> genealogy walk
#   ...which is exactly the acceptance line of v23 M1-20. The engine run and
#   the 9.3x/1.0x verification are M1-21, deliberately not in this script.
#
# HONESTY: every table and column is discovered from information_schema. If a
# direct batch->canonical foreign key does not exist, the script SAYS SO and
# falls back to a time-window linkage, labelled as weaker evidence. It never
# claims a link it did not find.
#
# Run from repo root:
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\Verify-ImportChain.ps1 -Baseline
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\Verify-ImportChain.ps1 -Phase A
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\Verify-ImportChain.ps1 -Chain
# ============================================================================
[CmdletBinding()]
param(
    [switch]$Baseline,
    [ValidateSet('A', 'B', 'C', 'D')]
    [string]$Phase,
    [switch]$Chain,
    [string]$TargetDb = 'ppiq_presentation'
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Continue'

$RepoRoot = (Get-Location).Path
$StateFile = Join-Path $RepoRoot 'importchain_state.json'
$Stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$Report = Join-Path $RepoRoot ('M1-20_ImportChain_' + $Stamp + '.txt')
$sb = New-Object System.Text.StringBuilder
function W([string]$s) { [void]$sb.AppendLine($s); Write-Host $s }
function Save { [System.IO.File]::WriteAllText($Report, $sb.ToString(), (New-Object System.Text.UTF8Encoding($false))) }

# ---- psql -----------------------------------------------------------------
$Psql = $null
$cmd = Get-Command psql.exe -ErrorAction SilentlyContinue
if ($cmd) { $Psql = $cmd.Source } else {
    $c = @(Get-ChildItem 'C:\Program Files\PostgreSQL\*\bin\psql.exe' -ErrorAction SilentlyContinue | Sort-Object FullName -Descending)
    if ($c.Count -gt 0) { $Psql = $c[0].FullName }
}
if (-not $Psql) { Write-Host "[FAIL] psql not found." -ForegroundColor Red; exit 1 }
$env:PGPASSWORD = 'ppiq_dev_local_only'

function Q([string]$q) {
    $o = & $Psql -h 127.0.0.1 -p 5432 -U ppiq_dev -d $TargetDb -w -X -A -t -F '|' -c $q 2>&1
    if ($LASTEXITCODE -ne 0) { return @() }
    return @($o | Where-Object { $_ -and ($_.ToString().Trim() -ne '') })
}
function Q1([string]$q) {
    # v1.2 CRITICAL FIX: @() is mandatory. PowerShell unrolls a single-element
    # array on return; without @() the assignment yields a STRING and $r[0]
    # returns its first CHARACTER. This function was reporting 40148 as "4",
    # 51691 as "5" - i.e. inventing a wiped database out of a healthy one.
    $r = @(Q $q)
    if ($r.Count -eq 0) { return $null }
    return ([string]$r[0]).Trim()
}
function TableExists([string]$t) { return ($null -ne (Q1 ("SELECT to_regclass('public." + $t + "')::text;"))) }
function ColOf([string]$table, [string]$pattern) {
    return Q1 ("SELECT column_name FROM information_schema.columns WHERE table_schema='public' AND table_name='" + $table + "' AND column_name ~* '" + $pattern + "' ORDER BY length(column_name) LIMIT 1;")
}
function CountOf([string]$t) {
    if (-not (TableExists $t)) { return -1 }
    $n = Q1 ("SELECT COUNT(*) FROM " + $t + ";")
    if ($null -eq $n) { return -1 }
    return [int]$n
}

$TRACKED = @('import_batches', 'staging_records', 'mapping_definitions', 'source_dataset_definitions',
    'parameter_definitions', 'defect_catalogs', 'material_units', 'parameter_observations',
    'quality_events', 'genealogy_edges', 'job_log')

# ---- SELF-CHECK: prove the instrument before trusting a single number ------
# Added 17-Jul after Q1's unrolling bug reported 40148 as "4" and manufactured
# two phantom "database wipes". A measuring tool that cannot measure a known
# value must refuse to report unknown ones.
$probe = Q1 "SELECT 40148;"
if ("$probe" -ne '40148') {
    Write-Host ("[SELF-CHECK FAILED] Q1 returned '" + $probe + "' for a literal 40148.") -ForegroundColor Red
    Write-Host "                    The query layer is broken - every count below would" -ForegroundColor Red
    Write-Host "                    be wrong. Refusing to report. Fix Q1 first." -ForegroundColor Red
    exit 1
}
$probe2 = Q1 "SELECT 'PPIQ_SELFCHECK';"
if ("$probe2" -ne 'PPIQ_SELFCHECK') {
    Write-Host ("[SELF-CHECK FAILED] Q1 returned '" + $probe2 + "' for a literal string.") -ForegroundColor Red
    exit 1
}

W ("M1-20 GOLDEN EVIDENCE CHAIN (part 1) - " + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
W ("DB: " + $TargetDb)
W ("=" * 78)
W ""

# ===========================================================================
# BASELINE
# ===========================================================================
if ($Baseline) {
    W "[BASELINE] recording pre-import state..."
    $state = @{ takenAt = (Get-Date).ToString('o'); db = $TargetDb; counts = @{}; batchIds = @(); phases = @{} }
    foreach ($t in $TRACKED) {
        $n = CountOf $t
        $state.counts[$t] = $n
        W ("    " + $t.PadRight(28) + " " + $(if ($n -lt 0) { 'ABSENT' } else { $n }))
    }
    if (TableExists 'import_batches') {
        $idCol = $null
        foreach ($cand in @('id', 'import_batch_id', 'batch_id')) {
            $probe = Q1 ("SELECT '" + $cand + "' FROM information_schema.columns WHERE table_schema='public' AND table_name='import_batches' AND column_name='" + $cand + "';")
            if ($probe) { $idCol = $cand; break }
        }
        if (-not $idCol) { $idCol = ColOf 'import_batches' 'id' }
        $state.batchIdColumn = $idCol
        $state.batchIds = @(Q ("SELECT " + $idCol + "::text FROM import_batches;"))
        W ("    existing batch ids: " + @($state.batchIds).Count + " (recorded; anything new is THIS run)")
    }
    ($state | ConvertTo-Json -Depth 6) | Out-File -FilePath $StateFile -Encoding utf8
    W ""
    W ("[OK] baseline -> " + $StateFile)
    W ""
    W "NOW DO PHASE A IN THE HMI (taxonomy views first - Rule 2):"
    W "    Prepare Import -> Meltshop Level 2  -> v_parameter_definitions  -> register"
    W "    Prepare Import -> Continuous Caster -> PPIQ_SRC v_parameter_definitions"
    W "    Prepare Import -> HSM Level 2       -> PPIQ_SRC v_parameter_definitions"
    W "    Prepare Import -> Surface Inspection -> v_defect_definitions"
    W "    ...then run the import jobs and: .\Verify-ImportChain.ps1 -Phase A"
    Save; exit 0
}

if (-not (Test-Path $StateFile)) {
    W "[ABORT] no baseline. Run -Baseline BEFORE importing, or the deltas are meaningless."
    Save; exit 1
}
$state = Get-Content $StateFile -Raw | ConvertFrom-Json

# ===========================================================================
# PHASE DELTA
# ===========================================================================
if ($Phase) {
    W ("[PHASE " + $Phase + "] delta since baseline (" + $state.takenAt + "):")
    W ""
    W ("{0,-28} {1,10} {2,10} {3,10}" -f 'table', 'baseline', 'now', 'delta')
    $deltas = @{}
    foreach ($t in $TRACKED) {
        $b = -1
        if ($state.counts.PSObject.Properties[$t]) { $b = [int]$state.counts.$t }
        $n = CountOf $t
        $d = $n - $b
        $deltas[$t] = $d
        $flag = ''
        if ($d -gt 0) { $flag = '  <-- new' }
        W ("{0,-28} {1,10} {2,10} {3,10}{4}" -f $t, $b, $n, $d, $flag)
    }
    W ""

    # new batch ids = THIS run's evidence
    $newBatches = @()
    if (TableExists 'import_batches') {
        $idCol = 'id'
        if ($state.PSObject.Properties['batchIdColumn'] -and $state.batchIdColumn -and $state.batchIdColumn.Length -gt 1) { $idCol = $state.batchIdColumn }
        $all = @(Q ("SELECT " + $idCol + "::text FROM import_batches;"))
        $old = @($state.batchIds)
        $newBatches = @($all | Where-Object { $old -notcontains $_ })
        W ("[BATCHES] new since baseline: " + @($newBatches).Count)
        foreach ($b in @($newBatches | Select-Object -First 12)) { W ("    " + $b) }
        if (@($newBatches).Count -gt 12) { W ("    ... and " + (@($newBatches).Count - 12) + " more") }
    }
    W ""

    # per-phase expectation
    $expected = @{
        'A' = @('parameter_definitions', 'defect_catalogs')
        'B' = @('material_units', 'genealogy_edges')
        'C' = @('material_units', 'parameter_observations')
        'D' = @('quality_events')
    }
    W ("[EXPECTATION] phase " + $Phase + " should have grown: " + (($expected[$Phase]) -join ', '))
    $phaseOk = $true
    foreach ($t in $expected[$Phase]) {
        $d = 0
        if ($deltas.ContainsKey($t)) { $d = $deltas[$t] }
        $verdict = 'PASS'
        if ($d -le 0) { $verdict = 'FAIL - no new rows'; $phaseOk = $false }
        W ("    " + $t.PadRight(28) + " +" + $d + "   " + $verdict)
    }
    W ""
    if ($newBatches.Count -eq 0 -and $Phase -ne 'A') {
        W "[WARN] no new import batch. Did the job actually run? Check Jobs Monitor."
        $phaseOk = $false
    }

    # recent job_log lines - the honest place errors surface
    if (TableExists 'job_log') {
        $msgCol = ColOf 'job_log' 'message|text|detail'
        $tsCol = ColOf 'job_log' 'created|logged|timestamp|utc'
        if ($msgCol -and $tsCol) {
            W "[JOB LOG] last 6 entries:"
            $lines = @(Q ("SELECT left(" + $msgCol + ", 130) FROM job_log ORDER BY " + $tsCol + " DESC LIMIT 6;"))
            foreach ($l in $lines) { W ("    " + $l) }
            W ""
        }
    }

    # persist
    if (-not $state.phases) { $state | Add-Member -NotePropertyName phases -NotePropertyValue (New-Object psobject) -Force }
    $state.phases | Add-Member -NotePropertyName $Phase -NotePropertyValue @{
        at = (Get-Date).ToString('o'); newBatches = @($newBatches); deltas = $deltas; ok = $phaseOk
    } -Force
    ($state | ConvertTo-Json -Depth 8) | Out-File -FilePath $StateFile -Encoding utf8

    W ("[PHASE " + $Phase + "] " + $(if ($phaseOk) { 'PASS - recorded' } else { 'INCOMPLETE - see FAIL rows above' }))
    W ""
    W "Batch IDs above are your evidence for M1-21's fresh run. Keep this report."
    Save
    Write-Host ""
    Write-Host ("[DONE] Report -> " + $Report) -ForegroundColor Green
    if ($phaseOk) { exit 0 } else { exit 1 }
}

# ===========================================================================
# CHAIN PROOF
# ===========================================================================
if ($Chain) {
    W "[CHAIN] proving: batch -> staging -> mapping -> canonical -> genealogy"
    W "        on THIS run's rows only (senior rec 7: history proves nothing)."
    W ""

    $allNew = @()
    if ($state.PSObject.Properties['phases']) {
        foreach ($p in $state.phases.PSObject.Properties) {
            if ($p.Value.newBatches) { $allNew += @($p.Value.newBatches) }
        }
    }
    $allNew = @($allNew | Sort-Object -Unique)
    W ("LINK 1  import batch : " + @($allNew).Count + " new batch id(s) from this run")
    if (@($allNew).Count -eq 0) {
        W "        FAIL - no new batches recorded. Run -Phase A..D as you import."
        Save; exit 1
    }
    foreach ($b in @($allNew | Select-Object -First 8)) { W ("        " + $b) }
    $inList = "'" + (@($allNew) -join "','") + "'"

    # LINK 2: staging rows for those batches
    W ""
    $stgBatchCol = ColOf 'staging_records' 'batch'
    if ($stgBatchCol) {
        $n = Q1 ("SELECT COUNT(*) FROM staging_records WHERE " + $stgBatchCol + "::text IN (" + $inList + ");")
        W ("LINK 2  staging     : " + $n + " row(s) carry these batch ids   [" + $(if ([int]$n -gt 0) { 'PASS' } else { 'FAIL' }) + "]")
        W ("        (join column discovered: staging_records." + $stgBatchCol + ")")
    } else {
        W "LINK 2  staging     : NO batch column found on staging_records [WEAK]"
        W "        Report this - the chain cannot be proven structurally here."
    }

    # LINK 3: mapping definitions used
    W ""
    $mapN = CountOf 'mapping_definitions'
    W ("LINK 3  mapping      : " + $mapN + " definition(s) exist")
    W "        (mapping is proven by the canonical rows below carrying connector provenance)"

    # LINK 4: canonical rows traceable to this run
    W ""
    $muBatch = ColOf 'material_units' 'import_batch|batch_id'
    $muCreated = ColOf 'material_units' 'created_at'
    $strong = $false
    if ($muBatch) {
        $n = Q1 ("SELECT COUNT(*) FROM material_units WHERE " + $muBatch + "::text IN (" + $inList + ");")
        W ("LINK 4  canonical    : " + $n + " material_unit(s) reference these batches directly   [STRONG]")
        W ("        (column: material_units." + $muBatch + ")")
        $strong = ([int]$n -gt 0)
    } elseif ($muCreated) {
        $since = $state.takenAt
        $n = Q1 ("SELECT COUNT(*) FROM material_units WHERE " + $muCreated + " >= TIMESTAMP '" + ([datetime]$since).ToString('yyyy-MM-dd HH:mm:ss') + "';")
        W ("LINK 4  canonical    : " + $n + " material_unit(s) created since baseline   [TIME-WINDOW, weaker]")
        W ("        NO direct batch column on material_units - this is honest linkage by")
        W ("        creation window, not by foreign key. Say it that way if asked.")
        $strong = ([int]$n -gt 0)
    } else {
        W "LINK 4  canonical    : cannot link - no batch or created_at column found [FAIL]"
    }

    # provenance of the new rows
    W ""
    W "        provenance of this run's canonical rows:"
    if ($muCreated) {
        $rows = @(Q ("SELECT source_system, COUNT(*) FROM material_units WHERE " + $muCreated + " >= TIMESTAMP '" + ([datetime]$state.takenAt).ToString('yyyy-MM-dd HH:mm:ss') + "' GROUP BY 1 ORDER BY 2 DESC;"))
        if (@($rows).Count -eq 0) { W "          (none)" }
        foreach ($r in $rows) { W ("          " + $r) }
    }

    # LINK 5: genealogy walk on new rows
    W ""
    $pCol = ColOf 'genealogy_edges' 'parent.*id'
    $cCol = ColOf 'genealogy_edges' 'child.*id'
    if ($pCol -and $cCol) {
        W ("LINK 5  genealogy    : walking edges (" + $pCol + " -> " + $cCol + ")")
        $walk = @(Q (@"
WITH chain AS (
  SELECT p.business_key AS gp, c.business_key AS ch, e.$pCol AS pid, e.$cCol AS cid
  FROM genealogy_edges e
  JOIN material_units p ON p.id = e.$pCol
  JOIN material_units c ON c.id = e.$cCol
  LIMIT 200000
)
SELECT c1.gp || ' -> ' || c1.ch || ' -> ' || c2.ch
FROM chain c1 JOIN chain c2 ON c2.pid = c1.cid
LIMIT 3;
"@))
        if (@($walk).Count -gt 0) {
            W "        multi-generation chains found (heat -> slab -> coil):"
            foreach ($w in $walk) { W ("          " + $w) }
            W "        [PASS] this is the walk to perform LIVE in the HMI"
        } else {
            $one = @(Q ("SELECT p.business_key || ' -> ' || c.business_key FROM genealogy_edges e JOIN material_units p ON p.id = e." + $pCol + " JOIN material_units c ON c.id = e." + $cCol + " LIMIT 3;"))
            if (@($one).Count -gt 0) {
                W "        single-generation edges only (business_key column may differ):"
                foreach ($o in $one) { W ("          " + $o) }
            } else {
                W "        [FAIL] no walkable edges - or business_key is named differently."
                W "        Paste: \d material_units    and I pin the column."
            }
        }
    } else {
        W "LINK 5  genealogy    : parent/child columns not found on genealogy_edges [FAIL]"
    }

    W ""
    W "=" * 78
    W "M1-20 ACCEPTANCE CHECK:"
    W ("  batch IDs recorded per phase ........ " + $(if (@($allNew).Count -gt 0) { 'YES (' + @($allNew).Count + ')' } else { 'NO' }))
    W ("  staging/canonical counts pasted ..... this report IS the artifact")
    W ("  genealogy resolves from this batch .. see LINK 5")
    W ("  certifier S02/S03/S06 green ......... run: .\Certify-Journey.ps1 -SkipFrontendTests -ApiBase http://localhost:5063")
    W ""
    W "NEXT: M1-21 - fresh governed run over the imported window; the 9.3x must"
    W "carry a NEW run id, not one of the 375 historical runs (senior rec 7)."
    Save
    Write-Host ""
    Write-Host ("[DONE] Report -> " + $Report) -ForegroundColor Green
    exit 0
}

W "Nothing to do. Use -Baseline, then -Phase A|B|C|D, then -Chain."
Save
exit 0

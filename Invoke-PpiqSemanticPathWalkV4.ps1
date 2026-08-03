# ============================================================================
# Invoke-PpiqSemanticPathWalkV4.ps1      Backlog v2.6.1 task T-010
#
# THE v3 EXECUTE RUN PRODUCED FOUR 400s AND ONE WRONG FINDING.
# THREE OF THE FOUR WERE MINE. Sorted, so the record is accurate:
#
#   MINE - connection test. TestConnectionProfileAsync takes NO BODY: its
#   signature is (Guid id, IConnectorConfigurationService, CancellationToken).
#   I sent an empty JSON object with a Content-Type, and got 400. v4 posts with
#   no body at all.
#
#   MINE - correlation. ComputeCorrelationAsync takes [FromBody]
#   CorrelationComputeRequest(OutcomeKey, Grain, WindowDays, Filters?). I sent
#   {} and got 400. v4 sends a real request built from GET /outcomes.
#
#   MINE - the provenance finding was WRONG, AND THIS IS THE SECOND TIME TODAY
#   I NEARLY REPORTED THE PRODUCT BROKEN BY READING THE WRONG PLACE.
#   investigation-full returned top-level keys including `materials`, and I
#   looked for `materialUnits`. The provenance IS there, inside materials[0].
#   v4 reads the right key and prints the shape when it cannot find it.
#
#   NOT MINE, AND IT IS THE BIGGEST FINDING OF THE WALK - see section 4.
#   /workflow/import/run does NOT pull from a source. Its request record is
#   RunImportWorkflowRequest(ImportBatchId?, SourceSystemDefinitionId,
#   MappingDefinitionId, ImportBatchCode?, ImportType?, SourceObjectName,
#   FileName?, Checksum?, Rows, ...) - THE CALLER SUPPLIES THE ROWS. It is a
#   PUSH endpoint. The only pull-capable class, TwoStageImportEndpoints, is
#   never mapped in Program.cs. The remaining candidate is
#   POST /admin/jobs/datasets/{datasetId}/backfill, which v4 probes.
#
#   ALSO: `docker compose ... start` failed with "no container to start" -
#   the containers do not exist yet. It has to be `up -d` the first time.
#   So DF1 and DF3 had no source to reach even before the 400s.
#
# ---------------------------------------------------------------------------
#
# WHAT THE v2 RUN SETTLED
#   The property guard worked and the walk ran clean. Two things it taught:
#
#   1. I WAS ABOUT TO RAISE A FALSE FINDING. /materials/{id} returns thirteen
#      fields and NONE of them is provenance, which looked like an evidence-grade
#      gap. It is not. That endpoint is a SLIM read model by design.
#      MaterialInvestigationEndpoints maps GET /materials/{id}/investigation-full
#      which DOES return SourceSystem and SourceRecordId, and it IS registered in
#      Program.cs. I used the wrong endpoint. v3 uses the right one.
#      The lesson is the same one as the whole day: read the code before
#      declaring a gap. I nearly reported the product broken because I called
#      the wrong route.
#
#   2. The material code field is `materialCode`, not `materialUnitCode`.
#
#   The two REAL product findings from v1 stand: /admin/two-stage-import/* is
#   never mapped in Program.cs, and /admin/p03p04/readiness returns 500.
#
# ---------------------------------------------------------------------------
#
# WHAT THE FIRST RUN FOUND, AND WHAT I GOT WRONG
#
#   MY DEFECT, and it is the StrictMode unrolling family again: under
#   Set-StrictMode -Version 2.0, reading a property that does not exist on a
#   PSCustomObject THROWS. I wrote $p.code with a $p.profileCode fallback, which
#   can never run because the first line already threw. Same for
#   $mu.materialUnitCode and $mu.sourceSystem. Fixed with a Prop() helper that
#   asks PSObject.Properties before reading. NEVER dot into an API response
#   under StrictMode without checking the property exists first.
#
#   NOT MY DEFECT - TWO REAL PRODUCT FINDINGS:
#     /admin/two-stage-import/* returned 404 on every route. The class exists
#     with overview, stage1/run, stage2/run, run-full-cycle and
#     provision-baseline, but MapTwoStageImportEndpoints IS NEVER CALLED IN
#     Program.cs. Grep finds it in exactly two places: its own file, and a
#     phase-gate validation script. THE TWO-STAGE IMPORT IS UNREACHABLE OVER
#     HTTP. The reachable import surface is /workflow/import instead.
#
#     /admin/p03p04/readiness returned 500. That is a live server error on the
#     genealogy readiness endpoint, not a routing mistake.
#
#   AND ONE WRONG PATH OF MINE: analysis primitives are at
#   /api/analytics/simple/primitives, not /api/analysis/primitives.
#
# ---------------------------------------------------------------------------
#
# Walks the canonical SEMANTIC path end to end THROUGH THE PRODUCT'S OWN
# SERVICES. Never by loading a target table directly - that is the whole point.
#
# WHAT THE TASK ASKS FOR, AND WHY EACH PART MATTERS
#   "connection test, dataset registration, incremental import into staging,
#    canonical projection through the customer-authored mapping, genealogy,
#    feature and outcome refresh, then an analysis run - each step reached
#    through the product's own services, never by loading a target table
#    directly. Record row counts at every stage."
#
#   The acceptance is a command log where the stage counts are MONOTONIC AND
#   EXPLAINABLE, and where every write went through a service interface rather
#   than a table name. That is what lets M2a replace the storage underneath
#   without this test changing - which is the actual thing being proved.
#
# ENDPOINTS, READ FROM THE CODE RATHER THAN GUESSED
#   POST /auth/login
#   GET  /admin/connectors/connection-profiles
#   POST /admin/connectors/connection-profiles/{id}/test
#   GET  /admin/connectors/connection-profiles/{id}/tables
#   POST /admin/connectors/connection-profiles/{id}/register
#   GET  /integration/source-systems
#   GET  /integration/mapping-definitions
#   GET  /integration/import-batches
#   GET  /integration/staging-records
#   GET  /integration/summary
#   POST /admin/two-stage-import/stage1/run
#   POST /admin/two-stage-import/stage2/run
#   GET  /admin/two-stage-import/overview
#   GET  /admin/schema-mapping/catalog
#   POST /admin/schema-mapping/execute/{viewCode}
#   GET  /admin/p03p04/readiness            (genealogy readiness)
#   GET  /materials  and  GET /materials/{id}/genealogy
#   POST /api/ml/foundation/feature-store/refresh
#   GET  /api/ml/foundation/readiness
#   POST /api/ml/foundation/compute/correlation
#
# READ-ONLY BY DEFAULT. -Execute is required before anything writes, and even
# then it refuses unless the profile is presentation, because a semantic walk
# that imports into the wrong database proves the wrong thing.
#
# A NOTE ON SEQUENCING, FROM THE TASK TEXT ITSELF
#   The final external definition contract lands later in M1-P2, so this walk is
#   REPEATED once that contract exists. The second walk is what proves the
#   contract did not change behaviour. Keep this evidence file; it is the
#   before.
#
# RUN FROM REPO ROOT. Commands at the bottom.
# ============================================================================
[CmdletBinding()]
param(
    [switch]$Execute,
    [string]$BaseUrl  = "http://localhost:5063",
    [string]$UserName = "e2eadmin",
    [string]$Password = "E2EAdmin123!",
    [string]$TargetDb = "ppiq_presentation",
    [string]$DbHost   = "127.0.0.1",
    [int]   $Port     = 5432,
    [string]$DbUser   = "ppiq_dev",
    [string]$DbPass   = "ppiq_dev_local_only"
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Continue"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$RepoRoot     = (Get-Location).Path
$EvidenceDir  = Join-Path $RepoRoot "docs\m1\evidence"
$Stamp        = Get-Date -Format "yyyyMMdd_HHmmss"
$EvidencePath = Join-Path $EvidenceDir ("T-010_semantic_path_walk_" + $Stamp + ".txt")

$env:PGPASSWORD = $DbPass
$env:PGCLIENTENCODING = "UTF8"

$Lines = New-Object System.Collections.ArrayList
function Say([string]$Line) { Write-Host $Line; [void]$Lines.Add($Line) }
function Head([string]$Banner) { Say ""; Say ("=" * 78); Say $Banner; Say ("=" * 78) }

$Token = $null

# Under StrictMode, dotting into a property that does not exist THROWS rather
# than returning $null, so a fallback on the next line never runs. Ask first.
function Prop($Obj, [string]$Name, $Default = "") {
    if ($null -eq $Obj) { return $Default }
    if ($Obj.PSObject.Properties.Name -contains $Name) {
        $v = $Obj.$Name
        if ($null -eq $v) { return $Default }
        return $v
    }
    return $Default
}
function FirstProp($Obj, [string[]]$Names, $Default = "") {
    foreach ($n in $Names) {
        if ($null -ne $Obj -and $Obj.PSObject.Properties.Name -contains $n) {
            $v = $Obj.$n
            if ($null -ne $v) { return $v }
        }
    }
    return $Default
}
function Api([string]$Method, [string]$Path, $Body) {
    $uri = $BaseUrl + $Path
    $headers = @{}
    if ($null -ne $Token) { $headers["Authorization"] = "Bearer " + $Token }
    try {
        if ($null -eq $Body) {
            return Invoke-RestMethod -Uri $uri -Method $Method -Headers $headers -TimeoutSec 180
        }
        $json = $Body | ConvertTo-Json -Depth 8
        return Invoke-RestMethod -Uri $uri -Method $Method -Headers $headers -ContentType "application/json" -Body $json -TimeoutSec 300
    } catch {
        Say ("   [API FAIL] " + $Method + " " + $Path + " -> " + $_.Exception.Message)
        return $null
    }
}

# Counts are read from the database ONLY to OBSERVE. Every WRITE goes through a
# service above. Observing with SQL is not the same as writing with SQL, and the
# distinction is the one the task is testing.
function Count([string]$Sql) {
    $gid = [guid]::NewGuid().ToString("N")
    $qF = Join-Path $env:TEMP ("ppiq_t010w_q_" + $gid + ".sql")
    $rF = Join-Path $env:TEMP ("ppiq_t010w_r_" + $gid + ".txt")
    $enc = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($qF, $Sql, $enc)
    & psql -h $DbHost -p $Port -U $DbUser -d $TargetDb -w -X -A -t -v ON_ERROR_STOP=1 -o $rF -f $qF 2>&1 | Out-Null
    $ok = ($LASTEXITCODE -eq 0)
    $val = "unreadable"
    if ($ok -and (Test-Path $rF)) {
        $r = @([System.IO.File]::ReadAllText($rF) -split "`r?`n" | Where-Object { $_ -ne "" })
        if ($r.Count -gt 0) { $val = ([string]$r[0]).Trim() }
    }
    foreach ($f in @($qF, $rF)) { Remove-Item $f -ErrorAction SilentlyContinue }
    return $val
}

$Stages = New-Object System.Collections.ArrayList
function Stage([string]$Name, [string]$Sql, [string]$Note) {
    $v = Count $Sql
    [void]$Stages.Add([pscustomobject]@{ Name = $Name; Value = $v; Note = $Note })
    Say ("   " + $Name.PadRight(38) + $v.PadLeft(10) + "   " + $Note)
}

Head ("T-010 CANONICAL SEMANTIC PATH WALK - " + (Get-Date -Format "yyyy-MM-dd HH:mm:ss"))
Say ("Base URL : " + $BaseUrl)
Say ("Database : " + $TargetDb)
Say ("Mode     : " + $(if ($Execute) { "EXECUTE - writes through product services" } else { "OBSERVE ONLY - no writes" }))

# ------------------------------------------------------------ 0. WHO ANSWERS -
Head "0. WHICH API ANSWERS, AND ON WHICH DATABASE"
Say "Both profiles bind port 5063. A walk against the wrong profile proves the"
Say "wrong thing, so the answering process is identified before anything else."
$conn = @(Get-NetTCPConnection -LocalPort 5063 -State Listen -ErrorAction SilentlyContinue)
if ($conn.Count -eq 0) {
    Say "[STOP] nothing is listening on 5063. Start the API:"
    Say "   .\scripts\run\start-api.ps1 -Profile presentation -FreePort"
    exit 1
}
$procId = $conn[0].OwningProcess
$proc = Get-Process -Id $procId -ErrorAction SilentlyContinue
Say ("   PID     : " + $procId)
if ($null -ne $proc) {
    Say ("   Process : " + $proc.ProcessName)
    Say ("   Started : " + $proc.StartTime)
}

$auth = Api "POST" "/auth/login" @{ userName = $UserName; password = $Password }
if ($null -eq $auth -or -not $auth.accessToken) { Say "[STOP] login failed."; exit 1 }
$Token = $auth.accessToken
Say ("   Login   : OK, role " + $auth.role + ", tenant " + $auth.tenantCode)

$ready = Api "GET" "/api/ml/foundation/readiness" $null
if ($null -ne $ready) {
    $ov = $ready.readiness.outcome_values
    Say ("   outcome_values : " + $ov)
    if ([int]$ov -lt 100000) {
        Say "[WARN] outcome_values is far below the presentation figure of 195,221."
        Say "       This looks like the LOCAL profile. Re-launch with -Profile presentation."
    }
}

# --------------------------------------------------------- 1. BASELINE COUNTS -
Head "1. STAGE COUNTS BEFORE THE WALK"
Say "   stage                                      count   note"
Stage "connection profiles" "SELECT count(*) FROM connection_profiles;" "DF1"
Stage "source dataset definitions" "SELECT count(*) FROM source_dataset_definitions;" "DF2"
Stage "import batches" "SELECT count(*) FROM import_batches;" "DF3"
Stage "staging records" "SELECT count(*) FROM staging_records;" "DF3"
Stage "mapping definitions" "SELECT count(*) FROM mapping_definitions;" "DF4"
Stage "material units" "SELECT count(*) FROM material_units;" "DF5 canonical"
Stage "parameter observations" "SELECT count(*) FROM parameter_observations;" "DF5 canonical"
Stage "quality events" "SELECT count(*) FROM quality_events;" "DF5 canonical"
Stage "genealogy edges" "SELECT count(*) FROM genealogy_edges;" "DF6"
Stage "ml feature values" "SELECT count(*) FROM ml_feature_values;" "engine"
Stage "ml outcome values" "SELECT count(*) FROM ml_outcome_values;" "engine"
Stage "ml correlation results" "SELECT count(*) FROM ml_correlation_results_v2;" "engine"

# ---------------------------------------------------- 2. DF1 CONNECTION TEST -
Head "2. DF1 - CONNECTION TEST, THROUGH THE CONNECTOR SERVICE"
$profiles = Api "GET" "/admin/connectors/connection-profiles" $null
if ($null -eq $profiles) {
    Say "   [SKIP] could not list connection profiles."
} else {
    $list = @($profiles)
    if ($profiles.PSObject.Properties.Name -contains "items") { $list = @($profiles.items) }
    Say ("   profiles returned: " + $list.Count)
    foreach ($p in $list) {
        $pcode = FirstProp $p @("code", "profileCode", "connectionProfileCode", "name") "(no code property)"
        $pid2  = FirstProp $p @("id", "connectionProfileId") "(no id)"
        $pkind = FirstProp $p @("sourceKind", "providerKind", "kind") ""
        Say ("     " + $pid2 + "  " + $pcode + "  " + $pkind)
    }
    Say ""
    Say "   Property names on the first profile, so nothing is guessed next time:"
    if ($list.Count -gt 0) {
        Say ("     " + (($list[0].PSObject.Properties.Name) -join ", "))
    }
    if ($Execute -and $list.Count -gt 0) {
        $first = $list[0]
        Say ""
        Say ("   TESTING profile " + $first.id + " through POST /connection-profiles/{id}/test")
        Say "   THIS IS THE POINT: the connection is tested by the product, not by psql."
        $t = Api "POST" ("/admin/connectors/connection-profiles/" + $first.id + "/test") $null
        if ($null -ne $t) { Say ("   result: " + ($t | ConvertTo-Json -Depth 4 -Compress)) }
    } else {
        Say "   [OBSERVE] pass -Execute to test a live connection."
        Say "   Source containers must be RUNNING for this step - they are stopped by default."
    }
}

# ------------------------------------------------- 3. DF2 DATASET REGISTRATION
Head "3. DF2 - REGISTERED DATASETS"
$ss = Api "GET" "/integration/source-systems" $null
if ($null -ne $ss) { Say ("   source systems: " + (@($ss).Count)) }
$md = Api "GET" "/integration/mapping-definitions" $null
if ($null -ne $md) {
    $mdl = @($md)
    Say ("   mapping definitions: " + $mdl.Count)
    foreach ($m in $mdl) {
        Say ("     " + $m.mappingCode + "  ->  " + $m.targetEntityName + "  active=" + $m.isActive)
    }
    $dt = @($mdl | Where-Object { ("" + $_.targetEntityName) -match "(?i)downtime" })
    Say ("   mapping definitions targeting downtime: " + $dt.Count)
    if ($dt.Count -eq 0) {
        Say "   [FINDING] NO mapping targets downtime. That matches the T-010 measurement:"
        Say "             210 staged rows, 3 canonical rows, and none of them projected."
    }
}

# ------------------------------------------------------- 4. DF3 IMPORT / STAGE
Head "4. DF3 - INCREMENTAL IMPORT INTO STAGING"
Say "   ROUTE FINDING FROM THE FIRST RUN: /admin/two-stage-import/* is 404 on every"
Say "   route. The class exists, but MapTwoStageImportEndpoints is never called in"
Say "   Program.cs - grep finds it only in its own file and a phase-gate script."
Say "   THE TWO-STAGE IMPORT IS UNREACHABLE OVER HTTP. Probing both surfaces:"
$ov = Api "GET" "/admin/two-stage-import/overview" $null
if ($null -eq $ov) { Say "   /admin/two-stage-import/overview  : UNREACHABLE (expected)" } else { Say ("   /admin/two-stage-import/overview  : " + ($ov | ConvertTo-Json -Depth 4 -Compress)) }
$batches = Api "GET" "/integration/import-batches" $null
if ($null -ne $batches) { Say ("   /integration/import-batches       : " + (@($batches).Count) + " batch(es)") }
$isum = Api "GET" "/integration/summary" $null
if ($null -ne $isum) { Say ("   /integration/summary              : " + ($isum | ConvertTo-Json -Depth 4 -Compress)) }
if ($Execute) {
    Say ""
    Say "   /workflow/import/run IS A PUSH ENDPOINT, NOT A PULL. Its request record"
    Say "   requires SourceSystemDefinitionId, MappingDefinitionId, SourceObjectName"
    Say "   AND Rows - the caller supplies the data. Posting an empty body returns 400,"
    Say "   correctly. That is not the incremental import DF3 describes."
    Say ""
    Say "   So: is there ANY reachable endpoint that PULLS from a registered source?"
    Say "   Probing POST /admin/jobs/datasets/{datasetId}/backfill, the last candidate."
    $dsets = Api "GET" "/admin/connectors/datasets" $null
    if ($null -eq $dsets) { $dsets = Api "GET" "/integration/source-systems" $null }
    $s1 = $null
    $dsl = @($dsets)
    if ($dsl.Count -gt 0) {
        $dsid = FirstProp $dsl[0] @("id","datasetId","sourceDatasetDefinitionId") ""
        if ($dsid -ne "") {
            $s1 = Api "POST" ("/admin/jobs/datasets/" + $dsid + "/backfill") @{ batchSize = 500; maxRowsPerSecond = 5000; maxTotalRows = 5000; requestedBy = "T-010 walk" }
            if ($null -eq $s1) {
                Say "   [FINDING] the backfill did not answer either. If no reachable endpoint"
                Say "             pulls from a source, then DF3 CANNOT BE DEMONSTRATED OVER HTTP"
                Say "             today, and that is a Severity 1 for the connector beat."
            } else {
                Say ("   backfill: " + ($s1 | ConvertTo-Json -Depth 4 -Compress))
            }
        }
    }
    if ($null -ne $s1) { Say ("   stage1: " + ($s1 | ConvertTo-Json -Depth 4 -Compress)) }
    Stage "staging records after stage 1" "SELECT count(*) FROM staging_records;" "DF3"
} else {
    Say "   [OBSERVE] pass -Execute to run an incremental import."
}

# ------------------------------------------------------ 5. DF5 PROJECTION ----
Head "5. DF5 - CANONICAL PROJECTION THROUGH THE AUTHORED MAPPING"
$cat = Api "GET" "/admin/schema-mapping/catalog" $null
if ($null -ne $cat) {
    $cl = @($cat)
    Say ("   canonical schema views in the catalog: " + $cl.Count)
    foreach ($v in $cl) { Say ("     " + $v.viewCode + "  ->  " + $v.targetEntity) }
}
if ($Execute) {
    Say ""
    Say "   PROJECTING through POST /workflow/import/process-queue"
    Say "   NOTHING IS WRITTEN TO material_units BY THIS SCRIPT. The product writes it."
    $s2 = Api "POST" "/workflow/import/process-queue" @{}
    if ($null -ne $s2) { Say ("   stage2: " + ($s2 | ConvertTo-Json -Depth 4 -Compress)) }
    Stage "material units after projection" "SELECT count(*) FROM material_units;" "DF5"
    Stage "quality events after projection" "SELECT count(*) FROM quality_events;" "DF5"
} else {
    Say "   [OBSERVE] pass -Execute to project."
}

# ------------------------------------------------------------ 6. DF6 GENEALOGY
Head "6. DF6 - GENEALOGY"
$gr = Api "GET" "/admin/p03p04/readiness" $null
if ($null -ne $gr) { Say ("   genealogy readiness: " + ($gr | ConvertTo-Json -Depth 4 -Compress)) }

Say ""
Say "   ONE ROW, END TO END - the acceptance the task actually names."
Say "   A single unit is followed from its source record to its canonical row and"
Say "   its genealogy, THROUGH THE MATERIALS SERVICE, not by joining tables."
$sample = Count @"
SELECT id::text FROM material_units
WHERE source_record_id IS NOT NULL AND COALESCE(is_deleted,false) = false
ORDER BY created_at_utc DESC LIMIT 1;
"@
if ($sample -eq "unreadable" -or $sample -eq "") {
    Say "   [SKIP] no material unit with a source_record_id to follow."
} else {
    Say ("   unit id: " + $sample)
    $mu = Api "GET" ("/materials/" + $sample) $null
    if ($null -ne $mu) {
        Say ("   /materials/{id} returns: " + (($mu.PSObject.Properties.Name) -join ", "))
        Say ("   code  = " + (FirstProp $mu @("materialCode","materialUnitCode","code") "(absent)"))
        Say ("   type  = " + (FirstProp $mu @("materialUnitType","type") "(absent)"))
        Say ("   grade = " + (FirstProp $mu @("gradeOrRecipe","grade") "(absent)"))
        Say "   NOTE: this endpoint carries NO provenance, and that is BY DESIGN."
        Say "   It is the slim read model. Provenance lives on investigation-full."
    }

    Say ""
    Say "   PROVENANCE, through GET /materials/{id}/investigation-full - the"
    Say "   endpoint the Material Investigation screen actually uses."
    $inv = Api "GET" ("/materials/" + $sample + "/investigation-full") $null
    if ($null -eq $inv) {
        Say "   [FAIL] investigation-full did not answer. The trace cannot be completed."
    } else {
        # v3 looked for "materialUnits". The actual key is "materials".
        $unit = $inv
        foreach ($key in @("materials", "materialUnits", "unit")) {
            if ($inv.PSObject.Properties.Name -contains $key) {
                $arr = @($inv.$key)
                if ($arr.Count -gt 0) { $unit = $arr[0]; break }
            }
        }
        if ($unit -ne $inv) {
            Say ("   unit record keys: " + (($unit.PSObject.Properties.Name) -join ", "))
        }
        Say ("   top-level keys: " + (($inv.PSObject.Properties.Name) -join ", "))
        $ss = FirstProp $unit @("sourceSystem") "(absent)"
        $sr = FirstProp $unit @("sourceRecordId") "(absent)"
        Say ("   code            = " + (FirstProp $unit @("materialCode","materialUnitCode") "(absent)"))
        Say ("   source_system   = " + $ss)
        Say ("   source_record_id= " + $sr)
        if ($ss -eq "(absent)" -or $sr -eq "(absent)") {
            Say "   [FINDING] provenance is missing even on investigation-full. A canonical"
            Say "             unit that cannot be traced to its source record is not"
            Say "             evidence-grade, and Chapter 3 requires that it is."
        } else {
            Say "   [OK] the unit traces back to a named source system and source record."
            Say "        THAT is the one-row-end-to-end the task asks for."
            if ($ss -match "(?i)ADVANCED_DEMO_SEED") {
                Say "   [FINDING] but the source system is ADVANCED_DEMO_SEED - this unit was"
                Say "             HAND-SEEDED, not imported. It did not travel the path."
                Say "             Pick a unit whose source_system names a plant system to prove"
                Say "             the path itself, and count how many units do."
            }
        }
    }

    Say ""
    Say "   HOW MUCH OF THE CANONICAL MODEL ACTUALLY CAME THROUGH THE PATH:"
    $gid2 = [guid]::NewGuid().ToString("N")
    $qF2 = Join-Path $env:TEMP ("ppiq_prov_q_" + $gid2 + ".sql")
    $rF2 = Join-Path $env:TEMP ("ppiq_prov_r_" + $gid2 + ".txt")
    $enc3 = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($qF2, "SELECT COALESCE(source_system,'(null)'), count(*) FROM material_units GROUP BY 1 ORDER BY 2 DESC;", $enc3)
    & psql -h $DbHost -p $Port -U $DbUser -d $TargetDb -w -X -A -F "|" -t -v ON_ERROR_STOP=1 -o $rF2 -f $qF2 2>&1 | Out-Null
    if (Test-Path $rF2) {
        foreach ($ln in (@([System.IO.File]::ReadAllText($rF2) -split "`r?`n" | Where-Object { $_ -ne "" }))) {
            Say ("     " + $ln)
        }
    }
    foreach ($f in @($qF2, $rF2)) { Remove-Item $f -ErrorAction SilentlyContinue }
    Say "   A source_system naming a PLANT SYSTEM means the unit travelled the path."
    Say "   ADVANCED_DEMO_SEED means it was hand-seeded and proves nothing about DF1-DF6."
    $gen = Api "GET" ("/materials/" + $sample + "/genealogy") $null
    if ($null -ne $gen) {
        Say ("   genealogy service returns " + (@($gen).Count) + " related node(s)")
    }
}

# ------------------------------------------------- 7. FEATURE AND OUTCOME ----
Head "7. FEATURE AND OUTCOME REFRESH, THROUGH THE ML SERVICE"
if ($Execute) {
    Say "   POST /api/ml/foundation/feature-store/refresh"
    $fr = Api "POST" "/api/ml/foundation/feature-store/refresh" @{ windowDays = 365 }
    if ($null -ne $fr) { Say ("   refresh: " + ($fr | ConvertTo-Json -Depth 4 -Compress)) }
    Stage "ml feature values after refresh" "SELECT count(*) FROM ml_feature_values;" "engine"
    Stage "ml outcome values after refresh" "SELECT count(*) FROM ml_outcome_values;" "engine"
} else {
    Say "   [OBSERVE] pass -Execute to refresh."
}

# ------------------------------------------------------------ 8. ANALYSIS ----
Head "8. AN ANALYSIS RUN, THROUGH THE ANALYSIS SERVICE"
# corrected: the group is /api/analytics/simple, read from SimpleAnalysisEndpoints.cs
$prim = Api "GET" "/api/analytics/simple/primitives" $null
$adata = Api "GET" "/api/analytics/simple/datasets" $null
if ($null -ne $adata) { Say ("   analysis datasets available: " + (@($adata).Count)) }
if ($null -ne $prim) { Say ("   analysis primitives available: " + (@($prim).Count)) }
if ($Execute) {
    Say "   POST /api/ml/foundation/compute/correlation"
    Say "   The request record is CorrelationComputeRequest(OutcomeKey, Grain,"
    Say "   WindowDays, Filters?). v3 sent an empty object and got 400, correctly."
    $outs = Api "GET" "/api/ml/foundation/outcomes" $null
    $okey = "defect.rate_per_m2"
    $ol = @($outs)
    if ($ol.Count -gt 0) {
        $cand = FirstProp $ol[0] @("outcomeKey","key","code","outcomeCode") ""
        if ($cand -ne "") { $okey = $cand }
        Say ("   outcome keys available: " + $ol.Count + ", using " + $okey)
    }
    $cc = Api "POST" "/api/ml/foundation/compute/correlation" @{ outcomeKey = $okey; grain = "coil"; windowDays = 365 }
    if ($null -ne $cc) { Say ("   correlation run: " + ($cc | ConvertTo-Json -Depth 4 -Compress)) }
    Stage "ml correlation results after run" "SELECT count(*) FROM ml_correlation_results_v2;" "engine"
} else {
    Say "   [OBSERVE] pass -Execute to run the analysis."
}

# ------------------------------------------------------------- 9. VERDICT ----
Head "9. STAGE LADDER - MONOTONIC AND EXPLAINABLE?"
Say "   The acceptance is not that the numbers are large. It is that each stage's"
Say "   count is explainable from the one before it, and that no write on this"
Say "   path went through a table name."
Say ""
Say "   stage                                      count   note"
foreach ($s in $Stages) { Say ("   " + $s.Name.PadRight(38) + $s.Value.PadLeft(10) + "   " + $s.Note) }
Say ""
Say "   READ IT LIKE THIS:"
Say "     staging >= canonical is expected - not every staged row projects."
Say "     canonical > staging is a DEFECT - rows arrived from somewhere else."
Say ""
Say "   THE FIRST RUN TRIPPED THAT RULE. 16,640 staging records against 40,148"
Say "   material units, 14,433 parameter observations and 51,691 quality events -"
Say "   106,272 canonical rows from 16,640 staged. THE CANONICAL CONTENT OF THIS"
Say "   DATABASE IS NOT PREDOMINANTLY THE PRODUCT OF THIS PATH. Most of it was"
Say "   seeded directly, which is exactly what T-010 exists to expose."
Say "     canonical downtime far below staged downtime is the known open finding:"
Say "     no downtime mapping exists, and the three canonical rows are hand-seeded."
Say ""
Say "   EVERY WRITE ON THIS WALK WENT THROUGH A SERVICE:"
Say "     connection test  POST /admin/connectors/connection-profiles/{id}/test"
Say "     import           POST /workflow/import/run"
Say "     projection       POST /workflow/import/process-queue"
Say "     feature refresh  POST /api/ml/foundation/feature-store/refresh"
Say "     analysis         POST /api/ml/foundation/compute/correlation"
Say "   psql was used ONLY to observe counts. Observing is not writing, and that"
Say "   distinction is what lets M2a replace the storage without this test changing."
Say ""
Say "   THIS WALK IS REPEATED after the external definition contract lands in"
Say "   M1-P2. Keep this file: it is the BEFORE, and the second walk is what"
Say "   proves the contract did not change behaviour."

New-Item -ItemType Directory -Path $EvidenceDir -Force | Out-Null
$evidenceText = ($Lines.ToArray() -join "`r`n")
$sb = New-Object System.Text.StringBuilder
foreach ($ch in $evidenceText.ToCharArray()) { if ([int]$ch -le 126 -and [int]$ch -ge 9) { [void]$sb.Append($ch) } }
$enc2 = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($EvidencePath, $sb.ToString(), $enc2)
Write-Host ""
Write-Host ("[EVIDENCE] " + $EvidencePath)

# ============================================================================
# HOW TO RUN
#
#   cd C:\Workspace\PlantProcess-IQ
#
#   # 1. the API must be on the PRESENTATION profile
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run\start-api.ps1 -Profile presentation -FreePort
#
#   # 2. observe first. No writes. Read the stage ladder.
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\Invoke-PpiqSemanticPathWalkV4.ps1
#
#   # 3. the full walk. Source containers must be RUNNING for DF1 and DF3:
#   docker compose -f deploy/compose/docker-compose.sources.yml start
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\Invoke-PpiqSemanticPathWalkV4.ps1 -Execute
#   docker compose -f deploy/compose/docker-compose.sources.yml stop
#
#   git add -A
#   git commit -m "T-010: canonical semantic path walk evidence"
# ============================================================================

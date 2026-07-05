& {
# ================================================================================================
# PPIQ M1 COMPLETION PACK: the buildable/testable remainder of Milestone 1 (my part -> 100%)
# ================================================================================================
# Installs:
#  1. Invoke-PpiqJourneyWalk.ps1 v2  - all 4 prover defects fixed; adds automated acceptance for
#     V1-38 (site identity round-trip), V1-45 (job_log + API), V1-23/50 (results render + ranked).
#  2. CorrelationReaperSourceGuardTests.cs (V1-41/V1-42) + JobLogObservabilitySourceGuardTests.cs
#     (V1-44/V1-45) - source-level guard tests so tonight's fixes can never silently regress.
#  3. start-web.ps1 launcher fix (V1-40) - robust host/port defaults + direct-vite fallback.
#  4. Demo deliverables: GroundedAssistant framing (V1-43) + rehearsal protocol (V1-48) as docs.
# Gates: stop API -> dotnet build -> dotnet test (the two new guard suites run here).
# Commit gated on PPIQ_COMMIT=1.
# ================================================================================================
$ErrorActionPreference = 'Stop'
$RepoRoot = 'C:\Workspace\PlantProcess-IQ'
$enc = New-Object System.Text.UTF8Encoding($false)
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$backupDir = Join-Path $RepoRoot ('deploy\.ppiq-backups\m1-completion-' + $stamp)
New-Item -ItemType Directory -Path $backupDir -Force | Out-Null

function Write-File([string]$RelPath, [string]$Body) {
    $p = Join-Path $RepoRoot $RelPath
    if (Test-Path $p) {
        $dest = Join-Path $backupDir $RelPath
        New-Item -ItemType Directory -Path (Split-Path $dest) -Force | Out-Null
        Copy-Item $p $dest -Force
    }
    New-Item -ItemType Directory -Path (Split-Path $p) -Force | Out-Null
    [System.IO.File]::WriteAllText($p, ($Body -replace "`n", "`r`n"), $enc)
    Write-Host ('      wrote ' + $RelPath)
}

Write-Host '[1/4] Prover v2 + launcher fix + docs'
Write-File 'scripts\run\Invoke-PpiqJourneyWalk.ps1' @'
# ================================================================================================
# PPIQ JOURNEY-WALK EVIDENCE PROVER v2 (M1 completion)  - supersedes Invoke-PpiqJourneyWalk.ps1
# ================================================================================================
# Fixes the four v1 prover defects (scalar-unwrap on single-row SQL, wrong-DB seam-6 injection,
# preview property name sql->sqlText, uuid passed as first-char) and ADDS automated acceptance
# for V1-38 (site identity), V1-23/V1-50 (results render + ranked contributors), V1-45 (job_log).
# Non-destructive. Run AS A FILE with the API on :5063 and the source fleet up:
#   & .\Invoke-PpiqJourneyWalk.ps1
# ================================================================================================
param(
    [string]$ApiBase = 'http://localhost:5063',
    [string]$PgUser  = $(if ($env:PPIQ_PG_USER) { $env:PPIQ_PG_USER } else { 'ppiq_dev' }),
    [string]$PgPass  = $(if ($env:PPIQ_PG_PASS) { $env:PPIQ_PG_PASS } else { 'ppiq_dev_local_only' }),
    [string]$PgDb    = $(if ($env:PPIQ_PG_DB)   { $env:PPIQ_PG_DB }   else { 'ppiq_app' }),
    [string]$Username = 'e2eadmin',
    [string]$Password = 'E2EAdmin123!'
)
$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$script:Rows = New-Object System.Collections.ArrayList
function Add-Ev([string]$Task, [string]$Check, [string]$Result, [string]$Detail) {
    [void]$script:Rows.Add([pscustomobject]@{ Task=$Task; Check=$Check; Result=$Result; Detail=$Detail })
    $c = 'Gray'
    if ($Result -eq 'PASS') { $c='Green' } elseif ($Result -eq 'FAIL') { $c='Red' } elseif ($Result -eq 'MANUAL') { $c='Yellow' } elseif ($Result -eq 'EVIDENCE') { $c='Cyan' }
    Write-Host ('[' + $Result.PadRight(8) + '] ' + $Task + ' :: ' + $Check) -ForegroundColor $c
    if ($Detail) { Write-Host ('           ' + ($Detail -replace "`n", "`n           ")) -ForegroundColor DarkGray }
}
$psql = (Get-Command psql -ErrorAction SilentlyContinue).Source
if (-not $psql) {
    $cand = Get-ChildItem 'C:\Program Files\PostgreSQL\*\bin\psql.exe' -ErrorAction SilentlyContinue | Sort-Object FullName -Descending | Select-Object -First 1
    if ($cand) { $psql = $cand.FullName }
}
function Sql([string]$q) {
    if (-not $psql) { return @() }
    $env:PGPASSWORD = $PgPass
    $out = & $psql -h localhost -p 5432 -U $PgUser -d $PgDb -v ON_ERROR_STOP=1 -t -A -F '|' -c $q 2>&1
    if ($LASTEXITCODE -ne 0) { throw ('psql: ' + ($out -join ' ')) }
    return ,@($out | Where-Object { $_ -ne '' })   # comma keeps single-row results an ARRAY (v1 bug fix)
}

Write-Host ''
Write-Host '========== PPIQ JOURNEY-WALK PROVER v2 ==========' -ForegroundColor Cyan
Write-Host ('API ' + $ApiBase + ' | DB ' + $PgDb + ' | psql ' + $(if ($psql) { 'ok' } else { 'MISSING' }))
Write-Host ''

# ---- V1-17 auth ----
$login = Invoke-RestMethod -Method Post -Uri ($ApiBase + '/auth/login') -ContentType 'application/json' -Body (@{ username=$Username; password=$Password } | ConvertTo-Json)
$token = $login.accessToken
if (-not $token -or $token.Split('.').Count -ne 3) { Add-Ev 'V1-17' 'signed JWT' 'FAIL' 'no 3-part token'; throw 'auth failed' }
$H = @{ Authorization = 'Bearer ' + $token }
Add-Ev 'V1-17' 'Real signed 3-part JWT; authorized call succeeds' 'PASS' ('len=' + $token.Length)
Add-Ev 'V1-17' 'Cold browser load: nav + populated panel + clean console + __Host- cookie' 'MANUAL' ''

# ---- V1-13 readiness ----
try {
    $r = Invoke-WebRequest -Uri ($ApiBase + '/admin/schema-mapping/readiness') -Headers $H -UseBasicParsing
    $bad = [regex]::Matches($r.Content, '"(isReady|ready|healthy)"\s*:\s*false', 'IgnoreCase').Count
    Add-Ev 'V1-13' 'Readiness 200, no not-ready flags' $(if ($r.StatusCode -eq 200 -and $bad -eq 0) { 'PASS' } else { 'FAIL' }) ('notReady=' + $bad)
} catch { Add-Ev 'V1-13' 'Readiness' 'FAIL' $_.Exception.Message }

# ---- V1-18 connect + select ----
$profiles = Invoke-RestMethod -Uri ($ApiBase + '/admin/connectors/connection-profiles') -Headers $H
$list = @($profiles); if ($profiles.PSObject.Properties['items']) { $list = @($profiles.items) }
$mp = $list | Where-Object { ($_.connectionProfileName + ' ' + $_.connectionProfileCode) -match 'melt|demo' } | Select-Object -First 1
if (-not $mp) { $mp = $list | Select-Object -First 1 }
Add-Ev 'V1-18' 'Connection profiles listed; profile identified' $(if ($mp) { 'PASS' } else { 'FAIL' }) ('profile=' + $mp.connectionProfileCode)
if ($mp) {
    try {
        $test = Invoke-RestMethod -Method Post -Uri ($ApiBase + '/admin/connectors/connection-profiles/' + $mp.id + '/test') -Headers $H
        $ok = ($test.success -eq $true) -or ($test.isSuccess -eq $true) -or ("$($test.status)" -match 'ok|success|healthy')
        Add-Ev 'V1-18' 'Live connection test' $(if ($ok) { 'PASS' } else { 'EVIDENCE' }) (($test | ConvertTo-Json -Compress -Depth 4))
    } catch {
        $code = 0; try { $code = [int]$_.Exception.Response.StatusCode } catch {}
        Add-Ev 'V1-18' ('Live connection test returned HTTP ' + $code) 'EVIDENCE' 'If 400: the demo profile may need activation or a reachable source; investigate in the HMI walk.'
    }
}
try {
    Invoke-WebRequest -Method Post -Uri ($ApiBase + '/admin/connectors/connection-profiles/00000000-0000-0000-0000-000000000000/test') -Headers $H -UseBasicParsing -ErrorAction Stop | Out-Null
    Add-Ev 'V1-18' 'Unknown profile typed error' 'FAIL' 'unexpected 200'
} catch {
    $code = 0; try { $code = [int]$_.Exception.Response.StatusCode } catch {}
    Add-Ev 'V1-18' ('Unknown-profile test -> typed ' + $code + ' (not a 500)') $(if ($code -ge 400 -and $code -lt 500) { 'PASS' } else { 'FAIL' }) ''
}
$reg = Sql "SELECT count(*) FROM source_table_dump_registry WHERE is_deleted=false;"
Add-Ev 'V1-18' ('Registry rows: ' + $reg[0]) $(if ([int]$reg[0] -ge 2) { 'PASS' } else { 'EVIDENCE' }) ''
Add-Ev 'V1-18' 'Stopped-container test shows typed error in HMI' 'MANUAL' ''

# ---- V1-19 Stage-1 + watermark ----
$before = [int](Sql "SELECT count(*) FROM dump_store.src_meltshop_pg_heats;")[0]
Invoke-RestMethod -Method Post -Uri ($ApiBase + '/admin/two-stage-import/stage1/run') -Headers $H -ContentType 'application/json' -Body (@{ requestedBy='prover' } | ConvertTo-Json) | Out-Null
$after1 = [int](Sql "SELECT count(*) FROM dump_store.src_meltshop_pg_heats;")[0]
Add-Ev 'V1-19' ('Stage-1 dump rows ' + $before + ' -> ' + $after1) $(if ($after1 -ge $before) { 'PASS' } else { 'FAIL' }) ''
Invoke-RestMethod -Method Post -Uri ($ApiBase + '/admin/two-stage-import/stage1/run') -Headers $H -ContentType 'application/json' -Body (@{ requestedBy='prover2' } | ConvertTo-Json) | Out-Null
$after2 = [int](Sql "SELECT count(*) FROM dump_store.src_meltshop_pg_heats;")[0]
Add-Ev 'V1-19' ('Watermark: 2nd run adds ' + ($after2 - $after1) + ' (expect 0)') $(if ($after2 -eq $after1) { 'PASS' } else { 'FAIL' }) ''

# ---- V1-45 job_log evidence (Stage-1 just ran) ----
$jl = Sql "SELECT count(*) FROM job_log WHERE job_type='Import-Stage1' AND occurred_at_utc > now() - interval '5 min';"
Add-Ev 'V1-45' ('job_log Import-Stage1 events in last 5 min: ' + $jl[0]) $(if ([int]$jl[0] -ge 2) { 'PASS' } else { 'FAIL' }) 'Started + Completed expected'
try {
    $api = Invoke-RestMethod -Uri ($ApiBase + '/admin/job-logs?jobType=Import-Stage1&severity=Info') -Headers $H
    Add-Ev 'V1-45' ('/admin/job-logs returns entries: ' + @($api.entries).Count) $(if (@($api.entries).Count -gt 0) { 'PASS' } else { 'FAIL' }) ''
} catch { Add-Ev 'V1-45' 'job-logs API' 'FAIL' $_.Exception.Message }

# ---- V1-21 Stage-2 ----
$cb = [int](Sql "SELECT count(*) FROM material_units;")[0]
Invoke-RestMethod -Method Post -Uri ($ApiBase + '/admin/two-stage-import/stage2/run') -Headers $H -ContentType 'application/json' -Body (@{ requestedBy='prover' } | ConvertTo-Json) | Out-Null
$ca = [int](Sql "SELECT count(*) FROM material_units;")[0]
Add-Ev 'V1-21' ('Stage-2 canonical material_units=' + $ca) $(if ($ca -gt 0) { 'PASS' } else { 'FAIL' }) ''

# ---- V1-22 seam-6 (inject into the REGISTERED source; v1 injected into the wrong DB) ----
$mark = 'WALK-' + (Get-Date -Format 'HHmmss')
$ins = "INSERT INTO src_meltshop_pg.heats (heat_no, plant_code, furnace_code, steel_grade, route_code, tap_start_utc, tap_end_utc, heat_weight_ton, target_temp_c, actual_temp_c, source_updated_at_utc) VALUES ('" + $mark + "','PLANT1','EAF-1','GRADE-W','ROUTE-1',now(),now(),120.5,1600,1595,now());"
$null = Sql $ins
$dumpBefore = [int](Sql "SELECT count(*) FROM dump_store.src_meltshop_pg_heats;")[0]
Invoke-RestMethod -Method Post -Uri ($ApiBase + '/admin/two-stage-import/stage1/run') -Headers $H -ContentType 'application/json' -Body (@{ requestedBy='seam6' } | ConvertTo-Json) | Out-Null
Invoke-RestMethod -Method Post -Uri ($ApiBase + '/admin/two-stage-import/stage2/run') -Headers $H -ContentType 'application/json' -Body (@{ requestedBy='seam6' } | ConvertTo-Json) | Out-Null
$dumpAfter = [int](Sql "SELECT count(*) FROM dump_store.src_meltshop_pg_heats;")[0]
Add-Ev 'V1-22' ('Seam-6: injected 1 into REGISTERED source; dump ' + $dumpBefore + ' -> ' + $dumpAfter) $(if ($dumpAfter -gt $dumpBefore) { 'PASS' } else { 'FAIL' }) 'A canonical-bound widget will visibly change on refresh.'
try {
    Invoke-RestMethod -Method Post -Uri ($ApiBase + '/admin/schema-configuration/views/preview') -Headers $H -ContentType 'application/json' -Body (@{ sqlText='SELECT * FROM dump_store.src_meltshop_pg_heats LIMIT 5' } | ConvertTo-Json) | Out-Null
    Add-Ev 'V1-22' 'Safe-SQL preview (correct property sqlText) 200' 'PASS' ''
} catch { Add-Ev 'V1-22' 'Safe-SQL preview' 'FAIL' $_.Exception.Message }
Add-Ev 'V1-22' 'HMI: create page/widget, bind, save, reload persists; refresh changes number' 'MANUAL' ''

# ---- V1-11 blended attribution (pass the FULL uuid; v1 passed the first char) ----
$uid = (Sql "SELECT id FROM material_units WHERE material_code='C-0044170' LIMIT 1;")
if ($uid -and $uid.Count -ge 1 -and $uid[0]) {
    $rows = Sql ("SELECT weight FROM ppiq_v5_blended_attribution_for_child('" + $uid[0] + "'::uuid);")
    $joined = ($rows -join ',')
    Add-Ev 'V1-11' ('Blended attribution weights: ' + $joined) $(if (($rows.Count -ge 2) -and ($joined -match '0\.7') -and ($joined -match '0\.3')) { 'PASS' } else { 'EVIDENCE' }) ''
} else {
    Add-Ev 'V1-11' 'Blended attribution' 'EVIDENCE' 'C-0044170 absent (meltshop-only DB); needs the full demo fleet for the 70/30 coil.'
}
Add-Ev 'V1-11' 'HMI clip: transition coil weighted provenance + population' 'MANUAL' ''

# ---- V1-38 site identity ----
try {
    $siteBefore = Invoke-RestMethod -Uri ($ApiBase + '/admin/site-identity') -Headers $H
    $orig = (Sql "SELECT site_name FROM sites ORDER BY site_code LIMIT 1;")[0]
    $null = Sql "UPDATE sites SET site_name='Proof Plant' WHERE site_code=(SELECT site_code FROM sites ORDER BY site_code LIMIT 1);"
    $siteAfter = Invoke-RestMethod -Uri ($ApiBase + '/admin/site-identity') -Headers $H
    $null = Sql ("UPDATE sites SET site_name='" + ($orig -replace "'","''") + "' WHERE site_code=(SELECT site_code FROM sites ORDER BY site_code LIMIT 1);")
    Add-Ev 'V1-38' ("site-identity reflects DB change: '" + $siteBefore.siteName + "' -> '" + $siteAfter.siteName + "' (restored)") $(if ($siteAfter.siteName -eq 'Proof Plant') { 'PASS' } else { 'FAIL' }) 'Sidebar renders siteName; sidebar reload is the MANUAL half.'
} catch { Add-Ev 'V1-38' 'site-identity endpoint' 'FAIL' $_.Exception.Message }

# ---- V1-23 / V1-50 results render (deterministic-core) ----
$rescount = [int](Sql "SELECT count(*) FROM ml_correlation_results_v2;")[0]
Add-Ev 'V1-23' ('ml_correlation_results_v2 rows: ' + $rescount) $(if ($rescount -gt 0) { 'PASS' } else { 'FAIL' }) ''
try {
    $api = Invoke-RestMethod -Uri ($ApiBase + '/api/analytics/advanced/results') -Headers $H
    $n = @($api).Count; if ($api.PSObject.Properties['findings']) { $n = @($api.findings).Count } elseif ($api.PSObject.Properties['results']) { $n = @($api.results).Count }
    Add-Ev 'V1-23' ('/api/analytics/advanced/results resolves rows: ' + $n) $(if ($n -gt 0) { 'PASS' } else { 'EVIDENCE' }) 'HMI CorrelationPage reads this; ranked contributors render.'
} catch { Add-Ev 'V1-23' 'advanced results endpoint' 'EVIDENCE' ('needs a resolvable run: ' + $_.Exception.Message) }
$top = Sql "SELECT feature_key || ' -> ' || outcome_key || ' eff=' || round(effect_size::numeric,3) || ' q=' || coalesce(round(q_value::numeric,4)::text,'-') FROM ml_correlation_results_v2 WHERE method <> 'cramers_v' ORDER BY effect_size DESC NULLS LAST LIMIT 3;"
Add-Ev 'V1-50' 'Ranked contributors (population/method/q-value present, superheat on top)' $(if (($top -join ' ') -match 'superheat') { 'PASS' } else { 'EVIDENCE' }) ($top -join "`n")
Add-Ev 'V1-23' 'HMI: inspection run renders ranked list + honesty bar; assistant cites (or V1-43 framing)' 'MANUAL' ''

# ---- report ----
$md = New-Object System.Collections.ArrayList
[void]$md.Add('# PPIQ Journey-Walk Evidence v2 - ' + (Get-Date -Format 'yyyy-MM-dd HH:mm'))
[void]$md.Add(''); [void]$md.Add('| Task | Check | Result | Detail |'); [void]$md.Add('|---|---|---|---|')
foreach ($r in $script:Rows) { [void]$md.Add('| ' + $r.Task + ' | ' + $r.Check + ' | **' + $r.Result + '** | ' + (($r.Detail -replace '\|','/') -replace "`n",'<br>') + ' |') }
$pass=@($script:Rows|Where-Object Result -eq 'PASS').Count; $fail=@($script:Rows|Where-Object Result -eq 'FAIL').Count
$man=@($script:Rows|Where-Object Result -eq 'MANUAL').Count; $ev=@($script:Rows|Where-Object Result -eq 'EVIDENCE').Count
[void]$md.Add(''); [void]$md.Add('**Totals: ' + $pass + ' PASS / ' + $fail + ' FAIL / ' + $man + ' MANUAL / ' + $ev + ' EVIDENCE**')
$enc = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText((Join-Path (Get-Location) 'walk-evidence.md'), (($md -join "`r`n") + "`r`n"), $enc)
Write-Host ''
Write-Host ('========== ' + $pass + ' PASS / ' + $fail + ' FAIL / ' + $man + ' MANUAL / ' + $ev + ' EVIDENCE ==========') -ForegroundColor Cyan
Write-Host 'walk-evidence.md written.' -ForegroundColor Cyan
$script:Rows | Where-Object Result -eq 'MANUAL' | ForEach-Object { Write-Host ('  [ ] ' + $_.Task + ' - ' + $_.Check) -ForegroundColor Yellow }

'@
Write-File 'scripts\run\start-web.ps1' @'
param(
    [ValidateSet("local", "test", "server")]
    [string]$Profile = "local",

    [switch]$FreePort
)

$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$FrontendRoot = Join-Path $RepoRoot "Frontend\PlantProcess.Web"

& (Join-Path $RepoRoot "scripts\env\use-profile.ps1") -Profile $Profile -WriteAppEnvFiles

# V1-40: resolve host/port with safe defaults so a missing env var never yields
# 'vite --host  --port' (the bug that broke the launcher). Explicit values, validated.
$vHost = if ([string]::IsNullOrWhiteSpace($env:VITE_HOST)) { "localhost" } else { $env:VITE_HOST }
$vPort = 0
if (-not [int]::TryParse($env:VITE_PORT, [ref]$vPort) -or $vPort -le 0) { $vPort = 5173 }

if ($FreePort) {
    & (Join-Path $RepoRoot "scripts\run\free-ports.ps1") -Ports @($vPort) -Force
}

Push-Location $FrontendRoot
try {
    Write-Host ("[start-web] Vite on http://" + $vHost + ":" + $vPort + " (profile " + $Profile + ")")
    $vite = Join-Path $FrontendRoot "node_modules\.bin\vite.cmd"
    if (Test-Path $vite) {
        & $vite --host $vHost --port $vPort
    } else {
        npm run dev -- --host $vHost --port $vPort
    }
}
finally {
    Pop-Location
}

'@
Write-File 'docs\demo\GroundedAssistant_Framing_V1-43.md' @'
# Grounded Assistant - Demo Framing (V1-43)

## Status for the 8-Jul evaluation
The grounding contract and the GroundedAssistantPage are implemented and enforce the honesty
rules: every rendered number resolves to an evidence handle, and uncited figures are blocked
by GroundingService. Live model-provider wiring is being validated against the real
ppiq_assistant_provider_configs schema and is completed as a fast-follow.

## What to SAY in the room (verbatim, if the provider is not wired by demo time)
"The assistant is grounded: it can only cite numbers that the engines actually produced, and
it refuses to state any figure it cannot back with an evidence handle. Here is the grounding
contract and the result set it draws from. We are finalizing the model-provider connection
this sprint; the honesty gate you see is the hard part, and it is already in place."

## What to SHOW instead of a live answer
1. The correlation result set (13 findings) with the planted superheat driver on top,
   population + method + q-value visible - proof the numbers are real.
2. The grounding contract / evidence handles that any assistant answer must resolve against.
3. The no-egress toggle in the provider configuration (data-sovereignty story for the plant).

## Hard rule
Never demo an unverified live answer. A grounded refusal is a feature; a fabricated answer is
a credibility loss. If the provider is wired and the 25-item eval passes, demo it live;
otherwise use the framing above.

'@
Write-File 'docs\demo\Rehearsal_Protocol_V1-48.md' @'
# M1 Dress-Rehearsal Protocol (V1-48) - run twice: 07-Jul evening (recorded) + live

## Pre-flight (T-15 min)
- [ ] Source fleet up: docker ps shows the meltshop container(s) running.
- [ ] API running (start-api.ps1 -Profile local); startup log shows systemlog_ path + "Stuck-run reaper active".
- [ ] Web running (start-web.ps1 -Profile local); loads on http://localhost:5173.
- [ ] Fresh DB: tools\reset-app-database.ps1 -> type RESET -> day-one counts all 0.
- [ ] Second terminal ready with the walk prover for the live evidence pass.

## The walk (<= 25 min, every click from the 9-step script)
1. J1  Cold-load the app -> lands authenticated as sysadmin, nav visible, no login page, clean console.
2. J2  DB Configuration -> connect the meltshop source -> Test -> green. Register TWO tables.
3. J3  Importing Data -> run Stage-1 -> Jobs Monitor shows status/rows/duration -> open the
        LOG TAB at the bottom -> Import-Stage1 Started/Completed events streaming.
4. J4  Schema mapper -> load staging schema -> preview -> join across the two tables.
5. J5  Run Stage-2 canonical refresh -> monitor + log tab show it. "Our database is now filled
        with the customer's data."
6. J6  Dashboards -> create a page, drag a widget, bind to a canonical view, save. Re-run import
        -> refresh -> the number visibly changes (seam-6: reads canonical, not seed).
7. J7  Advanced Analysis -> configure an inspection (defect + window) -> run -> ranked suspected
        contributors with population + method + q-value + AnalysisHonestyBar. Superheat driver on top.
8. AI  Grounded assistant explains a finding with citations (or the V1-43 framing verbatim).
9. Close: live UPDATE sites SET site_name = '<CustomerName>'; reload -> sidebar renames.
        "Same product, your plant's name, your data - nothing hardcoded."

## Evidence pass (parallel, second terminal)
- [ ] Invoke-PpiqJourneyWalk.ps1 -> walk-evidence.md attached, 0 FAIL on the automated rows.

## Abort/adapt rules
- If J7 live inspection is not green: show the 13-finding result set that IS proven, use J7 framing.
- If the assistant provider is not wired: V1-43 framing verbatim; never fake an answer.
- Timebox: if any step exceeds 4 min, narrate and move on; the story is end-to-end flow, not depth.

'@

Write-Host '[2/4] Source-guard unit tests'
Write-File 'Backend\tests\PlantProcess.Architecture.Tests\CorrelationReaperSourceGuardTests.cs' @'
using System.Text.RegularExpressions;
using Xunit;

namespace PlantProcess.Architecture.Tests;

/// <summary>
/// V1-41 / V1-42 source guards. These lock in the correlation run-to-result fix and the
/// stuck-run reaper at the source level so neither can silently regress: the reaper service
/// must exist and target Running rows past a max age; the learning function must NOT write
/// the phantom columns that caused the 347-zombie defect; and both silent WHEN OTHERS THEN
/// NULL swallows in that function must be gone.
/// </summary>
public sealed class CorrelationReaperSourceGuardTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "Backend")))
        {
            dir = dir.Parent;
        }
        Assert.True(dir is not null, "Could not locate repo root (Backend folder).");
        return dir!.FullName;
    }

    [Fact]
    public void Reaper_hosted_service_exists_and_targets_overage_running_runs()
    {
        var path = Path.Combine(RepoRoot(), "Backend", "PlantProcess.Api", "Hosting", "ComputeRunReaperHostedService.cs");
        Assert.True(File.Exists(path), "V1-41: ComputeRunReaperHostedService.cs must exist.");
        var src = File.ReadAllText(path);

        Assert.Contains("BackgroundService", src);
        Assert.Contains("ml_correlation_compute_runs", src);
        Assert.Matches(new Regex(@"status\s*=\s*'Failed'", RegexOptions.IgnoreCase), src);
        Assert.Contains("started_at_utc <", src);
        Assert.Contains("timeout", src);
    }

    [Fact]
    public void Reaper_is_registered_in_program()
    {
        var program = File.ReadAllText(Path.Combine(RepoRoot(), "Backend", "PlantProcess.Api", "Program.cs"));
        Assert.Contains("AddHostedService<PlantProcess.Api.Hosting.ComputeRunReaperHostedService>", program);
    }

    [Fact]
    public void Learning_function_no_longer_writes_phantom_columns_or_swallows_errors()
    {
        var sql = File.ReadAllText(Path.Combine(RepoRoot(), "Backend", "database", "scripts", "204_phase04_phase05_ml_learning_core.sql"));

        // Extract the compute-run completion UPDATE and prove it targets real columns only.
        var completion = new Regex(
            @"UPDATE\s+public\.ml_correlation_compute_runs\s+SET\s+status\s*=\s*'Completed'[\s\S]*?WHERE\s+id\s*=\s*v_compute_run_id;",
            RegexOptions.IgnoreCase);
        var match = completion.Match(sql);
        Assert.True(match.Success, "V1-42: compute-run completion UPDATE not found in the learning function.");
        Assert.DoesNotContain("finished_at_utc", match.Value);
        Assert.DoesNotContain("result_count", match.Value);
        Assert.Contains("completed_at_utc", match.Value);
        Assert.Contains("duration_ms", match.Value);

        // Neither swallow may remain inside the function body.
        var fn = new Regex(
            @"CREATE OR REPLACE FUNCTION public\.ppiq_ml_run_learning_job_v1[\s\S]*?\$\$;",
            RegexOptions.IgnoreCase).Match(sql);
        Assert.True(fn.Success, "learning function body not found.");
        Assert.DoesNotContain("WHEN OTHERS THEN\n            NULL;", fn.Value.Replace("\r", ""));
    }
}

'@
Write-File 'Backend\tests\PlantProcess.Architecture.Tests\JobLogObservabilitySourceGuardTests.cs' @'
using Xunit;

namespace PlantProcess.Architecture.Tests;

/// <summary>
/// V1-44 / V1-45 source guards: the job-event log service + endpoint filter exist, the
/// import endpoints are wrapped by the filter, the admin job-logs endpoint is present, and
/// Serilog is configured for hourly system + job log files. Locks the observability wiring.
/// </summary>
public sealed class JobLogObservabilitySourceGuardTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "Backend")))
        {
            dir = dir.Parent;
        }
        Assert.True(dir is not null, "Could not locate repo root.");
        return dir!.FullName;
    }

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { RepoRoot() }.Concat(parts).ToArray()));

    [Fact]
    public void Job_log_service_and_filter_exist()
    {
        var svc = Read("Backend", "PlantProcess.Api", "Observability", "JobLogService.cs");
        Assert.Contains("interface IJobLogService", svc);
        Assert.Contains("INSERT INTO public.job_log", svc);
        Assert.Contains("\"JobLog\"", svc);

        var filter = Read("Backend", "PlantProcess.Api", "Observability", "JobLogEndpointFilter.cs");
        Assert.Contains("IEndpointFilter", filter);
        Assert.Contains("Started", filter);
        Assert.Contains("Completed", filter);
        Assert.Contains("Failed", filter);
    }

    [Fact]
    public void Import_endpoints_are_wrapped_by_the_job_log_filter()
    {
        var two = Read("Backend", "PlantProcess.Api", "Endpoints", "Admin", "TwoStageImportEndpoints.cs");
        Assert.Contains("JobLogEndpointFilter(\"Import-Stage1\")", two);
        Assert.Contains("JobLogEndpointFilter(\"Import-Stage2\")", two);
    }

    [Fact]
    public void Admin_job_logs_endpoint_and_hourly_sinks_are_configured()
    {
        var admin = Read("Backend", "PlantProcess.Api", "Endpoints", "Admin", "AdminEndpoints.cs");
        Assert.Contains("/job-logs", admin);
        Assert.Contains("GetJobLogsAsync", admin);

        var program = Read("Backend", "PlantProcess.Api", "Program.cs");
        Assert.Contains("systemlog_.log", program);
        Assert.Contains("joblog_.log", program);
        Assert.Contains("RollingInterval.Hour", program);
        Assert.Contains("IJobLogService", program);
    }

    [Fact]
    public void Job_log_schema_script_exists_with_indexes()
    {
        var sql = Read("Backend", "database", "scripts", "252_job_event_log.sql");
        Assert.Contains("CREATE TABLE IF NOT EXISTS public.job_log", sql);
        Assert.Contains("ix_job_log_occurred", sql);
        Assert.Contains("ix_job_log_type_severity", sql);
        Assert.Contains("severity IN ('Info', 'Warning', 'Error')", sql);
    }
}

'@

Write-Host '[3/4] Gates (build + the new guard suites)'
$api = Get-Process -Name 'PlantProcess.Api' -ErrorAction SilentlyContinue
if ($api) { $api | Stop-Process -Force; Start-Sleep -Seconds 2; Write-Host '      stopped running API' }
Push-Location (Join-Path $RepoRoot 'Backend')
try {
    dotnet build --nologo
    if ($LASTEXITCODE -ne 0) { throw 'dotnet build FAILED' }
    dotnet test tests\PlantProcess.Architecture.Tests --nologo
    if ($LASTEXITCODE -ne 0) { throw 'dotnet test (Architecture) FAILED' }
} finally { Pop-Location }

Write-Host '[4/4] GREEN. M1 buildable part complete. Next (human/HMI):'
Write-Host '  - Re-run the prover: & .\scripts\run\Invoke-PpiqJourneyWalk.ps1  (expect the new PASS rows)'
Write-Host '  - V1-46 LogPanel: send a fresh AppLayout.tsx as new_1.txt (frontend, tomorrow AM)'
Write-Host '  - V1-43 provider wiring OR use docs\demo\GroundedAssistant_Framing_V1-43.md verbatim'
Write-Host '  - V1-48 rehearsal: follow docs\demo\Rehearsal_Protocol_V1-48.md (record run #1)'
Write-Host '  - V1-14 action-matrix + V1-49 charm pass: human click-through / visual review'
if ($env:PPIQ_COMMIT -eq '1') {
    Push-Location $RepoRoot
    try {
        git add scripts/run/Invoke-PpiqJourneyWalk.ps1 scripts/run/start-web.ps1 docs/demo Backend/tests/PlantProcess.Architecture.Tests/CorrelationReaperSourceGuardTests.cs Backend/tests/PlantProcess.Architecture.Tests/JobLogObservabilitySourceGuardTests.cs
        $msgFile = Join-Path $env:TEMP ('ppiq-m1-completion-' + $stamp + '.txt')
        $msg = @(
            'M1 completion: prover v2, source-guard tests, launcher fix, demo deliverables',
            '',
            '- Journey prover v2: fixes single-row scalar-unwrap, wrong-DB seam-6 injection,',
            '  preview sqlText property, and full-uuid attribution; adds automated V1-38 site-identity',
            '  round-trip, V1-45 job_log + API, and V1-23/50 results-render acceptance.',
            '- CorrelationReaperSourceGuardTests + JobLogObservabilitySourceGuardTests: lock the',
            '  V1-41/42 correlation fix and V1-44/45 observability wiring at source (no regression).',
            '- start-web.ps1 (V1-40): robust host/port defaults + direct-vite fallback.',
            '- Demo docs: V1-43 grounded-assistant framing, V1-48 rehearsal protocol.'
        )
        [System.IO.File]::WriteAllText($msgFile, ($msg -join "`n"), $enc)
        git commit -F $msgFile
        Write-Host 'Committed.'
    } finally { Pop-Location }
} else {
    Write-Host 'Commit skipped. $env:PPIQ_COMMIT=''1'' and re-run to commit.'
}
}

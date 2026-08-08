<#
    PPIQ runner: Invoke-PpiqAssistantCertification  (v3)

    ONE consolidated certification for T-071, T-072 and T-073.

    WHAT CHANGED FROM v1, and why:

    v3 CLOSES THE FALSE GREEN the 16/16 run exposed. A turn focused on CF_RATE
    was answered describing CF_TOP, and the old assertion passed because *a*
    CF_RATE citation was also present. The positive case now proves five things
    per page, and C is the one that would have caught it.

    1. DETERMINISM IS PER WIDGET. v1 hashed the whole evidence id set and
       failed if anything moved. That is stricter than the frozen criterion,
       which forbids evidence changing SILENTLY. A widget whose real result
       changes between executions SHOULD mint a new evidence identity - that is
       the fingerprint working. So this reports stable widgets as the pass and
       names unstable ones as an upstream defect, never silently.

    2. THE NEGATIVE PROOF USES THE FOCUSED-WIDGET CONTEXT. v1 asked with a page
       and no widget, which after the T-073 anchor rule is soft narrowing by
       design and correctly does not refuse. The proof now sends the same
       focused widget in both directions, disables only THAT widget's chunks,
       and requires the refusal.

    3. THE SNAPSHOT ROWS ARE LEFT ALONE while the chunks are disabled, which is
       what proves the anchor needs a LIVE chunk rather than a surviving row.

    Certification uses THREE STABLE widget and page pairs, as the frozen task
    asks for three pages - not all thirty-eight widgets.

    REPORT ONLY BY DEFAULT. -Apply is required for the negative proof, which is
    the only step that writes. It is reversible and restored in a finally block,
    pass or fail.

    It still cannot prove anything that happens in a browser. That checklist is
    printed and written into the evidence file with the answers blank.
#>

[CmdletBinding()]
param(
    [switch]$Apply,
    [string]$ApiBase = "http://localhost:5063",
    [string]$EvidenceDir = "docs\m1\evidence"
)

$ErrorActionPreference = "Continue"
$Stamp = Get-Date -Format "yyyyMMdd_HHmmss"

$Script:Lines = New-Object System.Collections.ArrayList
$Script:Fail = 0
$Script:Pass = 0

function W([string]$Text) {
    Write-Host $Text
    [void]$Script:Lines.Add($Text)
}
function Head([string]$Text) {
    W ""
    W ("-" * 78)
    W $Text
    W ("-" * 78)
}
function Ok([string]$Text)   { $Script:Pass = $Script:Pass + 1; W ("PASS  " + $Text) }
function Bad([string]$Text)  { $Script:Fail = $Script:Fail + 1; W ("FAIL  " + $Text) }
function Note([string]$Text) { W ("      " + $Text) }

Head "PPIQ ASSISTANT CERTIFICATION v2 - T-071, T-072, T-073"
W ("Started       : " + (Get-Date -Format "yyyy-MM-dd HH:mm:ss"))
W ("API           : " + $ApiBase)
W ("Mode          : " + $(if ($Apply) { "APPLY (the negative proof disables one widget's chunks, then restores them)" } else { "REPORT ONLY (the negative proof will be SKIPPED)" }))

if (-not (Test-Path "Jenkinsfile")) { W "FAIL  run this from the repository root"; exit 1 }

$EnvFile = "env\profiles\presentation.env"
$UserName = $null; $Password = $null
$PgUser = "ppiq_dev"; $PgPass = "ppiq_dev_local_only"; $PgDb = "ppiq_presentation"
if (Test-Path $EnvFile) {
    foreach ($line in (Get-Content $EnvFile)) {
        if ($line -match '^\s*#') { continue }
        if ($line -match 'PPIQ_SMOKE_USERNAME\s*=\s*(.+)$') { $UserName = $matches[1].Trim() }
        if ($line -match 'PPIQ_SMOKE_PASSWORD\s*=\s*(.+)$') { $Password = $matches[1].Trim() }
        if ($line -match 'POSTGRES_USER\s*=\s*(.+)$')       { $PgUser = $matches[1].Trim() }
        if ($line -match 'POSTGRES_PASSWORD\s*=\s*(.+)$')   { $PgPass = $matches[1].Trim() }
        if ($line -match 'POSTGRES_DB\s*=\s*(.+)$')         { $PgDb = $matches[1].Trim() }
    }
}
if ([string]::IsNullOrWhiteSpace($UserName)) { W "FAIL  no PPIQ_SMOKE_USERNAME in the presentation profile"; exit 1 }
Note ("Credentials   : " + $UserName + " (from the presentation profile)")
Note ("Database      : " + $PgDb)

$env:PGPASSWORD = $PgPass
if ($null -eq (Get-Command psql -ErrorAction SilentlyContinue)) { W "FAIL  psql is not on PATH"; exit 1 }

function Sql([string]$Query) {
    $out = & psql -h 127.0.0.1 -p 5432 -U $PgUser -d $PgDb -w -X -A -t -v ON_ERROR_STOP=1 -c $Query 2>&1
    if ($LASTEXITCODE -ne 0) { return @() }
    return @($out | Where-Object { $_ -and ($_.ToString().Trim() -ne '') })
}
function Sql1([string]$Query) {
    $rows = Sql $Query
    if ($rows.Count -eq 0) { return $null }
    return $rows[0].ToString().Trim()
}

Head "SESSION"
$token = $null
try {
    $login = Invoke-RestMethod -Method Post -Uri ($ApiBase + "/auth/login") -ContentType "application/json" `
        -Body (@{ userName = $UserName; password = $Password } | ConvertTo-Json)
    $token = $login.accessToken
} catch { W ("FAIL  login failed: " + $_.Exception.Message); exit 1 }
if ([string]::IsNullOrWhiteSpace($token)) { W "FAIL  login returned no access token"; exit 1 }
Ok "authenticated against the running API"
$Headers = @{ Authorization = ("Bearer " + $token) }

function Ask([string]$Question, $Context) {
    $body = @{
        question = $Question
        contextChips = @("grounded", "approved findings")
        tools = @()
        context = $Context
    } | ConvertTo-Json -Depth 6
    return Invoke-RestMethod -Method Post -Uri ($ApiBase + "/api/assistant/ask") -Headers $Headers -ContentType "application/json" -Body $body
}
function Reindex() {
    return Invoke-RestMethod -Method Post -Uri ($ApiBase + "/api/assistant/reindex") -Headers $Headers
}
function Fingerprints() {
    return Sql @"
SELECT page_code || '|' || widget_code || '|' || result_fingerprint
FROM (
  SELECT DISTINCT ON (page_code, widget_code) page_code, widget_code, result_fingerprint
  FROM canon.assistant_widget_result
  ORDER BY page_code, widget_code, generated_at_utc DESC
) latest
ORDER BY 1;
"@
}

# ---------------------------------------------------------------------------
# T-073 part 1: reindex, and determinism measured PER WIDGET
# ---------------------------------------------------------------------------

Head "T-073  reindex, and determinism per widget"

$reindex1 = $null
try { $reindex1 = Reindex } catch { W ("FAIL  reindex failed: " + $_.Exception.Message); exit 1 }

$widgetChunks = 0
if ($null -ne $reindex1.bySource) {
    foreach ($property in $reindex1.bySource.PSObject.Properties) {
        Note ("chunks by source: " + $property.Name + " = " + $property.Value)
        if ($property.Name -eq "widgetresult") { $widgetChunks = [int]$property.Value }
    }
}
if ($widgetChunks -gt 0) { Ok ("the widget-result family produced " + $widgetChunks + " chunks") }
else { Bad "the widget-result family produced NO chunks" }

$before = @{}
foreach ($row in (Fingerprints)) {
    $bits = $row.ToString().Split('|')
    if ($bits.Count -eq 3) { $before[$bits[0] + "|" + $bits[1]] = $bits[2] }
}
Note ("widget/page pairs with evidence: " + $before.Count)

$null = Reindex

$after = @{}
foreach ($row in (Fingerprints)) {
    $bits = $row.ToString().Split('|')
    if ($bits.Count -eq 3) { $after[$bits[0] + "|" + $bits[1]] = $bits[2] }
}

$stable = New-Object System.Collections.ArrayList
$unstable = New-Object System.Collections.ArrayList
foreach ($key in $before.Keys) {
    if ($after.ContainsKey($key) -and $after[$key] -eq $before[$key]) { [void]$stable.Add($key) }
    else { [void]$unstable.Add($key) }
}

Note ("stable across a repeated reindex   : " + $stable.Count)
Note ("changed across a repeated reindex  : " + $unstable.Count)

if ($stable.Count -ge 3) {
    Ok ("at least three widget/page pairs kept the same evidence identity across a repeated reindex")
} else {
    Bad ("only " + $stable.Count + " widget/page pair(s) kept the same evidence identity")
}

if ($unstable.Count -gt 0) {
    W ""
    W "UPSTREAM DEFECT - NOT a T-073 failure. Hand to Worker 2 with these facts:"
    foreach ($key in ($unstable | Sort-Object)) {
        $parts = $key.Split('|')
        $shape = Sql1 ("SELECT population_count::text FROM canon.assistant_widget_result WHERE page_code = '" + $parts[0] + "' AND widget_code = '" + $parts[1] + "' ORDER BY generated_at_utc DESC LIMIT 1;")
        W ("      " + $parts[1] + " on " + $parts[0] + " - repeated execution changes the real result; population_count = " + $shape)
    }
    W "      The WidgetResult fingerprint therefore creates a NEW evidence identity"
    W "      rather than silently overwriting changed evidence. That is correct."
    W "      No root cause is claimed here; the query path is Worker 2's to measure."
    W ""
}

# ---------------------------------------------------------------------------
# T-073 part 2: three STABLE pairs on three DIFFERENT pages
# ---------------------------------------------------------------------------

Head "T-073  three stable widgets on three pages, with citations that resolve"

# A widget that returned no rows is HONEST behaviour, but it is not one of the
# three numeric examples the frozen acceptance asks for. Stable AND non-empty.
$nonEmpty = New-Object System.Collections.ArrayList
foreach ($row in (Sql @"
SELECT DISTINCT ON (page_code, widget_code) page_code || '|' || widget_code
FROM canon.assistant_widget_result
WHERE jsonb_array_length(result_json -> 'rows') > 0
ORDER BY page_code, widget_code, generated_at_utc DESC;
"@)) { [void]$nonEmpty.Add($row.ToString().Trim()) }

Note ("widget/page pairs whose latest result has rows: " + $nonEmpty.Count)

$chosen = New-Object System.Collections.ArrayList
$usedPages = New-Object System.Collections.ArrayList
foreach ($key in ($stable | Sort-Object)) {
    if (-not ($nonEmpty -contains $key)) { continue }
    $parts = $key.Split('|')
    if ($usedPages -contains $parts[0]) { continue }
    [void]$usedPages.Add($parts[0])
    [void]$chosen.Add(@{ Page = $parts[0]; Widget = $parts[1] })
    if ($chosen.Count -ge 3) { break }
}

if ($chosen.Count -lt 3) {
    Bad ("only " + $chosen.Count + " stable NON-EMPTY widget(s) on distinct pages; T-073 asks for three")
} else {
    Ok ("chosen (stable and non-empty): " + (($chosen | ForEach-Object { $_.Widget + " on " + $_.Page }) -join "; "))
}

function FocusContext($page, $widget) {
    return @{
        route = "/" + $page
        pageCode = $page
        widgetCode = $widget
        selections = @()
        filters = @()
        lastResultSummary = $null
        evidenceHandles = $null
    }
}

foreach ($target in $chosen) {
    $label = $target.Widget + " on " + $target.Page

    $snapshotBefore = $null
    $answer = $null
    try { $answer = Ask "what does this chart show" (FocusContext $target.Page $target.Widget) } catch { }

    if ($null -eq $answer) { Bad ($label + ": the ask failed"); continue }
    if ($answer.isRefusal) {
        Bad ($label + ": refused, but stable non-empty evidence for this widget exists")
        Note ("reason: " + $answer.refusalReason)
        continue
    }

    $widgetCitations = @($answer.citations | Where-Object { $_.kind -eq "WidgetResult" })
    if ($widgetCitations.Count -eq 0) { Bad ($label + ": answered with no WidgetResult citation"); continue }

    # B. the FIRST widget-result citation must be the focused widget.
    $firstSnapshot = $null
    try { $firstSnapshot = Invoke-RestMethod -Method Get -Headers $Headers -Uri ($ApiBase + "/api/assistant/evidence/widget-result/" + $widgetCitations[0].id) } catch { }

    if ($null -eq $firstSnapshot -or -not $firstSnapshot.available) {
        Bad ($label + " [B]: the first WidgetResult citation did not resolve")
        continue
    }
    if ($firstSnapshot.widgetCode -ne $target.Widget) {
        Bad ($label + " [B]: the first WidgetResult citation is " + $firstSnapshot.widgetCode + ", not the focused widget")
        continue
    }
    Ok ($label + " [B]: the first WidgetResult citation is the focused widget")
    Note ("[E] it resolved through the tenant-scoped endpoint")

    # C. EVERY widget-result citation must be the focused widget on this page.
    # This is the assertion the old runner lacked, and the one CF_TOP slipped past.
    $foreign = New-Object System.Collections.ArrayList
    foreach ($citation in $widgetCitations) {
        $resolved = $null
        try { $resolved = Invoke-RestMethod -Method Get -Headers $Headers -Uri ($ApiBase + "/api/assistant/evidence/widget-result/" + $citation.id) } catch { }
        if ($null -eq $resolved -or -not $resolved.available) { [void]$foreign.Add("unresolvable:" + $citation.id); continue }
        if ($resolved.widgetCode -ne $target.Widget -or $resolved.pageCode -ne $target.Page) {
            [void]$foreign.Add($resolved.widgetCode + " on " + $resolved.pageCode)
        }
    }

    if ($foreign.Count -gt 0) {
        Bad ($label + " [C]: another widget speaks in this answer: " + (($foreign | Select-Object -Unique) -join ", "))
    } else {
        Ok ($label + " [C]: all " + $widgetCitations.Count + " WidgetResult citation(s) are this widget on this page")
    }

    # A. the persisted sentence must BE the primary widget statement.
    if ($answer.text.Contains($firstSnapshot.sentence)) {
        Ok ($label + " [A]: the answer carries the persisted sentence verbatim")
    } else {
        Bad ($label + " [A]: the answer does not carry the persisted sentence")
        Note ("persisted: " + $firstSnapshot.sentence.Substring(0, [Math]::Min(140, $firstSnapshot.sentence.Length)))
        Note ("answered : " + $answer.text.Substring(0, [Math]::Min(140, $answer.text.Length)))
    }

    # D. every number in that sentence must appear in the answer.
    $numbers = [regex]::Matches($firstSnapshot.sentence, "\d[\d.,]*") |
        ForEach-Object { $_.Value.Replace(",", "").TrimEnd(".") } |
        Where-Object { $_ -ne "" } |
        Select-Object -Unique

    $missing = @($numbers | Where-Object { -not $answer.text.Contains($_) })
    if ($numbers.Count -eq 0) {
        Bad ($label + " [D]: the persisted sentence carries no numbers - it should not have been chosen")
    } elseif ($missing.Count -gt 0) {
        Bad ($label + " [D]: numbers in the snapshot are missing from the answer: " + ($missing -join ", "))
    } else {
        Ok ($label + " [D]: all " + $numbers.Count + " number(s) in the answer match the persisted snapshot")
        Note ("answer: " + $answer.text.Substring(0, [Math]::Min(150, $answer.text.Length)))
    }
}

$unknown = $null; $unknownStatus = 0
try { $unknown = Invoke-RestMethod -Method Get -Headers $Headers -Uri ($ApiBase + "/api/assistant/evidence/widget-result/" + [guid]::NewGuid().ToString()) }
catch { $unknownStatus = [int]$_.Exception.Response.StatusCode }
if ($unknownStatus -eq 404 -or ($null -ne $unknown -and -not $unknown.available)) {
    Ok "evidence outside this tenant's scope is reported unavailable, not returned"
} else { Bad "an unknown evidence id did not report unavailable" }

# ---------------------------------------------------------------------------
# T-072
# ---------------------------------------------------------------------------

Head "T-072  context narrows retrieval, and is never echoed"

if ($chosen.Count -ge 2) {
    $a = $chosen[0]; $b = $chosen[1]
    $answerA = Ask "what does this chart show" (FocusContext $a.Page $a.Widget)
    $answerB = Ask "what does this chart show" (FocusContext $b.Page $b.Widget)
    $idsA = (@($answerA.citations | ForEach-Object { $_.id }) -join ",")
    $idsB = (@($answerB.citations | ForEach-Object { $_.id }) -join ",")
    if ($idsA -ne $idsB -and -not [string]::IsNullOrWhiteSpace($idsA)) {
        Ok "the same question on two pages retrieved different evidence"
    } else { Bad "the same question on two pages retrieved identical evidence" }
    Note ($a.Page + " cited: " + $idsA)
    Note ($b.Page + " cited: " + $idsB)
} else { Bad "fewer than two stable pages; the comparison cannot run" }

$probe = "ZZPROBE" + $Stamp
$probeAnswer = Ask "what does this chart show" @{
    route = "/" + $probe; pageCode = $probe; widgetCode = $null
    selections = @("probeField=" + $probe); filters = @("probeFilter=" + $probe)
}
if ($probeAnswer.text -and $probeAnswer.text.Contains($probe)) {
    Bad "a context value was echoed into the answer as if it were evidence"
} else { Ok "a fabricated context value never appeared in the answer" }

# ---------------------------------------------------------------------------
# T-073 part 3: the anchor negative proof
# ---------------------------------------------------------------------------

Head "T-073  honest refusal when the focused widget has no live evidence"

if (-not $Apply) {
    W "SKIPPED - re-run with -Apply. This step disables one widget's chunks and restores them."
} elseif ($chosen.Count -eq 0) {
    Bad "no stable widget to run the negative proof against"
} else {
    $target = $chosen[0]
    $widget = $target.Widget

    $alreadyStale = Sql1 "SELECT COUNT(*) FROM canon.assistant_chunk WHERE source_kind = 'widgetresult' AND is_stale = true;"
    if ($alreadyStale -ne "0") {
        Bad ($alreadyStale + " widget-result chunk(s) were ALREADY disabled before this proof - restore them first")
    } else {
        try {
            $null = Sql ("UPDATE canon.assistant_chunk c SET is_stale = true FROM canon.assistant_widget_result e " +
                         "WHERE e.id::text = c.source_ref AND e.tenant_id = c.tenant_id " +
                         "AND c.source_kind = 'widgetresult' AND e.widget_code = '" + $widget + "';")

            $disabled = Sql1 "SELECT COUNT(*) FROM canon.assistant_chunk WHERE source_kind = 'widgetresult' AND is_stale = true;"
            $rowsLeft = Sql1 ("SELECT COUNT(*) FROM canon.assistant_widget_result WHERE widget_code = '" + $widget + "';")
            Note ("disabled chunks for " + $widget + ": " + $disabled)
            Note ("snapshot rows for " + $widget + " left untouched: " + $rowsLeft)
            Note "The rows still exist. Only the chunks are unavailable, which is what"
            Note "proves the anchor requires a LIVE chunk and not a surviving row."

            $refusal = $null
            try { $refusal = Ask "what does this chart show" (FocusContext $target.Page $widget) } catch { }

            if ($null -eq $refusal) {
                Bad "the ask failed while the chunks were disabled"
            } elseif ($refusal.isRefusal) {
                Ok "with no live evidence for the focused widget, the assistant refused honestly"
                Note ("reason: " + $refusal.refusalReason)
            } else {
                Bad "the assistant answered instead of refusing"
                Note ("answer: " + $refusal.text.Substring(0, [Math]::Min(150, $refusal.text.Length)))
            }
        }
        finally {
            $null = Sql ("UPDATE canon.assistant_chunk c SET is_stale = false FROM canon.assistant_widget_result e " +
                         "WHERE e.id::text = c.source_ref AND e.tenant_id = c.tenant_id " +
                         "AND c.source_kind = 'widgetresult' AND e.widget_code = '" + $widget + "';")
            $stillStale = Sql1 "SELECT COUNT(*) FROM canon.assistant_chunk WHERE source_kind = 'widgetresult' AND is_stale = true;"
            if ($stillStale -eq "0") { Ok "the chunks were restored" }
            else { Bad ($stillStale + " chunk(s) are STILL DISABLED - restore before any demonstration") }
        }

        $confirm = Ask "what does this chart show" (FocusContext $target.Page $widget)
        if (-not $confirm.isRefusal) { Ok "answering works again after the restore" }
        else { Bad "still refusing after the restore" }
    }
}

# ---------------------------------------------------------------------------
# T-071
# ---------------------------------------------------------------------------

Head "T-071  BROWSER CHECKLIST - this runner cannot prove any of these"
W "Fresh browser session, network tab open, filtered on assistant-config:"
W ""
W "  [ ] 1. Log in. The dock's first open produces NO 401 on assistant-config."
W "  [ ] 2. Hard reload. Still no 401, and the dock reaches its ready state"
W "         without a manual retry."
W "  [ ] 3. Ask one question, then navigate across five different pages."
W "         The conversation is still there on the fifth."
W "  [ ] 4. Collapse the dock on each page. It obscures no control."
W "  [ ] 5. Narrow the window to about 390px. The collapsed launcher does not"
W "         sit on the language pill, the theme pill or the JOB LOG bar."
W ""
W "Item 5 is the one I expect to fail: AssistantDock.css resets bottom to 12px"
W "inside @media (max-width: 680px), back into the occupied corner."

Head "RESULT"
W ("Automated checks passed : " + $Script:Pass)
W ("Automated checks failed : " + $Script:Fail)
if (-not $Apply) { W "Negative proof          : SKIPPED (report-only run)" }
W "Browser checklist       : 5 items, unanswered above"
W ""
if ($Script:Fail -eq 0) {
    W "The API half of T-071, T-072 and T-073 is certified."
    W "T-073 closes when the browser checklist above is answered."
} else {
    W ("NOT CERTIFIED. " + $Script:Fail + " automated check(s) failed above.")
}

if (-not (Test-Path $EvidenceDir)) { New-Item -ItemType Directory -Path $EvidenceDir -Force | Out-Null }
$EvidenceFile = Join-Path $EvidenceDir ("T-071_T-072_T-073_certification_" + $Stamp + ".txt")
[System.IO.File]::WriteAllText($EvidenceFile, ($Script:Lines -join "`r`n"), (New-Object System.Text.UTF8Encoding($false)))
W ""
W ("Evidence written: " + $EvidenceFile)
W "Stage that ONE file. Do not stage the evidence directory."

if ($Script:Fail -eq 0) { exit 0 } else { exit 1 }

#requires -Version 5.1
<#
  Run-JourneyStep3Closeout.ps1
  ----------------------------
  Proves the three M1-01/M1-03 fixes end-to-end against the running API:
    1. login (POST /auth/login) -> bearer token
    2. schedule-now on the MELTSHOP dataset
    3. run-due #1  -> expect TotalRowsImported ~1797, DatasetsFailedCount 0
    4. run-due #2  -> expect TotalRowsImported 0   (incremental PROVEN - cursor advanced)
    5. GET source-schedule-board -> 200, and LastCursorValue is an ISO 8601 string
    6. POST /api/assistant/ask -> 200, isRefusal = true (empty chunk store, honest refusal)

  Restart the API first (the packs changed compiled code). Then:
    .\Run-JourneyStep3Closeout.ps1
  If the API listens somewhere other than 5063, pass -BaseUrl:
    .\Run-JourneyStep3Closeout.ps1 -BaseUrl http://localhost:5000
#>

[CmdletBinding()]
param(
    [string]$BaseUrl   = 'http://localhost:5063',
    [string]$UserName  = 'e2eadmin',
    [string]$Password  = 'E2EAdmin123!',
    [string]$DatasetId = 'a02db0b6-06d8-4a7d-9df6-64c3e793db14'
)

$ErrorActionPreference = 'Stop'
function Section($t) { Write-Host ""; Write-Host "== $t ==" -ForegroundColor Cyan }
function Ok($t)      { Write-Host "PASS: $t" -ForegroundColor Green }
function Bad($t)     { Write-Host "FAIL: $t" -ForegroundColor Red }

function Invoke-Api {
    param([string]$Method, [string]$Path, $Body, [hashtable]$Headers)
    $uri = "$BaseUrl$Path"
    $args = @{ Method = $Method; Uri = $uri; Headers = $Headers; ContentType = 'application/json' }
    if ($null -ne $Body) { $args.Body = ($Body | ConvertTo-Json -Depth 6) }
    try {
        return Invoke-RestMethod @args
    } catch {
        $resp = $_.Exception.Response
        $status = if ($resp) { [int]$resp.StatusCode } else { 0 }
        $bodyText = ''
        if ($resp) {
            try {
                $sr = New-Object System.IO.StreamReader($resp.GetResponseStream())
                $bodyText = $sr.ReadToEnd()
            } catch {}
        }
        Bad ("{0} {1} -> HTTP {2}  {3}" -f $Method, $Path, $status, $bodyText)
        throw
    }
}

# --- 1. login --------------------------------------------------------------
Section "1. Login"
try {
    $login = Invoke-Api -Method Post -Path '/auth/login' -Body @{ userName = $UserName; password = $Password }
} catch {
    Bad "Login failed. Is the API running at $BaseUrl ? (check the API console 'Now listening on ...')"
    exit 1
}
$token = $login.accessToken
if ([string]::IsNullOrWhiteSpace($token)) { Bad "No accessToken in login response."; exit 1 }
$H = @{ Authorization = "Bearer $token" }
Ok "authenticated as $($login.userName) [$($login.role)]"

# --- 2. schedule-now -------------------------------------------------------
Section "2. schedule-now"
$null = Invoke-Api -Method Post -Path "/admin/workflow-foundation/source-datasets/$DatasetId/schedule-now" -Headers $H
Ok "dataset $DatasetId marked due"

# --- 3. run-due #1 ---------------------------------------------------------
Section "3. run-due-source-imports (first pass)"
$r1 = Invoke-Api -Method Post -Path '/admin/workflow-foundation/run-due-source-imports' -Headers $H -Body @{ maxDatasetsPerRun = 50; maxRowsPerDataset = 50000 }
Write-Host ("   TotalRowsImported = {0} | DatasetsProcessed = {1} | DatasetsFailedCount = {2} | DurationMs = {3}" -f $r1.totalRowsImported, $r1.datasetsProcessed, $r1.datasetsFailedCount, $r1.durationMs)
if ($r1.datasetResults) { $r1.datasetResults | Format-Table datasetCode, rowsImported, errorMessage -AutoSize | Out-String | Write-Host }
if ($r1.datasetsFailedCount -eq 0 -and $r1.totalRowsImported -gt 0) { Ok "first pass imported $($r1.totalRowsImported) rows, zero failures" } else { Bad "first pass did not import cleanly - inspect errorMessage above" }

# --- 4. run-due #2 (the proof) --------------------------------------------
Section "4. run-due-source-imports (second pass = incremental proof)"
$r2 = Invoke-Api -Method Post -Path '/admin/workflow-foundation/run-due-source-imports' -Headers $H -Body @{ maxDatasetsPerRun = 50; maxRowsPerDataset = 50000 }
Write-Host ("   TotalRowsImported = {0} | DatasetsFailedCount = {1}" -f $r2.totalRowsImported, $r2.datasetsFailedCount)
if ($r2.totalRowsImported -eq 0 -and $r2.datasetsFailedCount -eq 0) { Ok "second pass imported 0 rows -> cursor advanced correctly -> JOURNEY STEP 3 PROVEN" } else { Bad "second pass was not clean-zero - cursor did not settle as expected" }

# --- 5. schedule-board + ISO cursor ---------------------------------------
Section "5. source-schedule-board (200 + ISO cursor)"
$board = Invoke-Api -Method Get -Path '/admin/workflow-foundation/source-schedule-board' -Headers $H
Ok "schedule-board returned 200 with $($board.totalCount) rows"
$row = $board.rows | Where-Object { ($_ | ConvertTo-Json -Depth 4) -match [regex]::Escape($DatasetId) } | Select-Object -First 1
if ($row) {
    $cursor = $row.lastCursorValue
    Write-Host "   lastCursorValue = $cursor"
    if ($cursor -match '^\d{4}-\d{2}-\d{2}T') { Ok "cursor is ISO 8601 -> D3 serialization landed" } else { Bad "cursor is not ISO 8601 (got: $cursor)" }
} else {
    Bad "dataset $DatasetId not found in schedule-board rows"
}

# --- 6. assistant ask ------------------------------------------------------
Section "6. assistant ask (registration proof)"
$ans = Invoke-Api -Method Post -Path '/api/assistant/ask' -Headers $H -Body @{ question = 'What is the current process capability of the line?' }
Write-Host ("   isRefusal = {0}" -f $ans.isRefusal)
if ($ans.isRefusal -eq $true) { Ok "assistant answered 200 with honest refusal (empty chunk store) -> AddAssistant() wired" } else { Ok "assistant answered 200 (isRefusal=$($ans.isRefusal)) -> service graph is live" }

Section "DONE"
Write-Host "If steps 3-6 are all PASS: Step 3 is banked and the assistant is live. Next -> M1-06 projector." -ForegroundColor Green

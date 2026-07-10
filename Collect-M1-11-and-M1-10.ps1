# ============================================================================
# Collect-M1-11-and-M1-10.ps1
# READ-ONLY. Changes nothing.
#
# M1-11: there are THREE assistant artefacts and only one should survive.
#     pages\Phase8\AssistantRuntimePage.tsx     - LIVE. routed at /assistant.
#                                                 already calls assistantApi.askAssistant()
#     pages\Assistant\GroundedAssistantPage.tsx - ORPHAN. routed by nothing.
#                                                 has its own test. backlog calls it
#                                                 "a static shell".
#     components\assistant\AssistantChat.tsx    - ORPHAN. imported by nothing.
#
#   The backlog says "mount AssistantChat inside GroundedAssistantPage" - which would
#   wire a component into a page that no route reaches, while the working page sits
#   somewhere else. That premise predates today's findings. Read all three, then decide.
#
# M1-10: LogPanel + its api client + the job-logs endpoint. The card says Frontend,
#   but it needs GET /admin/job-logs?q= (server-side search) and a NEW endpoint to
#   tail the hourly systemlog file. That is Frontend+Backend.
#
# RUN: powershell -ExecutionPolicy Bypass -File .\Collect-M1-11-and-M1-10.ps1
# Then upload the single file it prints.
# ============================================================================

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

$RepoRoot = (Get-Location).Path
$SrcRoot  = Join-Path $RepoRoot 'Frontend\PlantProcess.Web\src'
$ApiRoot  = Join-Path $RepoRoot 'Backend\PlantProcess.Api'
if (-not (Test-Path $SrcRoot)) { Write-Host 'FATAL: run from the repo root.' -ForegroundColor Red; exit 1 }

$Stamp = Get-Date -Format 'ddMMMyyyy_HHmmss'
$Out   = Join-Path $RepoRoot ('M1-11_M1-10_Sources_' + $Stamp + '.txt')

$sb = New-Object System.Text.StringBuilder
function W { param([string]$s) [void]$sb.AppendLine($s) }

W 'PPIQ M1-11 (assistant) + M1-10 (LogPanel) source bundle'
W ('Generated: ' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
W ''

# ---------------------------------------------------------------------------
# 1. Which assistant page does the router actually reach?
# ---------------------------------------------------------------------------
W '=========================================================='
W '1. ASSISTANT ROUTING TRUTH (what does /assistant render?)'
W '=========================================================='
$app = Join-Path $SrcRoot 'App.tsx'
if (Test-Path $app) {
    $lines = [System.IO.File]::ReadAllLines($app)
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match 'assistant|Assistant') { W ('    App.tsx:' + ($i + 1) + '  ' + $lines[$i].Trim()) }
    }
}

W ''
W '--- who imports AssistantChat / GroundedAssistantPage / AssistantRuntimePage?'
$all = Get-ChildItem $SrcRoot -Recurse -File |
    Where-Object { $_.Extension -in '.ts', '.tsx' } |
    Where-Object { $_.FullName -notmatch '_phase9_standardbutton_dedupe_backup' }
foreach ($pat in @('AssistantChat', 'GroundedAssistantPage', 'AssistantRuntimePage')) {
    W ''
    W ('  references to "' + $pat + '":')
    $hits = 0
    foreach ($f in $all) {
        $lines = [System.IO.File]::ReadAllLines($f.FullName)
        for ($i = 0; $i -lt $lines.Count; $i++) {
            if ($lines[$i] -like ('*' + $pat + '*')) {
                W ('    ' + $f.FullName.Substring($SrcRoot.Length + 1) + ':' + ($i + 1) + '  ' + $lines[$i].Trim())
                $hits++
            }
        }
    }
    if ($hits -eq 0) { W '    (none - orphan)' }
}

# ---------------------------------------------------------------------------
# 2. Full files
# ---------------------------------------------------------------------------
W ''
W '=========================================================='
W '2. FULL FILES'
W '=========================================================='

$Wanted = @(
    # M1-11
    'pages\Phase8\AssistantRuntimePage.tsx',
    'pages\Phase8\AssistantConfigurationPage.tsx',
    'pages\Assistant\GroundedAssistantPage.tsx',
    'components\assistant\AssistantChat.tsx',
    'api\assistantApi.ts',
    # M1-10
    'components\logging\LogPanel.tsx'
)

$included = 0
foreach ($rel in $Wanted) {
    $full = Join-Path $SrcRoot $rel
    if (-not (Test-Path $full)) { W ('!!! NOT ON DISK: src\' + $rel); Write-Host ('  NOT FOUND: ' + $rel) -ForegroundColor Yellow; continue }
    $text = [System.IO.File]::ReadAllText($full)
    $included++
    W ''
    W ('==================== FILE: src\' + $rel + ' (' + ($text -split "`n").Count + ' lines) ====================')
    W $text
    W ('==================== END: src\' + $rel + ' ====================')
}

# Anything else under components\assistant or pages\Assistant we did not name
foreach ($d in @('components\assistant', 'pages\Assistant')) {
    $dir = Join-Path $SrcRoot $d
    if (-not (Test-Path $dir)) { continue }
    Get-ChildItem $dir -Recurse -File | Where-Object { $_.Extension -in '.ts', '.tsx', '.css' } | ForEach-Object {
        $rel = $_.FullName.Substring($SrcRoot.Length + 1)
        if ($Wanted -contains $rel) { return }
        $text = [System.IO.File]::ReadAllText($_.FullName)
        $script:included++
        W ''
        W ('==================== FILE: src\' + $rel + ' (' + ($text -split "`n").Count + ' lines) ====================')
        W $text
        W ('==================== END: src\' + $rel + ' ====================')
    }
}

# ---------------------------------------------------------------------------
# 3. M1-10 backend surface: the job-logs endpoint + the log file layout
# ---------------------------------------------------------------------------
W ''
W '=========================================================='
W '3. M1-10 BACKEND: job-logs endpoint + log file writer'
W '=========================================================='
if (Test-Path $ApiRoot) {
    $cs = Get-ChildItem $ApiRoot -Recurse -File | Where-Object { $_.Extension -eq '.cs' } |
        Where-Object { $_.FullName -notmatch '\\bin\\|\\obj\\' }
    foreach ($f in $cs) {
        $t = [System.IO.File]::ReadAllText($f.FullName)
        if ($t -match 'job-logs|job_log|systemlog') {
            W ''
            W ('--- ' + $f.FullName.Substring($RepoRoot.Length + 1))
            $lines = [System.IO.File]::ReadAllLines($f.FullName)
            for ($i = 0; $i -lt $lines.Count; $i++) {
                if ($lines[$i] -match 'job-logs|job_log|systemlog|MapGet|MapPost|RequireAuthorization') {
                    W ('    ' + ($i + 1) + ': ' + $lines[$i].Trim())
                }
            }
        }
    }
}

W ''
W '--- Serilog / log file configuration (where do systemlog_*.log files live?)'
foreach ($n in @('Program.cs', 'appsettings.json', 'appsettings.Development.json')) {
    $p = Join-Path $ApiRoot $n
    if (-not (Test-Path $p)) { continue }
    $lines = [System.IO.File]::ReadAllLines($p)
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match 'systemlog|joblog|Serilog|rollingInterval|WriteTo|logs') {
            W ('    ' + $n + ':' + ($i + 1) + '  ' + $lines[$i].Trim())
        }
    }
}

W ''
W '--- the RBAC matrix (a new admin endpoint must be registered, or it 403s)'
$mx = Join-Path $ApiRoot 'Security\PlantAccessControl.cs'
if (Test-Path $mx) {
    $lines = [System.IO.File]::ReadAllLines($mx)
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match '\("/') { W ('    ' + ($i + 1) + ': ' + $lines[$i].Trim()) }
    }
}

$utf8 = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($Out, $sb.ToString(), $utf8)

Write-Host ''
Write-Host ('Bundle written: ' + $Out) -ForegroundColor Green
Write-Host ('Full files included: ' + $included)
Write-Host ('Size: ' + [math]::Round((Get-Item $Out).Length / 1KB, 1) + ' KB')
Write-Host 'Nothing on disk was modified. Upload that file.'

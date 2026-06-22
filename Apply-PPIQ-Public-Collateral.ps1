<#
.SYNOPSIS
    Implements the PlantProcess IQ public-collateral closure package:
      - Real public website lead delivery to a configured inbox.
      - A reproducible 2–3 minute demo-video production pipeline.
      - A sharpened PlantProcess IQ flagship page with honest coming-soon stubs.

.DESCRIPTION
    This installer is idempotent and purpose-named. It:
      1. Creates a dedicated anonymous, rate-limited public lead-capture endpoint.
      2. Persists every lead, sends a real SMTP email, records delivery state,
         and returns UI success only after SMTP accepts the message.
      3. Replaces the website form with a same-origin production-safe client.
      4. Rebuilds the public product presentation around one strong PPIQ page.
      5. Marks MES, QES, Yard and Energy honestly as coming soon.
      6. Adds static honesty checks and Playwright tests for desktop/tablet/mobile.
      7. Installs an FFmpeg-based video builder and optionally renders the final MP4.
      8. Generates an evidence folder with logs, hashes and acceptance results.

    No SMTP password is written into the repository. Development secrets are stored
    through `dotnet user-secrets`; production values are supplied as environment
    variables in the server's gitignored deployment environment.

.EXAMPLE
    # Apply code and configure Gmail/Google Workspace SMTP in .NET user-secrets.
    $smtpPassword = Read-Host 'SMTP app password' -AsSecureString
    .\Apply-PPIQ-Public-Collateral.ps1 `
      -LeadInboxAddress 'you@yourdomain.com' `
      -SmtpHost 'smtp.gmail.com' `
      -SmtpPort 587 `
      -SmtpUser 'you@yourdomain.com' `
      -SmtpPassword $smtpPassword `
      -SmtpFromAddress 'you@yourdomain.com' `
      -RunBuildValidation

.EXAMPLE
    # Also render the final 2–3 minute video. When VideoPlanPath is omitted,
    # the script asks interactively for segment timestamps.
    .\Apply-PPIQ-Public-Collateral.ps1 `
      -LeadInboxAddress 'you@yourdomain.com' `
      -SmtpHost 'smtp.gmail.com' `
      -SmtpUser 'you@yourdomain.com' `
      -SmtpPassword (Read-Host 'SMTP app password' -AsSecureString) `
      -DryRunVideo 'C:\Recordings\PPIQ-dry-run.mp4' `
      -RunBuildValidation

.NOTES
    Target repository: C:\Workspace\PlantProcess-IQ
    Supported shell: Windows PowerShell 5.1+ and PowerShell 7+
#>

[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string]$RepoRoot = 'C:\Workspace\PlantProcess-IQ',

    [Parameter()]
    [string]$LeadInboxAddress = '',

    [Parameter()]
    [string]$SmtpHost = '',

    [Parameter()]
    [ValidateRange(1, 65535)]
    [int]$SmtpPort = 587,

    [Parameter()]
    [string]$SmtpUser = '',

    [Parameter()]
    [Security.SecureString]$SmtpPassword,

    [Parameter()]
    [string]$SmtpFromAddress = '',

    [Parameter()]
    [string]$SmtpFromName = 'PlantProcess IQ Website',

    [Parameter()]
    [bool]$SmtpEnableSsl = $true,

    [Parameter()]
    [ValidatePattern('^[0-9a-fA-F-]{36}$')]
    [string]$PublicTenantId = '00000000-0000-0000-0000-000000000001',

    [Parameter()]
    [string]$DryRunVideo = '',

    [Parameter()]
    [string]$VideoPlanPath = '',

    [Parameter()]
    [string]$NarrationAudio = '',

    [Parameter()]
    [string]$ApiBaseUrl = 'http://localhost:5063',

    [Parameter()]
    [string]$DatabaseConnectionString = '',

    [Parameter()]
    [switch]$RunDatabaseMigration,

    [Parameter()]
    [switch]$RunBuildValidation,

    [Parameter()]
    [switch]$RunLiveLeadProof,

    [Parameter()]
    [switch]$StartApiForProof,

    [Parameter()]
    [switch]$SkipInboxConfirmation,

    [Parameter()]
    [switch]$InstallMissingVideoTool,

    [Parameter()]
    [switch]$NonInteractive,

    [Parameter()]
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

# -----------------------------------------------------------------------------
# Constants and paths
# -----------------------------------------------------------------------------

$script:Utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$script:Timestamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$script:EvidenceRoot = Join-Path $RepoRoot "Documentation\Acceptance\PublicCollateral_$($script:Timestamp)"
$script:BackupRoot = Join-Path $RepoRoot ".public-collateral-backup\$($script:Timestamp)"
$script:LogsRoot = Join-Path $script:EvidenceRoot 'logs'
$script:Changes = New-Object System.Collections.Generic.List[string]
$script:Warnings = New-Object System.Collections.Generic.List[string]
$script:ApiProcess = $null
$script:SmtpFromAddress = $SmtpFromAddress
$script:SmtpPassword = $SmtpPassword

$BackendApiRoot = Join-Path $RepoRoot 'Backend\PlantProcess.Api'
$BackendTestsRoot = Join-Path $RepoRoot 'Backend\tests'
$DatabaseScriptsRoot = Join-Path $RepoRoot 'Backend\database\scripts'
$WebsiteRoot = Join-Path $RepoRoot 'Website\PlantProcess.Website'
$ProgramPath = Join-Path $BackendApiRoot 'Program.cs'
$ApiProjectPath = Join-Path $BackendApiRoot 'PlantProcess.Api.csproj'
$WebsitePackagePath = Join-Path $WebsiteRoot 'package.json'
$VideoDocsRoot = Join-Path $RepoRoot 'Documentation\DemoVideo'
$VideoBuilderPath = Join-Path $RepoRoot 'tools\media\Build-PlantProcessDemoVideo.ps1'

# -----------------------------------------------------------------------------
# Utility functions
# -----------------------------------------------------------------------------

function Write-Stage {
    param([Parameter(Mandatory)][string]$Message)
    Write-Host ''
    Write-Host ('=' * 88) -ForegroundColor DarkGray
    Write-Host $Message -ForegroundColor Cyan
    Write-Host ('=' * 88) -ForegroundColor DarkGray
}

function Write-Ok {
    param([Parameter(Mandatory)][string]$Message)
    Write-Host "[GREEN] $Message" -ForegroundColor Green
}

function Write-Warn {
    param([Parameter(Mandatory)][string]$Message)
    $script:Warnings.Add($Message) | Out-Null
    Write-Host "[YELLOW] $Message" -ForegroundColor Yellow
}

function Assert-Path {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter()][ValidateSet('Leaf','Container')][string]$Type = 'Leaf'
    )

    if ($Type -eq 'Leaf' -and -not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Required file not found: $Path"
    }

    if ($Type -eq 'Container' -and -not (Test-Path -LiteralPath $Path -PathType Container)) {
        throw "Required directory not found: $Path"
    }
}

function Ensure-Directory {
    param([Parameter(Mandatory)][string]$Path)
    if (-not (Test-Path -LiteralPath $Path -PathType Container)) {
        New-Item -ItemType Directory -Path $Path -Force | Out-Null
    }
}

function Backup-Path {
    param([Parameter(Mandatory)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return
    }

    $relative = [IO.Path]::GetFullPath($Path).Substring([IO.Path]::GetFullPath($RepoRoot).Length).TrimStart('\','/')
    $destination = Join-Path $script:BackupRoot $relative
    Ensure-Directory (Split-Path -Parent $destination)
    Copy-Item -LiteralPath $Path -Destination $destination -Force
}

function Write-TextFile {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][AllowEmptyString()][string]$Content
    )

    Ensure-Directory (Split-Path -Parent $Path)
    Backup-Path $Path

    $normalized = $Content -replace "`r?`n", "`r`n"
    [IO.File]::WriteAllText($Path, $normalized, $script:Utf8NoBom)
    $script:Changes.Add($Path) | Out-Null
    Write-Ok "Wrote $Path"
}

function Add-TextIfMissing {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Text,
        [Parameter(Mandatory)][string]$Identity
    )

    Assert-Path $Path
    $current = [IO.File]::ReadAllText($Path)
    if ($current.Contains($Identity)) {
        return
    }

    Backup-Path $Path
    $updated = $current.TrimEnd() + "`r`n" + $Text.Trim() + "`r`n"
    [IO.File]::WriteAllText($Path, $updated, $script:Utf8NoBom)
    $script:Changes.Add($Path) | Out-Null
    Write-Ok "Updated $Path"
}

function Replace-ExactlyOnce {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Search,
        [Parameter(Mandatory)][string]$Replacement,
        [Parameter(Mandatory)][string]$AlreadyAppliedMarker
    )

    Assert-Path $Path
    $content = [IO.File]::ReadAllText($Path)

    if ($content.Contains($AlreadyAppliedMarker)) {
        Write-Ok "Already patched: $Path"
        return
    }

    $first = $content.IndexOf($Search, [StringComparison]::Ordinal)
    if ($first -lt 0) {
        throw "Patch anchor not found in $Path. Expected: $Search"
    }

    $second = $content.IndexOf($Search, $first + $Search.Length, [StringComparison]::Ordinal)
    if ($second -ge 0) {
        throw "Patch anchor occurred more than once in $Path; refusing an ambiguous edit."
    }

    Backup-Path $Path
    $updated = $content.Substring(0, $first) + $Replacement + $content.Substring($first + $Search.Length)
    [IO.File]::WriteAllText($Path, $updated, $script:Utf8NoBom)
    $script:Changes.Add($Path) | Out-Null
    Write-Ok "Patched $Path"
}

function Invoke-Logged {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][scriptblock]$Action,
        [switch]$AllowFailure
    )

    Ensure-Directory $script:LogsRoot
    $safeName = ($Name -replace '[^a-zA-Z0-9._-]', '_')
    $logPath = Join-Path $script:LogsRoot "$safeName.log"

    Write-Host "[RUN] $Name" -ForegroundColor DarkCyan
    try {
        & $Action 2>&1 | Tee-Object -FilePath $logPath
        if ($LASTEXITCODE -ne 0 -and $null -ne $LASTEXITCODE) {
            throw "$Name exited with code $LASTEXITCODE"
        }
        Write-Ok "$Name completed"
    }
    catch {
        if ($AllowFailure) {
            Write-Warn "$Name failed: $($_.Exception.Message). See $logPath"
            return $false
        }
        throw
    }
    return $true
}

function ConvertFrom-SecureStringPlainText {
    param([Parameter(Mandatory)][Security.SecureString]$SecureValue)

    $ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($SecureValue)
    try {
        return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr)
    }
    finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr)
    }
}

function Test-EmailAddress {
    param([Parameter(Mandatory)][string]$Value)
    try {
        $mail = New-Object Net.Mail.MailAddress($Value)
        return $mail.Address -eq $Value.Trim()
    }
    catch {
        return $false
    }
}

function Wait-HttpReady {
    param(
        [Parameter(Mandatory)][string]$Url,
        [int]$TimeoutSeconds = 120
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        try {
            $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 5
            if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 500) {
                return $true
            }
        }
        catch {
            Start-Sleep -Seconds 2
        }
    } while ((Get-Date) -lt $deadline)

    return $false
}

function Update-PackageScripts {
    param([Parameter(Mandatory)][string]$Path)

    Assert-Path $Path
    Backup-Path $Path
    $package = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json

    $scripts = [ordered]@{}
    foreach ($property in $package.scripts.PSObject.Properties) {
        $scripts[$property.Name] = [string]$property.Value
    }

    $scripts['validate:public-site'] = 'node scripts/validate-public-site.mjs'
    $scripts['test:public-site'] = 'playwright test --config playwright.public-site.config.ts'
    $scripts['acceptance:public-site'] = 'npm run build && npm run validate:public-site && npm run test:public-site && npm run phase10:guard'

    $package.scripts = [pscustomobject]$scripts
    $json = $package | ConvertTo-Json -Depth 32
    [IO.File]::WriteAllText($Path, ($json -replace "`n", "`r`n") + "`r`n", $script:Utf8NoBom)
    $script:Changes.Add($Path) | Out-Null
    Write-Ok "Updated website package scripts"
}

function Configure-LeadCaptureUserSecrets {
    if ([string]::IsNullOrWhiteSpace($LeadInboxAddress) -and [string]::IsNullOrWhiteSpace($SmtpHost)) {
        Write-Warn 'SMTP settings were not supplied. Code will be installed, but real inbox delivery remains disabled until configuration is provided.'
        return
    }

    if (-not (Test-EmailAddress $LeadInboxAddress)) {
        throw "LeadInboxAddress is not a valid email address: $LeadInboxAddress"
    }
    if ([string]::IsNullOrWhiteSpace($SmtpHost)) {
        throw 'SmtpHost is required when LeadInboxAddress is provided.'
    }
    if ([string]::IsNullOrWhiteSpace($SmtpFromAddress)) {
        if (-not [string]::IsNullOrWhiteSpace($SmtpUser) -and (Test-EmailAddress $SmtpUser)) {
            $script:SmtpFromAddress = $SmtpUser
        }
        else {
            throw 'SmtpFromAddress is required when SmtpUser is empty or not an email address.'
        }
    }
    if (-not (Test-EmailAddress $script:SmtpFromAddress)) {
        throw "SmtpFromAddress is not a valid email address: $($script:SmtpFromAddress)"
    }

    if ($null -eq $SmtpPassword -and -not $NonInteractive -and -not [string]::IsNullOrWhiteSpace($SmtpUser)) {
        $script:SmtpPassword = Read-Host 'Enter the SMTP password/app-password (stored only in .NET user-secrets)' -AsSecureString
    }

    $plainPassword = ''
    if ($null -ne $script:SmtpPassword) {
        $plainPassword = ConvertFrom-SecureStringPlainText $script:SmtpPassword
    }

    $pairs = [ordered]@{
        'PublicLeadCapture:Enabled' = 'true'
        'PublicLeadCapture:TenantId' = $PublicTenantId
        'PublicLeadCapture:InboxAddress' = $LeadInboxAddress
        'PublicLeadCapture:SmtpHost' = $SmtpHost
        'PublicLeadCapture:SmtpPort' = [string]$SmtpPort
        'PublicLeadCapture:SmtpUsername' = $SmtpUser
        'PublicLeadCapture:SmtpPassword' = $plainPassword
        'PublicLeadCapture:SmtpFromAddress' = $script:SmtpFromAddress
        'PublicLeadCapture:SmtpFromName' = $SmtpFromName
        'PublicLeadCapture:SmtpEnableSsl' = $SmtpEnableSsl.ToString().ToLowerInvariant()
        'PublicLeadCapture:RequireEmailDelivery' = 'true'
        'PublicLeadCapture:MinimumHumanSubmitSeconds' = '2'
    }

    foreach ($pair in $pairs.GetEnumerator()) {
        $key = [string]$pair.Key
        $value = [string]$pair.Value
        & dotnet user-secrets set $key $value --project $ApiProjectPath | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet user-secrets failed while setting $key"
        }
    }

    $plainPassword = $null
    Write-Ok 'Stored local lead-delivery configuration in .NET user-secrets.'
}

function Write-ServerConfigurationTemplate {
    $path = Join-Path $RepoRoot 'deploy\compose\public-lead-capture.server.env.example'
    $content = @'
# Public PlantProcess IQ website lead delivery.
# Copy these keys into the server's gitignored deployment .env file.
# Never commit the real SMTP password.
PublicLeadCapture__Enabled=true
PublicLeadCapture__TenantId=00000000-0000-0000-0000-000000000001
PublicLeadCapture__InboxAddress=REPLACE_WITH_REAL_INBOX
PublicLeadCapture__SmtpHost=REPLACE_WITH_SMTP_HOST
PublicLeadCapture__SmtpPort=587
PublicLeadCapture__SmtpUsername=REPLACE_WITH_SMTP_USERNAME
PublicLeadCapture__SmtpPassword=REPLACE_WITH_SECRET
PublicLeadCapture__SmtpFromAddress=REPLACE_WITH_VERIFIED_FROM_ADDRESS
PublicLeadCapture__SmtpFromName=PlantProcess IQ Website
PublicLeadCapture__SmtpEnableSsl=true
PublicLeadCapture__RequireEmailDelivery=true
PublicLeadCapture__MinimumHumanSubmitSeconds=2

# The public website uses same-origin /api in production by default.
# Set this only when website and API are on different approved origins.
VITE_WEBSITE_API_BASE_URL=
'@
    Write-TextFile -Path $path -Content $content
}

function Parse-ConnectionString {
    param([Parameter(Mandatory)][string]$ConnectionString)

    $values = @{}
    foreach ($segment in $ConnectionString.Split(';')) {
        if ([string]::IsNullOrWhiteSpace($segment) -or -not $segment.Contains('=')) { continue }
        $parts = $segment.Split('=', 2)
        $values[$parts[0].Trim().ToLowerInvariant()] = $parts[1].Trim()
    }
    return $values
}

function Resolve-PsqlCommand {
    $command = Get-Command psql -ErrorAction SilentlyContinue
    if ($null -ne $command) { return $command.Source }

    $candidate = 'C:\Program Files\PostgreSQL\16\bin\psql.exe'
    if (Test-Path -LiteralPath $candidate) { return $candidate }

    throw 'psql was not found. Install PostgreSQL client tools or add psql to PATH.'
}

function Invoke-DatabaseMigration {
    $connectionString = $DatabaseConnectionString
    if ([string]::IsNullOrWhiteSpace($connectionString)) {
        $connectionString = $env:ConnectionStrings__PlantProcessDb
    }
    if ([string]::IsNullOrWhiteSpace($connectionString)) {
        $connectionString = $env:PLANTPROCESS_DB
    }
    if ([string]::IsNullOrWhiteSpace($connectionString)) {
        throw 'RunDatabaseMigration requires -DatabaseConnectionString, ConnectionStrings__PlantProcessDb, or PLANTPROCESS_DB. The installer intentionally does not run the repository-wide SQL glob.'
    }

    $values = Parse-ConnectionString $connectionString
    $hostName = if ($values.ContainsKey('host')) { $values['host'] } else { 'localhost' }
    $port = if ($values.ContainsKey('port')) { $values['port'] } else { '5432' }
    $database = if ($values.ContainsKey('database')) { $values['database'] } else { throw 'Database key missing from the connection string.' }
    $username = if ($values.ContainsKey('username')) { $values['username'] } elseif ($values.ContainsKey('user id')) { $values['user id'] } else { throw 'Username key missing from the connection string.' }
    $password = if ($values.ContainsKey('password')) { $values['password'] } else { '' }
    $psql = Resolve-PsqlCommand
    $migration = Join-Path $DatabaseScriptsRoot '605_public_lead_delivery_hardening.sql'

    $oldPassword = $env:PGPASSWORD
    $oldEncoding = $env:PGCLIENTENCODING
    try {
        $env:PGPASSWORD = $password
        $env:PGCLIENTENCODING = 'UTF8'
        Invoke-Logged -Name 'public-lead-database-migration' -Action {
            & $psql -h $hostName -p $port -U $username -d $database -v ON_ERROR_STOP=1 -f $migration
        }
    }
    finally {
        $env:PGPASSWORD = $oldPassword
        $env:PGCLIENTENCODING = $oldEncoding
    }
}

function Invoke-DatabaseLeadProof {
    param([Parameter(Mandatory)][string[]]$LeadIds)

    if ([string]::IsNullOrWhiteSpace($DatabaseConnectionString)) {
        Write-Warn 'DatabaseConnectionString was not supplied; skipping direct SQL row proof.'
        return
    }

    $psql = Get-Command psql -ErrorAction SilentlyContinue
    if ($null -eq $psql) {
        $candidate = 'C:\Program Files\PostgreSQL\16\bin\psql.exe'
        if (Test-Path -LiteralPath $candidate) {
            $psql = Get-Item $candidate
        }
        else {
            Write-Warn 'psql was not found; skipping direct SQL row proof.'
            return
        }
    }

    $values = Parse-ConnectionString $DatabaseConnectionString
    $hostName = if ($values.ContainsKey('host')) { $values['host'] } else { 'localhost' }
    $port = if ($values.ContainsKey('port')) { $values['port'] } else { '5432' }
    $database = if ($values.ContainsKey('database')) { $values['database'] } else { throw 'Database key missing from DatabaseConnectionString.' }
    $username = if ($values.ContainsKey('username')) { $values['username'] } elseif ($values.ContainsKey('user id')) { $values['user id'] } else { throw 'Username key missing from DatabaseConnectionString.' }
    $password = if ($values.ContainsKey('password')) { $values['password'] } else { '' }

    $idList = ($LeadIds | ForEach-Object { "'$_'::uuid" }) -join ','
    $query = "SELECT id, request_reference, email_delivery_status, email_delivered_at_utc FROM public.ppiq_lead_captures WHERE id IN ($idList) ORDER BY created_at_utc;"
    $oldPassword = $env:PGPASSWORD
    try {
        $env:PGPASSWORD = $password
        $output = & $psql.FullName -h $hostName -p $port -U $username -d $database -v ON_ERROR_STOP=1 -At -F '|' -c $query
        if ($LASTEXITCODE -ne 0) { throw 'psql lead proof failed.' }
        $proofPath = Join-Path $script:EvidenceRoot 'lead-database-proof.txt'
        [IO.File]::WriteAllLines($proofPath, [string[]]$output, $script:Utf8NoBom)
        if (@($output).Count -ne 2) {
            throw "Expected two persisted lead rows; found $(@($output).Count)."
        }
        if (@($output | Where-Object { $_ -notmatch '\|delivered\|' }).Count -gt 0) {
            throw 'At least one persisted lead row is not marked delivered.'
        }
        Write-Ok 'Verified two distinct persisted and delivered lead rows in PostgreSQL.'
    }
    finally {
        $env:PGPASSWORD = $oldPassword
    }
}

function Start-ApiForAcceptance {
    if (Wait-HttpReady -Url "$ApiBaseUrl/api/public/lead-capture/health" -TimeoutSeconds 3) {
        Write-Ok 'API is already reachable.'
        return
    }

    if (-not $StartApiForProof) {
        throw "API is not reachable at $ApiBaseUrl. Start it first, or rerun with -StartApiForProof."
    }

    $stdout = Join-Path $script:LogsRoot 'api-proof.stdout.log'
    $stderr = Join-Path $script:LogsRoot 'api-proof.stderr.log'
    $oldEnvironment = $env:ASPNETCORE_ENVIRONMENT
    $oldUrls = $env:ASPNETCORE_URLS
    try {
        $env:ASPNETCORE_ENVIRONMENT = 'Development'
        $env:ASPNETCORE_URLS = $ApiBaseUrl
        $script:ApiProcess = Start-Process -FilePath 'dotnet' `
            -ArgumentList @('run','--project',$ApiProjectPath,'--no-launch-profile') `
            -WorkingDirectory $RepoRoot `
            -RedirectStandardOutput $stdout `
            -RedirectStandardError $stderr `
            -PassThru
    }
    finally {
        $env:ASPNETCORE_ENVIRONMENT = $oldEnvironment
        $env:ASPNETCORE_URLS = $oldUrls
    }

    if (-not (Wait-HttpReady -Url "$ApiBaseUrl/api/public/lead-capture/health" -TimeoutSeconds 150)) {
        throw "API did not become ready. Review $stdout and $stderr"
    }

    Write-Ok 'Started API for live lead proof.'
}

function Invoke-LiveLeadProof {
    Start-ApiForAcceptance

    $health = Invoke-RestMethod -Method Get -Uri "$ApiBaseUrl/api/public/lead-capture/health" -TimeoutSec 20
    if (-not $health.enabled -or -not $health.configured) {
        throw 'Public lead capture is not enabled/configured. Supply the real SMTP parameters and rerun.'
    }

    $responses = New-Object System.Collections.Generic.List[object]
    for ($index = 1; $index -le 2; $index++) {
        $suffix = Get-Date -Format 'yyyyMMddHHmmssfff'
        $payload = [ordered]@{
            companyName = "PPIQ Acceptance Company $index"
            contactName = "Acceptance Contact $index"
            email = "acceptance+$suffix-$index@example.com"
            phone = ''
            jobTitle = 'Technical evaluator'
            country = 'Germany'
            plantType = 'Steel manufacturing plant'
            interestArea = 'Genealogy, quality evidence, value analysis'
            painPoints = 'Manual cross-system investigation and slow quality forensics.'
            preferredContact = 'Email'
            consentGiven = $true
            honeypot = ''
            sourcePage = '/product'
            formVersion = 'public-site-v1'
            formStartedAtUtc = (Get-Date).ToUniversalTime().AddSeconds(-5).ToString('o')
        }

        $response = Invoke-RestMethod `
            -Method Post `
            -Uri "$ApiBaseUrl/api/public/leads" `
            -ContentType 'application/json' `
            -Body ($payload | ConvertTo-Json -Depth 8) `
            -TimeoutSec 45

        if (-not $response.accepted -or $response.deliveryStatus -ne 'delivered') {
            throw "Lead $index was not delivered. Response: $($response | ConvertTo-Json -Depth 8 -Compress)"
        }

        $responses.Add($response) | Out-Null
        Start-Sleep -Seconds 1
    }

    if ($responses[0].leadId -eq $responses[1].leadId) {
        throw 'The second lead overwrote the first lead; lead IDs are identical.'
    }

    $proofPath = Join-Path $script:EvidenceRoot 'live-lead-proof.json'
    Write-TextFile -Path $proofPath -Content ($responses | ConvertTo-Json -Depth 12)
    Invoke-DatabaseLeadProof -LeadIds @([string]$responses[0].leadId, [string]$responses[1].leadId)

    if (-not $SkipInboxConfirmation -and -not $NonInteractive) {
        $answer = Read-Host "Confirm that BOTH lead emails arrived in $LeadInboxAddress within one minute [yes/no]"
        if ($answer -notmatch '^(?i:y|yes)$') {
            throw 'Inbox delivery was not confirmed. The acceptance gate remains red.'
        }
    }

    Write-Ok 'Live lead proof passed: two distinct submissions, delivery acknowledged, no overwrite.'
}

function Get-VideoPlanInteractively {
    param([Parameter(Mandatory)][string]$Destination)

    if ($NonInteractive) {
        throw 'VideoPlanPath is required in NonInteractive mode.'
    }

    Write-Host ''
    Write-Host 'Enter source-video timestamps as HH:MM:SS.mmm.' -ForegroundColor Cyan
    Write-Host 'Choose only clean segments. Total final duration must be 120–180 seconds.' -ForegroundColor Cyan

    $definitions = @(
        @{ label = 'Opening and workflow'; caption = 'Connect plant data → map → investigate → explain'; required = 'workflow' },
        @{ label = 'Genealogy'; caption = 'Bidirectional genealogy: heat → slab → coil → quality evidence'; required = 'genealogy' },
        @{ label = 'Transition coil'; caption = 'Blended provenance: transition-coil contribution shown transparently'; required = 'transition' },
        @{ label = 'Value evidence'; caption = 'Bounded value estimate with assumptions — projected, not guaranteed'; required = 'value' },
        @{ label = 'Assistant honesty and close'; caption = 'The assistant refuses uncited numbers and keeps evidence handles'; required = 'assistant' }
    )

    $segments = New-Object System.Collections.Generic.List[object]
    foreach ($definition in $definitions) {
        Write-Host "`n$($definition.label)" -ForegroundColor Yellow
        $start = Read-Host '  Start'
        $end = Read-Host '  End'
        $segments.Add([ordered]@{
            label = $definition.label
            start = $start
            end = $end
            caption = $definition.caption
            keyMoment = $definition.required
        }) | Out-Null
    }

    $plan = [ordered]@{
        title = 'PlantProcess IQ — evidence-grade manufacturing intelligence'
        outputFileName = 'PlantProcessIQ_Demo_2-3min.mp4'
        segments = $segments
    }
    Write-TextFile -Path $Destination -Content ($plan | ConvertTo-Json -Depth 12)
    return $Destination
}

function Install-VideoToolIfRequested {
    if (Get-Command ffmpeg -ErrorAction SilentlyContinue) { return }
    if (-not $InstallMissingVideoTool) { return }

    $winget = Get-Command winget -ErrorAction SilentlyContinue
    if ($null -eq $winget) {
        throw 'FFmpeg is missing and winget is unavailable. Install FFmpeg manually and rerun.'
    }

    Invoke-Logged -Name 'install-ffmpeg' -Action {
        & $winget.Source install --id Gyan.FFmpeg.Shared --exact --accept-package-agreements --accept-source-agreements
    }
}

function Render-DemoVideoIfRequested {
    if ([string]::IsNullOrWhiteSpace($DryRunVideo)) {
        Write-Warn 'DryRunVideo was not supplied. The professional video pipeline is installed, but the final MP4 was not rendered.'
        return
    }

    Assert-Path $DryRunVideo
    Install-VideoToolIfRequested
    if (-not (Get-Command ffmpeg -ErrorAction SilentlyContinue)) {
        throw 'FFmpeg is not available. Install it or rerun with -InstallMissingVideoTool.'
    }

    $resolvedPlan = $VideoPlanPath
    if ([string]::IsNullOrWhiteSpace($resolvedPlan)) {
        $resolvedPlan = Join-Path $VideoDocsRoot 'demo-video-plan.local.json'
        $resolvedPlan = Get-VideoPlanInteractively -Destination $resolvedPlan
    }
    Assert-Path $resolvedPlan

    $outputPath = Join-Path $VideoDocsRoot 'PlantProcessIQ_Demo_2-3min.mp4'
    $arguments = @(
        '-SourceVideo', $DryRunVideo,
        '-PlanPath', $resolvedPlan,
        '-OutputPath', $outputPath,
        '-EvidenceDirectory', $script:EvidenceRoot
    )
    if (-not [string]::IsNullOrWhiteSpace($NarrationAudio)) {
        Assert-Path $NarrationAudio
        $arguments += @('-NarrationAudio', $NarrationAudio)
    }

    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $VideoBuilderPath @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Video builder exited with code $LASTEXITCODE"
    }
    Write-Ok "Rendered final demo video: $outputPath"
}

function Write-EvidenceSummary {
    $changedRows = $script:Changes | Sort-Object -Unique | ForEach-Object {
        $relative = $_.Substring($RepoRoot.Length).TrimStart('\','/')
        $hash = if (Test-Path -LiteralPath $_ -PathType Leaf) { (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash } else { '' }
        "| ``$relative`` | ``$hash`` |"
    }

    $warningRows = if ($script:Warnings.Count -eq 0) {
        '- None.'
    }
    else {
        ($script:Warnings | ForEach-Object { "- $_" }) -join "`r`n"
    }

    $summary = @"
# PlantProcess IQ — Public Collateral Acceptance Evidence

Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss K')

## Scope

- Real public website lead capture with database persistence and SMTP inbox delivery.
- Success UI shown only after the backend records SMTP delivery.
- Rate-limited anonymous capture endpoint; authenticated administration stays separate.
- Sharpened PlantProcess IQ flagship page.
- Honest coming-soon stubs for MES, QES, Yard and Energy.
- Responsive and honesty-lint validation.
- Reproducible FFmpeg demo-video pipeline and optional final render.

## Changed files

| File | SHA-256 |
|---|---|
$($changedRows -join "`r`n")

## Warnings / remaining manual gates

$warningRows

## Acceptance commands

```powershell
cd '$RepoRoot'
dotnet build .\Backend\PlantProcess.Api\PlantProcess.Api.csproj
cd .\Website\PlantProcess.Website
npm run acceptance:public-site
```

## Production configuration

Use ``deploy/compose/public-lead-capture.server.env.example`` as a template and place real values only in the server's gitignored environment.
"@

    Write-TextFile -Path (Join-Path $script:EvidenceRoot 'ACCEPTANCE_SUMMARY.md') -Content $summary
}

function Stop-StartedApi {
    if ($null -ne $script:ApiProcess -and -not $script:ApiProcess.HasExited) {
        try {
            Stop-Process -Id $script:ApiProcess.Id -Force -ErrorAction Stop
            Write-Ok "Stopped temporary API process PID=$($script:ApiProcess.Id)"
        }
        catch {
            Write-Warn "Could not stop temporary API process: $($_.Exception.Message)"
        }
    }
}

# -----------------------------------------------------------------------------
# Preflight
# -----------------------------------------------------------------------------

Write-Stage 'Preflight and backup preparation'
Assert-Path $RepoRoot -Type Container
Assert-Path $BackendApiRoot -Type Container
Assert-Path $WebsiteRoot -Type Container
Assert-Path $ProgramPath
Assert-Path $ApiProjectPath
Assert-Path $WebsitePackagePath

Ensure-Directory $script:EvidenceRoot
Ensure-Directory $script:BackupRoot
Ensure-Directory $script:LogsRoot
Ensure-Directory $VideoDocsRoot
Ensure-Directory (Split-Path -Parent $VideoBuilderPath)

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'dotnet is required but was not found in PATH.'
}
if (-not (Get-Command node -ErrorAction SilentlyContinue)) {
    throw 'Node.js is required but was not found in PATH.'
}
if (-not (Get-Command npm -ErrorAction SilentlyContinue)) {
    throw 'npm is required but was not found in PATH.'
}

Write-Ok "Evidence folder: $script:EvidenceRoot"
Write-Ok "Backup folder: $script:BackupRoot"

# -----------------------------------------------------------------------------
# Backend: public lead capture
# -----------------------------------------------------------------------------

Write-Stage 'Implementing public, rate-limited, real-inbox lead delivery'

$leadOptions = @'
namespace PlantProcess.Api.LeadCapture;

public sealed class PublicLeadCaptureOptions
{
    public const string SectionName = "PublicLeadCapture";

    public bool Enabled { get; set; }
    public Guid TenantId { get; set; } = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public string InboxAddress { get; set; } = string.Empty;
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public string SmtpUsername { get; set; } = string.Empty;
    public string SmtpPassword { get; set; } = string.Empty;
    public string SmtpFromAddress { get; set; } = string.Empty;
    public string SmtpFromName { get; set; } = "PlantProcess IQ Website";
    public bool SmtpEnableSsl { get; set; } = true;
    public bool RequireEmailDelivery { get; set; } = true;
    public int SmtpTimeoutSeconds { get; set; } = 30;
    public int MinimumHumanSubmitSeconds { get; set; } = 2;

    public IReadOnlyList<string> GetConfigurationErrors()
    {
        var errors = new List<string>();

        if (!Enabled)
            errors.Add("Public lead capture is disabled.");

        if (TenantId == Guid.Empty)
            errors.Add("TenantId must be configured.");

        if (string.IsNullOrWhiteSpace(InboxAddress))
            errors.Add("InboxAddress must be configured.");

        if (RequireEmailDelivery && string.IsNullOrWhiteSpace(SmtpHost))
            errors.Add("SmtpHost must be configured when email delivery is required.");

        if (RequireEmailDelivery && string.IsNullOrWhiteSpace(SmtpFromAddress))
            errors.Add("SmtpFromAddress must be configured when email delivery is required.");

        if (SmtpPort is < 1 or > 65535)
            errors.Add("SmtpPort must be between 1 and 65535.");

        if (SmtpTimeoutSeconds is < 5 or > 120)
            errors.Add("SmtpTimeoutSeconds must be between 5 and 120.");

        if (MinimumHumanSubmitSeconds is < 0 or > 30)
            errors.Add("MinimumHumanSubmitSeconds must be between 0 and 30.");

        return errors;
    }
}
'@
Write-TextFile -Path (Join-Path $BackendApiRoot 'LeadCapture\PublicLeadCaptureOptions.cs') -Content $leadOptions

$leadContracts = @'
namespace PlantProcess.Api.LeadCapture;

public sealed record PublicLeadCaptureRequest(
    string CompanyName,
    string ContactName,
    string Email,
    string? Phone,
    string? JobTitle,
    string? Country,
    string? PlantType,
    string? InterestArea,
    string? PainPoints,
    string? PreferredContact,
    bool ConsentGiven,
    string? Honeypot,
    string? SourcePage,
    string? FormVersion,
    DateTimeOffset? FormStartedAtUtc);

public sealed record PublicLeadCaptureResponse(
    bool Accepted,
    Guid LeadId,
    string RequestReference,
    string DeliveryStatus,
    DateTimeOffset DeliveredAtUtc);

public sealed record LeadEmailMessage(
    Guid LeadId,
    string RequestReference,
    PublicLeadCaptureRequest Lead,
    decimal FitScore,
    DateTimeOffset ReceivedAtUtc);

public sealed record LeadEmailDeliveryResult(
    bool Delivered,
    DateTimeOffset? DeliveredAtUtc,
    string? FailureCode,
    string? SafeFailureMessage);
'@
Write-TextFile -Path (Join-Path $BackendApiRoot 'LeadCapture\PublicLeadCaptureContracts.cs') -Content $leadContracts

$leadSender = @'
using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace PlantProcess.Api.LeadCapture;

public interface ILeadEmailSender
{
    Task<LeadEmailDeliveryResult> SendAsync(LeadEmailMessage message, CancellationToken cancellationToken);
}

public sealed class SmtpLeadEmailSender(
    IOptionsMonitor<PublicLeadCaptureOptions> optionsMonitor,
    ILogger<SmtpLeadEmailSender> logger) : ILeadEmailSender
{
    public async Task<LeadEmailDeliveryResult> SendAsync(
        LeadEmailMessage message,
        CancellationToken cancellationToken)
    {
        var options = optionsMonitor.CurrentValue;
        var configurationErrors = options.GetConfigurationErrors();
        if (configurationErrors.Count > 0)
        {
            return new LeadEmailDeliveryResult(
                false,
                null,
                "smtp_not_configured",
                "Lead email delivery is not configured.");
        }

        try
        {
            using var email = new MailMessage
            {
                From = new MailAddress(options.SmtpFromAddress, options.SmtpFromName),
                Subject = BuildSubject(message),
                Body = BuildBody(message),
                IsBodyHtml = false,
                BodyEncoding = System.Text.Encoding.UTF8,
                SubjectEncoding = System.Text.Encoding.UTF8
            };

            email.To.Add(new MailAddress(options.InboxAddress));
            email.ReplyToList.Add(new MailAddress(message.Lead.Email.Trim()));

            using var client = new SmtpClient(options.SmtpHost, options.SmtpPort)
            {
                EnableSsl = options.SmtpEnableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Timeout = checked(options.SmtpTimeoutSeconds * 1000)
            };

            if (!string.IsNullOrWhiteSpace(options.SmtpUsername))
                client.Credentials = new NetworkCredential(options.SmtpUsername, options.SmtpPassword);

            await client.SendMailAsync(email, cancellationToken);
            var deliveredAtUtc = DateTimeOffset.UtcNow;

            logger.LogInformation(
                "Website lead email accepted by SMTP. LeadId={LeadId} Reference={Reference}",
                message.LeadId,
                message.RequestReference);

            return new LeadEmailDeliveryResult(true, deliveredAtUtc, null, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Website lead SMTP delivery failed. LeadId={LeadId} Reference={Reference}",
                message.LeadId,
                message.RequestReference);

            return new LeadEmailDeliveryResult(
                false,
                null,
                exception.GetType().Name,
                "The lead was stored, but inbox delivery was not confirmed. Please retry.");
        }
    }

    private static string BuildSubject(LeadEmailMessage message)
    {
        var company = SanitizeHeader(message.Lead.CompanyName, 100);
        return $"PlantProcess IQ demo request — {company} — {message.RequestReference}";
    }

    private static string BuildBody(LeadEmailMessage message)
    {
        var lead = message.Lead;
        return string.Join(
            Environment.NewLine,
            [
                "PlantProcess IQ website lead",
                "",
                $"Reference: {message.RequestReference}",
                $"Lead ID: {message.LeadId}",
                $"Received UTC: {message.ReceivedAtUtc:O}",
                $"Fit score: {message.FitScore:P0}",
                "",
                $"Contact: {Clean(lead.ContactName)}",
                $"Company: {Clean(lead.CompanyName)}",
                $"Email: {Clean(lead.Email)}",
                $"Phone: {Clean(lead.Phone)}",
                $"Job title: {Clean(lead.JobTitle)}",
                $"Country: {Clean(lead.Country)}",
                $"Plant / industry: {Clean(lead.PlantType)}",
                $"Interest / source systems: {Clean(lead.InterestArea)}",
                $"Preferred contact: {Clean(lead.PreferredContact)}",
                $"Source page: {Clean(lead.SourcePage)}",
                $"Form version: {Clean(lead.FormVersion)}",
                "",
                "Pain points / message:",
                Clean(lead.PainPoints),
                "",
                "Consent: Yes — the visitor explicitly agreed to be contacted.",
                "",
                "This message was delivered by the PlantProcess IQ public lead-capture service."
            ]);
    }

    private static string SanitizeHeader(string? value, int maximumLength)
    {
        var clean = Clean(value).Replace("\r", " ").Replace("\n", " ");
        return clean.Length <= maximumLength ? clean : clean[..maximumLength];
    }

    private static string Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();
    }
}
'@
Write-TextFile -Path (Join-Path $BackendApiRoot 'LeadCapture\SmtpLeadEmailSender.cs') -Content $leadSender

$leadServiceRegistration = @'
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace PlantProcess.Api.LeadCapture;

public static class PublicLeadCaptureServiceCollectionExtensions
{
    public const string RateLimitPolicyName = "public-lead-capture";

    public static IServiceCollection AddPublicLeadCapture(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<PublicLeadCaptureOptions>(
            configuration.GetSection(PublicLeadCaptureOptions.SectionName));

        services.AddSingleton<ILeadEmailSender, SmtpLeadEmailSender>();

        services.AddRateLimiter(options =>
        {
            options.AddPolicy(RateLimitPolicyName, httpContext =>
                RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(10),
                        SegmentsPerWindow = 5,
                        QueueLimit = 0,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        AutoReplenishment = true
                    }));
        });

        return services;
    }
}
'@
Write-TextFile -Path (Join-Path $BackendApiRoot 'LeadCapture\PublicLeadCaptureServiceCollectionExtensions.cs') -Content $leadServiceRegistration

$leadEndpoints = @'
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Npgsql;

namespace PlantProcess.Api.LeadCapture;

public static partial class PublicLeadCaptureEndpoints
{
    private const int MaximumShortTextLength = 250;
    private const int MaximumLongTextLength = 4000;

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase)]
    private static partial Regex EmailRegex();

    public static IEndpointRouteBuilder MapPublicLeadCaptureEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/public")
            .WithTags("Public Lead Capture");

        group.MapGet("/lead-capture/health", (
            IOptionsMonitor<PublicLeadCaptureOptions> optionsMonitor) =>
        {
            var options = optionsMonitor.CurrentValue;
            return Results.Ok(new
            {
                enabled = options.Enabled,
                configured = options.GetConfigurationErrors().Count == 0,
                databasePersistence = true,
                emailDeliveryRequired = options.RequireEmailDelivery,
                successRequiresDelivery = true,
                rateLimited = true
            });
        })
        .AllowAnonymous()
        .WithName("PublicLeadCaptureHealth");

        group.MapPost("/leads", CaptureLeadAsync)
            .AllowAnonymous()
            .RequireRateLimiting(PublicLeadCaptureServiceCollectionExtensions.RateLimitPolicyName)
            .WithName("CapturePublicWebsiteLead")
            .Produces<PublicLeadCaptureResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return app;
    }

    private static async Task<IResult> CaptureLeadAsync(
        [FromBody] PublicLeadCaptureRequest request,
        HttpContext httpContext,
        NpgsqlDataSource dataSource,
        ILeadEmailSender emailSender,
        IOptionsMonitor<PublicLeadCaptureOptions> optionsMonitor,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("PublicLeadCapture");
        var options = optionsMonitor.CurrentValue;
        var configurationErrors = options.GetConfigurationErrors();

        if (configurationErrors.Count > 0)
        {
            logger.LogWarning(
                "Public lead capture rejected because configuration is incomplete. ErrorCount={ErrorCount}",
                configurationErrors.Count);

            return Results.Problem(
                title: "Lead delivery is temporarily unavailable.",
                detail: "Please email info@plantprocessiq.com while the delivery channel is being restored.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var validationErrors = Validate(request, options, DateTimeOffset.UtcNow);
        if (validationErrors.Count > 0)
            return Results.ValidationProblem(validationErrors);

        var receivedAtUtc = DateTimeOffset.UtcNow;
        var requestReference = $"PPIQ-{receivedAtUtc:yyyyMMdd}-{RandomNumberGenerator.GetHexString(8)}";
        var fitScore = ComputeFitScore(request);
        var clientIpHash = Hash(httpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty);
        var userAgentHash = Hash(httpContext.Request.Headers.UserAgent.ToString());

        Guid leadId;
        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        {
            await SetTenantAsync(connection, options.TenantId, cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

            leadId = await InsertLeadAsync(
                connection,
                transaction,
                options.TenantId,
                request,
                requestReference,
                fitScore,
                clientIpHash,
                userAgentHash,
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }

        var emailMessage = new LeadEmailMessage(
            leadId,
            requestReference,
            request,
            fitScore,
            receivedAtUtc);

        var delivery = await emailSender.SendAsync(emailMessage, cancellationToken);
        await RecordDeliveryAsync(
            dataSource,
            options.TenantId,
            leadId,
            delivery,
            cancellationToken);

        if (!delivery.Delivered || delivery.DeliveredAtUtc is null)
        {
            return Results.Json(
                new
                {
                    accepted = false,
                    leadId,
                    requestReference,
                    stored = true,
                    deliveryStatus = "failed",
                    message = delivery.SafeFailureMessage ?? "Inbox delivery was not confirmed."
                },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        return Results.Created(
            $"/api/public/leads/{leadId}",
            new PublicLeadCaptureResponse(
                true,
                leadId,
                requestReference,
                "delivered",
                delivery.DeliveredAtUtc.Value));
    }

    private static Dictionary<string, string[]> Validate(
        PublicLeadCaptureRequest request,
        PublicLeadCaptureOptions options,
        DateTimeOffset nowUtc)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        AddRequired(errors, nameof(request.ContactName), request.ContactName, "Your name is required.");
        AddRequired(errors, nameof(request.CompanyName), request.CompanyName, "Company is required.");
        AddRequired(errors, nameof(request.Email), request.Email, "A valid work email is required.");
        AddRequired(errors, nameof(request.PlantType), request.PlantType, "Plant / industry type is required.");
        AddRequired(errors, nameof(request.InterestArea), request.InterestArea, "Source systems / interest area is required.");
        AddRequired(errors, nameof(request.PainPoints), request.PainPoints, "The main plant-data pain point is required.");

        if (!string.IsNullOrWhiteSpace(request.Email) && !EmailRegex().IsMatch(request.Email.Trim()))
            Add(errors, nameof(request.Email), "A valid work email is required.");

        if (!request.ConsentGiven)
            Add(errors, nameof(request.ConsentGiven), "Consent is required before we can contact you.");

        if (!string.IsNullOrWhiteSpace(request.Honeypot))
            Add(errors, nameof(request.Honeypot), "Submission rejected.");

        if (request.FormStartedAtUtc is not null)
        {
            var elapsed = nowUtc - request.FormStartedAtUtc.Value;
            if (elapsed < TimeSpan.FromSeconds(options.MinimumHumanSubmitSeconds))
                Add(errors, nameof(request.FormStartedAtUtc), "Please review the form before submitting.");
        }

        CheckLength(errors, nameof(request.CompanyName), request.CompanyName, MaximumShortTextLength);
        CheckLength(errors, nameof(request.ContactName), request.ContactName, MaximumShortTextLength);
        CheckLength(errors, nameof(request.Email), request.Email, MaximumShortTextLength);
        CheckLength(errors, nameof(request.Phone), request.Phone, MaximumShortTextLength);
        CheckLength(errors, nameof(request.JobTitle), request.JobTitle, MaximumShortTextLength);
        CheckLength(errors, nameof(request.Country), request.Country, MaximumShortTextLength);
        CheckLength(errors, nameof(request.PlantType), request.PlantType, MaximumShortTextLength);
        CheckLength(errors, nameof(request.InterestArea), request.InterestArea, 1000);
        CheckLength(errors, nameof(request.PainPoints), request.PainPoints, MaximumLongTextLength);
        CheckLength(errors, nameof(request.PreferredContact), request.PreferredContact, MaximumShortTextLength);
        CheckLength(errors, nameof(request.SourcePage), request.SourcePage, MaximumShortTextLength);
        CheckLength(errors, nameof(request.FormVersion), request.FormVersion, 100);

        return errors.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<Guid> InsertLeadAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid tenantId,
        PublicLeadCaptureRequest request,
        string requestReference,
        decimal fitScore,
        string clientIpHash,
        string userAgentHash,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO public.ppiq_lead_captures
            (
                tenant_id,
                source,
                company_name,
                contact_name,
                email,
                phone,
                job_title,
                country,
                plant_type,
                interest_area,
                pain_points,
                preferred_contact,
                consent_given,
                gdpr_consent_text,
                honeypot,
                spam_score,
                fit_score,
                status,
                client_ip_hash,
                user_agent_hash,
                request_reference,
                email_delivery_status,
                source_page,
                form_version
            )
            VALUES
            (
                @tenant_id,
                'website',
                @company_name,
                @contact_name,
                @email,
                @phone,
                @job_title,
                @country,
                @plant_type,
                @interest_area,
                @pain_points,
                @preferred_contact,
                true,
                @gdpr_consent_text,
                NULL,
                0,
                @fit_score,
                'new',
                @client_ip_hash,
                @user_agent_hash,
                @request_reference,
                'pending',
                @source_page,
                @form_version
            )
            RETURNING id
            """;

        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("company_name", CleanRequired(request.CompanyName));
        command.Parameters.AddWithValue("contact_name", CleanRequired(request.ContactName));
        command.Parameters.AddWithValue("email", CleanRequired(request.Email).ToLowerInvariant());
        command.Parameters.AddWithValue("phone", DbText(request.Phone));
        command.Parameters.AddWithValue("job_title", DbText(request.JobTitle));
        command.Parameters.AddWithValue("country", DbText(request.Country));
        command.Parameters.AddWithValue("plant_type", DbText(request.PlantType));
        command.Parameters.AddWithValue("interest_area", DbText(request.InterestArea));
        command.Parameters.AddWithValue("pain_points", DbText(request.PainPoints));
        command.Parameters.AddWithValue("preferred_contact", DbText(request.PreferredContact));
        command.Parameters.AddWithValue("gdpr_consent_text", "Visitor explicitly consented to be contacted about PlantProcess IQ and a data diagnostic.");
        command.Parameters.AddWithValue("fit_score", fitScore);
        command.Parameters.AddWithValue("client_ip_hash", clientIpHash);
        command.Parameters.AddWithValue("user_agent_hash", userAgentHash);
        command.Parameters.AddWithValue("request_reference", requestReference);
        command.Parameters.AddWithValue("source_page", DbText(request.SourcePage));
        command.Parameters.AddWithValue("form_version", DbText(request.FormVersion));

        return (Guid)(await command.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("The database did not return a lead ID."));
    }

    private static async Task RecordDeliveryAsync(
        NpgsqlDataSource dataSource,
        Guid tenantId,
        Guid leadId,
        LeadEmailDeliveryResult delivery,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await SetTenantAsync(connection, tenantId, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE public.ppiq_lead_captures
            SET
                email_delivery_status = @delivery_status,
                email_delivered_at_utc = @delivered_at_utc,
                email_failure_reason = @failure_reason,
                delivery_attempt_count = delivery_attempt_count + 1,
                updated_at_utc = now()
            WHERE tenant_id = @tenant_id
              AND id = @lead_id
            """;

        command.Parameters.AddWithValue("delivery_status", delivery.Delivered ? "delivered" : "failed");
        command.Parameters.AddWithValue("delivered_at_utc", delivery.DeliveredAtUtc is null ? DBNull.Value : delivery.DeliveredAtUtc.Value.UtcDateTime);
        command.Parameters.AddWithValue("failure_reason", delivery.SafeFailureMessage is null ? DBNull.Value : delivery.SafeFailureMessage);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("lead_id", leadId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static decimal ComputeFitScore(PublicLeadCaptureRequest request)
    {
        var score = 0.25m;
        var text = $"{request.PlantType} {request.InterestArea} {request.PainPoints}".ToLowerInvariant();

        if (text.Contains("steel") || text.Contains("manufacturing") || text.Contains("plant"))
            score += 0.25m;
        if (text.Contains("quality") || text.Contains("defect") || text.Contains("genealogy"))
            score += 0.25m;
        if (text.Contains("historian") || text.Contains("mes") || text.Contains("qms") || text.Contains("oracle"))
            score += 0.15m;
        if (!string.IsNullOrWhiteSpace(request.Phone))
            score += 0.10m;

        return Math.Min(score, 1m);
    }

    private static async Task SetTenantAsync(
        NpgsqlConnection connection,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT set_config('app.current_tenant', @tenant_id, false)";
        command.Parameters.AddWithValue("tenant_id", tenantId.ToString());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddRequired(
        Dictionary<string, List<string>> errors,
        string key,
        string? value,
        string message)
    {
        if (string.IsNullOrWhiteSpace(value))
            Add(errors, key, message);
    }

    private static void CheckLength(
        Dictionary<string, List<string>> errors,
        string key,
        string? value,
        int maximumLength)
    {
        if (value?.Length > maximumLength)
            Add(errors, key, $"Maximum length is {maximumLength} characters.");
    }

    private static void Add(Dictionary<string, List<string>> errors, string key, string message)
    {
        if (!errors.TryGetValue(key, out var list))
        {
            list = [];
            errors[key] = list;
        }
        list.Add(message);
    }

    private static string CleanRequired(string value) => value.Trim();

    private static object DbText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
'@
Write-TextFile -Path (Join-Path $BackendApiRoot 'LeadCapture\PublicLeadCaptureEndpoints.cs') -Content $leadEndpoints

$leadSql = @'
-- Public website lead-delivery hardening.
-- Adds durable request identity and real inbox delivery state to the existing lead table.
-- Idempotent and safe to run after 600_v5_p11_outbound_notifications_leads.sql.

\set ON_ERROR_STOP on

BEGIN;

DO $$
BEGIN
    IF to_regclass('public.ppiq_lead_captures') IS NULL THEN
        RAISE EXCEPTION 'public.ppiq_lead_captures is missing. Apply the outbound lead-system foundation first.';
    END IF;
END $$;

ALTER TABLE public.ppiq_lead_captures
    ADD COLUMN IF NOT EXISTS request_reference text NULL,
    ADD COLUMN IF NOT EXISTS email_delivery_status text NOT NULL DEFAULT 'pending',
    ADD COLUMN IF NOT EXISTS email_delivered_at_utc timestamptz NULL,
    ADD COLUMN IF NOT EXISTS email_failure_reason text NULL,
    ADD COLUMN IF NOT EXISTS delivery_attempt_count integer NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS source_page text NULL,
    ADD COLUMN IF NOT EXISTS form_version text NULL;

UPDATE public.ppiq_lead_captures
SET request_reference = 'LEGACY-' || upper(substr(replace(id::text, '-', ''), 1, 16))
WHERE request_reference IS NULL OR btrim(request_reference) = '';

ALTER TABLE public.ppiq_lead_captures
    ALTER COLUMN request_reference SET NOT NULL;

CREATE UNIQUE INDEX IF NOT EXISTS ux_ppiq_lead_request_reference
    ON public.ppiq_lead_captures(request_reference);

CREATE INDEX IF NOT EXISTS ix_ppiq_lead_delivery_status
    ON public.ppiq_lead_captures(tenant_id, email_delivery_status, created_at_utc DESC);

DO $$
BEGIN
    IF NOT EXISTS
    (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'ck_ppiq_lead_email_delivery_status'
          AND conrelid = 'public.ppiq_lead_captures'::regclass
    ) THEN
        ALTER TABLE public.ppiq_lead_captures
            ADD CONSTRAINT ck_ppiq_lead_email_delivery_status
            CHECK (email_delivery_status IN ('pending', 'delivered', 'failed', 'suppressed'));
    END IF;
END $$;

COMMENT ON COLUMN public.ppiq_lead_captures.request_reference
    IS 'Public-safe immutable reference returned to the website after capture.';
COMMENT ON COLUMN public.ppiq_lead_captures.email_delivery_status
    IS 'SMTP delivery state; website success is shown only for delivered.';

COMMIT;
'@
Write-TextFile -Path (Join-Path $DatabaseScriptsRoot '605_public_lead_delivery_hardening.sql') -Content $leadSql

# Register services immediately before WebApplication construction.
$serviceRegistrationLine = 'PlantProcess.Api.LeadCapture.PublicLeadCaptureServiceCollectionExtensions.AddPublicLeadCapture(builder.Services, builder.Configuration);'
Replace-ExactlyOnce `
    -Path $ProgramPath `
    -Search 'var app = builder.Build();' `
    -Replacement "$serviceRegistrationLine`r`n`r`nvar app = builder.Build();" `
    -AlreadyAppliedMarker $serviceRegistrationLine

# Map the public endpoint beside the existing authenticated outbound system.
$mappingLine = 'PlantProcess.Api.LeadCapture.PublicLeadCaptureEndpoints.MapPublicLeadCaptureEndpoints(app);'
Replace-ExactlyOnce `
    -Path $ProgramPath `
    -Search 'app.MapV5OutboundLeadSystemEndpoints();' `
    -Replacement "app.MapV5OutboundLeadSystemEndpoints();`r`n`r`n$mappingLine" `
    -AlreadyAppliedMarker $mappingLine

Write-ServerConfigurationTemplate
Configure-LeadCaptureUserSecrets

# -----------------------------------------------------------------------------
# Website: form and flagship product page
# -----------------------------------------------------------------------------

Write-Stage 'Sharpening the public website and wiring delivery-confirmed CTA behavior'

$requestDemoForm = @'
import { useMemo, useRef, useState, type FormEvent } from "react";
import { requestDemoMail } from "../../content/phase1WebsiteProof";

type FormState = {
  name: string;
  company: string;
  email: string;
  role: string;
  plantType: string;
  sourceSystems: string;
  pain: string;
  timeline: string;
  message: string;
  consentGiven: boolean;
  honeypot: string;
};

type DeliveredLead = {
  leadId: string;
  requestReference: string;
  deliveryStatus: "delivered";
  deliveredAtUtc: string;
};

type ApiErrorBody = {
  title?: string;
  detail?: string;
  message?: string;
  errors?: Record<string, string[]>;
};

const initialState: FormState = {
  name: "",
  company: "",
  email: "",
  role: "",
  plantType: "",
  sourceSystems: "",
  pain: "",
  timeline: "",
  message: "",
  consentGiven: false,
  honeypot: "",
};

function resolveApiBaseUrl() {
  const configured = (
    import.meta.env.VITE_WEBSITE_API_BASE_URL ??
    import.meta.env.VITE_API_BASE_URL ??
    ""
  ).trim().replace(/\/+$/, "");

  if (configured) return configured;
  return import.meta.env.DEV ? "http://localhost:5063" : "";
}

const websiteApiBaseUrl = resolveApiBaseUrl();

function encode(value: string) {
  return encodeURIComponent(value);
}

function validate(form: FormState) {
  const errors: Partial<Record<keyof FormState, string>> = {};

  if (!form.name.trim()) errors.name = "Name is required.";
  if (!form.company.trim()) errors.company = "Company is required.";
  if (!/^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(form.email)) errors.email = "Valid work email is required.";
  if (!form.plantType.trim()) errors.plantType = "Plant / industry type is required.";
  if (!form.sourceSystems.trim()) errors.sourceSystems = "Source systems are required.";
  if (!form.pain.trim()) errors.pain = "Main pain point is required.";
  if (!form.consentGiven) errors.consentGiven = "Consent is required.";
  if (form.honeypot.trim()) errors.honeypot = "Submission rejected.";

  return errors;
}

function getApiErrorMessage(body: ApiErrorBody | null, status: number) {
  if (body?.errors) {
    const first = Object.values(body.errors).flat()[0];
    if (first) return first;
  }
  return body?.message ?? body?.detail ?? body?.title ?? `Delivery failed (${status}).`;
}

export function RequestDemoForm() {
  const [form, setForm] = useState<FormState>(initialState);
  const [errors, setErrors] = useState<Partial<Record<keyof FormState, string>>>({});
  const [deliveredLead, setDeliveredLead] = useState<DeliveredLead | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [submitMessage, setSubmitMessage] = useState("");
  const [submitKind, setSubmitKind] = useState<"idle" | "working" | "error" | "success">("idle");
  const formStartedAtUtc = useRef(new Date().toISOString());

  const mailtoHref = useMemo(() => {
    const subject = `PlantProcess IQ demo request - ${form.company || form.name || "New inquiry"}`;
    const body = [
      "PlantProcess IQ demo request",
      "",
      `Name: ${form.name}`,
      `Company: ${form.company}`,
      `Email: ${form.email}`,
      `Role: ${form.role}`,
      `Plant / industry type: ${form.plantType}`,
      `Source systems: ${form.sourceSystems}`,
      `Main pain: ${form.pain}`,
      `Timeline: ${form.timeline}`,
      "",
      "Message:",
      form.message,
    ].join("\n");

    return `mailto:${requestDemoMail}?subject=${encode(subject)}&body=${encode(body)}`;
  }, [form]);

  function patch<K extends keyof FormState>(key: K, value: FormState[K]) {
    setForm((current) => ({ ...current, [key]: value }));
    setErrors((current) => ({ ...current, [key]: undefined }));
    if (submitKind === "error") {
      setSubmitKind("idle");
      setSubmitMessage("");
    }
  }

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const nextErrors = validate(form);
    setErrors(nextErrors);
    setDeliveredLead(null);

    if (Object.keys(nextErrors).length > 0) return;

    setIsSubmitting(true);
    setSubmitKind("working");
    setSubmitMessage("Securely storing your request and confirming inbox delivery...");

    const controller = new AbortController();
    const timeout = window.setTimeout(() => controller.abort(), 20_000);

    try {
      const response = await fetch(`${websiteApiBaseUrl}/api/public/leads`, {
        method: "POST",
        credentials: "omit",
        signal: controller.signal,
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          companyName: form.company,
          contactName: form.name,
          email: form.email,
          phone: "",
          jobTitle: form.role,
          country: "",
          plantType: form.plantType,
          interestArea: form.sourceSystems,
          painPoints: [form.pain, form.message].filter(Boolean).join("\n\n"),
          preferredContact: form.timeline,
          consentGiven: form.consentGiven,
          honeypot: form.honeypot,
          sourcePage: window.location.pathname,
          formVersion: "public-site-v1",
          formStartedAtUtc: formStartedAtUtc.current,
        }),
      });

      let body: (DeliveredLead & { accepted?: boolean }) | ApiErrorBody | null = null;
      try {
        body = await response.json();
      } catch {
        body = null;
      }

      if (!response.ok) {
        throw new Error(getApiErrorMessage(body as ApiErrorBody | null, response.status));
      }

      const result = body as DeliveredLead & { accepted?: boolean };
      if (!result.accepted || result.deliveryStatus !== "delivered") {
        throw new Error("The request was not confirmed as delivered. Please retry.");
      }

      setDeliveredLead(result);
      setSubmitKind("success");
      setSubmitMessage("Delivered. The PlantProcess IQ team has received your request.");
      setForm(initialState);
      formStartedAtUtc.current = new Date().toISOString();
      window.dispatchEvent(new CustomEvent("ppiq:demo-lead-delivered", { detail: result }));
    } catch (error) {
      const message = error instanceof DOMException && error.name === "AbortError"
        ? "Delivery timed out. No success was recorded; please retry or email us directly."
        : error instanceof Error
          ? error.message
          : "Delivery was not confirmed. Please retry or email us directly.";

      setSubmitKind("error");
      setSubmitMessage(message);
      setDeliveredLead(null);
    } finally {
      window.clearTimeout(timeout);
      setIsSubmitting(false);
    }
  }

  return (
    <section className="website-section request-demo-section" id="request-demo">
      <div className="section-kicker">Request a live product fit check</div>

      <div className="request-demo-layout">
        <div>
          <h2>Bring one real plant-data problem. We will show the evidence path, not a canned AI claim.</h2>
          <p>
            Best fit: manufacturing teams with scattered process, quality, genealogy,
            inspection, downtime, lab or warehouse data who need a read-only investigation
            layer without replacing existing operational systems.
          </p>

          <div className="lead-delivery-contract">
            <strong>Delivery contract</strong>
            <span>Your success message appears only after the request is stored and the sales inbox accepts the email.</span>
          </div>

          {deliveredLead ? (
            <div className="lead-success" role="status" data-testid="lead-capture-success">
              <strong>Request delivered.</strong>
              <span>Reference {deliveredLead.requestReference}. Keep it for follow-up.</span>
            </div>
          ) : null}

          <p
            className={`lead-submit-message lead-submit-message--${submitKind}`}
            aria-live="polite"
            data-testid="lead-submit-message"
          >
            {submitMessage}
          </p>

          {submitKind === "error" ? (
            <a className="website-button website-button--secondary" href={mailtoHref}>
              Email us directly
            </a>
          ) : null}
        </div>

        <form className="request-demo-form" onSubmit={onSubmit} noValidate data-testid="demo-request-form">
          <label>
            Your name
            <input value={form.name} onChange={(event) => patch("name", event.target.value)} required autoComplete="name" />
            {errors.name ? <span className="form-error">{errors.name}</span> : null}
          </label>

          <label>
            Company
            <input value={form.company} onChange={(event) => patch("company", event.target.value)} required autoComplete="organization" />
            {errors.company ? <span className="form-error">{errors.company}</span> : null}
          </label>

          <label>
            Work email
            <input type="email" value={form.email} onChange={(event) => patch("email", event.target.value)} required autoComplete="email" />
            {errors.email ? <span className="form-error">{errors.email}</span> : null}
          </label>

          <label>
            Role
            <input value={form.role} onChange={(event) => patch("role", event.target.value)} placeholder="QA lead, process engineer, plant manager..." autoComplete="organization-title" />
          </label>

          <label>
            Plant / industry type
            <input value={form.plantType} onChange={(event) => patch("plantType", event.target.value)} required placeholder="Steel, paper, pharma, food, aluminum..." />
            {errors.plantType ? <span className="form-error">{errors.plantType}</span> : null}
          </label>

          <label>
            Source systems
            <input value={form.sourceSystems} onChange={(event) => patch("sourceSystems", event.target.value)} required placeholder="MES, QMS, historian, Oracle, SQL Server, Excel..." />
            {errors.sourceSystems ? <span className="form-error">{errors.sourceSystems}</span> : null}
          </label>

          <label>
            Main quality / process pain
            <textarea value={form.pain} onChange={(event) => patch("pain", event.target.value)} required rows={3} />
            {errors.pain ? <span className="form-error">{errors.pain}</span> : null}
          </label>

          <label>
            Timeline
            <select value={form.timeline} onChange={(event) => patch("timeline", event.target.value)}>
              <option value="">Select timeline</option>
              <option value="Discovery only">Discovery only</option>
              <option value="This month">This month</option>
              <option value="This quarter">This quarter</option>
              <option value="Pilot planning">Pilot planning</option>
            </select>
          </label>

          <label>
            Optional context
            <textarea value={form.message} onChange={(event) => patch("message", event.target.value)} rows={3} />
          </label>

          <label className="consent-row">
            <input
              type="checkbox"
              checked={form.consentGiven}
              onChange={(event) => patch("consentGiven", event.target.checked)}
              required
            />
            I agree to be contacted about PlantProcess IQ and a data diagnostic.
            {errors.consentGiven ? <span className="form-error">{errors.consentGiven}</span> : null}
          </label>

          <label className="website-hidden-field" aria-hidden="true">
            Leave this field empty
            <input tabIndex={-1} autoComplete="off" value={form.honeypot} onChange={(event) => patch("honeypot", event.target.value)} />
          </label>

          <button className="website-button website-button--primary" type="submit" disabled={isSubmitting}>
            {isSubmitting ? "Confirming delivery..." : "Request the live demo"}
          </button>
        </form>
      </div>
    </section>
  );
}

export default RequestDemoForm;
'@
Write-TextFile -Path (Join-Path $WebsiteRoot 'src\components\proof\RequestDemoForm.tsx') -Content $requestDemoForm

$websiteApp = @'
import type { ReactNode } from "react";
import { NavLink, Route, Routes, useParams } from "react-router-dom";
import {
  BadgeEuro,
  BarChart3,
  BrainCircuit,
  CalendarCheck,
  CheckCircle2,
  DatabaseZap,
  Factory,
  FileText,
  GitBranch,
  Mail,
  MapPin,
  MonitorCheck,
  Network,
  ShieldCheck,
  Workflow,
} from "lucide-react";

import { BrandProofSection } from "./components/BrandProofSection";
import ProductScreenshotShowcase from "./components/proof/ProductScreenshotShowcase";
import PricingLicenseMatrix from "./components/proof/PricingLicenseMatrix";
import PositioningTruthBlock from "./components/proof/PositioningTruthBlock";
import ConnectorHonestyBlock from "./components/proof/ConnectorHonestyBlock";
import RequestDemoForm from "./components/proof/RequestDemoForm";
import { licensePlans } from "./content/phase1WebsiteProof";
import "./styles/phase10.css";
import "./styles/public-site.css";

type ProductCode = "plantprocess-iq" | "mes" | "qes" | "yard" | "energy";
type ProductAvailability = "available-pilot" | "coming-soon";

type EcosystemProduct = {
  code: ProductCode;
  name: string;
  shortName: string;
  eyebrow: string;
  headline: string;
  description: string;
  benefit: string;
  availability: ProductAvailability;
  expectedDirection?: string;
  workflow: string[];
};

const productEcosystem: EcosystemProduct[] = [
  {
    code: "plantprocess-iq",
    name: "PlantProcess IQ",
    shortName: "PPIQ",
    eyebrow: "Read-only process-to-quality intelligence",
    headline: "Connect fragmented plant data. Investigate suspected contributors. Show the evidence.",
    description:
      "PlantProcess IQ stages source-shaped data, maps it into a configurable manufacturing model, traces genealogy, computes transparent statistics and bounded value evidence, and lets a grounded assistant explain only what the evidence supports.",
    benefit:
      "Shorten manual quality forensics and give process, quality and management teams one defensible evidence path — without replacing MES, SCADA, PLC or Level 2.",
    availability: "available-pilot",
    workflow: ["Connect", "Stage", "Map", "Trace", "Analyze", "Explain"],
  },
  {
    code: "mes",
    name: "SOU MES",
    shortName: "MES",
    eyebrow: "Execution product direction",
    headline: "A future production-execution product line, separate from PlantProcess IQ.",
    description: "Production execution, order progress and operator workflows remain a separate planned product direction.",
    benefit: "The roadmap is intentionally separated so the first customer sees one complete flagship product, not four thin promises.",
    availability: "coming-soon",
    expectedDirection: "Discovery and design-partner conversations only.",
    workflow: ["Orders", "Execution", "Booking", "Exceptions", "Status"],
  },
  {
    code: "qes",
    name: "SOU QES",
    shortName: "QES",
    eyebrow: "Quality execution product direction",
    headline: "A future operational quality-workflow product line.",
    description: "Inspection plans, samples, decisions and nonconformance workflows are planned separately from the current PPIQ pilot scope.",
    benefit: "The page is a transparent roadmap marker, not a claim that the product is available today.",
    availability: "coming-soon",
    expectedDirection: "Roadmap validation with quality teams.",
    workflow: ["Plan", "Inspect", "Decide", "Escalate", "Close"],
  },
  {
    code: "yard",
    name: "SOU Yard & Warehouse",
    shortName: "Yard",
    eyebrow: "Material-logistics product direction",
    headline: "A future yard and warehouse visibility product line.",
    description: "Material location, movements, inventory and logistics constraints are a planned product direction, not part of the first PPIQ pilot.",
    benefit: "Prospects can register interest without being shown an unfinished product as if it were live.",
    availability: "coming-soon",
    expectedDirection: "Design-partner discovery for plant logistics.",
    workflow: ["Locate", "Reserve", "Move", "Confirm", "Improve"],
  },
  {
    code: "energy",
    name: "SOU Energy Management",
    shortName: "Energy",
    eyebrow: "Energy-intelligence product direction",
    headline: "A future process-aware energy product line.",
    description: "Metering, process context and energy KPI workflows are planned after the flagship evidence platform is proven with customers.",
    benefit: "The current page states the direction honestly and avoids implying production availability.",
    availability: "coming-soon",
    expectedDirection: "Roadmap discovery for energy and sustainability teams.",
    workflow: ["Collect", "Context", "Compare", "Investigate", "Report"],
  },
];

const flagshipProofs = [
  {
    title: "Deterministic math first",
    text: "Statistical engines calculate and rank. The assistant explains; it does not invent the arithmetic.",
  },
  {
    title: "Suspected contributor, not guaranteed root cause",
    text: "Population, exclusions, method and confidence stay visible so engineers can challenge the result.",
  },
  {
    title: "Read-only by design",
    text: "PPIQ does not write to PLC, SCADA, MES, Level 2 or customer control paths.",
  },
  {
    title: "Evidence before narrative",
    text: "Every rendered number must resolve to a governed evidence handle or it is withheld.",
  },
];

const liveMoments = [
  "Trace a known coil backward to source material and forward to inspection evidence.",
  "Show a transition coil with blended provenance rather than hiding a mixed source.",
  "Convert a bounded quality or downtime signal into a transparent projected value range.",
  "Ask the assistant for an unsupported number and watch it refuse rather than fabricate.",
];

const trustPillars = [
  {
    title: "Read-only source layer",
    text: "PlantProcess IQ reads source data into controlled staging. It does not write back to operational control systems.",
    icon: <DatabaseZap size={24} />,
  },
  {
    title: "Separated data layers",
    text: "Source-shaped staging, versioned mapping, canonical records and customer-facing evidence remain distinguishable.",
    icon: <Network size={24} />,
  },
  {
    title: "Deployment choice",
    text: "SOU-hosted, customer-cloud, on-prem and air-gapped directions share one configurable codebase.",
    icon: <MonitorCheck size={24} />,
  },
  {
    title: "AI honesty",
    text: "Findings are evidence-ranked investigation signals, never automatic root-cause proof or autonomous process control.",
    icon: <BrainCircuit size={24} />,
  },
];

function productIcon(code: ProductCode): ReactNode {
  switch (code) {
    case "plantprocess-iq": return <BrainCircuit size={30} />;
    case "mes": return <Workflow size={30} />;
    case "qes": return <CheckCircle2 size={30} />;
    case "yard": return <GitBranch size={30} />;
    case "energy": return <BarChart3 size={30} />;
  }
}

function Layout({ children }: { children: ReactNode }) {
  return (
    <div className="site-shell phase10-shell">
      <header className="site-header phase10-header">
        <NavLink to="/" className="brand-link" aria-label="PlantProcess IQ home">
          <span className="sou-mark"><img src="/brand/sou-icon.svg" alt="SOU Industrial Software" width={38} height={38} /></span>
          <span className="brand-text"><strong>PlantProcess IQ</strong><small>SOU Industrial Software</small></span>
        </NavLink>

        <nav className="site-nav phase10-nav" aria-label="Main website navigation">
          <NavLink to="/">Home</NavLink>
          <NavLink to="/product">PPIQ</NavLink>
          <NavLink to="/products/mes">MES</NavLink>
          <NavLink to="/products/qes">QES</NavLink>
          <NavLink to="/products/yard">Yard</NavLink>
          <NavLink to="/products/energy">Energy</NavLink>
          <NavLink to="/pricing">Pricing</NavLink>
          <NavLink to="/security">Security</NavLink>
          <NavLink to="/contact">Contact</NavLink>
        </nav>

        <NavLink className="website-button website-button--primary header-cta" to="/contact">Request demo</NavLink>
      </header>

      <main>{children}</main>

      <footer className="site-footer phase10-footer">
        <div>
          <strong>PlantProcess IQ</strong>
          <p>Evidence-grade manufacturing intelligence. Read-only. Transparent. Configurable.</p>
        </div>
        <div className="footer-contact">
          <span><Mail size={16} /> info@plantprocessiq.com</span>
          <span><MapPin size={16} /> Düsseldorf / MENA industrial network</span>
          <span><FileText size={16} /> Pilot and technical review pack available</span>
        </div>
      </footer>
    </div>
  );
}

function EcosystemGraphic({ product }: { product: EcosystemProduct }) {
  return (
    <div className="ecosystem-graphic" aria-label={`${product.name} workflow graphic`}>
      <div className="ecosystem-graphic__center">{productIcon(product.code)}<strong>{product.shortName}</strong></div>
      {product.workflow.slice(0, 6).map((step, index) => (
        <div className={`ecosystem-node ecosystem-node--${index + 1}`} key={step}>
          <span>{String(index + 1).padStart(2, "0")}</span><strong>{step}</strong>
        </div>
      ))}
    </div>
  );
}

function ProductCard({ product }: { product: EcosystemProduct }) {
  const href = product.code === "plantprocess-iq" ? "/product" : `/products/${product.code}`;
  const available = product.availability === "available-pilot";

  return (
    <NavLink className={`ecosystem-product-card ${available ? "is-available" : "is-coming-soon"}`} to={href} data-product-status={product.availability}>
      <div className="product-card-topline">
        <span className="ecosystem-product-card__icon">{productIcon(product.code)}</span>
        <span className={`product-status-badge product-status-badge--${available ? "available" : "soon"}`}>
          {available ? "Pilot available" : "Coming soon"}
        </span>
      </div>
      <span className="section-kicker">{product.eyebrow}</span>
      <strong>{product.name}</strong>
      <p>{product.description}</p>
      <span className="card-link-text">{available ? "Open flagship page →" : "View roadmap stub →"}</span>
    </NavLink>
  );
}

function HomePage() {
  return (
    <>
      <section className="page-hero phase10-hero">
        <div className="hero-copy">
          <div className="section-kicker">Read-only manufacturing intelligence</div>
          <h1>Connect your plant data. Understand your process. Keep every claim challengeable.</h1>
          <p>
            PlantProcess IQ is the current flagship: a configurable investigation layer for process,
            quality, genealogy, risk and value evidence. It sits above existing systems and never
            pretends to be MES, SCADA, Level 2 or guaranteed root-cause AI.
          </p>
          <div className="hero-actions">
            <NavLink className="website-button website-button--primary" to="/product">Explore PlantProcess IQ</NavLink>
            <NavLink className="website-button website-button--secondary" to="/contact">Request a live demo</NavLink>
          </div>
        </div>

        <div className="hero-command-card">
          <Factory size={34} />
          <strong>One honest commercial focus</strong>
          <p>PPIQ is available for paid pilot conversations. The other SOU product lines remain clearly marked as roadmap directions.</p>
          <div className="hero-mini-grid"><span>Read-only</span><span>Evidence-first</span><span>Generic</span><span>Grounded</span></div>
        </div>
      </section>

      <section className="website-section product-ecosystem-section">
        <div className="section-kicker">Product ecosystem</div>
        <div className="section-heading-row">
          <div>
            <h2>One sellable flagship. Four honest roadmap stubs.</h2>
            <p>That is stronger than presenting five products at unequal depth.</p>
          </div>
        </div>
        <div className="ecosystem-card-grid">
          {productEcosystem.map((product) => <ProductCard product={product} key={product.code} />)}
        </div>
      </section>

      <ProductScreenshotShowcase />
      <ConnectorHonestyBlock />
      <PositioningTruthBlock />
      <BrandProofSection />
      <RequestDemoForm />
    </>
  );
}

function FlagshipProductPage() {
  const product = productEcosystem[0];
  return (
    <div data-testid="flagship-product-page">
      <section className="page-hero product-detail-hero flagship-hero">
        <div>
          <div className="product-availability-row">
            <span className="product-status-badge product-status-badge--available">Paid pilot available</span>
            <span>Read-only · customer-data proof · bounded claims</span>
          </div>
          <div className="section-kicker">{product.eyebrow}</div>
          <h1>{product.headline}</h1>
          <p>{product.description}</p>
          <p className="flagship-benefit">{product.benefit}</p>
          <div className="hero-actions">
            <a className="website-button website-button--primary" href="#request-demo">Request the live nine-step demo</a>
            <NavLink className="website-button website-button--secondary" to="/security">Review the trust contract</NavLink>
          </div>
        </div>
        <EcosystemGraphic product={product} />
      </section>

      <section className="website-section trust-contract-strip" aria-label="PlantProcess IQ trust contract">
        <strong>Commercial close:</strong>
        <span>Suspected contributor, not guaranteed root cause.</span>
        <span>Read-only, no OT control.</span>
        <span>Evidence handle or no rendered number.</span>
      </section>

      <section className="website-section flagship-proof-section">
        <div className="section-kicker">Why a skeptical engineer can trust it</div>
        <h2>The product shows its working, its limits and the evidence behind the result.</h2>
        <div className="flagship-proof-grid">
          {flagshipProofs.map((proof) => (
            <article className="flagship-proof-card" key={proof.title}>
              <ShieldCheck size={24} /><h3>{proof.title}</h3><p>{proof.text}</p>
            </article>
          ))}
        </div>
      </section>

      <section className="website-section workflow-proof-section">
        <div className="section-kicker">The workflow</div>
        <h2>Data → information → simple analysis → advanced evidence → suggestion → grounded explanation.</h2>
        <ol className="workflow-ribbon">
          {product.workflow.map((step, index) => <li key={step}><span>{index + 1}</span><strong>{step}</strong></li>)}
        </ol>
      </section>

      <ProductScreenshotShowcase />

      <section className="website-section live-moment-section">
        <div className="section-kicker">Four live proof moments</div>
        <h2>What the short demo must show — with no dead click.</h2>
        <div className="live-moment-grid">
          {liveMoments.map((moment, index) => (
            <article key={moment}><span>{String(index + 1).padStart(2, "0")}</span><p>{moment}</p></article>
          ))}
        </div>
      </section>

      <ConnectorHonestyBlock />
      <PositioningTruthBlock />
      <RequestDemoForm />
    </div>
  );
}

function ComingSoonProductPage({ product }: { product: EcosystemProduct }) {
  return (
    <div data-testid="coming-soon-product" data-product-code={product.code}>
      <section className="page-hero product-detail-hero coming-soon-hero">
        <div>
          <span className="product-status-badge product-status-badge--soon">Coming soon</span>
          <div className="section-kicker">{product.eyebrow}</div>
          <h1>{product.name}</h1>
          <p>{product.description}</p>
          <p><strong>Current status:</strong> {product.expectedDirection}</p>
          <div className="hero-actions">
            <a className="website-button website-button--primary" href="#request-demo">Register roadmap interest</a>
            <NavLink className="website-button website-button--secondary" to="/product">See the available PPIQ flagship</NavLink>
          </div>
        </div>
        <EcosystemGraphic product={product} />
      </section>

      <section className="website-section coming-soon-panel">
        <h2>What “coming soon” means here</h2>
        <p>
          This is an honest product-direction page. It is not a production-availability claim,
          not a live demo promise, and not included in the current PlantProcess IQ pilot offer.
        </p>
      </section>

      <RequestDemoForm />
    </div>
  );
}

function ProductPage({ fixedCode }: { fixedCode?: ProductCode }) {
  const params = useParams<{ code?: string }>();
  const requestedCode = fixedCode ?? params.code;
  const product = productEcosystem.find((item) => item.code === requestedCode) ?? productEcosystem[0];
  return product.availability === "available-pilot"
    ? <FlagshipProductPage />
    : <ComingSoonProductPage product={product} />;
}

function PricingPage() {
  return (
    <>
      <section className="page-hero pricing-hero">
        <div>
          <div className="section-kicker">Pricing and license architecture</div>
          <h1>Light → Pro → Pro Plus → Enterprise, aligned to evidence depth and deployment scope.</h1>
          <p>Signed entitlements control features and limits; a database edit cannot silently grant a higher tier.</p>
        </div>
        <div className="pricing-proof-card"><BadgeEuro size={36} /><strong>Pilot first</strong><p>Prove ROI on customer data before conversion to an annual license.</p></div>
      </section>
      <PricingLicenseMatrix />
      <RequestDemoForm />
    </>
  );
}

function SecurityPage() {
  return (
    <>
      <section className="page-hero security-hero">
        <div>
          <div className="section-kicker">Security, trust and model honesty</div>
          <h1>Read-only acquisition, controlled identity, signed licensing and evidence-scoped assistant output.</h1>
          <p>Every deployment still requires customer security review, but the product boundary is explicit before that review starts.</p>
        </div>
        <div className="trust-stack-card"><ShieldCheck size={38} /><strong>Trust contract</strong><span>Read-only · auditable · license-gated · evidence-first</span></div>
      </section>
      <section className="website-section trust-pillar-section">
        <div className="trust-pillar-grid">
          {trustPillars.map((pillar) => <article className="trust-pillar-card" key={pillar.title}>{pillar.icon}<h2>{pillar.title}</h2><p>{pillar.text}</p></article>)}
        </div>
      </section>
      <ConnectorHonestyBlock />
      <PositioningTruthBlock />
      <RequestDemoForm />
    </>
  );
}

function ContactPage() {
  return (
    <>
      <section className="page-hero contact-hero">
        <div>
          <div className="section-kicker">Request demo</div>
          <h1>Book a practical discovery call around one real plant-data problem.</h1>
          <p>Share the source systems, quality pain, plant type and current investigation effort. The form confirms real inbox delivery.</p>
        </div>
        <div className="contact-proof-card"><CalendarCheck size={38} /><strong>20-minute fit check</strong><span>Problem → data → evidence path → pilot fit</span></div>
      </section>
      <RequestDemoForm />
    </>
  );
}

export function App() {
  return (
    <Layout>
      <Routes>
        <Route path="/" element={<HomePage />} />
        <Route path="/product" element={<ProductPage fixedCode="plantprocess-iq" />} />
        <Route path="/services" element={<ProductPage fixedCode="plantprocess-iq" />} />
        <Route path="/products/:code" element={<ProductPage />} />
        <Route path="/pricing" element={<PricingPage />} />
        <Route path="/security" element={<SecurityPage />} />
        <Route path="/about" element={<ProductPage fixedCode="plantprocess-iq" />} />
        <Route path="/contact" element={<ContactPage />} />
      </Routes>
    </Layout>
  );
}

export default App;
'@
Write-TextFile -Path (Join-Path $WebsiteRoot 'src\App.tsx') -Content $websiteApp

$publicSiteCss = @'
/* Public-site commercial closure: one flagship, honest roadmap stubs, real lead delivery. */

.product-card-topline,
.product-availability-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  flex-wrap: wrap;
}

.product-status-badge {
  display: inline-flex;
  align-items: center;
  width: fit-content;
  border-radius: 999px;
  padding: 5px 10px;
  font-size: 0.75rem;
  font-weight: 800;
  letter-spacing: 0.04em;
  text-transform: uppercase;
}

.product-status-badge--available {
  border: 1px solid rgba(44, 230, 162, 0.55);
  background: rgba(44, 230, 162, 0.12);
  color: #2ce6a2;
}

.product-status-badge--soon {
  border: 1px solid rgba(142, 167, 193, 0.45);
  background: rgba(142, 167, 193, 0.1);
  color: #b8cadc;
}

.ecosystem-product-card.is-coming-soon {
  opacity: 0.82;
  border-style: dashed;
}

.ecosystem-product-card.is-available {
  box-shadow: 0 0 0 1px rgba(0, 212, 255, 0.2), 0 22px 70px rgba(0, 132, 255, 0.12);
}

.flagship-hero .flagship-benefit {
  color: #eaf6ff;
  font-weight: 650;
}

.trust-contract-strip {
  display: grid;
  grid-template-columns: auto repeat(3, minmax(0, 1fr));
  gap: 14px;
  align-items: center;
  border: 1px solid rgba(0, 212, 255, 0.35);
  border-radius: 14px;
  background: linear-gradient(120deg, rgba(10, 132, 255, 0.13), rgba(11, 23, 48, 0.94));
}

.trust-contract-strip strong {
  color: #00d4ff;
}

.trust-contract-strip span {
  border-left: 1px solid rgba(142, 167, 193, 0.25);
  padding-left: 14px;
}

.flagship-proof-grid,
.live-moment-grid {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 16px;
  margin-top: 22px;
}

.flagship-proof-card,
.live-moment-grid article,
.coming-soon-panel,
.lead-delivery-contract {
  border: 1px solid rgba(142, 167, 193, 0.22);
  border-radius: 14px;
  background: rgba(11, 23, 48, 0.88);
  padding: 18px;
}

.flagship-proof-card svg {
  color: #00d4ff;
}

.flagship-proof-card h3 {
  margin: 12px 0 8px;
}

.workflow-ribbon {
  list-style: none;
  padding: 0;
  margin: 24px 0 0;
  display: grid;
  grid-template-columns: repeat(6, minmax(0, 1fr));
  gap: 10px;
}

.workflow-ribbon li {
  display: flex;
  align-items: center;
  gap: 9px;
  min-height: 64px;
  border: 1px solid rgba(0, 212, 255, 0.22);
  border-radius: 12px;
  padding: 12px;
  background: rgba(5, 11, 24, 0.7);
}

.workflow-ribbon li span,
.live-moment-grid article > span {
  color: #00d4ff;
  font-family: "JetBrains Mono", Consolas, monospace;
  font-weight: 800;
}

.live-moment-grid article p {
  margin: 12px 0 0;
}

.coming-soon-hero {
  border-bottom: 1px dashed rgba(142, 167, 193, 0.35);
}

.coming-soon-panel {
  max-width: 900px;
}

.lead-delivery-contract {
  display: grid;
  gap: 5px;
  margin: 20px 0;
}

.lead-delivery-contract strong {
  color: #2ce6a2;
}

.lead-submit-message {
  min-height: 1.5em;
}

.lead-submit-message--working {
  color: #00d4ff;
}

.lead-submit-message--error {
  color: #ff8ba0;
}

.lead-submit-message--success {
  color: #2ce6a2;
}

@media (max-width: 1050px) {
  .flagship-proof-grid,
  .live-moment-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .workflow-ribbon {
    grid-template-columns: repeat(3, minmax(0, 1fr));
  }

  .trust-contract-strip {
    grid-template-columns: 1fr;
  }

  .trust-contract-strip span {
    border-left: 0;
    border-top: 1px solid rgba(142, 167, 193, 0.25);
    padding: 10px 0 0;
  }
}

@media (max-width: 680px) {
  .flagship-proof-grid,
  .live-moment-grid,
  .workflow-ribbon {
    grid-template-columns: 1fr;
  }

  .product-availability-row {
    align-items: flex-start;
    flex-direction: column;
  }
}
'@
Write-TextFile -Path (Join-Path $WebsiteRoot 'src\styles\public-site.css') -Content $publicSiteCss

$websiteEnvExample = @'
# Development may point directly to the local API.
# Production should normally leave this empty and use same-origin /api through Caddy.
VITE_WEBSITE_API_BASE_URL=http://localhost:5063
'@
Write-TextFile -Path (Join-Path $WebsiteRoot '.env.example') -Content $websiteEnvExample

$publicSiteValidator = @'
import fs from "node:fs";
import path from "node:path";
import process from "node:process";

const root = process.cwd();
const read = (relativePath) => fs.readFileSync(path.join(root, relativePath), "utf8");
const app = read("src/App.tsx");
const form = read("src/components/proof/RequestDemoForm.tsx");
const css = read("src/styles/public-site.css");

const failures = [];
const assert = (condition, message) => { if (!condition) failures.push(message); };

const forbiddenCommercialClaims = [
  /guaranteed\s+savings/i,
  /guaranteed\s+roi/i,
  /eliminates?\s+all\s+defects/i,
  /automatic\s+root[- ]cause/i,
  /autonomous\s+process\s+control/i,
  /writes?\s+back\s+to\s+(plc|scada|level\s*2)/i,
  /zero\s+false\s+positives/i,
];

for (const pattern of forbiddenCommercialClaims) {
  assert(!pattern.test(app), `Forbidden commercial claim matched: ${pattern}`);
}

assert(app.includes('data-testid="flagship-product-page"'), "Flagship PlantProcess IQ page marker is missing.");
assert(app.includes("Suspected contributor, not guaranteed root cause"), "Honesty boundary is missing from the flagship page.");
assert(app.includes("Read-only, no OT control"), "Read-only commercial close is missing.");
assert(app.includes("The assistant refuses uncited numbers"), "Assistant refusal proof moment is missing.");
assert(app.includes('data-testid="coming-soon-product"'), "Coming-soon product stub is missing.");

for (const code of ["mes", "qes", "yard", "energy"]) {
  assert(app.includes(`code: "${code}"`), `Product code ${code} is missing.`);
}

assert((app.match(/availability:\s*"coming-soon"/g) ?? []).length === 4, "Exactly four products must be marked coming soon.");
assert((app.match(/availability:\s*"available-pilot"/g) ?? []).length === 1, "Exactly one product must be marked pilot-available.");
assert(form.includes("/api/public/leads"), "Website form is not wired to the public lead endpoint.");
assert(!form.includes("localStorage"), "Lead capture must not use localStorage.");
assert(form.includes('result.deliveryStatus !== "delivered"'), "Success is not explicitly gated on confirmed delivery.");
assert(form.includes("import.meta.env.DEV ? \"http://localhost:5063\" : \"\""), "Production-safe same-origin API fallback is missing.");
assert(css.includes(".product-status-badge--soon"), "Coming-soon styling is missing.");
assert(css.includes("@media (max-width: 680px)"), "Phone responsive rule is missing.");

if (failures.length > 0) {
  console.error("Public-site acceptance failed:");
  for (const failure of failures) console.error(` - ${failure}`);
  process.exit(1);
}

console.log("Public-site static acceptance passed.");
'@
Write-TextFile -Path (Join-Path $WebsiteRoot 'scripts\validate-public-site.mjs') -Content $publicSiteValidator

$publicSiteConfig = @'
import { defineConfig, devices } from "@playwright/test";

export default defineConfig({
  testDir: "./e2e/public-site",
  fullyParallel: false,
  retries: 0,
  workers: 1,
  reporter: [["list"], ["html", { outputFolder: "playwright-report-public-site", open: "never" }]],
  use: {
    baseURL: "http://127.0.0.1:5080",
    trace: "retain-on-failure",
    screenshot: "only-on-failure",
    video: "retain-on-failure",
  },
  webServer: {
    command: "npm run dev -- --host 127.0.0.1 --port 5080",
    url: "http://127.0.0.1:5080",
    reuseExistingServer: false,
    timeout: 120_000,
  },
  projects: [
    { name: "desktop-chromium", use: { ...devices["Desktop Chrome"] } },
    { name: "tablet", use: { ...devices["iPad (gen 7)"] } },
    { name: "mobile", use: { ...devices["Pixel 5"] } },
  ],
});
'@
Write-TextFile -Path (Join-Path $WebsiteRoot 'playwright.public-site.config.ts') -Content $publicSiteConfig

$publicSiteSpec = @'
import { expect, test } from "@playwright/test";

async function fillLeadForm(page: import("@playwright/test").Page, suffix: string) {
  await page.getByLabel("Your name").fill(`Acceptance User ${suffix}`);
  await page.getByLabel("Company").fill(`Acceptance Plant ${suffix}`);
  await page.getByLabel("Work email").fill(`acceptance-${suffix}@example.com`);
  await page.getByLabel("Role").fill("Quality engineer");
  await page.getByLabel("Plant / industry type").fill("Steel manufacturing");
  await page.getByLabel("Source systems").fill("MES, QMS, historian, Oracle");
  await page.getByLabel("Main quality / process pain").fill("Manual genealogy and quality investigation.");
  await page.getByLabel(/I agree to be contacted/).check();
}

test("flagship page is deep and the other four products are honest stubs", async ({ page }) => {
  await page.goto("/product");
  await expect(page.getByTestId("flagship-product-page")).toBeVisible();
  await expect(page.getByText("Suspected contributor, not guaranteed root cause.")).toBeVisible();
  await expect(page.getByText(/assistant refuses uncited numbers/i)).toBeVisible();

  for (const code of ["mes", "qes", "yard", "energy"]) {
    await page.goto(`/products/${code}`);
    await expect(page.getByTestId("coming-soon-product")).toBeVisible();
    await expect(page.getByText("Coming soon").first()).toBeVisible();
    await expect(page.getByText(/not a production-availability claim/i)).toBeVisible();
  }
});

test("success appears only after the backend confirms inbox delivery", async ({ page }) => {
  let releaseResponse: (() => void) | undefined;
  const responseGate = new Promise<void>((resolve) => { releaseResponse = resolve; });

  await page.route("**/api/public/leads", async (route) => {
    await responseGate;
    await route.fulfill({
      status: 201,
      contentType: "application/json",
      body: JSON.stringify({
        accepted: true,
        leadId: "11111111-1111-1111-1111-111111111111",
        requestReference: "PPIQ-ACCEPTANCE-1",
        deliveryStatus: "delivered",
        deliveredAtUtc: new Date().toISOString(),
      }),
    });
  });

  await page.goto("/contact");
  await fillLeadForm(page, "one");
  await page.getByRole("button", { name: "Request the live demo" }).click();
  await expect(page.getByTestId("lead-capture-success")).toHaveCount(0);
  await expect(page.getByText(/confirming inbox delivery/i)).toBeVisible();

  releaseResponse?.();
  await expect(page.getByTestId("lead-capture-success")).toBeVisible();
  await expect(page.getByText("PPIQ-ACCEPTANCE-1")).toBeVisible();
});

test("delivery failure never produces a false success state", async ({ page }) => {
  await page.route("**/api/public/leads", async (route) => {
    await route.fulfill({
      status: 503,
      contentType: "application/json",
      body: JSON.stringify({ accepted: false, stored: true, deliveryStatus: "failed", message: "Inbox delivery was not confirmed." }),
    });
  });

  await page.goto("/contact");
  await fillLeadForm(page, "failure");
  await page.getByRole("button", { name: "Request the live demo" }).click();
  await expect(page.getByTestId("lead-capture-success")).toHaveCount(0);
  await expect(page.getByTestId("lead-submit-message")).toContainText(/not confirmed/i);
  await expect(page.getByRole("link", { name: "Email us directly" })).toBeVisible();
});

test("public pages render without horizontal overflow", async ({ page }) => {
  for (const route of ["/", "/product", "/products/mes", "/pricing", "/security", "/contact"]) {
    await page.goto(route);
    const overflow = await page.evaluate(() => document.documentElement.scrollWidth > document.documentElement.clientWidth + 1);
    expect(overflow, `Horizontal overflow on ${route}`).toBeFalsy();
    await expect(page.locator("main")).toBeVisible();
  }
});
'@
Write-TextFile -Path (Join-Path $WebsiteRoot 'e2e\public-site\public-site.spec.ts') -Content $publicSiteSpec

Update-PackageScripts -Path $WebsitePackagePath

# -----------------------------------------------------------------------------
# Video pipeline
# -----------------------------------------------------------------------------

Write-Stage 'Installing the reproducible 2–3 minute demo-video pipeline'

$videoBuilder = @'
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$SourceVideo,
    [Parameter(Mandatory)][string]$PlanPath,
    [Parameter(Mandatory)][string]$OutputPath,
    [Parameter()][string]$NarrationAudio = '',
    [Parameter()][string]$EvidenceDirectory = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)

function Require-Command([string]$Name) {
    $command = Get-Command $Name -ErrorAction SilentlyContinue
    if ($null -eq $command) { throw "$Name is required but was not found in PATH." }
    return $command.Source
}

function Parse-Time([string]$Value, [string]$Field) {
    $result = [TimeSpan]::Zero
    if (-not [TimeSpan]::TryParse($Value, [Globalization.CultureInfo]::InvariantCulture, [ref]$result)) {
        throw "Invalid $Field timestamp '$Value'. Use HH:MM:SS.mmm."
    }
    return $result
}

function Format-SrtTime([TimeSpan]$Value) {
    return '{0:00}:{1:00}:{2:00},{3:000}' -f [math]::Floor($Value.TotalHours), $Value.Minutes, $Value.Seconds, $Value.Milliseconds
}

function Invoke-Ffmpeg([string[]]$Arguments) {
    & $script:Ffmpeg @Arguments
    if ($LASTEXITCODE -ne 0) { throw "ffmpeg failed with exit code $LASTEXITCODE" }
}

$script:Ffmpeg = Require-Command 'ffmpeg'
$ffprobe = Require-Command 'ffprobe'

if (-not (Test-Path -LiteralPath $SourceVideo -PathType Leaf)) { throw "Source video not found: $SourceVideo" }
if (-not (Test-Path -LiteralPath $PlanPath -PathType Leaf)) { throw "Plan not found: $PlanPath" }
if ($NarrationAudio -and -not (Test-Path -LiteralPath $NarrationAudio -PathType Leaf)) { throw "Narration audio not found: $NarrationAudio" }

$plan = Get-Content -LiteralPath $PlanPath -Raw | ConvertFrom-Json
$segments = @($plan.segments)
if ($segments.Count -lt 4) { throw 'The plan must contain at least four clean segments.' }

$requiredMoments = @('genealogy','transition','value','assistant')
foreach ($moment in $requiredMoments) {
    if (@($segments | Where-Object { [string]$_.keyMoment -eq $moment }).Count -eq 0) {
        throw "The plan is missing the required key moment '$moment'."
    }
}

$totalDuration = [TimeSpan]::Zero
$parsed = New-Object System.Collections.Generic.List[object]
foreach ($segment in $segments) {
    $start = Parse-Time ([string]$segment.start) 'start'
    $end = Parse-Time ([string]$segment.end) 'end'
    if ($end -le $start) { throw "Segment '$($segment.label)' ends before it starts." }
    $duration = $end - $start
    $totalDuration += $duration
    $parsed.Add([pscustomobject]@{
        Label = [string]$segment.label
        Start = $start
        End = $end
        Duration = $duration
        Caption = [string]$segment.caption
        KeyMoment = [string]$segment.keyMoment
    }) | Out-Null
}

if ($totalDuration.TotalSeconds -lt 120 -or $totalDuration.TotalSeconds -gt 180) {
    throw ("The planned duration is {0:N1}s. It must be between 120 and 180 seconds." -f $totalDuration.TotalSeconds)
}

$outputDirectory = Split-Path -Parent $OutputPath
if (-not (Test-Path -LiteralPath $outputDirectory)) { New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null }
$work = Join-Path $outputDirectory ('.video-build-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $work | Out-Null

try {
    $concatLines = New-Object System.Collections.Generic.List[string]
    $srtLines = New-Object System.Collections.Generic.List[string]
    $cursor = [TimeSpan]::Zero
    $captionIndex = 1

    for ($i = 0; $i -lt $parsed.Count; $i++) {
        $segment = $parsed[$i]
        $segmentPath = Join-Path $work ('segment-{0:00}.mp4' -f ($i + 1))
        $startText = $segment.Start.ToString('c', [Globalization.CultureInfo]::InvariantCulture)
        $durationText = $segment.Duration.ToString('c', [Globalization.CultureInfo]::InvariantCulture)

        Invoke-Ffmpeg @(
            '-hide_banner','-loglevel','warning','-y',
            '-ss',$startText,'-i',$SourceVideo,'-t',$durationText,
            '-vf',"scale='min(1920,iw)':-2:flags=lanczos,fps=30,format=yuv420p",
            '-c:v','libx264','-preset','medium','-crf','19',
            '-c:a','aac','-b:a','160k','-ar','48000',
            '-movflags','+faststart',$segmentPath
        )

        $concatPath = $segmentPath.Replace('\','/').Replace("'", "'\\''")
        $concatLines.Add("file '$concatPath'") | Out-Null

        $captionStart = $cursor + [TimeSpan]::FromMilliseconds(500)
        $captionEnd = $cursor + [TimeSpan]::FromSeconds([Math]::Min(8, [Math]::Max(2, $segment.Duration.TotalSeconds - 0.5)))
        $srtLines.Add([string]$captionIndex) | Out-Null
        $srtLines.Add("$(Format-SrtTime $captionStart) --> $(Format-SrtTime $captionEnd)") | Out-Null
        $srtLines.Add($segment.Caption) | Out-Null
        $srtLines.Add('') | Out-Null
        $captionIndex++
        $cursor += $segment.Duration
    }

    $concatFile = Join-Path $work 'concat.txt'
    $captionFile = Join-Path $work 'captions.srt'
    [IO.File]::WriteAllLines($concatFile, $concatLines, $utf8NoBom)
    [IO.File]::WriteAllLines($captionFile, $srtLines, $utf8NoBom)

    $joined = Join-Path $work 'joined.mp4'
    Invoke-Ffmpeg @('-hide_banner','-loglevel','warning','-y','-f','concat','-safe','0','-i',$concatFile,'-c','copy',$joined)

    Push-Location $work
    try {
        if ($NarrationAudio) {
            Invoke-Ffmpeg @(
                '-hide_banner','-loglevel','warning','-y','-i',$joined,'-i',$NarrationAudio,
                '-filter_complex',"[0:v]subtitles=captions.srt:force_style='FontName=Arial,FontSize=24,PrimaryColour=&H00FFFFFF,OutlineColour=&H00102030,BorderStyle=1,Outline=2,Shadow=0,MarginV=42'[v];[1:a]loudnorm=I=-16:LRA=11:TP=-1.5[a]",
                '-map','[v]','-map','[a]','-shortest',
                '-c:v','libx264','-preset','medium','-crf','19','-pix_fmt','yuv420p',
                '-c:a','aac','-b:a','192k','-ar','48000','-movflags','+faststart',$OutputPath
            )
        }
        else {
            Invoke-Ffmpeg @(
                '-hide_banner','-loglevel','warning','-y','-i',$joined,
                '-vf',"subtitles=captions.srt:force_style='FontName=Arial,FontSize=24,PrimaryColour=&H00FFFFFF,OutlineColour=&H00102030,BorderStyle=1,Outline=2,Shadow=0,MarginV=42'",
                '-af','loudnorm=I=-16:LRA=11:TP=-1.5',
                '-c:v','libx264','-preset','medium','-crf','19','-pix_fmt','yuv420p',
                '-c:a','aac','-b:a','192k','-ar','48000','-movflags','+faststart',$OutputPath
            )
        }
    }
    finally {
        Pop-Location
    }

    $probeJson = & $ffprobe -v error -show_entries format=duration:stream=index,codec_name,codec_type,width,height,pix_fmt -of json $OutputPath
    if ($LASTEXITCODE -ne 0) { throw 'ffprobe failed.' }
    $probe = ($probeJson -join "`n") | ConvertFrom-Json
    $duration = [double]$probe.format.duration
    if ($duration -lt 120 -or $duration -gt 180) { throw "Rendered duration $duration is outside 120–180 seconds." }

    $videoStream = @($probe.streams | Where-Object codec_type -eq 'video')[0]
    $audioStream = @($probe.streams | Where-Object codec_type -eq 'audio')[0]
    if ($null -eq $videoStream -or $videoStream.codec_name -ne 'h264') { throw 'Rendered video is not H.264.' }
    if ($videoStream.pix_fmt -ne 'yuv420p') { throw 'Rendered video pixel format is not yuv420p.' }
    if ($null -eq $audioStream -or $audioStream.codec_name -ne 'aac') { throw 'Rendered audio is not AAC.' }

    $poster = [IO.Path]::ChangeExtension($OutputPath, '.jpg')
    Invoke-Ffmpeg @('-hide_banner','-loglevel','warning','-y','-ss','00:00:08','-i',$OutputPath,'-frames:v','1','-q:v','2',$poster)

    $evidence = [ordered]@{
        generatedAtUtc = [DateTime]::UtcNow.ToString('o')
        sourceVideo = (Resolve-Path $SourceVideo).Path
        plan = (Resolve-Path $PlanPath).Path
        output = (Resolve-Path $OutputPath).Path
        durationSeconds = [Math]::Round($duration, 3)
        videoCodec = $videoStream.codec_name
        pixelFormat = $videoStream.pix_fmt
        width = $videoStream.width
        height = $videoStream.height
        audioCodec = $audioStream.codec_name
        sha256 = (Get-FileHash -LiteralPath $OutputPath -Algorithm SHA256).Hash
        requiredMoments = $requiredMoments
    }

    $evidencePath = [IO.Path]::ChangeExtension($OutputPath, '.evidence.json')
    [IO.File]::WriteAllText($evidencePath, ($evidence | ConvertTo-Json -Depth 10), $utf8NoBom)

    if ($EvidenceDirectory) {
        if (-not (Test-Path -LiteralPath $EvidenceDirectory)) { New-Item -ItemType Directory -Force -Path $EvidenceDirectory | Out-Null }
        Copy-Item -LiteralPath $evidencePath -Destination (Join-Path $EvidenceDirectory 'demo-video-evidence.json') -Force
        Copy-Item -LiteralPath $captionFile -Destination (Join-Path $EvidenceDirectory 'demo-video-captions.srt') -Force
    }

    Write-Host "[GREEN] Video rendered: $OutputPath" -ForegroundColor Green
    Write-Host ("[GREEN] Duration: {0:N1}s | H.264/yuv420p + AAC | faststart | captions burned" -f $duration) -ForegroundColor Green
}
finally {
    if (Test-Path -LiteralPath $work) { Remove-Item -LiteralPath $work -Recurse -Force }
}
'@
Write-TextFile -Path $VideoBuilderPath -Content $videoBuilder

$videoPlanExample = @'
{
  "title": "PlantProcess IQ — evidence-grade manufacturing intelligence",
  "outputFileName": "PlantProcessIQ_Demo_2-3min.mp4",
  "segments": [
    {
      "label": "Opening and workflow",
      "start": "00:00:05.000",
      "end": "00:00:30.000",
      "caption": "Connect plant data → map → investigate → explain",
      "keyMoment": "workflow"
    },
    {
      "label": "Genealogy",
      "start": "00:01:10.000",
      "end": "00:01:40.000",
      "caption": "Bidirectional genealogy: heat → slab → coil → quality evidence",
      "keyMoment": "genealogy"
    },
    {
      "label": "Transition coil",
      "start": "00:02:05.000",
      "end": "00:02:35.000",
      "caption": "Blended provenance: transition-coil contribution shown transparently",
      "keyMoment": "transition"
    },
    {
      "label": "Value evidence",
      "start": "00:03:00.000",
      "end": "00:03:30.000",
      "caption": "Bounded value estimate with assumptions — projected, not guaranteed",
      "keyMoment": "value"
    },
    {
      "label": "Assistant honesty and close",
      "start": "00:04:00.000",
      "end": "00:04:35.000",
      "caption": "The assistant refuses uncited numbers and keeps evidence handles",
      "keyMoment": "assistant"
    }
  ]
}
'@
Write-TextFile -Path (Join-Path $VideoDocsRoot 'demo-video-plan.example.json') -Content $videoPlanExample

$videoReadme = @'
# PlantProcess IQ short demo video

The final deliverable must be 120–180 seconds and contain these visible captions:

1. Genealogy.
2. Transition-coil / blended provenance.
3. Bounded value evidence in euros.
4. The assistant refusing an uncited number.

## Build

```powershell
.\tools\media\Build-PlantProcessDemoVideo.ps1 `
  -SourceVideo 'C:\Recordings\PPIQ-dry-run.mp4' `
  -PlanPath '.\Documentation\DemoVideo\demo-video-plan.local.json' `
  -OutputPath '.\Documentation\DemoVideo\PlantProcessIQ_Demo_2-3min.mp4'
```

The builder rejects output outside 120–180 seconds and verifies H.264, yuv420p, AAC and fast-start MP4 compatibility for mobile and desktop playback.
'@
Write-TextFile -Path (Join-Path $VideoDocsRoot 'README.md') -Content $videoReadme

# -----------------------------------------------------------------------------
# Optional migration, build, tests and live proof
# -----------------------------------------------------------------------------

try {
    if ($RunDatabaseMigration) {
        Write-Stage 'Applying database decoration migration'
        Invoke-DatabaseMigration
    }

    if ($RunBuildValidation) {
        Write-Stage 'Building backend and validating the public website'

        Invoke-Logged -Name 'backend-build' -Action {
            & dotnet build $ApiProjectPath --nologo
        }

        Push-Location $WebsiteRoot
        try {
            if (-not (Test-Path -LiteralPath (Join-Path $WebsiteRoot 'node_modules'))) {
                Invoke-Logged -Name 'website-npm-ci' -Action { & npm ci }
            }
            Invoke-Logged -Name 'website-public-acceptance' -Action { & npm run acceptance:public-site }
        }
        finally {
            Pop-Location
        }
    }

    if ($RunLiveLeadProof) {
        Write-Stage 'Executing two-submission live inbox acceptance proof'
        Invoke-LiveLeadProof
    }

    Write-Stage 'Rendering or installing the demo-video deliverable'
    Render-DemoVideoIfRequested

    Write-EvidenceSummary

    Write-Host ''
    Write-Host ('=' * 88) -ForegroundColor Green
    Write-Host 'PUBLIC COLLATERAL IMPLEMENTATION COMPLETED' -ForegroundColor Green
    Write-Host ('=' * 88) -ForegroundColor Green
    Write-Host "Evidence : $script:EvidenceRoot"
    Write-Host "Backup   : $script:BackupRoot"
    Write-Host "Video    : $(Join-Path $VideoDocsRoot 'PlantProcessIQ_Demo_2-3min.mp4')"
    Write-Host ''

    if ($script:Warnings.Count -gt 0) {
        Write-Host 'Open gates / warnings:' -ForegroundColor Yellow
        $script:Warnings | ForEach-Object { Write-Host " - $_" -ForegroundColor Yellow }
    }
}
finally {
    Stop-StartedApi
}

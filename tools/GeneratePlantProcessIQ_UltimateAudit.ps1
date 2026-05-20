#requires -Version 5.1
<#
.SYNOPSIS
    PlantProcess IQ Ultimate Documentation + Deep AI Audit Generator.

.DESCRIPTION
    This script combines and improves the behavior of:
      1. GenerateProjectDocumentation_Advanced.ps1
      2. GenerateDeepAudit.ps1

    Main goals:
      - Generate a professional full-stack documentation package.
      - Export all relevant source/config/test/deploy/documentation files.
      - Keep strong AI-friendly categorization.
      - Include files with strange/no extensions such as:
          .env
          .env.example
          .config
          .cjs
          .sh
          Dockerfile
          Caddyfile
          docker-compose.demo.yml
      - Include any readable text file that is not inside excluded folders.
      - Exclude heavy/generated/noisy folders:
          .git, .vs, bin, obj, node_modules, dist, build, coverage, migrations
      - Create multiple category files + one combined full-stack file.
      - Mask sensitive values by default, especially .env/password/token/key/secret values.

.OUTPUT
    Creates a timestamped folder under:
        C:\Workspace\PlantProcess-IQ\Documentation\UltimateAudit_<timestamp>

    Output files:
      00_Master_Index_*.txt
      01_Backend_Core_*.txt
      02_Backend_Database_*.txt
      03_Backend_Tests_*.txt
      04_Frontend_App_*.txt
      05_Frontend_Misc_*.txt
      06_Infrastructure_*.txt
      07_Tools_Validation_Misc_*.txt
      08_Website_*.txt
      09_FullStack_Combined_*.txt
      manifest_*.csv
      manifest_*.json

.NOTES
    Author purpose: PlantProcess IQ internal documentation + AI audit upload.
    PowerShell: 5.1 compatible.
#>

[CmdletBinding()]
param(
    [string]$RepositoryRoot,
    [string]$OutputBaseFolder,

    [switch]$IncludeHidden,
    [switch]$IncludeMigrations,
    [switch]$IncludeLockFiles,
    [switch]$IncludeBinaryLikeFiles,
    [switch]$OpenAfterGeneration,

    [int]$MaxFileSizeMB = 8,

    [bool]$MaskSecrets = $true
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ============================================================
# 0. Console helpers
# ============================================================

function Write-Step {
    param(
        [Parameter(Mandatory = $true)][string]$Message,
        [ConsoleColor]$Color = [ConsoleColor]::Cyan
    )

    Write-Host $Message -ForegroundColor $Color
}

function Write-Info {
    param([Parameter(Mandatory = $true)][string]$Message)
    Write-Host $Message -ForegroundColor Yellow
}

function Write-Ok {
    param([Parameter(Mandatory = $true)][string]$Message)
    Write-Host $Message -ForegroundColor Green
}

# ============================================================
# 1. Path resolution
# ============================================================

$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path

function Resolve-RepositoryRoot {
    param([Parameter(Mandatory = $true)][string]$StartPath)

    $current = $StartPath

    for ($i = 0; $i -lt 10; $i++) {
        if ([string]::IsNullOrWhiteSpace($current)) {
            break
        }

        $backendCandidate = Join-Path $current "Backend"
        $frontendCandidate = Join-Path $current "Frontend"

        if ((Test-Path -LiteralPath $backendCandidate) -and (Test-Path -LiteralPath $frontendCandidate)) {
            return $current
        }

        $parent = Split-Path -Parent $current

        if ($parent -eq $current) {
            break
        }

        $current = $parent
    }

    return $StartPath
}

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = Resolve-RepositoryRoot -StartPath $scriptDirectory
}

if (-not (Test-Path -LiteralPath $RepositoryRoot)) {
    throw "Repository root not found: $RepositoryRoot"
}

$RepositoryRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path

$BackendRoot = Join-Path $RepositoryRoot "Backend"
$FrontendRoot = Join-Path $RepositoryRoot "Frontend"
$WebsiteRoot = Join-Path $RepositoryRoot "Website"
$InfrastructureRoot = Join-Path $RepositoryRoot "Infrastructure"
$ToolsRoot = Join-Path $RepositoryRoot "tools"

if ([string]::IsNullOrWhiteSpace($OutputBaseFolder)) {
    $OutputBaseFolder = Join-Path $RepositoryRoot "Documentation"
}

if (-not (Test-Path -LiteralPath $OutputBaseFolder)) {
    New-Item -ItemType Directory -Path $OutputBaseFolder -Force | Out-Null
}

$timestamp = (Get-Date).ToString("ddMMMyyyy_HHmmss")
$OutputFolder = Join-Path $OutputBaseFolder "UltimateAudit_$timestamp"
New-Item -ItemType Directory -Path $OutputFolder -Force | Out-Null

# ============================================================
# 2. Rules: excluded folders + textual file detection
# ============================================================

$excludedFolderNames = @(
    # Source-control / IDE
    ".git",
    ".vs",
    ".idea",
    ".vscode",

    # .NET generated
    "bin",
    "obj",

    # Frontend generated/dependencies
    "node_modules",
    "dist",
    "build",
    "coverage",
    ".vite",
    ".cache",
    "TestResults",
    "test-results",
    "playwright-report",

    # Generated documentation/export output - MUST NEVER BE INCLUDED
    "Documentation",

    # Local runtime/deployment data - MUST NEVER BE INCLUDED
    ".runtime",
    "logs",
    "reports",
    "backups",
    "app-dumps",
    "dumps",
    "postgres",

    # Other generated/noisy folders
    "packages",
    ".sonarqube"
)

if (-not $IncludeMigrations) {
    $excludedFolderNames += "migrations"
    $excludedFolderNames += "Migrations"
}

$lockFileNames = @(
    "package-lock.json",
    "yarn.lock",
    "pnpm-lock.yaml",
    "composer.lock",
    "packages.lock.json"
)

$excludedFileNamePatterns = @(
    "*.tmp",
    "*.user",
    "*.suo",
    "*.cache",
    "*.tsbuildinfo",
    "*.log",
    "*.bak",
    "*.backup",
    "*.old",
    "*.zip",
    "*.7z",
    "*.rar",
    "*.db",
    "*.sqlite",
    "*.sqlite3"
)


$knownTextExtensions = @(
    ".cs",
    ".csproj",
    ".props",
    ".targets",
    ".sln",
    ".ts",
    ".tsx",
    ".js",
    ".jsx",
    ".cjs",
    ".mjs",
    ".css",
    ".scss",
    ".sass",
    ".less",
    ".html",
    ".htm",
    ".sql",
    ".json",
    ".jsonc",
    ".yml",
    ".yaml",
    ".md",
    ".txt",
    ".xml",
    ".config",
    ".editorconfig",
    ".env",
    ".example",
    ".sh",
    ".bash",
    ".ps1",
    ".psm1",
    ".psd1",
    ".dockerignore",
    ".gitignore",
    ".gitattributes",
    ".http",
    ".cshtml",
    ".razor"
)

$specialTextFileNames = @(
    "Dockerfile",
    "dockerfile",
    "Caddyfile",
    "caddyfile",
    ".env",
    ".env.example",
    ".env.development",
    ".env.local",
    ".env.production",
    ".dockerignore",
    ".gitignore",
    ".gitattributes",
    "Jenkinsfile",
    "Makefile",
    "LICENSE",
    "README"
)

function Get-RelativePath {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$Path
    )

    $rootFull = (Resolve-Path -LiteralPath $Root).Path
    $pathFull = (Resolve-Path -LiteralPath $Path).Path

    if (-not $rootFull.EndsWith([System.IO.Path]::DirectorySeparatorChar)) {
        $rootFull += [System.IO.Path]::DirectorySeparatorChar
    }

    $rootUri = New-Object System.Uri($rootFull)
    $pathUri = New-Object System.Uri($pathFull)

    $relativeUri = $rootUri.MakeRelativeUri($pathUri)
    $relativePath = [System.Uri]::UnescapeDataString($relativeUri.ToString())

    return $relativePath -replace '/', '\'
}

function Test-IsUnderExcludedFolder {
    param(
        [Parameter(Mandatory = $true)][string]$RelativePath
    )

    $parts = $RelativePath -split '[\\/]'

    foreach ($part in $parts) {
        foreach ($excluded in $excludedFolderNames) {
            if ($part -ieq $excluded) {
                return $true
            }
        }
    }

    return $false
}

function Test-IsHiddenPath {
    param(
        [Parameter(Mandatory = $true)][System.IO.FileInfo]$File
    )

    if (($File.Attributes -band [System.IO.FileAttributes]::Hidden) -ne 0) {
        return $true
    }

    $parts = $File.FullName -split '[\\/]'
    foreach ($part in $parts) {
        if ($part.StartsWith(".") -and $part -notin @(".", "..")) {
            return $true
        }
    }

    return $false
}

function Test-IsKnownTextFile {
    param(
        [Parameter(Mandatory = $true)][System.IO.FileInfo]$File
    )

    if ($specialTextFileNames -contains $File.Name) {
        return $true
    }

    if ($File.Name -like ".env*") {
        return $true
    }

    if ($File.Name -like "docker-compose*.yml" -or $File.Name -like "docker-compose*.yaml") {
        return $true
    }

    if ($File.Extension -and ($knownTextExtensions -contains $File.Extension)) {
        return $true
    }

    return $false
}

function Test-IsProbablyTextByContent {
    param(
        [Parameter(Mandatory = $true)][System.IO.FileInfo]$File
    )

    if ($IncludeBinaryLikeFiles) {
        return $true
    }

    if ($File.Length -eq 0) {
        return $true
    }

    $maxProbeBytes = 4096
    $bufferLength = [Math]::Min($maxProbeBytes, [int]$File.Length)
    $buffer = New-Object byte[] $bufferLength

    $stream = [System.IO.File]::OpenRead($File.FullName)

    try {
        $read = $stream.Read($buffer, 0, $bufferLength)

        for ($i = 0; $i -lt $read; $i++) {
            if ($buffer[$i] -eq 0) {
                return $false
            }
        }

        return $true
    }
    finally {
        $stream.Dispose()
    }
}

# ============================================================
# 3. Secret masking
# ============================================================

function Protect-SecretContent {
    param(
        [Parameter(Mandatory = $true)][string]$Content,
        [Parameter(Mandatory = $true)][string]$RelativePath
    )

    if (-not $MaskSecrets) {
        return $Content
    }

    $isSensitiveFile =
        ($RelativePath -match '(^|[\\/])\.env') -or
        ($RelativePath -match 'appsettings') -or
        ($RelativePath -match 'launchSettings') -or
        ($RelativePath -match 'docker-compose') -or
        ($RelativePath -match 'Caddyfile') -or
        ($RelativePath -match 'Dockerfile')

    if (-not $isSensitiveFile) {
        return $Content
    }

    $masked = $Content

    # KEY=value style
    $masked = [regex]::Replace(
        $masked,
        '(?im)^(\s*[^#\r\n]*?(password|passwd|pwd|secret|token|apikey|api_key|signingkey|connectionstrings__[^=\r\n]*|connectionstring|privatekey|accesskey|clientsecret)[^=\r\n]*\s*=\s*)(.+)$',
        '$1[MASKED_FOR_AI_AUDIT]'
    )

    # JSON style: "Password": "..."
    $masked = [regex]::Replace(
        $masked,
        '(?im)("([^"]*(password|passwd|pwd|secret|token|apikey|api_key|signingkey|connectionstring|privatekey|accesskey|clientsecret)[^"]*)"\s*:\s*")([^"]*)(")',
        '$1[MASKED_FOR_AI_AUDIT]$5'
    )

    # YAML style: password: value
    $masked = [regex]::Replace(
        $masked,
        '(?im)^(\s*[^#\r\n:]*?(password|passwd|pwd|secret|token|apikey|api_key|signingkey|connectionstring|privatekey|accesskey|clientsecret)[^:\r\n]*\s*:\s*)(.+)$',
        '$1[MASKED_FOR_AI_AUDIT]'
    )

    return $masked
}

# ============================================================
# 4. File metadata + classification
# ============================================================

function Get-FileLineCount {
    param([Parameter(Mandatory = $true)][string]$Path)

    $count = 0
    $reader = New-Object System.IO.StreamReader($Path, [System.Text.Encoding]::UTF8, $true)

    try {
        while ($null -ne $reader.ReadLine()) {
            $count++
        }
    }
    catch {
        return -1
    }
    finally {
        $reader.Dispose()
    }

    return $count
}

function Get-FileClassification {
    param(
        [Parameter(Mandatory = $true)][string]$RelativePath,
        [Parameter(Mandatory = $true)][System.IO.FileInfo]$File
    )

    $rp = $RelativePath -replace '/', '\'
    $ext = $File.Extension.ToLowerInvariant()
    $name = $File.Name

    if ($rp -match '^Backend\\PlantProcess\.Api\\Endpoints\\') { return "Backend API Endpoint" }
    if ($rp -match '^Backend\\PlantProcess\.Api\\Middleware\\') { return "API Middleware" }
    if ($rp -match '^Backend\\PlantProcess\.Api\\Security\\') { return "API Security/Auth" }
    if ($rp -match '^Backend\\PlantProcess\.Api\\') { return "Backend API Source" }

    if ($rp -match '^Backend\\PlantProcess\.Application\\.*Contracts\\') { return "Application Contract / DTO" }
    if ($rp -match '^Backend\\PlantProcess\.Application\\.*Interfaces\\') { return "Application Interface" }
    if ($rp -match '^Backend\\PlantProcess\.Application\\.*Services\\') { return "Application Service" }
    if ($rp -match '^Backend\\PlantProcess\.Application\\') { return "Application Layer Source" }

    if ($rp -match '^Backend\\PlantProcess\.Domain\\Entities\\') { return "Domain Entity" }
    if ($rp -match '^Backend\\PlantProcess\.Domain\\Enums\\') { return "Domain Enum" }
    if ($rp -match '^Backend\\PlantProcess\.Domain\\') { return "Domain Source" }

    if ($rp -match '^Backend\\PlantProcess\.Infrastructure\\Persistence\\Configurations\\') { return "EF Core Configuration" }
    if ($rp -match '^Backend\\PlantProcess\.Infrastructure\\Migrations\\') { return "EF Migration" }
    if ($rp -match '^Backend\\PlantProcess\.Infrastructure\\Connectors\\') { return "Connector Infrastructure" }
    if ($rp -match '^Backend\\PlantProcess\.Infrastructure\\Bulk\\') { return "Bulk / Ingestion Support" }
    if ($rp -match '^Backend\\PlantProcess\.Infrastructure\\') { return "Infrastructure Source" }

    if ($rp -match '^Backend\\PlantProcess\.Workers\\') { return "Worker Service" }

    if ($rp -match '^Backend\\database\\scripts\\') { return "Database Script" }
    if ($rp -match '^Backend\\database\\seed\\') { return "Database Seed" }
    if ($rp -match '^Backend\\database\\views\\') { return "Database View" }
    if ($rp -match '^Backend\\database\\') { return "Database Asset" }

    if ($rp -match '^Backend\\tests\\') { return "Backend Test" }

    if ($rp -match '^Frontend\\PlantProcess\.Web\\src\\components\\') { return "Frontend Component" }
    if ($rp -match '^Frontend\\PlantProcess\.Web\\src\\pages\\') { return "Frontend Page" }
    if ($rp -match '^Frontend\\PlantProcess\.Web\\src\\api\\') { return "Frontend API Client" }
    if ($rp -match '^Frontend\\PlantProcess\.Web\\src\\state\\') { return "Frontend State" }
    if ($rp -match '^Frontend\\PlantProcess\.Web\\src\\styles\\') { return "Frontend Stylesheet" }
    if ($rp -match '^Frontend\\PlantProcess\.Web\\src\\') { return "Frontend Source" }
    if ($rp -match '^Frontend\\PlantProcess\.Web\\e2e\\') { return "Frontend E2E Test" }
    if ($rp -match '^Frontend\\PlantProcess\.Web\\scripts\\') { return "Frontend Script" }
    if ($rp -match '^Frontend\\PlantProcess\.Web\\test-results\\') { return "Frontend Test Result" }
    if ($rp -match '^Frontend\\PlantProcess\.Web\\playwright-report\\') { return "Frontend Playwright Report" }
    if ($rp -match '^Frontend\\PlantProcess\.Web\\') { return "Frontend Config / Misc" }

    if ($rp -match '^Website\\PlantProcess\.Website\\src\\components\\') { return "Website Component" }
    if ($rp -match '^Website\\PlantProcess\.Website\\src\\') { return "Website Source" }
    if ($rp -match '^Website\\PlantProcess\.Website\\') { return "Website Config / Asset" }

    if ($rp -match '^Infrastructure\\') { return "Deployment / Infrastructure" }

    if ($rp -match '^tools\\') { return "Tooling Script" }
    if ($rp -match '^Validation\\') { return "Validation Script" }

    if ($name -eq "Dockerfile") { return "Dockerfile" }
    if ($name -eq "Caddyfile") { return "Caddyfile" }
    if ($name -eq "Jenkinsfile") { return "Jenkins Pipeline" }
    if ($name -like ".env*") { return "Environment Config" }
    if ($name -like "docker-compose*") { return "Docker Compose" }

    if ($ext -eq ".sln") { return ".NET Solution" }
    if ($ext -eq ".csproj") { return ".NET Project File" }
    if ($ext -eq ".md") { return "Markdown Documentation" }
    if ($ext -eq ".json") { return "JSON Configuration" }
    if ($ext -eq ".yml" -or $ext -eq ".yaml") { return "YAML Configuration" }
    if ($ext -eq ".ps1") { return "PowerShell Script" }
    if ($ext -eq ".sh") { return "Shell Script" }

    return "Other Text File"
}

function Get-PrimaryCategory {
    param([Parameter(Mandatory = $true)][string]$RelativePath)

    $rp = $RelativePath -replace '/', '\'

    if (
        $rp -match '^Backend\\PlantProcess\.Api\\' -or
        $rp -match '^Backend\\PlantProcess\.Application\\' -or
        $rp -match '^Backend\\PlantProcess\.Domain\\' -or
        $rp -match '^Backend\\PlantProcess\.Infrastructure\\' -or
        $rp -match '^Backend\\PlantProcess\.Workers\\' -or
        $rp -match '^Backend\\PlantProcessIQ\.sln$'
    ) {
        return "01_Backend_Core"
    }

    if ($rp -match '^Backend\\database\\') {
        return "02_Backend_Database"
    }

    if ($rp -match '^Backend\\tests\\') {
        return "03_Backend_Tests"
    }

    if ($rp -match '^Frontend\\PlantProcess\.Web\\') {
        if (
            $rp -match '^Frontend\\PlantProcess\.Web\\e2e\\' -or
            $rp -match '^Frontend\\PlantProcess\.Web\\scripts\\' -or
            $rp -match '^Frontend\\PlantProcess\.Web\\test-results\\' -or
            $rp -match '^Frontend\\PlantProcess\.Web\\playwright-report\\'
        ) {
            return "05_Frontend_Misc"
        }

        return "04_Frontend_App"
    }

    if ($rp -match '^Infrastructure\\') {
        return "06_Infrastructure"
    }

    if (
        $rp -match '^tools\\' -or
        $rp -match '^Validation\\' -or
        $rp -match '^\.github\\' -or
        $rp -match '^README' -or
        $rp -match '^Jenkinsfile$' -or
        $rp -match '^\.env' -or
        $rp -match '^docker-compose'
    ) {
        return "07_Tools_Validation_Misc"
    }

    if ($rp -match '^Website\\PlantProcess\.Website\\') {
        return "08_Website"
    }

    return "07_Tools_Validation_Misc"
}

function Get-CategoryTitle {
    param([Parameter(Mandatory = $true)][string]$Category)

    switch ($Category) {
        "01_Backend_Core" { return "Backend Core: API, Application, Domain, Infrastructure, Workers" }
        "02_Backend_Database" { return "Backend Database: Scripts, Seed, Views" }
        "03_Backend_Tests" { return "Backend Tests" }
        "04_Frontend_App" { return "Frontend App: PlantProcess.Web Core Source" }
        "05_Frontend_Misc" { return "Frontend Misc: E2E, Scripts, Test Results, Reports" }
        "06_Infrastructure" { return "Infrastructure: Deployment, Docker, Caddy, CI/CD Runtime" }
        "07_Tools_Validation_Misc" { return "Tools, Validation Scripts, Root Config, Misc" }
        "08_Website" { return "Website: PlantProcess.Website" }
        default { return $Category }
    }
}

# ============================================================
# 5. Collect files
# ============================================================

Write-Step "============================================================"
Write-Step " PlantProcess IQ Ultimate Documentation + Deep Audit"
Write-Step "============================================================"
Write-Info "Repository root : $RepositoryRoot"
Write-Info "Output folder   : $OutputFolder"
Write-Info "Mask secrets    : $MaskSecrets"
Write-Info "Max file size   : $MaxFileSizeMB MB"
Write-Step "============================================================"

$maxFileSizeBytes = $MaxFileSizeMB * 1024 * 1024
$allCandidateFiles = Get-ChildItem -LiteralPath $RepositoryRoot -Recurse -File -Force

$includedFiles = New-Object System.Collections.Generic.List[object]
$skippedFiles = New-Object System.Collections.Generic.List[object]

foreach ($file in $allCandidateFiles) {
    $relativePath = Get-RelativePath -Root $RepositoryRoot -Path $file.FullName

    $shouldSkipByName = $false

    foreach ($pattern in $excludedFileNamePatterns) {
        if ($file.Name -like $pattern) {
            $shouldSkipByName = $true
            break
        }
    }

    if ($shouldSkipByName) {
        $skippedFiles.Add([pscustomobject]@{
            RelativePath = $relativePath
            Reason = "Excluded generated/backup/noisy file pattern"
            SizeBytes = $file.Length
        })
        continue
    }

    if (Test-IsUnderExcludedFolder -RelativePath $relativePath) {
        $skippedFiles.Add([pscustomobject]@{
            RelativePath = $relativePath
            Reason = "Excluded folder"
            SizeBytes = $file.Length
        })
        continue
    }

    if (-not $IncludeHidden -and (Test-IsHiddenPath -File $file)) {
        $specialAllowedHidden =
            ($file.Name -like ".env*") -or
            ($file.Name -eq ".dockerignore") -or
            ($file.Name -eq ".gitignore") -or
            ($file.Name -eq ".gitattributes")

        if (-not $specialAllowedHidden) {
            $skippedFiles.Add([pscustomobject]@{
                RelativePath = $relativePath
                Reason = "Hidden file/path"
                SizeBytes = $file.Length
            })
            continue
        }
    }

    if (-not $IncludeLockFiles -and ($lockFileNames -contains $file.Name)) {
        $skippedFiles.Add([pscustomobject]@{
            RelativePath = $relativePath
            Reason = "Lock file excluded"
            SizeBytes = $file.Length
        })
        continue
    }

    if ($file.Length -gt $maxFileSizeBytes) {
        $skippedFiles.Add([pscustomobject]@{
            RelativePath = $relativePath
            Reason = "File too large"
            SizeBytes = $file.Length
        })
        continue
    }

    $knownText = Test-IsKnownTextFile -File $file
    $probablyText = $false

    if ($knownText) {
        $probablyText = $true
    }
    else {
        $probablyText = Test-IsProbablyTextByContent -File $file
    }

    if (-not $probablyText) {
        $skippedFiles.Add([pscustomobject]@{
            RelativePath = $relativePath
            Reason = "Binary-like file"
            SizeBytes = $file.Length
        })
        continue
    }

    $lineCount = Get-FileLineCount -Path $file.FullName
    $category = Get-PrimaryCategory -RelativePath $relativePath
    $classification = Get-FileClassification -RelativePath $relativePath -File $file

    $includedFiles.Add([pscustomobject]@{
        FullName = $file.FullName
        RelativePath = $relativePath
        Category = $category
        CategoryTitle = Get-CategoryTitle -Category $category
        Classification = $classification
        Extension = $file.Extension
        Name = $file.Name
        SizeBytes = $file.Length
        SizeKB = [Math]::Round($file.Length / 1KB, 2)
        Lines = $lineCount
        LastWriteTime = $file.LastWriteTime
    })
}

$includedFiles = $includedFiles | Sort-Object Category, RelativePath
$skippedFiles = $skippedFiles | Sort-Object RelativePath

# ============================================================
# 6. Writer helpers
# ============================================================

function New-Utf8NoBomWriter {
    param([Parameter(Mandatory = $true)][string]$Path)

    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    return New-Object System.IO.StreamWriter($Path, $false, $utf8NoBom)
}

function Add-Separator {
    param([Parameter(Mandatory = $true)]$Writer)

    $Writer.WriteLine("")
    $Writer.WriteLine(("=" * 96))
    $Writer.WriteLine("")
}

function Add-SectionTitle {
    param(
        [Parameter(Mandatory = $true)]$Writer,
        [Parameter(Mandatory = $true)][string]$Title
    )

    $Writer.WriteLine(("=" * 96))
    $Writer.WriteLine($Title)
    $Writer.WriteLine(("=" * 96))
    $Writer.WriteLine("")
}

function Add-SubTitle {
    param(
        [Parameter(Mandatory = $true)]$Writer,
        [Parameter(Mandatory = $true)][string]$Title
    )

    $Writer.WriteLine(("-" * 96))
    $Writer.WriteLine($Title)
    $Writer.WriteLine(("-" * 96))
    $Writer.WriteLine("")
}

function Read-FileContentSafe {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$RelativePath
    )

    try {
        $content = [System.IO.File]::ReadAllText($Path, [System.Text.Encoding]::UTF8)
        return Protect-SecretContent -Content $content -RelativePath $RelativePath
    }
    catch {
        try {
            $content = Get-Content -LiteralPath $Path -Raw -ErrorAction Stop
            return Protect-SecretContent -Content $content -RelativePath $RelativePath
        }
        catch {
            return "[READ_ERROR] Unable to read file content. $($_.Exception.Message)"
        }
    }
}

function Write-Header {
    param(
        [Parameter(Mandatory = $true)]$Writer,
        [Parameter(Mandatory = $true)][string]$Title,
        [Parameter(Mandatory = $true)][object[]]$Files
    )

    $totalLines = 0
    $totalBytes = 0

    foreach ($file in $Files) {
        if ($file.Lines -gt 0) { $totalLines += $file.Lines }
        $totalBytes += $file.SizeBytes
    }

    Add-SectionTitle -Writer $Writer -Title $Title

    $Writer.WriteLine("Product Name          : PlantProcess IQ")
    $Writer.WriteLine("Generated At          : $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
    $Writer.WriteLine("Repository Root       : $RepositoryRoot")
    $Writer.WriteLine("Output Folder         : $OutputFolder")
    $Writer.WriteLine("Included Files        : $($Files.Count)")
    $Writer.WriteLine("Total Lines           : $totalLines")
    $Writer.WriteLine("Total Size            : $([Math]::Round($totalBytes / 1MB, 3)) MB")
    $Writer.WriteLine("Mask Secrets          : $MaskSecrets")
    $Writer.WriteLine("Max File Size         : $MaxFileSizeMB MB")
    $Writer.WriteLine("PowerShell Version    : $($PSVersionTable.PSVersion)")
    $Writer.WriteLine("Machine Name          : $env:COMPUTERNAME")
    $Writer.WriteLine("User Name             : $env:USERNAME")
    $Writer.WriteLine("")
}

function Write-TableOfContents {
    param(
        [Parameter(Mandatory = $true)]$Writer,
        [Parameter(Mandatory = $true)][object[]]$Files
    )

    Add-SubTitle -Writer $Writer -Title "TABLE OF CONTENTS"

    $index = 1
    foreach ($file in $Files) {
        $Writer.WriteLine(("{0,5}. {1} | {2} | {3} lines | {4} KB" -f $index, $file.RelativePath, $file.Classification, $file.Lines, $file.SizeKB))
        $index++
    }

    $Writer.WriteLine("")
}

function Write-CategorySummary {
    param(
        [Parameter(Mandatory = $true)]$Writer,
        [Parameter(Mandatory = $true)][object[]]$Files
    )

    Add-SubTitle -Writer $Writer -Title "CATEGORY SUMMARY"

    $groups = $Files | Group-Object Category | Sort-Object Name

    foreach ($group in $groups) {
        $categoryTitle = Get-CategoryTitle -Category $group.Name
        $lines = ($group.Group | Measure-Object -Property Lines -Sum).Sum
        $bytes = ($group.Group | Measure-Object -Property SizeBytes -Sum).Sum

        $Writer.WriteLine(("{0,-32} Files: {1,5} | Lines: {2,8} | Size: {3,8} MB" -f $categoryTitle, $group.Count, $lines, [Math]::Round($bytes / 1MB, 3)))
    }

    $Writer.WriteLine("")
}

function Write-ClassificationSummary {
    param(
        [Parameter(Mandatory = $true)]$Writer,
        [Parameter(Mandatory = $true)][object[]]$Files
    )

    Add-SubTitle -Writer $Writer -Title "CLASSIFICATION SUMMARY"

    $groups = $Files | Group-Object Classification | Sort-Object Name

    foreach ($group in $groups) {
        $lines = ($group.Group | Measure-Object -Property Lines -Sum).Sum
        $Writer.WriteLine(("{0,-42} Files: {1,5} | Lines: {2,8}" -f $group.Name, $group.Count, $lines))
    }

    $Writer.WriteLine("")
}

function Write-ExtensionSummary {
    param(
        [Parameter(Mandatory = $true)]$Writer,
        [Parameter(Mandatory = $true)][object[]]$Files
    )

    Add-SubTitle -Writer $Writer -Title "EXTENSION / SPECIAL FILE SUMMARY"

    $normalized = foreach ($file in $Files) {
        $extValue = $file.Extension
        if ([string]::IsNullOrWhiteSpace($extValue)) {
            $extValue = "[NO_EXTENSION]"
        }

        [pscustomobject]@{
            Extension = $extValue
            Lines = $file.Lines
            SizeBytes = $file.SizeBytes
        }
    }

    $groups = $normalized | Group-Object Extension | Sort-Object Name

    foreach ($group in $groups) {
        $lines = ($group.Group | Measure-Object -Property Lines -Sum).Sum
        $bytes = ($group.Group | Measure-Object -Property SizeBytes -Sum).Sum
        $Writer.WriteLine(("{0,-20} Files: {1,5} | Lines: {2,8} | Size: {3,8} KB" -f $group.Name, $group.Count, $lines, [Math]::Round($bytes / 1KB, 2)))
    }

    $Writer.WriteLine("")
}

function Write-DirectoryTree {
    param(
        [Parameter(Mandatory = $true)]$Writer,
        [Parameter(Mandatory = $true)][object[]]$Files
    )

    Add-SubTitle -Writer $Writer -Title "INCLUDED FILE TREE"

    foreach ($file in $Files) {
        $Writer.WriteLine(" - $($file.RelativePath)")
    }

    $Writer.WriteLine("")
}

function Write-FilesWithContent {
    param(
        [Parameter(Mandatory = $true)]$Writer,
        [Parameter(Mandatory = $true)][object[]]$Files
    )

    Add-SectionTitle -Writer $Writer -Title "FULL FILE CONTENT"

    foreach ($file in $Files) {
        $Writer.WriteLine("[FILE_START]")
        $Writer.WriteLine("[METADATA: Path='$($file.RelativePath)', Category='$($file.CategoryTitle)', Classification='$($file.Classification)', Lines=$($file.Lines), SizeKB=$($file.SizeKB), Modified='$($file.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"))']")
        $Writer.WriteLine("")

        $content = Read-FileContentSafe -Path $file.FullName -RelativePath $file.RelativePath
        $Writer.WriteLine($content)

        if (-not $content.EndsWith("`n")) {
            $Writer.WriteLine("")
        }

        $Writer.WriteLine("[FILE_END]")
        $Writer.WriteLine("")
    }
}

function Write-SkippedFiles {
    param(
        [Parameter(Mandatory = $true)]$Writer,
        [Parameter(Mandatory = $true)][object[]]$Skipped
    )

    Add-SectionTitle -Writer $Writer -Title "SKIPPED FILES"

    if ($Skipped.Count -eq 0) {
        $Writer.WriteLine("No files skipped.")
        $Writer.WriteLine("")
        return
    }

    foreach ($file in $Skipped) {
        $Writer.WriteLine("$($file.RelativePath) | $($file.Reason) | $([Math]::Round($file.SizeBytes / 1KB, 2)) KB")
    }

    $Writer.WriteLine("")
}

function Write-DocumentationFile {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Title,
        [Parameter(Mandatory = $true)][object[]]$Files,
        [object[]]$Skipped = @(),
        [switch]$IncludeSkipped
    )

    $writer = New-Utf8NoBomWriter -Path $Path

    try {
        Write-Header -Writer $writer -Title $Title -Files $Files
        Write-CategorySummary -Writer $writer -Files $Files
        Write-ClassificationSummary -Writer $writer -Files $Files
        Write-ExtensionSummary -Writer $writer -Files $Files
        Write-TableOfContents -Writer $writer -Files $Files
        Write-DirectoryTree -Writer $writer -Files $Files

        if ($IncludeSkipped) {
            Write-SkippedFiles -Writer $writer -Skipped $Skipped
        }

        Write-FilesWithContent -Writer $writer -Files $Files
    }
    finally {
        $writer.Dispose()
    }
}

# ============================================================
# 7. Generate category files
# ============================================================

$categoryDefinitions = @(
    [pscustomobject]@{ Key = "01_Backend_Core"; FilePrefix = "01_Backend_Core"; Title = "PLANTPROCESS IQ BACKEND CORE AUDIT - API / APPLICATION / DOMAIN / INFRASTRUCTURE / WORKERS" },
    [pscustomobject]@{ Key = "02_Backend_Database"; FilePrefix = "02_Backend_Database"; Title = "PLANTPROCESS IQ BACKEND DATABASE AUDIT - SCRIPTS / SEED / VIEWS" },
    [pscustomobject]@{ Key = "03_Backend_Tests"; FilePrefix = "03_Backend_Tests"; Title = "PLANTPROCESS IQ BACKEND TESTS AUDIT" },
    [pscustomobject]@{ Key = "04_Frontend_App"; FilePrefix = "04_Frontend_App"; Title = "PLANTPROCESS IQ FRONTEND APP AUDIT - CORE REACT SOURCE" },
    [pscustomobject]@{ Key = "05_Frontend_Misc"; FilePrefix = "05_Frontend_Misc"; Title = "PLANTPROCESS IQ FRONTEND MISC AUDIT - E2E / SCRIPTS / TEST RESULTS" },
    [pscustomobject]@{ Key = "06_Infrastructure"; FilePrefix = "06_Infrastructure"; Title = "PLANTPROCESS IQ INFRASTRUCTURE AUDIT - DEPLOYMENT / DOCKER / CADDY / CI-CD" },
    [pscustomobject]@{ Key = "07_Tools_Validation_Misc"; FilePrefix = "07_Tools_Validation_Misc"; Title = "PLANTPROCESS IQ TOOLS / VALIDATION / ROOT MISC AUDIT" },
    [pscustomobject]@{ Key = "08_Website"; FilePrefix = "08_Website"; Title = "PLANTPROCESS IQ WEBSITE AUDIT - PLANTPROCESS.WEBSITE" }
)

foreach ($category in $categoryDefinitions) {
    $categoryFiles = @($includedFiles | Where-Object { $_.Category -eq $category.Key })

    $outputPath = Join-Path $OutputFolder ("{0}_{1}.txt" -f $category.FilePrefix, $timestamp)

    Write-DocumentationFile `
        -Path $outputPath `
        -Title $category.Title `
        -Files $categoryFiles

    Write-Ok "Generated $($category.FilePrefix): $($categoryFiles.Count) files"
}

# ============================================================
# 8. Generate master index
# ============================================================

$masterIndexPath = Join-Path $OutputFolder ("00_Master_Index_{0}.txt" -f $timestamp)
$masterWriter = New-Utf8NoBomWriter -Path $masterIndexPath

try {
    Write-Header -Writer $masterWriter -Title "PLANTPROCESS IQ MASTER INDEX - ULTIMATE AUDIT PACKAGE" -Files $includedFiles
    Write-CategorySummary -Writer $masterWriter -Files $includedFiles
    Write-ClassificationSummary -Writer $masterWriter -Files $includedFiles
    Write-ExtensionSummary -Writer $masterWriter -Files $includedFiles

    Add-SectionTitle -Writer $masterWriter -Title "CATEGORY OUTPUT FILES"

    foreach ($category in $categoryDefinitions) {
        $count = @($includedFiles | Where-Object { $_.Category -eq $category.Key }).Count
        $masterWriter.WriteLine("$($category.FilePrefix)_$timestamp.txt | $($category.Title) | Files: $count")
    }

    $masterWriter.WriteLine("09_FullStack_Combined_$timestamp.txt | FULL COMBINED SOURCE DOCUMENTATION | Files: $($includedFiles.Count)")
    $masterWriter.WriteLine("manifest_$timestamp.csv | Machine-readable manifest")
    $masterWriter.WriteLine("manifest_$timestamp.json | Machine-readable manifest")
    $masterWriter.WriteLine("")

    Write-TableOfContents -Writer $masterWriter -Files $includedFiles
    Write-SkippedFiles -Writer $masterWriter -Skipped $skippedFiles
}
finally {
    $masterWriter.Dispose()
}

Write-Ok "Generated Master Index"

# ============================================================
# 9. Generate combined full-stack file
# ============================================================

$combinedPath = Join-Path $OutputFolder ("09_FullStack_Combined_{0}.txt" -f $timestamp)

Write-DocumentationFile `
    -Path $combinedPath `
    -Title "PLANTPROCESS IQ FULL-STACK COMBINED SOURCE DOCUMENTATION - ULTIMATE AUDIT" `
    -Files $includedFiles `
    -Skipped $skippedFiles `
    -IncludeSkipped

Write-Ok "Generated FullStack Combined: $($includedFiles.Count) files"

# ============================================================
# 10. Generate machine-readable manifest
# ============================================================

$manifestCsvPath = Join-Path $OutputFolder ("manifest_{0}.csv" -f $timestamp)
$manifestJsonPath = Join-Path $OutputFolder ("manifest_{0}.json" -f $timestamp)

$includedFiles |
    Select-Object RelativePath, Category, CategoryTitle, Classification, Extension, Lines, SizeBytes, SizeKB, LastWriteTime |
    Export-Csv -LiteralPath $manifestCsvPath -NoTypeInformation -Encoding UTF8

$includedFiles |
    Select-Object RelativePath, Category, CategoryTitle, Classification, Extension, Lines, SizeBytes, SizeKB, LastWriteTime |
    ConvertTo-Json -Depth 5 |
    Set-Content -LiteralPath $manifestJsonPath -Encoding UTF8

Write-Ok "Generated manifest CSV"
Write-Ok "Generated manifest JSON"

# ============================================================
# 11. Console summary
# ============================================================

$totalLines = ($includedFiles | Measure-Object -Property Lines -Sum).Sum
$totalBytes = ($includedFiles | Measure-Object -Property SizeBytes -Sum).Sum

Write-Host ""
Write-Step "============================================================"
Write-Ok "PlantProcess IQ Ultimate Audit generated successfully."
Write-Step "============================================================"
Write-Info "Output folder        : $OutputFolder"
Write-Info "Included files       : $($includedFiles.Count)"
Write-Info "Skipped files        : $($skippedFiles.Count)"
Write-Info "Total lines          : $totalLines"
Write-Info "Total size           : $([Math]::Round($totalBytes / 1MB, 3)) MB"
Write-Info "Master index         : $masterIndexPath"
Write-Info "Full combined        : $combinedPath"
Write-Info "Manifest CSV         : $manifestCsvPath"
Write-Info "Manifest JSON        : $manifestJsonPath"
Write-Step "============================================================"

Write-Host ""
Write-Host "Generated category files:" -ForegroundColor Cyan
foreach ($category in $categoryDefinitions) {
    $count = @($includedFiles | Where-Object { $_.Category -eq $category.Key }).Count
    Write-Host (" - {0}: {1} files" -f (Get-CategoryTitle -Category $category.Key), $count) -ForegroundColor Yellow
}

Write-Host ""

if ($OpenAfterGeneration) {
    Invoke-Item $OutputFolder
}
#requires -Version 5.1
<#
.SYNOPSIS
    PlantProcess IQ Ultimate Documentation + Deep AI Audit Generator (v2.1).

.DESCRIPTION
    Produces a professional, AI-friendly, evidence-grade documentation + audit
    package for the PlantProcess IQ full stack. This is a hardened rewrite of the
    original GeneratePlantProcessIQ_UltimateAudit.ps1 with the following goals:

      - Export every relevant source / config / test / deploy / CI / docs file.
      - NEVER silently drop important files. Exclusions are split into two tiers:
          * Hard-excluded segments  : unambiguously generated (bin, obj, node_modules, .git ...).
          * Runtime-data segments    : runtime dumps only (.runtime, logs, pgdata ...).
        Bare collision-prone names (postgres, reports, build...) are no longer
        excluded globally, so demo-source seed scripts under Infrastructure survive.
      - A force-include rule set guarantees CI/deploy artefacts are always captured
        (.github/workflows, Jenkinsfile, Dockerfile, Caddyfile, docker-compose*,
         .env*, demo-source *.sql) even when a parent segment looks "hidden".
      - Empty categories render as empty sections instead of crashing the run
        (root cause of the original "empty array" bind error).
      - Secret masking is OPT-IN (default OFF: -MaskSecrets $true to enable). When on,
        it is structure-preserving (key=value, JSON, YAML, ADO.NET / libpq connection
        strings, URI credentials) so the AI auditor sees the SHAPE of config without
        live secrets. With masking OFF, .env / appsettings / Jenkinsfile / compose /
        Caddyfile / Dockerfile / .github workflows export verbatim (clear text).
      - A built-in Audit-Signal scanner greps included files for PlantProcess-specific
        red flags (CI false-pass, wrong connection key, hardcoded signing key /
        server IP, dev-seed endpoints, gate-closing shims) and reports file:line.
      - Rich manifests (CSV + JSON) including SHA-256 per file and detected signals.
      - Per-file failures are isolated; one unreadable file never kills the run.
      - Demo schema/data fixtures under deploy\fixtures\demo are exported into a
        dedicated DEMO SQL Data Seed document instead of 07_Tools_Validation_Misc.

.OUTPUTS
    A timestamped folder under <Repo>\Documentation\UltimateAudit_<timestamp> containing:
      00_Master_Index_*.txt
      01_Backend_Core_*.txt
      02_Backend_Database_*.txt
      03_Backend_Tests_*.txt
      04_Frontend_App_*.txt
      05_Frontend_Misc_*.txt
      06_Infrastructure_*.txt
      07_Tools_Validation_Misc_*.txt
      07A_DEMO_SQL_Data_Seed_*.txt
      08_Website_*.txt
      09_FullStack_Combined_*.txt
      10_Audit_Signals_*.txt
      manifest_*.csv
      manifest_*.json

.NOTES
    PowerShell 5.1 compatible. UTF-8 (no BOM), LF-friendly output via .NET writers.
    Author purpose: PlantProcess IQ internal documentation + AI audit upload.
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

    [bool]$MaskSecrets = $false,
    [bool]$ComputeHashes = $true,
    [bool]$RunAuditSignals = $true,
    [bool]$RenderTree = $true
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

function Write-Warn2 {
    param([Parameter(Mandatory = $true)][string]$Message)
    Write-Host $Message -ForegroundColor DarkYellow
}

# ============================================================
# 1. Path resolution
# ============================================================

$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path

function Resolve-RepositoryRoot {
    param([Parameter(Mandatory = $true)][string]$StartPath)

    $current = $StartPath

    for ($i = 0; $i -lt 10; $i++) {
        if ([string]::IsNullOrWhiteSpace($current)) { break }

        $backendCandidate = Join-Path $current "Backend"
        $frontendCandidate = Join-Path $current "Frontend"

        if ((Test-Path -LiteralPath $backendCandidate) -and (Test-Path -LiteralPath $frontendCandidate)) {
            return $current
        }

        $parent = Split-Path -Parent $current
        if ($parent -eq $current) { break }
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
# 2. Exclusion / inclusion rule sets
# ============================================================

# Tier 1: hard-excluded directory segments (unambiguously generated / dependency).
# A path is excluded if ANY of its segments equals one of these (case-insensitive).
$hardExcludedSegments = @(
    ".git", ".vs", ".idea", ".vscode",
    "bin", "obj",
    "node_modules", "dist", "build", "coverage",
    ".vite", ".cache", ".nuget", ".sonarqube",
    "TestResults", "test-results", "playwright-report",
    "packages",
    # The generator's own output must never be re-ingested.
    "Documentation"
)

# Tier 2: runtime-data directory segments. Excluded ONLY as bare segments, used for
# live runtime dumps. NOTE: collision-prone names (postgres, reports, backups) are
# intentionally NOT here -- they are handled by binary/size detection so that
# legitimate source (e.g. Infrastructure\demo-sources\postgres\init\*.sql) survives.
$runtimeDataSegments = @(
    ".runtime",
    "logs",
    "app-dumps",
    "dumps",
    "pgdata",
    "pg_data"
)

if (-not $IncludeMigrations) {
    $runtimeDataSegments += "migrations"
    $runtimeDataSegments += "Migrations"
}

$allExcludedSegments = @($hardExcludedSegments + $runtimeDataSegments)

# Hidden segments that ARE allowed (config / CI living under dot-folders or dot-names).
$allowedHiddenSegments = @(
    ".github", ".config", ".husky", ".changeset"
)

$allowedHiddenFilePatterns = @(
    ".env*",
    ".dockerignore",
    ".gitignore",
    ".gitattributes",
    ".editorconfig",
    ".npmrc",
    ".nvmrc",
    ".prettierrc*",
    ".eslintrc*",
    ".eslintignore"
)

# Force-include: even if a parent segment looks hidden, these files are always kept
# (subject only to size + binary checks). Critical for CI/deploy evidence.
$forceIncludeFilePatterns = @(
    "Dockerfile", "dockerfile", "Caddyfile", "caddyfile", "Jenkinsfile", "Makefile",
    "docker-compose*.yml", "docker-compose*.yaml", "compose*.yml", "compose*.yaml",
    ".env*", "*.env",
    "*.sql"
)

# Relative-path globs that force inclusion regardless of hidden/segment heuristics.
$forceIncludeRelGlobs = @(
    ".github\workflows\*",
    "Infrastructure\*",
    "Backend\database\*",
    "deploy\fixtures\demo\*"
)

$lockFileNames = @(
    "package-lock.json",
    "yarn.lock",
    "pnpm-lock.yaml",
    "composer.lock",
    "packages.lock.json"
)

$excludedFileNamePatterns = @(
    "*.tmp", "*.user", "*.suo", "*.cache", "*.tsbuildinfo",
    "*.log", "*.bak", "*.backup", "*.old",
    "*.zip", "*.7z", "*.rar", "*.gz", "*.tar",
    "*.db", "*.sqlite", "*.sqlite3",
    "*.dll", "*.exe", "*.pdb", "*.nupkg",
    "*.png", "*.jpg", "*.jpeg", "*.gif", "*.ico", "*.webp", "*.bmp",
    "*.woff", "*.woff2", "*.ttf", "*.eot", "*.otf",
    "*.pdf", "*.xlsx", "*.docx", "*.pptx"
)

$knownTextExtensions = @(
    ".cs", ".csproj", ".props", ".targets", ".sln",
    ".ts", ".tsx", ".js", ".jsx", ".cjs", ".mjs",
    ".css", ".scss", ".sass", ".less",
    ".html", ".htm", ".sql",
    ".json", ".jsonc", ".yml", ".yaml",
    ".md", ".mdx", ".txt", ".xml", ".csv",
    ".config", ".editorconfig", ".env", ".example",
    ".sh", ".bash", ".ps1", ".psm1", ".psd1",
    ".dockerignore", ".gitignore", ".gitattributes",
    ".http", ".rest", ".cshtml", ".razor", ".tf", ".tfvars", ".toml", ".ini"
)

$specialTextFileNames = @(
    "Dockerfile", "dockerfile", "Caddyfile", "caddyfile",
    ".env", ".env.example", ".env.development", ".env.local", ".env.production",
    ".dockerignore", ".gitignore", ".gitattributes",
    "Jenkinsfile", "Makefile", "LICENSE", "README", "Procfile"
)

# ============================================================
# 3. Rule engine
# ============================================================

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

function Get-PathSegments {
    param([Parameter(Mandatory = $true)][string]$RelativePath)
    return @($RelativePath -split '[\\/]' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
}

function Test-MatchesAnyPattern {
    param(
        [Parameter(Mandatory = $true)][string]$Value,
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][string[]]$Patterns
    )
    foreach ($pattern in $Patterns) {
        if ($Value -like $pattern) { return $true }
    }
    return $false
}

function Test-RelMatchesAnyGlob {
    param(
        [Parameter(Mandatory = $true)][string]$RelativePath,
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][string[]]$Globs
    )
    foreach ($glob in $Globs) {
        if ($RelativePath -like $glob) { return $true }
    }
    return $false
}

function Test-IsUnderExcludedFolder {
    param([Parameter(Mandatory = $true)][string]$RelativePath)

    $segments = @(Get-PathSegments -RelativePath $RelativePath)
    foreach ($segment in $segments) {
        foreach ($excluded in $allExcludedSegments) {
            if ($segment -ieq $excluded) { return $true }
        }
    }
    return $false
}

function Test-IsForceIncluded {
    param(
        [Parameter(Mandatory = $true)][System.IO.FileInfo]$File,
        [Parameter(Mandatory = $true)][string]$RelativePath
    )

    if (Test-MatchesAnyPattern -Value $File.Name -Patterns $forceIncludeFilePatterns) { return $true }
    if (Test-RelMatchesAnyGlob -RelativePath $RelativePath -Globs $forceIncludeRelGlobs) { return $true }
    return $false
}

function Test-IsHiddenPath {
    param(
        [Parameter(Mandatory = $true)][System.IO.FileInfo]$File,
        [Parameter(Mandatory = $true)][string]$RelativePath
    )

    # Honor the explicit Hidden attribute, but allow whitelisted config files.
    if (($File.Attributes -band [System.IO.FileAttributes]::Hidden) -ne 0) {
        if (-not (Test-MatchesAnyPattern -Value $File.Name -Patterns $allowedHiddenFilePatterns)) {
            return $true
        }
    }

    $segments = @(Get-PathSegments -RelativePath $RelativePath)

    for ($i = 0; $i -lt $segments.Count; $i++) {
        $segment = $segments[$i]
        if (-not $segment.StartsWith(".")) { continue }
        if ($segment -in @(".", "..")) { continue }

        $isLastSegment = ($i -eq ($segments.Count - 1))

        if ($isLastSegment) {
            # Dot-named file (e.g. .editorconfig): allow if whitelisted.
            if (Test-MatchesAnyPattern -Value $segment -Patterns $allowedHiddenFilePatterns) { continue }
        }
        else {
            # Dot-named folder: allow if whitelisted (.github, .config ...).
            $allowedFolder = $false
            foreach ($allowed in $allowedHiddenSegments) {
                if ($segment -ieq $allowed) { $allowedFolder = $true; break }
            }
            if ($allowedFolder) { continue }
        }

        return $true
    }

    return $false
}

function Test-IsKnownTextFile {
    param([Parameter(Mandatory = $true)][System.IO.FileInfo]$File)

    if ($specialTextFileNames -contains $File.Name) { return $true }
    if ($File.Name -like ".env*") { return $true }
    if ($File.Name -like "docker-compose*.yml" -or $File.Name -like "docker-compose*.yaml") { return $true }
    if ($File.Name -like "compose*.yml" -or $File.Name -like "compose*.yaml") { return $true }
    if ($File.Extension -and ($knownTextExtensions -contains $File.Extension)) { return $true }
    return $false
}

function Test-IsProbablyTextByContent {
    param([Parameter(Mandatory = $true)][System.IO.FileInfo]$File)

    if ($IncludeBinaryLikeFiles) { return $true }
    if ($File.Length -eq 0) { return $true }

    $maxProbeBytes = 4096
    $bufferLength = [Math]::Min($maxProbeBytes, [int]$File.Length)
    $buffer = New-Object byte[] $bufferLength
    $stream = [System.IO.File]::OpenRead($File.FullName)

    try {
        $read = $stream.Read($buffer, 0, $bufferLength)
        $controlCount = 0

        for ($i = 0; $i -lt $read; $i++) {
            $b = $buffer[$i]
            if ($b -eq 0) { return $false }
            # Count non-text control chars (allow tab/lf/cr/ff/esc).
            if ($b -lt 9 -or ($b -gt 13 -and $b -lt 32 -and $b -ne 27)) { $controlCount++ }
        }

        if ($read -gt 0 -and (($controlCount / [double]$read) -gt 0.30)) { return $false }
        return $true
    }
    finally {
        $stream.Dispose()
    }
}

# ============================================================
# 4. Secret masking (structure-preserving)
# ============================================================

$secretKeyAlternation = 'password|passwd|pwd|secret|token|apikey|api[_-]?key|signing[_-]?key|jwt[_-]?key|private[_-]?key|access[_-]?key|client[_-]?secret|bearer|authorization|auth[_-]?token|connection[_-]?string|connectionstring|conn[_-]?str'

function Test-IsSensitiveFile {
    param([Parameter(Mandatory = $true)][string]$RelativePath)

    return (
        ($RelativePath -match '(^|[\\/])\.env') -or
        ($RelativePath -match 'appsettings') -or
        ($RelativePath -match 'launchSettings') -or
        ($RelativePath -match 'docker-compose') -or
        ($RelativePath -match '(^|[\\/])compose[^\\/]*\.ya?ml$') -or
        ($RelativePath -match 'Caddyfile') -or
        ($RelativePath -match 'Dockerfile') -or
        ($RelativePath -match 'Jenkinsfile') -or
        ($RelativePath -match '(^|[\\/])\.github[\\/]workflows[\\/]') -or
        ($RelativePath -match '\.tfvars$') -or
        ($RelativePath -match 'secrets?')
    )
}

function Protect-SecretContent {
    param(
        [Parameter(Mandatory = $true)][AllowEmptyString()][string]$Content,
        [Parameter(Mandatory = $true)][string]$RelativePath
    )

    if (-not $MaskSecrets) { return $Content }
    if (-not (Test-IsSensitiveFile -RelativePath $RelativePath)) { return $Content }
    if ([string]::IsNullOrEmpty($Content)) { return $Content }

    $masked = $Content
    $mask = '[MASKED_FOR_AI_AUDIT]'

    # KEY=value (env / properties / shell). Also covers ConnectionStrings__Xyz=...
    $masked = [regex]::Replace(
        $masked,
        ('(?im)^(\s*(?:export\s+)?[^#\r\n=]*?(?:' + $secretKeyAlternation + ')[^=\r\n]*?\s*=\s*)(.+)$'),
        ('$1' + $mask)
    )

    # JSON: "Password": "value"  (key may be nested e.g. ConnectionStrings:Default)
    $masked = [regex]::Replace(
        $masked,
        ('(?im)("(?:[^"\r\n]*?(?:' + $secretKeyAlternation + ')[^"\r\n]*?)"\s*:\s*")([^"\r\n]*)(")'),
        ('$1' + $mask + '$3')
    )

    # YAML: password: value
    $masked = [regex]::Replace(
        $masked,
        ('(?im)^(\s*[^#\r\n:]*?(?:' + $secretKeyAlternation + ')[^:\r\n]*?\s*:\s*)(?!\s*$)([^\r\n#]+)$'),
        ('$1' + $mask)
    )

    # ADO.NET / libpq inline credentials inside a connection string.
    $masked = [regex]::Replace($masked, '(?i)(Password\s*=\s*)([^;"\r\n]+)', ('$1' + $mask))
    $masked = [regex]::Replace($masked, '(?i)(Pwd\s*=\s*)([^;"\r\n]+)', ('$1' + $mask))

    # URI credentials: scheme://user:pass@host  -> scheme://user:[MASKED]@host
    $masked = [regex]::Replace(
        $masked,
        '(?i)([a-z][a-z0-9+\-.]*://[^\s:/@]+:)([^\s:/@]+)(@)',
        ('$1' + $mask + '$3')
    )

    return $masked
}

# ============================================================
# 5. Hashing
# ============================================================

function Get-FileSha256 {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not $ComputeHashes) { return "" }
    try {
        return (Get-FileHash -LiteralPath $Path -Algorithm SHA256 -ErrorAction Stop).Hash
    }
    catch {
        return ""
    }
}

# ============================================================
# 6. Classification + categorization
# ============================================================

function Test-IsDemoSeedRelativePath {
    param([Parameter(Mandatory = $true)][string]$RelativePath)

    $rp = $RelativePath -replace '/', '\'
    return (
        $rp -match '^deploy\\fixtures\\demo\\' -and
        $rp -match '\.(sql|csv)$'
    )
}

function Get-FileClassification {
    param(
        [Parameter(Mandatory = $true)][string]$RelativePath,
        [Parameter(Mandatory = $true)][System.IO.FileInfo]$File
    )

    $name = $File.Name
    $ext = $File.Extension

    if ($RelativePath -match '(^|[\\/])tools[\\/]') { return "Tooling Script" }
    if ($RelativePath -match '(^|[\\/])Validation[\\/]') { return "Validation Script" }
    if ($RelativePath -match '(^|[\\/])\.github[\\/]workflows[\\/]') { return "GitHub Actions Workflow" }
    if (Test-IsDemoSeedRelativePath -RelativePath $RelativePath) {
        if ($ext -eq ".sql") { return "Demo SQL Seed Script" }
        if ($ext -eq ".csv") { return "Demo CSV Seed Data" }
        return "Demo Source / Seed"
    }
    if ($RelativePath -match '(^|[\\/])demo-sources[\\/]') { return "Demo Source / Seed" }

    if ($name -eq "Dockerfile") { return "Dockerfile" }
    if ($name -eq "Caddyfile") { return "Caddyfile" }
    if ($name -eq "Jenkinsfile") { return "Jenkins Pipeline" }
    if ($name -like ".env*") { return "Environment Config" }
    if ($name -like "docker-compose*" -or $name -like "compose*.yml" -or $name -like "compose*.yaml") { return "Docker Compose" }

    if ($ext -eq ".sln") { return ".NET Solution" }
    if ($ext -eq ".csproj") { return ".NET Project File" }
    if ($ext -eq ".cs") { return "C# Source" }
    if ($ext -eq ".sql") { return "SQL Script" }
    if ($ext -in @(".ts", ".tsx")) { return "TypeScript Source" }
    if ($ext -in @(".js", ".jsx", ".cjs", ".mjs")) { return "JavaScript Source" }
    if ($ext -in @(".css", ".scss", ".sass", ".less")) { return "Stylesheet" }
    if ($ext -in @(".md", ".mdx")) { return "Markdown Documentation" }
    if ($ext -eq ".json" -or $ext -eq ".jsonc") { return "JSON Configuration" }
    if ($ext -in @(".yml", ".yaml")) { return "YAML Configuration" }
    if ($ext -in @(".ps1", ".psm1", ".psd1")) { return "PowerShell Script" }
    if ($ext -in @(".sh", ".bash")) { return "Shell Script" }
    if ($ext -in @(".html", ".htm", ".cshtml", ".razor")) { return "Markup / View" }

    return "Other Text File"
}

function Get-PrimaryCategory {
    param([Parameter(Mandatory = $true)][string]$RelativePath)

    $rp = $RelativePath -replace '/', '\'

    if (
        $rp -match '^Backend\\PlantProcess\.Api\\' -or
        $rp -match '^Backend\\PlantProcess\.Application\\' -or
        $rp -match '^Backend\\PlantProcess\.Analytics\.' -or
        $rp -match '^Backend\\PlantProcess\.Domain\\' -or
        $rp -match '^Backend\\PlantProcess\.Infrastructure\\' -or
        $rp -match '^Backend\\PlantProcess\.Workers\\' -or
        $rp -match '^Backend\\PlantProcessIQ\.sln$'
    ) {
        return "01_Backend_Core"
    }

    if ($rp -match '^Backend\\database\\') { return "02_Backend_Database" }
    if ($rp -match '^Backend\\tests\\') { return "03_Backend_Tests" }

    # Keep large prepared demo schema/data fixtures out of the general tools/misc
    # document. This dedicated category includes the SQL seed scripts and the two
    # CSV fixture datasets used by the Excel demo sources.
    if (Test-IsDemoSeedRelativePath -RelativePath $rp) {
        return "07A_DEMO_SQL_Data_Seed"
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

    if ($rp -match '^Infrastructure\\') { return "06_Infrastructure" }
    if ($rp -match '^\.github\\') { return "06_Infrastructure" }
    if ($rp -match '(^|\\)Jenkinsfile$') { return "06_Infrastructure" }
    if ($rp -match '^docker-compose') { return "06_Infrastructure" }
    if ($rp -match '(^|\\)Dockerfile$') { return "06_Infrastructure" }
    if ($rp -match '(^|\\)Caddyfile$') { return "06_Infrastructure" }

    if ($rp -match '^Website\\') { return "08_Website" }

    if (
        $rp -match '^tools\\' -or
        $rp -match '^Validation\\' -or
        $rp -match '^README' -or
        $rp -match '^\.env' -or
        $rp -match '^docs\\'
    ) {
        return "07_Tools_Validation_Misc"
    }

    return "07_Tools_Validation_Misc"
}

function Get-CategoryTitle {
    param([Parameter(Mandatory = $true)][string]$Category)

    switch ($Category) {
        "01_Backend_Core" { return "Backend Core: API, Application, Analytics, Domain, Infrastructure, Workers" }
        "02_Backend_Database" { return "Backend Database: Scripts, Seed, Views" }
        "03_Backend_Tests" { return "Backend Tests" }
        "04_Frontend_App" { return "Frontend App: PlantProcess.Web Core Source (HMI)" }
        "05_Frontend_Misc" { return "Frontend Misc: E2E, Scripts, Test Results, Reports" }
        "06_Infrastructure" { return "Infrastructure: Deployment, Docker, Caddy, CI/CD (.github, Jenkins)" }
        "07_Tools_Validation_Misc" { return "Tools, Validation Scripts, Root Config, Docs, Misc" }
        "07A_DEMO_SQL_Data_Seed" { return "DEMO SQL Data Seed: Schema, SQL Data, Excel/CSV Fixtures" }
        "08_Website" { return "Website: PlantProcess.Website" }
        default { return $Category }
    }
}

# ============================================================
# 7. Audit-signal scanner (PlantProcess-specific red flags)
# ============================================================
#
# Each rule: Name, Severity (CRIT/WARN/INFO), Regex (line-level), and an optional
# FileGlob to scope the rule. Extend freely -- this is the project conscience.

$auditSignalRules = @(
    [pscustomobject]@{
        Name = "CI: frontend tests enumerated, not executed (--list)"
        Severity = "CRIT"
        Regex = '(?i)(vitest|playwright|npm\s+(run\s+)?test)[^\r\n]*--list'
        FileGlob = '*'
    },
    [pscustomobject]@{
        Name = "CI: catchError forcing SUCCESS"
        Severity = "CRIT"
        Regex = "(?i)catchError[^\r\n]*SUCCESS|buildResult\s*[:=]\s*['""]SUCCESS"
        FileGlob = '*'
    },
    [pscustomobject]@{
        Name = "Config: wrong connection-string key (__DefaultConnection)"
        Severity = "CRIT"
        Regex = '(?i)ConnectionStrings__DefaultConnection|ConnectionStrings:DefaultConnection'
        FileGlob = '*'
    },
    [pscustomobject]@{
        Name = "Security: hardcoded signing key literal"
        Severity = "CRIT"
        Regex = "(?i)(Signing|Jwt)Key\s*[:=]\s*[""'][A-Za-z0-9+/=_\-]{20,}"
        FileGlob = '*'
    },
    [pscustomobject]@{
        Name = "Security: bootstrap admin enabled in config"
        Severity = "WARN"
        Regex = '(?i)IsBootstrapAdmin\s*[:=]\s*true'
        FileGlob = '*'
    },
    [pscustomobject]@{
        Name = "Security: dev seed endpoint reference"
        Severity = "WARN"
        Regex = '(?i)DevSeedEndpoints|MapDevSeed|/dev/seed'
        FileGlob = '*'
    },
    [pscustomobject]@{
        Name = "Config: hardcoded server IP (178.105.152.180)"
        Severity = "WARN"
        Regex = '178\.105\.152\.180'
        FileGlob = '*'
    },
    [pscustomobject]@{
        Name = "Refactor: gate-closing / shim wrapper comment"
        Severity = "WARN"
        Regex = '(?i)(gate[-\s]?clos|closes?\s+the\s+gate|satisf(y|ies)\s+the\s+gate|thin\s+shim|wrapper\s+shim)'
        FileGlob = '*'
    },
    [pscustomobject]@{
        Name = "Hygiene: TODO / FIXME / HACK marker"
        Severity = "INFO"
        Regex = '(?i)\b(TODO|FIXME|HACK|XXX)\b'
        FileGlob = '*'
    }
)

function Get-AuditSignalsForContent {
    param(
        [Parameter(Mandatory = $true)][AllowEmptyString()][string]$Content,
        [Parameter(Mandatory = $true)][string]$RelativePath
    )

    $results = New-Object System.Collections.Generic.List[object]
    if (-not $RunAuditSignals) { return $results }
    if ([string]::IsNullOrEmpty($Content)) { return $results }

    $isSensitive = Test-IsSensitiveFile -RelativePath $RelativePath
    $lines = @($Content -split "`r?`n")

    foreach ($rule in $auditSignalRules) {
        if (-not ($RelativePath -like $rule.FileGlob)) { continue }

        for ($i = 0; $i -lt $lines.Count; $i++) {
            $line = $lines[$i]
            if ($line -match $rule.Regex) {
                $snippet = $line.Trim()
                if ($snippet.Length -gt 160) { $snippet = $snippet.Substring(0, 160) + " ..." }
                # Withhold snippet from sensitive files only when masking is enabled.
                if ($isSensitive -and $MaskSecrets) { $snippet = "[snippet withheld - sensitive file]" }

                $results.Add([pscustomobject]@{
                    RelativePath = $RelativePath
                    Line = $i + 1
                    Severity = $rule.Severity
                    Rule = $rule.Name
                    Snippet = $snippet
                })
            }
        }
    }

    return $results
}

# ============================================================
# 8. Safe IO
# ============================================================

function Read-RawTextSafe {
    param([Parameter(Mandatory = $true)][string]$Path)
    try {
        return [System.IO.File]::ReadAllText($Path, [System.Text.Encoding]::UTF8)
    }
    catch {
        try { return (Get-Content -LiteralPath $Path -Raw -ErrorAction Stop) }
        catch { return $null }
    }
}

function Get-LineCountFromContent {
    param([Parameter(Mandatory = $true)][AllowEmptyString()][string]$Content)
    if ([string]::IsNullOrEmpty($Content)) { return 0 }
    $count = ([regex]::Matches($Content, "`n")).Count
    if (-not $Content.EndsWith("`n")) { $count++ }
    return $count
}

# ============================================================
# 9. Collect files
# ============================================================

Write-Step "============================================================"
Write-Step " PlantProcess IQ Ultimate Documentation + Deep Audit (v2.1)"
Write-Step "============================================================"
Write-Info "Repository root : $RepositoryRoot"
Write-Info "Output folder   : $OutputFolder"
Write-Info "Mask secrets    : $MaskSecrets"
Write-Info "Compute hashes  : $ComputeHashes"
Write-Info "Audit signals   : $RunAuditSignals"
Write-Info "Max file size   : $MaxFileSizeMB MB"
Write-Step "============================================================"

$maxFileSizeBytes = $MaxFileSizeMB * 1024 * 1024
$allCandidateFiles = Get-ChildItem -LiteralPath $RepositoryRoot -Recurse -File -Force

$includedFiles = New-Object System.Collections.Generic.List[object]
$skippedFiles = New-Object System.Collections.Generic.List[object]
$allSignals = New-Object System.Collections.Generic.List[object]

$processed = 0
$total = @($allCandidateFiles).Count

foreach ($file in $allCandidateFiles) {

    $processed++
    if (($processed % 250) -eq 0) {
        Write-Host ("  ... scanned {0}/{1} files" -f $processed, $total) -ForegroundColor DarkGray
    }

    try {
        $relativePath = Get-RelativePath -Root $RepositoryRoot -Path $file.FullName
    }
    catch {
        continue
    }

    $forceInclude = Test-IsForceIncluded -File $file -RelativePath $relativePath

    # Excluded file-name patterns (binary/noise). Force-include cannot rescue true binaries.
    if (Test-MatchesAnyPattern -Value $file.Name -Patterns $excludedFileNamePatterns) {
        $skippedFiles.Add([pscustomobject]@{
            RelativePath = $relativePath; Reason = "Excluded generated/binary/noisy file pattern"; SizeBytes = $file.Length
        })
        continue
    }

    # Hard-excluded segments are never rescued (bin/obj/node_modules/.git/Documentation...).
    $segments = @(Get-PathSegments -RelativePath $relativePath)
    $inHardExcluded = $false
    foreach ($segment in $segments) {
        foreach ($excluded in $hardExcludedSegments) {
            if ($segment -ieq $excluded) { $inHardExcluded = $true; break }
        }
        if ($inHardExcluded) { break }
    }
    if ($inHardExcluded) {
        $skippedFiles.Add([pscustomobject]@{
            RelativePath = $relativePath; Reason = "Hard-excluded folder"; SizeBytes = $file.Length
        })
        continue
    }

    # Runtime-data segments are skipped unless force-included.
    if (-not $forceInclude -and (Test-IsUnderExcludedFolder -RelativePath $relativePath)) {
        $skippedFiles.Add([pscustomobject]@{
            RelativePath = $relativePath; Reason = "Runtime/data folder"; SizeBytes = $file.Length
        })
        continue
    }

    # Hidden paths skipped unless force-included.
    if (-not $IncludeHidden -and -not $forceInclude -and (Test-IsHiddenPath -File $file -RelativePath $relativePath)) {
        $skippedFiles.Add([pscustomobject]@{
            RelativePath = $relativePath; Reason = "Hidden file/path"; SizeBytes = $file.Length
        })
        continue
    }

    if (-not $IncludeLockFiles -and ($lockFileNames -contains $file.Name)) {
        $skippedFiles.Add([pscustomobject]@{
            RelativePath = $relativePath; Reason = "Lock file excluded"; SizeBytes = $file.Length
        })
        continue
    }

    if ($file.Length -gt $maxFileSizeBytes) {
        $skippedFiles.Add([pscustomobject]@{
            RelativePath = $relativePath; Reason = ("File too large (> {0} MB)" -f $MaxFileSizeMB); SizeBytes = $file.Length
        })
        continue
    }

    $knownText = Test-IsKnownTextFile -File $file
    $probablyText = $true
    if (-not $knownText) {
        $probablyText = Test-IsProbablyTextByContent -File $file
    }

    if (-not $probablyText) {
        $skippedFiles.Add([pscustomobject]@{
            RelativePath = $relativePath; Reason = "Binary-like file"; SizeBytes = $file.Length
        })
        continue
    }

    # Single raw read: used for line count + audit-signal scan (NOT stored).
    $rawContent = Read-RawTextSafe -Path $file.FullName
    if ($null -eq $rawContent) {
        $skippedFiles.Add([pscustomobject]@{
            RelativePath = $relativePath; Reason = "Unreadable (IO error)"; SizeBytes = $file.Length
        })
        continue
    }

    $lineCount = Get-LineCountFromContent -Content $rawContent

    if ($RunAuditSignals) {
        $signals = Get-AuditSignalsForContent -Content $rawContent -RelativePath $relativePath
        foreach ($s in $signals) { $allSignals.Add($s) }
    }

    $category = Get-PrimaryCategory -RelativePath $relativePath
    $classification = Get-FileClassification -RelativePath $relativePath -File $file
    $hash = Get-FileSha256 -Path $file.FullName

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
        Sha256 = $hash
        ForceIncluded = $forceInclude
        LastWriteTime = $file.LastWriteTime
    })
}

$includedFiles = @($includedFiles | Sort-Object Category, RelativePath)
$skippedFiles = @($skippedFiles | Sort-Object RelativePath)
$allSignals = @($allSignals | Sort-Object @{Expression='Severity';Descending=$false}, RelativePath, Line)

# Classification integrity gate: no SQL/CSV demo fixture may leak back into the
# generic 07_Tools_Validation_Misc document. Fail fast rather than generating a
# misleading audit package.
$misclassifiedDemoSeedFiles = @(
    $includedFiles | Where-Object {
        (Test-IsDemoSeedRelativePath -RelativePath $_.RelativePath) -and
        $_.Category -ne "07A_DEMO_SQL_Data_Seed"
    }
)

if ($misclassifiedDemoSeedFiles.Count -gt 0) {
    $badPaths = ($misclassifiedDemoSeedFiles | ForEach-Object { $_.RelativePath }) -join "; "
    throw "Demo seed classification integrity failure. These files were not routed to 07A_DEMO_SQL_Data_Seed: $badPaths"
}

$demoSeedFileCount = @($includedFiles | Where-Object { $_.Category -eq "07A_DEMO_SQL_Data_Seed" }).Count
Write-Ok ("Collected {0} included files, {1} skipped, {2} audit signals. Demo seed files: {3}." -f $includedFiles.Count, $skippedFiles.Count, $allSignals.Count, $demoSeedFileCount)

# ============================================================
# 10. Writer helpers
# ============================================================

function New-Utf8NoBomWriter {
    param([Parameter(Mandatory = $true)][string]$Path)
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    $sw = New-Object System.IO.StreamWriter($Path, $false, $utf8NoBom)
    $sw.NewLine = "`n"
    return $sw
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
    $content = Read-RawTextSafe -Path $Path
    if ($null -eq $content) {
        return "[READ_ERROR] Unable to read file content."
    }
    return Protect-SecretContent -Content $content -RelativePath $RelativePath
}

function Write-Header {
    param(
        [Parameter(Mandatory = $true)]$Writer,
        [Parameter(Mandatory = $true)][string]$Title,
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][object[]]$Files
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
    $Writer.WriteLine("Compute Hashes        : $ComputeHashes")
    $Writer.WriteLine("Audit Signals         : $RunAuditSignals")
    $Writer.WriteLine("Max File Size         : $MaxFileSizeMB MB")
    $Writer.WriteLine("PowerShell Version    : $($PSVersionTable.PSVersion)")
    $Writer.WriteLine("Machine Name          : $env:COMPUTERNAME")
    $Writer.WriteLine("User Name             : $env:USERNAME")
    $Writer.WriteLine("")
}

function Write-CategorySummary {
    param(
        [Parameter(Mandatory = $true)]$Writer,
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][object[]]$Files
    )
    Add-SubTitle -Writer $Writer -Title "CATEGORY SUMMARY"

    if ($Files.Count -eq 0) { $Writer.WriteLine("(no files in this category)"); $Writer.WriteLine(""); return }

    $groups = $Files | Group-Object Category | Sort-Object Name
    foreach ($group in $groups) {
        $categoryTitle = Get-CategoryTitle -Category $group.Name
        $lines = ($group.Group | Measure-Object -Property Lines -Sum).Sum
        $bytes = ($group.Group | Measure-Object -Property SizeBytes -Sum).Sum
        $Writer.WriteLine(("{0,-40} Files: {1,5} | Lines: {2,8} | Size: {3,8} MB" -f $categoryTitle, $group.Count, $lines, [Math]::Round($bytes / 1MB, 3)))
    }
    $Writer.WriteLine("")
}

function Write-ClassificationSummary {
    param(
        [Parameter(Mandatory = $true)]$Writer,
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][object[]]$Files
    )
    Add-SubTitle -Writer $Writer -Title "CLASSIFICATION SUMMARY"

    if ($Files.Count -eq 0) { $Writer.WriteLine("(none)"); $Writer.WriteLine(""); return }

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
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][object[]]$Files
    )
    Add-SubTitle -Writer $Writer -Title "EXTENSION / SPECIAL FILE SUMMARY"

    if ($Files.Count -eq 0) { $Writer.WriteLine("(none)"); $Writer.WriteLine(""); return }

    $normalized = foreach ($file in $Files) {
        $extValue = $file.Extension
        if ([string]::IsNullOrWhiteSpace($extValue)) { $extValue = "[NO_EXTENSION]" }
        [pscustomobject]@{ Extension = $extValue; Lines = $file.Lines; SizeBytes = $file.SizeBytes }
    }

    $groups = $normalized | Group-Object Extension | Sort-Object Name
    foreach ($group in $groups) {
        $lines = ($group.Group | Measure-Object -Property Lines -Sum).Sum
        $bytes = ($group.Group | Measure-Object -Property SizeBytes -Sum).Sum
        $Writer.WriteLine(("{0,-20} Files: {1,5} | Lines: {2,8} | Size: {3,8} KB" -f $group.Name, $group.Count, $lines, [Math]::Round($bytes / 1KB, 2)))
    }
    $Writer.WriteLine("")
}

function Write-TableOfContents {
    param(
        [Parameter(Mandatory = $true)]$Writer,
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][object[]]$Files
    )
    Add-SubTitle -Writer $Writer -Title "TABLE OF CONTENTS"

    if ($Files.Count -eq 0) { $Writer.WriteLine("(no files)"); $Writer.WriteLine(""); return }

    $index = 1
    foreach ($file in $Files) {
        $Writer.WriteLine(("{0,5}. {1} | {2} | {3} lines | {4} KB" -f $index, $file.RelativePath, $file.Classification, $file.Lines, $file.SizeKB))
        $index++
    }
    $Writer.WriteLine("")
}

function Write-DirectoryTree {
    param(
        [Parameter(Mandatory = $true)]$Writer,
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][object[]]$Files
    )
    Add-SubTitle -Writer $Writer -Title "INCLUDED FILE TREE"

    if ($Files.Count -eq 0) { $Writer.WriteLine("(no files)"); $Writer.WriteLine(""); return }

    if (-not $RenderTree) {
        foreach ($file in $Files) { $Writer.WriteLine(" - $($file.RelativePath)") }
        $Writer.WriteLine("")
        return
    }

    $prevSegments = @()
    foreach ($file in ($Files | Sort-Object RelativePath)) {
        $segments = @(Get-PathSegments -RelativePath $file.RelativePath)
        $dirCount = $segments.Count - 1

        for ($d = 0; $d -lt $dirCount; $d++) {
            $samePrefix = ($prevSegments.Count -gt $d) -and ($prevSegments[$d] -eq $segments[$d])
            if (-not $samePrefix) {
                $indent = "    " * $d
                $Writer.WriteLine(("{0}[{1}]" -f $indent, $segments[$d]))
            }
        }

        $fileIndent = "    " * $dirCount
        $Writer.WriteLine(("{0}{1}" -f $fileIndent, $segments[$segments.Count - 1]))
        $prevSegments = $segments
    }
    $Writer.WriteLine("")
}

function Write-FilesWithContent {
    param(
        [Parameter(Mandatory = $true)]$Writer,
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][object[]]$Files
    )
    Add-SectionTitle -Writer $Writer -Title "FULL FILE CONTENT"

    if ($Files.Count -eq 0) { $Writer.WriteLine("(no files in this category)"); $Writer.WriteLine(""); return }

    foreach ($file in $Files) {
        $Writer.WriteLine("[FILE_START]")
        $Writer.WriteLine("[METADATA: Path='$($file.RelativePath)', Category='$($file.CategoryTitle)', Classification='$($file.Classification)', Lines=$($file.Lines), SizeKB=$($file.SizeKB), SHA256='$($file.Sha256)', Modified='$($file.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"))']")
        $Writer.WriteLine("")

        $content = Read-FileContentSafe -Path $file.FullName -RelativePath $file.RelativePath
        $Writer.WriteLine($content)
        if (-not $content.EndsWith("`n")) { $Writer.WriteLine("") }

        $Writer.WriteLine("[FILE_END]")
        $Writer.WriteLine("")
    }
}

function Write-SkippedFiles {
    param(
        [Parameter(Mandatory = $true)]$Writer,
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][object[]]$Skipped
    )
    Add-SectionTitle -Writer $Writer -Title "SKIPPED FILES"

    if ($Skipped.Count -eq 0) {
        $Writer.WriteLine("No files skipped.")
        $Writer.WriteLine("")
        return
    }

    Add-SubTitle -Writer $Writer -Title "SKIP REASON ROLLUP"
    $reasonGroups = $Skipped | Group-Object Reason | Sort-Object Count -Descending
    foreach ($rg in $reasonGroups) {
        $Writer.WriteLine(("{0,-45} {1,6} files" -f $rg.Name, $rg.Count))
    }
    $Writer.WriteLine("")

    Add-SubTitle -Writer $Writer -Title "SKIPPED FILE DETAIL"
    foreach ($file in $Skipped) {
        $Writer.WriteLine("$($file.RelativePath) | $($file.Reason) | $([Math]::Round($file.SizeBytes / 1KB, 2)) KB")
    }
    $Writer.WriteLine("")
}

function Write-AuditSignalSummary {
    param(
        [Parameter(Mandatory = $true)]$Writer,
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][object[]]$Signals
    )
    Add-SubTitle -Writer $Writer -Title "AUDIT SIGNAL SUMMARY"

    if (-not $RunAuditSignals) { $Writer.WriteLine("(audit signal scan disabled)"); $Writer.WriteLine(""); return }
    if ($Signals.Count -eq 0) { $Writer.WriteLine("No audit signals detected."); $Writer.WriteLine(""); return }

    foreach ($sev in @("CRIT", "WARN", "INFO")) {
        $sevHits = @($Signals | Where-Object { $_.Severity -eq $sev })
        $Writer.WriteLine(("{0,-6} {1,5} hits" -f $sev, $sevHits.Count))
    }
    $Writer.WriteLine("")

    $byRule = $Signals | Group-Object Rule | Sort-Object Count -Descending
    foreach ($rg in $byRule) {
        $sev = ($rg.Group | Select-Object -First 1).Severity
        $Writer.WriteLine(("[{0}] {1,-58} {2,4} hits" -f $sev, $rg.Name, $rg.Count))
    }
    $Writer.WriteLine("")
}

function Write-DocumentationFile {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Title,
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][object[]]$Files,
        [AllowEmptyCollection()][object[]]$Skipped = @(),
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
        if ($IncludeSkipped) { Write-SkippedFiles -Writer $writer -Skipped $Skipped }
        Write-FilesWithContent -Writer $writer -Files $Files
    }
    finally {
        $writer.Dispose()
    }
}

# ============================================================
# 11. Generate category files
# ============================================================

$categoryDefinitions = @(
    [pscustomobject]@{ Key = "01_Backend_Core"; FilePrefix = "01_Backend_Core"; Title = "PLANTPROCESS IQ BACKEND CORE AUDIT - API / APPLICATION / ANALYTICS / DOMAIN / INFRASTRUCTURE / WORKERS" },
    [pscustomobject]@{ Key = "02_Backend_Database"; FilePrefix = "02_Backend_Database"; Title = "PLANTPROCESS IQ BACKEND DATABASE AUDIT - SCRIPTS / SEED / VIEWS" },
    [pscustomobject]@{ Key = "03_Backend_Tests"; FilePrefix = "03_Backend_Tests"; Title = "PLANTPROCESS IQ BACKEND TESTS AUDIT" },
    [pscustomobject]@{ Key = "04_Frontend_App"; FilePrefix = "04_Frontend_App"; Title = "PLANTPROCESS IQ FRONTEND APP AUDIT - CORE REACT SOURCE (HMI)" },
    [pscustomobject]@{ Key = "05_Frontend_Misc"; FilePrefix = "05_Frontend_Misc"; Title = "PLANTPROCESS IQ FRONTEND MISC AUDIT - E2E / SCRIPTS / TEST RESULTS" },
    [pscustomobject]@{ Key = "06_Infrastructure"; FilePrefix = "06_Infrastructure"; Title = "PLANTPROCESS IQ INFRASTRUCTURE AUDIT - DEPLOYMENT / DOCKER / CADDY / CI-CD" },
    [pscustomobject]@{ Key = "07_Tools_Validation_Misc"; FilePrefix = "07_Tools_Validation_Misc"; Title = "PLANTPROCESS IQ TOOLS / VALIDATION / ROOT MISC AUDIT" },
    [pscustomobject]@{ Key = "07A_DEMO_SQL_Data_Seed"; FilePrefix = "07A_DEMO_SQL_Data_Seed"; Title = "PLANTPROCESS IQ DEMO SQL DATA SEED - SCHEMA / DATA / EXCEL-CSV FIXTURES" },
    [pscustomobject]@{ Key = "08_Website"; FilePrefix = "08_Website"; Title = "PLANTPROCESS IQ WEBSITE AUDIT - PLANTPROCESS.WEBSITE" }
)

foreach ($category in $categoryDefinitions) {
    $categoryFiles = @($includedFiles | Where-Object { $_.Category -eq $category.Key })
    $outputPath = Join-Path $OutputFolder ("{0}_{1}.txt" -f $category.FilePrefix, $timestamp)

    Write-DocumentationFile -Path $outputPath -Title $category.Title -Files $categoryFiles

    if ($categoryFiles.Count -eq 0) {
        Write-Warn2 ("Generated {0}: 0 files (EMPTY - verify this is expected)" -f $category.FilePrefix)
    }
    else {
        Write-Ok ("Generated {0}: {1} files" -f $category.FilePrefix, $categoryFiles.Count)
    }
}

# ============================================================
# 12. Generate master index
# ============================================================

$masterIndexPath = Join-Path $OutputFolder ("00_Master_Index_{0}.txt" -f $timestamp)
$masterWriter = New-Utf8NoBomWriter -Path $masterIndexPath
try {
    Write-Header -Writer $masterWriter -Title "PLANTPROCESS IQ MASTER INDEX - ULTIMATE AUDIT PACKAGE" -Files $includedFiles
    Write-CategorySummary -Writer $masterWriter -Files $includedFiles
    Write-ClassificationSummary -Writer $masterWriter -Files $includedFiles
    Write-ExtensionSummary -Writer $masterWriter -Files $includedFiles
    Write-AuditSignalSummary -Writer $masterWriter -Signals $allSignals

    Add-SectionTitle -Writer $masterWriter -Title "CATEGORY OUTPUT FILES"
    foreach ($category in $categoryDefinitions) {
        $count = @($includedFiles | Where-Object { $_.Category -eq $category.Key }).Count
        $masterWriter.WriteLine("$($category.FilePrefix)_$timestamp.txt | $($category.Title) | Files: $count")
    }
    $masterWriter.WriteLine("09_FullStack_Combined_$timestamp.txt | FULL COMBINED SOURCE DOCUMENTATION | Files: $($includedFiles.Count)")
    $masterWriter.WriteLine("10_Audit_Signals_$timestamp.txt | AUDIT SIGNAL REPORT | Hits: $($allSignals.Count)")
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
# 13. Generate combined full-stack file
# ============================================================

$combinedPath = Join-Path $OutputFolder ("09_FullStack_Combined_{0}.txt" -f $timestamp)
Write-DocumentationFile -Path $combinedPath -Title "PLANTPROCESS IQ FULL-STACK COMBINED SOURCE DOCUMENTATION - ULTIMATE AUDIT" -Files $includedFiles -Skipped $skippedFiles -IncludeSkipped
Write-Ok ("Generated FullStack Combined: {0} files" -f $includedFiles.Count)

# ============================================================
# 14. Generate dedicated audit-signal report
# ============================================================

$signalsPath = Join-Path $OutputFolder ("10_Audit_Signals_{0}.txt" -f $timestamp)
$signalWriter = New-Utf8NoBomWriter -Path $signalsPath
try {
    Add-SectionTitle -Writer $signalWriter -Title "PLANTPROCESS IQ AUDIT SIGNAL REPORT"
    $signalWriter.WriteLine("Generated At          : $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
    $signalWriter.WriteLine("Repository Root       : $RepositoryRoot")
    $signalWriter.WriteLine("Total Signals         : $($allSignals.Count)")
    $signalWriter.WriteLine("")

    Write-AuditSignalSummary -Writer $signalWriter -Signals $allSignals

    if ($allSignals.Count -gt 0) {
        foreach ($sev in @("CRIT", "WARN", "INFO")) {
            $sevHits = @($allSignals | Where-Object { $_.Severity -eq $sev })
            if ($sevHits.Count -eq 0) { continue }

            Add-SectionTitle -Writer $signalWriter -Title ("{0} SIGNALS ({1})" -f $sev, $sevHits.Count)
            foreach ($hit in $sevHits) {
                $signalWriter.WriteLine(("{0}:{1}  [{2}]" -f $hit.RelativePath, $hit.Line, $hit.Rule))
                $signalWriter.WriteLine(("    {0}" -f $hit.Snippet))
            }
            $signalWriter.WriteLine("")
        }
    }
}
finally {
    $signalWriter.Dispose()
}
Write-Ok ("Generated Audit Signal Report: {0} hits" -f $allSignals.Count)

# ============================================================
# 15. Generate machine-readable manifests (UTF-8 no BOM)
# ============================================================

$manifestCsvPath = Join-Path $OutputFolder ("manifest_{0}.csv" -f $timestamp)
$manifestJsonPath = Join-Path $OutputFolder ("manifest_{0}.json" -f $timestamp)

$manifestRows = $includedFiles | Select-Object RelativePath, Category, CategoryTitle, Classification, Extension, Lines, SizeBytes, SizeKB, Sha256, ForceIncluded, @{Name='LastWriteTime';Expression={$_.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")}}

$csvLines = $manifestRows | ConvertTo-Csv -NoTypeInformation
[System.IO.File]::WriteAllText($manifestCsvPath, ($csvLines -join "`n") + "`n", (New-Object System.Text.UTF8Encoding($false)))

$manifestObject = [pscustomobject]@{
    GeneratedAt = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
    RepositoryRoot = $RepositoryRoot
    IncludedFileCount = $includedFiles.Count
    SkippedFileCount = $skippedFiles.Count
    SignalCount = $allSignals.Count
    Files = $manifestRows
    Signals = $allSignals
    SkippedRollup = @($skippedFiles | Group-Object Reason | Sort-Object Count -Descending | ForEach-Object { [pscustomobject]@{ Reason = $_.Name; Count = $_.Count } })
}
$json = $manifestObject | ConvertTo-Json -Depth 6
[System.IO.File]::WriteAllText($manifestJsonPath, $json, (New-Object System.Text.UTF8Encoding($false)))

Write-Ok "Generated manifest CSV"
Write-Ok "Generated manifest JSON"

# ============================================================
# 16. Console summary
# ============================================================

$totalLines = ($includedFiles | Measure-Object -Property Lines -Sum).Sum
$totalBytes = ($includedFiles | Measure-Object -Property SizeBytes -Sum).Sum
$critCount = @($allSignals | Where-Object { $_.Severity -eq "CRIT" }).Count
$warnCount = @($allSignals | Where-Object { $_.Severity -eq "WARN" }).Count

Write-Host ""
Write-Step "============================================================"
Write-Ok "PlantProcess IQ Ultimate Audit generated successfully."
Write-Step "============================================================"
Write-Info "Output folder        : $OutputFolder"
Write-Info "Included files       : $($includedFiles.Count)"
Write-Info "Skipped files        : $($skippedFiles.Count)"
Write-Info "Total lines          : $totalLines"
Write-Info "Total size           : $([Math]::Round($totalBytes / 1MB, 3)) MB"
Write-Info "Audit signals        : $($allSignals.Count) (CRIT: $critCount, WARN: $warnCount)"
Write-Info "Master index         : $masterIndexPath"
Write-Info "Full combined        : $combinedPath"
Write-Info "Audit signals        : $signalsPath"
Write-Info "Manifest CSV         : $manifestCsvPath"
Write-Info "Manifest JSON        : $manifestJsonPath"
Write-Step "============================================================"

Write-Host ""
Write-Host "Generated category files:" -ForegroundColor Cyan
foreach ($category in $categoryDefinitions) {
    $count = @($includedFiles | Where-Object { $_.Category -eq $category.Key }).Count
    $color = if ($count -eq 0) { "DarkYellow" } else { "Yellow" }
    Write-Host (" - {0}: {1} files" -f (Get-CategoryTitle -Category $category.Key), $count) -ForegroundColor $color
}

if ($critCount -gt 0) {
    Write-Host ""
    Write-Host "WARNING: $critCount critical audit signal(s) detected. See 10_Audit_Signals_$timestamp.txt" -ForegroundColor Red
}

Write-Host ""

if ($OpenAfterGeneration) {
    Invoke-Item $OutputFolder
}
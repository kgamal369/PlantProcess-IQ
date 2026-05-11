#requires -Version 5.1
<#
.SYNOPSIS
    Generates a professional full-stack source-code documentation TXT file for PlantProcess IQ.

.DESCRIPTION
    This script supports the new repository structure:

        C:\Workspace\PlantProcess-IQ
        ├── Backend
        │   ├── PlantProcessIQ.sln
        │   ├── PlantProcess.Api
        │   ├── PlantProcess.Application
        │   ├── PlantProcess.Domain
        │   ├── PlantProcess.Infrastructure
        │   └── PlantProcess.Workers
        │
        ├── Frontend
        │   └── PlantProcess.Web
        │
        └── Documentation

    Backend scan:
        .cs, .csproj, .json

    Frontend scan:
        .ts, .tsx, .js, .jsx, .json, .css, .html

    Output:
        C:\Workspace\PlantProcess-IQ\Documentation

.NOTES
    - Excludes bin, obj, node_modules, dist, build, coverage, .git, .vs, etc.
    - Excludes lock files by default to avoid huge generated dependency dumps.
    - Uses UTF-8 without BOM.
#>

[CmdletBinding()]
param(
    [string]$RepositoryRoot,
    [string]$BackendRoot,
    [string]$FrontendRoot,
    [string]$OutputFolder,

    [string[]]$BackendExtensions = @(".cs", ".csproj", ".json"),
    [string[]]$FrontendExtensions = @(".ts", ".tsx", ".js", ".jsx", ".json", ".css", ".html"),

    [switch]$IncludeHidden,
    [switch]$ExcludeMigrations,
    [switch]$ExcludeGeneratedDesignerFiles,
    [switch]$IncludeLockFiles,
    [switch]$OpenAfterGeneration
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ------------------------------------------------------------
# 1. Resolve repository paths
# ------------------------------------------------------------

$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path

function Resolve-DefaultRepositoryRoot {
    param([Parameter(Mandatory = $true)][string]$ScriptDirectory)

    $current = $ScriptDirectory

    for ($i = 0; $i -lt 8; $i++) {
        if ([string]::IsNullOrWhiteSpace($current)) {
            break
        }

        $backendCandidate = Join-Path $current "Backend"
        $frontendCandidate = Join-Path $current "Frontend"

        if ((Test-Path -LiteralPath $backendCandidate) -and (Test-Path -LiteralPath $frontendCandidate)) {
            return $current
        }

        if ((Split-Path $current -Leaf) -eq "Backend") {
            $parent = Split-Path -Parent $current
            if (-not [string]::IsNullOrWhiteSpace($parent)) {
                return $parent
            }
        }

        $parentPath = Split-Path -Parent $current

        if ($parentPath -eq $current) {
            break
        }

        $current = $parentPath
    }

    # Expected location fallback:
    # C:\Workspace\PlantProcess-IQ\Backend\tools
    $backendFolder = Split-Path -Parent $ScriptDirectory
    if ((Split-Path $backendFolder -Leaf) -eq "Backend") {
        return Split-Path -Parent $backendFolder
    }

    return Split-Path -Parent $ScriptDirectory
}

function Normalize-Extensions {
    param([Parameter(Mandatory = $true)][string[]]$Extensions)

    return $Extensions |
        ForEach-Object {
            if ($_.StartsWith(".")) {
                $_.ToLowerInvariant()
            }
            else {
                ".$($_.ToLowerInvariant())"
            }
        } |
        Sort-Object -Unique
}

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = Resolve-DefaultRepositoryRoot -ScriptDirectory $scriptDirectory
}

if (-not (Test-Path -LiteralPath $RepositoryRoot)) {
    throw "Repository root does not exist: $RepositoryRoot"
}

$RepositoryRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path

if ([string]::IsNullOrWhiteSpace($BackendRoot)) {
    $BackendRoot = Join-Path $RepositoryRoot "Backend"
}

if ([string]::IsNullOrWhiteSpace($FrontendRoot)) {
    $FrontendRoot = Join-Path $RepositoryRoot "Frontend"
}

if ([string]::IsNullOrWhiteSpace($OutputFolder)) {
    $OutputFolder = Join-Path $RepositoryRoot "Documentation"
}

if (-not (Test-Path -LiteralPath $BackendRoot)) {
    throw "Backend root does not exist: $BackendRoot"
}

if (-not (Test-Path -LiteralPath $FrontendRoot)) {
    Write-Warning "Frontend root does not exist yet: $FrontendRoot. Frontend scan will be skipped."
}

if (-not (Test-Path -LiteralPath $OutputFolder)) {
    New-Item -ItemType Directory -Path $OutputFolder | Out-Null
}

$BackendRoot = (Resolve-Path -LiteralPath $BackendRoot).Path
if (Test-Path -LiteralPath $FrontendRoot) {
    $FrontendRoot = (Resolve-Path -LiteralPath $FrontendRoot).Path
}
$OutputFolder = (Resolve-Path -LiteralPath $OutputFolder).Path

$normalizedBackendExtensions = Normalize-Extensions -Extensions $BackendExtensions
$normalizedFrontendExtensions = Normalize-Extensions -Extensions $FrontendExtensions

# ------------------------------------------------------------
# 2. Naming
# ------------------------------------------------------------

$projectFolderName = Split-Path $RepositoryRoot -Leaf
$cleanProjectName = ($projectFolderName -replace '[^a-zA-Z0-9]', '')

$culture = [System.Globalization.CultureInfo]::InvariantCulture
$generationDateTime = Get-Date
$timestampForFileName = $generationDateTime.ToString("ddMMMyyyy_HHmm", $culture)

$outputFileName = "${cleanProjectName}_FullStack_${timestampForFileName}.txt"
$outputFilePath = Join-Path $OutputFolder $outputFileName

if (Test-Path -LiteralPath $outputFilePath) {
    $baseName = [System.IO.Path]::GetFileNameWithoutExtension($outputFileName)
    $counter = 1

    do {
        $candidateName = "{0}_{1:00}.txt" -f $baseName, $counter
        $outputFilePath = Join-Path $OutputFolder $candidateName
        $counter++
    }
    while (Test-Path -LiteralPath $outputFilePath)
}

# ------------------------------------------------------------
# 3. Exclusions and helpers
# ------------------------------------------------------------

$excludedFolders = @(
    ".git",
    ".github",
    ".vs",
    ".vscode",
    ".idea",
    "bin",
    "obj",
    "node_modules",
    ".vite",
    "dist",
    "build",
    "coverage",
    "TestResults",
    "packages",
    ".sonarqube",
    "logs",
    "Documentation"
)

$excludedFoldersLower = $excludedFolders | ForEach-Object { $_.ToLowerInvariant() }

function Get-RelativePath {
    param(
        [Parameter(Mandatory = $true)][string]$FullPath,
        [Parameter(Mandatory = $true)][string]$BasePath
    )

    $baseUri = New-Object System.Uri(($BasePath.TrimEnd('\') + '\'))
    $fileUri = New-Object System.Uri($FullPath)
    $relativeUri = $baseUri.MakeRelativeUri($fileUri)

    return [System.Uri]::UnescapeDataString($relativeUri.ToString()).Replace('/', '\')
}

function Test-IsLockFile {
    param([Parameter(Mandatory = $true)][System.IO.FileInfo]$File)

    $name = $File.Name.ToLowerInvariant()

    return (
        $name -eq "package-lock.json" -or
        $name -eq "yarn.lock" -or
        $name -eq "pnpm-lock.yaml" -or
        $name -eq "package-lock.json"
    )
}

function Test-IsExcludedPath {
    param(
        [Parameter(Mandatory = $true)][System.IO.FileInfo]$File,
        [Parameter(Mandatory = $true)][string]$BasePath,
        [Parameter(Mandatory = $true)][string]$Scope
    )

    $relativePath = Get-RelativePath -FullPath $File.FullName -BasePath $BasePath
    $segments = $relativePath -split '[\\/]'

    foreach ($segment in $segments) {
        if ($excludedFoldersLower -contains $segment.ToLowerInvariant()) {
            return $true
        }
    }

    if (-not $IncludeLockFiles -and (Test-IsLockFile -File $File)) {
        return $true
    }

    if ($Scope -eq "Backend") {
        if ($ExcludeMigrations -and ($segments -contains "Migrations")) {
            return $true
        }

        if ($ExcludeGeneratedDesignerFiles -and ($File.FullName -like "*.Designer.cs")) {
            return $true
        }
    }

    return $false
}

function Read-TextFileSafe {
    param([Parameter(Mandatory = $true)][string]$Path)

    $reader = $null

    try {
        $reader = New-Object System.IO.StreamReader(
            $Path,
            [System.Text.Encoding]::UTF8,
            $true
        )

        return $reader.ReadToEnd()
    }
    finally {
        if ($null -ne $reader) {
            $reader.Dispose()
        }
    }
}

function Get-LineCount {
    param([AllowNull()][string]$Content)

    if ([string]::IsNullOrEmpty($Content)) {
        return 0
    }

    return ([regex]::Matches($Content, "\r\n|\n|\r").Count + 1)
}

function Get-FirstRegexGroup {
    param(
        [AllowNull()][string]$Content,
        [Parameter(Mandatory = $true)][string]$Pattern
    )

    if ([string]::IsNullOrWhiteSpace($Content)) {
        return ""
    }

    $match = [regex]::Match($Content, $Pattern)

    if ($match.Success -and $match.Groups.Count -gt 1) {
        return $match.Groups[1].Value
    }

    return ""
}

function Get-DistinctRegexGroups {
    param(
        [AllowNull()][string]$Content,
        [Parameter(Mandatory = $true)][string]$Pattern,
        [int]$GroupIndex = 1
    )

    if ([string]::IsNullOrWhiteSpace($Content)) {
        return @()
    }

    return [regex]::Matches($Content, $Pattern) |
        ForEach-Object { $_.Groups[$GroupIndex].Value } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        Sort-Object -Unique
}

function Get-TypeDeclarations {
    param(
        [AllowNull()][string]$Content,
        [Parameter(Mandatory = $true)][string]$Scope
    )

    if ([string]::IsNullOrWhiteSpace($Content)) {
        return @()
    }

    if ($Scope -eq "Backend") {
        $backendPattern = '(?m)^\s*(?:public|private|internal|protected|static|sealed|abstract|partial|readonly|\s)*\s*(class|record|interface|enum|struct)\s+([A-Za-z_][A-Za-z0-9_]*)'

        return [regex]::Matches($Content, $backendPattern) |
            ForEach-Object { "$($_.Groups[1].Value) $($_.Groups[2].Value)" } |
            Sort-Object -Unique
    }

    $frontendDeclarations = New-Object System.Collections.Generic.List[string]

    $componentMatches = [regex]::Matches($Content, '(?m)^\s*(?:export\s+default\s+)?function\s+([A-Za-z_][A-Za-z0-9_]*)\s*\(')
    foreach ($match in $componentMatches) {
        $frontendDeclarations.Add("function $($match.Groups[1].Value)")
    }

    $constComponentMatches = [regex]::Matches($Content, '(?m)^\s*(?:export\s+)?const\s+([A-Za-z_][A-Za-z0-9_]*)\s*=')
    foreach ($match in $constComponentMatches) {
        $frontendDeclarations.Add("const $($match.Groups[1].Value)")
    }

    $interfaceMatches = [regex]::Matches($Content, '(?m)^\s*(?:export\s+)?interface\s+([A-Za-z_][A-Za-z0-9_]*)')
    foreach ($match in $interfaceMatches) {
        $frontendDeclarations.Add("interface $($match.Groups[1].Value)")
    }

    $typeMatches = [regex]::Matches($Content, '(?m)^\s*(?:export\s+)?type\s+([A-Za-z_][A-Za-z0-9_]*)')
    foreach ($match in $typeMatches) {
        $frontendDeclarations.Add("type $($match.Groups[1].Value)")
    }

    return $frontendDeclarations | Sort-Object -Unique
}

function Get-FileClassification {
    param(
        [Parameter(Mandatory = $true)][System.IO.FileInfo]$File,
        [AllowNull()][string]$Content,
        [Parameter(Mandatory = $true)][string]$Scope
    )

    $extension = $File.Extension.ToLowerInvariant()
    $fullName = $File.FullName
    $name = $File.Name

    if ($Scope -eq "Backend") {
        if ($extension -eq ".csproj") {
            return ".NET Project File"
        }

        if ($extension -eq ".json") {
            if ($name -eq "appsettings.json" -or $name -like "appsettings.*.json") {
                return "Backend Application Settings JSON"
            }

            if ($name -eq "launchSettings.json") {
                return "Backend Launch Settings JSON"
            }

            return "Backend JSON Configuration File"
        }

        if ($fullName -like "*\Migrations\*") {
            return "EF Migration / Generated Database Code"
        }

        if ($name -like "*.Designer.cs" -or ($Content -match "<auto-generated")) {
            return "Generated / Designer Code"
        }

        if ($name -eq "Program.cs") {
            return "Backend Application Entry Point"
        }

        if ($name -like "*Endpoints.cs") {
            return "Backend API Endpoint"
        }

        if ($fullName -like "*\Configurations\*") {
            return "EF Core Configuration"
        }

        if ($fullName -like "*\Entities\*") {
            return "Domain Entity"
        }

        if ($fullName -like "*\Common\*") {
            return "Backend Common / Shared Code"
        }

        if ($fullName -like "*\Bulk\*") {
            return "Bulk / Ingestion Support"
        }

        if ($fullName -like "*\Contracts\*") {
            return "Application Contract / DTO"
        }

        if ($fullName -like "*\Services\*") {
            return "Application Service"
        }

        if ($fullName -like "*\Middleware\*") {
            return "API Middleware"
        }

        if ($fullName -like "*\Extensions\*") {
            return "Extension Method / API Helper"
        }

        return "Backend C# Source File"
    }

    if ($name -eq "package.json") {
        return "Frontend NPM Package Manifest"
    }

    if ($name -like "tsconfig*.json") {
        return "Frontend TypeScript Configuration"
    }

    if ($name -eq "vite.config.ts" -or $name -eq "vite.config.js") {
        return "Frontend Vite Configuration"
    }

    if ($name -eq "index.html") {
        return "Frontend HTML Entry Point"
    }

    if ($extension -eq ".css") {
        return "Frontend Stylesheet"
    }

    if ($name -eq "main.tsx" -or $name -eq "main.ts") {
        return "Frontend React Entry Point"
    }

    if ($name -eq "App.tsx" -or $name -eq "App.ts") {
        return "Frontend React Root Component"
    }

    if ($fullName -like "*\pages\*" -or $fullName -like "*\Pages\*") {
        return "Frontend Page"
    }

    if ($fullName -like "*\components\*" -or $fullName -like "*\Components\*") {
        return "Frontend Component"
    }

    if ($fullName -like "*\api\*" -or $fullName -like "*\Api\*") {
        return "Frontend API Client"
    }

    if ($fullName -like "*\hooks\*" -or $fullName -like "*\Hooks\*") {
        return "Frontend React Hook"
    }

    if ($extension -eq ".tsx") {
        return "Frontend React TSX Source"
    }

    if ($extension -eq ".ts") {
        return "Frontend TypeScript Source"
    }

    if ($extension -eq ".js" -or $extension -eq ".jsx") {
        return "Frontend JavaScript Source"
    }

    if ($extension -eq ".json") {
        return "Frontend JSON Configuration File"
    }

    return "Frontend Source File"
}

function Get-NamespaceOrModule {
    param(
        [AllowNull()][string]$Content,
        [Parameter(Mandatory = $true)][string]$Scope
    )

    if ($Scope -eq "Backend") {
        return Get-FirstRegexGroup -Content $Content -Pattern '(?m)^\s*namespace\s+([A-Za-z_][A-Za-z0-9_.]*)\s*[;{]'
    }

    $imports = Get-DistinctRegexGroups -Content $Content -Pattern '(?m)^\s*import\s+.*?\s+from\s+[''"]([^''"]+)[''"]' -GroupIndex 1
    return ($imports -join ", ")
}

function Get-UsingOrImports {
    param(
        [AllowNull()][string]$Content,
        [Parameter(Mandatory = $true)][string]$Scope
    )

    if ($Scope -eq "Backend") {
        $usings = Get-DistinctRegexGroups -Content $Content -Pattern '(?m)^\s*using\s+([A-Za-z_][A-Za-z0-9_.]*)\s*;' -GroupIndex 1
        return ($usings -join ", ")
    }

    $imports = Get-DistinctRegexGroups -Content $Content -Pattern '(?m)^\s*import\s+.*?\s+from\s+[''"]([^''"]+)[''"]' -GroupIndex 1
    return ($imports -join ", ")
}

function Convert-BytesToReadable {
    param([long]$Bytes)

    if ($Bytes -ge 1MB) {
        return "{0:N2} MB" -f ($Bytes / 1MB)
    }

    if ($Bytes -ge 1KB) {
        return "{0:N2} KB" -f ($Bytes / 1KB)
    }

    return "$Bytes bytes"
}

# ------------------------------------------------------------
# 4. Collect files from Backend and Frontend
# ------------------------------------------------------------

$scanTargets = New-Object System.Collections.Generic.List[object]

$scanTargets.Add([pscustomobject]@{
    Scope = "Backend"
    RootPath = $BackendRoot
    Extensions = $normalizedBackendExtensions
})

if (Test-Path -LiteralPath $FrontendRoot) {
    $scanTargets.Add([pscustomobject]@{
        Scope = "Frontend"
        RootPath = $FrontendRoot
        Extensions = $normalizedFrontendExtensions
    })
}

$allCandidateFiles = New-Object System.Collections.Generic.List[object]
$solutionFiles = New-Object System.Collections.Generic.List[object]

foreach ($target in $scanTargets) {
    $scope = $target.Scope
    $targetRoot = $target.RootPath
    $extensions = $target.Extensions

    $candidateFiles = Get-ChildItem -Path $targetRoot -Recurse -File -Force |
        Where-Object { $extensions -contains $_.Extension.ToLowerInvariant() } |
        Where-Object { $IncludeHidden -or -not ($_.Attributes -band [System.IO.FileAttributes]::Hidden) } |
        Where-Object { -not (Test-IsExcludedPath -File $_ -BasePath $targetRoot -Scope $scope) } |
        Sort-Object FullName

    foreach ($file in $candidateFiles) {
        $allCandidateFiles.Add([pscustomobject]@{
            Scope = $scope
            RootPath = $targetRoot
            File = $file
        })
    }

    $keyFiles = Get-ChildItem -Path $targetRoot -Recurse -File -Force |
        Where-Object {
            $_.Extension.ToLowerInvariant() -in @(".sln", ".csproj", ".props", ".targets", ".json", ".yml", ".yaml", ".html")
        } |
        Where-Object { $IncludeHidden -or -not ($_.Attributes -band [System.IO.FileAttributes]::Hidden) } |
        Where-Object { -not (Test-IsExcludedPath -File $_ -BasePath $targetRoot -Scope $scope) } |
        Sort-Object FullName

    foreach ($file in $keyFiles) {
        $solutionFiles.Add([pscustomobject]@{
            Scope = $scope
            RootPath = $targetRoot
            File = $file
        })
    }
}

$allCandidateFiles = $allCandidateFiles | Sort-Object { $_.Scope }, { $_.File.FullName }
$solutionFiles = $solutionFiles | Sort-Object { $_.Scope }, { $_.File.FullName }

$generationErrors = New-Object System.Collections.Generic.List[string]
$fileMetadata = New-Object System.Collections.Generic.List[object]

# ------------------------------------------------------------
# 5. Build numbering for full repository tree
# ------------------------------------------------------------

$numberByRelativePath = @{}
$nextIndexByParentPath = @{}

foreach ($entry in $allCandidateFiles) {
    $file = $entry.File
    $repositoryRelativePath = Get-RelativePath -FullPath $file.FullName -BasePath $RepositoryRoot
    $parts = $repositoryRelativePath -split '[\\/]'
    $currentPath = ""

    for ($i = 0; $i -lt $parts.Length; $i++) {
        $part = $parts[$i]

        if ([string]::IsNullOrWhiteSpace($currentPath)) {
            $currentPath = $part
            $parentPath = ""
        }
        else {
            $parentPath = ($parts[0..($i - 1)] -join "\")
            $currentPath = "$parentPath\$part"
        }

        if (-not $numberByRelativePath.ContainsKey($currentPath)) {
            if (-not $nextIndexByParentPath.ContainsKey($parentPath)) {
                $nextIndexByParentPath[$parentPath] = 1
            }

            $childIndex = $nextIndexByParentPath[$parentPath]
            $nextIndexByParentPath[$parentPath] = $childIndex + 1

            if ([string]::IsNullOrWhiteSpace($parentPath)) {
                $numberByRelativePath[$currentPath] = "$childIndex"
            }
            else {
                $numberByRelativePath[$currentPath] = "$($numberByRelativePath[$parentPath]).$childIndex"
            }
        }
    }
}

# ------------------------------------------------------------
# 6. Analyze files
# ------------------------------------------------------------

foreach ($entry in $allCandidateFiles) {
    $scope = $entry.Scope
    $targetRoot = $entry.RootPath
    $file = $entry.File

    try {
        $repositoryRelativePath = Get-RelativePath -FullPath $file.FullName -BasePath $RepositoryRoot
        $scopeRelativePath = Get-RelativePath -FullPath $file.FullName -BasePath $targetRoot
        $relativeParts = $scopeRelativePath -split '[\\/]'

        $projectName = $scope
        if ($relativeParts.Length -gt 0 -and -not [string]::IsNullOrWhiteSpace($relativeParts[0])) {
            $projectName = "$scope\$($relativeParts[0])"
        }

        $content = Read-TextFileSafe -Path $file.FullName
        $namespaceOrModule = Get-NamespaceOrModule -Content $content -Scope $scope
        $usingOrImports = Get-UsingOrImports -Content $content -Scope $scope
        $types = Get-TypeDeclarations -Content $content -Scope $scope
        $sha256 = (Get-FileHash -Path $file.FullName -Algorithm SHA256).Hash
        $classification = Get-FileClassification -File $file -Content $content -Scope $scope

        $fileMetadata.Add([pscustomobject]@{
            Number              = $numberByRelativePath[$repositoryRelativePath]
            Scope               = $scope
            ProjectName         = $projectName
            FileName            = $file.Name
            FullName            = $file.FullName
            RepositoryRelativePath = $repositoryRelativePath
            ScopeRelativePath   = $scopeRelativePath
            Directory           = $file.DirectoryName
            Extension           = $file.Extension
            SizeBytes           = $file.Length
            SizeReadable        = Convert-BytesToReadable -Bytes $file.Length
            CreatedAt           = $file.CreationTime
            LastModified        = $file.LastWriteTime
            LineCount           = Get-LineCount -Content $content
            NamespaceOrModule   = $namespaceOrModule
            TypeDeclarations    = ($types -join ", ")
            UsingOrImports      = $usingOrImports
            Sha256              = $sha256
            Classification      = $classification
        })
    }
    catch {
        $generationErrors.Add("Failed to analyze file '$($file.FullName)': $($_.Exception.Message)")
    }
}

# ------------------------------------------------------------
# 7. Write output
# ------------------------------------------------------------

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$writer = New-Object System.IO.StreamWriter($outputFilePath, $false, $utf8NoBom)

function Add-Line {
    param([AllowNull()][string]$Text = "")
    $script:writer.WriteLine($Text)
}

function Add-Separator {
    Add-Line "================================================================================"
}

function Add-Section {
    param([Parameter(Mandatory = $true)][string]$Title)
    Add-Line ""
    Add-Separator
    Add-Line $Title
    Add-Separator
}

try {
    $script:writer = $writer

    $totalLines = 0
    $totalBytes = 0
    $backendFileCount = 0
    $frontendFileCount = 0
    $backendLineCount = 0
    $frontendLineCount = 0

    foreach ($meta in $fileMetadata) {
        $totalLines += $meta.LineCount
        $totalBytes += $meta.SizeBytes

        if ($meta.Scope -eq "Backend") {
            $backendFileCount++
            $backendLineCount += $meta.LineCount
        }
        elseif ($meta.Scope -eq "Frontend") {
            $frontendFileCount++
            $frontendLineCount += $meta.LineCount
        }
    }

    Add-Separator
    Add-Line "PLANTPROCESS IQ FULL-STACK SOURCE DOCUMENTATION"
    Add-Separator
    Add-Line "Product Name          : PlantProcess IQ"
    Add-Line "Repository Root Path  : $RepositoryRoot"
    Add-Line "Backend Root Path     : $BackendRoot"
    Add-Line "Frontend Root Path    : $FrontendRoot"
    Add-Line "Documentation Folder  : $OutputFolder"
    Add-Line "Generated At          : $($generationDateTime.ToString('yyyy-MM-dd HH:mm:ss'))"
    Add-Line "Generated By          : GenerateProjectDocumentation.ps1"
    Add-Line "Documentation Level   : Advanced / Professional / Full-Stack Source Snapshot"
    Add-Line "Output File           : $outputFilePath"
    Add-Line "Backend Extensions    : $($normalizedBackendExtensions -join ', ')"
    Add-Line "Frontend Extensions   : $($normalizedFrontendExtensions -join ', ')"
    Add-Line "Total Included Files  : $($fileMetadata.Count)"
    Add-Line "Backend Files         : $backendFileCount"
    Add-Line "Frontend Files        : $frontendFileCount"
    Add-Line "Total Included Lines  : $totalLines"
    Add-Line "Backend Lines         : $backendLineCount"
    Add-Line "Frontend Lines        : $frontendLineCount"
    Add-Line "Total Included Size   : $(Convert-BytesToReadable -Bytes $totalBytes)"
    Add-Line "PowerShell Version    : $($PSVersionTable.PSVersion)"
    Add-Line "Machine Name          : $env:COMPUTERNAME"
    Add-Line "User Name             : $env:USERNAME"
    Add-Separator

    Add-Section "1. GENERATOR SETTINGS"
    Add-Line "RepositoryRoot                   : $RepositoryRoot"
    Add-Line "BackendRoot                      : $BackendRoot"
    Add-Line "FrontendRoot                     : $FrontendRoot"
    Add-Line "OutputFolder                     : $OutputFolder"
    Add-Line "IncludeHidden                    : $IncludeHidden"
    Add-Line "ExcludeMigrations                : $ExcludeMigrations"
    Add-Line "ExcludeGeneratedDesignerFiles    : $ExcludeGeneratedDesignerFiles"
    Add-Line "IncludeLockFiles                 : $IncludeLockFiles"
    Add-Line "Excluded Folders                 : $($excludedFolders -join ', ')"
    Add-Line "File Name Format                 : ${cleanProjectName}_FullStack_ddMMMyyyy_HHmm.txt"
    Add-Line "Current Generated File Name      : $(Split-Path $outputFilePath -Leaf)"

    Add-Section "2. SOLUTION / PROJECT / FRONTEND CONFIG FILES"
    if ($solutionFiles.Count -eq 0) {
        Add-Line "No solution/project/config files found."
    }
    else {
        foreach ($entry in $solutionFiles) {
            $file = $entry.File
            $relative = Get-RelativePath -FullPath $file.FullName -BasePath $RepositoryRoot
            Add-Line "- [$($entry.Scope)] $relative | $($file.Extension) | $(Convert-BytesToReadable -Bytes $file.Length) | Modified: $($file.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss'))"
        }
    }

    Add-Section "3. SUMMARY BY STACK"
    Add-Line ("{0,-15} {1,10} {2,14} {3,14}" -f "Stack", "Files", "Lines", "Size")
    Add-Line ("{0,-15} {1,10} {2,14} {3,14}" -f ("-" * 15), ("-" * 10), ("-" * 14), ("-" * 14))

    foreach ($group in ($fileMetadata | Group-Object Scope | Sort-Object Name)) {
        $lineSum = ($group.Group | Measure-Object -Property LineCount -Sum).Sum
        $byteSum = ($group.Group | Measure-Object -Property SizeBytes -Sum).Sum
        Add-Line ("{0,-15} {1,10} {2,14} {3,14}" -f $group.Name, $group.Count, $lineSum, (Convert-BytesToReadable -Bytes $byteSum))
    }

    Add-Section "4. SUMMARY BY PROJECT / TOP-LEVEL FOLDER"
    Add-Line ("{0,-40} {1,10} {2,14} {3,14}" -f "Project/Folder", "Files", "Lines", "Size")
    Add-Line ("{0,-40} {1,10} {2,14} {3,14}" -f ("-" * 40), ("-" * 10), ("-" * 14), ("-" * 14))

    foreach ($group in ($fileMetadata | Group-Object ProjectName | Sort-Object Name)) {
        $lineSum = ($group.Group | Measure-Object -Property LineCount -Sum).Sum
        $byteSum = ($group.Group | Measure-Object -Property SizeBytes -Sum).Sum
        Add-Line ("{0,-40} {1,10} {2,14} {3,14}" -f $group.Name, $group.Count, $lineSum, (Convert-BytesToReadable -Bytes $byteSum))
    }

    Add-Section "5. FILE CLASSIFICATION SUMMARY"
    Add-Line ("{0,-45} {1,10}" -f "Classification", "Files")
    Add-Line ("{0,-45} {1,10}" -f ("-" * 45), ("-" * 10))

    foreach ($group in ($fileMetadata | Group-Object Classification | Sort-Object Name)) {
        Add-Line ("{0,-45} {1,10}" -f $group.Name, $group.Count)
    }

    Add-Section "6. NUMBERED FULL-STACK PROJECT STRUCTURE"
    $printed = @{}

    foreach ($meta in ($fileMetadata | Sort-Object RepositoryRelativePath)) {
        $parts = $meta.RepositoryRelativePath -split '[\\/]'
        $currentPath = ""

        for ($i = 0; $i -lt $parts.Length; $i++) {
            $part = $parts[$i]

            if ([string]::IsNullOrWhiteSpace($currentPath)) {
                $currentPath = $part
            }
            else {
                $currentPath = "$currentPath\$part"
            }

            if (-not $printed.ContainsKey($currentPath)) {
                $indent = "    " * $i
                Add-Line "$indent$($numberByRelativePath[$currentPath]). $part"
                $printed[$currentPath] = $true
            }
        }
    }

    Add-Section "7. FILE INDEX"
    Add-Line ("{0,-12} {1,-10} {2,-38} {3,-42} {4,8} {5,12}" -f "No.", "Stack", "Project", "File", "Lines", "Size")
    Add-Line ("{0,-12} {1,-10} {2,-38} {3,-42} {4,8} {5,12}" -f ("-" * 12), ("-" * 10), ("-" * 38), ("-" * 42), ("-" * 8), ("-" * 12))

    foreach ($meta in ($fileMetadata | Sort-Object RepositoryRelativePath)) {
        Add-Line ("{0,-12} {1,-10} {2,-38} {3,-42} {4,8} {5,12}" -f $meta.Number, $meta.Scope, $meta.ProjectName, $meta.FileName, $meta.LineCount, $meta.SizeReadable)
    }

    Add-Section "8. FULL FILE CONTENT APPENDIX"

    foreach ($meta in ($fileMetadata | Sort-Object RepositoryRelativePath)) {
        Add-Line ""
        Add-Separator
        Add-Line "$($meta.Number). $($meta.FileName)"
        Add-Separator
        Add-Line "Stack             : $($meta.Scope)"
        Add-Line "Project / Folder  : $($meta.ProjectName)"
        Add-Line "Full Path         : $($meta.FullName)"
        Add-Line "Repository Path   : $($meta.RepositoryRelativePath)"
        Add-Line "Scope Path        : $($meta.ScopeRelativePath)"
        Add-Line "Directory         : $($meta.Directory)"
        Add-Line "Classification    : $($meta.Classification)"
        Add-Line "Namespace/Module  : $($meta.NamespaceOrModule)"
        Add-Line "Type Declarations : $($meta.TypeDeclarations)"
        Add-Line "Line Count        : $($meta.LineCount)"
        Add-Line "Size              : $($meta.SizeReadable)"
        Add-Line "Created At        : $($meta.CreatedAt.ToString('yyyy-MM-dd HH:mm:ss'))"
        Add-Line "Last Modified     : $($meta.LastModified.ToString('yyyy-MM-dd HH:mm:ss'))"
        Add-Line "SHA256            : $($meta.Sha256)"
        Add-Line "Using/Imports     : $($meta.UsingOrImports)"
        Add-Separator
        Add-Line ""

        try {
            $content = Read-TextFileSafe -Path $meta.FullName
            Add-Line $content
        }
        catch {
            $errorMessage = "Failed to write content for '$($meta.FullName)': $($_.Exception.Message)"
            Add-Line $errorMessage
            $generationErrors.Add($errorMessage)
        }

        Add-Line ""
    }

    Add-Section "9. GENERATION WARNINGS / ERRORS"
    if ($generationErrors.Count -eq 0) {
        Add-Line "No generation errors."
    }
    else {
        foreach ($err in $generationErrors) {
            Add-Line "- $err"
        }
    }

    Add-Line ""
    Add-Separator
    Add-Line "END OF PLANTPROCESS IQ FULL-STACK SOURCE DOCUMENTATION"
    Add-Separator
}
finally {
    $writer.Dispose()
}

Write-Host ""
Write-Host "Full-stack documentation generated successfully." -ForegroundColor Green
Write-Host "Output file:" -ForegroundColor Cyan
Write-Host $outputFilePath
Write-Host ""
Write-Host "Repository root       : $RepositoryRoot" -ForegroundColor Yellow
Write-Host "Backend root          : $BackendRoot" -ForegroundColor Yellow
Write-Host "Frontend root         : $FrontendRoot" -ForegroundColor Yellow
Write-Host "Documentation folder  : $OutputFolder" -ForegroundColor Yellow
Write-Host "Included files        : $($fileMetadata.Count)" -ForegroundColor Yellow
Write-Host "Total included lines  : $totalLines" -ForegroundColor Yellow
Write-Host ""

if ($OpenAfterGeneration) {
    Start-Process notepad.exe $outputFilePath
}
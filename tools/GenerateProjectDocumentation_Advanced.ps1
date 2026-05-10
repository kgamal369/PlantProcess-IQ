#requires -Version 5.1
<#
.SYNOPSIS
    Generates a professional full source-code documentation TXT file for a .NET/C# solution.

.DESCRIPTION
    This script scans the project root recursively, finds all .cs files, builds a professional
    documentation TXT file containing:
      - Generator metadata
      - Solution/project summary
      - Project structure tree with numbering
      - File index
      - Per-file metadata
      - Full source code content

.DEFAULT BEHAVIOR
    Expected script location:
        C:\Workspace\PlantProcess-IQ\tools\GenerateProjectDocumentation.ps1

    Default project root:
        One folder above the tools folder:
        C:\Workspace\PlantProcess-IQ

    Default output:
        Same tools folder.

    Output file name example:
        PlantProcessIQ_10May2026_1015.txt

.NOTES
    The script excludes bin, obj, .git, .vs, node_modules and other non-source folders.
    It uses UTF-8 without BOM.
#>

[CmdletBinding()]
param(
    [string]$RootPath,
    [string]$OutputFolder,

    # Keep default as .cs because the main purpose is full C# source documentation.
    [string[]]$IncludeExtensions = @(".cs"),

    # Optional switches.
    [switch]$IncludeHidden,
    [switch]$ExcludeMigrations,
    [switch]$ExcludeGeneratedDesignerFiles,
    [switch]$OpenAfterGeneration
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ------------------------------------------------------------
# 1. Resolve paths
# ------------------------------------------------------------

$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path

if ([string]::IsNullOrWhiteSpace($RootPath)) {
    $RootPath = Split-Path -Parent $scriptDirectory
}

if (-not (Test-Path -LiteralPath $RootPath)) {
    throw "Root path does not exist: $RootPath"
}

$RootPath = (Resolve-Path -LiteralPath $RootPath).Path

if ([string]::IsNullOrWhiteSpace($OutputFolder)) {
    $OutputFolder = $scriptDirectory
}

if (-not (Test-Path -LiteralPath $OutputFolder)) {
    New-Item -ItemType Directory -Path $OutputFolder | Out-Null
}

$OutputFolder = (Resolve-Path -LiteralPath $OutputFolder).Path

# ------------------------------------------------------------
# 2. Naming
# ------------------------------------------------------------

$projectFolderName = Split-Path $RootPath -Leaf
$cleanProjectName = ($projectFolderName -replace '[^a-zA-Z0-9]', '')

$culture = [System.Globalization.CultureInfo]::InvariantCulture
$generationDateTime = Get-Date
$timestampForFileName = $generationDateTime.ToString("ddMMMyyyy_HHmm", $culture)

$outputFileName = "${cleanProjectName}_${timestampForFileName}.txt"
$outputFilePath = Join-Path $OutputFolder $outputFileName

# Avoid overwriting if the script is run twice in the same minute.
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
    "packages",
    "TestResults",
    "coverage",
    ".sonarqube"
)

$normalizedExtensions = $IncludeExtensions |
    ForEach-Object {
        if ($_.StartsWith(".")) {
            $_.ToLowerInvariant()
        }
        else {
            ".$($_.ToLowerInvariant())"
        }
    }

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

function Test-IsExcludedPath {
    param(
        [Parameter(Mandatory = $true)][string]$FullPath,
        [Parameter(Mandatory = $true)][string]$BasePath
    )

    $relativePath = Get-RelativePath -FullPath $FullPath -BasePath $BasePath
    $segments = $relativePath -split '[\\/]'

    foreach ($segment in $segments) {
        if ($excludedFolders -contains $segment) {
            return $true
        }
    }

    if ($ExcludeMigrations -and ($segments -contains "Migrations")) {
        return $true
    }

    if ($ExcludeGeneratedDesignerFiles -and ($FullPath -like "*.Designer.cs")) {
        return $true
    }

    return $false
}

function Read-TextFileSafe {
    param(
        [Parameter(Mandatory = $true)][string]$Path
    )

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
    param(
        [AllowNull()][string]$Content
    )

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
        [AllowNull()][string]$Content
    )

    if ([string]::IsNullOrWhiteSpace($Content)) {
        return @()
    }

    $pattern = '(?m)^\s*(?:public|private|internal|protected|static|sealed|abstract|partial|readonly|\s)*\s*(class|record|interface|enum|struct)\s+([A-Za-z_][A-Za-z0-9_]*)'

    return [regex]::Matches($Content, $pattern) |
        ForEach-Object { "$($_.Groups[1].Value) $($_.Groups[2].Value)" } |
        Sort-Object -Unique
}

function Get-FileClassification {
    param(
        [Parameter(Mandatory = $true)][System.IO.FileInfo]$File,
        [AllowNull()][string]$Content
    )

    if ($File.FullName -like "*\Migrations\*") {
        return "EF Migration / Generated Database Code"
    }

    if ($File.Name -like "*.Designer.cs" -or ($Content -match "<auto-generated")) {
        return "Generated / Designer Code"
    }

    if ($File.Name -eq "Program.cs") {
        return "Application Entry Point"
    }

    if ($File.Name -like "*Endpoints.cs") {
        return "API Endpoint"
    }

    if ($File.FullName -like "*\Configurations\*") {
        return "EF Core Configuration"
    }

    if ($File.FullName -like "*\Entities\*") {
        return "Domain Entity"
    }

    if ($File.FullName -like "*\Common\*") {
        return "Common Base / Shared Code"
    }

    if ($File.FullName -like "*\Bulk\*") {
        return "Bulk / Ingestion Support"
    }

    return "C# Source File"
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
# 4. Collect files
# ------------------------------------------------------------

$allCandidateFiles = Get-ChildItem -Path $RootPath -Recurse -File -Force |
    Where-Object {
        $normalizedExtensions -contains $_.Extension.ToLowerInvariant()
    } |
    Where-Object {
        $IncludeHidden -or -not ($_.Attributes -band [System.IO.FileAttributes]::Hidden)
    } |
    Where-Object {
        -not (Test-IsExcludedPath -FullPath $_.FullName -BasePath $RootPath)
    } |
    Sort-Object FullName

$solutionFiles = Get-ChildItem -Path $RootPath -Recurse -File -Force |
    Where-Object {
        $_.Extension.ToLowerInvariant() -in @(".sln", ".csproj", ".props", ".targets", ".json", ".yml", ".yaml")
    } |
    Where-Object {
        -not (Test-IsExcludedPath -FullPath $_.FullName -BasePath $RootPath)
    } |
    Sort-Object FullName

$generationErrors = New-Object System.Collections.Generic.List[string]
$fileMetadata = New-Object System.Collections.Generic.List[object]

# ------------------------------------------------------------
# 5. Build numbering for folder/file tree
# ------------------------------------------------------------

$numberByRelativePath = @{}
$nextIndexByParentPath = @{}

foreach ($file in $allCandidateFiles) {
    $relativePath = Get-RelativePath -FullPath $file.FullName -BasePath $RootPath
    $parts = $relativePath -split '[\\/]'
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

foreach ($file in $allCandidateFiles) {
    try {
        $relativePath = Get-RelativePath -FullPath $file.FullName -BasePath $RootPath
        $relativeParts = $relativePath -split '[\\/]'
        $projectName = $relativeParts[0]

        $content = Read-TextFileSafe -Path $file.FullName
        $namespace = Get-FirstRegexGroup -Content $content -Pattern '(?m)^\s*namespace\s+([A-Za-z_][A-Za-z0-9_.]*)\s*[;{]'
        $usings = Get-DistinctRegexGroups -Content $content -Pattern '(?m)^\s*using\s+([A-Za-z_][A-Za-z0-9_.]*)\s*;' -GroupIndex 1
        $types = Get-TypeDeclarations -Content $content
        $sha256 = (Get-FileHash -Path $file.FullName -Algorithm SHA256).Hash
        $classification = Get-FileClassification -File $file -Content $content

        $fileMetadata.Add([pscustomobject]@{
            Number          = $numberByRelativePath[$relativePath]
            ProjectName     = $projectName
            FileName        = $file.Name
            FullName        = $file.FullName
            RelativePath    = $relativePath
            Directory       = $file.DirectoryName
            Extension       = $file.Extension
            SizeBytes       = $file.Length
            SizeReadable    = Convert-BytesToReadable -Bytes $file.Length
            CreatedAt       = $file.CreationTime
            LastModified    = $file.LastWriteTime
            LineCount       = Get-LineCount -Content $content
            Namespace       = $namespace
            TypeDeclarations = ($types -join ", ")
            UsingNamespaces = ($usings -join ", ")
            Sha256          = $sha256
            Classification  = $classification
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

    foreach ($meta in $fileMetadata) {
        $totalLines += $meta.LineCount
        $totalBytes += $meta.SizeBytes
    }

    # Header
    Add-Separator
    Add-Line "PROJECT SOURCE DOCUMENTATION"
    Add-Separator
    Add-Line "Project Name        : $projectFolderName"
    Add-Line "Project Root Path   : $RootPath"
    Add-Line "Generated At        : $($generationDateTime.ToString('yyyy-MM-dd HH:mm:ss'))"
    Add-Line "Generated By        : GenerateProjectDocumentation.ps1"
    Add-Line "Documentation Level : Advanced / Professional / Full Source Snapshot"
    Add-Line "Output File         : $outputFilePath"
    Add-Line "Included Extensions : $($normalizedExtensions -join ', ')"
    Add-Line "Total Source Files  : $($fileMetadata.Count)"
    Add-Line "Total Source Lines  : $totalLines"
    Add-Line "Total Source Size   : $(Convert-BytesToReadable -Bytes $totalBytes)"
    Add-Line "PowerShell Version  : $($PSVersionTable.PSVersion)"
    Add-Line "Machine Name        : $env:COMPUTERNAME"
    Add-Line "User Name           : $env:USERNAME"
    Add-Separator

    # Generator settings
    Add-Section "1. GENERATOR SETTINGS"
    Add-Line "RootPath                         : $RootPath"
    Add-Line "OutputFolder                     : $OutputFolder"
    Add-Line "IncludeHidden                    : $IncludeHidden"
    Add-Line "ExcludeMigrations                : $ExcludeMigrations"
    Add-Line "ExcludeGeneratedDesignerFiles    : $ExcludeGeneratedDesignerFiles"
    Add-Line "Excluded Folders                 : $($excludedFolders -join ', ')"
    Add-Line "File Name Format                 : ${cleanProjectName}_ddMMMyyyy_HHmm.txt"
    Add-Line "Current Generated File Name      : $(Split-Path $outputFilePath -Leaf)"

    # Solution/project files
    Add-Section "2. SOLUTION AND PROJECT FILES"
    if ($solutionFiles.Count -eq 0) {
        Add-Line "No solution/project/config files found."
    }
    else {
        foreach ($file in $solutionFiles) {
            $relative = Get-RelativePath -FullPath $file.FullName -BasePath $RootPath
            Add-Line "- $relative | $($file.Extension) | $(Convert-BytesToReadable -Bytes $file.Length) | Modified: $($file.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss'))"
        }
    }

    # Project summary
    Add-Section "3. PROJECT SUMMARY BY TOP-LEVEL FOLDER"
    Add-Line ("{0,-35} {1,10} {2,14} {3,14}" -f "Project/Folder", "Files", "Lines", "Size")
    Add-Line ("{0,-35} {1,10} {2,14} {3,14}" -f ("-" * 35), ("-" * 10), ("-" * 14), ("-" * 14))

    foreach ($group in ($fileMetadata | Group-Object ProjectName | Sort-Object Name)) {
        $lineSum = ($group.Group | Measure-Object -Property LineCount -Sum).Sum
        $byteSum = ($group.Group | Measure-Object -Property SizeBytes -Sum).Sum

        Add-Line ("{0,-35} {1,10} {2,14} {3,14}" -f $group.Name, $group.Count, $lineSum, (Convert-BytesToReadable -Bytes $byteSum))
    }

    # Classification summary
    Add-Section "4. FILE CLASSIFICATION SUMMARY"
    Add-Line ("{0,-45} {1,10}" -f "Classification", "Files")
    Add-Line ("{0,-45} {1,10}" -f ("-" * 45), ("-" * 10))

    foreach ($group in ($fileMetadata | Group-Object Classification | Sort-Object Name)) {
        Add-Line ("{0,-45} {1,10}" -f $group.Name, $group.Count)
    }

    # Numbered tree
    Add-Section "5. NUMBERED PROJECT STRUCTURE"
    $printed = @{}

    foreach ($file in $allCandidateFiles) {
        $relativePath = Get-RelativePath -FullPath $file.FullName -BasePath $RootPath
        $parts = $relativePath -split '[\\/]'
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

    # File index
    Add-Section "6. FILE INDEX"
    Add-Line ("{0,-12} {1,-30} {2,-45} {3,8} {4,12}" -f "No.", "Project", "File", "Lines", "Size")
    Add-Line ("{0,-12} {1,-30} {2,-45} {3,8} {4,12}" -f ("-" * 12), ("-" * 30), ("-" * 45), ("-" * 8), ("-" * 12))

    foreach ($meta in ($fileMetadata | Sort-Object RelativePath)) {
        Add-Line ("{0,-12} {1,-30} {2,-45} {3,8} {4,12}" -f $meta.Number, $meta.ProjectName, $meta.FileName, $meta.LineCount, $meta.SizeReadable)
    }

    # Full source code
    Add-Section "7. FULL SOURCE CODE APPENDIX"

    foreach ($meta in ($fileMetadata | Sort-Object RelativePath)) {
        Add-Line ""
        Add-Separator
        Add-Line "$($meta.Number). $($meta.FileName)"
        Add-Separator
        Add-Line "Full Path         : $($meta.FullName)"
        Add-Line "Relative Path     : $($meta.RelativePath)"
        Add-Line "Project / Folder  : $($meta.ProjectName)"
        Add-Line "Directory         : $($meta.Directory)"
        Add-Line "Classification    : $($meta.Classification)"
        Add-Line "Namespace         : $($meta.Namespace)"
        Add-Line "Type Declarations : $($meta.TypeDeclarations)"
        Add-Line "Line Count        : $($meta.LineCount)"
        Add-Line "Size              : $($meta.SizeReadable)"
        Add-Line "Created At        : $($meta.CreatedAt.ToString('yyyy-MM-dd HH:mm:ss'))"
        Add-Line "Last Modified     : $($meta.LastModified.ToString('yyyy-MM-dd HH:mm:ss'))"
        Add-Line "SHA256            : $($meta.Sha256)"
        Add-Line "Using Namespaces  : $($meta.UsingNamespaces)"
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

    # Errors
    Add-Section "8. GENERATION WARNINGS / ERRORS"
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
    Add-Line "END OF PROJECT SOURCE DOCUMENTATION"
    Add-Separator
}
finally {
    $writer.Dispose()
}

Write-Host ""
Write-Host "Advanced documentation generated successfully." -ForegroundColor Green
Write-Host "Output file:" -ForegroundColor Cyan
Write-Host $outputFilePath
Write-Host ""
Write-Host "Included source files: $($fileMetadata.Count)" -ForegroundColor Yellow
Write-Host "Total source lines   : $totalLines" -ForegroundColor Yellow
Write-Host ""

if ($OpenAfterGeneration) {
    Start-Process notepad.exe $outputFilePath
}

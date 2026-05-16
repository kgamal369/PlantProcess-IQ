#requires -Version 5.1
<#
.SYNOPSIS
    Enterprise AI Codebase Extractor for PlantProcess IQ.
    Generates 5 highly structured, AI-optimized text files for Iterative Deep Auditing.

.DESCRIPTION
    Advanced features:
    - Creates a dynamic 'GeminiExport_DateTime' directory.
    - Captures .sql database files into a dedicated chunk.
    - Captures .env*, docker-compose*, and solution files into a configuration chunk.
    - Generates AI-optimized [METADATA] and Table of Contents tags.
    - Safely extracts environment variables keys.
#>

[CmdletBinding()]
param(
    [string]$RepositoryRoot,
    [string]$OutputBaseFolder,
    [switch]$IncludeHidden,
    [switch]$OpenAfterGeneration
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ====================================================================
# 1. PATH RESOLUTION & SETUP
# ====================================================================
$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path

function Resolve-Root {
    param([string]$StartPath)
    $curr = $StartPath
    for ($i = 0; $i -lt 5; $i++) {
        if (Test-Path (Join-Path $curr "Backend")) { return $curr }
        $curr = Split-Path $curr -Parent
        if ([string]::IsNullOrEmpty($curr)) { break }
    }
    return $StartPath
}

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) { $RepositoryRoot = Resolve-Root $scriptDirectory }
if (-not (Test-Path $RepositoryRoot)) { throw "Repository root not found: $RepositoryRoot" }

$BackendRoot = Join-Path $RepositoryRoot "Backend"
$FrontendRoot = Join-Path $RepositoryRoot "Frontend"

# Create GeminiExport_Date_Time Folder
$timestamp = (Get-Date).ToString("ddMMMyyyy_HHmm")
if ([string]::IsNullOrWhiteSpace($OutputBaseFolder)) { $OutputBaseFolder = Join-Path $RepositoryRoot "Documentation" }
$ExportDir = Join-Path $OutputBaseFolder "GeminiExport_$timestamp"
New-Item -ItemType Directory -Path $ExportDir -Force | Out-Null

Write-Host "========================================================" -ForegroundColor Cyan
Write-Host " PLANTPROCESS IQ - ENTERPRISE AI EXTRACTOR" -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "Target: $RepositoryRoot"
Write-Host "Export: $ExportDir"
Write-Host "========================================================" -ForegroundColor Cyan

# ====================================================================
# 2. ADVANCED FILE COLLECTION
# ====================================================================
$allowedExtensions = @(".cs", ".csproj", ".ts", ".tsx", ".js", ".jsx", ".css", ".html", ".sql", ".json", ".yml", ".yaml", ".md", ".sln")
$excludedFolders = @("\.git\", "\.vs\", "\bin\", "\obj\", "\node_modules\", "\dist\", "\build\", "\coverage\", "\migrations\")

function Is-FileValid($file) {
    $path = $file.FullName
    foreach ($ex in $excludedFolders) {
        if ($path -match ($ex -replace '\\', '[\\/]')) { return $false }
    }
    
    # Catch Env files and Docker compose explicitly
    if ($file.Name -match "^\.env" -or $file.Name -match "^docker-compose") { return $true }
    
    # Check standard extensions
    if ($allowedExtensions -contains $file.Extension.ToLowerInvariant()) { return $true }
    
    return $false
}

$allFiles = New-Object System.Collections.Generic.List[object]
$searchPaths = @()
if (Test-Path $BackendRoot) { $searchPaths += [pscustomobject]@{ Path=$BackendRoot; Scope="Backend"} }
if (Test-Path $FrontendRoot) { $searchPaths += [pscustomobject]@{ Path=$FrontendRoot; Scope="Frontend"} }
$searchPaths += [pscustomobject]@{ Path=$RepositoryRoot; Scope="Root"} # Catch root docker-compose/.env files

$processedPaths = New-Object System.Collections.Generic.HashSet[string]

foreach ($target in $searchPaths) {
    $files = Get-ChildItem -Path $target.Path -File -Recurse -Force | Where-Object { Is-FileValid $_ }
    
    foreach ($f in $files) {
        if ($processedPaths.Contains($f.FullName)) { continue }
        $processedPaths.Add($f.FullName) | Out-Null
        
        $relPath = $f.FullName.Substring($RepositoryRoot.Length).Trim('\', '/')
        $allFiles.Add([pscustomobject]@{
            Scope = $target.Scope
            File = $f
            FullName = $f.FullName
            Name = $f.Name
            RelativePath = $relPath
            Extension = $f.Extension.ToLowerInvariant()
            Size = $f.Length
            Lines = 0
            Content = ""
        })
    }
}

# ====================================================================
# 3. DEEP CONTENT READING & METADATA EXTRACTION
# ====================================================================
for ($i = 0; $i -lt $allFiles.Count; $i++) {
    $f = $allFiles[$i]
    Write-Progress -Activity "Reading Source Code" -Status "Processing: $($f.Name)" -PercentComplete (($i / $allFiles.Count) * 100)
    
    try {
        $content = [System.IO.File]::ReadAllText($f.FullName, [System.Text.Encoding]::UTF8)
        $f.Lines = ($content -split '\r?\n').Length
        
        # Advanced masking for .env files to protect secrets but show structure
        if ($f.Name -match "^\.env") {
            $masked = @()
            foreach ($line in ($content -split '\r?\n')) {
                if ($line -match "^([A-Z0-9_]+)=" -and -not $line.StartsWith("#")) {
                    $key = $matches[1]
                    $masked += "$key=[MASKED_FOR_AI_AUDIT]"
                } else {
                    $masked += $line
                }
            }
            $f.Content = $masked -join "`n"
        } else {
            $f.Content = $content
        }
    } catch {
        $f.Content = ">> ERROR READING FILE <<"
    }
}
Write-Progress -Activity "Reading Source Code" -Completed

# ====================================================================
# 4. CHUNKING LOGIC (THE 5 FILES)
# ====================================================================
$chunks = @(
    @{
        Id = "1_BackendCore"
        Title = "Backend Domain & Application"
        Filter = { $_.Scope -eq "Backend" -and ($_.RelativePath -match "PlantProcess\.Domain" -or $_.RelativePath -match "PlantProcess\.Application") }
    },
    @{
        Id = "2_BackendInfra"
        Title = "Backend API, Infra & Workers"
        Filter = { $_.Scope -eq "Backend" -and ($_.RelativePath -match "PlantProcess\.Infrastructure" -or $_.RelativePath -match "PlantProcess\.Workers" -or $_.RelativePath -match "PlantProcess\.Api") }
    },
    @{
        Id = "3_Frontend"
        Title = "React UI Architecture"
        Filter = { $_.Scope -eq "Frontend" }
    },
    @{
        Id = "4_Database"
        Title = "SQL Database Scripts"
        Filter = { $_.Scope -eq "Backend" -and $_.RelativePath -match "[\\/]database[\\/]" -and $_.Extension -eq ".sql" }
    }
)

$assignedPaths = New-Object System.Collections.Generic.HashSet[string]
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)

foreach ($chunk in $chunks) {
    $chunkFiles = $allFiles | Where-Object $chunk.Filter | Sort-Object RelativePath
    
    if ($chunkFiles.Count -eq 0) { continue }
    $chunkFiles | ForEach-Object { $assignedPaths.Add($_.FullName) | Out-Null }
    
    $outPath = Join-Path $ExportDir "PlantProcessIQ_Audit_$($chunk.Id).txt"
    $writer = New-Object System.IO.StreamWriter($outPath, $false, $utf8NoBom)
    
    # WRITE HEADER & TOC
    $writer.WriteLine("=====================================================================")
    $writer.WriteLine(" AI AUDIT TARGET : $($chunk.Title)")
    $writer.WriteLine(" TOTAL FILES     : $($chunkFiles.Count)")
    $writer.WriteLine("=====================================================================")
    $writer.WriteLine(" TABLE OF CONTENTS:")
    foreach ($cf in $chunkFiles) {
        $writer.WriteLine("  - $($cf.RelativePath) ($($cf.Lines) lines)")
    }
    $writer.WriteLine("=====================================================================`n")
    
    # WRITE FILES WITH AI TAGS
    foreach ($cf in $chunkFiles) {
        $writer.WriteLine("[FILE_START]")
        $writer.WriteLine("[METADATA: Path='$($cf.RelativePath)', Lines=$($cf.Lines), Size=$($cf.Size) bytes]")
        $writer.WriteLine($cf.Content)
        $writer.WriteLine("[FILE_END]`n")
    }
    $writer.Dispose()
    Write-Host " => Generated: $($chunk.Id) ($($chunkFiles.Count) files)" -ForegroundColor Green
}

# ====================================================================
# 5. GENERATE CHUNK 5: MISC & CONFIG (.env, docker-compose)
# ====================================================================
$miscFiles = $allFiles | Where-Object { -not $assignedPaths.Contains($_.FullName) } | Sort-Object RelativePath
if ($miscFiles.Count -gt 0) {
    $outPath = Join-Path $ExportDir "PlantProcessIQ_Audit_5_MiscConfig.txt"
    $writer = New-Object System.IO.StreamWriter($outPath, $false, $utf8NoBom)
    
    $writer.WriteLine("=====================================================================")
    $writer.WriteLine(" AI AUDIT TARGET : Configuration & Misc (.env, docker, sln)")
    $writer.WriteLine(" TOTAL FILES     : $($miscFiles.Count)")
    $writer.WriteLine("=====================================================================")
    $writer.WriteLine(" TABLE OF CONTENTS:")
    foreach ($cf in $miscFiles) {
        $writer.WriteLine("  - $($cf.RelativePath) ($($cf.Lines) lines)")
    }
    $writer.WriteLine("=====================================================================`n")
    
    foreach ($cf in $miscFiles) {
        $writer.WriteLine("[FILE_START]")
        $writer.WriteLine("[METADATA: Path='$($cf.RelativePath)']")
        $writer.WriteLine($cf.Content)
        $writer.WriteLine("[FILE_END]`n")
    }
    $writer.Dispose()
    Write-Host " => Generated: 5_MiscConfig ($($miscFiles.Count) files)" -ForegroundColor Green
}

Write-Host "========================================================" -ForegroundColor Cyan
Write-Host " Extraction Complete. Ready for AI Iterative Audit." -ForegroundColor Cyan
Write-Host " Directory: $ExportDir"
if ($OpenAfterGeneration) { Invoke-Item $ExportDir }
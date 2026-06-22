[CmdletBinding()]
param(
    [string]$ProjectRoot = (Resolve-Path ".").Path
)

$ErrorActionPreference = "Stop"

$archive = Join-Path $ProjectRoot "tools\archive\deduplicated-build-files"
if (-not (Test-Path $archive)) { New-Item -ItemType Directory -Force -Path $archive | Out-Null }

function Hash([string]$path) {
    if (-not (Test-Path $path)) { return $null }
    return (Get-FileHash -Algorithm SHA256 -Path $path).Hash
}

$rootJenkins = Join-Path $ProjectRoot "Jenkinsfile"
$deployJenkins = Join-Path $ProjectRoot "deploy\ci\Jenkinsfile"

if ((Test-Path $rootJenkins) -and (Test-Path $deployJenkins)) {
    if ((Hash $rootJenkins) -eq (Hash $deployJenkins)) {
        Move-Item $deployJenkins (Join-Path $archive "Jenkinsfile.deploy-ci.archived") -Force
        Write-Host "Archived duplicate deploy/ci/Jenkinsfile; root Jenkinsfile remains authoritative." -ForegroundColor Green
    }
    else {
        $doc = Join-Path $ProjectRoot "docs\phase1-phase2\JENKINSFILE_AUTHORITY.md"
        $txt = "# Jenkinsfile authority`r`n`r`nRoot Jenkinsfile and deploy/ci/Jenkinsfile are not byte-identical. No automatic deletion was performed. Root Jenkinsfile is treated as authoritative until CI owner confirms otherwise.`r`n"
        [IO.File]::WriteAllText($doc, $txt, (New-Object Text.UTF8Encoding($false)))
        Write-Host "Jenkinsfiles differ; wrote authority note instead of deleting." -ForegroundColor Yellow
    }
}

$workers = Get-ChildItem -Path $ProjectRoot -Recurse -File -Filter "TelemetryIngestionWorker.cs" |
    Where-Object { $_.FullName -notmatch "\\bin\\|\\obj\\|\\.git\\" }

if ($workers.Count -gt 1) {
    $groups = $workers | Group-Object { (Get-FileHash -Algorithm SHA256 -Path $_.FullName).Hash }
    foreach ($g in $groups) {
        if ($g.Count -gt 1) {
            $keep = $g.Group | Sort-Object FullName | Select-Object -First 1
            $remove = $g.Group | Where-Object { $_.FullName -ne $keep.FullName }
            foreach ($r in $remove) {
                $name = $r.FullName.Substring($ProjectRoot.Length).TrimStart("\","/").Replace("\","__")
                Move-Item $r.FullName (Join-Path $archive $name) -Force
                Write-Host "Archived duplicate TelemetryIngestionWorker: $name" -ForegroundColor Green
            }
        }
    }
}
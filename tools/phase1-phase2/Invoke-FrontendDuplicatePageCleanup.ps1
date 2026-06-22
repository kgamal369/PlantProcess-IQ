[CmdletBinding()]
param(
    [string]$ProjectRoot = (Resolve-Path ".").Path
)

$ErrorActionPreference = "Stop"

$frontend = Join-Path $ProjectRoot "Frontend\PlantProcess.Web"
$src = Join-Path $frontend "src"
$archive = Join-Path $ProjectRoot "tools\archive\retired-duplicate-pages"

function Ensure-Dir([string]$p) {
    if (-not (Test-Path $p)) { New-Item -ItemType Directory -Force -Path $p | Out-Null }
}

function Replace-InSource([string]$Old, [string]$New) {
    Get-ChildItem -Path $src -Recurse -File -Include "*.ts","*.tsx" |
        Where-Object { $_.FullName -notmatch "\\node_modules\\|\\dist\\" } |
        ForEach-Object {
            $text = [IO.File]::ReadAllText($_.FullName)
            $next = $text.Replace($Old, $New)
            if ($next -ne $text) {
                [IO.File]::WriteAllText($_.FullName, $next, (New-Object Text.UTF8Encoding($false)))
                Write-Host "Updated import in $($_.FullName)" -ForegroundColor Green
            }
        }
}

Ensure-Dir $archive

$pairs = @(
    @{ Delete = "src\pages\AdminPage.tsx"; KeepImport = "@/pages/Admin/AdminPage"; Old1 = "@/pages/AdminPage"; Old2 = "./pages/AdminPage"; New2 = "./pages/Admin/AdminPage" },
    @{ Delete = "src\pages\CorrelationPage.tsx"; KeepImport = "@/pages/Correlation/CorrelationPage"; Old1 = "@/pages/CorrelationPage"; Old2 = "./pages/CorrelationPage"; New2 = "./pages/Correlation/CorrelationPage" },
    @{ Delete = "src\pages\DashboardPage.tsx"; KeepImport = "@/pages/Dashboard/DashboardPage"; Old1 = "@/pages/DashboardPage"; Old2 = "./pages/DashboardPage"; New2 = "./pages/Dashboard/DashboardPage" },
    @{ Delete = "src\pages\DataQualityPage.tsx"; KeepImport = "@/pages/DataQuality/DataQualityPage"; Old1 = "@/pages/DataQualityPage"; Old2 = "./pages/DataQualityPage"; New2 = "./pages/DataQuality/DataQualityPage" },
    @{ Delete = "src\pages\RiskDashboardPage.tsx"; KeepImport = "@/pages/RiskDashboard/RiskDashboardPage"; Old1 = "@/pages/RiskDashboardPage"; Old2 = "./pages/RiskDashboardPage"; New2 = "./pages/RiskDashboard/RiskDashboardPage" }
)

foreach ($p in $pairs) {
    Replace-InSource $p.Old1 $p.KeepImport
    Replace-InSource $p.Old2 $p.New2

    $full = Join-Path $frontend $p.Delete
    if (Test-Path $full) {
        $dest = Join-Path $archive ($p.Delete.Replace("\", "__"))
        Move-Item -Path $full -Destination $dest -Force
        Write-Host "Archived duplicate page: $($p.Delete)" -ForegroundColor Green
    }
}

# MaterialInvestigation is intentionally kept flat if the router already uses it.
# If both remain, this script leaves the active route untouched to avoid regression.

Write-Host "Duplicate page cleanup complete." -ForegroundColor Cyan
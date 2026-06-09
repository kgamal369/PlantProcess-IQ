param(
    [switch]$RunE2E
)

$ErrorActionPreference = "Stop"

function Invoke-NativeChecked {
    param(
        [Parameter(Mandatory=$true)][string]$FilePath,
        [Parameter(Mandatory=$true)][string[]]$Arguments,
        [string]$WorkingDirectory = ""
    )

    if ($WorkingDirectory -and $WorkingDirectory.Trim().Length -gt 0) {
        Push-Location $WorkingDirectory
    }

    try {
        & $FilePath @Arguments

        if ($LASTEXITCODE -ne 0) {
            $message = "Command failed with exit code {0}: {1} {2}" -f $LASTEXITCODE, $FilePath, ($Arguments -join " ")
            throw $message
        }
    }
    finally {
        if ($WorkingDirectory -and $WorkingDirectory.Trim().Length -gt 0) {
            Pop-Location
        }
    }
}

Write-Host "PPIQ Phase 11 UI hardening rollup" -ForegroundColor Cyan

Write-Host "---- Phase 11 file/source validator" -ForegroundColor Cyan
Invoke-NativeChecked -FilePath "node" -Arguments @(".\tools\phase11\validate-phase11-ui-hardening.cjs")

Write-Host "---- Phase 11 unit tests" -ForegroundColor Cyan
Invoke-NativeChecked `
  -FilePath "npm" `
  -WorkingDirectory ".\Frontend\PlantProcess.Web" `
  -Arguments @(
    "run",
    "test",
    "--",
    "src/phase11/phase11UiState.test.ts",
    "src/phase11/phase11StandardControlContract.test.ts",
    "src/phase11/phase11WidgetLayout.test.ts",
    "src/phase11/phase11HeatmapInteractions.test.ts"
  )

Write-Host "---- Frontend build" -ForegroundColor Cyan
Invoke-NativeChecked `
  -FilePath "npm" `
  -WorkingDirectory ".\Frontend\PlantProcess.Web" `
  -Arguments @("run", "build")

if ($RunE2E) {
    Write-Host "---- Phase 11 e2e regression" -ForegroundColor Cyan
    Invoke-NativeChecked `
      -FilePath "npx" `
      -WorkingDirectory ".\Frontend\PlantProcess.Web" `
      -Arguments @(
        "playwright",
        "test",
        "e2e/phase11-ui-interaction-regression.spec.ts",
        "--project=chromium",
        "--workers=1"
      )
}
else {
    Write-Host "Phase 11 e2e spec generated. Run with -RunE2E when frontend/API are available." -ForegroundColor Yellow
}

Write-Host "PPIQ Phase 11 UI hardening rollup GREEN." -ForegroundColor Green
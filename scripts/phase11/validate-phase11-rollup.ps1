param(
    [switch]$RunE2E
)

$ErrorActionPreference = "Stop"

Write-Host "PPIQ Phase 11 UI hardening rollup" -ForegroundColor Cyan

Write-Host "---- Phase 11 file/source validator" -ForegroundColor Cyan
node .\tools\phase11\validate-phase11-ui-hardening.cjs

Write-Host "---- Phase 11 unit tests" -ForegroundColor Cyan
Push-Location .\Frontend\PlantProcess.Web
try {
    npm run test -- `
      src/phase11/phase11UiState.test.ts `
      src/phase11/phase11StandardControlContract.test.ts `
      src/phase11/phase11WidgetLayout.test.ts `
      src/phase11/phase11HeatmapInteractions.test.ts

    Write-Host "---- Frontend build" -ForegroundColor Cyan
    npm run build

    if ($RunE2E) {
        Write-Host "---- Phase 11 e2e regression" -ForegroundColor Cyan
        npx playwright test e2e/phase11-ui-interaction-regression.spec.ts --project=chromium --workers=1
    }
    else {
        Write-Host "Phase 11 e2e spec generated. Run with -RunE2E when frontend/API are available." -ForegroundColor Yellow
    }
}
finally {
    Pop-Location
}

Write-Host "PPIQ Phase 11 UI hardening rollup GREEN." -ForegroundColor Green
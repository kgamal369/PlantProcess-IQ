$ErrorActionPreference = "Stop"

Write-Host "PPIQ Phase 10 license rollup" -ForegroundColor Cyan

Write-Host "---- File validator" -ForegroundColor Cyan
node .\tools\phase10\validate-phase10-license-runtime.cjs

Write-Host "---- Backend build" -ForegroundColor Cyan
dotnet build .\Backend

Write-Host "---- Backend Phase 10 tests" -ForegroundColor Cyan
dotnet test .\Backend\tests\PlantProcess.Application.UnitTests\PlantProcess.Application.UnitTests.csproj `
  --filter "FullyQualifiedName~Phase10LicenseRuntimeTests" `
  --no-build

Write-Host "---- Frontend Phase 10 tests" -ForegroundColor Cyan
Push-Location .\Frontend\PlantProcess.Web
try {
    npm run test -- src/license/phase10License.test.ts
    npm run build
}
finally {
    Pop-Location
}

Write-Host "PPIQ Phase 10 license rollup GREEN." -ForegroundColor Green
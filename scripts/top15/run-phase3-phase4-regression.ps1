$ErrorActionPreference = "Stop"

Write-Host "PPIQ Top15 Phase 3/4 regression gate" -ForegroundColor Cyan

dotnet test .\Backend\tests\PlantProcess.Application.UnitTests\PlantProcess.Application.UnitTests.csproj `
  --filter "FullyQualifiedName~Top15Phase34RuntimeTests" `
  --no-build

if ($LASTEXITCODE -ne 0) {
    throw "PPIQ Top15 Phase 3/4 regression gate FAILED."
}

Write-Host "PPIQ Top15 Phase 3/4 regression gate GREEN." -ForegroundColor Green
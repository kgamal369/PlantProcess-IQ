$ErrorActionPreference = "Stop"

Write-Host "PPIQ Phase 8 Assistant Eval Regression Gate" -ForegroundColor Cyan

dotnet test .\Backend\tests\PlantProcess.Application.UnitTests\PlantProcess.Application.UnitTests.csproj `
  --filter "FullyQualifiedName~Phase8AssistantRegressionEvalGateTests" `
  --no-build

Write-Host "PPIQ Phase 8 Assistant Eval Regression Gate GREEN" -ForegroundColor Green
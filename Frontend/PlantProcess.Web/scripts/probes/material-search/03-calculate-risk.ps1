param(
  [string]$ApiBaseUrl = "http://localhost:5063",
  [string]$Token = $env:PPIQ_TOKEN,
  [Parameter(Mandatory=$true)][string]$MaterialUnitId
)

$headers = @{ "Content-Type" = "application/json" }
if ($Token) { $headers["Authorization"] = "Bearer $Token" }

$body = @{ riskType = "QualityRisk" } | ConvertTo-Json

Invoke-RestMethod -Method POST -Uri "$ApiBaseUrl/risk-scores/materials/$MaterialUnitId/calculate" -Headers $headers -Body $body

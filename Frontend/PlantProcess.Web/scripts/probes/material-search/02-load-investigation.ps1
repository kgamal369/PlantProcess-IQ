param(
  [string]$ApiBaseUrl = "http://localhost:5063",
  [string]$Token = $env:PPIQ_TOKEN,
  [Parameter(Mandatory=$true)][string]$MaterialUnitId
)

$headers = @{}
if ($Token) { $headers["Authorization"] = "Bearer $Token" }

Invoke-RestMethod -Method GET -Uri "$ApiBaseUrl/materials/$MaterialUnitId/investigation-full?maxDepth=5&parameterPage=1&parameterPageSize=100" -Headers $headers

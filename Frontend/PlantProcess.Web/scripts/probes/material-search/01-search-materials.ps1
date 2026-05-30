param(
  [string]$ApiBaseUrl = "http://localhost:5063",
  [string]$Token = $env:PPIQ_TOKEN,
  [string]$Query = ""
)

$headers = @{}
if ($Token) { $headers["Authorization"] = "Bearer $Token" }

Invoke-RestMethod -Method GET -Uri "$ApiBaseUrl/analytics/dashboard/materials?search=$Query&pageSize=10" -Headers $headers

param(
  [string]$ApiBaseUrl = "http://localhost:5063",
  [string]$Token = $env:PPIQ_TOKEN,
  [Parameter(Mandatory=$true)][string]$MaterialUnitId,
  [string]$OutFile = ".\material-investigation-report.pdf"
)

$headers = @{}
if ($Token) { $headers["Authorization"] = "Bearer $Token" }

Invoke-WebRequest -Method GET -Uri "$ApiBaseUrl/reports/materials/$MaterialUnitId/investigation/pdf" -Headers $headers -OutFile $OutFile
Write-Host "Saved $OutFile"

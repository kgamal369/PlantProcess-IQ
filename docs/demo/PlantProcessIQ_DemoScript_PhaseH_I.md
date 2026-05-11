# PlantProcess IQ — Phase H/I Demo Script

## Goal
Demonstrate the path from correlation-ready data to inspectable feature vectors, calculated risk, dashboard summaries, and PDF investigation report.

## Steps

1. Build and migrate:

```powershell
dotnet clean
dotnet restore
dotnet build
dotnet ef migrations add AddModelRegistry --project PlantProcess.Infrastructure --startup-project PlantProcess.Api
dotnet ef database update --project PlantProcess.Infrastructure --startup-project PlantProcess.Api
```

2. Start API:

```powershell
dotnet run --project PlantProcess.Api
```

3. Choose one material:

```http
GET https://localhost:7001/dev/material-sample?take=5
```

4. Inspect feature vector:

```http
GET https://localhost:7001/analytics/features/{materialUnitId}
```

5. Calculate and store risk:

```http
POST https://localhost:7001/risk-scores/materials/{materialUnitId}/calculate
Content-Type: application/json

{
  "riskType": "OverallQualityRisk",
  "modelVersion": "rule-risk-v1.0",
  "storeResult": true
}
```

6. Generate dashboards:

```http
GET https://localhost:7001/analytics/dashboard/overview
GET https://localhost:7001/analytics/dashboard/quality
GET https://localhost:7001/analytics/dashboard/risk
GET https://localhost:7001/analytics/dashboard/data-quality
```

7. Generate investigation report:

```http
GET https://localhost:7001/reports/materials/{materialUnitId}/investigation
GET https://localhost:7001/reports/materials/{materialUnitId}/investigation/pdf
```

## Expected story
PlantProcess IQ can now explain why a material is higher/lower risk using process features, parameter anomalies, downtime exposure, quality signals, data-quality issues, and correlation hints.

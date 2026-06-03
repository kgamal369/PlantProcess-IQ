# PlantProcess IQ — Doctrine v5 P03/P04 Runbook

## P03 — Time-Series Foundation

### What is installed

- TimescaleDB capability probe.
- Hypertable conversion attempt for `parameter_observations`.
- Native partition fallback contract when TimescaleDB is unavailable.
- Hourly and daily telemetry rollup tables.
- Compression/retention policy contract.
- Batched ingestion queue with bounded channel/backpressure and binary COPY attempt.
- Ingestion metrics and checkpoints.

### SQL acceptance

```sql
SELECT * FROM public.ppiq_v5_p03_timeseries_acceptance();
SELECT * FROM public.ppiq_time_series_capabilities;
SELECT * FROM public.ppiq_time_series_policy;
```

## P04 — Assistant Model Gateway & Data Boundary

### What is installed

- Provider config table.
- Redaction policy table.
- Assistant audit log.
- Eval cases/runs.
- Model-version pinning.
- API endpoint `/api/v5/assistant-gateway`.
- Extractive fallback provider.
- OpenAI-compatible provider adapter for self-hosted/private/BYOM endpoints.
- Mandatory grounding guard on model output.

### SQL acceptance

```sql
SELECT * FROM public.ppiq_v5_p04_acceptance();
```

### API smoke

```http
GET /api/v5/assistant-gateway/health

POST /api/v5/assistant-gateway/ask
{
  "question": "What evidence do we have?",
  "evidenceHandles": ["evidence-1"],
  "evidenceChunks": ["Demo evidence chunk [citation: evidence-1]"],
  "providerCode": "extractive-default"
}
```

## Full validation

```powershell
.\tools\v5\validate-p03-p04.ps1
```

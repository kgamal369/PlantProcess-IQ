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
# Mapping and Drift Troubleshooting Runbook

Marker: PPIQ_PACK_A5_T035_MAPPING_DRIFT_DOCS

## Situation 1 — A dashboard widget is empty

Check in this order:

1. Source is registered and enabled
2. Connector job has run successfully
3. Staging or dump-copy rows exist
4. Schema discovery contains expected columns
5. Business-key dictionary is valid
6. Canonical mapping version is valid
7. No active drift blocks the mapping
8. Safe SQL resolver allows the query
9. Widget configuration references mapped fields, not raw source-only columns

## Situation 2 — Mapping validation fails

Common causes:

- Missing source column
- Type mismatch
- Wrong timestamp format
- Business-key missing or not unique
- New source schema not approved
- Dashboard references a retired field

Corrective action:

1. Confirm whether the source changed or the mapping is wrong
2. If source changed, create or approve a new mapping version
3. If mapping is wrong, fix the mapping rule and rerun validation
4. If key quality is poor, block production-grade usage until corrected

## Situation 3 — Schema drift appears after customer changes

Classify the drift:

- Low: additive non-required column
- Medium: nullable or type change on non-critical field
- High: required column change
- Critical: business-key, timestamp, quality-result, material-flow, or process-event change

Response:

1. Keep old mapping version as historical evidence
2. Create a new mapping candidate
3. Run mapping validation
4. Run dashboard/report impact analysis
5. Only promote the mapping after gate success

## Situation 4 — Safe SQL is rejected

Check:

- Query uses approved mapped schema
- Query has row limit
- Query has no destructive command
- Query does not bypass license/feature gates
- Query does not access raw customer tables directly when mapped access is required

## Situation 5 — ML readiness is blocked

ML readiness must be blocked when:

- Mapping is missing
- Drift is active and unresolved
- Business keys are unstable
- Feature vectors cannot be generated consistently
- Quality labels are incomplete or unreliable
- Data coverage is too low for a meaningful model

## Operational rule

Never bypass mapping and drift gates to make a demo look better. Demo honesty is part of the product position.

# PlantProcess IQ Mapping and Drift Developer Guide

Marker: PPIQ_PACK_A5_T035_MAPPING_DRIFT_DOCS

## Purpose

This guide explains how PlantProcess IQ moves data from configured sources into staging, maps it into canonical manufacturing concepts, validates business-key rules, detects schema drift, and protects analysis/dashboard/ML readiness from unsafe or ambiguous data structures.

PlantProcess IQ must remain generic. The mapping layer must not assume steel-only names, one fixed MES schema, one historian vendor, or one plant topology. Every source must pass through a configurable mapping and validation lifecycle before it is treated as reportable or ML-ready.

## Core lifecycle

The expected lifecycle is:

1. Source registration
2. Connector configuration
3. Staging or dump-copy ingestion
4. Schema discovery
5. Business-key dictionary configuration
6. Canonical mapping draft
7. Mapping validation
8. Drift detection
9. Safe SQL resolution
10. Dashboard/report/ML-readiness usage

## Source registration

A source is not automatically trusted. A source only becomes usable when its provider, connection profile, refresh policy, license gate, and schema exposure are known. Source configuration must be treated as customer-specific and must not be hardcoded into dashboards, widgets, jobs, or ML features.

Expected source metadata:

- Source id and display name
- Provider type, such as SQL, CSV, historian, or future connector type
- Connection profile
- Refresh mode and refresh interval
- License tier eligibility
- Schema discovery status
- Mapping status
- Drift status

## Staging and dump-copy layer

The staging layer is the safety boundary between customer data and PlantProcess IQ canonical logic. Data should first land in staging or a dump-copy table/area before it is mapped into normalized concepts. This allows auditability, rollback, re-mapping, and drift inspection without corrupting configured dashboards.

The staging layer should answer:

- What source produced this row?
- When was it ingested?
- What schema version was used?
- What mapping version was applied?
- Was this data accepted, rejected, quarantined, or partially mapped?

## Schema discovery

Schema discovery creates an inventory of tables, columns, data types, nullable flags, candidate keys, sample values, and source-level metadata. Discovery must not mean approval. It only creates the raw knowledge required for mapping and drift decisions.

Discovery output should be stable enough to compare versions. Any later difference between the stored schema fingerprint and a newly discovered schema should be treated as schema drift.

## Business-key dictionary

The business-key dictionary defines how rows are linked across datasets. Examples include material id, batch id, heat id, coil id, order id, production unit id, timestamp bucket, inspection id, or any customer-specific operational key.

Rules:

- Business keys must be explicit, not guessed silently.
- Composite keys must be supported.
- Key quality must be validated before downstream analytics.
- Missing or unstable business keys should block production-grade mapping.
- Business-key configuration must remain generic across manufacturing industries.

## Canonical mapping

Canonical mapping translates customer-specific schema into PlantProcess IQ concepts. The canonical model is not one fixed database clone. It is a normalized layer that allows dashboards, widgets, reports, and future ML features to understand equivalent concepts from different factories.

Typical canonical concept groups:

- Material or unit flow
- Process events
- Measurements and signals
- Quality results
- Defects and inspections
- Downtime and process interruptions
- Jobs, batches, orders, or production campaigns
- Equipment, line, unit, area, and route metadata

Mapping validation should confirm:

- Required source columns exist
- Required data types are compatible
- Business keys are present and stable
- Time fields are parseable and timezone-aware where needed
- Numeric fields are usable for aggregation and model-readiness
- Null rates and duplicate rates are within acceptable bounds
- No dashboard or report depends on an unmapped column

## Safe SQL resolver

Safe SQL exists to protect dynamic, configurable analysis from arbitrary unsafe execution. Developer-created SQL must be parameterized, bounded, explainable, and restricted to approved source/mapping contexts.

Safe SQL rules:

- No unrestricted customer SQL execution
- No destructive commands
- Enforce row limits
- Enforce timeout limits
- Validate allowed schemas/tables
- Prefer typed resolver outputs
- Log query intent and source context

## Drift detection

Drift detection compares current discovered schema and observed data behavior against the approved mapping baseline. Drift is not only a column disappearing. It can also be a type change, semantic change, key-quality change, null-rate spike, duplicate-rate spike, or value distribution shift.

Drift categories:

- Schema drift: table, column, type, nullable, or key change
- Mapping drift: existing mapping no longer resolves
- Business-key drift: key uniqueness or completeness changes
- Data-quality drift: null, duplicate, or range behavior changes
- Semantic drift: field still exists but meaning or unit changes

Expected drift response:

1. Detect difference
2. Classify severity
3. Mark impacted mappings/widgets/jobs/reports
4. Block or warn depending on severity
5. Require developer/admin review for critical changes
6. Create a new mapping version when accepted

## Validation gates

Mapping and drift work should be protected by automated gates. These gates are not decoration; they are part of product safety.

Developer gate examples:

- Business-key dictionary validator
- Canonical mapping validator
- Safe SQL typed resolver validator
- Mapping lifecycle proof
- Drift detection validator
- Pack-level task closure validator
- CI certification gate

## Troubleshooting principles

When mapping fails, do not fix the dashboard first. Fix the lifecycle root cause. Check source configuration, staging data, schema discovery, business keys, mapping version, drift classification, safe SQL limits, then dashboard/report usage.

## Developer ownership

A developer changing schema mapping, drift logic, or safe SQL behavior must update the relevant validator and this documentation. Any new feature that bypasses the mapping lifecycle is considered a product-genericity regression.

# PlantProcess IQ — Database Architecture, Naming and Dictionary Standard

**Version 1.1 — 4 September 2026**  
**Role:** generated engineering reference subordinate to Master Design v4.10.2. It does not replace Chapter 3 DDL authority.  
**Evidence baseline:** 04-Sep-2026 Ultimate Audit, canonical migration order, terminal storage-topology convergence, EF model, and the current `ppiq_app` counts supplied from `pg_tables`.

## 1. The four physical schema roles

| Schema | Role | Fresh-install state | What belongs here |
|---|---|---|---|
| `ppiq_staging` | Transit / source-shaped landing | Empty | import batches, raw/staging rows, watermarks, connector transit, projection quarantine |
| `ppiq_meta` | Product control plane | Generic prefill only | definitions, UI/BI authoring, Canvas, jobs, source/mapping configuration, users/security, licence, Assistant/ML governance, logs/audit, retention/routing |
| `ppiq_plant` | Customer plant truth + generated intelligence | Empty | plant structure, canonical process/quality facts, analysis subjects, statistics/ML results, predictions/practices, suggestions/value evidence |
| `public` | Platform infrastructure only | platform-owned | migration history and extension/platform infrastructure; no product/business table authority |

A row is placed by one question: **whose knowledge is this?** Raw source copy → staging. Customer plant reality/result → plant. Product configuration/governance → meta.

## 2. Database instance roles

- `postgres`: PostgreSQL maintenance database; keep.
- `ppiq_app`: long-lived daily development database. It may be populated and may contain upgrade history. It is not the clean-install oracle.
- `ppiq_acceptance_empty`: fresh-install / Rule-2 certification authority. Recreate or refresh it from the canonical path.
- `ppiq_presentation`: frozen historical populated presentation/regression baseline; never closes M2 generic-product acceptance.
- probe/scratch/backup databases: disposable; the creating pack owns cleanup in `finally`, on success and failure.
- `plantprocessiq`: historical legacy candidate; archive and retire only after zero runtime/profile dependency is proven.

## 3. Naming standard for new database objects

1. lower-case `snake_case` only.
2. No customer, plant or industry vocabulary in product physical names.
3. New objects inside `ppiq_meta` / `ppiq_plant` do not add a redundant `ppiq_` prefix unless an external interoperability contract requires it.
4. Stable machine identities use `*_code`; human labels use `*_name` or `display_name`.
5. Tenant-owned tables use `tenant_id` and tenant-aware unique keys.
6. UTC authority uses `*_at_utc`, `*_from_utc`, `*_to_utc`; source/local timestamps are separate when required.
7. Foreign-key columns use `<subject>_id` unless a specifically governed semantic key is required.
8. JSON payloads use a semantic noun or `*_json`, never an unexplained `data` blob.
9. New physical table/view names do **not** end in schema-generation suffixes such as `_v1`, `_v2`, `_v3`; relation versioning belongs in migration history and governed row/version columns. Existing version-suffixed names are grandfathered and explicitly flagged in the catalogue.
10. Existing names are grandfathered. This standard does **not** authorise a mass rename. A rename requires a separate compatibility-safe owner and proof.

## 4. Logical subsystem families

### `ppiq_meta`

- `AUTHORING_DEFINITION`: definition store, versions, typed details, registry.
- `BI_PRESENTATION`: dashboards, pages, widgets, KPI/filter presentation metadata.
- `CANVAS`: Canvas/wiring/SQL authoring, versions, sessions and dry-runs.
- `JOB_RUNTIME`: job definitions, schedules, runs, progress and execution evidence.
- `SOURCE_INTEGRATION`: connection, source, dataset, mapping, connector, historian and edge configuration.
- `MAPPING_RELATIONSHIP`: business keys, joins, relationship members/paths and grain authority.
- `IDENTITY_SECURITY`: users, tenant, role, session, MFA, SSO, SCIM, OIDC, secrets and isolation.
- `LICENCE_ENTITLEMENT`: signed licence, feature/entitlement and activation authority.
- `ASSISTANT`: Assistant retrieval, provider configuration, evaluation, redaction and gateway policy.
- `ML_GOVERNANCE`: model/feature/outcome/job governance and learning runtime metadata.
- `OBSERVABILITY_AUDIT`: audit/log/event/run/telemetry evidence.
- `RETENTION_ROUTING`: retention, alert, notification, delivery and escalation policy.
- `REPORTING_I18N`: reports, export and localisation metadata.
- `VALUE_GOVERNANCE`: cost assumptions and decision/value governance metadata.

### `ppiq_plant`

- `CANONICAL_STRUCTURE`: site/area/equipment, material identities, aliases and genealogy.
- `CANONICAL_PROCESS`: process execution, process events, parameters and telemetry.
- `QUALITY_DOWNTIME`: quality events, downtime, data-quality issues.
- `INTELLIGENCE_RESULTS`: Analysis Subjects, correlation/statistical results, risk and ML outputs.
- `PREDICTION_PRACTICE`: predictions, practices, remediation, suggestions and realised value.

### `ppiq_staging`

- `STAGING_TRANSIT`: import/staging rows, cursors/watermarks, connector buffers/readings, schema drift and quarantine.

## 5. Lifecycle classes — every table gets exactly one

- `PERMANENT_AUTHORITY`
- `COMPATIBILITY_PROJECTION`
- `OPERATIONAL_EVIDENCE_LOG`
- `INSTALLATION_GRAMMAR`
- `HISTORICAL_RETIRED`
- `FIXTURE_ONLY`
- `DISPOSABLE`

A table with no family, lifecycle, owner and design clause is a governance defect. It is **not** automatically deleted. `GENERATED_INFERENCE` is draft scaffolding, not certification-grade evidence for those authority fields; fresh-release certification requires explicit design/source support or a reviewed adjudication recorded as such.

## 6. Current baseline and why the counts do not yet reconcile one-to-one

The current long-lived `ppiq_app` reports:

- `ppiq_meta`: **190** tables
- `ppiq_plant`: **34** tables
- `ppiq_staging`: **22** tables
- `public`: **5** tables

The attached generated source catalogue currently resolves **214 known source/EF-defined tables**: 171 meta, 27 plant, 10 staging and 6 public/infrastructure-or-review rows. This mismatch is intentional evidence that **source catalogue and long-lived live database have not yet been mechanically reconciled**. It is not permission to delete the delta.

The detailed workbook `PPIQ_Database_Architecture_and_Data_Dictionary_v1_0.xlsx` contains:

1. schema overview;
2. naming standard;
3. logical-family legend;
4. table catalogue with creator/reference evidence;
5. 2,626 discovered column rows;
6. 101 physical/EF relation rows;
7. live-reconciliation queries.

## 7. Relationship rule

Two graphs are deliberately separate:

- **Physical referential graph:** PostgreSQL PK/FK/UNIQUE/CHECK constraints. This is generated into the dictionary.
- **Semantic plant relationship graph:** customer-authored relationships, keys, cardinality, grain, attribution and preferred paths in `ppiq_meta`. It is not replaced by physical FKs.

A BI/analytics dimension resolves through the published semantic relationship model to an Analysis Subject. It never infers meaning from a table name or a convenient FK alone.

## 8. Zero-unknown target

A fresh certified database must reach:

- 100% base tables/views catalogued;
- 100% columns catalogued;
- 100% physical FKs mapped;
- every table assigned source/origin schema, governed target schema, live-observed schema when available, logical family, lifecycle, owner and design clause;
- zero product/business tables in `public`;
- zero unowned source/database objects;
- zero unresolved `GENERATED_INFERENCE` / `REVIEW_REQUIRED` on lifecycle, family, owner or design clause;
- canonical plant fact families classified as permanent authority unless explicitly governed otherwise;
- no purpose cell consisting only of banner/comment metadata;
- catalogue regenerated in the same change as every DDL change.

The detailed catalogue is generated evidence, not a second DDL source.

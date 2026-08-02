# PlantProcess IQ - Master Design Document

**Version 4.0 | Author: Karim, SOU Industrial Software, Dusseldorf**

---

# CHAPTER 3 - GENERAL TECHNICAL FUNCTION DESCRIPTION

> **Audience:** the customer's advanced IT, database and software staff, and our own developers taking hand-over for further development phases.
>
> **Voice:** senior software engineer and technical lead.
>
> **Contract of this chapter:** every artifact named here is a real artifact. Routes, handler names, client methods, hooks, components, table names, column names, indexes, constraints, labels, placeholders and validation messages are the ones that exist or are specified to exist. Where a name is provisional it is marked `[to fix]`. Where behaviour is target rather than present, the Implementation Status Register carries the distance; this chapter does not report build state.

---

## Contents

| # | Section |
|---|---|
| 3.1 | The data flow, step by step, to endpoint level |
| 3.2 | The UI page inventory |
| 3.3 | Per-page technical specification |
| 3.4 | Database schemas, tables, keys and joins |
| 3.5 | Credentials, identities and topology |

**Provenance:** roughly 35 percent carried, 30 percent enhanced, 35 percent new. The step-level endpoint contracts, the per-page component and hook specifications, the join graph and the index rationale did not exist in any draft.

---

## 3.1 The data flow, step by step, to endpoint level

Each step is specified with the same eleven fields. Read the fields, not the prose.

```
CONCEPT      what this step exists to achieve
ACTOR        which role performs it
PRECONDITION what must already be true
SURFACE      the page and route
SEQUENCE     the ordered interaction, control by control
CALLS        client method -> HTTP verb + route -> handler
PAYLOAD      the essential request and response content
PERSISTS     what rows are written, in which schema
VALIDATION   client-side and server-side, with the exact refusal
FAILURE      what the user sees when each layer fails
ACCEPTANCE   the observable condition that proves the step
```

---

### Step 1 - Create and configure the source connection

**CONCEPT.** Establish a read-only path to one customer database or file share, prove it works, and record how it may be used (schedule window, load budget). This is the only door for plant data (Chapter 1, Rule 2).

**ACTOR.** Administrator or Data Engineer.

**PRECONDITION.** Licence activated; the customer's DBA has created a read-only account and opened the port to the collector.

**SURFACE.** Connections, route `/data-integration/connections`, rendered inside `DataIntegrationLayout` (shared parent for every acquisition page). Layout header: title "Data Integration", subtitle "Connect plant sources, map them to the canonical model, run imports and watch every job.", a secondary **Refresh** button (`RefreshCw` icon), and the standing read-only promise line: *"Connections are read-only toward your source systems at all times."*

**SEQUENCE.**

1. The page mounts and fires two calls in parallel (below). Until both settle, the two panels render skeletons, never a bare spinner.
2. Panel 1, **"DB Link Configuration"**, subtitle "Connection profiles to customer source databases and files", renders in LIST mode: a `StandardDataTable` of existing profiles with row actions Edit, Test, Activate/Deactivate.
3. Panel 2, **"Supported Connectors"**, subtitle "Available and planned data source provider types", renders the provider-type grid. **Every card is backend-driven**; a provider the build does not implement renders dimmed, unselectable, badged *Planned*. The frontend cannot invent a connector (Chapter 1.5.9).
4. **New Connection Profile** (primary, `Plus` icon) switches the same panel to FORM mode. A secondary **Back** returns to LIST without a route change.
5. Form fields, in order, with placeholders: Name ("e.g. Production MES Database"), Code ("Auto-generated if empty"), Provider type (select, options from call 2), Host ("e.g. 192.168.1.100 or db.plant.local"), Port, Database ("e.g. mes_production"), Schema ("e.g. public / dbo"), Username, Password (masked input), and for file providers File path ("e.g. /data/imports or C:\Imports"). Provider-dependent fields show and hide on provider change: **Oracle and MySQL do not ask identical questions.**
6. Below the credentials: **Source system tag** (select: MES, Level 2, Historian, LIMS, ERP, Inspection) - lineage only, no behaviour branch.
7. Below that, the **load budget** group: Max rows per read, Statement timeout (s), Requests per minute, Approved window (from/to, days-of-week).
8. **Save** (primary). **Test connection** (secondary) is enabled once host, database and credentials are non-empty.

**CALLS.**

| Client method | HTTP | Route | Handler |
|---|---|---|---|
| `productApi.getConnectionProfiles(includeSecrets)` | GET | `/api/connections` | `GetConnectionProfilesAsync` |
| `productApi.getProviderTypes()` | GET | `/api/connections/catalog` | `GetProviderTypes` |
| `productApi.createConnectionProfile(req)` | POST | `/api/connections` | `CreateConnectionProfileAsync` |
| `productApi.updateConnectionProfile(id, req)` | PUT | `/api/connections/{id}` | `UpdateConnectionProfileAsync` |
| `productApi.testConnectionProfile(id)` | POST | `/api/connections/{id}/test` | `TestConnectionProfileAsync` |
| `productApi.activateConnectionProfile(id)` / `deactivate...` | POST | `/api/connections/{id}/activate` / `/deactivate` | `ActivateConnectionProfileAsync` / `Deactivate...` |
| `productApi.updateConnectionImportSchedule(id, req)` | PUT | `/api/connections/{id}/schedule` | `UpdateConnectionImportScheduleAsync` |

*Present implementation serves these under `/admin/connectors/connection-profiles*`; the namespace migration is a Status Register row.*

**PAYLOAD.** Request: `{ name, code?, providerType, host, port, database, schema, username, password, filePath?, sourceSystemTag, loadBudget { maxRowsPerRead, statementTimeoutSeconds, requestsPerMinute, approvedWindow } }`. Response on create: `{ id, code, providerType, isActive }`. Test response: `ConnectionTestResult { success, message, latencyMs, serverVersion?, readOnlyVerified }`.

**PERSISTS.** `ppiq_meta.connection_profiles` (one row). Credentials are written to the vault; the row stores a vault reference, never the secret. `ppiq_meta.audit_log_entries` gains a create entry.

**VALIDATION.**
- Client: Name empty -> inline error under the field, **no network call**. Port non-numeric -> inline. Provider-specific required field empty -> inline. Implemented through `useInlineFormValidation`.
- Server: unknown `providerType` -> 400 naming the accepted set. Write-capable credential detected during test -> **the test fails with the read-only verification named**, because a read-write account violates Chapter 1.7.

**FAILURE.**

| Layer | User sees |
|---|---|
| Network unreachable | Red result pill in-page: "Host unreachable" plus the host and port tried |
| Authentication | "Authentication failed for user `<name>`" - no stack trace, problem body `{title, errorCode, traceId}` |
| Permission | "Connected, but the account cannot read `<schema>`" |
| API down | Contained error card in the panel; the other panel keeps working (widget-level isolation) |

**ACCEPTANCE.** Two profiles of different provider types created through the interface, both Test green, password masked on re-open, a stopped source producing a clean in-page failure rather than an exception page, and one row per profile in `connection_profiles`.

---

### Step 2 - Register datasets and configure the watermark

**CONCEPT.** Choose which of the source's tables, views and files enter the product, and declare for each the incremental cursor. **Registration is what makes a dataset due for import.**

**ACTOR.** Data Engineer.

**PRECONDITION.** Step 1 profile active and tested.

**SURFACE.** Two pages. **Table Registry**, `/data-integration/registry`, nav description "Map source tables to the canonical model", hosting the schema-configuration surface. Then **Prepare Import**, `/data-integration/prepare`, nav description "Pick columns, keys and watermark".

**SEQUENCE.**

1. On Table Registry: select the connection profile.
2. A live source browse loads: schema tree, then tables, then columns with observed types. **Seeing the customer's own table names on screen is the demonstration beat of this step.**
3. Select a table. Columns load with inferred roles: the discovery service marks likely keys and likely timestamps (`LooksLikeKey`, `LooksLikeTimestamp`, `InferDataType`).
4. **Register** creates the dataset. A registered dataset appears in the registry list with its source binding.
5. On Prepare Import, for that dataset: choose imported columns (multi-select, default all), the business key column or columns, and the **watermark column**. Save.
6. Register a second dataset for the taxonomy source (defect definitions, parameter definitions) - taxonomy first, per Rule 2.

**CALLS.**

| Client method | HTTP | Route |
|---|---|---|
| `productApi.listSourceTables(profileId)` | GET | `/api/connections/{id}/discover` |
| `productApi.listSourceColumns(profileId, schema, table)` | GET | `/api/connections/{id}/discover/{schema}/{table}/columns` |
| `productApi.registerSourceTable(profileId, req)` | POST | `/api/datasets` |
| `productApi.getSourceDatasets()` | GET | `/api/datasets` |
| `workflowFoundation.updateDatasetCursor(id, req)` | PUT | `/api/datasets/{id}/cursor` |
| `productApi.previewCsv` / `discoverCsvSchema` / `importCsvSnapshot` | POST | `/api/datasets/file/*` |

**PAYLOAD.** Register request: `{ connectionProfileId, sourceSchema, sourceTable, includedColumns[], businessKeyColumns[], watermarkColumn, isTaxonomy }`. Response `{ id, stagingTableName, isDue }`.

**PERSISTS.** `ppiq_meta.source_dataset_definitions` plus one `source_field_definitions` row per included column. The staging table for the dataset is created in `ppiq_staging` on first import, not at registration.

**VALIDATION.** Client: no columns selected -> inline. No watermark chosen -> **warning, not error**, with the sentence "Without a watermark every run re-reads the whole table." Server: watermark column absent from the source -> 400 naming the column; a non-orderable watermark type -> 400 naming the type.

**FAILURE.** Discovery timeout inside the load budget -> "The source did not answer within `<n>`s under the configured budget", with a link to raise the timeout. A 500 that nevertheless persisted the dataset is an interface-only defect and the list shows the row after Refresh.

**ACCEPTANCE.** Two datasets registered from a live browse, one of them taxonomy, watermark configured and persisted across a reload, and an import job row visible for each.

---

### Step 3 - Incremental import into staging

**CONCEPT.** Move only the delta into staging, exactly as it arrived, with the batch as the unit of lineage and retry.

**ACTOR.** Data Engineer, or the scheduler.

**SURFACE.** **Importing**, `/data-integration/importing`, nav description "Run import jobs and watch batches". **Jobs Monitor**, `/data-integration/jobs`.

**SEQUENCE.**

1. In Jobs Monitor, run the taxonomy dataset job first, then the readings job. Row actions are **Run now**, **Pause**, **Resume**.
2. The row's Status, Last Run and Duration update. `StatusPill` renders Paused, Running, Completed, Failed.
3. The batch appears in the Importing page's batch list with source object, status, started timestamp, row count.
4. A second run with no new source rows completes fast with a zero or small delta: the cursor was honoured.

**CALLS.**

| Client method | HTTP | Route |
|---|---|---|
| `productApi.runJobNow(jobId, "Admin UI")` | POST | `/api/jobs/{id}/run` |
| `productApi.pauseJob(jobId)` / `resumeJob(jobId)` | POST | `/api/jobs/{id}/pause` / `/resume` |
| `integration.getImportBatches()` | GET | `/api/imports/batches` |
| `workflowFoundation.runDueSourceImports()` | POST | `/api/imports/run-due` |
| `workflowFoundation.getStagingRecords(batchId)` / `getStagingSummary()` | GET | `/api/imports/batches/{id}/records`, `/api/imports/staging/summary` |
| `productApi.getJobHistory(jobId)` | GET | `/api/jobs/{id}/runs` |

**PAYLOAD.** Batch: `{ id, sourceObjectName, sourceSystem, status, startedAtUtc, finishedAtUtc, rowCount, watermarkFrom, watermarkTo, checksum }`.

**PERSISTS.** `ppiq_staging.import_batches` (one row per run) and `ppiq_staging.staging_records` (one row per source row) with `raw_json jsonb` mirroring the source row verbatim, `row_number`, `processing_status` default `'Pending'`, and `import_batch_id` referencing the batch with `ON DELETE RESTRICT`.

**VALIDATION.** Server: read attempted outside the approved window -> refused before touching the source, naming the window. Read exceeding the row cap -> refused with the cap named. Both enforced by `ThrottlingDataSourceReader`, which wraps the real reader and evaluates each one-shot read against the budget **before it reaches the source**; incremental backfill passes through and is governed by the backfill worker's own cumulative rate throttle.

**FAILURE.** Source down -> the job reaches a terminal Failed state with a clean message in the monitor, never an unhandled exception. Cursor type mismatch, null cursor and locale-dependent date parsing are the three historical failure classes and each must produce a named error rather than a database-level fault.

**ACCEPTANCE.** Batches and staging rows grow; the second run is delta-only; one row inserted into the emulated source propagates as exactly one staging row; a stopped source fails cleanly.

---

### Step 4 - Author the Transformation Definition (S1)

**CONCEPT.** The customer's own engineer declares how staged data becomes the canonical model: which source column feeds which canonical field, which identifiers join across sources, which literals apply. **This is the permanent model of the plant** (Chapter 1.7).

**ACTOR.** Data Engineer.

**SURFACE.** **Transformation Studio**, `/prep/canvas` for the node canvas and `/data-integration/author-mapping` for the field-map form. Nav description "Author a mapping and project staged rows", icon `Network`.

**SEQUENCE (field-map path).**

1. Mount loads import batches. Pending text: "Loading import batches...". Empty state, verbatim: *"No import batches yet. Connect a source and import data first (steps 1-3), then return here."*
2. **Import batch** select, options formatted `<sourceObjectName> - <status> - <startedAtUtc>`, first batch selected by default.
3. **Target entity** select with exactly eight options: `DefectCatalog`, `ParameterDefinition`, `MaterialUnit`, `MaterialAlias`, `ProcessStepExecution`, `ParameterObservation`, `QualityEvent`, `GenealogyEdge`.
4. Hint line renders: *"Source object `<name>` from system `<system>`. Source can be a column name or `const:VALUE` for a literal."*
5. Choosing a target entity seeds the suggested target fields. `DefectCatalog` seeds DefectCode, DefectName, DefectCategory. `ParameterObservation` seeds MaterialCode, ParameterCode, ObservedAtUtc, NumericValue. `QualityEvent` seeds MaterialCode, DefectCode, EventType, EventAtUtc. `GenealogyEdge` seeds ParentMaterialCode, ChildMaterialCode, RelationshipType.
6. The grid: **Add field** appends an empty row; the `x` button removes a row.
7. **Save mapping**, then **Execute (project)**.
8. Result panel renders "Projection result" with Mapped, Failed and Total when present, and always the raw response as a defensive display.

**SEQUENCE (canvas path).** Staged tables drag from the inline-start schema tree onto the board as nodes whose columns are typed ports. A key column wired to a key column produces an edge labelled with the equality, for example `piece_id = material_id`. **Preview (dry-run)** renders sample rows per node. A third table with no join to the graph produces the named refusal *"table X has no join to the graph"* rather than a crash. **Publish version** returns a version number. Full canvas design, port typing and refusal taxonomy: Chapter 4.

**CALLS.**

| Client method | HTTP | Route |
|---|---|---|
| `mappingAuthor.listImportBatches()` | GET | `/api/imports/batches` |
| `mappingAuthor.createMappingDefinition(body)` | POST | `/api/transformations` |
| `mappingAuthor.executeMapping(id, batchId, stopOnFirstError)` | POST | `/api/transformations/{id}/execute?importBatchId=&stopOnFirstError=` |
| `canvasApi.listStagedDatasets()` | GET | `/api/transformations/staged-datasets` |
| `canvasApi.createSession()` / `saveGraph()` | POST | `/api/transformations/sessions`, `/api/transformations/{id}/graph` |
| `canvasApi.runDryRun()` | POST | `/api/transformations/{id}/preview` |
| `canvasApi.runAuthoredSql()` / `saveSqlVersion()` | POST | `/api/transformations/{id}/sql/run`, `/sql/version` |
| `canvasApi.publishVersion()` | POST | `/api/transformations/{id}/publish` |
| `schemaMapping.previewJoin()` / `materializeJoin()` | POST | `/api/transformations/joins/preview`, `/joins/materialize` |

**PAYLOAD.** Create body: `{ sourceSystemDefinitionId, mappingCode, mappingName, sourceObjectName, targetEntityName, mappingJson, mappingVersion, description, isSynthetic: false, sourceSystem, sourceRecordId: null }`. Response `{ id }`. Success notice: *"Mapping saved (id ...). Now Execute to project this batch."*

**PERSISTS.** `ppiq_meta.mapping_definitions` with lifecycle `draft | validated | published | paused_by_drift | rolled_back`, immutable published versions and a rollback pointer. Canvas versions persist to the visual-mapper version table with `version_number`, `version_status`, `published_by`.

**VALIDATION.** Client: zero complete rows -> *"Add at least one field map (target field + source column or const:VALUE)."* Server: `SafeSqlValidator` on any authored SQL - `SELECT` and `WITH` only, a forbidden-token list covering DDL, DML, `COPY`, large-object functions, `dblink*`, `pg_sleep*`, catalog and `information_schema` access, and `xp_*`; identifiers validated against the allowlist provider and values always parameter-bound. Refusal is a first-class dry-run status `rejected_by_safe_sql`, not an exception.

**FAILURE.** A target mapped to a nonsense column produces `Failed > 0` with typed field errors in the job log, classified by `MappingFaultClassifier`, and never a crash. Re-executing the same mapping and batch produces no duplicate canonical rows: the projector is idempotent per batch.

**ACCEPTANCE.** Taxonomy imports produce real catalogues whose `source_system` is the connector's system and never a configuration literal; observations, quality events and genealogy edges project; the genealogy weight constraint does not reject; re-execute leaves counts unchanged.

---

### Step 5 - Schedule the projection

**CONCEPT.** Bind each Transformation Definition to a scheduled job so canonical data refreshes without a human.

**SURFACE.** Importing page hosts the mapping refresh control: mapping selector, interval in minutes (default 15), Save.

**CALLS.** `productApi.updateMappingRefreshSchedule(mappingId, { scheduleExpression: "Every 15 minutes", refreshIntervalMinutes: 15 })` -> PUT `/api/transformations/{id}/schedule`. Toast, verbatim: *"Canonical refresh schedule saved and JobDefinition updated"*.

**PERSISTS.** `ppiq_meta.job_definitions` gains a row of class `CanonicalRefresh` targeting the mapping.

**ACCEPTANCE.** A `CanonicalRefresh` job row exists, runs, pauses and resumes; on import completion the projector runs for the batch's active mapping without a manual Execute.

---

### Step 6 - Canonical verification

**CONCEPT.** Prove through the interface, not the database, that imported data became a navigable plant model.

**SURFACE.** Material Investigation, `/materials`.

**SEQUENCE.** Search an imported material code; the unit resolves. Its observations list shows imported readings with source timestamps. Its quality events show **defect names, not only codes**, which proves the catalogue joined. Its genealogy renders parent and child edges. Provenance is visible on the unit detail: source system, source record, batch.

**CALLS.** `productApi.getMaterialInvestigation(id)` -> GET `/api/plant/materials/{id}`; `getMaterialFeatures`, `getMaterialSample`, `searchDashboardMaterials` -> GET `/api/plant/materials?search=`.

**ACCEPTANCE.** One imported unit fully navigable with observations, events, genealogy and provenance; a nonexistent code produces a designed not-found state, not a crash; `is_synthetic` false on imported rows.

---

### Step 7 - Author pages and widgets (S2)

**CONCEPT.** Compose analysis surfaces from data, not code. A widget's source is either a catalogue selection or an authored query.

**SURFACE.** Page Builder and Interactive Workspace.

**SEQUENCE.** Add widget -> choose kind (chart, table, KPI, calculated label, calendar filter, filter) -> name -> the shared shell opens in S2 mode with the current definition loaded -> catalogue binding or query binding -> **Run test** -> inspect returned columns -> map columns to axes -> preview -> save. Filters are authored widgets, not fixed furniture.

**CALLS.**

| Client method | HTTP | Route | Handler |
|---|---|---|---|
| `getDashboardMetadata()` | GET | `/analytics/dashboard/metadata` | `GetMetadataAsync` |
| `getDashboardReferenceData()` | GET | `/analytics/dashboard/reference-data` | `GetReferenceDataAsync` |
| `getDashboardWorkspace(req)` | POST | `/analytics/dashboard/workspace` | `GetWorkspaceAsync` |
| `queryDashboardWidget(req)` | POST | `/analytics/dashboard/widgets/query` | `QueryWidgetAsync` |
| `executeWidgetQueryExpression(req)` | POST | `/analytics/dashboard/widgets/execute` | `ExecuteWidgetExpressionAsync` |
| `createDashboardDefinition` / `update` / `delete` | POST/PUT/DELETE | `/analytics/dashboard/definitions[/{id}]` | `CreateDashboardDefinitionAsync` ... |
| `updateDashboardLayout(id, layout)` | PATCH | `/analytics/dashboard/definitions/{id}/layout` | `UpdateDashboardLayoutAsync` |
| `createDashboardWidgetDefinition` | POST | `/analytics/dashboard/definitions/{id}/widgets` | `CreateDashboardWidgetDefinitionAsync` |
| `updateDashboardWidgetLayout` | PATCH | `.../widgets/{wid}/layout` | `UpdateDashboardWidgetLayoutAsync` |
| `cloneDashboardWidgetDefinition` | POST | `.../widgets/{wid}/clone` | `CloneDashboardWidgetDefinitionAsync` |
| `deactivateDashboardWidgetDefinition` | DELETE | `.../widgets/{wid}` | `DeactivateDashboardWidgetDefinitionAsync` |

**The registry is the Rule 1 mechanism.** `GetMetadataAsync` returns `{ dimensions, measures, chartTypes }` and every palette reads it. The server-side `DashboardWidgetQuerySafetyRegistry` fixes the closed sets and the hard limits:

- Widget types: `Kpi`, `Chart`, `Table`.
- Chart types: `Kpi`, `Bar`, `Line`, `Area`, `Pie`, `Donut`, `Scatter`, `Heatmap`, `Pareto`, `Table`.
- Dimensions: Site, Area, Equipment, SourceSystem, MaterialUnitType, ProductFamily, GradeOrRecipe, ShiftCode, DefectType, ParameterCode, Day, Week, Month, RiskClass.
- Measures: MaterialCount, DefectCount, ObservationCount, DefectRate, AvgParameterValue, MaxParameterValue, MinParameterValue, DowntimeMinutes, RiskScore, ProcessStepDuration, DataQualityIssueCount.
- Limits: `DefaultMaxRows` 100, `AbsoluteMaxRows` 500; `DefaultRawRowLimit` 50,000, `AbsoluteRawRowLimit` 250,000; `DefaultLookbackDays` 90, `AbsoluteLookbackDays` 730.

**A design obligation follows, and it is the most important Rule 1 item in this chapter.** Several of those dimension codes are plant vocabulary: `ShiftCode`, `DefectType`, `RiskClass`, `GradeOrRecipe`. A closed set containing them is a Rule 1 violation even though every *value* is dynamic, because a plant that filters by batch, recipe, tool number or ambient humidity cannot add a dimension. **Target design: the dimension and measure sets are registry rows derived from the canonical model and the customer's own mapping, not a compiled `HashSet`.** The chart-type set, the widget-type set and the numeric limits stay closed, because those are the product's own grammar and its safety envelope (Chapter 1, Rule 1, the four kinds of fixed value).

**PERSISTS.** `ppiq_meta.dashboard_definitions` and `dashboard_widget_definitions`, with the authored expression stored on the widget, the saved filter as its permanent scope, and `widget_expression_status` recording declared-versus-served so a declared capability cannot silently answer empty.

**VALIDATION.** `DashboardWidgetValidationService` refuses a widget whose chart type does not support the chosen dimension or measure, using each chart type's `supportsDimension` and `supportsMeasure` flags; a widget with neither a dimension nor a measure is refused; a measure requiring a dimension without one is refused, each with the rule named.

**ACCEPTANCE.** A widget authored against real canonical data, previewed before commit, committed, surviving reload, moving when new data lands, and showing the sample-data badge only when the data is emulated.

---

### Step 8 - Author the analysis (S3)

**CONCEPT.** Declare what to relate to what, at which grain, over which window, by which method.

**SURFACE.** Analysis Toolbox, `/analysis/toolbox`, and the definition list.

**CALLS.**

| Client method | HTTP | Route | Handler |
|---|---|---|---|
| `getAnalysisJobDefinitionOptions()` | GET | `/api/analysis-jobs/definition-options` | `GetDefinitionOptionsAsync` |
| `listAnalysisJobDefinitions()` | GET | `/api/analysis-jobs` | `ListDefinitionsAsync` |
| `getAnalysisJobDefinition(code)` | GET | `/api/analysis-jobs/{code}` | `GetDefinitionAsync` |
| `createAnalysisJobDefinition(req)` | POST | `/api/analysis-jobs` | `CreateDefinitionAsync` |
| `updateAnalysisJobDefinition(code, req)` | PUT | `/api/analysis-jobs/{code}` | `UpdateDefinitionAsync` |
| `runAnalysisJobDefinition(code)` | POST | `/api/analysis-jobs/{code}/run` | `RunDefinitionAsync` |
| `getAnalysisJobDefinitionResults(code)` | GET | `/api/analysis-jobs/{code}/results` | `GetDefinitionResultsAsync` |

**The three toolbox blocks** are Outcome, Grain and Window; changing any one updates the payload panel live, and a parity line asserts that the payload the interface will send is identical to the payload the engine will receive. **Every option comes from `definition-options`**, never from a frontend list.

**VALIDATION.** No outcome selected -> refused with the field named. An outcome not registered in the outcome registry -> 400 naming it, never a silent empty result.

**ACCEPTANCE.** Two definitions authored and persisted from the interface, one of them a null control that must not produce a finding.

---

### Step 9 - Run the analysis through the readiness gate

**CONCEPT.** Evaluate whether the data can support a defensible answer **before** computing, and abstain with a reason if it cannot.

**CALLS.** `POST /api/analysis-jobs/{code}/run`; `GET /api/readiness/evaluate?outcome=&grain=&window=`; `advancedAnalysis.getAnalysisReadinessGates()`.

**THE GATE, exactly.** `ReadinessGate.Evaluate(ReadinessInput, ReadinessThresholds?)` returns `ReadinessReport(Overall, Dimensions[])` where `CanRun => Overall != Blocked`. Five dimensions, three states `Ready | Partial | Blocked`, and **the overall state is the worst across dimensions**, computed as `Math.Max` over the enum, never an average.

| Dimension | Direction | Ready | Partial |
|---|---|---|---|
| Independent heats | higher better | >= 60 | >= 30 |
| Outcome events | higher better | >= 40 | >= 15 |
| Minority-class balance | higher better | >= 10% | >= 3% |
| Freshness factor (age / cadence) | **lower better** | <= 1.0 | <= 2.0 |
| Required-field completeness | higher better | >= 95% | >= 85% |

Every dimension returns a `Reason` string built from the measured value and the threshold, for example `"42 in [30,60) (Partial)."`. **That string is why a blocked run is explainable from the database alone.** Thresholds are per-tenant configurable through a governed change with a recorded justification (Chapter 1.5.2) and are never lowered to make a run pass.

**PERSISTS.** A compute-run row with status, window and timestamps; results rows tied to it by run identifier. A blocked run is a **persisted run**, not an absent one.

**FAILURE.** A run killed mid-flight is reaped to a terminal state by `ComputeRunReaperHostedService`; no run remains `running` past the reaper window. A second run triggered immediately either queues or is refused cleanly; never two half-runs.

**ACCEPTANCE.** Runs complete or block honestly; the compute-run and results tables populate; the blocked state renders as an honest state and not an error.

---

### Step 10 - Read the findings

**CONCEPT.** Present what was found, ranked so that the important thing is first, with everything needed to disbelieve it.

**SURFACE.** Findings, `/correlations`.

**COLUMNS.** Feature, Outcome, Method, Effect size, q-value, Sample size, Stability, Stratum survival, Population, Run.

**THE STATISTICAL CONTRACT, exactly.** `PlantProcess.Analytics.Core.Discipline` defines the records and the rules:

- `Finding(Id, EffectSize, PValue, Method, SampleSize)`.
- `EffectRanking.RankByEffect` orders by `Math.Abs(EffectSize)` descending, **with the p-value only as a tie-breaker**. Ranking by p-value is prohibited.
- `BenjaminiHochberg.Adjust(pValues, q = 0.05)` returns `FdrItem(Index, PValue, QValue, Significant)`. **q-values are reported; raw p-values are not the headline.**
- `StratificationVerdict(Survives, Strata[], Reason)` with `StratumEffect(Stratum, EffectSize, SampleSize)`: a finding states whether it survives stratification and why.
- `BootstrapResult(PointEstimate, Lower, Upper, SignConsistency, Stable)`: a contributor that does not survive resampling is flagged.
- `CorrelationEngineRegistry.Resolve` selects between the `managed` engine (`ManagedStatisticalComputeEngine`, `DotNetAdvancedCorrelationEngine`) and the `postgres` engine (`PostgresCorrelationComputeEngine`) behind one `ICorrelationComputeEngine` interface. **One implementation per capability; the registry chooses the execution site, not the mathematics.**

**ACCEPTANCE.** A planted relation is recovered with q below the threshold; the planted null control is displayed as a first-class not-significant result rather than hidden; every number on screen matches the results row exactly; no fabricated value anywhere on the page.

---

### Steps 11 to 13 - Machine learning, prediction, recommendation

**CONCEPT.** The same governed machinery, deeper methods, licence-gated from Pro Plus.

**SURFACE.** ML Readiness and Models, Risk Dashboard, Suggestions.

**CALLS.** `mlReadiness.getWorkspace() | getScore() | getJobs() | ensureJobs() | getLabelPreview()`; `analytics.calculateRiskScore(...)`, `calculateRiskScoresBatch(...)`, `getMaterialFeatureVector(...)`, `getGenealogyAwareCorrelation(...)`; `assistantApi.generateSuggestions()`, `decideSuggestion()`, `getSuggestionHealth()`.

**THE GENEALOGY-AWARE FEATURE PATH** is the part worth naming, because no business-intelligence tool has it: `MaterialFeatureVector` is assembled by `PostgresCanonicalFeatureSource` and `NpgsqlFeatureVectorLoader` **through the genealogy graph**, so a parameter recorded at a parent grain is attributed to a child outcome by `ContributionWeight`. That is what makes an upstream melt parameter relatable to a downstream coil defect, and it is the mechanism behind the cross-source claim of Chapter 2.4.2.

**PERSISTS.** `model_registry` with version, features, training window and metrics; risk scores per unit with horizon and drivers; suggestions with evidence references and an outcome-tracking audit trail.

**VALIDATION.** Licence gate at the endpoint layer through `LicenseFeatureEndpointFilter` and `LicenseFeature`; entitlement derives only from the signed token, never a database row (Chapter 1.7).

**ACCEPTANCE.** An ML run executes under the same gate, telemetry and honesty contract as a statistical run; a tiny population blocks honestly; a lower tier is denied cleanly rather than shown a broken surface.

---

### Step 14 - The Supervisor

**CONCEPT.** A governed review that proposes improvements to other jobs, with provenance, and **no write access to the honesty machinery**.

**SURFACE.** Supervisor, `/data-integration/supervisor`, icon `BrainCircuit`, nav description "Weekly engine review (step 14)". Header subtitle contains, verbatim: *"Read-only: it never changes a job automatically."*

**SEQUENCE.** Header button **Run review now**, disabled with a spinner while busy. Empty state, verbatim: *"No supervisor reports yet. Click 'Run review now' to generate the first one."* Loading text: *"Loading reports..."*. Newest card title: `Supervisor report <yyyy-MM-dd HH:mm> UTC`. The body states the window covered, the count of evaluated associations and how many were significant at q below 0.05, up to three `Top associations:` lines formatted `feature -> outcome (effect X, q Y)`, and a recommendation line. Where no completed run exists, the body says so rather than inventing content.

**CALLS.** `supervisor.listSupervisorReports()` -> GET `/api/supervisor/reports`; `supervisor.runSupervisor()` -> POST `/api/supervisor/run`, returning `{ id, itemKey, title, body, findings, significant }`.

**PERSISTS.** A knowledge-base item of type `SUPERVISOR_REPORT` with `item_key` `supervisor-report-<timestamp>`, plus one job-log entry.

**THE GUARDRAIL, demonstrable.** Results counts before and after a Supervisor run are identical: it writes the report and the log entry and nothing else. Any threshold or configuration change is a separate provenance row naming who, from what, to what and why. **The honest-abstain machinery is outside its write scope by construction, not by convention** (Chapter 1.5.2).

---

### Step 15 - The assistant, and the plant data log

**THE ASSISTANT.**

**SURFACE.** Assistant, `/assistant`, nav description "Grounded chat runtime". Assistant Configuration, `/assistant-config`, exposing the grounding policy (`strict-citations-required`), the evidence policy (`citations-and-provenance-required`) and a **Max citations** control, with Save and Reset.

**CALLS.** `assistantApi.askAssistant(req)` -> POST `/api/assistant/ask`; `POST /api/assistant/reindex`; `getAssistantConfig`, `saveAssistantConfig`, `resetAssistantConfig`.

**THE COMPOSITION, exactly.** `AssistantService` assembles an answer from `AssistantTools` and retrieval only. The tools are typed and role-scoped: `FetchFindingTool`, `RunKpiTool`, `OpenSuggestionTool`, over resources including `canonical_material_units` and `material_unit_count`, with role scopes `viewer` and `operator` and structured refusals `bad_args` and `not_found`. Retrieval runs through `IRetrievalIndex` (`NpgsqlRetrievalIndex`) over chunks produced by `CanonicalChunkProducer`, embedded by `IEmbeddingProvider` (`DeterministicEmbeddingProvider`, `LocalSemanticEmbedder`). `GroundingService` enforces the citation contract, `AssistantEgressGuard.Plan` produces an `AssistantEgressPlan` deciding exactly what may leave the tenant, and `IAssistantModel` (`ExtractiveAssistantModel` by default) only phrases. **The model never computes**; `RunKpiTool` calls the deterministic engine.

**ACCEPTANCE.** Grounded questions return cited answers whose citations resolve to real rows; an unanswerable question produces a refusal rather than a guess; an off-corpus question deflects to scope; the assistant writes nothing but its audit log.

**THE PLANT DATA LOG (S5).**

**SURFACE.** Plant Data Log, `/data-integration/alerting`, icon `AlertTriangle`, nav description "Threshold alerts on imported observations". Header button **Run evaluation**, disabled with a spinner while busy.

**RULE FORM FIELDS.** Rule name (placeholder "Superheat high"), Parameter code (placeholder "SUPERHEAT_C"), Comparator select with exactly `>`, `>=`, `<`, `<=`, `=`, Limit (numeric input mode, placeholder "36"), Severity select Info / Warning / Critical.

**EMPTY STATES, verbatim.** Rules: *"No rules yet. Add one above."* Log: *"No breaches logged yet. Create a rule and run evaluation."*

**VALIDATION, verbatim.** *"Rule name and parameter code are required."* and *"Limit must be a number."* Server-side, a comparator outside the set returns 400 *"comparator must be one of > >= < <= ="*, never a 500 - enforced in the database as well by `CONSTRAINT ck_alert_rules_comparator CHECK (comparator IN ('>', '>=', '<', '<=', '='))`.

**CALLS.** `alerts.listRules()` -> GET `/api/log/rules`; `alerts.createRule(req)` -> POST `/api/log/rules` returning `{ id, ruleName, parameterCode, comparator, limitValue, severity }`; `alerts.evaluateAlerts()` -> POST `/api/log/evaluate` returning `{ logged: N }`; `alerts.listLog()` -> GET `/api/log/entries`.

**IDEMPOTENCE, by design.** A unique index on (rule, observation) plus `ON CONFLICT DO NOTHING` means a second evaluation over the same data returns `{ logged: 0 }`. **Zero double-logging is a demonstrable property, not a claim.**

**TABLES.** `alert_rules` and `plant_data_log`, specified column by column in 3.4.

---

## 3.2 The UI page inventory

Twenty-eight target pages. Every page publishes the contract of Chapter 4 (page contract) and is specified in 3.3.

| # | Page | Route | Primary components |
|---|---|---|---|
| A1 | Login | `/login` | `StandardCard`, `StandardInput`, `AuthProvider` |
| A2 | Home | `/` | `StandardStatGrid`, journey rail, readiness meter |
| B1 | Connections | `/data-integration/connections` | `DataIntegrationLayout`, `StandardDataTable`, provider grid |
| B2 | Dataset Registry | `/data-integration/registry` | schema tree, column table |
| B3 | Prepare Import | `/data-integration/prepare` | column multi-select, key and watermark pickers |
| B4 | Importing | `/data-integration/importing` | batch table, mapping refresh control |
| B5 | Jobs Monitor | `/data-integration/jobs` | `StandardDataTable`, `StatusPill` |
| B6 | Connector Truth | `/data-integration/connector-truth` | capability matrix |
| C1 | Transformation Studio (S1) | `/prep/canvas`, `/data-integration/author-mapping` | canvas, schema tree, palette, debug log |
| C2 | Mapping Health | `/mapping-health` | coverage table, drift panel |
| C3 | Data Quality | `/data-quality` | issue table by class |
| C4 | Plant Model Explorer | `/plant-model` | layout tree |
| C5 | Genealogy Explorer | `/materials/{id}` | thread view, edge graph |
| D1 | Interactive Workspace | `/workspace/:dashboardCode` | `DashboardGridLayout`, `SavedDashboardWidget`, `DashboardFilterBar`, `SelectionBreadcrumb`, associative panel |
| D2 | Page Builder (S2) | `/page-builder` | kind picker, shared shell |
| D3 | Analysis Toolbox (S3) | `/analysis/toolbox` | three blocks, payload panel |
| D4 | Findings | `/correlations` | `SortableDataTable`, evidence drawer |
| D5 | Risk Dashboard | `/risk` | score table, driver panel |
| D6 | Suggestions | `/suggestions` | suggestion cards, decision actions |
| D7 | Value Dashboard | `/value` | range cards, input drill-through |
| D8 | ML Readiness and Models | `/ml-readiness` | gate panels, model registry table |
| E1 | Assistant | `/assistant` | chat surface, citation chips |
| E2 | Assistant Configuration | `/assistant-config` | policy selects, max-citations |
| E3 | Plant Data Log (S5) | `/data-integration/alerting` | rule form, rules table, log table |
| E4 | Supervisor | `/data-integration/supervisor` | report cards |
| E5 | Reports | `/reports` | definition list, generate action |
| F1 | Users and Roles | `/admin/users` | role matrix |
| F2 | Licence and Entitlement | `/admin/license` | tier panel, capacity meters |
| F3 | Logging and Audit | `/admin/logs` | four-layer tabs |
| F4 | System Settings | `/admin/settings` | identity, units, language, log config |

---

## 3.3 Per-page technical specification

Every page is specified with this template. The template is the deliverable; a page without a completed template does not ship.

```
AIM            what the user achieves
ROLES          who sees it; who may act
LAYOUT         region by region, in logical (inline-start/end) terms
CONTROLS       every control: label, type, style token, position, enabled-when
HOOKS          the hooks it uses and what each owns
CALLS          mount calls and action calls
STATES         empty / loading / populated / filtered-empty / error / refused
SELECTIONS     associative participation
EMPTY-INSTALL  what it shows on day one
A11Y + RTL     keyboard path; mirrored verification
```

### B1 - Connections

**AIM.** Create and prove a read-only path to one customer source.
**ROLES.** Administrator and Data Engineer act; Engineer reads; Viewer denied.
**LAYOUT.** `DataIntegrationLayout` header (title, subtitle, Refresh, read-only promise line). Below it, two stacked `StandardCard` panels: DB Link Configuration, then Supported Connectors. In FORM mode panel 1 expands and panel 2 collapses.
**CONTROLS.**

| Control | Type | Token | Position | Enabled when |
|---|---|---|---|---|
| Refresh | secondary button, `RefreshCw` | Corporate Blue | layout header, inline-end | always |
| New Connection Profile | primary button, `Plus` | Electric Blue | panel 1 header, inline-end | LIST mode |
| Back | secondary | Corporate Blue | panel 1 header, inline-start | FORM mode |
| Name, Code, Host, Port, Database, Schema, Username | `StandardInput` | Industrial Blue field | form grid, two columns | always |
| Password | `StandardInput type=password` | as above | form grid | always |
| Provider type, Source system tag, Severity-like selects | `StandardSelect` | as above | form grid | options loaded |
| Test connection | secondary | Corporate Blue | form footer | host + database + credentials non-empty |
| Save | primary | Electric Blue | form footer, inline-end | client validation passes |
| Row: Edit / Test / Activate / Deactivate | icon buttons | Muted Steel, hover Electric Cyan | table row, inline-end | per row state |

**HOOKS.** `useDataIntegration` owns the layout's single load and the Refresh fan-out. `useApiResource` owns each panel's fetch state so **one panel failing never blanks the other**. `useOptimisticSave` owns Save: it shows the row immediately, reconciles on response, reverts with a named error on failure. `useInlineFormValidation` owns field-level refusal with no network call. `useStandardToast` owns the auto-dismissing success toast. `useEntitlements` hides tier-locked provider cards.
**CALLS.** Mount: `getConnectionProfiles`, `getProviderTypes` in parallel. Actions: `createConnectionProfile`, `updateConnectionProfile`, `testConnectionProfile`, `activateConnectionProfile`, `deactivateConnectionProfile`, `updateConnectionImportSchedule`.
**STATES.** Empty: "No connections yet. Create the first read-only link to a plant database." with the primary action inline. Loading: skeleton rows, never a spinner past one second. Error: contained card naming the layer. Refused: the read-only verification message.
**SELECTIONS.** None.
**EMPTY-INSTALL.** Empty profile list; the full provider catalogue with availability badges.
**A11Y + RTL.** Tab order follows the form grid; Escape leaves FORM mode; Enter in any field submits. Panels use inline-start and inline-end only, verified mirrored.

### C1 - Transformation Studio (S1)

**AIM.** Declare, once and permanently, how staged data becomes the canonical model.
**ROLES.** Data Engineer authors; Administrator publishes; Viewer denied. SQL mode requires Pro upward **and** an authoring role.
**LAYOUT.** Four regions. Inline-start: three-level schema tree (schema, table, attribute with type), presenting **two groups on S1 only** - staging shapes and the plant schema, because S1's purpose is to move data between them. Centre: the board, or the SQL editor in SQL mode. Inline-end: the block palette, grouped and searchable; **hidden entirely, not disabled, in SQL mode**. Bottom: the debug log, always present.
**CONTROLS.** Mode toggle (Block / SQL) at the top, always present. Canvas toolbar: zoom in, zoom out, **Zoom fit**, **Arrange**, minimap. **Preview (dry-run)**, **Compiled SQL**, **Publish version**, **Export**, **Import**. Node inspector on selection with typed controls fed from live schema, never free text for a key.
**HOOKS.** `useDebugLog` owns the three-severity log. `useApiResource` owns dry-run state. `useOptimisticSave` owns graph saves. `useInlineFormValidation` owns node-level refusal.
**CALLS.** `listStagedDatasets`, `createSession`, `saveGraph`, `runDryRun`, `runAuthoredSql`, `saveSqlVersion`, `publishVersion`; `previewJoin`, `materializeJoin`; `createMappingDefinition`, `executeMapping`.
**STATES.** Empty board: "Drag a staged table from the left to begin." Dry-run empty: distinguished from filtered-empty. Refused: the debug log carries the sentence; the wire never lands.
**EMPTY-INSTALL.** Tree lists registered datasets only; with none, it states so and links to Dataset Registry.
**A11Y + RTL.** Full keyboard node placement and wiring; the palette is a listbox; the tree is a treegrid. Every layout property is logical; no name encodes a side.

### D1 - Interactive Workspace

**AIM.** See the whole plant on one surface and narrow it by clicking anything.
**LAYOUT.** Page header. Then the **always-present selections bar**, reading "No selections applied" when empty. Then the associative strip. Then the twelve-column responsive grid.
**CONTROLS.** Per-card hover toolbar: maximise, collapse, export, clone, remove, edit. Chart-type switcher offering only types the server registry accepts for that binding - **absence elsewhere is correct, not a defect**. Filter bar with clear-all. Save layout, Reset layout.
**HOOKS.** `useDashboardFilters` and `useDashboardSelection` implement publish-and-subscribe: widgets publish selections, all widgets subscribe to the merged filter set. `useAssociative` owns selected, possible and excluded state. `useDashboardGridLayout` and `useDashboardLayoutPersistence` own drag, resize with live neighbour displacement, and persistence to `layout_json`. `useLatestOnlyPolling` prevents a slow response overwriting a newer one.
**CALLS.** `getDashboardWorkspace`, `queryDashboardWidget` per widget, `updateDashboardLayout`, `updateDashboardWidgetLayout`.
**RULE.** Data access is **exclusively** through the widget-query contract. No page-private fetch for an analytics visual.
**STATES.** Widget-level isolation: one widget's endpoint failing shows an error in that card only, and the page stays interactive. This is the single most important resilience behaviour a buyer notices.
**SELECTIONS.** Full participation; every selection is visible in the breadcrumb and individually removable.

### E3 - Plant Data Log (S5)

Specified in full in 3.1, step 15, including every label, placeholder, comparator, validation message and empty state.

### The remaining pages

A1, A2, B2 to B6, C2 to C5, D2 to D8, E1, E2, E4, E5, F1 to F4 follow the identical template. Their control tables, hook assignments and call lists are written against the same six regions and the same nine states; where a page's surface design needs depth beyond the contract - the workspace, the shell, the toolbox, the assistant - Chapter 4 owns it.

---

## 3.4 Database schemas, tables, keys and joins

Three application schemas (Chapter 4.6 of the previous chapter set: `ppiq_staging`, `ppiq_plant`, `ppiq_meta`). Common conventions on every table: `id uuid PRIMARY KEY`, `tenant_id uuid NOT NULL`, `created_at_utc timestamptz NOT NULL`, `updated_at_utc timestamptz NULL`, soft-delete triple `is_deleted boolean NOT NULL DEFAULT false`, `deleted_at_utc`, `deleted_reason varchar(500)`, and on every imported row the provenance triple `source_system varchar(100)`, `source_record_id varchar(100)`, `import_batch_id uuid`.

### 3.4.1 Staging

**`ppiq_staging.import_batches`** - one row per push. Columns include source dataset reference, `source_object_name`, status, `started_at_utc`, `finished_at_utc`, row count, watermark range, checksum.

**`ppiq_staging.staging_records`** - one row per source row, verbatim.

| Column | Type | Notes |
|---|---|---|
| `id` | uuid | PK |
| `import_batch_id` | uuid | **FK -> `import_batches(id)` ON DELETE RESTRICT** |
| `source_object_name` | varchar(200) | NOT NULL |
| `row_number` | integer | NOT NULL |
| `raw_json` | jsonb | NOT NULL - the source row, uninterpreted |
| `is_processed` | boolean | NOT NULL DEFAULT false |
| `processed_at_utc` | timestamptz | NULL |
| `processing_status` | varchar(50) | NOT NULL DEFAULT `'Pending'` |
| `processing_error` | varchar(4000) | NULL |
| `canonical_entity_id` | uuid | NULL - what this row became |
| `canonical_entity_name` | varchar(200) | NULL |
| provenance + audit + soft-delete | | as per conventions |

`ON DELETE RESTRICT` is deliberate: a batch cannot be deleted while its rows exist, because that would orphan the lineage every canonical row depends on.

### 3.4.2 Canonical - the four tables that carry the product

**`ppiq_plant.material_units`**

| Column | Type |
|---|---|
| `material_code` | varchar(100) NOT NULL |
| `material_unit_type` | varchar(50) NOT NULL |
| `product_family` | varchar(100) |
| `grade_or_recipe` | varchar(100) |
| `site_id` | uuid FK -> `sites(id)` |
| `production_start_utc`, `production_end_utc` | timestamptz |
| provenance triple | |

Indexes: unique `(site_id, material_code)`; unique `(source_system, source_record_id)` **filtered** `WHERE source_system IS NOT NULL AND source_record_id IS NOT NULL`; `(site_id)`; `(material_unit_type)`; `(site_id, material_unit_type)`; `(material_unit_type, grade_or_recipe)`.

*Why the filtered unique index matters:* it makes projection idempotent per source row without forbidding rows that legitimately have no source identity, which is what makes re-executing a mapping safe.

**`ppiq_plant.genealogy_edges`**

| Column | Type |
|---|---|
| `parent_material_unit_id` | uuid FK -> `material_units(id)` |
| `child_material_unit_id` | uuid FK -> `material_units(id)` |
| `relationship_type` | varchar(50) NOT NULL |
| `contribution_weight` | **numeric(9,6) NOT NULL** |
| `provenance_confidence` | numeric(9,6) NOT NULL |
| `is_transition` | boolean NOT NULL DEFAULT false |
| `effective_from_utc`, `effective_to_utc` | timestamptz |

Indexes: `(parent_material_unit_id)`; `(child_material_unit_id)`; unique `(parent_material_unit_id, child_material_unit_id)`; and the covering index `(child_material_unit_id, is_transition, contribution_weight)`.

**The invariant:** `SUM(contribution_weight) = 1.0` exactly, per child, enforced at the database level by constraint and trigger. `numeric(9,6)` rather than a float is deliberate - a float cannot hold that invariant. `is_transition` marks a unit spanning two parents, which is the case where blended attribution actually matters, and the covering index exists because the feature loader reads exactly those three columns per child.

**`ppiq_plant.parameter_observations`** - the volume table.

| Column | Type |
|---|---|
| `material_unit_id` | uuid FK -> `material_units(id)` |
| `parameter_definition_id` | uuid FK -> `parameter_definitions(id)` |
| `process_step_execution_id` | uuid FK, nullable |
| `equipment_id` | uuid FK, nullable |
| `observed_at_utc` | timestamptz |
| `observed_at_local` | **timestamp without time zone** |
| `plant_time_zone_id` | varchar(100) NOT NULL |
| `numeric_value` | **numeric(18,6)** |
| `text_value` | varchar(500) |
| `unit_of_measure` | varchar(50) |
| `quality_flag` | varchar(50) NOT NULL |
| `raw_value` | varchar(500) |

Indexes on `(material_unit_id)`, `(parameter_definition_id)`, `(process_step_execution_id)`, `(equipment_id)`, `(observed_at_utc)`, `(observed_at_local)`.

*Two design points a reviewer should check.* Storing **both** `observed_at_utc` and `observed_at_local` with an explicit `plant_time_zone_id` is what makes shift-boundary analysis correct across daylight-saving transitions; a single UTC column silently corrupts shift attribution twice a year. And `numeric(18,6)` with `raw_value` retained means the original string survives, so a parsing dispute is resolvable.

**`ppiq_plant.quality_events`**

| Column | Type |
|---|---|
| `material_unit_id` | uuid FK -> `material_units(id)` |
| `defect_catalog_id` | uuid FK -> `defect_catalogs(id)` |
| `event_type` | varchar(100) NOT NULL |
| `severity` | varchar(50) |
| `decision` | varchar(100) |
| `description` | varchar(1000) |
| `event_at_utc` | timestamptz |
| `event_at_local` | timestamp without time zone |
| `plant_time_zone_id` | varchar(100) NOT NULL |

Indexes on `(material_unit_id)`, `(defect_catalog_id)`, `(event_type)`, `(event_at_utc)`, `(event_at_local)`, and `(material_unit_id, event_type, event_at_utc)`.

*Acceptance query for the taxonomy rule:* `SELECT count(*) FROM quality_events WHERE defect_catalog_id IS NULL` must not grow after a projection. A growing count means the resolver did not find the imported catalogue, which means taxonomy was not imported first.

### 3.4.3 Operational tables

**`alert_rules`**: `id uuid PK DEFAULT gen_random_uuid()`, `rule_name text NOT NULL`, `parameter_code text NOT NULL`, `comparator text NOT NULL`, `limit_value double precision NOT NULL`, `severity text NOT NULL DEFAULT 'Warning'`, `is_active boolean NOT NULL DEFAULT true`, `created_at_utc timestamptz NOT NULL DEFAULT now()`, plus `CONSTRAINT ck_alert_rules_comparator CHECK (comparator IN ('>', '>=', '<', '<=', '='))`.

**`plant_data_log`**: `id`, `alert_rule_id uuid NOT NULL REFERENCES alert_rules(id) ON DELETE CASCADE`, `parameter_observation_id uuid NULL`, `material_code text`, `parameter_code text NOT NULL`, `observed_value double precision`, `comparator text NOT NULL`, `limit_value double precision NOT NULL`, `severity text NOT NULL`, `message text NOT NULL`, `logged_at_utc timestamptz NOT NULL DEFAULT now()`, plus the unique `(alert_rule_id, parameter_observation_id)` index that makes evaluation idempotent.

*Note the deliberate denormalisation:* the log stores `comparator`, `limit_value` and `material_code` rather than only the rule reference, so that **editing a rule later does not rewrite history**. A log entry states the condition that fired at the time it fired.

**`job_log`**: `occurred_at_utc`, `job_type`, `job_name`, `run_id`, `severity`, `message`, `site_code`, context payload. One monitor reads it for every job family: import, `CanonicalRefresh`, analysis, ML, `SUPERVISOR`, `ALERT_EVAL`.

**`ppiq_business_key_definitions`** and **`ppiq_business_key_members`** (`definition_id` FK `ON DELETE CASCADE`, `member_role`, `source_field`, `sort_order`) hold the business-key dictionary that resolves the customer's several identifiers for one physical unit. This is the table that makes cross-source joining auditable rather than magical.

### 3.4.4 The join graph

Every analytical question resolves along one of five paths. A reviewer should be able to draw these from memory.

```
J1  Parameter to defect, same unit
    parameter_observations -> material_units -> quality_events -> defect_catalogs
    joined on material_unit_id; the base of every same-grain correlation

J2  Parameter to defect, ACROSS GRAIN  (the product's reason to exist)
    parameter_observations (parent grain)
      -> genealogy_edges (parent_material_unit_id)
      -> material_units (child)
      -> quality_events (child grain)
    attributed by genealogy_edges.contribution_weight, weights summing to 1.0 per child

J3  Cross-source identity resolution
    staging_records(raw_json) -> [Transformation Definition + business key dictionary]
      -> material_aliases -> material_units
    the join is DECLARED here, once, and never re-derived downstream

J4  Loss attribution
    downtime_events -> equipment -> areas -> sites
      and equipment -> process_step_executions -> material_units
    carrying BOTH stopped_minutes and production_impact_minutes

J5  Provenance walk-back (the audit path)
    any canonical row -> import_batch_id -> import_batches
      -> source_dataset_definitions -> connection_profiles
    every figure on every screen resolves along J5 to the source it came from
```

**J2 is the one to show a sceptical engineer.** It is what a business-intelligence tool cannot do without the genealogy table and the weight invariant, and it is the mechanism behind every claim in Chapter 2.

---

## 3.5 Credentials, identities and topology

*Per the author's ruling, this document carries operational credentials in full. They are held in this single contiguous section and nowhere else, so a customer-safe extract is one deletion.*

### 3.5.1 Component topology

| Component | Listens | Role |
|---|---|---|
| Web application | 5173 (dev), 443 via proxy | The interface |
| API service | 5063 (dev), behind proxy | 27 API domains |
| Workers | none | Import, projection, analysis, ML, supervisor, report pools |
| PostgreSQL | 5432 | Three schemas |
| Reverse proxy | 80, 443 | TLS and routing |
| Collector | customer DMZ | One-way push |
| Model gateway | internal | Assistant serving modes |

### 3.5.2 Launch and lifecycle commands

Two profiles select the database without a branch: `local` for the empty-start development database and `presentation` for the populated demonstration database. Migration, update, list, build, test and end-to-end procedures are specified in Chapter 7; note that the first end-to-end run on a machine writes visual baselines and reports them as failures, which is baseline creation and not regression.

### 3.5.3 Emulated source fleet

Six containers mirroring real plant systems across PostgreSQL, Oracle, SQL Server and MySQL, plus file drops, each reachable by the collector exactly as a customer source would be. Hosts, service names, ports and database names are emulation fixtures, versioned outside the product (Chapter 1.6).

### 3.5.4 Credentials

> **[CREDENTIALS BLOCK - verbatim insertion at assembly from `commands.txt`]**
>
> This block receives, unaltered: server SSH access; the application database identity; the build-system identity; the public service URLs; and the six emulated source identities with hosts, ports and database names.
>
> `commands.txt` was supplied earlier in this project and is not in my current working context. Re-attach it with the assembly request and the values go in unchanged.
>
> **Standing operations obligation carried into Chapter 7:** one password is currently shared between the application database and the build system. The rotation task and the per-environment credential split are registered there.

---

*End of Chapter 3. Chapter 4 specifies the analysis page, the low-code shell, concurrency and load balancing, the gate and engine internals, statistics and correlation, AI and ML, and the assistant.*

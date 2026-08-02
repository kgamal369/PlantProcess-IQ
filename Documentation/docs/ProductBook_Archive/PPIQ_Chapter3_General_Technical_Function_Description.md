# PlantProcess IQ - Master Design Document

**Version 4.1 | Author: Karim, SOU Industrial Software, Dusseldorf**

---

# CHAPTER 3 - GENERAL SOFTWARE PRODUCT TECHNICAL FUNCTION DESCRIPTION

*PPIQ.txt item 4, "Chapter 3". Sections numbered 4.1 to 4.8 per the guideline: 4.1 the step list, 4.2 each step to endpoint level, 4.3 the page list, 4.4 every page in depth, 4.5 schemas with keys and joins, 4.6 credentials and topology. Audience (4.7): the customer's advanced IT and software staff and our developers taking hand-over. Voice (4.8): senior software engineer and technical lead.*

**One file per chapter.** Extra sub-numbers 4.5.10 to 4.5.14 are appended per the guideline's allowance; main numbers are unchanged.

---

## 4.1 The list of data-flow steps, and 4.2 each step to endpoint level

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


---

## 4.3 The page inventory

| # | Page | Route | Journey step |
|---|---|---|---|
| A1 | Login | `/login` | 1 |
| A2 | Home | `/` | - |
| B1 | Connections | `/data-integration/connections` | 4 |
| B2 | Dataset Registry | `/data-integration/registry` | 5 |
| B3 | Prepare Import | `/data-integration/prepare` | 5 |
| B4 | Importing | `/data-integration/importing` | 6, 8 |
| B5 | Jobs Monitor | `/data-integration/jobs` | 6, 8, 12 |
| B6 | Connector Truth | `/data-integration/connector-truth` | 4 |
| C1 | Transformation Studio (S1) | `/prep/canvas`, `/data-integration/author-mapping` | 7 |
| C2 | Mapping Health | `/mapping-health` | 8 |
| C3 | Data Quality | `/data-quality` | 8 |
| C4 | Plant Model Explorer | `/plant-model` | 8 |
| C5 | Genealogy Explorer | `/materials/{id}` | 9 |
| D1 | Interactive Workspace | `/workspace/:dashboardCode` | 11 |
| D2 | Page Builder (S2) | `/page-builder` | 10 |
| D3 | Analysis Toolbox (S3) | `/analysis/toolbox` | 12 |
| D4 | Findings | `/correlations` | 13 |
| D5 | Risk Dashboard | `/risk` | 13 |
| D6 | Suggestions | `/suggestions` | 13 |
| D7 | Value Dashboard | `/value` | 13 |
| D8 | ML Readiness and Models | `/ml-readiness` | 12, 13 |
| D9 | Early Warning | `/early-warning` | 13 |
| D10 | Practice Insights | `/practice-insights` | 13 |
| E1 | Assistant (persistent dock) | every page | 14 |
| E2 | Assistant Configuration | `/assistant-config` | 14 |
| E3 | Plant Data Log (S5) | `/data-integration/alerting` | 15 |
| E4 | Supervisor | `/data-integration/supervisor` | 15 |
| E5 | Reports | `/reports` | 15 |
| F1 | Users and Roles | `/admin/users` | 3 |
| F2 | Licence and Entitlement | `/admin/license` | 2 |
| F3 | Authoring Quota and Limits | `/admin/quota` | 3 |
| F4 | Jobs Administration | `/admin/jobs` | 5 |
| F5 | Logging and Audit | `/admin/logs` | 15 |
| F6 | Log Channel Configuration | `/admin/log-channels` | 15 |
| F7 | System Settings | `/admin/settings` | 1 |
| F8 | Translation and Language | `/admin/translation` | 1 |

---

## 4.4 Per-page specification

### The template

Every page below is specified with these ten fields. A page without a completed template does not ship.

```
AIM            what the user achieves
ROLES          who acts, who reads, who is denied
LAYOUT         region by region, in logical terms (inline-start / inline-end / block-start / block-end)
CONTROLS       every control: label, type, colour token, position, enabled-when
HOOKS          each hook and what it owns
CALLS          mount calls, then action calls
STATES         empty / loading / populated / filtered-empty / error / refused
SELECTIONS     associative participation
EMPTY-INSTALL  what it shows on day one at a customer
A11Y + RTL     keyboard path and mirrored verification
```

### Conventions used throughout

- Colour tokens are the thirteen of Chapter 1.9.1. Primary action is Electric Blue `#0A84FF`; secondary is Corporate Blue `#2F80ED`; destructive confirmation is Hot Red `#FF4D6D`; icon default Muted Steel `#8EA7C1` with Electric Cyan `#00D4FF` on hover and focus.
- Primitives are the twenty-two `Standard*` components. Raw HTML controls are forbidden outside the primitive layer.
- Every page inherits `AuthProvider`, `EntitlementsProvider`, `LicenseProvider`, `ThemeProvider`, `StandardToastProvider` and `V5I18nProvider` from the application shell; those are not repeated per page.
- Every page's loading state is a skeleton, never a bare spinner past one second.
- Every refusal renders the sentence: what was refused, why, and what would satisfy it (Chapter 1.5.7).

---

## Group A - Enter

### A1 Login - `/login`

**AIM.** Prove identity and obtain a session.
**ROLES.** Unauthenticated. No navigation is rendered.
**LAYOUT.** Single centred `StandardCard` on the Deep Navy Black field. Block-start: wordmark. Centre: form. Block-end: build identifier and language selector.
**CONTROLS.**

| Control | Type | Token | Position | Enabled when |
|---|---|---|---|---|
| Username | `StandardInput` | Industrial Blue field | card, first | always |
| Password | `StandardInput type=password` | as above | card, second | always |
| Sign in | primary button | Electric Blue | card footer, full width | both fields non-empty |
| Second factor code | `StandardInput` | as above | replaces form after step 1 | challenge issued |
| Language | select | Muted Steel | block-end | always |

**HOOKS.** `useAuth` owns the credential exchange, token custody and redirect. `useInlineFormValidation` owns empty-field refusal with no network call. `useV5I18n` owns the language switch.
**CALLS.** Actions: `POST /api/auth/login` -> `{ accessToken, user, entitlements }`; `POST /api/auth/refresh`; `POST /api/auth/logout`.
**STATES.** Error: "Sign-in failed. Check your username and password." Never which half failed. Locked: "This account is locked. Contact your administrator." Refused: expired licence states that the installation is read-only and login still succeeds.
**SELECTIONS.** None.
**EMPTY-INSTALL.** Identical. The vendor support account exists; the customer administrator was created at commissioning.
**A11Y + RTL.** Autofocus on username; Enter submits from either field; the card is a labelled form region. **Access token is held in memory with a rotating refresh cookie, never in browser storage** (Chapter 1.7).

### A2 Home - `/`

**AIM.** Answer "is the installation healthy, how far is commissioning, and what is new" in one screen.
**ROLES.** All authenticated roles; content is permission-scoped.
**LAYOUT.** Block-start: page header with site name. Then the journey rail, ten nodes, current node highlighted. Then a `StandardStatGrid` of four figures. Then two columns: readiness meter (inline-start), recent findings (inline-end). Block-end: collecting-data state when the installation is young.
**CONTROLS.**

| Control | Type | Token | Position | Enabled when |
|---|---|---|---|---|
| Journey rail node | link chip | Cyan Green complete, Electric Cyan current, Muted Steel pending | rail | node's page permitted |
| Stat tile | `StandardStatGrid` cell | Near-White value, Muted Steel label | grid | always |
| Readiness dimension bar | progress bar | Cyan Green / Amber / Hot Red | inline-start column | gate evaluated |
| Finding row | link | Near-White | inline-end column | findings exist |
| Refresh | secondary | Corporate Blue | header inline-end | always |

**HOOKS.** `useApiResource` per region, so one failing region never blanks the page. `useEntitlements` hides tier-locked tiles. `useLatestOnlyPolling` refreshes figures without a slow response overwriting a newer one.
**CALLS.** Mount, in parallel: `getDashboardOverview()` -> `GET /analytics/dashboard/overview`; `GET /api/readiness/evaluate`; `GET /api/findings?limit=5`; `GET /api/imports/watermarks`.
**STATES.** Empty (no data yet): the collecting-data state naming the next commissioning action and linking to it. **Never a blank landing page.** Filtered-empty does not apply. Error: per-region card.
**SELECTIONS.** None.
**EMPTY-INSTALL.** Journey rail with node 1 current, all figures zero and labelled as such, readiness meter showing measured zeros with thresholds, and the sentence naming step 4 as the next action.
**A11Y + RTL.** Rail is a navigation list; each stat tile is a labelled figure; column order is logical.

---

## Group B - Connect and import

### B1 Connections - `/data-integration/connections`

**AIM.** Create and prove a read-only path to one customer source, and record how it may be used.
**ROLES.** Administrator and Data Engineer act. Engineer reads. Viewer denied.
**LAYOUT.** `DataIntegrationLayout` header: title "Data Integration", subtitle "Connect plant sources, map them to the canonical model, run imports and watch every job.", **Refresh** button inline-end, and the permanent line "Connections are read-only toward your source systems at all times." Then two stacked `StandardCard` panels: "DB Link Configuration" (subtitle "Connection profiles to customer source databases and files"), then "Supported Connectors" (subtitle "Available and planned data source provider types"). In FORM mode panel 1 expands and panel 2 collapses.
**CONTROLS.**

| Control | Type | Token | Position | Enabled when |
|---|---|---|---|---|
| Refresh | secondary, `RefreshCw` | Corporate Blue | layout header, inline-end | always |
| New Connection Profile | primary, `Plus` | Electric Blue | panel 1 header, inline-end | LIST mode |
| Back | secondary | Corporate Blue | panel 1 header, inline-start | FORM mode |
| Name / Code / Host / Port / Database / Schema / Username | `StandardInput` | Industrial Blue field | form grid, two columns | always |
| Password | `StandardInput type=password` | as above | form grid | always |
| Provider type | `StandardSelect` | as above | form grid | provider types loaded |
| Source system tag | `StandardSelect` | as above | form grid | always |
| File path | `StandardInput` | as above | form grid | provider is file-based |
| Max rows / Timeout / Requests per minute / Approved window | `StandardInput`, time pickers | as above | budget group | always |
| Test connection | secondary | Corporate Blue | form footer | host, database and credentials non-empty |
| Save | primary | Electric Blue | form footer, inline-end | client validation passes |
| Row Edit / Test / Activate / Deactivate | icon buttons | Muted Steel, hover Electric Cyan | table row, inline-end | per row state |

Provider-dependent fields show and hide on provider change. **Oracle and MySQL do not ask identical questions.**
**HOOKS.** `useDataIntegration` owns the layout load and the Refresh fan-out. `useApiResource` per panel for failure isolation. `useOptimisticSave` shows the row immediately and reverts with a named error on failure. `useInlineFormValidation` refuses empty required fields with no network call. `useStandardToast` for the auto-dismissing success toast. `useEntitlements` hides tier-locked provider cards.
**CALLS.** Mount, parallel: `getConnectionProfiles(includeSecrets)` -> `GET /api/connections`; `getProviderTypes()` -> `GET /api/connections/catalog`. Actions: `createConnectionProfile`, `updateConnectionProfile`, `testConnectionProfile`, `activateConnectionProfile`, `deactivateConnectionProfile`, `updateConnectionImportSchedule`.
**STATES.** Empty: "No connections yet. Create the first read-only link to a plant database." with the primary action inline. Error: contained card naming the layer - network, authentication or permission. **Refused:** a write-capable credential fails the test with the read-only verification named.
**SELECTIONS.** None.
**EMPTY-INSTALL.** Empty profile list; full provider catalogue with availability badges, unavailable providers dimmed and badged Planned.
**A11Y + RTL.** Tab follows the form grid; Escape leaves FORM mode; Enter submits. Panels use inline-start and inline-end only.

### B2 Dataset Registry - `/data-integration/registry`

**AIM.** Choose which source objects enter the product.
**ROLES.** Data Engineer acts; Engineer reads; Viewer denied.
**LAYOUT.** Layout header. Inline-start: connection selector then the three-level source tree (schema, table, column with observed type). Inline-end: the registered-dataset table.
**CONTROLS.**

| Control | Type | Token | Position | Enabled when |
|---|---|---|---|---|
| Connection selector | `StandardSelect` | Industrial Blue | inline-start, block-start | profiles exist |
| Tree node expander | disclosure | Muted Steel | tree | node has children |
| Register | primary | Electric Blue | tree row, inline-end | a table is selected |
| Unregister | secondary | Corporate Blue | registered row | row selected |
| Filter tables | `StandardInput` search | Industrial Blue | above tree | tree loaded |

**HOOKS.** `useApiResource` for discovery and for the registered list separately. `useOptimisticSave` for Register. `useDebugLog` surfaces discovery refusals with their reason.
**CALLS.** `listSourceTables(profileId)` -> `GET /api/connections/{id}/discover`; `listSourceColumns(profileId, schema, table)` -> `GET /api/connections/{id}/discover/{schema}/{table}/columns`; `registerSourceTable` -> `POST /api/datasets`; `getSourceDatasets` -> `GET /api/datasets`.
**STATES.** Empty tree: "Select a connection to browse its tables." Discovery timeout: "The source did not answer within `<n>`s under the configured budget", with a link to raise the timeout. Filtered-empty on the search: distinguished, with "clear filter".
**SELECTIONS.** None.
**EMPTY-INSTALL.** No connections, so the selector states so and links to B1.
**A11Y + RTL.** The tree is a treegrid with full keyboard traversal; column type is announced with the name.

### B3 Prepare Import - `/data-integration/prepare`

**AIM.** Declare, per dataset, the imported columns, the business key and the watermark.
**ROLES.** Data Engineer acts.
**LAYOUT.** Layout header. Inline-start: dataset list. Inline-end: three stacked groups - Columns, Business key, Watermark - then the footer action.
**CONTROLS.**

| Control | Type | Token | Position | Enabled when |
|---|---|---|---|---|
| Dataset row | list item | Near-White | inline-start | always |
| Column include | checkbox per column, plus select-all | Electric Cyan when checked | Columns group | dataset selected |
| Business key | multi-select of columns | Industrial Blue | Business key group | dataset selected |
| Watermark column | `StandardSelect` of orderable columns only | Industrial Blue | Watermark group | dataset selected |
| Save preparation | primary | Electric Blue | footer, inline-end | at least one column included |

**HOOKS.** `useInlineFormValidation`, `useOptimisticSave`, `useStandardToast`.
**CALLS.** `getSourceDatasets`; `updateDatasetCursor(id, req)` -> `PUT /api/datasets/{id}/cursor`.
**STATES.** **Warning, not error**, when no watermark is chosen: "Without a watermark every run re-reads the whole table." Refused: a watermark column of a non-orderable type is rejected server-side with the type named.
**SELECTIONS.** None.
**EMPTY-INSTALL.** "No datasets registered yet", linking to B2.
**A11Y + RTL.** Select-all is a labelled checkbox with an indeterminate state; groups are fieldsets.

### B4 Importing - `/data-integration/importing`

**AIM.** Run imports, watch batches, and schedule the projection per mapping.
**ROLES.** Data Engineer acts; Engineer reads.
**LAYOUT.** Layout header. Block-start: batch table. Block-end: the mapping refresh schedule card.
**CONTROLS.**

| Control | Type | Token | Position | Enabled when |
|---|---|---|---|---|
| Run due imports | primary | Electric Blue | batch card header | at least one dataset is due |
| Batch row expander | disclosure | Muted Steel | batch table | always |
| Re-run batch | secondary | Corporate Blue | expanded row | batch is terminal |
| Mapping selector | `StandardSelect` | Industrial Blue | schedule card | mappings exist |
| Interval minutes | `StandardInput` numeric, default 15 | Industrial Blue | schedule card | mapping selected |
| Save schedule | primary | Electric Blue | schedule card footer | interval valid |

**HOOKS.** `useApiResource` per card. `useLatestOnlyPolling` for live batch progress. `useStandardToast`.
**CALLS.** `getImportBatches()` -> `GET /api/imports/batches`; `runDueSourceImports()` -> `POST /api/imports/run-due`; `getStagingSummary()`; `updateMappingRefreshSchedule(mappingId, { scheduleExpression, refreshIntervalMinutes })` -> `PUT /api/transformations/{id}/schedule`. Toast: "Canonical refresh schedule saved and JobDefinition updated".
**STATES.** Batch failed: the message in the row, never an exception page. Progress: streamed counts, not a spinner.
**SELECTIONS.** None.
**EMPTY-INSTALL.** "No import batches yet. Register a dataset and run the first import."
**A11Y + RTL.** Row expansion is a disclosure button with expanded state announced.

### B5 Jobs Monitor - `/data-integration/jobs`

**AIM.** One surface answering what ran, what is running, what failed and why, across every job family.
**ROLES.** Operator and above read. Data Engineer and Administrator may act.
**LAYOUT.** Layout header. Filter bar: family, status, time range. Then the job table. Row expansion reveals the run log.
**CONTROLS.**

| Control | Type | Token | Position | Enabled when |
|---|---|---|---|---|
| Family / Status / Time filter | `StandardSelect` | Industrial Blue | filter bar | always |
| Run now | primary small | Electric Blue | row, inline-end | job runnable and not running |
| Pause / Resume | secondary small | Corporate Blue | row, inline-end | per state |
| Cancel | destructive small | Hot Red | row, inline-end | job running |
| Status pill | `StatusPill` | Cyan Green / Amber / Hot Red / Muted Steel | Status column | always |

Columns: Job, Type, Target, Status, Last Run, Duration, Runtime, Actions.
**HOOKS.** `useApiResource`, `useLatestOnlyPolling`, `useCustomerSafeAction` (guards Cancel behind a confirmation), `useStandardToast`.
**CALLS.** `getAdminJobsMonitor()` -> `GET /api/jobs`; `getJobHistory(id)` -> `GET /api/jobs/{id}/runs`; `runJobNow(id, "Admin UI")`, `pauseJob(id)`, `resumeJob(id)`; `GET /api/log/entries?layer=job`.
**STATES.** **A refused run appears as a real run** with status blocked and the failing dimension named, not as an absence. Paused is a first-class state and survives a restart. Error severity renders in Hot Red with a clean message.
**SELECTIONS.** None.
**EMPTY-INSTALL.** "No jobs defined yet. Registering a dataset creates its import job."
**A11Y + RTL.** The table is sortable by keyboard; status is announced as text, not colour alone.

### B6 Connector Truth - `/data-integration/connector-truth`

**AIM.** State honestly, per connector, what is proven.
**ROLES.** All read; nobody acts.
**LAYOUT.** Layout header. A matrix: connector down, capability across (discover, read, incremental, taxonomy view, read-only certified, load budget honoured).
**CONTROLS.** Filter by state; export the matrix. No mutating control exists on this page by design.
**HOOKS.** `useApiResource`.
**CALLS.** `getConnectorTruth()` -> `GET /api/connections/truth`.
**STATES.** Every cell is proven, planned or not applicable. **There is no "probably" state**, because the page exists to remove that word.
**SELECTIONS.** None.
**EMPTY-INSTALL.** Fully populated; this page describes the product, not the customer's data.
**A11Y + RTL.** Matrix is a table with row and column headers; state is text plus icon.

---

## Group C - Model the plant

### C1 Transformation Studio (S1) - `/prep/canvas`, `/data-integration/author-mapping`

**AIM.** Declare, once and permanently, how staged data becomes the canonical model.
**ROLES.** Data Engineer authors. Administrator publishes. Viewer denied. **SQL mode requires Pro upward and an authoring role.**
**LAYOUT.** Four regions. Block-start: mode toggle (Block / SQL), always present, plus the canvas toolbar and the global validity indicator beside Run. Inline-start: three-level schema tree; **on S1 only it presents two groups** - staging shapes and the plant schema - because S1's purpose is to move data between them. Centre: the board, or the SQL editor in SQL mode. Inline-end: the block palette, grouped and searchable, **hidden entirely and not disabled in SQL mode**. Block-end: the debug log, always present.
**CONTROLS.**

| Control | Type | Token | Position | Enabled when |
|---|---|---|---|---|
| Mode toggle Block / SQL | segmented control | Electric Cyan active | block-start, inline-start | SQL requires tier and role |
| Zoom in / out / **Zoom fit** / **Arrange** | icon buttons | Muted Steel, hover Electric Cyan | canvas toolbar | board has nodes |
| Validity indicator | badge | Cyan Green valid, Hot Red invalid | beside Run | always |
| Preview (dry-run) | secondary | Corporate Blue | toolbar inline-end | graph valid |
| Compiled SQL | secondary | Corporate Blue | toolbar inline-end | graph valid |
| Publish version | primary | Electric Blue | toolbar inline-end | graph valid and previewed |
| Export / Import definition | icon buttons | Muted Steel | toolbar overflow | always / always |
| Node status badge | badge on node | Cyan Green OK, Amber warning, Hot Red error | each node | always |
| Node inspector fields | typed controls fed from live schema | Industrial Blue | inline-end when a node is selected | node selected |
| Debug log severity filter | segmented | Muted Steel | log header | log has entries |

**Port colours by type** (Chapter 1.9.1): key `#00D4FF`, number `#0A84FF`, text `#7AA7C7`, date `#B48CFF`. **The colour communicates the type and the type enforces legality**; a colour that does not correspond to an enforced rule is a lie told by the interface.
**HOOKS.** `useDebugLog` owns the three-severity log. `useApiResource` owns dry-run state. `useOptimisticSave` owns graph saves. `useInlineFormValidation` owns node-level refusal.
**CALLS.** `listStagedDatasets()`, `createSession()`, `saveGraph()`, `runDryRun()`, `runAuthoredSql()`, `saveSqlVersion()`, `publishVersion()`; `previewJoin()`, `materializeJoin()`; `createMappingDefinition(body)` -> `POST /api/transformations`; `executeMapping(id, batchId, stopOnFirstError)`.
**STATES.** Empty board: "Drag a staged table from the left to begin." Dry-run empty distinguished from filtered-empty. **Refused:** the wire never lands and the log carries the sentence naming the rule. Published version is locked with an explicit unlock and a stated reason.
**SELECTIONS.** None.
**EMPTY-INSTALL.** The tree lists registered datasets only; with none it says so and links to B2.
**A11Y + RTL.** Full keyboard node placement and wiring; the palette is a listbox, the tree a treegrid. Every layout property is logical; no name encodes a side.

### C2 Mapping Health - `/mapping-health`

**AIM.** Show whether the authored model still matches the sources.
**ROLES.** Data Engineer and Engineer read; Data Engineer acts on drift.
**LAYOUT.** Header. Block-start: summary tiles (coverage, unmapped columns, orphan rate, drift events). Then a per-mapping table. Row expansion shows column-level coverage.
**CONTROLS.** Time range select; Re-validate (secondary); Open in Studio (link per row); Acknowledge drift (secondary).
**HOOKS.** `useApiResource`, `useStandardToast`.
**CALLS.** `getSchemaMappingWorkbench()`; `GET /api/mapping-health/summary`; `GET /api/mapping-health/mappings/{id}`.
**STATES.** Drift present renders Amber with the changed column named and the date first seen. Zero drift renders a designed all-clear, not an empty table.
**SELECTIONS.** None.
**EMPTY-INSTALL.** "No mappings authored yet", linking to C1.
**A11Y + RTL.** Tiles are labelled figures; drift severity is text plus colour.

### C3 Data Quality - `/data-quality`

**AIM.** Turn "the data is bad" into a named, countable list.
**ROLES.** Engineer reads; Data Engineer acts.
**LAYOUT.** Header. Filter bar: class, source, severity, time. Then the issue table grouped by class. Row expansion shows example rows.
**CONTROLS.** Class / Source / Severity filters; Run scan (primary); Export (secondary); Open the affected dataset (link).
**HOOKS.** `useApiResource`, `useLatestOnlyPolling` during a scan, `useStandardToast`.
**CALLS.** `getDataQualityDashboard()` -> `GET /api/data-quality`; `POST /api/data-quality/scan`.
**STATES.** Zero issues renders "No data-quality issues detected in this window", with the window stated. Filtered-empty tells the user which filter to relax.
**SELECTIONS.** None.
**EMPTY-INSTALL.** "No data imported yet."
**A11Y + RTL.** Grouped rows use row-group semantics.

### C4 Plant Model Explorer - `/plant-model`

**AIM.** Confirm the product's structural picture of the plant matches reality.
**ROLES.** All read; Administrator corrects identity in F7.
**LAYOUT.** Header. Inline-start: hierarchy tree - site, area, equipment. Inline-end: detail panel for the selected node, plus routes and operations that reference it.
**CONTROLS.** Tree expanders; search; Show unmapped only (toggle); Open in Genealogy (link where a unit is selected).
**HOOKS.** `useApiResource`.
**CALLS.** `getPlantLayout()` -> `GET /api/plant/layout`; `GET /api/plant/equipment/{id}`.
**STATES.** Unmapped equipment renders Amber with the source object that referenced it. Empty renders "The plant model is built from imported data. Nothing has been projected yet."
**SELECTIONS.** None.
**EMPTY-INSTALL.** Empty tree with the sentence above.
**A11Y + RTL.** Treegrid with keyboard traversal; the detail panel is a labelled region updated on selection.

### C5 Genealogy Explorer - `/materials/{id}`

**AIM.** Walk a unit's ancestry and descendants on the customer's own keys, and read the thread that surrounds it.
**ROLES.** All read.
**LAYOUT.** Header with the unit's own code as the title. Block-start: identity and provenance strip (source system, source record, import batch). Then the genealogy graph, backward and forward. Then the time-aligned thread: parameters, process events, quality events, downtime.
**CONTROLS.**

| Control | Type | Token | Position | Enabled when |
|---|---|---|---|---|
| Search unit | `StandardInput` | Industrial Blue | header inline-end | always |
| Direction toggle back / forward / both | segmented | Electric Cyan active | graph header | graph loaded |
| Depth | stepper | Muted Steel | graph header | graph loaded |
| Edge weight badge | badge | Near-White on Industrial Blue | each edge | weight present |
| Drill to source rows | link per node | Electric Cyan | node detail | provenance present |
| Open in Workspace | secondary | Corporate Blue | header inline-end | always |

**HOOKS.** `useApiResource` for the unit and the graph separately.
**CALLS.** `getMaterialInvestigation(id)` -> `GET /api/plant/materials/{id}`; `GET /api/plant/materials/{id}/genealogy?direction=&depth=`; `GET /api/plant/materials/{id}/thread`; `searchDashboardMaterials(q)`.
**STATES.** Not found renders a designed state, never a crash. A unit with no edges states "No genealogy edges were declared for this unit" and names the mapping that would add them. **Where a unit spans two parents the weights are shown and they sum to 1.0**; a set that does not sum is a data defect and is flagged Hot Red rather than rendered silently.
**SELECTIONS.** None on this page; Open in Workspace carries the unit as a selection.
**EMPTY-INSTALL.** Search states that no material units exist yet.
**A11Y + RTL.** The graph has an equivalent nested-list rendering for screen readers; the thread is a time-ordered table.

---

## Group D - See and analyse

### D1 Interactive Workspace - `/workspace/:dashboardCode`

**AIM.** See the whole plant on one surface and narrow it by clicking anything.
**ROLES.** All read within page permissions; Engineer and above may edit layout and widgets.
**LAYOUT.** Block-start: page header with the sheet selector. Then the **always-present selections bar**, reading "No selections applied" when empty. Then the associative strip. Then the twelve-column responsive grid. Inline-start, collapsible: the sheet navigator with layout thumbnails.
**CONTROLS.**

| Control | Type | Token | Position | Enabled when |
|---|---|---|---|---|
| Sheet selector | `StandardSelect` | Industrial Blue | header | more than one sheet |
| Filter bar field | dropdown per field | Industrial Blue | filter bar | field is filterable |
| Clear all | secondary | Corporate Blue | selections bar, inline-end | a selection exists |
| Selection chip | removable chip | Cyan Green selected, Muted Steel excluded | selections bar | per selection |
| Card hover tools: maximise, collapse, export, clone, remove, edit | icon buttons | Muted Steel, hover Electric Cyan | card header, inline-end | per permission |
| Chart-type switcher | select on card | Industrial Blue | card header | server registry accepts the type for this binding |
| Save layout / Reset layout | primary / secondary | Electric Blue / Corporate Blue | header inline-end | layout dirty |
| Edit mode toggle | segmented | Electric Cyan active | header | edit permission |

**Associative tri-state:** selected renders Cyan Green, possible renders Near-White, excluded renders struck Muted Steel and **remains clickable**, because clicking an excluded value pivots the selection to it.
**HOOKS.** `useDashboardFilters` and `useDashboardSelection` implement publish and subscribe: widgets publish selections, all widgets subscribe to the merged filter set. `useAssociative` owns selected, possible and excluded computation. `useDashboardGridLayout` and `useDashboardLayoutPersistence` own drag, resize with live neighbour displacement, and persistence to `layout_json`. `useLatestOnlyPolling` prevents a slow response overwriting a newer one. `useEntitlements` hides tier-locked widget kinds.
**CALLS.** Mount: `getDashboardWorkspace(req)` -> `POST /analytics/dashboard/workspace`. Per widget: `queryDashboardWidget(req)` -> `POST /analytics/dashboard/widgets/query`. Actions: `updateDashboardLayout(id, layout)` -> `PATCH .../definitions/{id}/layout`; `updateDashboardWidgetLayout` -> `PATCH .../widgets/{wid}/layout`; `cloneDashboardWidgetDefinition` -> `POST .../widgets/{wid}/clone`; `deactivateDashboardWidgetDefinition` -> `DELETE .../widgets/{wid}`.
**BINDING RULE.** Data access is **exclusively** through the widget-query contract. No page-private fetch for an analytics visual, enforced by an architecture test.
**FILTER COMPOSITION RULE.** A widget's saved filter is that widget's **permanent scope**. The page filter bar and any associative click apply **on top of it**, narrowing further inside that scope, combined with AND. Leave the saved filter empty and the widget follows the page alone. The two compose; they never compete. This sentence appears in the authoring panel's own hint text so a user reads it where the choice is made.
**STATES.** **Widget-level isolation:** one widget's endpoint failing shows an error inside that card only and the page stays interactive. Filtered-to-empty is distinguished from genuinely empty and names the filter to relax. A widget whose dimension is outside the safety registry degrades honestly rather than breaking.
**SELECTIONS.** Full participation. Every selection is visible in the breadcrumb and individually removable.
**EMPTY-INSTALL.** "This page has no widgets yet", with Add widget inline for a permitted role.
**A11Y + RTL.** Grid is keyboard-reorderable; every chart has a table equivalent; selection state is announced as text. The grid uses logical columns and mirrors correctly.

### D2 Page Builder (S2) - `/page-builder`

**AIM.** Author pages, widgets and filters without code.
**ROLES.** Engineer and above; subject to the F3 quota.
**LAYOUT.** Header. Inline-start: the page list with Create page. Centre: the page preview with the grid. Inline-end: the authoring panel, which is the shared shell in S2 mode.
**CONTROLS.**

| Control | Type | Token | Position | Enabled when |
|---|---|---|---|---|
| Create page | primary | Electric Blue | inline-start header | quota not exhausted |
| Add widget | primary | Electric Blue | preview header | page selected, quota available |
| Kind picker: chart, table, KPI, calculated label, calendar filter, filter | tile picker | Electric Cyan on select | modal step 1 | Add widget pressed |
| Widget name | `StandardInput` | Industrial Blue | modal step 2 | kind chosen |
| Binding mode: Catalogue / Query | segmented | Electric Cyan active | shell, block-start | always; Query needs Pro and role |
| Chart type / Dimension / Measure | `StandardSelect` | Industrial Blue | shell, catalogue mode | catalogue mode |
| Query editor | monospace editor, IBM Plex Mono | Panel Navy field | shell, query mode | query mode |
| Run test | secondary | Corporate Blue | shell footer | query non-empty |
| Result columns to axes | mapping rows | Industrial Blue | shell, after Run test | test returned columns |
| Saved filter | filter rows | Industrial Blue | shell | always |
| Preview | secondary | Corporate Blue | shell footer | binding valid |
| Save | primary | Electric Blue | shell footer, inline-end | binding valid |

**Catalogue mode is a simplified face of the same shell, not a second surface.** Switching Catalogue to Query carries the catalogue selection in as a starting query. **Dimension and measure hide themselves for a chart type whose `supportsDimension` or `supportsMeasure` is false**, and the form refuses only when neither a dimension nor a measure is chosen.
**HOOKS.** `useApiResource`, `useOptimisticSave`, `useInlineFormValidation`, `useDebugLog` for the query result and its refusals, `useEntitlements` for the Query mode gate, `useStandardToast`.
**CALLS.** `getDashboardMetadata()` -> `GET /analytics/dashboard/metadata` (dimensions, measures, chart types with their supports flags); `getDashboardReferenceData()`; `executeWidgetQueryExpression(req)` -> `POST /analytics/dashboard/widgets/execute`; `createDashboardDefinition`, `updateDashboardDefinition`, `deleteDashboardDefinition`; `createDashboardWidgetDefinition`, `updateDashboardWidgetDefinition`; `pageBuilder.create | update | delete | listMine | getBySlug`.
**STATES.** Query refused: the debug log names what was refused and echoes the offending fragment; the widget is not saved. Quota exhausted: the create action is disabled with the reason and the administrator named. Test returning zero rows is a **warning**, not an error, and says so.
**SELECTIONS.** The preview participates so the author sees the widget behave before saving.
**EMPTY-INSTALL.** "No pages yet. Create the first analysis page."
**A11Y + RTL.** The kind picker is a radiogroup; the editor announces its language; every step of the modal is reachable and escapable by keyboard.

### D3 Analysis Toolbox (S3) - `/analysis/toolbox`

**AIM.** Declare an analysis and run it under the gate.
**ROLES.** Engineer and above; ML methods require Pro Plus.
**LAYOUT.** Header. Inline-start: definition list. Centre: the three blocks - Outcome, Grain, Window - then the method block. Inline-end: the payload panel and the readiness panel.
**CONTROLS.**

| Control | Type | Token | Position | Enabled when |
|---|---|---|---|---|
| New definition | primary | Electric Blue | inline-start header | quota available |
| Outcome / Grain / Window blocks | selects fed from `definition-options` | Industrial Blue | centre | options loaded |
| Method | `StandardSelect` from the method registry | Industrial Blue | centre | outcome and grain chosen |
| Population filters | filter rows | Industrial Blue | centre | always |
| Parity line | badge | Cyan Green IDENTICAL | payload panel | payload built |
| Check readiness | secondary | Corporate Blue | readiness panel | outcome, grain, window chosen |
| Run governed analysis | primary | Electric Blue | footer inline-end | definition valid |
| Save definition | primary | Electric Blue | footer | definition valid |

**Every option comes from the registry.** The payload panel shows exactly what the engine will receive, and the parity line asserts the interface payload and the engine payload are identical.
**HOOKS.** `useApiResource`, `useInlineFormValidation`, `useOptimisticSave`, `useDebugLog`, `useEntitlements`, `useStandardToast`.
**CALLS.** `getAnalysisJobDefinitionOptions()` -> `GET /api/analysis-jobs/definition-options`; `listAnalysisJobDefinitions()`; `getAnalysisJobDefinition(code)`; `createAnalysisJobDefinition`; `updateAnalysisJobDefinition`; `runAnalysisJobDefinition(code)` -> `POST /api/analysis-jobs/{code}/run`; `getAnalysisJobDefinitionResults(code)`; `getAnalysisReadinessGates()` -> `GET /api/readiness/evaluate`.
**STATES.** **Blocked is a first-class state**, rendered Hot Red with the failing dimension, its measured value and its threshold, plus the sentence that the engine will compute when the dimension is satisfied. Refused: an unregistered outcome is rejected naming it, never a silent empty result.
**SELECTIONS.** None.
**EMPTY-INSTALL.** "No outcomes registered yet. Outcomes appear once data is projected."
**A11Y + RTL.** The three blocks are fieldsets in reading order; the readiness panel is a status region announced on change.

### D4 Findings - `/correlations`

**AIM.** Present what was found, ranked so the important thing is first, with everything needed to disbelieve it.
**ROLES.** All read within permission; Engineer drills through.
**LAYOUT.** Header. Filter bar: outcome, window, method, significance. Then the findings table. Row click opens the evidence drawer.
**CONTROLS.**

| Control | Type | Token | Position | Enabled when |
|---|---|---|---|---|
| Outcome / Window / Method / Significance filters | `StandardSelect` | Industrial Blue | filter bar | always |
| Sortable column headers | `SortableDataTable` | Near-White | table head | always |
| Evidence drawer | drawer | Panel Navy | inline-end on row click | row selected |
| Drill to population | link | Electric Cyan | drawer | population recorded |
| Export | secondary | Corporate Blue | header inline-end | rows present |
| Add to page | secondary | Corporate Blue | drawer footer | authoring permission |

Columns: Feature, Outcome, Method, Effect size, q-value, Sample size, Stability, Stratum survival, Population, Run.
**Ranking is by effect size**, with the p-value only as a tie-breaker. **The default sort is not user-defeatable into p-value order**, because ranking by p-value is prohibited by Chapter 1.5.8.
**HOOKS.** `useApiResource`, `useDashboardFilters` (shares the page filter vocabulary).
**CALLS.** `GET /api/findings?outcome=&window=`; `GET /api/findings/{id}/evidence`; `getCorrelationRuns()`; `getGenealogyAwareCorrelation()`.
**STATES.** **A non-significant result renders as a first-class honest answer**, labelled not significant, never hidden. Zero findings after a completed run states that the run completed and found nothing at this threshold, which is different from no run having happened.
**SELECTIONS.** Filters compose with the workspace vocabulary so a finding can be carried into a page.
**EMPTY-INSTALL.** "No analysis has run yet", linking to D3.
**A11Y + RTL.** Sort state is announced; the drawer traps focus and returns it on close.

### D5 Risk Dashboard - `/risk`

**AIM.** Show predictive risk with its drivers and its horizon.
**ROLES.** Pro Plus; Engineer and above.
**LAYOUT.** Header. Block-start: distribution summary. Then the scored-unit table. Inline-end drawer: drivers for the selected unit.
**CONTROLS.** Horizon select; risk-class filter; Recalculate (secondary); Open in Genealogy (link); Export.
**HOOKS.** `useApiResource`, `useEntitlements`, `useLatestOnlyPolling` during recalculation.
**CALLS.** `getRiskDashboard()` -> `GET /api/risk`; `calculateRiskScore(id)`, `calculateRiskScoresBatch(req)`; `getMaterialFeatureVector(id)`.
**STATES.** Below the gate: the page shows the gate state and the readiness meter instead of scores, and says which dimension blocks. **It never shows a score computed on insufficient data.**
**SELECTIONS.** Carries a unit into Genealogy Explorer.
**EMPTY-INSTALL.** Tier-locked for Light and Pro, stated as a capability of Pro Plus rather than as an error.
**A11Y + RTL.** Risk class is text plus colour; the driver drawer is a labelled region.

### D6 Suggestions - `/suggestions`

**AIM.** Turn a finding into an action, and record what happened.
**ROLES.** Pro Plus; Engineer decides; Viewer reads.
**LAYOUT.** Header. Suggestion cards in a single column, each with its evidence references, its proposed action, and its decision controls. Inline-end: outcome tracking.
**CONTROLS.** Generate suggestions (primary); Accept / Reject / Defer (per card, Accept primary, Reject Hot Red); Comment (`StandardTextArea`); Open the evidence (link per reference).
**HOOKS.** `useApiResource`, `useCustomerSafeAction` (a decision is confirmed), `useStandardToast`.
**CALLS.** `generateSuggestions()`; `decideSuggestion(id, decision)`; `getSuggestionHealth()`.
**STATES.** Empty: "No suggestions yet. Suggestions are generated from findings." Every card carries its evidence handles; **a suggestion without a resolvable handle is not rendered.**
**SELECTIONS.** None.
**EMPTY-INSTALL.** Tier-locked, stated as capability.
**A11Y + RTL.** Cards are articles; decision controls are a labelled group per card.

### D7 Value Dashboard - `/value`

**AIM.** State the euro consequence with every input traceable.
**ROLES.** Pro Plus; Engineer and commercial roles read; Administrator edits assumptions.
**LAYOUT.** Header. Block-start: the impact range cards. Then the input table with drill-through. Block-end: the scenario comparison.
**CONTROLS.** Period select; Edit cost assumptions (secondary, Administrator only); Drill to rows (link per input); Compare scenario (secondary); Export monthly report (primary).
**HOOKS.** `useApiResource`, `useOptimisticSave` for assumptions, `useStandardToast`.
**CALLS.** `GET /api/value/impacts?finding=`; `PUT /api/value/assumptions`; `computeValueImpact()`, `saveCostAssumptions()`, `computePayback()`, `buildMonthlyValueReportHtml()`.
**STATES.** **Insufficient basis is an explicit rendered state**, naming which cost input is missing and linking to where it is set. A range is always a range; **no single point figure is ever rendered as the answer.**
**SELECTIONS.** None.
**EMPTY-INSTALL.** Tier-locked, and states that cost assumptions must be entered before any figure can be produced.
**A11Y + RTL.** Ranges are announced as from-to; drill-through links state their target.

### D8 ML Readiness and Models - `/ml-readiness`

**AIM.** Answer, per outcome and grain, whether machine learning is possible here yet, and manage what has been trained.
**ROLES.** Pro Plus; Engineer reads; Data Engineer trains.
**LAYOUT.** Header. Block-start: readiness matrix, outcome down, dimension across. Then the model registry table. Inline-end: label preview for the selected outcome.
**CONTROLS.** Outcome select; Ensure jobs (secondary); Train (primary, per model row); Retire model (Hot Red); Label preview refresh.
**HOOKS.** `useApiResource`, `useEntitlements`, `useCustomerSafeAction` for Retire, `useLatestOnlyPolling` during training.
**CALLS.** `mlReadiness.getWorkspace()`, `getScore()`, `getJobs()`, `ensureJobs()`, `getLabelPreview()`; `evaluateMlLifecycle()`; `getMlLifecycle()`.
**STATES.** Every cell carries its measured value and threshold. **A blocked outcome is the normal case on a young installation and is presented as a countdown, not a fault.**
**SELECTIONS.** None.
**EMPTY-INSTALL.** Tier-locked; states the readiness prerequisites.
**A11Y + RTL.** The matrix is a table with headers; state is text plus colour.

---


### D9 Early Warning - `/early-warning`

**AIM.** See which units, mid-process now, are predicted to fail downstream, why, and what the plant's own history says can still be done.
**ROLES.** Pro Plus. Engineer and Operator read; Engineer acts on remediation cards.
**LAYOUT.** Header with horizon select. Block-start: summary strip (units at risk, by defect class, by stage). Then the risk queue table, ranked by risk score then time-to-stage. Inline-end drawer: the selected unit's drivers and its remediation card.
**CONTROLS.**

| Control | Type | Token | Position | Enabled when |
|---|---|---|---|---|
| Horizon | `StandardSelect` | Industrial Blue | header | always |
| Defect class filter | `StandardSelect` | Industrial Blue | header | classes exist |
| Queue row | selectable row | Near-White; risk class chip Cyan Green / Amber / Hot Red | table | always |
| Driver bar | horizontal bar per driver | Electric Cyan | drawer | unit selected |
| Remediation card | card with practice, support count, expected effect | Panel Navy, Cyan Green accent | drawer | history support >= 20 cases |
| Acknowledge / Assign | secondary | Corporate Blue | drawer footer | engineer role |
| Open in Genealogy | link | Electric Cyan | drawer | always |

**HOOKS.** `useApiResource`, `useEntitlements`, `useLatestOnlyPolling`, `useStandardToast`.
**CALLS.** `GET /api/predictions/queue?horizon=&defect=`; `GET /api/predictions/{unitId}/drivers`; `GET /api/predictions/{unitId}/remediations`; `POST /api/predictions/{id}/acknowledge`.
**STATES.** Below the gate: the readiness meter and the blocking dimension, never a score. A unit whose remediation has insufficient historical support shows "insufficient support (<n> of 20 cases)" instead of a card - reported honestly, never padded. Filtered-empty distinguished.
**SELECTIONS.** Carries a unit into Genealogy Explorer and the Workspace.
**EMPTY-INSTALL.** Tier-locked below Pro Plus; above it, the gate state until readiness.
**A11Y + RTL.** Risk class is text plus colour; the drawer traps focus; driver bars carry value text.

### D10 Practice Insights - `/practice-insights`

**AIM.** See the plant's own best demonstrated practices, the practices that preceded failures, and where current operation drifts from its own best.
**ROLES.** Pro Plus. Engineer and Plant Manager read.
**LAYOUT.** Header with outcome select (productivity, downtime, defect class). Block-start: benchmark cards - best demonstrated practice per context, with support count and outcome rate. Then the comparison table: practice against outcome, with confidence. Block-end: the drift panel - current operation against own best, per parameter.
**CONTROLS.** Outcome select; context select (grade family, route); practice row expander showing the parameter combination; Evidence link per practice; Export; drift period select.
**HOOKS.** `useApiResource`, `useEntitlements`, `useStandardToast`.
**CALLS.** `GET /api/practices?outcome=&context=`; `GET /api/practices/{id}/evidence`; `GET /api/practices/drift?period=`.
**STATES.** Every benchmark carries its support count; a practice below the support threshold is shown as observed-but-unproven, never as a benchmark. Below the gate: the gate state. Drift with no current data states so.
**SELECTIONS.** A practice's population can be opened in the Workspace.
**EMPTY-INSTALL.** Tier-locked; above it, the gate state until enough history exists.
**A11Y + RTL.** Cards are articles; the drift panel announces direction as text.

## Group E - Ask and operate

### E1 Assistant - the persistent dock (on every page)

**AIM.** Reach an answer by asking, from any page, with every number cited.
**FORM.** Not a page. A launcher, 56 px, anchored inline-end block-end on every authenticated page, offset 24 px. Expanded: a 400 x 600 px panel on the same corner; dockable to 640 px with page reflow; full-screen on demand; a full-height sheet on mobile. State (collapsed, width, last conversation) is per user and persists across navigation. Escape collapses; a keyboard shortcut opens and focuses the composer; the dock mirrors to the other inline edge in a right-to-left locale.
**ROLES.** Pro Plus; retrieval permission-scoped per role. Below Pro Plus the dock is absent, not broken.
**CONTEXT.** On open, the client sends the route, the page definition code, the current associative selection, the visible window and the selected entity. Context narrows retrieval; it never widens permission. Context-aware starters per page ("This page is filtered to <selection>. Ask about what you are seeing.").
**CONTROLS.** Composer (auto-growing, Enter sends, Shift-Enter newline); Send (Electric Blue); citation chips (Electric Cyan) under each answer; evidence strip sliding from block-end with Open in page; expand-width, full-screen, close; three registry-derived suggested questions on an empty conversation.
**HOOKS.** `useApiResource` per message, `useEntitlements`, `useStandardToast`.
**CALLS.** `askAssistant(req)` -> `POST /api/assistant/ask` with the context envelope; `POST /api/assistant/reindex` (admin).
**STATES.** Thinking streams the tool in use ("Reading findings..."). **Refusal is amber and evidential** ("I don't have evidence for that", plus what would answer it). **Transport failure is red** ("Request failed", Retry) - never dressed as an abstention. Out of scope, index empty (admins get Reindex), tier locked (absent).
**SELECTIONS.** Reads the page's selection as context; never mutates it.
**EMPTY-INSTALL.** Present, with starters pointing at commissioning.
**A11Y + RTL.** The conversation is a polite log region; chips are links with accessible names; position is expressed as inline-end, never "right".

### E2 Assistant Configuration - `/assistant-config`

**AIM.** Configure every aspect of the assistant from the interface.
**ROLES.** Administrator.
**LAYOUT.** Header. Grouped form: Grounding, Tools, Knowledge sources, Glossary, Guardrails, Serving.
**CONTROLS.** Grounding policy select (`strict-citations-required`); Evidence policy select (`citations-and-provenance-required`); Max citations stepper; Tools matrix (tool against role and tier); Knowledge sources checklist; Glossary term and synonym rows; Guardrail phrase rows; Serving mode select (self-hosted, private endpoint, customer model) with the no-egress indicator; Save (primary); Reset (secondary, confirmed); Reindex (secondary).
**HOOKS.** `useApiResource`, `useOptimisticSave`, `useCustomerSafeAction` for Reset, `useStandardToast`.
**CALLS.** `getAssistantConfig()`, `saveAssistantConfig(cfg)`, `resetAssistantConfig()`, `POST /api/assistant/reindex`.
**STATES.** Saving a policy that would weaken grounding is **refused with the reason**, because the grounding contract is not a preference. Reindex reports chunk counts per family.
**SELECTIONS.** None.
**EMPTY-INSTALL.** Defaults present; glossary empty with the sentence that plant terms improve retrieval.
**A11Y + RTL.** Groups are fieldsets; the tools matrix is a table of labelled checkboxes.

### E3 Plant Data Log (S5) - `/data-integration/alerting`

**AIM.** Author rules that raise entries against imported observations, and read what they raised.
**ROLES.** Engineer authors; Operator acknowledges; all read.
**LAYOUT.** Layout header, title "Plant Data Log", subtitle naming the evaluator that scans imported observations, and header button **Run evaluation**. Block-start: the rule form card. Then the rules table. Then the log table.
**CONTROLS.**

| Control | Type | Token | Position | Enabled when |
|---|---|---|---|---|
| Run evaluation | primary | Electric Blue | header inline-end | not already running |
| Rule name | `StandardInput`, placeholder "Superheat high" | Industrial Blue | rule form | always |
| Parameter code | `StandardInput`, placeholder "SUPERHEAT_C" | Industrial Blue | rule form | always |
| Comparator | `StandardSelect`, exactly `>` `>=` `<` `<=` `=` | Industrial Blue | rule form | always |
| Limit | `StandardInput` numeric, placeholder "36" | Industrial Blue | rule form | always |
| Severity | `StandardSelect` Info / Warning / Critical | Industrial Blue | rule form | always |
| Add rule | primary | Electric Blue | rule form footer | validation passes |
| Deactivate rule | secondary | Corporate Blue | rules row | rule active |
| Acknowledge | secondary | Corporate Blue | log row | entry unacknowledged |

Rules table columns: Name, Parameter, Condition, Severity. Log table columns: Time, Rule, Material, Parameter, Value, Condition, Severity.
**HOOKS.** `useApiResource` for rules and log separately, `useInlineFormValidation`, `useOptimisticSave`, `useStandardToast`.
**CALLS.** Mount, parallel: `listRules()` -> `GET /api/log/rules`; `listLog()` -> `GET /api/log/entries`. Actions: `createRule(req)` -> `POST /api/log/rules` returning `{ id, ruleName, parameterCode, comparator, limitValue, severity }`; `evaluateAlerts()` -> `POST /api/log/evaluate` returning `{ logged: N }`.
**STATES, verbatim.** Rules empty: "No rules yet. Add one above." Log empty: "No breaches logged yet. Create a rule and run evaluation." Validation: "Rule name and parameter code are required." and "Limit must be a number." After creation: "Rule created. Click 'Run evaluation' to scan observations." After evaluation: "Evaluation complete: N new log row(s)." Server-side, a comparator outside the set returns 400 "comparator must be one of > >= < <= =", never a 500, and the database enforces the same set with a check constraint.
**IDEMPOTENCE.** A second evaluation over the same data returns `{ logged: 0 }`. Zero double-logging is a demonstrable property.
**SELECTIONS.** None.
**EMPTY-INSTALL.** Both empty states, with the parameter-code field noting that codes appear after the first projection.
**A11Y + RTL.** The comparator select announces its symbol as a word; the log table is sortable by keyboard.

### E4 Supervisor - `/data-integration/supervisor`

**AIM.** Review the engine and propose governed improvements, changing nothing automatically.
**ROLES.** Administrator runs and approves; Engineer reads.
**LAYOUT.** Layout header, title "Engine Supervisor", subtitle containing "Read-only: it never changes a job automatically.", header button **Run review now**. Then report cards, newest first.
**CONTROLS.** Run review now (primary, disabled with a spinner while busy); per proposal: Dry-run (secondary), Approve (primary, confirmed), Reject (Hot Red); Open provenance (link).
**HOOKS.** `useApiResource`, `useCustomerSafeAction` for Approve, `useStandardToast`.
**CALLS.** `listSupervisorReports()` -> `GET /api/supervisor/reports`; `runSupervisor()` -> `POST /api/supervisor/run` returning `{ id, itemKey, title, body, findings, significant }`; `POST /api/supervisor/proposals/{id}/dry-run`; `POST /api/supervisor/proposals/{id}/approve`.
**STATES, verbatim.** Loading: "Loading reports...". Empty: "No supervisor reports yet. Click 'Run review now' to generate the first one." Card title: `Supervisor report <yyyy-MM-dd HH:mm> UTC`. Where no completed analysis run exists the body says so rather than inventing content.
**GUARDRAIL, demonstrable.** Results counts before and after a run are identical: it writes a report and a log entry and nothing else. Every threshold change is a separate provenance row naming who, from what, to what and why. **The honest-abstain machinery is outside its write scope by construction.**
**SELECTIONS.** None.
**EMPTY-INSTALL.** The empty state above.
**A11Y + RTL.** Cards are articles; the busy button announces its state.

### E5 Reports - `/reports`

**AIM.** Get a finding out of the product and into a meeting.
**ROLES.** Engineer and above generate; Viewer downloads.
**LAYOUT.** Header. Inline-start: report definition list. Centre: definition editor. Inline-end: generated-output history.
**CONTROLS.** New definition (primary); Sections picker; Period select; Recipients rows; Schedule (cron-like select, plain language); Generate now (primary); Download (link per output); Delivery target rows (email, webhook).
**HOOKS.** `useApiResource`, `useOptimisticSave`, `useLatestOnlyPolling` during generation, `useStandardToast`.
**CALLS.** `GET /api/reports`; `POST /api/reports`; `POST /api/reports/{id}/generate`; `GET /api/reports/{id}/outputs`.
**STATES.** Generation is a job and appears in Jobs Monitor. Output renders on the **light Report Surface** `#F4F6F8`, not the dark application theme, because it will be printed. A report whose sections are all empty refuses to generate and says which section had no data.
**SELECTIONS.** None.
**EMPTY-INSTALL.** "No report definitions yet."
**A11Y + RTL.** Schedule is expressed in words, not cron syntax; outputs list their period in the accessible name.

---

## Group F - Administer

### F1 Users and Roles - `/admin/users`

**AIM.** Manage accounts and permissions.
**ROLES.** Administrator only. **The page is not rendered for any other role**, not merely disabled.
**LAYOUT.** Header. Inline-start: account list with state. Centre: the selected account. Inline-end: the permission grid, surface down, action across.
**CONTROLS.** New user (primary); Role select; permission cells (inherited rendered Muted Steel, overridden rendered Electric Cyan); Reset overrides (secondary); Deactivate (Hot Red, confirmed); Force password reset (secondary); Require second factor (toggle).
**HOOKS.** `useApiResource`, `useOptimisticSave`, `useCustomerSafeAction` for Deactivate, `useStandardToast`.
**CALLS.** `GET /api/admin/users`; `POST /api/admin/users`; `PATCH /api/admin/users/{id}`; `GET /api/admin/users/roles`; `PATCH /api/admin/users/{id}/permissions`.
**STATES.** **Deactivate rather than delete is the default**, because an account referenced by an audit row must stay resolvable; the page states this where the action is. Every action writes an audit entry, and the page says so once.
**SELECTIONS.** None.
**EMPTY-INSTALL.** The vendor support account, marked undeletable with the reason, and the customer administrator.
**A11Y + RTL.** The permission grid is a table of labelled checkboxes; inherited versus overridden is text as well as colour.

### F2 Licence and Entitlement - `/admin/license`

**AIM.** Apply the licence and see consumption against the envelope.
**ROLES.** Administrator and commercial administrator.
**LAYOUT.** Header. Three regions: token, capability, meters.
**CONTROLS.** Paste token (`StandardTextArea`); Apply (primary); Verify (secondary); capability list (read-only, next-tier additions shown Muted Steel); five meter bars each linking to the administration page that would relieve it.
**HOOKS.** `useLicense`, `useEntitlements`, `useApiResource`, `useStandardToast`.
**CALLS.** `POST /api/admin/license/activate`; `GET /api/admin/license`; `GET /api/admin/license/meters`; `getLicenseStatus()`, `getLicensePlans()`, `verifyOfflineEnvelope()`, `evaluateLifecycle()`.
**STATES.** A tampered token is refused with the signature failure named. Approaching expiry renders a counting banner. After expiry the page states that existing dashboards remain readable and **no data has been destroyed**. Exceeding a meter states that work is throttled, not stopped, and what is queued.
**SELECTIONS.** None.
**EMPTY-INSTALL.** The install token with its tier.
**A11Y + RTL.** Meters are progress elements with measured text values, never colour alone.

### F3 Authoring Quota and Limits - `/admin/quota`

**AIM.** Divide the tier's total capacity between roles and users.
**ROLES.** Administrator.
**LAYOUT.** Header. Block-start: the matrix, creatable object type down, role across, each cell a limit plus current consumption. Then per-user overrides. Block-end: top consumers by object type.
**CONTROLS.** Cell limit editor; Add override (secondary); Remove override; Save (primary); Reset role default (secondary, confirmed).
**HOOKS.** `useApiResource`, `useOptimisticSave`, `useCustomerSafeAction`, `useStandardToast`.
**CALLS.** `GET /api/admin/quota`; `PUT /api/admin/quota/roles/{role}`; `PUT /api/admin/quota/users/{id}`.
**STATES.** A cell at or above eighty percent renders Amber; at one hundred percent Hot Red with the count. **A limit is soft by default:** the authoring surface warns at eighty and disables create at one hundred, naming the administrator; **never a silent failure and never a lost draft.**
**SELECTIONS.** None.
**EMPTY-INSTALL.** Role defaults from the tier, no overrides.
**A11Y + RTL.** Matrix cells are labelled inputs stating object type and role in their accessible name.

### F4 Jobs Administration - `/admin/jobs`

**AIM.** Decide what runs, when, in which pool, at what weight.
**ROLES.** Administrator and Data Engineer.
**LAYOUT.** Header. Block-start: pool summary, configured parallelism beside current utilisation. Then the job definition table. Row expansion is the definition editor.
**CONTROLS.** New definition (primary); Schedule editor; Pool select (import, analysis, ML, report) - **a separate confirmed action**; Compute weight stepper - **also confirmed**; Enable / Disable (secondary); Delete (Hot Red, confirmed).
**HOOKS.** `useApiResource`, `useOptimisticSave`, `useCustomerSafeAction` for pool, weight and delete, `useStandardToast`.
**CALLS.** `GET /api/jobs/definitions`; `POST /api/jobs/definitions`; `PATCH /api/jobs/definitions/{id}`; `GET /api/jobs/pools`.
**STATES.** Changing a pool or a weight is confirmed **because it changes what the executor will admit concurrently**, and the confirmation states the new utilisation. A disabled definition states who disabled it and when.
**SELECTIONS.** None.
**EMPTY-INSTALL.** Only the premade Supervisor weekly definition.
**A11Y + RTL.** Pool utilisation is text plus bar; confirmations are dialogs with focus trapping.

### F5 Logging and Audit - `/admin/logs`

**AIM.** Read the four layers and export what was read.
**ROLES.** Administrator and auditor; operator reads system and job.
**LAYOUT.** Header. Four `StandardTabs`: System, Job, Data, Audit. Shared filter bar. Then the entry table; rows expand to the full context payload.
**CONTROLS.** Time range, severity, actor, job family filters; Pin filter (secondary); Export (secondary); row expander. **The Audit tab has no edit and no delete control anywhere on it.**
**HOOKS.** `useApiResource`, `useLatestOnlyPolling`, `useStandardToast`.
**CALLS.** `GET /api/log/entries?layer=&severity=&actor=&from=&to=`; `GET /api/admin/audit`; `POST /api/log/export`.
**STATES.** Filtered-empty names the filter to relax. **A refusal is logged like a result**, so the job layer answers "why not" as readily as "what". Export produces a file in the light Report Surface style with the filter stated in its header, so an exported log is self-describing.
**SELECTIONS.** None.
**EMPTY-INSTALL.** System and audit populated by the install itself; job and data empty with the reason.
**A11Y + RTL.** Tabs are a tablist; the audit tab announces that it is read-only.

### F6 Log Channel Configuration - `/admin/log-channels`

**AIM.** Define a new log channel without a code change.
**ROLES.** Administrator.
**LAYOUT.** Header. Channel list with, per channel, name, severity mapping, retention, export target, reading roles. Inline-end: the channel editor with a live preview of an entry.
**CONTROLS.** New channel (primary); Name; Severity mapping rows; Retention (days); Export target select (none, file, syslog, webhook) with its address field; Reading roles multi-select; Save (primary); Disable (secondary).
**HOOKS.** `useApiResource`, `useOptimisticSave`, `useInlineFormValidation`, `useStandardToast`.
**CALLS.** `GET /api/admin/log-channels`; `POST /api/admin/log-channels`; `PATCH /api/admin/log-channels/{id}`.
**STATES.** Built-in channels are visible but locked, **with the lock explained in a sentence rather than merely rendered**. The audit channel cannot be created, edited or targeted from here at all, and the page states that rather than leaving it to be discovered. The preview shows what an entry will look like before saving.
**SELECTIONS.** None.
**EMPTY-INSTALL.** The four built-in channels, locked.
**A11Y + RTL.** The editor is a fieldset group; the preview is a labelled region.

### F7 System Settings - `/admin/settings`

**AIM.** Hold what is true of this installation.
**ROLES.** Administrator.
**LAYOUT.** Header. Five groups, each stating the consequence of its settings in one line: Identity, Units and formats, Time, Retention, Data boundary.
**CONTROLS.** Site name and code; units selects; **plant time zone select - confirmed**; date and number format selects with a live example; retention per stage; per-tenant no-egress toggle; Save (primary).
**HOOKS.** `useApiResource`, `useOptimisticSave`, `useCustomerSafeAction` for the time zone and the egress toggle, `useStandardToast`.
**CALLS.** `GET /api/admin/system`; `PUT /api/admin/system`.
**STATES.** Changing the plant time zone is confirmed because **it re-frames shift analysis**; the confirmation says so. Toggling no-egress names which capabilities change behaviour as a result. Date and number formats are stated explicitly and **never inherited from the machine locale**.
**SELECTIONS.** None.
**EMPTY-INSTALL.** Site identity from the licence; everything else at documented defaults.
**A11Y + RTL.** Each group is a fieldset with a description; format examples update as live regions.

### F8 Translation and Language - `/admin/translation`

**AIM.** Manage language packs and approve labels per language.
**ROLES.** Administrator and translator.
**LAYOUT.** Header with per-language completion bars. Filter bar: state (untranslated, translated-unreviewed, verified-in-mirror). Then the label table, label down, language across. Inline-end: the context panel for the selected label.
**CONTROLS.** Language add; per-cell text editor; Mark reviewed (secondary); Mark verified in mirror (secondary); Export pack (secondary); Import pack (secondary, confirmed); Fallback language select.
**HOOKS.** `useV5I18n`, `useApiResource`, `useOptimisticSave`, `useCustomerSafeAction` for Import, `useStandardToast`.
**CALLS.** `GET /api/admin/system/translations`; `PUT /api/admin/system/translations`; `POST /api/admin/system/translations/import`; `GET .../export`.
**STATES.** A label translated but not verified in a mirrored layout renders Amber. **The context panel shows where the label appears in the product**, because a translator who cannot see the context will translate a button as a noun.
**SELECTIONS.** None.
**EMPTY-INSTALL.** The shipped languages at full completion; others absent.
**A11Y + RTL.** The table is navigable by keyboard; the context panel is a labelled region; Arabic rows render right-to-left in place.

---



---

## 4.5 Database schemas, tables, keys and joins

### 4.5.1 Universal conventions

Applied to every table unless the table's own entry overrides it.

| Column | Type | Rule |
|---|---|---|
| `id` | `uuid` | **PRIMARY KEY**, `DEFAULT gen_random_uuid()` |
| `tenant_id` | `uuid` | `NOT NULL`. Row-level security predicate column (4.5.8) |
| `created_at_utc` | `timestamptz` | `NOT NULL` |
| `updated_at_utc` | `timestamptz` | `NULL` |
| `is_deleted` | `boolean` | `NOT NULL DEFAULT false` |
| `deleted_at_utc` | `timestamptz` | `NULL` |
| `deleted_reason` | `varchar(500)` | `NULL` |
| `is_synthetic` | `boolean` | `NOT NULL DEFAULT false`. Separates emulated from production data |

**The provenance triple**, on every table that holds imported data:

| Column | Type |
|---|---|
| `source_system` | `varchar(100)` |
| `source_record_id` | `varchar(100)` |
| `import_batch_id` | `uuid` |

**Four standing rules.**

1. **Surrogate keys are internal; business keys are the customer's.** Every join in analysis resolves to a `uuid`, never to a source string. Business keys carry a unique index so projection is idempotent, but they are never the join target.
2. **Soft delete, never hard delete, on any row an audit entry may reference.** Cascading deletes never cross a provenance boundary.
3. **Re-projection supersedes; it does not destroy.** A second run over the same batch updates in place under the filtered unique index of 4.5.3.
4. **Time is stored twice where a shift matters:** universal, local, and the zone identifier. A single universal column silently corrupts shift attribution twice a year at the daylight-saving boundary.

### 4.5.2 Schema topology

| Schema | Holds | Day one | Written by |
|---|---|---|---|
| `ppiq_staging` | Source-shaped copies, envelopes, watermarks | Empty | The import pipeline only |
| `ppiq_plant` | The canonical model and the analytical results area | **Empty, provably, one query** | The projector and the engines only |
| `ppiq_meta` | Product configuration | Prefilled under a declared per-table contract, from versioned scripts, each row past the genericity lint | The application |
| `public` | Platform infrastructure only: extensions, migration history | - | The platform |

**The classification test, one question: whose knowledge is this row?** The customer's plant reality goes to `ppiq_plant`. The product's configuration goes to `ppiq_meta`. An uninterpreted copy of what a source sent goes to `ppiq_staging`. A row that seems to belong to two is two tables.

**Isolation as topology.** No surface reads `ppiq_staging`. Administration surfaces read `ppiq_meta` and never plant rows. Analytical surfaces read `ppiq_plant` only.

**Transitional note.** The governing data-flow contract operates staging under the physical name `dump_store`, with the schema name held in one configuration key (`Prep:StagingSchema`) precisely so the rename to `ppiq_staging` is one configuration-and-migration change. Emulator source shapes live in `src_*_shape` schemas which belong to the emulated factory, not to the product. This section specifies the target; the Implementation Status Register carries the distance and the migration script.

### 4.5.3 `ppiq_staging` - transit

**`import_batches`** - one row per push; the unit of lineage and retry.

| Column | Type | Notes |
|---|---|---|
| `id` | uuid | PK |
| `source_dataset_definition_id` | uuid | FK -> `ppiq_meta.source_dataset_definitions(id)` ON DELETE RESTRICT |
| `source_object_name` | varchar(200) | NOT NULL |
| `source_system` | varchar(100) | NOT NULL |
| `status` | varchar(50) | NOT NULL. `Pending | Running | Completed | Failed | Cancelled` |
| `started_at_utc`, `finished_at_utc` | timestamptz | |
| `row_count` | integer | NOT NULL DEFAULT 0 |
| `watermark_from`, `watermark_to` | text | The cursor range actually read |
| `checksum` | varchar(128) | Of the read payload |
| `failure_reason` | varchar(4000) | NULL. **A failure is stored, not only logged** |

Indexes: `(source_dataset_definition_id, started_at_utc DESC)`; `(status)`; `(started_at_utc DESC)`.

**`staging_records`** - one row per source row, verbatim.

| Column | Type | Notes |
|---|---|---|
| `id` | uuid | PK |
| `import_batch_id` | uuid | **FK -> `import_batches(id)` ON DELETE RESTRICT** |
| `source_object_name` | varchar(200) | NOT NULL |
| `row_number` | integer | NOT NULL |
| `raw_json` | **jsonb** | NOT NULL. The source row, uninterpreted |
| `is_processed` | boolean | NOT NULL DEFAULT false |
| `processed_at_utc` | timestamptz | NULL |
| `processing_status` | varchar(50) | NOT NULL DEFAULT `'Pending'` |
| `processing_error` | varchar(4000) | NULL |
| `canonical_entity_id` | uuid | NULL. What this row became |
| `canonical_entity_name` | varchar(200) | NULL |

Indexes: `(import_batch_id, row_number)` unique; `(processing_status)` partial `WHERE is_processed = false`; GIN on `raw_json`.

*`ON DELETE RESTRICT` is deliberate: a batch cannot be deleted while its rows exist, because that orphans the lineage every canonical row depends on. The partial index exists because the projector's hot query is "unprocessed rows in this batch", and a partial index on that predicate stays small no matter how large staging grows.*

**`cursor_watermarks`**: `source_dataset_definition_id` (FK, unique), `watermark_column`, `watermark_value text`, `watermark_type`, `last_advanced_at_utc`. One row per dataset; the freshness ground truth the gate reads.

**`schema_drift_events`**: `source_dataset_definition_id` (FK), `detected_at_utc`, `change_type` (`ColumnAdded | ColumnRemoved | TypeChanged | ObjectMissing`), `column_name`, `old_type`, `new_type`, `acknowledged_at_utc`, `acknowledged_by`. Index `(source_dataset_definition_id, detected_at_utc DESC)`.

**`edge_collector_batches`** and **`edge_collector_buffer_status`**: the collector's own queue and buffer state, so a one-way push is resumable across a network outage.

### 4.5.4 `ppiq_plant` - the canonical model

#### Cluster 1: plant structure

**`sites`**: `site_code varchar(50) NOT NULL`, `site_name varchar(200)`, `plant_time_zone_id varchar(100) NOT NULL`, `country varchar(100)`. Unique `(site_code)`.

**`areas`**: `site_id uuid NOT NULL FK -> sites(id)`, `area_code varchar(50) NOT NULL`, `area_name varchar(200)`. Unique `(site_id, area_code)`; index `(site_id)`.

**`equipment`**: `area_id uuid NOT NULL FK -> areas(id)`, `equipment_code varchar(50) NOT NULL`, `equipment_name varchar(200)`, `equipment_type varchar(100)`, provenance triple. Unique `(area_id, equipment_code)`; indexes `(area_id)`, `(equipment_type)`.

**`routes`**: `site_id uuid FK -> sites(id)`, `route_code varchar(50) NOT NULL`, `route_name varchar(200)`. Unique `(site_id, route_code)`.

**`route_steps`**: `route_id uuid NOT NULL FK -> routes(id) ON DELETE CASCADE`, `operation_definition_id uuid FK -> operation_definitions(id)`, `equipment_id uuid FK -> equipment(id)`, `step_order integer NOT NULL`. Unique `(route_id, step_order)`; indexes `(route_id)`, `(equipment_id)`.

**`operation_definitions`**: `operation_code varchar(50) NOT NULL`, `operation_name varchar(200)`, `operation_type varchar(100)`. Unique `(operation_code)`.

**`material_unit_type_definitions`**: `unit_type_code varchar(50) NOT NULL`, `unit_type_name varchar(200)`, `grain_level integer NOT NULL`, `parent_unit_type_code varchar(50)`. Unique `(unit_type_code)`; index `(grain_level)`. **Imported vocabulary; the multi-grain hierarchy is declared here, never shipped.**

**`industry_templates`**: `template_code`, `template_name`, `payload jsonb`. Configuration only; it can never seed plant data.

#### Cluster 2: material and genealogy - the heart

**`material_units`**

| Column | Type |
|---|---|
| `material_code` | varchar(100) NOT NULL |
| `material_unit_type` | varchar(50) NOT NULL |
| `product_family` | varchar(100) |
| `grade_or_recipe` | varchar(100) |
| `site_id` | uuid FK -> `sites(id)` |
| `native_grain` | varchar(50) |
| `production_start_utc`, `production_end_utc` | timestamptz |
| provenance triple, audit, soft-delete | per conventions |

Indexes:

| Index | Purpose |
|---|---|
| **unique `(site_id, material_code)`** | The business key. One code per site |
| **unique `(source_system, source_record_id)` FILTERED** `WHERE source_system IS NOT NULL AND source_record_id IS NOT NULL` | Makes projection idempotent per source row **without forbidding rows that legitimately have no source identity.** This is the mechanism behind safe re-execution |
| `(site_id)` | Site-scoped scans |
| `(material_unit_type)` | Grain-scoped scans |
| `(site_id, material_unit_type)` | The common workspace filter pair |
| `(material_unit_type, grade_or_recipe)` | The common analysis stratification pair |

**`material_aliases`**: `material_unit_id uuid NOT NULL FK -> material_units(id) ON DELETE CASCADE`, `alias_system varchar(100) NOT NULL`, `alias_value varchar(100) NOT NULL`, provenance triple. Unique `(alias_system, alias_value)`; index `(material_unit_id)`.

*This table is what makes cross-source identity resolution auditable. The customer's several identifiers for one physical unit resolve here; analysis then joins on `material_unit_id` only.*

**`genealogy_edges`**

| Column | Type |
|---|---|
| `parent_material_unit_id` | uuid NOT NULL FK -> `material_units(id)` |
| `child_material_unit_id` | uuid NOT NULL FK -> `material_units(id)` |
| `relationship_type` | varchar(50) NOT NULL |
| `contribution_weight` | **numeric(9,6) NOT NULL** |
| `provenance_confidence` | numeric(9,6) NOT NULL |
| `is_transition` | boolean NOT NULL DEFAULT false |
| `effective_from_utc`, `effective_to_utc` | timestamptz |
| provenance triple | |

Indexes: `(parent_material_unit_id)`; `(child_material_unit_id)`; **unique `(parent_material_unit_id, child_material_unit_id)`**; and the covering index **`(child_material_unit_id, is_transition, contribution_weight)`**.

**Constraints.** `CHECK (contribution_weight > 0 AND contribution_weight <= 1)`; `CHECK (parent_material_unit_id <> child_material_unit_id)`; and **the invariant `SUM(contribution_weight) = 1.0` exactly per child**, enforced by a deferred constraint trigger on insert, update and delete.

*Three design points. `numeric(9,6)` rather than a float, because a float cannot hold that invariant. `is_transition` marks a unit spanning two parents, which is the only case where blended attribution actually matters. And the covering index exists because the feature loader's hot query reads exactly those three columns per child, so it never touches the heap.*

#### Cluster 3: process execution

**`process_step_executions`**: `material_unit_id uuid NOT NULL FK -> material_units(id)`, `route_step_id uuid FK -> route_steps(id)`, `equipment_id uuid FK -> equipment(id)`, `started_at_utc`, `ended_at_utc`, `started_at_local timestamp`, `plant_time_zone_id varchar(100) NOT NULL`, `duration_seconds integer`, `status varchar(50)`, provenance triple. Indexes `(material_unit_id)`, `(route_step_id)`, `(equipment_id)`, `(started_at_utc)`, `(material_unit_id, started_at_utc)`.

**`process_events`**: `equipment_id uuid FK -> equipment(id)`, `material_unit_id uuid NULL FK -> material_units(id)`, `event_type varchar(100) NOT NULL`, `event_at_utc`, `event_at_local`, `plant_time_zone_id`, `payload jsonb`, provenance triple. Indexes `(equipment_id, event_at_utc)`, `(material_unit_id)`, `(event_type)`.

**`parameter_definitions`**: `parameter_code varchar(100) NOT NULL`, `parameter_name varchar(200)`, `unit_of_measure varchar(50)`, `data_type varchar(50)`, `equipment_id uuid NULL FK`, `operation_definition_id uuid NULL FK`, `min_expected numeric(18,6)`, `max_expected numeric(18,6)`, provenance triple. Unique `(parameter_code)`; index `(equipment_id)`. **Imported vocabulary.** `min_expected` and `max_expected` are what a chemistry-range rule reads in S5.

**`parameter_observations`** - the volume table.

| Column | Type |
|---|---|
| `material_unit_id` | uuid FK -> `material_units(id)` |
| `parameter_definition_id` | uuid FK -> `parameter_definitions(id)` |
| `process_step_execution_id` | uuid NULL FK -> `process_step_executions(id)` |
| `equipment_id` | uuid NULL FK -> `equipment(id)` |
| `observed_at_utc` | timestamptz |
| `observed_at_local` | **timestamp without time zone** |
| `plant_time_zone_id` | varchar(100) NOT NULL |
| `numeric_value` | **numeric(18,6)** |
| `text_value` | varchar(500) |
| `unit_of_measure` | varchar(50) |
| `quality_flag` | varchar(50) NOT NULL |
| `raw_value` | varchar(500) |
| provenance triple | |

Indexes: `(material_unit_id)`, `(parameter_definition_id)`, `(process_step_execution_id)`, `(equipment_id)`, `(observed_at_utc)`, `(observed_at_local)`, and the composite `(parameter_definition_id, observed_at_utc)` for a single-parameter time series.

**Partitioning.** Range-partitioned by `observed_at_utc`, monthly, from the Medium capacity class upward. Retention and downsampling per Chapter 7. *`raw_value` is retained deliberately: the original string survives, so a parsing dispute is resolvable rather than arguable.*

#### Cluster 4: quality and loss

**`defect_catalogs`**: `defect_code varchar(100) NOT NULL`, `defect_name varchar(200)`, `defect_category varchar(100)`, `severity_default varchar(50)`, provenance triple. Unique `(source_system, defect_code)`; index `(defect_category)`. **Imported taxonomy, per source, never seeded.**

**`quality_events`**

| Column | Type |
|---|---|
| `material_unit_id` | uuid FK -> `material_units(id)` |
| `defect_catalog_id` | uuid NULL FK -> `defect_catalogs(id)` |
| `event_type` | varchar(100) NOT NULL |
| `severity` | varchar(50) |
| `decision` | varchar(100) |
| `description` | varchar(1000) |
| `event_at_utc` | timestamptz |
| `event_at_local` | timestamp without time zone |
| `plant_time_zone_id` | varchar(100) NOT NULL |
| `position_json` | jsonb |
| provenance triple | |

Indexes: `(material_unit_id)`, `(defect_catalog_id)`, `(event_type)`, `(event_at_utc)`, `(event_at_local)`, `(material_unit_id, event_type, event_at_utc)`.

**Acceptance query for the taxonomy rule:** `SELECT count(*) FROM quality_events WHERE defect_catalog_id IS NULL` must not grow after a projection. A growing count means the resolver did not find the imported catalogue, which means taxonomy was not imported first.

**`downtime_events`**: `equipment_id uuid NOT NULL FK -> equipment(id)`, `started_at_utc`, `ended_at_utc`, **`stopped_minutes numeric(12,3) NOT NULL`**, **`production_impact_minutes numeric(12,3) NOT NULL`**, `cause_code varchar(100)`, `cause_description varchar(1000)`, provenance triple. Indexes `(equipment_id, started_at_utc)`, `(cause_code)`.

*Two quantities, always both, never interchanged (Chapter 1.7). A twenty-minute mill stoppage absorbed by buffering is twenty stopped minutes and zero impact minutes. A three-minute caster pump stoppage forcing a sequence rebuild is three stopped minutes and several hundred impact minutes. Storing one column makes every value calculation wrong.*

**`data_quality_issues`**: `issue_class varchar(100) NOT NULL`, `severity varchar(50) NOT NULL`, `entity_name varchar(200)`, `entity_id uuid NULL`, `source_dataset_definition_id uuid NULL FK`, `first_seen_at_utc`, `last_seen_at_utc`, `occurrence_count integer`, `detail jsonb`. Indexes `(issue_class, severity)`, `(source_dataset_definition_id)`.

#### Cluster 5: the results area (also `ppiq_plant`, because it derives from customer data)

**`compute_runs`**: `analysis_definition_id uuid FK`, `run_status varchar(50) NOT NULL` (`Running | Completed | Blocked | Failed | Reaped`), `outcome_code`, `grain_code`, `window_days integer`, `started_at_utc`, `finished_at_utc`, **`gate_state varchar(20)`**, **`gate_evidence text`**, `blocking_dimension varchar(100)`, `engine_kind varchar(20)`, `definition_version integer`. Indexes `(analysis_definition_id, started_at_utc DESC)`, `(run_status)` partial `WHERE run_status = 'Running'`.

*The partial index on running is what the reaper scans. `gate_evidence` is the string that makes a blocked run explainable from the database alone.*

**`correlation_results`**: `compute_run_id uuid NOT NULL FK -> compute_runs(id) ON DELETE CASCADE`, `feature_code`, `outcome_code`, `method varchar(50) NOT NULL`, `effect_size numeric(18,6)`, `p_value numeric(18,12)`, `q_value numeric(18,12)`, `sample_size integer`, `odds_ratio numeric(18,6)`, `population_description text`, `stability_lower numeric`, `stability_upper numeric`, `sign_consistency numeric`, `is_stable boolean`, `stratum_survival jsonb`, **`framing_text text NOT NULL`**, **`llm_participated boolean NOT NULL DEFAULT false`**. Indexes `(compute_run_id)`, `(outcome_code, q_value)`, `(feature_code)`.

*`framing_text` and `llm_participated` are stored as data, not rendered as interface copy, so the framing survives an export, a screenshot and a report (Chapter 1.5.5).*

**`risk_scores`**: `material_unit_id uuid FK`, `model_registry_id uuid FK`, `score numeric(9,6)`, `risk_class varchar(50)`, `horizon_hours integer`, `drivers jsonb`, `scored_at_utc`. Indexes `(material_unit_id, scored_at_utc DESC)`, `(risk_class)`.

**`model_registry`**: `model_code`, `model_version integer`, `algorithm varchar(100)`, `feature_list jsonb`, `training_window_from/to`, `metrics jsonb`, `status varchar(50)`, `trained_at_utc`. Unique `(model_code, model_version)`.

**`value_impacts`**: `finding_id uuid FK -> correlation_results(id)`, `period_from/to`, `lower_bound numeric(18,2)`, `upper_bound numeric(18,2)`, `point_estimate numeric(18,2)`, `currency char(3) NOT NULL DEFAULT 'EUR'`, `inputs jsonb NOT NULL`, **`basis_status varchar(30) NOT NULL`** (`Sufficient | InsufficientBasis`), `computed_at_utc`. Index `(finding_id)`.

**`cost_assumptions`** and **`cost_assumption_audit`**: per-tenant euro-per-tonne, cost-per-impact-minute, grade premium, each with effective dates; every change audited. **A missing assumption produces `InsufficientBasis`, never a default the vendor invented.**

**`value_realization_ledger`**: `suggestion_id`, `decided_at_utc`, `decision`, `expected_lower/upper`, `observed_value`, `observed_at_utc`. This is what makes the pilot's measurement real rather than asserted.

**`suggestions`**, **`suggestion_audit`**, **`suggestion_comments`**: the recommendation layer with evidence references and outcome tracking.

**`assistant_chunks`**: `chunk_family varchar(50) NOT NULL` (`CONNECTOR | DATASET | MAPPING | DOC | FINDING`), `source_entity_name`, `source_entity_id`, `content text`, `embedding vector`, `role_scope varchar(50) NOT NULL`, `indexed_at_utc`. Indexes `(chunk_family)`, `(role_scope)`, and a vector index on `embedding`. **`role_scope` is why retrieval cannot leak across roles.**

**`assistant_audit_log`**: `asked_at_utc`, `actor_id`, `question text`, `answer text`, `citations jsonb`, `tools_invoked jsonb`, `egress_plan jsonb`, `refused boolean`, `refusal_reason`. *The assistant writes nothing but this.*

**`plant_data_log`** and **`alert_rules`**: specified in 4.5.6, because their evaluation is operational rather than analytical.

### 4.5.5 `ppiq_meta` - product configuration

Each table declares its prefill state, per Rule 2.

| Table | Key columns | Prefill |
|---|---|---|
| `tenants` | `tenant_code` unique | **Prefilled**: one row at install |
| `users` | `username` unique, `password_hash`, `is_bootstrap_admin`, `mfa_required` | **Prefilled**: vendor support only |
| `roles` | `role_code` unique | **Prefilled**: the eight-role catalogue |
| `role_permissions` | `(role_id, surface_code, action_code)` unique | **Prefilled**: the default matrix |
| `user_permission_overrides` | `(user_id, surface_code, action_code)` unique | Empty |
| `sessions` | `refresh_token_hash`, `expires_at_utc` | Empty |
| `license_artifacts` | `token_blob`, `signature`, `tier_code`, `issued_at`, `expires_at` | **Prefilled**: the install token |
| `authoring_quotas` | `(scope_type, scope_id, object_type)` unique | **Prefilled**: role defaults per tier |
| `connection_profiles` | `code` unique, `provider_type`, `vault_reference`, `source_system_tag`, budget columns | Empty |
| `source_system_definitions` | `system_code` unique | Empty |
| `source_dataset_definitions` | `(connection_profile_id, source_schema, source_table)` unique | Empty |
| `source_field_definitions` | `(source_dataset_definition_id, column_name)` unique | Empty |
| `mapping_definitions` | `mapping_code`, `mapping_version`; unique `(mapping_code, mapping_version)`; `lifecycle_status`, `definition_hash`, `output_schema jsonb`, `rollback_pointer` | Empty |
| `schema_view_definitions` | `view_code` unique, `sql_text`, `approval_status` | Empty |
| `job_definitions` | `job_code` unique, `job_class`, `schedule_expression`, `pool_code`, **`compute_weight`**, `is_enabled` | **Prefilled**: the Supervisor weekly definition only |
| `job_run_history` | `(job_definition_id, started_at_utc)` | Empty |
| `dashboard_definitions` | `dashboard_code` unique, `layout_json`, `audience_roles` | **Prefilled only if genericity-lint clean** |
| `dashboard_widget_definitions` | `(dashboard_definition_id, widget_code)` unique; `widget_kind`, `chart_type`, `dimension_code`, `measure_code`, `expression_text`, `filter_json` | As above |
| `widget_expression_status` | `(widget_definition_id)` unique; `declared`, `served`, `last_checked_at_utc` | Empty |
| `kpi_definitions` | `kpi_code` unique | Empty |
| `registry_dimensions`, `registry_measures` | `code` unique; `source_table`, `source_column`, `data_type`, `is_filterable` | **Derived, not prefilled** - see the note below |
| `chart_type_registry` | `chart_type_code` unique; `supports_dimension`, `supports_measure`, `requires_dimension` | **Prefilled**: product grammar |
| `log_channels` | `channel_code` unique; `severity_map jsonb`, `retention_days`, `export_target`, `reading_roles`, `is_builtin` | **Prefilled**: the four built-ins, locked |
| `audit_log_entries` | `(occurred_at_utc, actor_id)`; **append-only** | Grows from install |
| `translations` | `(label_key, language_code)` unique; `text`, `review_state` | **Prefilled**: shipped languages |
| `system_settings` | `setting_key` unique | **Prefilled**: documented defaults |

**The registry note, and it is the most consequential design item in this section.** `registry_dimensions` and `registry_measures` are **derived from the canonical model and the customer's own mapping**, not shipped as rows and not compiled into a code-level set. The present implementation holds them as a compiled `HashSet` containing plant vocabulary - `ShiftCode`, `DefectType`, `RiskClass`, `GradeOrRecipe` - which is a Rule 1 violation even though every value is dynamic, because a plant that filters by batch, recipe, tool number or ambient humidity cannot add a dimension. **What stays closed is the chart-type registry, the widget-kind set and the numeric safety limits**, because those are the product's own grammar and its safety envelope.

**Safety limits, held as configuration and enforced server-side:** default maximum returned rows 100, absolute 500; default raw row limit 50,000, absolute 250,000; default lookback 90 days, absolute 730.

### 4.5.6 Operational tables

**`alert_rules`**

| Column | Type |
|---|---|
| `id` | uuid PK DEFAULT `gen_random_uuid()` |
| `rule_name` | text NOT NULL |
| `parameter_code` | text NOT NULL |
| `comparator` | text NOT NULL |
| `limit_value` | double precision NOT NULL |
| `severity` | text NOT NULL DEFAULT `'Warning'` |
| `is_active` | boolean NOT NULL DEFAULT true |
| `created_at_utc` | timestamptz NOT NULL DEFAULT `now()` |

`CONSTRAINT ck_alert_rules_comparator CHECK (comparator IN ('>', '>=', '<', '<=', '='))`.

**`plant_data_log`**

| Column | Type |
|---|---|
| `id` | uuid PK DEFAULT `gen_random_uuid()` |
| `alert_rule_id` | uuid NOT NULL **REFERENCES `alert_rules(id)` ON DELETE CASCADE** |
| `parameter_observation_id` | uuid NULL |
| `material_code` | text NULL |
| `parameter_code` | text NOT NULL |
| `observed_value` | double precision NULL |
| `comparator` | text NOT NULL |
| `limit_value` | double precision NOT NULL |
| `severity` | text NOT NULL |
| `message` | text NOT NULL |
| `logged_at_utc` | timestamptz NOT NULL DEFAULT `now()` |
| `acknowledged_at_utc`, `acknowledged_by` | timestamptz, uuid |

**Unique `(alert_rule_id, parameter_observation_id)`** plus `ON CONFLICT DO NOTHING` in the evaluator. That pair is the whole idempotence mechanism: a second evaluation over the same data logs nothing.

*Note the deliberate denormalisation. The log stores `comparator`, `limit_value`, `parameter_code` and `material_code` rather than only the rule reference, so **editing a rule later does not rewrite history.** A log entry states the condition that fired at the time it fired.*

**`job_log`**: `occurred_at_utc`, `job_type`, `job_name`, `run_id`, `severity`, `message`, `site_code`, `context jsonb`, `channel_code`. Indexes `(job_type, occurred_at_utc DESC)`, `(run_id)`, `(severity)`. One monitor reads this for every family: import, canonical refresh, analysis, ML, supervisor, alert evaluation.

**`ppiq_business_key_definitions`**: `key_code text NOT NULL`, `entity_scope text`, `version_number integer NOT NULL DEFAULT 1`, `created_at_utc`. **`ppiq_business_key_members`**: `definition_id uuid NOT NULL REFERENCES ppiq_business_key_definitions(id) ON DELETE CASCADE`, `member_role text`, `source_field text`, `sort_order integer NOT NULL DEFAULT 0`. *The dictionary that makes cross-source joining auditable rather than magical.*

### 4.5.7 The join graph

Every analytical question resolves along one of five paths.

```
J1  Parameter to defect, same grain
    parameter_observations
      -> material_units            ON parameter_observations.material_unit_id = material_units.id
      -> quality_events            ON quality_events.material_unit_id = material_units.id
      -> defect_catalogs           ON quality_events.defect_catalog_id = defect_catalogs.id
    The base of every same-grain correlation.

J2  Parameter to defect, ACROSS GRAIN         <-- the product's reason to exist
    parameter_observations (parent grain)
      -> genealogy_edges           ON genealogy_edges.parent_material_unit_id = parameter_observations.material_unit_id
      -> material_units (child)    ON material_units.id = genealogy_edges.child_material_unit_id
      -> quality_events (child)    ON quality_events.material_unit_id = material_units.id
    Attributed by genealogy_edges.contribution_weight, weights summing to 1.0 per child.
    Served by the covering index (child_material_unit_id, is_transition, contribution_weight).

J3  Cross-source identity resolution
    staging_records.raw_json
      -> [Transformation Definition + ppiq_business_key_* dictionary]
      -> material_aliases          ON (alias_system, alias_value)
      -> material_units            ON material_aliases.material_unit_id = material_units.id
    The join is DECLARED here, once, and never re-derived downstream.

J4  Loss attribution
    downtime_events
      -> equipment -> areas -> sites
      and equipment -> process_step_executions -> material_units
    Carrying BOTH stopped_minutes and production_impact_minutes.

J5  Provenance walk-back (the audit path)
    any canonical row.import_batch_id
      -> import_batches
      -> source_dataset_definitions
      -> connection_profiles
    Every figure on every screen resolves along J5 to the source it came from.
```

**J2 is the one to show a sceptical engineer.** It is what a business-intelligence tool cannot do without the genealogy table and the weight invariant, and it is the mechanism behind every cross-source claim the product makes.

### 4.5.8 Row-level security, stated as schema

Every tenant-owned table in all three schemas carries `tenant_id NOT NULL`, and enforcement is layered so no single mistake crosses a boundary.

1. **Row-level security enabled on every tenant-owned table**, one policy pattern: `tenant_id = current_setting('ppiq.tenant')::uuid`. The setting is bound per connection from the authenticated principal, **never from client input**.
2. **One resolver** maps principal to tenant. Shared, dedicated, on-premise and air-gapped deployments all use it; a dedicated deployment simply has one tenant and the same policies never exclude a row.
3. **The application composes tenant scope as well**, defence in depth.
4. **An architecture test asserts that every tenant-owned table has its policy**, so a new table cannot ship without one. `FORCE ROW LEVEL SECURITY` is set, so even the table owner is subject to it - which also means **a NULL `tenant_id` makes a row invisible to the application**, and that is why `tenant_id` is `NOT NULL` everywhere rather than defaulted.
5. **Retrieval and export run under the same scope.** There is no side door for search indexes or reports.

### 4.5.9 The index rationale, summarised

| Pattern | Where | Why |
|---|---|---|
| Filtered unique on the provenance pair | `material_units` and every projected entity | Idempotent projection without forbidding rows that have no source identity |
| Covering index on the three genealogy columns | `genealogy_edges` | The feature loader's hot query never touches the heap |
| Partial index on an unprocessed predicate | `staging_records`, `compute_runs` | Stays small no matter how large the table grows |
| Composite `(entity, time)` | observations, events, executions, batches | Every analytical window is a range scan on one entity |
| Composite `(dimension, measure-ordering)` | `(outcome_code, q_value)` on results | Findings are read ranked, so the index serves the sort |
| GIN on `jsonb` | `staging_records.raw_json` | Ad-hoc inspection of a source row during an investigation |
| Vector index | `assistant_chunks.embedding` | Retrieval latency |

---


### 4.5.10 The relationship model: where the user's joins live, and who reads them

The Transformation Definition holds the authored graph, but a graph blob is not queryable. **Publishing a definition emits a relationship model** - the durable, queryable statement of how this plant's data joins - and five consumers read it, which is what makes the join declared once and honoured everywhere.

**`ppiq_meta.plant_relationships`**: `relationship_code` unique, `left_entity`, `right_entity`, `cardinality` (`1-1 | 1-n | n-m`), `join_type`, `source_definition_id` FK -> definition_store, `definition_version`, `is_active`. Index `(left_entity, right_entity)`.

**`ppiq_meta.plant_relationship_members`**: `relationship_id` FK ON DELETE CASCADE, `left_column`, `right_column`, `member_order`. Composite keys are ordered members.

**`ppiq_meta.plant_relationship_paths`**: `from_entity`, `to_entity`, `hop_count`, `path_json`, `is_preferred`. Materialised transitive paths, refreshed on publish, so path-finding is a lookup rather than a graph search at query time.

| Consumer | What it reads | Effect |
|---|---|---|
| **The registry** | Which dimensions are reachable from which measures | A dimension appears in a palette only when a join path to the measure exists |
| **The widget query compiler** | The join path between the author's chosen columns | The compiled SQL joins exactly as the engineer declared, never re-derived |
| **The associative engine** | The join graph | possible / excluded computation walks the declared model |
| **The feature assembler** | Paths crossing grain via genealogy | A parent-grain parameter reaches a child-grain outcome along the declared route |
| **The assistant** | The traversable model | Retrieval and tools cannot join what was never declared |

A relationship retired by a new definition version is deactivated, never deleted, so historical results remain explainable.

### 4.5.11 The definition store: where every no-code / low-code file lives

One store for all five surfaces. One permission model, one versioning model, one export format, one search, one dependency graph.

**`ppiq_meta.definition_store`**: `definition_code` unique, `surface` (`S1..S5`), `name`, `owner_id`, `folder_path`, `tags`, `current_version`.

**`ppiq_meta.definition_versions`**: `definition_id` FK, `version_number`, `status` (`draft | validated | published | paused_by_drift | rolled_back`), `mode` (`block | sql`), `graph_json`, `sql_text`, `compiled_sql`, `definition_hash`, `output_schema jsonb`, `input_bindings jsonb`, `published_by`, `published_at_utc`, `rollback_pointer`. Unique `(definition_id, version_number)`. Published rows are immutable; editing forks the next draft.

**`ppiq_meta.definition_dependencies`**: `definition_id` -> `depends_on_definition_id`, `dependency_kind`. This is what answers "what breaks if I change this?" before it breaks.

### 4.5.12 Analysis, prediction and ML result tables

The tables the intelligence layers write, completing the results area of 4.5.4.

| Table | Grain | Key columns |
|---|---|---|
| `feature_store` | one unit x one feature set version | `material_unit_id`, `feature_set_version`, `features jsonb`, `label_value`, `assembled_at_utc`; unique `(material_unit_id, feature_set_version)` |
| `feature_refresh_watermarks` | one feature set | `feature_set_version`, `last_batch_watermark`, `refreshed_at_utc` - incremental refresh reads this |
| `model_training_runs` | one training | `model_registry_id` FK, `split_strategy`, `missing_value_policy`, `scaling_params jsonb`, `metrics jsonb`, `trained_at_utc` |
| `prediction_runs` | one scoring pass | `model_registry_id` FK, `gate_state`, `gate_evidence`, `units_scored`, `run_at_utc` |
| `predictions` | one unit x one run | `prediction_run_id` FK, `material_unit_id` FK, `defect_code`, `risk_score numeric(9,6)`, `risk_class`, `horizon_stage`, `acknowledged_at_utc`; index `(risk_class, horizon_stage)` |
| `prediction_drivers` | one driver of one prediction | `prediction_id` FK ON DELETE CASCADE, `feature_code`, `contribution numeric(9,6)`, `direction`, `rank` |
| `practice_statistics` | one practice x one context x one outcome | `practice_signature_hash`, `practice_json`, `context_json`, `outcome_code`, `outcome_rate`, `support_count integer`, `confidence_low/high`, `computed_at_utc`; index `(outcome_code, support_count DESC)` |
| `remediation_candidates` | one remediation for one early condition | `condition_signature_hash`, `defect_code`, `practice_signature_hash`, `historical_success_rate`, `support_count integer`, `expected_effect jsonb`; **`support_count >= 20` is checked before a card ever renders** |
| `model_drift_observations` | one drift check | `model_registry_id` FK, `checked_at_utc`, `feature_drift jsonb`, `performance_delta`, `verdict` |

All carry the universal conventions, the tenant column, and where computed on emulated data the synthetic flag.

### 4.5.13 Projection validation and the quarantine: when the user's mapping is wrong

A wrong mapping is the mistake a customer will actually make, so its failure behaviour is specified, typed and recoverable.

**The error classes**, each with a stable code:

| Code | Class | Example sentence |
|---|---|---|
| `PV01` | Unresolved key | "Row 4,812: `piece_id` = `C-9931` matches no material unit and no alias." |
| `PV02` | Type mismatch | "Row 219: `temp` = `n/a` cannot become a number." |
| `PV03` | Null into required | "Row 77: `observed_at` is empty; ParameterObservation requires a time." |
| `PV04` | Duplicate business key | "Rows 12 and 3,405 both declare material `C-700394` for site `HSM`." |
| `PV05` | Orphan reference | "Row 91: defect code `SCR_9` is not in the imported catalogue." |
| `PV06` | Weight-sum violation | "Child `C-700394`: genealogy weights sum to 0.85, not 1.0." |
| `PV07` | Out-of-range value | "Row 5,120: `speed` = `-40` is outside the declared expected range." |
| `PV08` | Unknown taxonomy code | "Row 3: unit type `MEGACOIL` is not a registered material unit type." |

**The behaviour.** Rows fail **individually**, never the batch: each failed row lands in **`ppiq_staging.projection_quarantine`** (`staging_record_id` FK, `mapping_definition_id` FK, `error_code`, `error_detail`, `offending_value`, `quarantined_at_utc`, `resolved_at_utc`) with its reason. The batch result reports `Mapped / Quarantined / Total`. The author sees quarantined rows **grouped by error code with example rows** on Mapping Health, each group naming the fix. After the mapping is corrected, **re-projection reprocesses only the quarantined rows** - the filtered unique index of 4.5.4 makes this idempotent. A quarantine older than the staging retention is surfaced before it is pruned, never silently dropped.

**Pre-flight.** Before the first full projection of a new mapping version, a bounded dry-run over a sample reports the projected error profile, so the author fixes `PV02` on two hundred rows rather than two million.

### 4.5.14 Entity-relationship diagrams

One diagram per cluster, drawn as text so they live inside the document. `||--o{` reads one-to-many; `}o--o{` many-to-many.

**Structure and material**

```
 sites ||--o{ areas ||--o{ equipment
 sites ||--o{ routes ||--o{ route_steps }o--|| operation_definitions
 route_steps }o--o| equipment
 material_unit_type_definitions ||--o{ material_units }o--|| sites
 material_units ||--o{ material_aliases
 material_units ||--o{ genealogy_edges (as parent)
 material_units ||--o{ genealogy_edges (as child)     weight sums to 1.0 per child
```

**Process, quality and loss**

```
 material_units ||--o{ process_step_executions }o--|| route_steps
 process_step_executions }o--o| equipment
 parameter_definitions ||--o{ parameter_observations }o--|| material_units
 parameter_observations }o--o| process_step_executions
 defect_catalogs ||--o{ quality_events }o--|| material_units
 equipment ||--o{ downtime_events        (stopped_minutes AND production_impact_minutes)
 equipment ||--o{ process_events
```

**Acquisition and mapping**

```
 connection_profiles ||--o{ source_dataset_definitions ||--o{ source_field_definitions
 source_dataset_definitions ||--o{ import_batches ||--o{ staging_records
 import_batches ||--o{ projection_quarantine }o--|| mapping_definitions
 definition_store ||--o{ definition_versions
 definition_store ||--o{ definition_dependencies
 definition_versions ||--o{ plant_relationships ||--o{ plant_relationship_members
```

**Results and intelligence**

```
 compute_runs ||--o{ correlation_results ||--o{ value_impacts
 model_registry ||--o{ model_training_runs
 model_registry ||--o{ prediction_runs ||--o{ predictions ||--o{ prediction_drivers
 predictions }o--|| material_units
 practice_statistics ||--o{ remediation_candidates
 correlation_results ||--o{ suggestions ||--o{ value_realization_ledger
 alert_rules ||--o{ plant_data_log
```

**Overview**

```
 SOURCES -> collector -> ppiq_staging (batches, records, quarantine)
         -> [Transformation Definition + relationship model + business keys]
         -> ppiq_plant canonical (structure, material+genealogy, process, quality, loss)
         -> ppiq_plant results (runs, findings, predictions, practices, value)
         -> surfaces (pages, dock, reports)          config: ppiq_meta
```

## 4.6 Credentials, identities and topology

*Per the author's ruling, this document carries operational credentials in full. They are held in this single contiguous section and nowhere else in the document, so that a customer-safe extract is one deletion.*

### 4.6.1 Component topology

| Component | Listens | Role | Talks to |
|---|---|---|---|
| Web application | 5173 dev, 443 via proxy | The interface | API service |
| API service | 5063 dev, behind proxy | The 27 API domains | PostgreSQL, model gateway |
| Workers | none inbound | Import, projection, analysis, ML, supervisor, report pools | PostgreSQL, collector queue |
| PostgreSQL | 5432 | `ppiq_staging`, `ppiq_plant`, `ppiq_meta` | - |
| Reverse proxy | 80, 443 | TLS termination and routing | web, API |
| Collector | customer DMZ, outbound only | One-way push from sources | customer sources, API ingest |
| Model gateway | internal | Assistant serving: self-hosted, private endpoint, customer model | assistant service |

**The direction rule.** The collector connects **outward** to the core. The core never initiates a connection into the operational network. This is what allows a plant automation team to approve the installation without a control-systems risk review.

### 4.6.2 Environments and profiles

| Environment | Database | Selected by |
|---|---|---|
| Local development | empty-start development database | launch profile `local` |
| Demonstration | populated demonstration database | launch profile `presentation` |
| Staging server | server database | server compose file |
| Customer | customer database | customer configuration |

**One branch, two databases, profile-selected.** There is no demonstration branch and no demonstration code path (Chapter 1.6).

### 4.6.3 Emulated source fleet

Six containers mirroring real plant systems across PostgreSQL, Oracle, SQL Server and MySQL, plus file drops, each reachable by the collector exactly as a customer source would be. Service names, ports, database names and credentials are emulation fixtures, versioned outside the product, and are listed in 4.6.5 with the rest.

### 4.6.4 Identity and secret handling

| Class | Where it lives | Rule |
|---|---|---|
| User passwords | `ppiq_meta.users.password_hash` | Memory-hard hash; never reversible |
| Access tokens | Client memory only | Never browser storage; rotating refresh cookie |
| Source credentials | Encrypted vault; `connection_profiles.vault_reference` | Masked on every read-back; never in the browser; never in application configuration |
| Licence token | `ppiq_meta.license_artifacts` | Signed, offline-verifiable; a client-supplied tier override is ignored |
| Server and build credentials | Operator custody | Listed in 4.6.5, per the author's ruling |

Administrative resets are product endpoints that write audit records, never direct database statements (Rule 2). Vendor support access is scoped, time-boxed and audited.

### 4.6.5 Credentials

> **[CREDENTIALS BLOCK - verbatim insertion at assembly]**
>
> This block receives, unaltered and complete: server SSH access and password; the application database host, port, database, username and password; the build-system URL, username and password; the public service URLs; the local development connection string and its environment variables; and the six emulated source service names, ports, database names and credentials.
>
> **Source:** `commands.txt`. It was supplied earlier in this project and has since left my working context, so I cannot reproduce the values from memory and will not invent them. **Re-attach `commands.txt` with the assembly request and this block fills verbatim.**
>
> Everything around this block - the topology, the ports, the profiles, the identity model, the secret-handling rules and the fleet description - is complete above and needs nothing further.
>
> **Standing operations obligation, carried to Chapter 7:** one password is currently shared between the application database and the build system, and the deployment scripts carry a hardcoded server address in fifteen places. The rotation task, the per-environment credential split and the address parameterisation are registered there.

---




*End of Chapter 3.*

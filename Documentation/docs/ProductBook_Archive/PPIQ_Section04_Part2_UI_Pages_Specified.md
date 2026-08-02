# PlantProcess IQ - Master Design Document

**Version 4.0 | Author: Karim, SOU Industrial Software, Dusseldorf**

*File maps to PPIQ.txt section 4.3 and 4.4, labelled "Chapter 3: General software product technical Function Description".*

---

# SECTION 4, PART 2 - THE UI PAGES, SPECIFIED

> **Audience (4.7):** the customer's advanced IT and software staff, and our developers taking hand-over.
>
> **Voice (4.8):** senior software engineer and technical lead.
>
> **Completes:** 4.3 (page list) and 4.4 (deep specification of every page, to control level, with every hook and every call). Thirty-four pages, none omitted.
>
> **Relationship to other parts.** Part 1 (4.1, 4.2) specifies the fifteen data-flow steps to endpoint level and is unchanged. Part 3 (4.5, 4.6) specifies the schema and the topology.

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
| E1 | Assistant | `/assistant` | 14 |
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

## Group E - Ask and operate

### E1 Assistant - `/assistant`

**AIM.** Reach an answer by asking, with every number cited.
**ROLES.** Pro Plus; retrieval is permission-scoped per role.
**LAYOUT.** Header. Centre: the conversation, newest at the block-end. Block-end: the composer. Inline-end, collapsible: the evidence panel for the selected message.
**CONTROLS.**

| Control | Type | Token | Position | Enabled when |
|---|---|---|---|---|
| Question input | `StandardTextArea`, auto-growing | Panel Navy field | composer | always |
| Send | primary, also **Enter** | Electric Blue | composer inline-end | input non-empty |
| Citation chip | chip per citation | Electric Cyan | under each answer | citation resolvable |
| Evidence panel | drawer | Panel Navy | inline-end | citation selected |
| New conversation | secondary | Corporate Blue | header inline-end | always |
| Copy answer | icon | Muted Steel | per message | always |

**HOOKS.** `useApiResource` per message, `useEntitlements`, `useStandardToast`.
**CALLS.** `askAssistant(req)` -> `POST /api/assistant/ask`; `POST /api/assistant/reindex`.
**STATES.** **Refusal is a designed state, not an error:** amber, with the sentence that the evidence is absent. **A transport failure is red and says the request failed**; a transport fault is never dressed as an evidential abstention. A citation that cannot resolve is not rendered, and the sentence containing it is rejected before display by the no-fabrication guard.
**SELECTIONS.** None.
**EMPTY-INSTALL.** Tier-locked; for a permitted tier with no index, states that the index is empty and offers reindex to an administrator.
**A11Y + RTL.** The conversation is a log region with polite announcement; Enter sends and Shift-Enter newlines; citation chips are links with accessible names.

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

*End of Section 4, Part 2. Part 3 specifies 4.5 the database schema and 4.6 credentials and topology.*

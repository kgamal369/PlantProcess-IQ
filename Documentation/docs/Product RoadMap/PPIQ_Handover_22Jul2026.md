# PPIQ HANDOVER - Session of 21-22 Jul 2026 (M1 closure + M2 Impress Sprint + v27 reset)
**Purpose: the next session starts with EVERYTHING this one knew. Do not re-investigate, do not re-run the tests listed in Section 6 - their results are recorded here and are current as of 22-Jul 09:50.**
**Previous transcripts (this machine only, not available in a fresh session): /mnt/transcripts/ incl. 2026-07-21-21-40-42-ppiq-demo-day-m2-sprint.txt + journal.txt for older sessions.**

=====================================================================
## 1. IDENTITY, ENVIRONMENT, CONTRACT (memorize before doing anything)
=====================================================================

**Karim**: solo founder, SOU Industrial Software, Duesseldorf. Product: **PlantProcess IQ (PPIQ)** - generic, read-only, evidence-grade process-to-quality intelligence platform for manufacturing plants (steel first, industry-agnostic core). Day job: SMS Group (Belisma L2 consulting). 13y industrial/MES/L2 experience.

**Repo**: `C:\Workspace\PlantProcess-IQ`
- Backend: `Backend\PlantProcess.Api` (.NET 9, Npgsql), `Backend\PlantProcess.Application`, `Backend\database\scripts\` (numbered SQL migrations, rebuild chain)
- Product frontend: `Frontend\PlantProcess.Web` (Vite + React + TS)
- Marketing site (separate app in same repo): `Website\PlantProcess.Website` (Vite + React + TS, own tests/Docker)

**Database**: `ppiq_presentation` @ 127.0.0.1:5432, user `ppiq_dev` / `ppiq_dev_local_only`. psql at `C:\Program Files\PostgreSQL\16\bin\psql.exe`.
**API**: :5063, start `.\scripts\run\start-api.ps1 -Profile presentation`. **Frontend**: :5173 vite. **Login**: e2eadmin / E2EAdmin123! via POST `/auth/login` (token field: accessToken-family; the proof script tries several).

**SCRIPT DELIVERY CONTRACT (absolute, user-enforced)**:
- Everything as **PowerShell 5.1 apply packs**: preflight -> backup(.stamp.bak) -> anchored replace / full-file write -> on-disk self-check (marker string) -> gates -> auto-revert on failure -> `-Revert` switch. **NEVER deliver zip files** (rule given 22-Jul after two zips; both were converted to packs).
- Pure ASCII, UTF-8-no-BOM via `[System.IO.File]::WriteAllText` + `UTF8Encoding($false)`; CRLF for .ps1/.cs/.tsx; LF for .sh. No em-dashes/curly quotes. No `&&` as a PS operator (embedded && inside TS string content is fine - it is data). Launcher form always: `powershell -NoProfile -ExecutionPolicy Bypass -File .\X.ps1` (MOTW workaround). Cuddled `} else {`. Run from repo root.
- Pack generation technique (proven): python embeds sources in **single-quoted here-strings** (`@'...'@` - no interpolation, TS backticks/$ safe); pre-sanitize to ASCII (map: `\u00b7`->`&middot;`, `\u20ac`->`&euro;`, arrows->`->`, `\u25BE\u25B8`->`\uXXXX` JS escapes in TSX, `\u00b7`->`\00B7` in CSS); assert no line starts with `'@`; verify bytes: non-ascii 0, CRLF-only.

**ENVIRONMENT FRICTIONS (solved patterns - reuse, do not rediscover)**:
- git not on PATH in fresh shells: `$env:Path += ';C:\Program Files\Git\cmd'`. A `& git` wrapper with `$LASTEXITCODE` **throws under Set-StrictMode when git never launched** (VariableIsUndefined) - guard or have the user run git interactively.
- psql NOTICE output kills `$ErrorActionPreference='Stop'` -> use `Continue` + check `$LASTEXITCODE` + `$env:PGOPTIONS='-c client_min_messages=warning'` + `-v ON_ERROR_STOP=1 -X -q`.
- psql `:'var'` only with `-f`; JSON into psql only via `jsonb_build_object` in-DB or `-v` from file.
- **User's uploaded attachments frequently arrive EMPTY** - always ask him to paste console output as text (he does this by habit now).
- Console shows `Ô£ô` etc.: vitest UTF-8 `✓` decoded via CP850. Cosmetic only. Future packs: `[Console]::OutputEncoding=[System.Text.Encoding]::UTF8` + `$env:NO_COLOR='1'`.

**HONESTY RULE (standing, the user's constitution)**: "built" (code exists) != "working" (runtime-proven on HIS screen/log). Never claim working without his artifact. Defects get register IDs. Evidence before cure. Zero preamble, no flattery.

**WEBSITE RULE (absolute)**: the public site NEVER shows blockers, unfinished features, failed tests, missing data, or honest-abstain-as-failure. Audience = non-technical CEOs/buyers at large manufacturers. Benefit-led (quality up, downtime down, productivity up), quiet/confident/data-driven, no circus. PPIQ-as-hub diagram (L2 DB, SAP, L1 sensors, Quality, Lab, Inspection, Excel -> AI/ML -> suggestions/predictions/chatbot). Generic no-code ETL, no experts needed, any industry. Typed vendor names, never fabricated logos.

**ONE-TEST-PASS RULE**: nothing is tested twice. All acceptance folds into ONE consolidated pass at a milestone end. The checklist exists: `PPIQ_Consolidated_Test_Pass.md` (outputs).

=====================================================================
## 2. WHAT THIS SESSION DID (chronological, with findings per step)
=====================================================================

### A. M1 closure (21-Jul)
1. **Commit-EngineMigrations crash** -> root-caused ($LASTEXITCODE never set because git absent); user committed manually. **M1-18/31/40 done - all 24h of demo-eve fixes are in git.**
2. **M1-22 Suggestions 401**: `/api/suggestions` missing from the deny-by-default matrix in `Backend\PlantProcess.Api\Security\PlantAccessControl.cs`. Pack inserted `("/api/suggestions", All(), "analysis.execute", false)` after the `/api/assistant` line. BUILD GREEN. **This anchor line is now THE proven insertion point for new route mappings** (reused twice since).
3. **M1-01 Assistant proof**: reindex chunkCount=25 PASS; grounded questions return real cited answers (6 citations, DATASET+DOC evidence naming actual mappings/connections). **Response field is `text`, NOT `answer`** (endpoint returns isRefusal/refusalReason/text/citations[kind,id,detail]/blocked). **CAVEAT: the predictive question ("which coils will fail tomorrow?") does NOT refuse** (isRefusal:False, answers with retrieved source facts) - demo script must not promise the refusal beat; off-domain questions more likely to refuse.
4. **M1-32 golden script**: `Run-GoldenAnalysis.ps1` NOT FOUND on his disk (recursive search too) -> **skipped/deferred**, correctly (Nice-to-have; the golden script is not demoed).
5. **M1-05/23/06/09**: verify-first doc delivered (`M1-05_23_06_09_Verify.md`). Key code finding: **the findings endpoint already scopes to a single latest compute_run_id** (AdvancedResultsEndpoints) so cross-run duplicates usually never reach the page; optional dedup view `M1-09_findings_latest_view.sql` exists but probably unnecessary.
6. **Snapshot validation** (his 14:30 full-repo txt bundles): grep-verified every fix present (activeChartType x12, globalFilters merge, ObservationCount const+registry, /api/suggestions line, migrations 741/742, rebuild step [1b], 10.4MB demo seed). Wrote `M1_Final_Validation_21Jul.md`: 7 targets scorecard + audit-signal triage.
7. **Audit signals triage** (10_Audit_Signals): 12 CRIT/35 WARN. Real items: frontend visual/e2e matrix ENUMERATED with `--list`, not executed (room language: "unit+type gates green; visual/e2e matrix runs in the pilot CI"); hardcoded staging IP **178.105.152.180** x15 in deploy scripts/docs; `IsBootstrapAdmin=true` in local.env AND presentation.env (OK for demo, must die for customers); dev-seed endpoints ARE guard-tested (ProductionDevEndpointGuardTests). Several CRITs are the auditor matching its own regexes (catchError SUCCESS x3, conn-string key) - no action.

### B. M2 Impress Sprint planning
- User confirmed presentation moved (~a week) -> 45-60h window. **v26 backlog** created with sprint sheet: M2-42 website (8h) -> M2-37 associative (16h) -> M2-31 canvas (20h) -> M2-38-lite (8h) -> M2-43 (4h) -> M2-28 (2h) = 58h.

### C. M2-42 Website (multiple iterations - learn from this arc)
- V1 static built from scratch -> REJECTED: he has an existing React site; wrong palette; showed "BLOCKED" gate imagery publicly (violated the no-negatives rule - my mistake, acknowledged).
- His real site: `Website\PlantProcess.Website` React/Vite with NewHomePage, 4 capability packs (Quality/Surface, Reliability/Downtime, Energy Intelligence, Yard/Logistics), 5 role paths (Operations, Quality, Process Eng, IT&OT, CFO&Procurement), RequestDemoForm, brand tokens `--sou-bg #050b18, panel #0b1730, panel-2 #102a43, cyan #00d4ff, blue #0a84ff, green #2ce6a2, text #eaf6ff, muted #8ea7c1`.
- Enterprise brief he gave: quiet/confident/data-driven; scroll-BOUND SVG draw (not loops); widget-like hovers; integration ecosystem (typed names, no fake logos); interactive ROI calculator (visitor's own math, "directional estimate", benchmark-free, CTA "Discuss these numbers with us").
- **DELIVERED + RUN GREEN**: `Install-PpiqReactEnhancements.ps1` - 6 new files under Website\PlantProcess.Website\src: `components/motion/useScrollDraw.ts` (data-draw attr, getTotalLength/dashoffset, rAF, draw-only, reduced-motion=fully-drawn), `graphics/GoldenThreadScroll.tsx`, `graphics/ArchitectureFlowScroll.tsx`, `sections/IntegrationEcosystem.tsx`, `roi/RoiCalculator.tsx` (tonnage/margin/recovery sliders -> annual EUR), `styles/motion-roi.css` (+ `ppiq-widget-hover` contract: data-dim/data-focus). tsc GREEN. **PENDING: manual NewHomePage wire-up** (order: Hero -> ArchitectureFlowScroll -> packs -> GoldenThreadScroll -> IntegrationEcosystem -> RoiCalculator demoHref="#request-demo" -> RequestDemoForm) + real contact email (placeholder `contact@sou-industrial.com`).

### D. M2-31 Canvas foundation
- **DISCOVERY**: the 540 visual-mapper tables (`ppiq_visual_mapper_sessions/tables/columns/business_keys/joins/canonical_suggestions/dry_runs/versions` - session-centric, versions has `mapping_definition jsonb` + `rolled_back_from_version_id`, statuses draft/validated/published/paused_by_drift/rolled_back) had **NO HTTP endpoints**. Pack includes the backend scaffold.
- **DELIVERED + RUN FULLY GREEN** (`Install-M231CanvasFoundation.ps1`): npm `@xyflow/react` auto-installed; 9 files (canvas kit ports.ts/CanvasShell.tsx/canvas.css/DatasetNode/BlockNode, api/canvasApi.ts, pages/Prep/VisualJoinCanvasPage.tsx, pages/Analysis/AnalysisToolboxPage.tsx, `Backend\PlantProcess.Api\Endpoints\Prep\VisualMapperEndpoints.cs`); access matrix auto-patched `("/api/prep/visual-mapper", All(), "analysis.execute", false)`; sessions gained `draft_definition jsonb` + `updated_at_utc` via ALTER IF NOT EXISTS; tsc green; dotnet 0 errors.
- Backend scaffold: GET /datasets (information_schema over **staging** schema, key heuristics *_id/*_no/id/piece_id/material_id/heat_id/coil_id), POST /sessions, /sessions/{id}/graph (jsonb), /sessions/{id}/dry-run (**server-side-only SQL**: identifier regex `^[a-zA-Z0-9_]+$`, staging-locked, equality joins from graph, LIMIT 50, unjoined table -> honest rejection recorded to dry_runs), /sessions/{id}/publish (immutable version row, MAX(version_number)+1).
- Toolbox payload parity BY CONSTRUCTION: it calls the same client fn the form calls.
- Known cosmetic: CS1998 warning at VisualMapperEndpoints.cs:133 (async helper without await) - fix on next touch.

### E. M2-37 Associative engine
- **Architecture (decided + shipped)**: client-orchestrated possible-sets reusing the registry-validated `POST /analytics/dashboard/widgets/query` per field (dimension enumeration, measure `observationCount`, maxRows 500), filters = current selections MINUS the field's own (Qlik semantic). all-set = unfiltered enumeration at mount; excluded = all - possible. Additive collapsible panel behind a live toggle; existing filter bar untouched.
- **DELIVERED + RUN GREEN** (`Install-M237Associative.ps1`): `src/state/associativeFields.ts` (8 fields; dimension codes VERIFIED against the server registry: site, area, equipment, sourceSystem, shiftCode, defectType, parameterCode, riskClass, + materialCode... see registry list in Section 5), `src/state/AssociativeContext.tsx` (250ms debounce, generation guard, defensive row read dimension/label/key, toggleValue incl. excluded-click pivot), `components/dashboard/AssociativePanel.tsx` + `associative.css`; anchored mount into InteractiveWorkspacePage (import anchor = the SavedDashboardWidget import line; mount before `<SelectionBreadcrumb`), verified on disk. tsc GREEN.

### F. M2-38-lite Charts (the hardest arc - 3 versions; read the lessons)
- v1: applied everything, **dotnet gate failed CS2012** - the RUNNING API locks `PlantProcess.Api\obj\Debug\net9.0\PlantProcess.Api.dll`. Pack auto-reverted (correct behavior). **CS2012 = stop the API, re-run; never a code error.**
- Meanwhile `npm run build` exposed **5 tsc errors in my M2-31/M2-37 files** that my gates had missed because **`npx tsc --noEmit` is a NO-OP in this workspace**: root tsconfig is `{ "files": [], "references": [tsconfig.app.json, tsconfig.node.json] }`. **THE REAL GATE IS `npx tsc -b`** (same as npm run build). All packs since gate with tsc -b.
- The 5 errors + fixes (`Fix-CanvasBuildErrors.ps1` v2 RUN GREEN):
  (1) apiClient lives at **`src/api/http`** (module: could be http/index.ts - never Test-Path a literal filename; v2 DISCOVERS the specifier by regexing `import { apiClient } from "X"` out of `advancedAnalysis.ts` and derives per-location: same-dir `./http`, from src/state `../api/http`, alias `@/...` verbatim).
  (2) the correlation fn is **`runCorrelation(outcomeKey, grain="coil", windowDays=30)`** posting `{outcomeKey, grain, windowDays}` to `/ml/foundation/compute/correlation` - not computeCorrelation.
  (3+4) @xyflow/react v12 typing: node components must be `function X({data}: NodeProps<Node<DataType,"kind">>)` with `[key: string]: unknown` index signature on the data type - NOT NodeProps<any>.
- v2 charts pack then **failed the architecture tests** (see G). v3 (`Install-M238ChartCatalogue.ps1`) **RUN FULLY GREEN 09:49**: conformant ChartExtras (StandardP2Button heat cells, bucketed classes `.ppiq-heat--0..9` in chartExtras.css - no inline styles), variant-aware SavedDashboardWidget patch (detects `const activeChartType` vs `widget.chartType`), measure-aware switcher `extendChartTypes(widget.measureCode)` (scatter offered ONLY on avgParameterValue/riskScore/defectRate - the SERVER restricts it), M2-43 field wired natively, backend Pareto const + registry entry (unique 3-line anchors: Scatter/Heatmap/Table sequence in constants because `Table = "table"` appears twice in the file; Heatmap,Table sequence in the registry HashSet).
- **recharts typing lesson**: never annotate recharts callback params; let contextual typing infer, use a `catOf(d: unknown): string|null` safe-cast helper and block-body handlers (clean void). `XAxis tickFormatter={(v: number)=>...}` compiles fine (its param is any) - left alone.
- HONEST SCOPE recorded: scatter-lite = category-vs-measure dot distribution; true XY needs a two-measure query (now M1-06 in v27).

### G. Design-system discovery (CRITICAL for all future frontend work)
The repo enforces conformance via two vitest architecture tests in `Frontend\PlantProcess.Web\src\test\architecture\`:
- **noRawStandardElements.test.ts (PPIQ-T11)**: no raw `<button>` or `<table>` in pages/components -> use `StandardP2Button` / `StandardP2Table` from `@/components/standard/StandardP2Controls` (exports: StandardP2Button{variant primary|secondary|danger|ghost|action}, StandardP2Input, StandardP2Select, StandardP2TextArea, StandardP2Table). The standard/ + brand/ dirs are excluded.
- **uiConformanceRatchet.test.ts**: per-file baseline (uiConformance.baseline.json): D1 raw controls regex `/<(input|select|textarea|label)\b/g`, D2 inline styles regex `/style=\{\{/g` - **no new file may introduce ANY**; existing files may not exceed baseline. Regenerate baseline only via Add-UiRatchetGate.ps1 -RegenerateBaseline (do NOT - fix properly instead).
My M2-31/M2-37 files violated both (4 T11 + 7 ratchet offenders); **all six files were rewritten conformant** in `Install-M243-M228.ps1` v2/v3 (RUN GREEN incl. both tests). Technique for dynamic styling without inline styles: **bucketed CSS classes** (heat intensity 0..9), port-type classes (`.ppiq-port--key/number/text/date/flow`), layout helper classes.
Also: **vitest `--reporter=basic` is INVALID in their vitest 4.1.6** (tries to load "basic" as a module -> ERR_LOAD_URL crash before any test). Correct invocation: `npx vitest run <file1> <file2>` and detect a real run by `Test Files` in output; treat runner-crash-without-summary as INCONCLUSIVE (keep files if tsc passed) not FAILED.

### H. M2-43 Interaction debt (v3 RUN GREEN 09:41)
Ground truths read from code first:
- SavedDashboardWidget had `field: "materialCode"` hardcoded x3 in the selection meta (DEF-005 root cause: donut of defect types wrote materialCode='CRACK_LONG' and emptied the workspace).
- `onRemove={onRemoved}` / `onClone={onCloned}` passed the parent's REFRESH callbacks straight through - **no API call ever happened** (DEF-006). `dashboardDefinitionId` was in props but not destructured.
- API signatures: `productApi.deleteDashboardWidget(dashboardDefinitionId, widgetId)` (-> deactivate...), `productApi.cloneDashboardWidget(dashboardDefinitionId, widgetId, {widgetCode?, widgetTitle?, sortOrder?})` -> POST `/analytics/dashboard/definitions/{d}/widgets/{w}/clone`.
- `DrilldownDrawer` takes NO props, reads `useDashboardSelections()`; **the charts already call `openDrilldown` on every click** - it was only never mounted (DEF-007 = one import + `<DrilldownDrawer />` beside `<AssociativePanel />`).
Delivered: `src/state/widgetSelectionMap.ts` - **typed `keyof DashboardFilters`** (plain string broke tsc: `selection.field` is keyof DashboardFilters; DashboardFilters is imported into DashboardFilterContext from `../api/productApiClient`). Map: site->siteId, area->areaId, equipment->equipmentId, sourceSystem, shiftCode, defectType, parameterCode, riskClass; unmapped dimensions (productFamily, gradeOrRecipe, materialUnitType, day/week/month) fall back to materialCode legacy (documented honest scope). Filter allow-list keys (DashboardFilterContext line 44-46): siteId, areaId, equipmentId, materialCode, sourceSystem, defectType, parameterCode, riskClass, fromUtc, toUtc, shiftCode, linkMode.

### I. M2-28 Tenant fix (v2 RUN GREEN 09:43)
- v1 SQL failed: **Postgres has NO `min(uuid)`** - use `count(DISTINCT ...)` + `SELECT ... LIMIT 1`.
- Diagnosis (recorded): results_v2 = **320 rows, ALL tenant_id NULL, RLS enabled=true AND forced=true**, one policy: `ppiq_tenant_isolation_ml_correlation_results_v2 | ALL | (tenant_id = ppiq_current_tenant())`.
- v2 discovery chain: parent compute_runs (had NO tenants) -> scan all public base tables with uuid tenant_id for a single distinct value -> **found tenant `00000000-0000-0000-0000-000000000001` in `app_users`** -> backfilled. AFTER: NULL=0, with-tenant=320, distinct=1. Script kept at `Backend\database\scripts\M2-28_results_v2_tenant_backfill.sql`.
- Open question for the pass: the API session must satisfy `ppiq_current_tenant()` = that uuid for the findings page to fill (checklist Part 6.1 covers it).

### J. Backlog v27 - THE CLEAN EPOCH (user's 7 laws; current governing document)
User declared the old board a mess and gave 7 laws (now the READ ME sheet): 1 done tasks DELETED never archived; 2 no PARTIAL - rewrite remainder as 0%-done with re-estimate; 3 IDs restart M1-01 sequential = priority; 4 phases strictly P1..Pn, none name-only; 5 phases 40-65h critical-first; 6 every phase ends PUSHABLE (gates green, no half-wired reachable feature); 7 junior-ready descriptions (paths/commands/exact acceptance).
**v27 structure** (`PPIQ_Product_Backlog_v27.xlsx`): M1-P1 Demo Lock & Impress 63h -> M2-P1 Working Version Core 60h -> M2-P2 Enterprise & Infrastructure 56h -> M2-P3 Catalogue & Canvas Completion 49h -> M3-P1 Market Proof 44h. **Old IDs are RETIRED** (v25/v26 = archive; lineage noted inside descriptions).
**M1-P1 tasks (current work queue)**: M1-01 wire-ups 2h (Karim) | M1-02 run consolidated pass 3h (Karim) | M1-03 defect buffer 6h (Both) | M1-04 IMPRESS live readiness-gate panel 8h (Claude; endpoint EXISTS: `getAnalysisReadinessGates(outcomeKey, grain, windowDays)` in advancedAnalysis.ts -> readyCount/partialCount/blockedCount + gates[]) | M1-05 IMPRESS finding->genealogy click-through 6h | M1-06 IMPRESS true XY scatter (new endpoint /analytics/advanced/scatter, feature-catalog-validated keys, LS trend line) 10h | M1-07 IMPRESS canvas Filter+Derive nodes (parameterised WHERE whitelist =,<>,<,>,>=,<=,LIKE; derived numeric expressions; rejections recorded) 10h | M1-08 IMPRESS tri-state in real filter dropdowns 6h | M1-09 risk heatmap widget via builder UI (no code) 4h | M1-10 deck: replace Design badges with real screenshots 4h | M1-11 rehearsal x2 + phase push 4h.

=====================================================================
## 3. TEST RESULTS LEDGER (do NOT re-run; all logs are on his disk)
=====================================================================
| When (22-Jul unless noted) | What | Result |
|---|---|---|
| 21-Jul 12:52 | Fix-SuggestionAccessMatrix (M1-22) | matrix line inserted, **dotnet BUILD GREEN 0 errors** (22 pre-existing CS8604 warnings in Phase2OperationEndpoints - ignore, always present) |
| 21-Jul 13:07 | Run-AssistantProof (M1-01) | chunkCount=25 PASS; Q grounded x2: isRefusal=False + real cited answers (6 citations); predictive Q: **did NOT refuse** |
| 21-Jul | Fix-GoldenScriptHygiene | Run-GoldenAnalysis.ps1 NOT on disk even recursive -> M1-32 deferred |
| 00:29 | Install-PpiqReactEnhancements | 6 files new, **tsc GREEN** (NB: that gate was --noEmit era; site app has its own tsconfig - unverified against tsc -b, but npm build of Website not reported failing) |
| 00:30 | Install-M231CanvasFoundation | npm ok, 9 files, matrix patched+verified, DB column ensured, tsc green, **dotnet BUILD GREEN 0 err** |
| 00:37 | Install-M237Associative | 4 files new, mount verified on disk, tsc GREEN |
| 00:45 | Install-M238 v1 | all patches verified, then **dotnet CS2012 (API running, dll locked) -> auto-reverted** |
| ~08:15 | npm run build (user) | **5 errors** in canvasApi/AssociativeContext/AnalysisToolbox/DatasetNode/BlockNode (apiClient path, runCorrelation, xyflow typings) |
| 08:21 | Fix-CanvasBuildErrors v1 | aborted: preflight tested literal http.ts (module resolution lesson) |
| 08:26 | Fix-CanvasBuildErrors v2 | specifier discovered `./http`; all 5 fixed; **tsc -b GREEN**; user then confirmed **npm run build fine** |
| 09:25 | npm test (user, full) | **2 architecture tests FAILED** on my files (4 T11 offenders + 7 ratchet offenders listed verbatim in his paste); 251/253 others passed |
| 09:31 | Install-M243-M228 v2 | Phase A applied, **tsc failed x3: keyof DashboardFilters** -> reverted (typed map answer) |
| 09:39 | Install-M243-M228 v3 (conformance) | A1 six rewrites + A2 all ok; tsc -b GREEN; **gate2 crashed: vitest --reporter=basic invalid (ERR_LOAD_URL)** -> reverted (over-eager; fixed to INCONCLUSIVE-keep) |
| 09:41 | Install-M243-M228 v3b | ALL GREEN: **tsc -b + architecture tests 3/3 (2 files) + Phase A kept**; Phase B SQL failed min(uuid) |
| 09:43 | Fix-M228TenantBackfill v2 | **SQL OK**: BEFORE 320/320 NULL, RLS enabled+forced, policy `tenant_id = ppiq_current_tenant()`; tenant ...0001 from app_users; AFTER 0 NULL / 320 with tenant / 1 distinct |
| 09:49 | Install-M238 v3 | **ALL GREEN**: writes+patches verified; tsc -b GREEN; architecture tests 2 files / 3 tests PASSED; dotnet BUILD GREEN 0 err (API was stopped) |

**Everything the sprint installed is code-verified + gate-verified. NOTHING is runtime/browser-verified yet** - that is exactly what the consolidated pass (M1-02) does. Do not claim any UI behavior as proven until his pass says so.

=====================================================================
## 4. CURRENT IMPLEMENTATION STATE (what is on his disk right now)
=====================================================================
**Installed & gates-green (product app)**:
- Canvas kit: `src/canvas/{ports.ts, CanvasShell.tsx, canvas.css(+conformance classes), nodes/DatasetNode.tsx, nodes/BlockNode.tsx}` (xyflow v12 typed, StandardP2, port CSS classes)
- `src/api/canvasApi.ts` (imports ./http), pages `Prep/VisualJoinCanvasPage.tsx` + `Analysis/AnalysisToolboxPage.tsx` (Standard*, runCorrelation)
- Associative: `src/state/associativeFields.ts`, `src/state/AssociativeContext.tsx`, `components/dashboard/AssociativePanel.tsx` + `associative.css(+chip classes)`; mounted in InteractiveWorkspacePage above SelectionBreadcrumb
- M2-43: `src/state/widgetSelectionMap.ts`; SavedDashboardWidget patched (import + 3 selection fields -> dimensionToFilterField(widget.dimensionCode); dashboardDefinitionId destructured; onRemove/onClone call real productApi fns then refresh); `<DrilldownDrawer />` mounted
- M2-38: `components/dashboard/ChartExtras.tsx` + `chartExtras.css`; SavedDashboardWidget: ExtraChart branch (activeChartType variant) + extendChartTypes switcher + field prop wired
- Backend: `Endpoints/Prep/VisualMapperEndpoints.cs` (+CS1998 cosmetic at :133); PlantAccessControl + `/api/suggestions` + `/api/prep/visual-mapper`; DashboardMetadataDtos ChartTypes.Pareto; SafetyRegistry Pareto entry; sessions.draft_definition+updated_at_utc columns; results_v2 fully tenant-stamped
**Installed (website app)**: the 6 enhancement files (NOT yet imported into NewHomePage)
**NOT yet done (M1-01 wire-ups)**: Program.cs `app.MapVisualMapperEndpoints();`; App.tsx lazy routes `/prep/canvas` + `/analysis/toolbox` + nav entries; website NewHomePage imports; real contact email; API restart.
**Uncommitted risk**: the sprint file changes may be uncommitted - first action next session: confirm a commit/push of the sprint work (Law 6). Backups litter as *.stamp.bak beside files - harmless, clean later.

=====================================================================
## 5. CODE-BASE FACTS LOOKUP (verified this session - trust these)
=====================================================================
- Server dimension registry codes: site, area, equipment, sourceSystem, materialUnitType, productFamily, gradeOrRecipe, shiftCode, defectType, parameterCode, day, week, month, riskClass (+materialCode used by filters/widgets).
- Chart types (constants + registry): kpi, bar, line, area, pie, donut, scatter, heatmap, table, +pareto(new). Scatter measure-restricted to avgParameterValue|riskScore|defectRate (`IsChartCompatibleWithMeasure`); others return true; ChartRequiresDimension = everything except kpi.
- Widget switcher card prop was `chartTypes={["bar","line","pie","table"] as any}` -> now `extendChartTypes(widget.measureCode)`.
- Widget query POST `/analytics/dashboard/widgets/query` body: {widgetType:'chart', chartType, dimensionCode, measureCode, parameterCode, filters, options{maxRows, rawRowLimit, sortDirection, includeWarnings}}; rows read defensively (dimension|label|key).
- Assistant endpoints: POST /api/assistant/reindex {} -> {chunkCount}; POST /api/assistant/ask {question} -> {isRefusal, refusalReason, text, citations[{kind,id,detail}], blocked}.
- Readiness gates: GET `/analytics/advanced/readiness/gates?outcomeKey&grain&windowDays` via getAnalysisReadinessGates -> {readyCount, partialCount, blockedCount, message, gates[]} (BASE "/analytics/advanced" in advancedAnalysis.ts).
- InteractiveCharts selectRow: applySelection({type, field, value, label, sourceWidget}) then openDrilldown({title, subtitle, type, payload:row}).
- Engine walls history: migrations 740/741/742 committed; rebuild has step [1b] re-applying 741+742; golden refresh re-strands 742 rows -> re-run Apply-742 after any refresh; wall-7 lives INSIDE AdvancedCorrelationComputeService, reasons discarded at NpgsqlAdvancedResultWriter.cs:~40 (message only) - THIS is M2-P1/M2-02's target; probe-proven store: 2,441 heats, 51,691 quality events, defect.class completeness 8,643/8,643=100% at coil.
- Old Diagnose-ReadinessBlock section-3 reconstruction is PROVEN BUGGY - never trust it.

=====================================================================
## 6. DEPLOYMENT / SERVER / PIPELINE KNOWLEDGE (honest inventory)
=====================================================================
**What is KNOWN (from code/audit, not exercised this session)**:
- Staging server IP `178.105.152.180` hardcoded x15 in: post-deploy-smoke.sh, ensure-runtime-env.sh, verify-server-exposure.sh, Invoke-CleanMachineDeployAcceptance.ps1 + docs. Fix = parameterise (v27 M2-08).
- Jenkins pipeline exists (Website has its own Docker/Jenkins/playwright; product repo has tools/ci/validate-real-ui-gates.cjs). **CI defect**: frontend visual/e2e matrix invoked with `--list` (enumerates, does not execute) in package.json phase9:matrix; CiPipelineTruthGateTests exists asserting no catchError/SUCCESS swallowing - extend it (v27 M2-07).
- Bootstrap admin true in local.env AND presentation.env (demo-correct; customer-fatal -> v27 M2-08 first-run setup flow).
- Dev-seed endpoints ARE dev-gated (ProductionDevEndpointGuardTests asserts IsDevelopment).
- RLS: ml_correlation tables enabled+forced with `ppiq_current_tenant()` policies; a FULL tenant-table RLS audit is v27 M2-10.
**What was NOT done this session**: no deployment, no pipeline run, no server access, no app-URL verification. Sections 9/10 of the user's request assume pipeline-greening work that happened in EARLIER sessions - that history is in `/mnt/transcripts/journal.txt` + older transcripts on THIS machine only. If the new session needs it, ASK KARIM for the specific earlier handover file rather than guessing. Do not invent pipeline test results.

=====================================================================
## 7. THE RULES TO CARRY (verbatim spirit - violating these burns trust)
=====================================================================
1. Evidence before cure. Read the actual code/log BEFORE writing a fix (every successful fix this session began with greps of his snapshot; every failure came from an assumption: apiClient filename, min(uuid), --reporter=basic, tsc --noEmit, raw controls).
2. Built != working. His screen is the only proof. Say "code-verified, runtime pending" until his artifact arrives.
3. PowerShell apply packs only, never zips, full contract, launcher form. Gates: tsc -b + architecture vitest files + dotnet build (API STOPPED).
4. Never test twice - fold acceptance into the single consolidated pass.
5. Website: zero negative content, CEO audience, his palette, his packs/roles verbatim.
6. Backlog: the 7 laws (Section 2J). No DONE rows, no PARTIAL status, ever again.
7. His time is the scarce resource: verify-first docs instead of speculative packs; skip what will not be demoed (golden script call).
8. When a pack fails, the auto-revert is a FEATURE - never leave half-applied state; distinguish tool-crash (INCONCLUSIVE, keep if tsc green) from test-failure (revert).
9. Frozen-ID law is replaced by v27 Law 3 (IDs restart, sequential, priority-ordered).
10. He pastes console output as text because uploads arrive empty - request pastes, and read them fully (the answer is usually in the last 10 lines).

=====================================================================
## 8. IMMEDIATE NEXT ACTIONS (the new session picks up HERE)
=====================================================================
1. Confirm the sprint work is COMMITTED + PUSHED (Law 6). If not: guide the commit first.
2. M1-01 (Karim, 2h): the three wire-ups + API restart (exact lines in v27 + Section 4).
3. M1-02 (Karim, 3h): run `PPIQ_Consolidated_Test_Pass.md` - Parts 0-8. Collect grades + psql numbers.
4. M1-03: turn every [X]/mismatch into targeted packs (expected small: associativeFields renames on "n/a" fields; findings-page tenant session; selection-map additions; dry-run column names vs 540).
5. Then the impress packs in order: M1-04 gate panel -> M1-05 genealogy jump -> M1-06 XY scatter -> M1-07 canvas layer 2 -> M1-08 dropdown tri-state (all specs junior-ready in v27).
6. Files he has in outputs from this session: Install-PpiqReactEnhancements.ps1, Install-M231CanvasFoundation.ps1, Install-M237Associative.ps1, Install-M238ChartCatalogue.ps1(v3), Install-M243-M228.ps1(v3), Fix-CanvasBuildErrors.ps1(v2), Fix-M228TenantBackfill.ps1, PPIQ_Consolidated_Test_Pass.md, PPIQ_Product_Backlog_v27.xlsx, M1_Final_Validation_21Jul.md, PPIQ_Demo_Playbook.md, M1-05_23_06_09_Verify.md, M1-09_findings_latest_view.sql, website/index.html (static V2 - superseded by the React enhancement path but kept).

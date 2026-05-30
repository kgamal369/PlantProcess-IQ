# PlantProcess IQ — Implementation Analysis & Task-List Validation

**Prepared:** 30 May 2026
**Scope:** Line-by-line validation of the current implementation against the 4-Track Vision and the v3 task list (PlantProcessIQ_TaskList_v3_29May2026), plus validation of the v3 task list itself against the 4-Track Vision.
**Evidence base:** 824-file source manifest (240,653 lines), the 9-part code dump (Backend Core/Database/Tests, Frontend App/Misc, Infrastructure, Tools/Validation, Website), the v3 task workbook, and the 4-Track Vision document.

---

## 1. Executive summary

The repository has moved a long way since the v3 list was authored on 29 May. The dump captured one day later (30 May, 11:53) shows that **v3 Phases 1 through 8 (tasks PPIQ-T001–T062) have been executed and validated** — there are matching `apply-*` and `validate-*-acceptance` runners for Phase 3/4, Phase 5/6 and Phase 7/8, a full canonical `src/components/standard/` library with Storybook, an accessibility audit dated 30 May, and the legacy duplicate component paths are gone. Those phases are therefore **>85% complete and have been removed** from the updated list.

What remains, and what was **never adequately specified in any earlier list**, is the part of the vision that actually differentiates the product: **the ML engine** (scheduled correlation-learning jobs, inspection-job→generated-page workflow, and the suggestion/recommendation assistant), **the generic schema-mapping and two-stage delta-import architecture**, **the demo source-database suite at real scale**, **the page builder**, and **the full 5-product website**. The v3 list treated ML as a single UI-migration task (T040) and omitted the engine entirely — which is exactly the gap you flagged.

The updated task list (v4) closes the genuine residuals from the "done" phases, fixes the defects introduced along the way, and adds the missing capabilities so that **completing v4 brings the 4-Track Vision to 100%**.

| | v3 (29 May) | Current reality (30 May) |
|---|---|---|
| Tasks | 91 across 13 phases | Phases 1–8 (T001–T062) done; 9–13 (T063–T091) untouched |
| Track 1 Workflow | 44% | ~55% (UI + connectors + jobs + widget compiler done; **schema-mapping, delta-import, page-builder, ML engine open**) |
| Track 2 Hardening | 38% | ~70% (standard UI, a11y, security headers, audit immutability done; **resilience/action-matrix/heatmap open**) |
| Track 3 Demo | 36% | ~35% (demo-reset + lifecycle done; **source-DB suite, 100K dataset, scripts/personas open**) |
| Track 4 Website | 50% | ~55% (proof site + pricing matrix exist; **5 products, logo set, legal open**) |
| **ML engine (Vision §3)** | **not in plan** | **~20% — analytics primitives only; engine/inspection-jobs/suggestions missing** |

---

## 2. Validation method

For each vision clause and each v3 task I checked three things against the actual repository: (a) **does the artifact exist** (manifest path + line count + last-write date); (b) **is it wired/used** (import and call-site grep across the dumps); and (c) **was it validated** (presence of an acceptance runner that references the task ID and asserts the contract). A task is treated as "done >85%" only when all three hold. Where an artifact exists but is unwired or unvalidated, it is carried forward as a residual.

---

## 3. v3 task-list completion verdict — phase by phase

The single strongest evidence signal: **no task ID above PPIQ-T062 appears anywhere in the 240,653-line dump** — no source reference, no validation runner, no fix script. Combined with the dated `apply-*`/`validate-*-acceptance` runners for Phases 3/4, 5/6, and 7/8, this draws a clean line: **Phases 1–8 executed; Phases 9–13 untouched.**

| Phase | Name | v3 tasks | Verdict | Primary evidence |
|---|---|---|---|---|
| P01 | P0 Critical Security & Brand | T001–T008 | ✅ Done (7 of 8) | Forbidden phrase only in guards/tests/lint; `5432` bound to `127.0.0.1` (compose L496); bcrypt placeholder gone; HSTS+CSP+X-Frame present; `;plantadmin` fix script + token located; bootstrap/credential fix scripts. **T005 auth-matrix probe NOT built** (only material-search probes). |
| P02 | Standard UI Component System | T009–T018 | ✅ Done | Full `src/components/standard/*`: Button 133 L, Table 486 L, Tabs 173 L, Fields 370 L, Surface 283 L, boundaries, `tokens.ts`, 744 L CSS, Storybook stories + **static build**, button/table/tabs/input inventories. |
| P03 | Component Consolidation | T019–T024 | ✅ Done | Exactly one `StandardButton`/`DataFetchBoundary`/`ErrorBoundary`; legacy `src/hardening/` retains only `actionMatrix`+`routeContracts`; ESLint rules, codemod, validators present. |
| P04 | Material Search Standardization | T025–T032 | ✅ Done | `phase3/` apply + finalize + `validate-phase3-phase4-acceptance` runners; material-search probes; hardening codemods. |
| P05 | Analytics Pages Migration | T033–T039 | ✅ Done | `phase56/` apply + `validate-phase5-phase6-acceptance`; visual-regression + e2e specs; a11y audit. |
| P06 | Intelligence & System Migration | T040–T047 | ✅ Done | Same `phase56` runners; `docs/a11y/audit-30May2026.md`. Note T040 (ML Readiness) was **UI migration only** — not engine. |
| P07 | Demo Lifecycle & Orphan Wiring | T048–T054 | 🟡 Mostly done (6 of 7) | `SaveInspectionJobModal` + `OperationProgressPanel` re-touched 30 May and wired (2 / 8 call-sites); `POST /demo-lifecycle/reset` + progress live; reset UI; tier-override pushed to green via fix script; routes. **T048 `Phase1WorkflowTruthPanel` still 0 importers.** |
| P08 | Widget Script Layer Compiler | T055–T062 | ✅ Done (≈7 of 8) | Entity 218 L + `WidgetExpressionStatus` enum + 7 columns; EF map + migration 117; `WidgetQueryExpressionService` 418 L; validation service + safety registry; `WidgetScriptStep` editor; unit/domain tests. **T058 `SchemaViewResolver` not present by name** (validation folded into other services). |
| P09 | Source-DB Connector Demo Suite | T063–T069 | ❌ Not started | No `docker-compose.demo-sources.yml`, no live source containers; smoke tests only for CSV/Excel. Connectors themselves exist (see Track 1). |
| P10 | Demo Track Storytelling | T070–T075 | ❌ Not started | No demo scripts, recovery lines, personas, canonical-layout doc, or recording. |
| P11 | Hardening Completion | T076–T081 | 🟡 Partial | Validators exist but T079/T080 not proven as Jenkins gates; **no HeatMap (T076)**; tier banner (T077) pending; some contract tests present. |
| P12 | Website & Legal | T082–T086 | 🟡 Partial | Proof site + pricing matrix exist; **no NDA/SOW/DPA**; pricing/CTA wiring + screenshots pending. |
| P13 | Documentation & Runbook | T087–T091 | ❌ Not started | No deployment/incident runbooks, onboarding, grammar reference, or security-architecture docs found. |

**Removed from the updated list (>85% complete):** P01–P08 = **T001–T062**, minus the residuals in §6. **Retained / rewritten:** P09–P13 plus the missing capabilities in §4.

---

## 4. Implementation status vs the 4-Track Vision

### Track 1 — Workflow

**Built and working.** All six source connectors exist and are non-trivial: `PostgreSqlConnector` (323 L), `MsSqlConnector` (401 L), `MySqlConnector` (389 L), `OracleConnector` (494 L), `CsvConnector` (505 L), `ExcelConnector` (407 L), behind a `DataSourceConnectorFactory` and a 1,246-line `ConnectorConfigurationService`. The job system is real — `JobDefinition` (233 L), `JobRunHistory`, orchestrator/runtime/registration services, a `PlantProcess.Workers` project (`Worker.cs` 483 L), and an `AdminJobsMonitorTab` (347 L). Licensing is comprehensive: `LicenseService` (379 L), `LicenseAdminEndpoints` (397 L), tier-override (T053) wired and pushed to green via fix scripts, plus `LicenseGate`/`LicenseBadge`/`LicenseUsagePanel` and feature gating. The **widget script layer compiler** (v3 Phase 8) is implemented: `WidgetQueryExpressionService` (418 L), `DashboardWidgetDefinition` (218 L) with the `WidgetExpressionStatus` enum and the 7 new columns, EF mapping + migration 117, validation service + safety registry, the `WidgetScriptStep` editor, and unit/domain tests.

**Missing or thin against the vision.**

- **§1.4 Generic schema mapping** — the vision's hardest requirement (map any plant's imported tables into a generic schema; author views/queries; **join across dump files**, e.g. HSM-Oracle `piece_id` ↔ inspection-MySQL `material_id`; KPI-as-view). Today there is a SQL-view editor *UI* and a `113_phase1_demo_mapping_views.sql`, but no general mapping engine, no `canonical_schema_views` catalog as a first-class registered artifact, and no cross-source join authoring. There is **no `SchemaViewResolver`** by name (v3 T058) — validation is partially covered by `DashboardWidgetValidationService` + the SQL safety registry, but the schema catalog it should resolve against is not a built component.
- **§1.1–1.3, 1.5 Two-stage delta import** — the "copy source into a dump-file store that mirrors the *customer's* structure, compare last index, import the last few records, then a second job maps dump→our generic schema at HMI-refresh rate" architecture is only partially present (a delta-execution service exists per prior work) and is not modelled as the explicit two-stage, dump-copy + last-index-delta design the vision describes.
- **§2.1–2.2 Page builder** — `WidgetBuilderWizard`/`WidgetBuilderWizardContent` exist (1,512 + 1,709 L) and are substantial, but full **page** CRUD, the drag-drop widget *library/canvas*, and persistent user-defined pages (`/pages/{slug}`) are stubs (`/pages/{slug}` was the unimplemented v3 T054).
- **§3 ML engine** — see §4 “The ML layer” below; this is the largest single gap.

### Track 2 — Hardening

**Built and working.** The canonical `src/components/standard/` library is complete — `StandardButton` (133 L), `StandardTable` (486 L), `StandardTabs` (173 L), `StandardFields` (370 L, input/select/textarea), `StandardSurface` (283 L, card/modal/toast), `DataFetchBoundary` (115 L), `ErrorBoundary` (146 L), `tokens.ts`, a 744-line stylesheet, Storybook stories + a static build, and inventories for buttons/tables/tabs/inputs. The **forbidden phrase "could not be loaded / could not load" no longer appears in UI copy** — all 31 remaining matches are in lint rules, Playwright assertions, comments, and the `validate-forbidden-copy.mjs` guard, which is exactly the defensive end-state T001 asked for. Security headers are present in the Caddyfile (HSTS, CSP, X-Frame-Options, etc.), the Postgres port is bound to `127.0.0.1` (T002), the bcrypt placeholder is gone (T003), audit-log immutability is enforced by trigger + tests, and an a11y audit (`docs/a11y/audit-30May2026.md`) exists (T047).

**Missing against the vision.**

- **§4.2 Heat map** — no `HeatMapWidget` yet (v3 T076).
- **§2.1 progress on long calls** — loading boundaries exist, but explicit **progress-percentage** UI for large datasets across heavy endpoints is not systematic.
- **§2.2 / §3.1.1 resilience + action matrix** — no completed end-to-end "every button/hook/recall on every page does its job 100%" test pass, and no systematic PK/FK/null/timeout audit across every endpoint + join.
- **CI gates** — the validators exist as scripts, but wiring the forbidden-phrase repo-wide sweep and the non-standard-import block as enforced Jenkins stages (v3 T079/T080) is unconfirmed.

### Track 3 — Demo

**Built and working.** The demo-reset workflow is real: `POST /demo-lifecycle/reset` (+ progress GET, 1-per-5-min rate limit, WF-022), the reset UI with confirmation, and the now-wired `OperationProgressPanel` (157 L, 8 call-sites) and `SaveInspectionJobModal` (97 L, wired). Synthetic source seeds exist — `synthetic_inspection_source_insert.sql` (6,601 L), `synthetic_hsm_source_insert.sql` (2,821 L), `synthetic_qms_source_insert.sql` (419 L), a unified realistic seed (1,628 L), and Python generators.

**Missing against the vision.**

- **Source-DB suite (v3 P09)** — connectors exist, but there is **no `docker-compose.demo-sources.yml`** and no live source containers. The vision needs eight distinct source systems (PostgreSQL MeltShop EAF/LF, Oracle Caster, Oracle HSM, MSSQL PKL, MySQL Downtime, MySQL Parsytec, Excel Yard, Excel QA) so the demo is configured **live from the HMI** — the Track-3 golden rule ("no hardcoded pages; configure everything from the app"). Connector smoke tests exist only for CSV and Excel.
- **Dataset scale** — the in-repo seed is on the order of ~10K insert rows and covers HSM/inspection/QMS only. The vision asks for **~630 heats, ~5,600 slabs/coils, 100K+ rows, ≈1 month**, with full genealogy (EAF→LF→CC→TF→HSM→SKP→LCT/HCT/STL→PKL→GVL→Yard) including chemistry samples, additives, EAF steps, downtime taxonomy (production-vs-equipment stoppage logic), and QA sampling rules. This is roughly an order of magnitude short and structurally incomplete.
- **Storytelling (v3 P10)** — no demo scripts, recovery lines, personas, or canonical-layout docs.

### Track 4 — Website

**Built and working.** A real proof-oriented site exists: `App.tsx` (533 L), brand config, `PricingLicenseMatrix`, `RequestDemoForm` (162 L), `ConnectorHonestyBlock`, `PositioningTruthBlock`, `ProductScreenshotShowcase`, and the engineer brand brief. The positioning is honest and on-brand.

**Missing against the vision.**

- **Five products** — only PlantProcess IQ is real; MES is mentioned but has no dedicated page; **QES, Yard/Warehouse, and Energy Management are absent**. The vision wants a full page per product (description, benefits, interactive graphics, license, the golden rule).
- **Logo asset set** — the brief exists; the actual full/icon/stacked × color/dark/light/mono asset set does not.
- **Legal templates (v3 P12)** — no NDA / SOW / DPA.

### The ML layer (Vision §3) — the flagged gap, in detail

The product is **honestly positioned as rule-based and statistical**, which is correct and matches your brand voice ("say: rule-based risk scoring, correlation analysis, suspected contributor; never say: AI-powered prediction, guaranteed root cause"). The code carries `no_production_prediction = true` and a `ModelRegistry` baseline named "Transparent rule-based quality risk scorer". The analytics **primitives** exist: `CorrelationService` (437 L), `CorrelationEndpoints` (717 L), `FeatureEngineeringService` (445 L), `MaterialFeatureVector` (125 L), `MlReadinessService` (482 L), and `CorrelationResult` + `ModelRegistry` entities. There is also early job scaffolding — `IsMlJob`, `LooksLikeCorrelationJob`, `EnsureMlJobDefinitions`, `GetMlJobs`.

But the **engine the vision describes is not there**:

- **§3.1 Scheduled learning jobs** — the four off-hours jobs (process-params × defect-id, × downtime-id, × KPI-id, and a weekly overall pass) exist as *definitions* at most; there is no worker that actually runs them on a schedule, computes correlations across all parameters, and persists results for monitoring. `CorrelationService` looks on-demand, not a scheduled batch learner.
- **§3.2 Inspection-job → generated page** — `SaveInspectionJobModal` is wired, but there is **no backend inspection-job runner** that takes a (defect/downtime/KPI id + window + name), runs the engine, and **auto-generates a page of widgets/charts** showing the most influential parameters, with run-now and periodic scheduling. `CorrelationPage.tsx` is a 5-line stub.
- **§3.3 Suggestion & recommendation assistant** — **entirely absent** (a grep for suggestion/recommend returns nothing). The vision wants an assistant-style layer that turns the learning outputs into ranked, evidence-linked recommendations — framed in the same honest, rule-based language.

This is why the new plan dedicates three full phases (v4 P04–P06) to the ML engine, plus the schema-mapping and delta-import phases the engine depends on for clean feature inputs.

### Vision-clause coverage matrix

Status legend: ✅ built & working · 🟡 partial / built-but-unwired / not-at-vision-depth · ❌ missing.

| Vision clause | What it asks for | Status | Note |
|---|---|---|---|
| T1 §1.1 | Access MSSQL/PostgreSQL/MySQL/Oracle/Excel/CSV | ✅ | All six connectors built (323–505 L each) + factory. |
| T1 §1.2 | DB-link config, per-table import, rate 2 min–days | 🟡 | Connectors + config UI + delta service exist; per-table rate scheduling not fully exposed. |
| T1 §1.3 | Dump store = latest copy of customer DBs (not our schema) | ❌ | Dump-copy store not modelled as a first-class artifact. |
| T1 §1.4 | Map tables → generic schema; write views; cross-source joins; KPI-as-view | ❌ | SQL-view editor UI only; no mapping engine, view catalog, joins, or `SchemaViewResolver`. |
| T1 §1.5 | Dump → app import job at HMI-refresh rate | 🟡 | Delta-execution exists; not modelled as the explicit second stage. |
| T1 §1.6 | Jobs Monitor: status / last-run / duration / crash | 🟡 | Full job system + `AdminJobsMonitorTab` exist; unified monitor incl. ML pending. |
| T1 §2.1 | Create/edit/delete pages; widget library drag-drop; save | 🟡 | `WidgetBuilderWizard` built; page CRUD + drag-drop canvas + `/pages/{slug}` are stubs. |
| T1 §2.2 | Widget edit → link DB values + script layer | 🟡 | Compiler + `WidgetScriptStep` built; live-builder integration pending. |
| T1 §3.1 | Scheduled correlation-learning jobs (defect/downtime/KPI/weekly) | ❌ | Job defs scaffolded; `CorrelationService` on-demand; no scheduled learner. |
| T1 §3.2 | Inspection job → generated page → save → schedule | ❌ | Modal wired; runner + page-gen missing; `CorrelationPage` a 5-line stub. |
| T1 §3.3 | Suggestion & recommendation assistant | ❌ | Entirely absent. |
| T1 §4 | Lite/Pro/ProPlus/Enterprise + live toggle demo | ✅ | `LicenseService` + gating + tier-override built; toggle banner UI (T077) pending. |
| T1 §5 | User / Role / Admin | ✅ | Auth, roles, admin endpoints present. |
| T2 §1 | Standard styling for buttons/tables/tabs/alignment | ✅ | Standard library + Phase 4–6 migrations. |
| T2 §1.6 | Responsive on any screen/browser, http + https | 🟡 | Not systematically tested across viewports/browsers. |
| T2 §2.1 | No hang/lag; progress % on large datasets | 🟡 | Loading boundaries yes; progress-% UI not systematic. |
| T2 §2.2 | Resilient APIs/SQL/joins (PK/FK, no surprises) | ❌ | No systematic edge-case/timeout audit. |
| T2 §3.1.1 | Every control on every page tested | ❌ | Action-matrix scaffold only. |
| T2 §3.2 | ML/correlation/suggestion ready | ❌ | Engine missing (see T1 §3). |
| T2 §4.2 | Heat map works | ❌ | No `HeatMapWidget` (T076). |
| T2 (brand) | Eliminate "could not be loaded" | ✅ | Only in guards/tests/lint now. |
| T3 | No hardcoded pages; configure live from HMI | 🟡 | Architecture supports it; live-config proof path pending. |
| T3 | 6+ separate source DBs with the dataset | ❌ | No source containers. |
| T3 | 100K+ rows / ~630 heats / ~5,600 coils / ~1 month | ❌ | ~10K rows; HSM/inspection/QMS only. |
| T3 | Demo-reset lifecycle | ✅ | Reset endpoint + progress + UI built. |
| T3 | Personas / scripts / canonical layout | ❌ | Not started. |
| T4 | Brand identity (palette / typography / logo) | 🟡 | Palette + fonts in place; full logo asset set missing. |
| T4 | Shiny, evidence-grade proof site | ✅ | Proof site with pricing matrix + request-demo form. |
| T4 | Five full product pages (PPIQ/MES/QES/Yard/Energy) | ❌ | PPIQ partial; MES mentioned; QES/Yard/Energy absent. |
| T4 | Legal (NDA / SOW / DPA) | ❌ | None. |

---

## 5. Validation of the v3 task list against the vision

The v3 list was a strong **hardening-and-standardization** plan — 45 of 91 tasks were Track 2, almost entirely the UI-standard mandate — and it executed well. As a plan to *complete the vision*, it had three structural gaps:

1. **No ML engine.** ML appears only as T040 ("migrate ML Readiness page to Standard* components") — a UI task. Vision §3.1–3.3 (the scheduled learning jobs, the inspection-job→page workflow, and the suggestion assistant) had **zero** implementation tasks. This is the gap you noticed.
2. **No generic schema-mapping or two-stage delta-import tasks.** Vision §1.1–1.5 — arguably the core of "generic" — was represented only by UI-migration of the existing SQL-view editor (T043), not by tasks to build the mapping engine, the canonical view catalog, the cross-source join authoring, or the dump-copy/last-index delta architecture.
3. **No page-builder tasks.** Vision §2.1–2.2 (create/edit/delete pages, drag-drop widget library, widget script layer in the builder) was implied but never given explicit tasks beyond the widget *compiler* (Phase 8).

The v3 list was also internally accurate where it did reach: the orphan-wiring tasks (T048–T050), the demo-reset endpoint (T051), the tier override (T053), and the widget script layer (T055–T062) all map to real, now-built artifacts. Two residuals remain inside the "done" set — see §6.

---

## 6. Residuals and defects inside the "done" phases

These are the "mistakes / get back on track" items — places where a v3 task was marked addressable but the contract is not fully met, or where a fix introduced a new risk.

- **T005 — JWT auth-matrix probe is incomplete.** Only the four `scripts/probes/material-search/*.ps1` exist; there is **no `auth-matrix-probe`** hitting every admin endpoint group under no-token / operator / admin and asserting 401/403/200. (This is also listed as outstanding in your own notes.) → v4 hotfix.
- **T048 — `Phase1WorkflowTruthPanel` orphan unresolved.** The file (285 L, last touched 25 May) still has **0 importers**; it appears superseded by `ConnectorTruthPanel`. Either wire it or delete the dead file. → v4 hotfix.
- **T058 — no `SchemaViewResolver`.** Widget-expression validation works via the validation service + safety registry, but the named schema resolver and a first-class `canonical_schema_views` catalog do not exist; this also blocks clean §1.4 mapping. → folded into v4 P02.
- **Caddyfile CSP host mismatch (new risk).** The CSP `connect-src` references `api.plantprocessiq.com`, while production runs on the `178.105.152.180.sslip.io` wildcard. If the frontend calls the sslip.io API host, **CSP will block those XHRs in production**. Re-tune `connect-src` to the real API origin(s), validate in report-only, then enforce. → v4 hotfix.
- **`;plantadmin` token (close on prod).** A fix script exists that strips the stray trailing `plantadmin` token from a seed string; the closing step is to confirm it has been applied on the Hetzner staging clone and to sweep all seed/migration scripts for any other stray trailing identifiers, with a CI guard. → v4 hotfix.
- **T002 full-surface audit.** Postgres is loopback-bound, but the "audit every other service (redis/seq/jaeger/grafana/prometheus) and bind to loopback unless intentionally public" sweep should be completed and proven with an external scan. → v4 hotfix.

---

## 7. Recommended remediation plan (v4 outline)

The validation above converts directly into a remediation plan. In the updated task list, I would:

- **Remove** every v3 task that is >85% complete (Phases 1–8, T001–T062), minus the residuals in §6.
- **Carry forward** the genuinely remaining v3 work (Phases 9–13, T063–T091), rewritten and expanded where the vision needs more depth, each cross-referencing its original v3 ID.
- **Add the residual hotfixes** from §6 as a Phase 1 closeout (auth-matrix probe, orphan resolution, CSP host fix, `plantadmin` close-on-prod, loopback sweep, CI-gate wiring).
- **Add the missing capabilities**: the ML engine across three phases (scheduled correlation learning; inspection-job → generated page; suggestion/recommendation assistant), the generic schema-mapping + view-catalog engine, the two-stage delta-import architecture, the page builder, the demo source-DB suite at real scale, and the full 5-product website.
- Keep the **exact column contract** of the previous version — Task ID · Task Name · Phase ID · Severity · Track · Module · Submodule · Description · Task Type · Validation · Effort (hrs) — with the same brand styling and the supporting How-to-Use / Phase Summary / UI Standards Reference / Rollups sheets.

When that plan is complete, all four tracks reach the depth the vision specifies: a generic data lifecycle end-to-end; a real, honestly-framed ML engine (scheduled learning + generated analysis pages + a recommendation assistant); a hardened, resilient HMI; a live-configurable demo on real source databases at scale; and a website that sells all five products.

*This document is the analysis-and-validation report (part 1). The updated task-list workbook (part 2) is produced separately on request.*

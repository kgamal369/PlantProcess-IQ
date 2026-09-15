# PlantProcess IQ — Implementation Aspect & Persona Scoreboard, Design Validation and Review

**Revision:** v2 — validated and extended against the implementation snapshot
**Review date:** 2026-09-08
**Implementation snapshot:** UltimateAudit generated 2026-09-08 13:53 (2,698 files / 498,737 lines / 32.56 MB, `Mask Secrets: False`)
**Execution authority:** Backlog v2.19.0 workbook (`PPIQ_Backlog_08Sep2026_v2.xlsx`, Verified Task Ledger dated 08-Sep-2026)
**Design authority:** Master Design v4.10.x, six chapters only (workbook `Current Authority` sheet)
**Method:** every finding in this revision was re-checked by reading the file inside the audit package. Findings carry `path:line`. Nothing below was carried forward from the v1 review without re-reading its source. Where v1 was wrong, the v1 claim is retracted in §0 rather than silently corrected.

---

## 0. Validation of the v1 review against the snapshot

The v1 review is structurally sound: its aspect model, persona model, scoring bands and evidence-confidence ladder are kept. Its claims were then read against source. Result: **24 claims confirmed, 1 retracted, 6 revised, 12 sharpened with exact evidence, and 2 numbers in the v1 executive summary were carried into this revision unchanged after re-checking the workbook (513/993 h = 51.7 %, P1 205/217 h = 94.5 %, P2 80/320 h = 25.0 %).**

### 0.1 Retracted — v1 finding #1 (P0) "Jenkins E2E is gated off while the CI truth test forbids it"

**Wrong.** The current `Jenkinsfile` stage 5 is `stage('5. Frontend e2e - BLOCKING')` with `steps { sh 'bash deploy/scripts/ci-e2e-stack.sh' }` and **no `when {}` clause** (`Jenkinsfile:107-108`). The script it calls raises an ephemeral compose stack, waits for `/health`, runs `npm run e2e`, and tears down in an `EXIT` trap (`deploy\scripts\ci-e2e-stack.sh`). The gated stage v1 quoted exists only in three archived copies under `deploy\.ppiq-backups\ci-truth-gate-*-20260710-*\Jenkinsfile:107-108` and in the XML-doc comment of the test itself, which says the stage *"previously carried"* that clause (`CiPipelineTruthGateTests.cs:97`). `E2e_stage_cannot_be_gated_off()` (`:100-119`) would pass against the current file.

v1 read a backup, not the live file. This is the exact failure mode the project's own rule guards against ("source documents must be read directly"). The v1 P0 #1, R6 and step 1 of the v1 sequence are withdrawn. What survives is a smaller hygiene item: `deploy\scripts\ensure-runtime-env.sh:38` and `Ensure-RuntimeEnv.ps1:26` still write `PPIQ_RUN_E2E=off` into the runtime env — a dead knob that no consumer reads (bug #24 below).

### 0.2 Revised

| v1 claim | Verified state | Revision |
|---|---|---|
| Audit shows "20 CRIT" incl. three `__DefaultConnection` hits | All three `__DefaultConnection` hits are the scanner's own regex (`tools\GeneratePlantProcessIQ_UltimateAudit*.ps1:671/690/741`). Real CRIT after removing self-reference and the guard test: **8** (`package.json:84`, `validate-real-ui-gates.cjs:13-15`, `apply-phase5-phase6-full-ui-migration.cjs:74-76`, `phase9:matrix`) | Executive CRIT = 8, all of one family (`--list` enumeration) |
| OPC production runtime ~20 % | `OpcUaHistorianConnector.cs` (157 lines) is a configuration validator; `requireLiveHandshake` always fails with "no customer historian gateway is attached". **No OPC UA client package exists in any `.csproj`** (`PlantProcess.Infrastructure.csproj:9-16` lists Npgsql, SqlClient, MySqlConnector, Oracle, ClosedXML only) | Realization **10 %**. Honest code, but zero protocol capability |
| Jobs: "serial foreach, not final distributed plane" | Confirmed (`ImportBatchQueueProcessorService.cs:53`). Stronger: **zero** occurrences of `Task.WhenAll`, `Parallel.For*` or `SemaphoreSlim` in `PlantProcess.Application/Infrastructure/Api/Workers` | Parallelism realization ~5 %, not 30 % |
| "God files: SharedAuthoringShell ~1.1k, CustomerAssessmentEngine ~1.6k" | Largest file is missed: `WidgetResultSources.cs` reports 7,537 lines, of which 2,116 are non-blank — the file (and two others) is blank-line-inflated 3.5× by a line-ending fault. Real top-5 by non-blank lines: WidgetResultSources 2,116; CustomerAssessmentEngine 1,617; V5PrivateModelGatewayCertificationEndpoints 1,567; DashboardWidgetQueryService 1,505; DashboardDefinitionService 1,348; Program.cs 1,269 | Add the inflation defect (bug #21) and correct the size table |
| Two vacuous `Assert.True(true)` tests | `Phase04TenantIsolationProofTests.cs:11` is vacuous (confirmed). `CurrentEngineCompatibilityTests.cs:161` is a documented diagnostic probe ("records leads; does not hold the fixture hostage") — not vacuous evidence, but it is a `[Fact]` that always passes and inflates the green count | Keep #7; add the second as a low-severity test-truth item |
| Eight-role model "35 %, UI vocabulary only" | Backend already has `enum FormalPlantRole` with Executive, ChiefExecutiveOfficer, Operator, Viewer, PlantAdmin (`FormalRoleAccessMatrix.cs:12`). The **wire mapping collapses it to four strings with `_ => "viewer"` as the default** (`:281-289`); frontend union is `"Admin"\|"DataManager"\|"Engineer"\|"Viewer"\|string` (`apiClient.ts:24`) | Gap is in the mapping and the surface, not the catalogue. Realization 45 % |

### 0.3 Confirmed verbatim (evidence attached)

`SharedAuthoringShell.tsx:618` `canonicalEntity: "canonical_material_units"` · `AdminDbConfigurationTab.tsx:97` `PROVIDER_DETAIL` second capability truth · `ProductScreenshotShowcase.tsx:43` and `phase1WebsiteProof.ts:170` "6 live source systems" · `JobRunOrchestratorService.cs:178,182` "no manual executor" / "not implemented yet" · `AuthOptions.cs:35` `RequireAdminMfa = false` · `AssistantTools.cs:85-89` `public.canonical_material_units` / `material_unit_count` · `PageBuilderPage.implementation.tsx:58` four-role `audienceRoleOptions` · four `productApi as legacyApi` slices (`admin.api.ts:1`, `analytics.api.ts:1`, `dashboarding.api.ts:4`, `integration.api.ts:1`) · `AuthoringSupportEndpoints.cs:42,370` enum-driven method palette awaiting `ml_method_definitions` · hardcoded `178.105.152.180` in 17 lines · `Mask Secrets: False` · workbook `Progress Dashboard` already records "T-245: Backlog said In Progress, but dependency contract keeps it queued".

### 0.4 Workbook consistency notes (not implementation defects)

- T-094 is **Done** in the Verified Task Ledger with four commits (`377aabdf…; 6ab97ee2; e74ecd1c; 5a967f1b…`). Any session note that still treats "T-094 Stage 2" as open is superseded by the ledger.
- T-252 (disposable-DB lifecycle) sits in **P5, Not Started** in v2.19. If the ruling that promoted it to M2/P1 stands, the workbook is stale and must be re-cut; if not, `W1-INFRA-TESTS-DBCLONE-01` has no P1 owner.
- Worker 1 carries **281 of 480 remaining M2 hours** (58 %) across 30 open tasks; Worker 2 131 h / 12 tasks; Worker 3 68 h / 7 tasks. P5 alone is 152 h, 140 of them Worker 1, none started. This is the schedule risk for 30 September, independent of any score below.

---

## 1. Executive verdict

PlantProcess IQ is a governed product architecture with validation maturity ahead of operational maturity. That verdict from v1 holds after re-reading source. Three things changed:

1. **CI release truth is better than v1 said.** E2E is a real blocking stage on an ephemeral stack. The remaining CI-truth defects are enumeration-only scripts (`--list`) that must never be wired as gates, and a dead `PPIQ_RUN_E2E` knob.
2. **The execution plane and OPC are weaker than v1 said.** There is no concurrency primitive anywhere in the backend runtime and no OPC UA protocol stack in the solution.
3. **Genericity has a second front.** Beyond the two v1 regressions, the runtime backend still references 64 `public.*` objects across 25 files, still maps material-specific endpoint families (`MapMaterialEndpoints`, `MapMaterialInvestigationEndpoints`, three `*Material*` workflow handlers) and ships a 1,022-line `MaterialAnalyticsPages.tsx`. Rule 1 and Rule 2 are both open in code that is live in the generic binary.

### Executive numbers (revised)

| Measure | v1 | v2 | Why it moved |
|---|---:|---:|---|
| Full-design implementation maturity | 67 | **65** | Jobs, OPC, genericity residue down; CI truth up |
| Functional coverage | 69 | **68** | OPC and parallelism re-rated |
| Design alignment | 66 | **63** | 64 `public.*` runtime refs; phase/version vocabulary in `Program.cs`; material surfaces live |
| Validation evidence | 76 | **78** | Blocking E2E confirmed; 279 backend / 136 FE-unit / 84 E2E / 31 Python test files; zero `.skip/.only` in FE unit suite |
| Production readiness | 53 | **52** | No health-check framework, root containers, plaintext licence key, RLS on 15 tables |
| Executive CRIT (audit, de-duplicated) | 20 | **8** | Scanner self-reference removed |
| M2 nominal-hour completion | 51.7 % | 51.7 % | Workbook, unchanged |
| M2 P1 / P2 / P5 completion | 94.5 / 25.0 / 0 % | same | Workbook, unchanged |

---

## 2. Methodology

Unchanged from v1 (Functional 35 % · Alignment 25 % · Validation 20 % · Production 20 %; bands 90+/75/60/45/<45; confidence ladder Runtime-proven → Test-proven → Source-proven → Planned). One addition:

- **Evidence rule.** A claim about the repository cites a path and line inside the audit package. Claims about backups, comments or documentation describing a *former* state are not evidence of the *current* state. Where a file exists in both a runtime location and a backup/archive location, only the runtime location counts.

---

## 3. Aspect scoreboard (revised)

| # | Aspect | Func. | Align. | Valid. | Prod. | v1 | **v2** | Verdict |
|---|---|---:|---:|---:|---:|---:|---:|---|
| 1.1 | Front End HMI / BI UI/UX | 75 | 70 | 82 | 60 | 72 | **72** | Strong Page Builder/associative core; enterprise BI parity ~58. 104 hardcoded hex in 30 components; 58 `any` in 13 files |
| 1.2 | Canvas / Wiring / SQL Editor | 68 | 66 | 82 | 48 | 67 | **66** | Shell, typed ports, SQL lifecycle strong; T-243/245/246/247 open; material output entity hardcoded |
| 1.3 | Jobs / Monitor / Logging / Parallelism | 48 | 40 | 58 | 32 | 47 | **44** | Zero concurrency primitives; switch-based orchestration; job log correlation partial |
| 1.4 | ML / Engine / Orchestration | 66 | 62 | 80 | 45 | 63 | **64** | MF-01..04 + protocol + isolation tests strong; MF-05..07, active model identity, serving/training split open |
| 1.5 | Backend / API | 84 | 78 | 88 | 72 | 84 | **81** | Versioned definitions, relationship authority, genericity ratchets strong; 31 phase/V5-named endpoint families and material endpoints still mapped |
| 1.6 | User / Role | 62 | 58 | 70 | 48 | 59 | **60** | 8-role enum exists; wire mapping collapses to 4 with silent `viewer` default; no admin surface (T-119/T-120) |
| 1.7 | DB Link / Schema / OPC / Interfaces | 55 | 52 | 66 | 36 | 55 | **52** | CSV/Excel/PG real; SQL Server/MySQL/Oracle drivers present but not certified; OPC has no protocol stack |
| 1.8 | DB / Schemas / Canonical Persistence | 90 | 84 | 90 | 78 | 89 | **86** | Three-schema migration authority real; 64 `public.*` runtime references and RLS on 15 tables keep Rule 2 open until T-251/T-112 |
| 1.9 | License Control | 70 | 62 | 70 | 50 | 66 | **64** | ECDSA-signed artifacts + entitlement projection + events exist; key generated and stored in customer DB, plaintext in `_encrypted` column |
| 1.10 | Security | 74 | 68 | 80 | 52 | 68 | **69** | Rate limiter, HSTS, HTTPS redirect, dev-only Swagger/dev-seed, default-password startup guard; MFA off by default, root containers, partial RLS |
| 1.11 | ChatBot / LLM / Assistant | 74 | 70 | 80 | 55 | 71 | **70** | Planner/retrieval/verifier strong; one OpenAI-compatible HTTP provider serves four advertised provider types; legacy `run_kpi` material tool |

### 3.1 Front End HMI / BI — 72

Confirmed strengths: Page Builder persistence, associative cross-filter state machine, chart compatibility grammar, persisted definition replay gate, 136 unit test files with **no** `.skip/.only/.todo` markers, 84 E2E specs. Weaknesses quantified this revision: 104 raw hex colours across 30 non-theme component files (design-token drift); 58 `any`/`as any` across 13 source files; four API slices dispatch through `productApi as legacyApi`; localStorage still used as dashboard-layout fallback (`DashboardGridLayoutContext.implementation.tsx:10`) though backend layout persistence exists; `MaterialAnalyticsPages.tsx` (1,022 lines) is an industry-specific page in the generic HMI. Enterprise-BI parity remains ~58 (no property inspector, bookmarks, pivot/crosstab, conditional formatting, export fidelity).

### 3.2 Canvas / Wiring / SQL Editor — 66

Confirmed: `SharedAuthoringShell.tsx` (1,173 lines) single-shell model, typed ports/graph refusal, T-242 deterministic algebra, T-244 SQL compile/validate/dry-run/version. Confirmed regression: `doSaveSql()` binds every authored SQL definition to `canonical_material_units` (`:618`). Confirmed open: T-243 In Progress, T-245 Queued (dependency-correct), T-246/T-247 Not Started. Confirmed authority contradiction: backlog T-242 requested `ForEach/RepeatN/WhileBounded` board blocks; the Layer-B rule places FOR/WHILE in orchestration. Central ruling still required before S3/S4.

### 3.3 Jobs / Monitor / Logging / Parallelism — 44

`JobRunOrchestratorService.cs:178,182` returns "no manual executor" / "not implemented yet"; `ImportBatchQueueProcessorService.cs:53` serial `foreach`; **no `Task.WhenAll`, `Parallel.For*`, or `SemaphoreSlim` in any runtime project**. `JobLogService.cs` writes to `ppiq_meta.job_log` and enriches Serilog with `JobRunId` (`:61`) — good — but `runId` is nullable on every call site (`:18,37`) so correlation is optional, not enforced. No `AddHealthChecks`, no OpenTelemetry, no Prometheus anywhere; 36 hand-written `MapGet("/health")` in 29 files. T-106/T-107/T-109/T-110/T-111/T-118/T-125 all Not Started.

### 3.4 ML / Engine — 64

Confirmed: versioned C#↔Python protocol, MF-01..MF-04, TreeSHAP, promotion/remediation contracts, 72 Python source and 31 Python test files. Newly credited: five boundary tests (`test_isolation.py:20`, `test_t170/172/173/176_boundary.py`) assert that no DB driver, socket, subprocess or HTTP module is importable from the ML runtime — the Layer-B "Python never touches the database" rule is test-proven, not prose. `requirements.lock` pins versions (`pyarrow==25.0.1`, `lightgbm==4.7.0`). Open: MF-05..07, active model identity/rollback, serving-vs-training separation, `ml_method_definitions` registry (still absent — `AuthoringSupportEndpoints.cs:42,370`).

### 3.5 Backend / API — 81

Confirmed strengths as v1. Deductions sharpened: `Program.cs` (1,269 lines) maps 31 endpoint families named by phase or version (`MapPhase1WorkflowTruthEndpoints`, `MapPhase2*`, `MapPhase34*`, `MapPhase45Closure*`, `MapPhase8*`, `MapPhase9*`, `MapPhase10License*`, `MapV5*` ×9) — the workbook's own governance says phase codes are not product vocabulary; 17 `V5*` source files are still live surfaces; `MapMaterialEndpoints` / `MapMaterialInvestigationEndpoints` (`:962,964`) and `WorkflowEndpoints.Handlers.007/008/019 *Material*` are industry-specific surfaces inside the generic API; 64 `public.*` object references in 25 runtime files.

### 3.6 User / Role — 60

`FormalPlantRole` enum (`FormalRoleAccessMatrix.cs:12`) already carries the enterprise catalogue. The defect is the string mapping: `"admin" => PlantAdmin`, `Executive`/`ChiefExecutiveOfficer` both → `"executive"`, and `_ => "viewer"` (`:248-289`). An unrecognised role silently becomes viewer; two roles are indistinguishable on the wire. Frontend `role: … | string` (`apiClient.ts:24`) defeats the type. No Users/Roles admin surface (T-120 Not Started).

### 3.7 DB Link / Schema / OPC / Interfaces — 52

Drivers present: Npgsql, `Microsoft.Data.SqlClient 5.2.2`, `MySqlConnector 2.3.7`, `Oracle.ManagedDataAccess.Core 23.26.200`, ClosedXML (`PlantProcess.Infrastructure.csproj:9-16`). No OPC UA package. `OpcUaHistorianConnector` validates configuration only and refuses live handshake claims — honest, and rated 10 %. A backend truth view `ppiq_v_connector_runtime_truth_state` exists; the frontend `PROVIDER_DETAIL` map (`AdminDbConfigurationTab.tsx:97-216`) is a second, contradicting truth. T-224/225/226 Not Started (36 h, Worker 1).

### 3.8 DB / Schemas — 86

`Backend\database\canonical-migration-order.json` is executable topology authority ("Product tables live in ppiq_meta, ppiq_plant or ppiq_staging… a single terminal convergence file relocates them", `:25`). Against that rule the runtime still references `public.ppiq_register_dump_source` ×11, `public.ppiq_resolve_safe_sql` ×4, `public.ppiq_run_stage` ×4, `public.canonical_material_units`, `public.canonical_genealogy_edges`, `public.ppiq_material_investigation`, `public.demo_runtime_settings` ×3 and others (64 hits / 25 files). RLS: 15 `ENABLE ROW LEVEL SECURITY` + 15 `CREATE POLICY` statements against `tenant_id` appearing in 51 SQL files. T-251 In Progress; T-112 Not Started.

### 3.9 License Control — 64

`V5SignedLicensingEndpoints.cs`: ECDSA P-256 signing (`:309`), canonical JSON, artifact insert (`:438`), entitlement projection (`:512`), licence events (`:595`), issuance gated to Development (`:59`). Defects: the key is generated **inside the customer's database** (`EnsureDevSigningKeyAsync`, `:300-337`) so the trust root is on-premise; the column `private_key_pem_encrypted` receives the raw PEM (`:321-334`) — the name promises what the code does not do. T-121 Not Started.

### 3.10 Security — 69

Credited: `AddRateLimiter` fixed-window (`Program.cs:628-644`) + `UseRateLimiter` (`:885`); `UseHsts` and `UseHttpsRedirection` outside Development (`:773,855-859`); Swagger and dev-seed mapped only under `IsDevelopment()` (`:912-918,1020-1024`); `RequireHttpsMetadata` outside Development (`:508`); startup guards reject `ChangeMe123!` (`P01P02StartupGuard.cs:119`, `StartupConfigurationValidator.cs:392`); frontend test asserts the demo password literal is absent (`AuthContext.bootstrap.test.tsx:52`); CI truth gate suite. Deductions: `RequireAdminMfa=false` default and CI stack forces it false (`ci-e2e-stack.sh:21`) so MFA is never exercised in release truth; Dockerfiles have no `USER` line (root); `presentation.env:41` bootstrap admin enabled; tenant-isolation proof vacuous; audit unmasked.

### 3.11 ChatBot / LLM / Assistant — 70

Confirmed planner/retrieval/verifier discipline. Sharpened: `V5AssistantGateway.cs:326-327` routes `self_hosted_openai_compatible`, `azure_openai`, `aws_bedrock`, `byom_openai_compatible` all to one `OpenAiCompatibleAssistantProvider` posting to `/v1/chat/completions` — Azure and Bedrock are advertised, not implemented. `AssistantTools.cs:85-89` legacy `run_kpi` still counts `public.canonical_material_units`. T-223 Not Started.

---

## 4. Persona scoreboard (revised) and Persona × Aspect matrix

| Persona | v1 | **v2** | What moved |
|---|---:|---:|---|
| Developer / Maintainer | 72 | **70** | Root-level stray pack payload and state files; three audit-tool generations; 168 task-ID filenames; blank-line-inflated sources |
| Customer Software / IT | 61 | **58** | No health-check framework, root containers, zero parallelism, OPC without a stack |
| Customer Operations / Quality | 75 | **75** | Unchanged; associative/evidence core confirmed |
| Customer CEO / Executive | 64 | **63** | Licence trust root on-premise; role mapping collapse |

### 4.1 Persona × Aspect (0-100, how well each aspect serves each persona today)

| Aspect | Developer | IT | Ops/Quality | CEO |
|---|---:|---:|---:|---:|
| 1.1 HMI/BI | 74 | 62 | 78 | 70 |
| 1.2 Canvas/SQL | 78 | 60 | 66 | 55 |
| 1.3 Jobs | 50 | 38 | 46 | 42 |
| 1.4 ML/Engine | 76 | 52 | 62 | 58 |
| 1.5 Backend/API | 84 | 72 | 80 | 72 |
| 1.6 User/Role | 62 | 56 | 58 | 52 |
| 1.7 DB Link/OPC | 60 | 48 | 50 | 50 |
| 1.8 DB/Schemas | 90 | 84 | 80 | 74 |
| 1.9 Licence | 66 | 60 | 60 | 58 |
| 1.10 Security | 74 | 66 | 68 | 62 |
| 1.11 Assistant | 76 | 58 | 72 | 66 |
| **Persona mean** | **72** | **60** | **65** | **60** |

Reading: Ops/Quality is best served by what exists; IT and CEO are held back by the same three aspects (Jobs, DB Link/OPC, Licence). Those three are also the aspects with the most Not-Started P2/P5 hours. The persona gap and the schedule gap point at the same place.

---

## 5. Top 25 celebration areas — at or above ~90 % of the relevant design slice

Items 1–15 are v1's, re-verified; scores adjusted only where source moved them. Items 16–25 are new.

| # | Area | Score | Authority | Why it deserves celebration | Evidence (path:line) |
|---:|---|---:|---|---|---|
| 1 | Three-schema topology + canonical migration authority | 96 | T-087/T-088 | Executable order with an explicit relocation rule; duplicate creation adjudicated | `Backend\database\canonical-migration-order.json:25,43-44,99-101` |
| 2 | Unified versioned definition authority | 96 | T-089/T-090 | Immutable history, server-side version allocation, convergence guards | `Infrastructure\Definitions\CanonicalDefinitionWriter.cs` (1,046 lines); `Application\Definitions\Interfaces\IDefinitionService.cs` |
| 3 | Plant-vocabulary / genericity ratchets | 93 | T-093/T-094 (Done, 4 commits) | Vocabulary moved out of runtime authority; scoped genericity gate exists | Ledger: `377aabdf…; 6ab97ee2; e74ecd1c; 5a967f1b…`; `Apply-T206-ScopeAwareGenericityGate.ps1` |
| 4 | Analysis Subject + Grain authority | 95 | T-209/T-231 | Explicit semantic authorities replace implicit table assumptions | Backend Application/Infrastructure subject-link resolver lane |
| 5 | Relationship model + preferred-path resolver | 95 | T-095/096/097 | Reusable relationship contract with consumer certification | Backend + tests |
| 6 | Canonical signal / aggregation semantics | 94 | T-210 (Done `92a655e1…`) | Aggregation is governed, not defaulted; consumed by Canvas binding | Ledger; Canvas aggregate/window binding |
| 7 | Source Time Authority + temporal alignment | 94 | T-216/T-217 | Typed source-time semantics with known-answer tests | Backend analytics kernels + tests |
| 8 | Persisted page/widget definition replay gate | 94 | T-202 | Definitions are replayable release truth | Frontend misc / tests |
| 9 | Associative cross-filter state machine | 91 | T-204 | Durable selection semantics with release-truth assets | Frontend + E2E |
| 10 | Page Builder persistence / layout round-trip | 92 | M1 → M2 | Backing workspace, normalized geometry, reload | `state\DashboardGridLayoutContext.implementation.tsx` |
| 11 | One Shared Authoring Shell | 94 | T-032/T-241 | Single parameterized shell: schema tree, mode bar, board/SQL, toolbox, debug log | `src\authoring\SharedAuthoringShell.tsx` |
| 12 | Typed Canvas ports + graph refusal | 96 | T-241 (Done `eb7fb2ab`) | Incompatible connections refused before execution | `src\authoring\graphSemantics.ts` (1,075 lines) |
| 13 | Deterministic block algebra + governed aggregate/window | 92 | T-242 (Done, 3 commits) | Known-answer arithmetic/comparison/Boolean; bounded loops | Ledger; Canvas tests |
| 14 | SQL-mode compile / validate / dry-run / version | 94 | T-244 (Done `4d32c6b1…`) | Server validation, bounded preview, immutable version | Ledger; `SharedAuthoringShell.tsx` SQL path |
| 15 | Assistant planner + permission-first retrieval + verifier | 92 | T-179/180/181 | Deterministic planning, evidence handles, verification | `Infrastructure\Assistant\*` |
| **16** | **ML runtime isolation is test-proven** | **95** | Layer-B rule (frozen) | Five boundary tests assert no DB driver, socket, subprocess or HTTP module is importable from the ML runtime; the "Python never touches the database" rule is executable | `ML\tests\test_isolation.py:20`; `test_t170_boundary.py:86`; `test_t172_boundary.py:116`; `test_t173_boundary.py:212`; `test_t176_boundary.py:77` |
| **17** | **Blocking E2E on an ephemeral CI stack, with a truth-gate test that would catch regression** | **93** | CI truth (T-02 lineage) | Stage 5 has no `when{}`; script raises compose stack, waits for health, runs Playwright, tears down in `EXIT` trap; `E2e_stage_cannot_be_gated_off()` and `Pipeline_never_swallows_failures_with_catchError_success()` guard it | `Jenkinsfile:107-108`; `deploy\scripts\ci-e2e-stack.sh:14-15,40`; `CiPipelineTruthGateTests.cs:63,100` |
| **18** | **Production hardening baseline in the composition root** | **90** | Ch.6 | Fixed-window rate limiter, HSTS, HTTPS redirect, `RequireHttpsMetadata`, Swagger and dev-seed mapped only in Development | `Program.cs:508,628-644,773,855-859,885,912-918,1020-1024` |
| **19** | **Default-credential startup refusal** | **92** | T-021/P01P02 | API refuses to start with `ChangeMe123!`-class defaults; frontend test asserts the demo password literal is absent from source | `P01P02StartupGuard.cs:119-120`; `StartupConfigurationValidator.cs:392`; `AuthContext.bootstrap.test.tsx:52`; `tools\security\Scan-PPIQ-Phase1-Defaults.ps1` |
| **20** | **Connector honesty in code** | **90** | T-207 (Done) | `OpcUaHistorianConnector` refuses to claim a live handshake it cannot prove; a runtime truth view exists for connector state | `OpcUaHistorianConnector.cs:11-16,49-50`; `public.ppiq_v_connector_runtime_truth_state` refs |
| **21** | **Signed licence artifact chain** | **90** (of its slice) | T-121 precursor | ECDSA P-256 signing over canonical JSON, artifact table, entitlement projection, licence event log — all in `ppiq_meta` | `V5SignedLicensingEndpoints.cs:104,309,438,512,595,625` |
| **22** | **Formal role catalogue already exists in Application** | **90** (catalogue only) | T-119 precursor | `FormalPlantRole` enum with Executive/CEO/Operator/PlantAdmin/Viewer and an access matrix — the eight-role work is a mapping/surface task, not a modelling task | `Application\Security\Rbac\FormalRoleAccessMatrix.cs:12` |
| **23** | **Test estate scale and hygiene** | **91** | T-205/T-250 (Done) | 279 backend, 136 FE-unit, 84 E2E, 31 Python test files; zero `.skip/.only/.todo` in the FE unit suite; `TESTS.md:15` requires a ledger row for any new `SkippableFact` | Manifest counts; `Backend\tests\TESTS.md:15` |
| **24** | **Job log is canonical and structured** | **90** (of its slice) | T-125 precursor | `ppiq_meta.job_log` + Serilog `ForContext("JobRunId")`, severity, context JSON | `Api\Observability\JobLogService.cs:48,61` |
| **25** | **Pinned ML dependency lock** | **90** | Layer-B | `requirements.lock` pins the numerical stack; `pyproject` pins Python ≥3.11 | `ML\requirements.lock:19,27`; `ML\pyproject.toml:9` |

**Interpretation.** Scores apply to the named slice. #21 and #22 are celebrated as foundations precisely because §7 and §8 below list the defects that stop them being production truth.

---

## 6. Top 25 boost areas — ≤45 % of the relevant final-design slice

| # | Gap area | v1 | **v2** | Backlog owner (status) | Current reality | Evidence |
|---:|---|---:|---:|---|---|---|
| 1 | Job scheduler / DAG / admission / weighted pools / parallelism | 30 | **20** | T-106, T-107, T-118 (all Not Started) | Switch orchestration; serial import loop; **no concurrency primitive in the runtime** | `JobRunOrchestratorService.cs:178,182`; `ImportBatchQueueProcessorService.cs:53`; grep `Task.WhenAll\|Parallel\|SemaphoreSlim` = 0 |
| 2 | Canvas-to-job compilation / monitor / per-block evidence | 15 | **15** | T-245 (Queued behind T-243) | Binder defers to T-245 | Workbook Progress Dashboard |
| 3 | Canvas model / intelligence blocks | 10 | **10** | T-246 (Not Started) | S4 structural only | Workbook |
| 4 | Canvas immutable save / reopen / edit round-trip | 40 | **40** | T-243 (In Progress, 12 h) | Backend pieces exist; closure unproven | Workbook |
| 5 | Production OPC-UA edge runtime | 20 | **10** | T-224/225/226 (Not Started, 36 h) | **No OPC UA client package in any csproj**; connector validates config only | `PlantProcess.Infrastructure.csproj:9-16`; `OpcUaHistorianConnector.cs` |
| 6 | Production-certified relational connector breadth | 35 | **35** | T-207 (Done) + onboarding | Drivers present (SqlClient 5.2.2, MySqlConnector 2.3.7, Oracle 23.26); certification absent | csproj:13-16 |
| 7 | Qlik / Power BI / Tableau authoring parity | 42 | **42** | P2/P3/P5 distributed | No property inspector, bookmarks, pivot, conditional formatting, export fidelity | Frontend |
| 8 | Enterprise chart styling / property inspector | 38 | **38** | partial | 104 hex literals in 30 component files | grep |
| 9 | MF-05/06/07 + active model registry / rollback | 40 | **40** | P4/P5 | MF-01..04 only | `ML\src` |
| 10 | Intelligence datasets as ordinary Page Builder data | 30 | **30** | T-221/T-223/T-246 | No integration | Workbook |
| 11 | Eight-role enforcement (mapping + three layers) | 35 | **45** | T-119 (Not Started) | Catalogue exists; mapping collapses to 4, `_ => "viewer"` | `FormalRoleAccessMatrix.cs:248-289` |
| 12 | Users / Roles admin surface | 30 | **30** | T-120 (Not Started) | None | Workbook |
| 13 | Licence enforcement / meters / admin | 40 | **35** | T-121 (Not Started) | Signing chain exists; trust root on-premise; plaintext key | `V5SignedLicensingEndpoints.cs:300-337` |
| 14 | Observability: pools, queue depth, SAR, latency | 25 | **20** | T-111, T-125 (Not Started) | No `AddHealthChecks`/OTel/Prometheus; 36 ad-hoc `/health` in 29 files | grep |
| 15 | Deploy / upgrade / backup / restore / rollback acceptance | 35 | **35** | T-123/124/126/150/227 (Not Started, 54 h) | Scripts exist; no acceptance | Workbook; `deploy\dr\backup.ps1` |
| **16** | **Tenant isolation at the database (RLS)** | — | **25** | T-112 (Not Started) | 15 `ENABLE ROW LEVEL SECURITY` / 15 `CREATE POLICY` vs `tenant_id` in 51 SQL files; the only "proof" test is `Assert.True(true)` | grep; `Phase04TenantIsolationProofTests.cs:11` |
| **17** | **Quarantine, identity resolution, genealogy hardening, versioned projection** | — | **0** | T-099..T-105, T-108, T-110 (Not Started, 64 h, Worker 1) | Whole P2 Worker-1 data-integrity batch untouched | Workbook Verified Task Ledger |
| **18** | **Assistant provider breadth** | — | **35** | none explicit | `azure_openai` and `aws_bedrock` provider types resolve to the OpenAI-compatible HTTP client | `V5AssistantGateway.cs:326-327` |
| **19** | **Method palette registry (`ml_method_definitions`)** | — | **30** | future registry work | Table does not exist; palette is enum-driven and says so | `AuthoringSupportEndpoints.cs:42,370` |
| **20** | **Typed frontend API convergence** | — | **40** | none | 4 slices via `productApi as legacyApi`; 58 `any` in 13 files | `admin.api.ts:1`; `analytics.api.ts:1`; `dashboarding.api.ts:4`; `integration.api.ts:1` |
| **21** | **Container / runtime hardening** | — | **30** | T-123 partial | Five Dockerfiles, none with `USER`; run as root | grep `^USER` = 0 |
| **22** | **Rule-2 convergence in runtime code** | — | **40** | T-251 (In Progress) | 64 `public.*` references in 25 runtime files (`ppiq_register_dump_source` ×11, `ppiq_resolve_safe_sql` ×4, `ppiq_run_stage` ×4, `demo_runtime_settings` ×3 …) | grep |
| **23** | **Phase/version vocabulary removal from live surfaces** | — | **35** | none | 31 `MapPhaseN*`/`MapV5*` families in `Program.cs`; 17 `V5*` files live | `Program.cs:1034-1140` |
| **24** | **Material-specific surfaces retired from the generic binary** | — | **40** | regression vs T-093/T-094 | `MapMaterialEndpoints`, `MapMaterialInvestigationEndpoints`, three `*Material*` workflow handlers, `MaterialAnalyticsPages.tsx` (1,022 lines), 26 material/heat/coil-named source files | `Program.cs:962,964`; `Endpoints\Materials\*`; `pages\MaterialAnalytics\*` |
| **25** | **Disposable-DB test lifecycle** | — | **0** | T-252 (P5 Not Started — see §0.4) | Fixture clones live dev DB (`W1-INFRA-TESTS-DBCLONE-01`); T-210 fixture fails rather than skips when probe DB unset | Worker-2 findings; workbook |

---

## 7. Top 30 bugs / quick hotfixes / release-truth defects

Backlog labels: **Yes** (open task owns the corrective scope) · **Partial** (related task, defect not explicit in its acceptance) · **Regression** (contradicts a closed contract; route as a narrow hotfix, not a reopened task) · **No** (not represented).

v1 #1 (Jenkins E2E gated) is **withdrawn** — see §0.1. The remaining v1 items are renumbered 1–19 and re-evidenced; items 20–30 are new.

| # | Prio | Finding | Type | Backlog? | Required hotfix | Evidence |
|---:|---|---|---|---|---|---|
| 1 | **P0** | Shared SQL authoring hardcodes `canonical_material_units` as output entity | Genericity regression | Regression (T-094/T-241 closed) | Resolve output entity from the authored definition / subject authority; add a genericity gate line for `canonicalEntity:` literals in `src\authoring` | `SharedAuthoringShell.tsx:618` |
| 2 | **P0** (sharing) | Audit package generated with `Mask Secrets: False`; includes `env\profiles\*.env`, machine and user names | Process / security | Partial (T-113) | Regenerate masked before any external share; keep this one internal | `00_Master_Index:12`; `env\profiles\local.env:15-51` |
| 3 | **P1** | Frontend `PROVIDER_DETAIL` is a second connector truth that overclaims | Truth / UX | Regression (T-207 closed) | Render backend availability/certification text; delete the map | `AdminDbConfigurationTab.tsx:97-216` |
| 4 | **P1** | Website "6 live source systems" / "Live source systems across 4 database engines" | Commercial honesty | Partial (T-207/T-150) | Reword to demo-fixture truth or prove six certified live sources | `ProductScreenshotShowcase.tsx:43`; `phase1WebsiteProof.ts:170` |
| 5 | **P1** (prod) | `RequireAdminMfa` defaults `false`; CI forces it `false` | Security hardening | Partial (T-150) | Production startup gate requiring MFA for admin unless waived; add one E2E run with MFA on | `AuthOptions.cs:35`; `ci-e2e-stack.sh:21`; `AdminMfaFlagTests.cs:2,31` |
| 6 | **P1** | Tenant-isolation proof is `Assert.True(true)` | Test truth | Partial (T-112) | Replace with two-tenant positive/negative test or exclude from scorecards | `Phase04TenantIsolationProofTests.cs:11` |
| 7 | **P1** (gov.) | T-245 backlog status In Progress while T-243 open | Backlog metadata | Workbook already flags it | Set backlog status to Queued so the two sheets agree | Workbook `Progress Dashboard` row T-245 |
| 8 | **P1** | Run Now has job types with no executor / "not implemented yet" | Functional gap | Yes (T-106/T-118) | Expose executor availability per type; refuse at definition time, not at run time | `JobRunOrchestratorService.cs:178,182` |
| 9 | **P1** | Import batch queue is a serial `foreach` | Performance | Yes (T-107/T-118) | Weighted admission + per-family limits, not bare `Task.WhenAll` | `ImportBatchQueueProcessorService.cs:53` |
| 10 | **P2** | `runId` nullable on every `JobLogService` call; endpoint-style events pass `null` | Observability | Partial (T-125/T-245) | Make correlation id mandatory; generate at the family boundary | `JobLogService.cs:18,37` |
| 11 | **P2** | `phase9:matrix` and `validate-real-ui-gates.cjs` invoke Playwright/vitest with `--list` | Test truth | No | Rename as enumeration-only or make them execute; forbid `--list` in any gate script by test | `package.json:84`; `tools\ci\validate-real-ui-gates.cjs:13-15`; `apply-phase5-phase6-full-ui-migration.cjs:74-76` |
| 12 | **P2** | Audit scanner reports its own regex table and guard-test names as CRIT (12 of 20) | Audit tool | No | Exclude scanner sources, tests and backups from severity; keep raw hits | `10_Audit_Signals`; `GeneratePlantProcessIQ_UltimateAudit*.ps1:659-741` |
| 13 | **P1** | Assistant `run_kpi` queries `public.canonical_material_units` → `material_unit_count` | Genericity / semantic wall | Partial (T-223) | Retire or route through registry measure/subject | `AssistantTools.cs:85-89` |
| 14 | **P2** | Assistant page context from first path segment (`pageCode` heuristic) | Context correctness | Partial (T-223) | Bind to Page Definition identity | Tools closure evidence |
| 15 | **P2** | Assistant snapshot evidence records `filter_context_json = {}` | Context / evidence | Partial (T-223) | Filtered snapshot semantics before claiming context-bound evidence | Tools closure evidence |
| 16 | **P1** (portab.) | Executable defaults carry `178.105.152.180` (a host already ruled compromised) | Portability / security | Partial (T-123/T-150) | Require env/customer profile; no IP fallback in `post-deploy-smoke.sh`, `ensure-runtime-env.sh`, `verify-server-exposure.sh`, `Invoke-CleanMachineDeployAcceptance.ps1` | `post-deploy-smoke.sh:6-7`; `ensure-runtime-env.sh:41`; `verify-server-exposure.sh:12`; `Invoke-CleanMachineDeployAcceptance.ps1:3-4` |
| 17 | **P1** | Page Builder audience roles hardcoded to four | Role contract | Yes (T-119/T-120) | Bind to canonical role registry | `PageBuilderPage.implementation.tsx:58,557` |
| 18 | **P2** | Generated Playwright report bundles (19 files) and backups inside audited tree | Repo hygiene | No | `.gitignore` + audit exclusion | `Frontend\PlantProcess.Web\playwright-report-journey\*` |
| 19 | **P0** (evidence) | No single machine-readable current-HEAD certification in the package | Validation | Yes/Partial (T-150/T-227) | One run: build → arch/security gates → FE unit → E2E → fresh DB replay → smoke, as one artifact | `00_Master_Index` |
| **20** | **P1** | **Three source files are blank-line-inflated ~3.5× (line-ending fault)** | Hygiene / pack defect (`W1-PACK-CRLF` family) | No | Normalise; add a hygiene test: no file with >60 % blank lines | `WidgetResultSources.cs` 7,537 → 2,116 non-blank; `AdvancedCorrelationComputeService.cs` 1,167 → 348; `760_t025_lineage_and_outcome_producer.sql` 1,793 → 393 |
| **21** | **P1** | **Stale M1 pack payload committed at repository root**: `AuthoringSupportEndpoints.cs` (239 lines) diverged from the real 375-line file, plus `pack.json`, `edits.Program.json` | Repo integrity | No | Delete the three root files; add a root-allowlist test | `AuthoringSupportEndpoints.cs` (root) vs `Backend\PlantProcess.Api\Endpoints\Prep\AuthoringSupportEndpoints.cs`; `pack.json:1-12` |
| **22** | **P1** | **PowerShell run-state files tracked at root** referencing `ppiq_presentation` and a DB OID | Repo integrity | No | Delete; `.gitignore` `*_state.json` | `wipetrap_state.json:2-4`; `importchain_state.json:2-3` |
| **23** | **P2** | Third task-ID pack at root (`Apply-T206-ScopeAwareGenericityGate.ps1`) plus `CORRECTION.md`, `walk-evidence.md` — 14 root entries, 7 of which are transient | Naming law | No | Move packs to evidence archive; root = `Jenkinsfile`, `README.md`, dotfiles | Manifest root listing |
| **24** | **P2** | Dead knob `PPIQ_RUN_E2E=off` still written by runtime-env scripts; nothing reads it | Config residue | No | Remove both writers | `ensure-runtime-env.sh:38`; `Ensure-RuntimeEnv.ps1:26` |
| **25** | **P1** | Licence signing private key stored **plaintext** in column `private_key_pem_encrypted`; key generated inside customer DB | Security / truth | Partial (T-113/T-121) | Encrypt-at-rest or rename the column; production keys vendor-held, DB holds public key only | `V5SignedLicensingEndpoints.cs:316-334` |
| **26** | **P1** | Role wire mapping: `_ => "viewer"` default and `Executive`/`ChiefExecutiveOfficer` both → `"executive"` | Auth correctness | Yes (T-119) | Fail closed on unknown role (refuse, not downgrade); one string per enum member | `FormalRoleAccessMatrix.cs:281-289` |
| **27** | **P2** | 12 runtime-conditional `test.skip(...)` in E2E specs — a spec can go green without exercising anything when an env var or seed is missing | Test truth | Partial (T-205/T-250 closed) | Gate profile must fail on any skip; report skip count in the machine-readable result | `phase3-golden-thread-honesty.spec.ts:13-29`; `phase4-demo-journey.spec.ts:78`; `phase5-role-scope-nav.spec.ts:16`; `phase6-exec-ops.spec.ts:60` |
| **28** | **P2** | Three generations of the audit generator tracked (`v1`, `v2_2`, `v2_3`) | Tool authority | No | Keep one; archive the rest | `tools\GeneratePlantProcessIQ_UltimateAudit*.ps1` |
| **29** | **P2** | `CurrentEngineCompatibilityTests` unconditional `[Fact]` always passes | Test truth (low) | No | Convert to a diagnostic (not counted) or emit a trait excluded from gates | `CurrentEngineCompatibilityTests.cs:157-161` |
| **30** | **P2** | Dockerfiles run as root (no `USER`) | Container hardening | Partial (T-123) | Non-root user in all five Dockerfiles | `deploy\edge-agent\Dockerfile`; `Website\PlantProcess.Website\Dockerfile`; compose Dockerfiles |

### 7.1 CRIT accounting

Raw audit: 20 CRIT / 43 WARN / 26 INFO. After removing scanner self-reference (12), the guard test (1) and backups: **8 CRIT, one family (`--list` enumeration)**; **WARN 43 → ~21** (17 IP hits, of which 8 are docs/README/validators; dev-seed references are gated by `IsDevelopment()` at `Program.cs:1020-1024`; bootstrap admin in two dev profiles); **INFO 26 → 2** (real TODOs in `verify_demo_dataset.py:23` and `BenchmarkHarnessTests.cs:64`).

---

## 8. Top 30 design drift / dirty implementation / genericity and enterprise-rework items

| # | Drift / rework | Category | Backlog | Why it needs rework | Evidence |
|---:|---|---|---|---|---|
| 1 | Task-ID filenames: **168 files** (docs 63, Backend 56, tools 19, ML 18, Frontend 9, root 3) | Naming law | No | Task identity belongs in commits/evidence, not structure | Manifest |
| 2 | Root-level packs, pack payload, state files mixed with enduring tooling | Repo lifecycle | No | See bugs #21–23 | Manifest root |
| 3 | Backups / `.broken` / `.quarantine` / `.t04bak` / timestamped copies (25 residue entries incl. three `tools\backups\T042-*` snapshots and `JobAdminEndpoints.cs.20260609_110000`) | Repo cleanliness | Partial (T-251) | Retention policy: runtime source, migration authority, evidence archive, disposable residue | grep |
| 4 | Generated Playwright report assets inside audited tree (19 files) | Artifact hygiene | No | Exclude from source metrics | Manifest |
| 5 | God files (non-blank lines): WidgetResultSources 2,116; CustomerAssessmentEngine 1,617; V5PrivateModelGatewayCertificationEndpoints 1,567; DashboardWidgetQueryService 1,505; DashboardDefinitionService 1,348; Program.cs 1,269; AnalysisJobDefinitionEndpoints 1,235; AdminDbConfigurationTab.tsx 1,198; SharedAuthoringShell.tsx 1,173; DeckPage.tsx 1,140 | Maintainability | No | Split on stable domain seams only | Manifest + non-blank count |
| 6 | `productApi as legacyApi` dynamic dispatch in four API slices | Type safety | No | Missing methods become runtime errors | `admin.api.ts:1`; `analytics.api.ts:1`; `dashboarding.api.ts:4`; `integration.api.ts:1` |
| 7 | Frontend provider descriptions as second capability registry | SSoT drift | Regression (T-207) | Consume backend truth | `AdminDbConfigurationTab.tsx:97` |
| 8 | 104 hardcoded hex colours in 30 component files | Design-system drift | Partial | Token-driven palette roles | grep |
| 9 | Canvas SQL save hardcodes material output entity | Genericity | Regression | Same as bug #1 | `SharedAuthoringShell.tsx:618` |
| 10 | Assistant legacy KPI tool material-specific | Semantic wall | Partial (T-223) | Same as bug #13 | `AssistantTools.cs:85-89` |
| 11 | Job orchestration is a switch over job families | Extensibility | Yes (T-106/T-107) | Executor registry + target/version policy | `JobRunOrchestratorService.cs` |
| 12 | Import queue couples selection, mapping, status mutation, DQ scan | Separation | Yes (T-107/T-118) | Thin orchestration; explicit admission/retry/metrics | `ImportBatchQueueProcessorService.cs` |
| 13 | `JobLogService` writes SQL directly from the API observability layer | Layering | Partial (T-125) | Repository port | `JobLogService.cs:48` |
| 14 | Page Builder role options hardcoded | Registry drift | Yes (T-119/T-120) | Server-driven vocabulary | `PageBuilderPage.implementation.tsx:58` |
| 15 | T-242 board-level FOR/WHILE vs Layer-B orchestration rule | Design/backlog contradiction | Backlog created it | Central ruling before S3/S4 | Workbook T-242; Layer-B rule |
| 16 | T-242 board-level arithmetic/logic vs Layer-B "expression blocks" rule | Design/backlog contradiction | Backlog created it | Pick one compositional model | Workbook T-242; Layer-B rule |
| 17 | Method palette enum-driven; `ml_method_definitions` absent | Registry completeness | Future | Replace implementation authority, keep wire contract | `AuthoringSupportEndpoints.cs:42,370` |
| 18 | Compatibility views / old public canonical names still referenced | DB convergence | Yes/Partial (T-251) | Label platform/compat/retired; forbid new dependence | `canonical-migration-order.json` |
| 19 | Marketing copy drifts from connector/runtime truth | Product honesty | Partial | Generate or verify against capability snapshot | `phase1WebsiteProof.ts:170` |
| 20 | Audit severity mixes runtime, tests, tooling, backups | Governance | No | Source-class + reachability + gate relevance | `10_Audit_Signals` |
| **21** | **64 `public.*` object references in 25 runtime backend files** (`ppiq_register_dump_source` ×11, `ppiq_v_ed` ×6, `ppiq_resolve_safe_sql` ×4, `ppiq_run_stage` ×4, `demo_runtime_settings` ×3, `canonical_material_units` ×2, `canonical_genealogy_edges`, `ppiq_material_investigation`, `document_sections` ×2, `ml_*` ×5 …) | Rule 2 (three-schema) | Partial (T-251) | Every runtime reference resolves to `ppiq_meta`/`ppiq_plant`/`ppiq_staging` or is an explicit, catalogued compatibility object; add an architecture test that greps runtime projects for `public\.` | grep |
| **22** | **Phase/version vocabulary in the composition root**: 31 `MapPhase1…Phase10*` / `MapV5*` families; 17 `V5*` source files live | Governance vocabulary | No | Rename by capability; the workbook says no alternative phase codes are authority | `Program.cs:1034-1140` |
| **23** | **Material-specific surfaces live in the generic binary**: `MapMaterialEndpoints`, `MapMaterialInvestigationEndpoints`, `WorkflowEndpoints.Handlers.007/008/019 *Material*`, `MaterialAnalyticsPages.tsx` (1,022), `MaterialUnitTypeDefinition`, `MaterialFeatureVector`, `DashboardMaterialRow` type — 26 named source files | Rule 1 (genericity) | Regression vs T-093/T-094 | Either these are declared "legacy oracle surfaces" behind an explicit compat flag, or they are retired; the scoped genericity gate must cover `Endpoints\Materials`, `Services\Materials`, `pages\MaterialAnalytics` | `Program.cs:962,964`; manifest |
| **24** | **Licence trust root on-premise**: signing key generated and stored in the customer's `ppiq_meta`; plaintext in an `_encrypted` column | Commercial security | Partial (T-121) | Vendor-held private key; DB holds public key only; column encrypted or renamed | `V5SignedLicensingEndpoints.cs:300-337` |
| **25** | **Assistant provider taxonomy overclaims**: four provider types → one OpenAI-compatible client | Truth | No | Advertise only implemented providers; add provider-conformance test | `V5AssistantGateway.cs:326-327` |
| **26** | **36 ad-hoc `/health` endpoints in 29 files; no `AddHealthChecks`** | Observability architecture | Partial (T-125) | One health-check pipeline with named checks; per-feature health becomes a check, not a route | grep |
| **27** | **Duplicated scripts with dual authority**: `dead-button-scan.mjs` (root `scripts\` and `Frontend\…\scripts\`); `ensure-runtime-env.sh` + `Ensure-RuntimeEnv.ps1` | Tool drift | No | One owner per script; the other imports or is deleted | Manifest |
| **28** | **localStorage as dashboard-layout fallback after backend persistence landed**; also theme/locale | Persistence drift | No | Retire the fallback; keep localStorage only for pure UI preference | `DashboardGridLayoutContext.implementation.tsx:10,475`; `phase56ThemeRuntime.ts:6,14`; `phase78I18n.ts:64` |
| **29** | **Frontend role type `… \| string`** defeats the union | Type truth | Yes (T-119) | Closed union generated from the server catalogue | `apiClient.ts:24` |
| **30** | **Test fixture clones the live dev DB; T-210 fixture fails instead of skipping when probe DB unset** | Test infrastructure determinism | T-252 (P5 Not Started — see §0.4) | Dedicated template DB; skip-with-reason when probe absent | `W1-INFRA-TESTS-DBCLONE-01`; Worker-2 findings |

---

## 9. Central rulings recommended

- **R1 — T-243 current, T-245 queued.** Unchanged. Align the backlog `Status` cell with the ledger.
- **R2 — Rule T-242 control flow before S3/S4.** Unchanged.
- **R3 — Rule arithmetic/logic placement before S3/S4.** Unchanged.
- **R4 — Backend capability catalogue is the connector truth.** Unchanged; add: the Assistant provider list is subject to the same rule (drift #25).
- **R5 — Genericity is a ratchet across authoring, Assistant, endpoints and pages.** Extended: the scoped genericity gate (T-206 pack) must enumerate `Endpoints\Materials`, `Services\Materials`, `pages\MaterialAnalytics`, `src\authoring` and `Infrastructure\Assistant` explicitly, and must fail on `public\.` in runtime projects.
- **R6 (replaces v1 R6) — CI truth is confirmed; protect it.** No "release green" statement without the single current-HEAD certification artifact (bug #19). Forbid `--list` in any script referenced by a gate by architecture test.
- **R7 — Repository root allowlist.** Root contains `Jenkinsfile`, `README.md`, dotfiles, solution/workspace files. Everything else fails a hygiene test. Retire bugs #21–23 in one commit.
- **R8 — Licence trust root.** Production licence issuance is vendor-side; customer databases hold verification keys only. Decide before T-121 starts, not during.
- **R9 — Schedule.** Worker 1 holds 281 of 480 remaining hours and all of P5 except T-120. Either P5 scope is re-cut for Release 1, or Worker-1 P2 data-integrity tasks (T-099..T-105, 64 h) move to Release 2. Both cannot fit before 30 September at the current closure rate (Worker 1: 164 h closed since 25 Aug).

---

## 10. Recommended execution sequence

**Immediate (one session, no task reopen):**
1. Bug #1 — `canonicalEntity` genericity hotfix in `SharedAuthoringShell.tsx:618`.
2. Bugs #21–24 — root cleanup commit (pack payload, state files, dead `PPIQ_RUN_E2E` knob), plus root-allowlist test.
3. Bug #20 — normalise the three blank-line-inflated files; add the >60 %-blank hygiene test.
4. Bugs #3–4 — connector copy and website claim.
5. Bug #2 — regenerate masked audit.
6. Bug #7 — backlog status cell for T-245.

**Critical path (unchanged):** T-251 → T-243 → T-245 → T-246 → T-247.

**Parallel boost lane:** T-106/T-107/T-118 job plane; then T-112 RLS with a real two-tenant test replacing bug #6; then T-119/T-120/T-121 on one entitlement authority with R8 decided.

**Not before 30 September:** OPC-UA runtime (needs a protocol stack decision first — no library is in the solution), MF-05..07, BI visual parity.

---

## 11. What I would not do

- Rewrite definition/relationship/database architecture — current strengths.
- Count raw audit CRIT as product defects (8 real, not 20).
- Trust any claim sourced from `deploy\.ppiq-backups`, `tools\backups`, or a test's prose comment about a *previous* state.
- Claim SQL Server / MySQL / Oracle / OPC readiness because a driver or class exists.
- Call T-242 bad work because two authorities disagree.
- Add `Task.WhenAll` to the import loop as a "quick win" — the design is right to demand bounded, weighted admission.
- Reopen closed tasks broadly; route regressions (#1, #3, #13, drift #23) as narrow hotfixes with their own commit.

---

## 12. Evidence source index

| ID | Source | Role |
|---|---|---|
| IMP-00 | `00_Master_Index_08Sep2026_134853.txt` | Size, classification, `Mask Secrets: False`, machine/user metadata |
| IMP-BE | `01_Backend_Core_08Sep2026_134853.txt` | 759 files — API/Application/Infrastructure/Domain/Workers |
| IMP-DB | `02_Backend_Database_08Sep2026_134853.txt` | 163 files — scripts, seed, views, `canonical-migration-order.json` |
| IMP-TEST | `03_Backend_Tests_08Sep2026_134853.txt` | 295 files (279 `.cs`) |
| IMP-ML | `03A_ML_Runtime_08Sep2026_134853.txt` | 108 files (72 src, 31 tests) |
| IMP-FE | `04_Frontend_App_08Sep2026_134853.txt` | 647 files (136 unit-test files) |
| IMP-FEMISC | `05_Frontend_Misc_08Sep2026_134853.txt` | 117 files (84 E2E specs, 19 report assets) |
| IMP-INFRA | `06_Infrastructure_08Sep2026_134853.txt` | `Jenkinsfile`, Caddy, Dockerfiles, three Jenkinsfile backups |
| IMP-TOOLS | `07_Tools_Validation_Misc_08Sep2026_134853.txt` | 500 files — packs, validators, docs, root files |
| IMP-SEED | `07A_DEMO_SQL_Data_Seed_08Sep2026_134853.txt` | 14 files |
| IMP-WEB | `08_Website_08Sep2026_134853.txt` | 87 files |
| IMP-AUDIT | `10_Audit_Signals_08Sep2026_134853.txt` | 89 raw signals, re-classified in §7.1 |
| MANIFEST | `manifest_08Sep2026_134853.csv/.json` | 2,698 rows — path, category, lines, SHA-256 |
| BACKLOG | `PPIQ_Backlog_08Sep2026_v2.xlsx` | Verified Task Ledger, Progress Dashboard, Phase × Worker, Current Authority |
| DESIGN-2/3/4/6 | `PPIQ_Chapter2/3/4/6_*.md` | Design targets |
| DESIGN-B | Layer-B Learned Intelligence Engine Rule (frozen) | Intelligence/Canvas/orchestration target |

---

## 13. Final management statement

The v1 verdict stands: strong governed foundations, validation ahead of operations, convergence before feature scatter. Two corrections to the management picture: the release pipeline is more honest than v1 reported, and the execution plane and connector estate are thinner than v1 reported. The single largest risk to 30 September is not any defect in this document; it is 281 Worker-1 hours against 22 calendar days. Ruling R9 is the decision that matters this week.

**Central review status:** implementation direction accepted; v1 P0 #1 withdrawn; six P0/P1 hotfixes and one root-cleanup commit required; critical path unchanged; schedule re-cut required.

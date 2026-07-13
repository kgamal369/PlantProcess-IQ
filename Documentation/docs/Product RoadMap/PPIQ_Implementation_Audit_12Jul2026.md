# PPIQ Deep Implementation Audit v2 - Rules-vs-Reality
**12-Jul-2026 (late)** | Basis: 12-Jul full source dump (8 files, 27MB) + live session proofs (M1-01/03/05/06/07/08 console transcripts) | Rules: rules.txt + 12-Jul clarification (*taxonomy = imported plant data*)
**Evidence grades:** [P] proven live this week (pasted console) | [T] source-traced (file/line) | [U] unverified - flagged, not assumed.
**Bands:** <55 crit / 55-69 needs-work / 70-84 solid / 85+ strong. **Headline = lowest persona.**

## 0. Scoreboard
| Persona | Score | Band |
|---|---:|---|
| A1 Developer / Maintainer | 76 | Solid |
| A2 Security / IT / Procurement | 68 | Needs work |
| A3 Process / Quality Engineer | 61 | Needs work |
| A4 Reliability / Ops | 67 | Needs work |
| A5 Executive Sponsor | 72 | Solid |
| A6 Brand / Website / Story | 63 | Needs work |
| A7 Data Governance / Auditor | 59 | Needs work |
| A8 Commercial / Licensing / Deployment | 64 | Needs work |
| **A9 UI/UX & Journey Experience (NEW)** | **56** | **HEADLINE - needs work** |
| A10 AI / Statistics / Engine (NEW) | 58 | Needs work |
| A11 Infrastructure Engineer (NEW) | 62 | Needs work |

**Headline 56/100 (A9).** The user's journey has three hard walls: step 4's UI drives the wrong subsystem, the 4th low-code UI (alerting/plant-data-log) does not exist anywhere in the codebase, and no human has yet walked the 09-Jul surfaces in a browser (M1-02 open) - so *experienced* quality is unproven even where code quality is high.

---

## A1 Developer / Maintainer - 76

**Better than design (5)**
1. [P] **Projector existed and beat its card.** M1-06 was carded 16h greenfield; `MappingExecutionService` already had `RunAsync -> ParseFieldMap -> MapOneRowAsync` with six EF entity mappers, typed field errors, `MarkMapped/MarkFailed`, staging-status idempotency. Only `const:` support (one edit) was needed. Idempotency proven live: re-projection = 0 rows.
2. [P] **One connector-layer fix cured two orchestrators.** D1-D4 fixed inside the 4 connector families; both `BackfillExecutionService` and `DeltaImportExecutionService` healed at once - 1,802-row first pass, 0-row second pass, ISO cursor `2026-07-02T05:42:44.0000000Z`.
3. [P] **One anti-pattern, three 500s, one generic cure.** Dataset readback (D4), schedule-board ordering, and `GetConnectionProfileByIdAsync` were all filter/order-after-DTO-projection; each fixed pre-projection so every caller benefits.
4. [T] **Auto import->project pipeline pre-existed.** `ImportBatchQueueProcessorService` + `ImportWorkflowService` (registered ~Program.cs:60038) auto-execute the active `MappingDefinition` per batch, failing the batch with a *named* reason when absent - journey step 5's engine-room was already built.
5. [P] **Contract-preserving surgery.** M1-05 swapped the handler behind `/connection-profiles/{id}/register` (Arch-B SQL registry -> `SourceDatasetDefinition` upsert) keeping `RegisterSourceTableResult` intact; the untouched frontend registered the two new rigged tables first try.

**Critical (5)**
1. [T] **Two rival mapping subsystems, no bridge.** `canonical_schema_views` (SQL-view path, what the UI drives) vs `MappingDefinition` (projector). Near-identical vocabulary ("mapping", "execute"), zero shared code. Every mapping this week was scripted. M1-14 owns the verdict; the wiring will be its own Critical task.
2. [T] **Projector router incomplete for the clarified Rule 2.** `MapOneRowAsync` routes 6 entities; `DefectCatalog` + `ParameterDefinition` missing -> taxonomy cannot be imported. ~2-3h: two mappers (code-keyed upserts) + two router cases + rig definition tables.
3. [T] **Arch-B corpse still in-tree.** Unreachable (C.1a) but present: `TwoStageImportMonitorPanel(.implementation).tsx`, `twoStageImport.api.ts`, route/nav/types, `ppiq_run_stage1/2(_all)` SQL, `src_*` schemas. Dead code with live-looking names misleads every new reader.
4. [T] **Schema-drift class unguarded.** The 310/320 same-table CREATE collision (fixed once) has no CI truth-gate; the two deferred gates (no-two-scripts-create-same-table; seed NOT-NULL coverage) remain unimplemented - the defect class can recur silently.
5. [T] **Test suite gaps at the seams.** No test covers: register->dataset upsert, projector `const:` branch, mapping execute query-string binding (the 400 we hit), or assistant registration (the carded guard test was never shipped - confirmed zero `AddAssistant` refs in the tests dump; folded to M2-06).

**Rework (5)**
1. [T] Backend mojibake in 6 .cs files (deferred list) - cosmetic but flags encoding discipline.
2. [T] `src/pages/Phase8|Phase9` dir names + `phase9-*` testids violate the Naming Golden Rule.
3. [P] Session scripts accumulated environment-workarounds (psql NOTICE/stderr, `-Db`/`-Debug` alias, exec-policy) - fold into one shared PS module instead of re-solving per script.
4. [T] `CorrelationService` obsolete warning (CS0618) still referenced in DI (DependencyInjection.cs:112) - retire the registration.
5. [P] Multi-batch drain + failed-row reset logic lives in a chat-delivered script; promote to `tools/` as a maintained runbook until M2-04 productizes it.

## A2 Security / IT / Procurement - 68

**Better than design (5)**
1. [P] **Deny-by-default proven in anger.** `AccessControlMiddleware` static matrix (longest-prefix, unmapped POST=403) blocked us twice this week - it is real enforcement, not decoration.
2. [T] **Three-layer logging + assistant audit.** `AuditLogMiddleware`, `RequestResponseLoggingMiddleware`, `JobLogService` (33247) + `ppiq_assistant_audit_log` (4099) - every assistant ask is auditable; most competitors log requests only.
3. [T] **License gating is per-endpoint plumbing.** `LicenseFeatureEndpointFilter` sits on 14+ endpoint registration sites (7456-21115) - feature gating exists at the right layer, not in the UI.
4. [T] **Read-only enforcement on source connections.** `ReadOnlyEnforced` on ConnectionProfile + throttled reader (row cap, rate limit, approved window, `SourceLoadRejectedException`) - a plant IT reviewer's first three questions, answered in code.
5. [T] **Role scoping reaches retrieval.** Assistant chunks carry `scope_role` viewer/engineer (80439-80595) - data-leak separation designed into step 15, ahead of the feature itself.

**Critical (5)**
1. [P] **Out-of-band DB writes with dev superuser creds.** This week's taxonomy + staging-reset went via psql as `ppiq_dev` - the exact bypass procurement flags. Cure: taxonomy mappers + purge of `PPIQ_CONFIG` rows; staging-reset becomes an admin endpoint (M2).
2. [T] **Untracked master demo dataset.** `loadA.sql.gz` + `Apply-SessionA-PlantThroughPipeline.ps1` gitignored on one laptop (M1-04 open) - business-continuity finding; archive TODAY.
3. [T] **Dev Ed25519 keypair still active** (Option 1); production keypair migration (Option 3) deferred - license tokens are forgeable by anyone with the repo.
4. [T] **Secrets in env files / scripts.** `VITE_SMOKE_PASSWORD` history shows bundle-baked creds happen; no secrets-manager story for customer installs.
5. [T] **Mail/domain hygiene deferred.** Hetzner/Spamhaus remediation (SPF/DKIM/PTR, relay 587) still open - procurement diligence will find the listing.

**Rework (5)**
1. [T] Role model is thin: claim fallback `?? "viewer"` (19152) + "Admin" literals - needs named policy constants + an explicit role catalog per the journey's user/role step.
2. [T] `/admin/users` group exists (7365) but user-management UX vs journey spec (create user, assign role, license seat) is unvalidated [U].
3. [T] Audit log query surface exists (`audit_log_entries` 13740) but no retention/immutability statement; the 5 AuditLogImmutabilityTests are SkippableFacts needing live triggers.
4. [P] AdminMfaRequirementMiddleware present in every trace - but MFA enrolment flow unverified [U].
5. [T] SSO/SCIM endpoints exist (`V5EnterpriseSsoScimEndpoints`, `ppiq_scim_users`) with an unreachable-code warning (CS0162:694) - finish or fence.

## A3 Process / Quality Engineer - 61

**Better than design (5)**
1. [P] **Falsifiable discovery.** Planted OR pre-verified in raw source (9.51x: 184/375/61/1182), engine rediscovered superheat->defect_rate_per_m2 at effect 0.924, q=0.0001 - and the SCRATCH control held (0.93) with non-drivers honestly null (q 0.47-0.87). The card never demanded a control.
2. [P] **The refusal machinery is honest.** `BlockedTooFewRows` until real observations existed; typed `Required mapped field 'X' is missing` errors named the exact taxonomy gap live (5,000 rows, exact codes) - the honest-error design worked in production conditions.
3. [P] **Full evidentiary chain.** 1,802 heats + 14,416 obs + 420 QEs, every row traceable staging->canonical with `MarkMapped(canonicalEntityId)` provenance.
4. [T] **Multi-grain feature store.** v6 refresh emits 54,574 feature / 91,839 outcome rows across grains with provenance - richer than the journey's flat description.
5. [T] **Genealogy semantics are engineered.** Weighted provenance ledger (trigger enforces sum=1.0 per child) vs structural graph - blended attribution is a differentiator competitors lack.

**Critical (5)**
1. [P] **Discovery is confounded.** `ml_learning_observations_v1` (204 generator) feeds the refresh - `operations.crew_shift`/`product.grade_family` findings the customer never imported sit beside the real one. Pristine only after C.2 + one clean re-run.
2. [T] **Engineer cannot bring their own defect vocabulary** (router gap above) - for a quality engineer this is the product's front door.
3. [P] **Findings table shows duplicates** (block 4a: same feature/outcome repeated across runs, mixed sample_size 120/31) - no run-versioning/dedup in the read path; trust-killer for an engineer.
4. [T] **Supervisor job (step 14) absent** and unspecified - the "engine brain" is narrative only; also needs guardrails (may tune configs/windows; must never touch gates/evidence; log before/after).
5. [T] **Step 15 mute.** Chunk store zero rows; every ask refuses. M1-09 is the largest open build (10h).
**Rework (5)**
1. [P] Odds-ratio not surfaced beside q-value - deck wants "9.5x"; add a canonical-side OR to the finding read.
2. [T] SCRATCH null result is computed but not *displayed* as a control narrative - make "what we did NOT find" a first-class demo beat.
3. [T] `parameter_definitions` expected_min/max exist but no range-validation on observation ingest [U] - out-of-range readings would enter silently.
4. [P] Observation grain is per-heat single reading; time-series (multiple readings/heat) untested through the pipeline.
5. [T] `ppiq_normalize_business_key` works but mapping UI exposes no key-normalization control - engineer can't express it no-code.

## A4 Reliability / Ops - 67

**Better than design (5)**
1. [P] Cursor incrementality: second run = 0 rows; throttled ~5k chunks drained deterministically over passes.
2. [P] Failure->reset->recover exercised in anger: 5,000 Failed rows with named reasons, `ResetProcessing`, re-projected 14,416/0.
3. [T] Reapers everywhere: stuck-run reaper (30min/5min) + `ComputeRunReaperHostedService` - runaway jobs self-heal.
4. [P] Idempotency by construction (Pending-only selection) and by proof (re-run = 0).
5. [T] `ScheduleNextRunAfterSuccess/Failure` with backoff on `SourceDatasetDefinition` - scheduling survives source outages (we saw the clean connection-refused error path live).

**Critical (5)**
1. [T] **No unified job visibility** - four journey job types not in one monitor (M1-10 open); ops needs psql today.
2. [P] **Orchestration is script-bound** - drain/project loop is not a product job; per-view Loading = M2-04.
3. [T] **Mapper errors don't reach job_log** - typed field errors stay on staging rows; `JobLogService` records endpoint-level only.
4. [T] **Worker interval/caps config-driven but unmonitored** [U] - no alerting when `SYSTEM_DELTA_IMPORT_JOB` stops ticking.
5. [P] **Source stack fragility** - docker name-conflict on `up -d` (pkl-mssql) shows compose drift; the demo depends on these 6 containers.
**Rework (5)**
1. [P] Projector tz default "UTC" -> read Site config.
2. [P] Add API-running preflight to every build-gated pack (standardized after the lock incident).
3. [T] `run-due` response lacks per-dataset cursor after value - add for monitorability.
4. [P] Multi-pass import needs a single "drain" verb (loop lives client-side).
5. [T] Backup-runner exists on server project [T from memory]; local DB backup story absent before C.2 purge - snapshot ppiq_app first.

## A5 Executive Sponsor - 72

**Better than design (5)**
1. [P] The differentiator is now a *demonstration*, not a claim - organic rediscovery end-to-end.
2. [P] Four of six M1-P2 build tasks banked with pasted evidence in one working session.
3. [P] Emulation story upgraded: rig is mathematically verified *before* import - "we can prove the data is honest" is a sales line.
4. [T] Competitive moat items exist in code (CI truth gate, honesty-lint, blended attribution, scope-role retrieval).
5. [P] Every risk on the critical path is *filed with an experiment*, not latent (M1-14 S1/S2/S3, taxonomy task, C.2 sequence).
**Critical (5)**
1. Time: 3 days, two builds (M1-09/10) + taxonomy + purge + rehearsal - zero slack for new scope.
2. Step 4 demo risk: without M1-14 wiring, mapping is shown via API - a technical evaluator will ask to click it.
3. Step 14 absent - deck must frame supervisor as roadmap, or credibility burns.
4. Step 15 mute unless M1-09 lands - the chatbot is the CEO-visible feature.
5. Dataset continuity risk (M1-04) - a laptop failure before Thursday erases the demo plant.
**Rework (5)**
1. Deck: add the discovery screenshot + OR + q-value as the money slide.
2. Pricing/licensing narrative vs implemented gates needs a one-page mapping (A8 below).
3. Rules_v2 addendum (config=identity-only; step-4 artifact; supervisor guardrails) - 1 page, closes audit ambiguity.
4. Demo runbook: scripted fallback path for each live step (source-down, API-down contingencies).
5. Second-meeting asks: define the 2-3 commitments to request (pilot data schema, IT contact, success metric).

## A6 Brand / Website / Story - 63

**Better than design (5)**
1. [P] Honest-refusal assistant behavior is on-brand and live.
2. [T] Website honesty-lint enforced in CI; "Coming soon" badges over vaporclaims.
3. [P] "Even the taxonomy imports" (post-fix) is a *stronger* story than rules v1 required.
4. [T] Sample-data disclosure badge ships in product UI - transparency by default.
5. [P] The null-control (SCRATCH) enables the rare claim "our engine also tells you what's NOT a cause."
**Critical (5)**
1. Story gap: "show me the user doing it" (step 4) - the wiring or an honest framing must exist by Thursday.
2. CRACK_LONG origin story currently "a script inserted it" - fix with taxonomy import before the meeting if possible.
3. Website carries no discovery result; deck and site diverge from the new proof.
4. Supervisor language on any asset must be future-tense until M2.
5. No customer-facing definition of Standard/Pro/Enterprise feature split despite gates existing (A8-1).
**Rework (5)**
1. Rename residual "Two-Stage Delta Import" nav before any screen-share (C.1b).
2. Screenshot set refresh post-purge (empty-start -> filled-by-import sequence).
3. i18n/RTL card claims "Ready" - verify against actual Arabic rendering [U].
4. Assistant refusal copy: make refusal reason customer-readable, not internal.
5. Publish the three Product Rules on the website - they are the brand.

## A7 Data Governance / Auditor (NEW) - 59  << HEADLINE

**Better than design (5)**
1. [T] Row-level provenance: `source_system` + `source_record_id` on every canonical row; `MarkMapped` links staging->canonical id.
2. [T] Genealogy weight trigger (sum=1.0) is a governed-ledger property auditors rarely see.
3. [T] `is_synthetic` flag pervades entities - synthetic/real separable by design.
4. [T] Assistant audit log per ask (4099) with role scoping - answer provenance is queryable.
5. [P] Batch lineage: ImportBatch -> StagingRecord -> canonical proved queryable live (our diagnostics used it).
**Critical (5)**
1. [P] **38,346/40,148 material_units are non-imported**; all 35,906 edges seed - the dataset itself fails audit today.
2. [P] **Taxonomy hand-inserted** (`PPIQ_CONFIG`) - violates the clarified rule the moment it was written; purge + re-import.
3. [P] **Engine input mixes demo and imported data** (204 generator) - any finding is challengeable until retired.
4. [T] **Master dataset unreproducible** (untracked loader) - an auditor cannot regenerate the evidence base.
5. [P] **Out-of-band mutations happened** (psql resets) with no audit trail - governance requires those paths closed or logged.
**Rework (5)**
1. Add `source_system` allow-list check in projection (only registered connector systems may write canonical) [design].
2. Findings need run_id lineage surfaced in UI (exists in table, not shown).
3. Retention policy for staging_records (14k+ rows now permanent) undefined.
4. Deletion semantics: `is_deleted/deleted_reason` exist; no UI/endpoint exercises them [U].
5. Purge script (C.2) must itself write an audit record of what it removed.

## A8 Commercial / Licensing / Deployment (NEW) - 64

**Better than design (5)**
1. [T] Per-endpoint license filter (14+ sites) - enforcement layer correct.
2. [T] `max_users` limit read from license payload (41758) - seat limits are real.
3. [T] Ed25519 signed tokens, committable dev pubkey - offline verification works.
4. [T] Two-project server topology (infra vs app) is deliberate and deployment-sane.
5. [T] Jenkins CI with config backup/restore around git reset - deploys survived prior mistakes.
**Critical (5)**
1. [U] **No tier->feature matrix validated**: which features gate Standard vs Pro Plus vs Enterprise ($12k/$28k/$50k tiers) is undocumented against the actual filter codes.
2. [T] Dev keypair in production path (A2-3) - commercially, licenses are currently unenforceable.
3. [U] ML/AI tier gating (journey: "higher license" for AI+ML and chatbot) unverified against `LicenseFeature` codes.
4. [T] `/license` demo route removed but license admin UX vs sales flow unvalidated [U].
5. Install story: "sysadmin auto-provisioned, Customer Admin manual" (Golden Rule) - no installer/runbook artifact exists [U].
**Rework (5)**
1. Produce the tier->feature-code matrix (1 page) and test one gated endpoint per tier.
2. Rotate to production keypair (Option 3) before any paid pilot.
3. Fold license checks into the smoke walk (viewer vs admin vs unlicensed).
4. Price/feature page (A6-5) once the matrix exists.
5. Backup/restore runbook for customer installs (ties A4-5).

---

## A9 UI/UX & Journey Experience (NEW) - 56  << HEADLINE

*Method note: coloring/visual-hierarchy points are graded from code primitives + design tokens; nothing has been eyeballed in a browser since the 09-Jul packs (M1-02 open). Where only a human can judge, points are marked [U] and the cure is the M1-02 walk itself.*

**Better than design (5)**
1. [T] **Design-system primitives exist and are enforced.** `StandardPageHeader` + `StandardStatGrid` shipped 09-Jul across 8 customer pages; raw buttons/tables retrofitted (T11 debt fixed in prior M1-05 work) - consistency is structural, not per-page luck.
2. [T] **The IA was professionally restructured.** 12 canonical routes renamed with reverse redirects + a strict gate; phase-tokens eradicated from customer-visible navigation; kickers stripped - the journey's *naming* now matches the journey's *language*.
3. [T] **Honest empty/error states are componentized.** `ppiq-state-panel --error` + branded ErrorBoundary with render-time-throw test (PPIQ-202) - failure UX was designed, not defaulted.
4. [T] **Widget wizard is stepped, not a form dump.** data -> filter -> preview -> view steps with live preview before commit - correct low-code pattern for non-software users.
5. [T] **Sample-data disclosure badge** in-product - the user is never silently shown synthetic numbers; trust-preserving UX rare in this market.

**Critical (5)**
1. [T] **The 4th low-code UI does not exist.** Rules define it: plant-data-log/alerting when a parameter exceeds a value, material takes wrong routing, chemistry out of range. Grep across backend+frontend: zero `AlertRule`/notification/threshold-breach machinery - the only "alert" hits are error-boundary panels. A whole journey surface is absent (backend rule-engine + evaluation job + UI). File as a numbered M2 feature with its own spec (trigger types: threshold / routing deviation / chemistry range; delivery: in-app log first, then email/webhook).
2. [T] **Step 4's UI leads the user to the wrong destination.** A user "preparing data" in the mapping panel builds SQL/KPI views that never fill the plant schema (M1-14) - the worst UX failure class: the button works, the outcome is wrong, and the user cannot know.
3. [U] **No human has walked the current build.** All 09-Jul surfaces proven only by tsc/vitest/dotnet; M1-02's 9-row screenshot checklist is still open - visual regressions, RTL, contrast, focus order are all unknowns.
4. [T] **Journey has no in-product guide.** Nothing tells a new user "you are at step 3 of 15; next: author a mapping" - each surface is competent in isolation; the *thread* between them lives only in your head and docs (discoverability carded M2-19; the journey-progress affordance is uncarded).
5. [T] **Taxonomy dead-end UX.** When projection fails on a missing defect/parameter code, the error names the field (good) but no UI path exists to *resolve* it (import taxonomy) - the user hits a wall with a correct error message and no door (couples to the router-gap fix).

**Rework (5)**
1. [T] Two surfaces named "mapping" (schema-views vs definitions) will confuse every user even after wiring - rename by outcome ("Prepare views" vs "Load to plant data").
2. [T] Widget wizard lacks the journey's "lite SQL" helpers as a guided toolbox (group-by/filter/select exist in the `WidgetScript` compiler but are expression-typed, not click-built) [U on exact affordances - M1-14 S2 will grade it].
3. [T] Jobs are invisible mid-journey (M1-10) - after clicking Register, the user gets no "your import is scheduled, next run at X" feedback loop.
4. [T] `/i18n-rtl` card claims Ready - unverified against real Arabic rendering [U]; your market (MENA) makes this a first-demo risk, not a nicety.
5. [P] Error payloads leak internals (full EF LINQ trees + stack traces returned to the client, seen twice this week) - dev-friendly, customer-alarming; production error shape needed.

## A10 AI / Statistics / Engine (NEW) - 58

**Better than design (5)**
1. [P] **The statistics are honest end-to-end.** Pearson + Benjamini-Hochberg q-values (FDR control) with per-finding sample_size - and the pipeline proved it can both find a planted signal (q=0.0001) and *refuse* noise (controls q 0.47-0.87). Multiple-testing correction is more rigor than the rules asked for.
2. [P] **Governed execution, not raw compute.** `ppiq_ml_run_learning_job_governed_v1` wraps every run in a readiness gate (`BlockedTooFewRows` -> `Ready`) with reasons - the AI layer cannot silently run on garbage.
3. [T] **Multi-grain feature/outcome store.** v6 refresh emits features and outcomes per grain with provenance (54,574/91,839 rows live) - the substrate for cross-unit correlation (heat vs slab vs coil) exists.
4. [T] **A knowledge-base seam exists** (`ppiq_ml_upsert_kb_item`/`ppiq_ml_search_kb`) - the engine was designed to accumulate learnings; the precondition for the supervisor concept.
5. [P] **Chatbot read-path architecture is correct.** `AssistantService(IRetrievalIndex, ToolRegistry, IAssistantModel)` - retrieval-grounded, tool-mediated, citation-bearing, role-scoped, audit-logged, refusal-first on empty evidence. The *shape* is exactly right.

**Critical (5)**
1. [T] **There is no LLM.** `IAssistantModel`'s only implementation is `ExtractiveAssistantModel` (line 51358) - extractive answering over retrieved chunks. The journey's "chatbot with some LLM" requires a model binding (local Ollama for on-prem plants; hosted API where permitted) behind the existing interface. Clean swap by design - but absent, and Thursday's framing must say "grounded assistant", not "LLM".
2. [T] **Statistics toolbox is one test deep.** Pearson only - no Spearman (monotonic, outlier-robust: essential for process data), no chi-square (categorical drivers: crew/grade), no ANOVA/Mann-Whitney (group comparisons). `ppiq_ml_compute_basic_correlations` is well-named: basic. The 3rd UI's "toolbox" promise needs a method registry the user picks from.
3. [T] **No drag-and-drop authoring.** The 3rd UI is form-based parameter selection; the rules describe a toolbox of draggable analysis components - carded nowhere. UX + method-registry work (M2/M3).
4. [T] **"Jobs improve each other" is not implemented.** No mechanism feeds one job's output into another's configuration; the supervisor (step 14) is absent; the KB seam is unused by any job. Today: independent jobs + a shared store. The differentiator remains design.
5. [T] **100 concurrent jobs: not supported.** Concurrency inventory: 2 Channels, one ConcurrentQueue (compute-run reaper), ConcurrentDictionary - no SemaphoreSlim, no MaxDegreeOfParallelism, no job-queue executor. Jobs execute on worker ticks, serially per tick, against one Postgres. At 100 defined jobs the system *schedules* them but drifts unboundedly; heavy analysis SQL (feature refresh = full-store rewrite) serializes behind imports. Needs: bounded-parallelism executor (Channel<JobRequest> + N consumers), per-job-class pools (import / analysis / ML), statement timeouts, NOTIFY wakeups. An M2 architecture task, not a tweak.

**Rework (5)**
1. [P] Dedup/version `ml_correlation_results_v2` reads (duplicates visible live) - one latest-run-per-job view.
2. [T] Feature refresh is full-rewrite (54k rows/run); make it incremental per import-batch - the single biggest scale lever.
3. [P] Surface odds-ratio + population next to effect/q in the finding payload (deck + engineer trust).
4. [T] Expose readiness reasons in the Surface-3 UI (they exist in the function result; users should see *why* blocked).
5. [T] Wire `ppiq_ml_search_kb` into the assistant's ToolRegistry as a 4th tool - the two AI halves don't talk today.

## A11 Infrastructure Engineer (NEW) - 62

**Better than design (5)**
1. [T] **Two-project server topology is deliberate and correct** - infra stack (Jenkins/Caddy/backup-runner) isolated from app deploys; survived a config-overwrite incident by design (Jenkinsfile backs up .env/Caddyfile before git reset).
2. [T] **Read-side is throttle-armored** - row caps, rate limits, approved windows on every source read; a misconfigured import cannot flatten a customer's production DB. The #1 plant-IT objection, pre-answered.
3. [P] **Native PG16 handles current scale trivially** - 40k units + 92k outcome rows + full refresh + correlation in seconds on a dev laptop.
4. [T] **Reapers + backoff give crash-only behavior** - stuck runs self-clear (30min/5min); schedules back off on failure; no operator babysitting.
5. [T] **CI/CD exists with webhook deploys** - rare for a one-person product at this stage.

**Critical (5)**
1. **No sizing model existed - here is v1 (validate against pilot telemetry):**

| Plant profile | Units/yr | Params | Observations/yr | QEs/yr | Jobs defined | DB growth | Recommended infra |
|---|---|---|---|---|---|---|---|
| Small (1 line) | ~50k | 15 | ~750k | ~25k | 10-20 | 15-30 GB/yr | 1 VM: 4 vCPU / 16 GB, PG16 same host, NVMe |
| Medium (steel ref: heat+slab+coil) | ~250k | 30 | ~7.5M | ~150k | 30-60 | 100-200 GB/yr | App VM 8 vCPU/32 GB + dedicated PG 8 vCPU/64 GB NVMe + PgBouncer |
| Large (multi-line) | ~1M | 60+ | ~60M | ~500k | 100+ | 0.5-1 TB/yr | App x2 behind LB + PG 16 vCPU/128 GB, partitioned obs/feature tables, analytics read replica |

  Stated assumptions: 1 reading/param/unit (true time-series multiplies observations 10-100x -> partitioning mandatory at Medium); feature store currently full-rewrite (blocks Large until A10-R2).
2. [T] **parameter_observations + ml_feature_values are unpartitioned** - at Medium (7.5M obs/yr) refresh/correlation windows degrade; declarative monthly partitions + BRIN on observed_at_utc before the first paid pilot.
3. [T] **Single Hetzner VPS, no HA, restore untested** - backup-runner exists; a restore drill does not [U]. The C.2 purge this week must be preceded by a local snapshot.
4. [T] **No connection-pooling story** - 100 jobs + API + workers on raw PG connections exhausts default max_connections; PgBouncer (transaction mode) is a one-evening add preventing a class of pilot outages.
5. [U] **No resource telemetry** - no job-duration/row-count/bloat metrics; the sizing model above cannot be validated without it. Minimal: pg_stat_statements + a jobs-duration table (M1-10's data doubles as this).

**Rework (5)**
1. [T] Incremental feature refresh (shared A10-R2).
2. [T] Statement timeouts + per-job-class work_mem (analysis SQL must not inherit API defaults).
3. [T] Staging retention (archive Mapped rows >90 days) - keeps the hot set small (shared A7-R3).
4. [P] Source-stack compose drift (container name conflict seen live) - `external: true` network + idempotent up.
5. [T] Demo assets to durable off-laptop storage TODAY (shared M1-04) - continuity, not just governance.

## Journey Deep Matrix (15 steps vs implementation)
| # | Journey step (rules.txt) | Status | Evidence | Gap owner |
|---|---|---|---|---|
| 1 | Create/configure DB-link to customer sources | WORKS [P] | profiles CRUD; by-id 500 fixed; ReadOnlyEnforced | browser walk M1-02 |
| 2 | Link DB-link -> import job (schedule+monitor) | PARTIAL [T] | register schedules (`ScheduleNextRunImmediately`); worker ticks; *visibility* missing | M1-10; M1-05b |
| 3 | Incremental import -> staging (per-cycle delta) | PROVEN [P] | 1,802 then 0; ISO cursor; 3x re-proven (14,416/420 in throttled chunks) | - |
| 4 | 1st no-code UI: prep/filter/link/group + match schema | AT RISK [T] | UI drives `canonical_schema_views`; projector consumes `MappingDefinition`; no bridge | **M1-14 verdict -> wiring task** |
| 5 | Link prep file -> data-loading job | PARTIAL [T] | auto project-on-import exists (queue processor); per-view Loading jobs = M2-04 | M2-04 |
| 6 | Data loaded to plant schema | PROVEN [P] | 3 projections, idempotent, NOT-NULL held, typed errors on gap | - |
| 7 | 2nd no-code UI: dashboards/widgets/KPI, formulas/casting | EXISTS [T] | wizard (data/filter/preview/view) + WidgetScript SQL compiler | M1-14 S2 live bind test |
| 8 | 3rd no-code UI: analysis/correlation authoring + tools | EXISTS [P] | Surface-3 shipped (M1-05 prior); governed run path proven | M1-14 S3 |
| 9 | Analysis job scheduled/monitored | PARTIAL [P] | governed run Completed live; scheduling/monitor surface thin | M1-10 |
| 10 | Results dashboards from analysis tables | PARTIAL [T] | results_v2 populated (33 rows); dedup + widget binding unverified | A3-crit-3; M1-14 S2 |
| 11 | Higher license: AI+ML authoring via same UI | PARTIAL [U] | ML layer runs; tier gating unvalidated | A8-1/3 |
| 12 | AI+ML job scheduled/monitored | PARTIAL [P] | v6 refresh + governed job run live; monitor absent | M1-10 |
| 13 | AI+ML results dashboards | PARTIAL | same as 10 | - |
| 14 | **Engine supervisor** (weekly deep revision of all jobs) | **ABSENT** [T] | zero code; no spec | **M2 P0 + guardrail spec** |
| 15 | Chatbot answering *from the engine* | PARTIAL [P] | wired, honest refusal, scope_role designed; store empty | **M1-09 (next build)** |

## Rules Compliance
- **R1 Generic Only:** mostly enforced [P/T] - projection grep-clean; ladder unreachable; residue = C.1b/C.2 + 204-feed retirement.
- **R2 Starts Empty / DB-link only:** violated by standing data [P]; the compliant path is proven three times over; taxonomy clause now also requires the 2 new mappers + purge of PPIQ_CONFIG.
- **R3 Journey:** spine proven (1-3,5,6,8-10 partially/fully); 4 at-risk, 14 absent, 15 mute.

## Sequence to Thursday
M1-09 -> M1-10 -> taxonomy mappers -> C.1b -> **DB snapshot** -> C.2 purge + full re-import (definitions first) -> clean M1-08 re-run -> M1-14 S1/S2/S3 + HMI walk (=M1-02) -> M1-12. Parallel today: M1-04 archive.

# PlantProcess IQ — Deep Technical Session Handover
## 15-Aug-2026 — current Tech-Lead session → next ChatGPT session

**Repository:** `C:\Workspace\PlantProcess-IQ`  
**Current authoritative backlog:** `PPIQ_Backlog_v2_10_1_12Aug2026.md/.xlsx`  
**Current date/time at handover:** 15-Aug-2026, ~15:39 +03:00  
**Purpose:** preserve the exact implementation state, executed evidence, debugging discoveries, architecture rulings, worker sequencing, server/pipeline history and management laws so a new session continues immediately instead of rediscovering or rerunning already-proven work.

> **Security:** secret literals, passwords, private keys and tokens are deliberately not copied into this handover. Use the project Identity/Topology and protected environment/profile files as the authority. Local host/user/database names are preserved where useful; do not invent credentials.

---

# 0. ABSOLUTE START POINT — READ BEFORE DOING ANYTHING

## 0.1 The user said the new session may think it should start at T-175. That is now stale.

**DO NOT START T-175. T-175 is already completed and committed at `2413c6e1`.** Starting it again would waste tokens, repeat dependency/debug work, and risk cross-lane contamination.

The correct current execution points are:

```text
WORKER 2 / PRESENTATION LANE
  M1 populated-data PostgreSQL recertification = GREEN (17/17, non-vacuous)
  CURRENT = T-045-R1-C v3 (Risk provenance / contributors / temporal-history truth)
  NEXT    = T-045-R1-B Correlation evidence/execution LAST
  THEN    = T-045-R1 closure gate
  THEN    = T-044 bounded corrective remainder
  THEN    = T-046-R1 Heatmap + Combo/paired renderer only
  THEN    = T-047 FINAL seven-page visual/product certification

WORKER 3 / SAFE-NOW AI+ML LANE
  T-178 base = CLOSED at b8792516 / 501 Python tests
  CURRENT = T-178 W3-023 canonical precedence corrective
  NEXT    = T-179 deterministic Assistant tool planner
```

## 0.2 Exact resume keyword to paste into the next session

```text
RESUME-PPIQ-15AUG-CURRENT-STATE
W2=T045-R1-C-v3
W3=T178-W3-023-CORRECTIVE
T175=CLOSED-2413c6e1-DO-NOT-REOPEN
BACKLOG=v2.10.1
DO-NOT-RERUN-PROVEN-TESTS-UNLESS-CHANGED-CODE-INVALIDATES-EVIDENCE
```

If the next session only follows one worker, still give it this full key so it does not resurrect stale task context.

## 0.3 First commands in a shared worktree

Before any mutating pack:

```powershell
cd C:\Workspace\PlantProcess-IQ
git status --short
git diff --cached --name-status
git log --oneline -12
```

**Never reset, checkout, clean, restore, unstage or overwrite another worker's live files merely to obtain a clean tree.** A non-empty index at the start of staging is a lane-collision signal.

---

# 1. AUTHORITY HIERARCHY AND USER MANAGEMENT LAW

The current project skill/workflow requires: doctrine/vision first, actual code/DB/tests/logs as evidence, task scoring from executed acceptance, backlog revision without losing traceability, and strict parallel-worker isolation. Planned text is not implementation evidence.

Use this evidence hierarchy:

```text
executed runtime / DB / browser evidence
  > executed tests
  > current source trace
  > commit/closure evidence
  > handover notes
  > backlog/design intent
```

The user's management law is now explicit:

- **COMPLETE ≠ BIGGER.** Complete means every written requirement + truthfulness prerequisites + acceptance evidence. Anything beyond that is drift.
- Time estimates are diagnostic signals, **not quality ceilings**. Fast AI execution is acceptable if scope/evidence is complete.
- Findings must be classified: current mandatory scope; smallest required prerequisite; existing future owner; improvement/refactor parking lot.
- No bare “later”. A deferred item must state why it is not current acceptance, its owner task, dependency and when it becomes blocking.
- Investigation must answer one executable question and stop once answered.
- Green acceptance → **COMMIT / CLOSE / MOVE**. No opportunistic improvement pack after green.
- Never weaken a written acceptance test merely to close faster.
- Never reopen frozen/proven decisions without a concrete contradiction, failing test or new executable evidence.
- Full suites are phase/gate evidence, not a ritual after every tiny edit.
- For pack machinery, fix concrete generator/anchor defects directly; do not turn pack mechanics into mini-projects.

### Task-lock protocol to prevent stale-context drift

Every worker should begin a pack with:

```text
TASK LOCK
Current task:
Last accepted task/commit:
Allowed paths:
Forbidden/reopened tasks:
```

and close with:

```text
CLOSE LOCK
Task:
Commit:
Acceptance:
Next task:
```

This protocol was added after Worker 2 drifted backwards toward old T-047 Pack A work and Worker 3 resurrected T-175 despite both already being far beyond those points.

---

# 2. PRODUCT IDENTITY — NON-NEGOTIABLE

PlantProcess IQ is a **generic cross-industry manufacturing BI + deterministic analytics + governed intelligence platform**. Fleet-v2 / steel is demo/reference data, not product identity. The same product must onboard steel, aluminium, tyres, food, bottling, paper, pharma, cement, refining and future industries by data/metadata/mappings/relationships — not by customer-specific C#/React branches.

## 2.1 Truth layers

- **Layer A:** exact BI/facts — counts, sums, filtered/grouped KPIs, deterministic exact queries.
- **Layer B:** statistical/learned — correlation, similarity, novelty, risk, prediction, contribution, practice/remediation evidence.
- Never use ML to approximate an exact Layer-A fact.

## 2.2 Assistant law

```text
engines calculate
→ governed evidence/results
→ tenant/permission-scoped tools/retrieval
→ LLM explains/qualifies/cites
→ deterministic verifier
```

The LLM does not choose tools, manufacture plant numbers, replace engine refusal, erase uncertainty, or upgrade association into causality.

## 2.3 Human/read-only boundary

Customer systems are read-only. No autonomous writeback/control. Remediation can be suggested/evidence-ranked; a human Accept/Reject/Defer decision is recorded later under governed server authority.

## 2.4 Genericity law

Generic product code must not branch on steel, Fleet-v2, customer name, one dashboard, one parameter code or one seeded widget. Presentation seed content may be plant-specific; product services may not.

---

# 3. POST-M1 GENERIC BI DISCUSSION — SAVED FOR AFTER M1, DO NOT MODIFY BACKLOG YET

This discussion was explicitly saved by the user for later. **Do not turn it into new tasks during M1.**

## 3.1 Branch strategy after M1

After M1 closes:

```text
Presentation branch
  = stable M1 customer-demo/presentation baseline
  = known presentation dataset/profile
  = reusable for future customer presentations

main branch
  = real generic product
  = ppiq_app / final canonical architecture
  = generic DB + backend + frontend + AI/ML integration
```

## 3.2 Correct Generic BI target

The earlier phrase “do not make BI too dynamic” was corrected. The target is **highly dynamic, metadata-driven BI comparable in authoring flexibility to Qlik Sense / Power BI**, but with governed execution, manufacturing semantic truth, grain/cardinality protection, tenant isolation and read-only safety.

Customer-specific analytical content should be **configuration/data, not application code**. In ordinary onboarding, the customer should be able to add/edit/remove: pages, widgets, dimensions, measures, calculated expressions, filters, hierarchies, relationships, sorting, grouping, formatting, drill and selections without React/C#/product SQL code changes or a product redeploy.

The governed product grammar stays code-owned: security/RLS, allowed expression operators, query limits, chart-renderer implementation, relationship/cardinality rules, permissions, ML authority and write restrictions.

## 3.3 Important decision: proposed backlog changes are NOT approved yet

A prior analysis proposed +~34h and two candidate tasks (P1-G01/P1-G02) for generic query/filter/frontend runtime. **Those are analysis candidates, not backlog rulings. Do not add them yet.**

After M1: freeze a Generic BI Product Contract, compare it against M2a-P1/P2 **and the actual final M1 code**, then classify each requirement as already covered / acceptance weak / scope expansion / genuine new task. Only then revise v2.11.

---

# 4. CURRENT TOPOLOGY AND ENVIRONMENT

## 4.1 Local laptop

- Repo: `C:\Workspace\PlantProcess-IQ`
- Main local PostgreSQL: native Windows PostgreSQL 16 on `127.0.0.1:5432`; **not Docker**.
- `ppiq_app`: default local/development/integration DB.
- `ppiq_presentation`: populated M1 presentation/certification DB.
- API: `http://localhost:5063`.
- Frontend dev: `http://localhost:5173`.
- Demo/customer source emulators may run as Docker containers; do not confuse them with the main native DB.
- Python ML project is separate under `ML/`; no `.py` under Backend/tools as product implementation.

## 4.2 Database validation ruling discovered this session

`ResolveIntegrationTestConnectionString()` defaults to `ppiq_app`. This caused some M1 presentation proofs to be logically valid but potentially vacuous while manual `psql` probes were looking at `ppiq_presentation`. The correct permanent rule is:

```text
generic integration correctness → ppiq_app default is fine
M1 populated presentation certification → explicitly set PPIQ_TEST_CONNECTION_STRING to ppiq_presentation for that gate
```

Never change the global resolver merely to make presentation certification convenient.

## 4.3 Historical server topology — not current certification

- Historical host: `178.105.152.180`.
- Historical app: `https://app.178.105.152.180.sslip.io`.
- Historical API: `https://api.178.105.152.180.sslip.io`.
- Historical website: `https://website.178.105.152.180.sslip.io`.
- Historical Jenkins: `https://jenkins.178.105.152.180.sslip.io`.
- Long-lived infra project: `plantprocessiq` (Caddy/Jenkins/backups).
- App deployment project: `ppiq-app`.
- Only Caddy exposes public 80/443; app/API/PG stay internal/private/loopback.
- Server main PostgreSQL is Dockerized, unlike the laptop native DB.

Do not say these URLs are currently production-certified unless reverified in the new session.

---

# 5. CURRENT ROADMAP / BACKLOG AUTHORITY

Backlog v2.10.1 contains **193 tasks / 1,738 hours** across 18 phases. Phase intent:

| Phase | Tasks | Hours | Current meaning |
|---|---:|---:|---|
| M1-P1 | 12 | 84 | Presentation truth/dataset foundation |
| M1-P1b | 17 | 114 | Fleet-v2 capture/reconcile/scale/materialise/prove |
| M1-P2 | 11 | 107 | No-code authoring shell |
| M1-P3 | 12 | 80 | BI workspace + seven showcase pages |
| M1-P4 | 16 | 106 | Journey J4-J15 + engine slice |
| M1-P5 | 15 | 83 | Assistant dock + presentation certification |
| M2a-P1 | 11 | 114 | Canonical schema authority + unified definition store |
| M2a-P2 | 11 | 92 | Permanent relationship model + projection quarantine |
| M2a-P3 | 12 | 106 | Job runtime/delta/security |
| M2a-P4 | 10 | 120 | Commissioning/roles/licence/on-site package |
| M2b-P0A | 8 | 86 | SAFE-NOW ML runtime/data-artifact foundations |
| M2b-P0B | 8 | 86 | SAFE-NOW model/statistical/Assistant kernels |
| M2b-P1 | 8 | 92 | Canonical intelligence persistence + practice |
| M2b-P2 | 7 | 80 | Model integration/intelligence binding/readiness |
| M2b-P3 | 10 | 104 | Prediction/remediation/canonical surfaces |
| M2b-P4 | 8 | 80 | Assistant cutover/engine convergence/gates |
| M3-P1 | 8 | 96 | Site stabilisation/real-data performance |
| M3-P2 | 9 | 108 | Production certification/enterprise ops |

### Important architectural split introduced in v2.10.1

Worker 3 SAFE-NOW tasks implement isolated pure contracts/runtimes/tests only. They do **not** imply production integration. Final persistence/integration owners remain T-183..T-190 and related M2b tasks after M2a schema/relationship hand-off.

---

# 6. WORKER 2 — PRESENTATION / BI LANE: FULL CURRENT STATE

## 6.1 T-046 — original chart grammar is closed

Original T-046 closed at `fc146483b1132a89d181d6508f495a5ddeb08765`. It established 17 chart types, availability vs compatibility separation, server refusal verbatim, unavailable ≠ incompatible, retired `extendChartTypes()`, and removed decision-tree logic from React. Do not broadly reopen it.

A later **T-046-R1 bounded corrective** is planned only for the specific missing renderers required by final T-047: **Heatmap + Combo/paired renderer**. Nothing more.

## 6.2 T-047 authoritative final grammar

The final seven-page acceptance remains:

- Production: KPI/trend, stacked column by shift, bar by grade, area weekly throughput, detail table.
- Quality: KPI sparkline, Pareto defect type, stacked bar by grade, positional heatmap along material length/width, conditionally formatted chemistry table.
- Equipment: bar, Pareto, paired-column stoppage vs production impact.
- Parameter Deep Analysis: histogram, box plot by grade, scatter.
- Correlation: parameter×outcome heatmap, ranked contributor bar, before/after conditioning pair.
- Risk: KPI, trend, distribution, contribution table — but trend must refuse if current temporal evidence is insufficient.
- Model Insights: readiness status cards with five dimensions and evidence/thresholds, no fabricated prediction curve.

### T-047 accepted packs

- **Pack A:** Histogram semantic source + renderer + Npgsql execution proof. Build clean, architecture green, Npgsql 3/3, frontend targeted 22/22, architecture ratchets 4/4. The source `PARAMETER_NOT_SELECTED` state is defence-in-depth because validation normally refuses earlier.
- **Pack B:** `0074582203b819e23349fbdf2af36b5de311543c`. Native BoxPlot spread + quartile kernel. Backend build, architecture, PostgreSQL 3/3, TypeScript, 27 targeted frontend tests, 4 architecture ratchets.
- **Pack C1:** `dc0b0e16b42d7322dd671ea2b8a88f672634c11e`. SQL grammar/bindings for histogram+box.
- **Pack C2:** `124276dcb179fa92d3a6635a095165681929b1db`. True `parameterRelationship` source/scatter; PostgreSQL 3/3; Parameter Deep Analysis complete.
- **Pack D:** `413df51aa4960ccc903d665098de91ccd6a21910`. Native throughput-by-shift and defectTypeMix, stacked renderer roles, 4/4 PostgreSQL proof, build/arch/ts/frontend ratchets green.
- **Pack E:** `a16b7b31`. SQL-only seven-page composition adds PO_AREA, PO_BAR, QM_KPI, EO_PARETO, RI_DIST and converts QM_SEV to Pareto. T-047 deliberately STOPPED/BLOCKED after Pack E rather than inventing Packs F/G/H.

## 6.3 Exact six T-047 blockers found after Pack E

1. **Quality positional heatmap:** source generator has defect code + length/width positions, but presentation canonical `QualityEvent` lacks positional fields. First missing layer = presentation canonical; owner = bounded T-044 remainder.
2. **Quality chemistry/specification:** source `grade_specification` exists/populated, but no canonical spec materialisation. First missing layer = presentation canonical; owner = bounded T-044 remainder.
3. **Equipment stopped vs production impact:** canonical `DowntimeEvent` already has both `StoppedMinutes` and `ProductionImpactMinutes`; R1-D has now created the native analytical surface. Remaining gap = T-046 Combo/paired renderer.
4. **Model Insights readiness:** gate had measured values+thresholds but transport originally dropped them. **Fixed by T-045-R1-A.**
5. **Correlation:** canonical table exists but current `correlation_results` was measured at 0 rows; zero supported findings can be truthful. Need bounded existing execution/certification in R1-B; never weaken threshold or fabricate findings.
6. **Risk:** current `RiskScore` rows exist, but job status alone cannot infer provenance. R1-C now owns row-level provenance, contributors and temporal truth.

## 6.4 T-045-R1-A — CLOSED `c56008c0`

Defect: readiness gate judged measured values against ready/partial thresholds, then DTO/widget narrowed result to only Name/State/Reason. Fix carries:

```text
dimension
measuredValue
readyThreshold
partialThreshold
higherIsBetter
state
reason
```

Evidence: 15/15 anchors, build clean, 7/7 known answers, full Analytics.Core green, architecture green. Six files, +286/-22. No defaults on new fields by design, so future mapping cannot silently omit evidence.

Carried only: `T045-R1-A-F01` unit metadata absent; do not infer units by dimension name. MI_RATE visual rendering belongs T-047 final.

## 6.5 T-045-R1-D — CLOSED `283aae2c`

Added `equipmentStoppageAndImpact` semantic/native source with roles: `state`, `equipmentId`, `equipmentCode`, `equipmentLabel`, `stoppedMinutes`, `productionImpactMinutes`. Both totals are independent SUMs over the same downtime-event population. No ratio, derivation, schema change or renderer.

Initial integration DB had only one downtime event, so the logical proof was correct but presentation claim was weak. Later populated-data recertification reran it against **630 downtime events**, proving totals against independent source aggregation.

The commit showed large +1535/-1124 churn because a pack normalized line endings in `WidgetResultSources.cs`. A read-only `git diff --ignore-space-at-eol c56008c0 283aae2c -- WidgetResultSources.cs` confirmed the logical delta was only the intended source. Record `W2-PACK-LE01`; do not create a cleanup task now. Future packs should preserve newline convention and targeted edits.

## 6.6 M1 populated-data PostgreSQL recertification — GREEN, do not rerun for orientation

Discovery: generic integration tests default to `ppiq_app`, while manual M1 probes were on `ppiq_presentation`. Some earlier presentation proofs could pass on empty/near-empty data. Worker 2 correctly refused to weaken tests.

A run-only recertification gate explicitly resolved `ppiq_presentation`, set `PPIQ_TEST_CONNECTION_STRING` for that process only, printed the target, and refused vacuous evidence.

| Population | Measured count |
|---|---:|
| ParameterObservations | 301,560 |
| RiskScores | 500 |
| DowntimeEvents | 630 |
| CrewSteps | 3,780 |
| QualityEvents | 7,844 |
| GradedMaterials | 35,915 |
| OverlappingPairs | 160 |

Executed **17 tests across 5 suites; all PASS; all non-vacuous**:

- Parameter + Risk distribution: 3/3.
- Parameter spread: 3/3.
- Parameter relationship: 3/3.
- Production + Quality multi-series: 4/4.
- Equipment stoppage + production impact: 4/4.

Therefore Packs A/C2/D and R1-D remain closed; no implementation task reopens. Permanent test-design rule: a product correctness test may accept Empty/Blocked/Refused, but a populated-execution certification must independently prove the source population required by the claim.

## 6.7 CURRENT Worker-2 task — T-045-R1-C v3

At handover, recertification is green and Worker 2 is ready to cut R1-C v3. **No R1-C commit has been shown yet.** Production code design remains the reviewed v2 design; v3 only fixes certification target/non-vacuity.

Current measured `ppiq_presentation.risk_scores` truth:

```text
rows                          500
IsSynthetic=true              500/500
ModelVersion present          500/500
SourceSystem present          500/500
SourceRecordId present        500/500
MainContributorsJson present  500/500
ExplanationJson present       500/500
risk types                    1
distinct scoring days         1
scoring span                  ~27 seconds
```

`MainContributorsJson` is JSON array; some rows are `[]`, some contain structured objects such as `contributorCode`, `contributorName`, `contributorType`, `weight`, `direction`, `contribution`, `explanation`.

Required R1-C truths:

- Synthetic status **is proven** by row-level `IsSynthetic=true`. Do not call rows seeded/demo/job-generated/production-model-generated unless separate lineage proves it.
- Contribution surface publishes only actual persisted contributor objects; empty arrays remain honest no-contributor evidence; malformed/incompatible JSON refuses.
- Current temporal population is one legitimate period; return **`INSUFFICIENT_TEMPORAL_RISK_HISTORY`**, not a one-point/fake trend. A deterministic multi-period fixture must prove the contract can publish a true trend when real history exists.
- PostgreSQL gate must assert DB=`ppiq_presentation` and `risk_scores > 0` before certification.

After R1-C commit, **R1-B correlation is last**. Do not start correlation before R1-C closes.

## 6.8 R1-B correlation — frozen intent, not started

Current evidence: `correlation_results` measured zero published rows; job definitions reported `Ok`, but `Ok` does not prove valid zero vs wrote nothing. R1-B is a bounded execution/certification of the existing T-045 statistical path. Outcomes allowed: supported findings with evidence **or** truthful `NO_SUPPORTED_FINDINGS_CURRENTLY_PUBLISHED` with proof. Never fabricate, lower thresholds or relabel unrelated data as correlation.

## 6.9 Worker-2 sequence after T-045-R1

```text
T-045-R1-C  ← NOW
T-045-R1-B  ← LAST
T-045-R1 closure gate
T-044 corrective remainder: positional defects + grade specification only
T-046-R1: Heatmap + Combo/paired renderer only
T-047 FINAL: bind truthful surfaces + screenshots + common design-language certification
```

No broad reopen of T-024/T-025/T-044/T-045/T-046. Corrective remainders occur only at the first missing layer.

---

# 7. WORKER 3 — AI/ML/LLM SAFE-NOW LANE: COMPLETE TECHNICAL LEDGER

## 7.1 Frozen architecture / C#–Python split

- C#: control/governance/orchestration/API/jobs/Layer-A/registry/readiness/promotion/persistence/Assistant planner-verifier boundary.
- Python: heavy ML computation in separate `ML/` project.
- Boundary: versioned JobSpec + sealed typed snapshot/artifact → Python → structured ResultManifest/artifacts → C# validates/gates/registers/persists.
- Python training must not freely query PostgreSQL; SnapshotMaterialiser is later sole feature_store reader for sealing.
- Semantic Contract Manifest = immutable tenant/content/version reproducibility pin; production persistence later T-183.
- Sequence payload chunks in object/artifact storage; PostgreSQL only manifests/metadata. T-170 is isolated library; T-185 later persistence.
- VectorSimilarityIndex contract with ExactFlat permanent correctness oracle; ANN candidates are replaceable.
- Text/docs retrieval only grounds/cites Assistant answers; it cannot manufacture plant scores.

## 7.2 T-168 — CLOSED

Commits: `16ea041a` Python runtime, `94ab4e40` C# protocol, `723b9434` real C#→Python→C#, `6aef283f` closure evidence. W3-016 CI dependency defect fixed `ef588f0c`. Python runtime had 34 tests; C# protocol suite is 35 tests in the lane; full Backend regression was run after CI dependency correction with no skips.

Key learning: runtime truth comes from structured result manifest and process exit/stdout/stderr identity; never parse free-form logs as model truth.

## 7.3 T-169 — CLOSED `a887c554`

Typed columnar artifact abstraction with Parquet + Arrow IPC adapters, column projection, schema/type mapping, deterministic order, corruption detection and B-03 benchmark hook. Critical distinction: **logical content hash is format-independent; byte hash is physical-format identity**. 81 Python tests green.

## 7.4 T-171 — CLOSED

Capability Profiler returns Available/Degraded/Unavailable based on declared facts, without DB access. Collapsed one-level dimension is not an error; genealogy absence degrades only methods that require it; missing outcomes degrades supervised/practice; attribution distinguishes Declaration vs Data vs DataModel. Pack 2 `95cc019c`, 24 targeted tests. Correction retained: T-102 is identity resolution; grain ownership remains split across T-094/T-095/T-147.

## 7.5 T-177 — CLOSED

Statistical method kernel with source/test parity. 52 targeted tests: Parity 9/9, T-177 13/13, P06 30/30. Commits recorded: `502f8ca3`, `502e0da9`, `bb4c5686`. Numeric×Categorical uses assumption-aware ANOVA with Kruskal-Wallis fallback. Production cutover remains later T-146/T-147; do not replace current presentation engine while Worker 2 is certifying M1.

## 7.6 T-175 — CLOSED `2413c6e1` — DO NOT RESTART

This is the task the user feared the new session would repeat. It is already fully implemented.

Implementation: typed fixture OutcomeDefinition + sealed T-169 artifact → leakage/eligibility gate → mandatory `PriorBaseline` → LightGBM candidate → shared out-of-time holdout → structured metrics / T-168 ResultManifest. Supports binary, multiclass, ordinal and continuous. Post-cutoff feature injection is blocked. No candidate becomes champion here; no promotion/activation/deploy/serve entry point.

Dependencies pinned in the task: `lightgbm==4.7.0`, `numpy==2.4.4` (with pyarrow inherited from artifact work). Final suite: **141 tests OK, 0 skipped**, up from 81. SAFE-NOW only: no `ppiq_app` binding, no customer data, fixture-declared outcome semantics. Production integration remains T-187 after canonical SM-06 binding.

## 7.7 T-176 — CLOSED `08b54b61` + `0a61ccfb`

Pack 1 built pure promotion kernel across QUALITY / SERVING / TRAINING and encoder inequality. User correctly rejected closure when explanation stability was only synthetic/constant evidence. Pack 2 added a real LightGBM TreeSHAP provider behind `ExplanationProvider`.

Evidence: base value + contributions reconstruct raw model output to ~`4.4e-15`; contribution claim class = predictive contribution, never causality; stability from fitted-model explanations; mismatched snapshots/holdouts/metrics refuse. Suite progression **141 → 195 → 230**, final 230 OK / 0 skipped.

Critical lesson: tests written by the same implementer can green a misread contract. T-176 Pack 1 proved this. Always compare to frozen task semantics and falsify the real candidate, not only a mathematical helper.

## 7.8 T-173 — CLOSED `00a14286`

VectorSimilarityIndex with ExactFlat correctness oracle + dependency-free approximate `PartitionedProbe` candidate. Approximate candidate is measured against exact neighbors on same population/metric. Deliberately degraded candidate got recall ~0.6933 below 0.90 floor and became ineligible despite speed. Second unrelated population proved genericity. FAISS remains an optional candidate, not product contract. **297 tests OK / 0 skipped.**

## 7.9 T-174 — CLOSED `db033796`

MF-03 novelty runtime: robust-deviation baseline + neighbour-density candidate using T-173 ExactFlat; planted outliers; small/degenerate population refuses; refusal manifest leaves no fabricated scores. **340 tests green.**

## 7.10 T-170 — CLOSED `5a216483`

Immutable chunked typed numeric sequence artifact with explicit little-endian layout, compression seam, footer chunk index, per-chunk hashes, per-channel/payload logical hash and mmap/bounded reader. Structural bugs found/fixed during work: payload hash originally depended on chunk size; manifest-open risked whole-file hashing; compression ratio wrongly included footer; arbitrary memory assertions replaced by falsifiable “peak tracks chunk, not payload”. **402 tests OK / 0 skipped.**

Important property: logical payload identity is independent of codec/chunk size; physical chunk hashes change with layout. No PostgreSQL numeric array payloads. Persistence later T-185.

## 7.11 T-172 — CLOSED `d4321856`

Real PyTorch 2.13.0 CPU temporal-convolution ProcessEncoder behind replaceable contract. `channel_set_version` + channel-set identity; seed/environment manifest; train/encode/artifact contracts; serving-cost telemetry; input only sealed T-170 sequence artifacts. User host default torch threads were 14; code pins 1 for reproducibility.

Cross-process embeddings/logical artifact identity reproducible within **1e-5**; serialized `.pt` bytes are explicitly not required to be byte-identical. Refuses insufficient channels, invalid/ragged/wrong windows, too few windows, wrong/reused channel set version, missing/corrupt/non-encoder artifact. **448 tests OK / 0 skipped.**

W3-022: MF-01 sits under `encoders/` while MF-03/04 sit under `models/`; ruled **RESOLVED / ACCEPTED TOPOLOGY**, not technical debt. Do not refactor for symmetry absent a concrete packaging/dependency failure.

## 7.12 T-178 base — CLOSED `b8792516`, corrective still pending

Base implementation: 8 new files, 1,442 insertions, no modifications/deletions, nine RM checks, four states, seven-condition `can_accept`, deterministic failed-check/blocker order, no DB/API/UI/control client. **501 Python tests OK / 0 skips** from 448; exhaustive 512 check-combination coverage.

Worker raised W3-023 because the four frozen precedence rows did not cover every 9-check combination. The base implementation used an overly broad derived rule (“any RM01–RM04 failure → evidence_only”). This is the one required correction before T-179.

### W3-023 exact corrective ruling — CURRENT Worker-3 task

```text
1. if RM04 fails
      → suppressed

2. if RM01..RM09 all pass
      → actionable

3. if RM05..RM09 ALL pass
   AND one or more of RM01/RM02/RM03 fail
      → evidence_only

4. every other non-safety failed combination
      → exploratory
```

Explicit regression cases required: RM01 only→evidence_only; RM02 only→evidence_only; RM03 only→evidence_only; RM05 only→exploratory; RM09 only→exploratory; RM01+RM05→exploratory; RM03+RM09→exploratory; RM04+anything→suppressed; no failures→actionable. Rerun exhaustive 512 combinations + full ML suite, exact-stage only corrective files/tests, commit `T-178 corrective: canonical remediation precedence totalisation`. **No corrective hash has been shown by handover time.**

W3-020 remains deferred repo hygiene: `tools/packs/backup/` and `tools/packs/trx/` generated output should eventually be ignored in root `.gitignore`; do not let Worker 3 edit shared root during active lanes.

## 7.13 Next after corrective — T-179

Deterministic Assistant tool planner. Inputs = resolved tenant/permission + resolved intent/canonical entities + declared Layer A/B tool registry. Output = auditable deterministic plan. LLM never participates in tool selection. Equivalent paraphrases → same tool plan; ambiguity → clarification/no guessed execution; forbidden tool absent; structured tools preferred for exact facts; unsupported intent → unsupported/refusal; zero LLM calls during planning. No current dock/DI/runtime cutover; T-138 owns final integration.

## 7.14 Worker-3 speed/rework assessment

Worker 3 closed many 10–12h-estimated tasks in ~1–2h agent wall-clock. Current assessment: strong execution + conservative human estimates + SAFE-NOW isolation, not evidence that tasks are automatically wrong. Confidence in isolated contracts is high, but cross-module integration risk remains medium until convergence.

**Before leaving SAFE-NOW and entering production integration, run one cross-task convergence gate across T-168/T-169/T-170/T-172/T-173/T-174/T-175/T-176/T-178**: identity semantics, artifact lineage, refusal taxonomy, interface compatibility, dependency direction and duplicated authorities. Do not hide this gate inside a random task.

Do not shrink production-integration estimates based on SAFE-NOW speed. DB persistence/canonical integration, C#↔Python production integration, scheduler/registry/fallback cutover and real-site calibration remain materially larger/riskier.

---

# 8. TEST / EVIDENCE LEDGER — ANTI-REPEAT REGISTER

The next session should **not rerun these merely to orient itself**. Rerun only if changed code invalidates the evidence, a later acceptance explicitly requires recertification, or a current symptom contradicts it.

## 8.1 Inherited pre-15-Aug evidence

- 09-Jun RBAC backend 8 passed; frontend persona 5 passed; builds passed.
- T025 runtime: Risk 500 rows ~28s; correlation 8 attempts / 8 refusals / 0 findings; learning jobs 4 / enabled 0; refreshes 200 with ~81.5s and ~67s; 517,602 rows, EXCEPT both ways zero.
- Phenomenon harness: PASS fixture PASS; FAIL fixture FAIL; INSUFFICIENT fixture INSUFFICIENT; NEGCTL fixture FAIL; CONSTANT fixture FAIL.
- T029: 1,167 reconciled; tiny tail accepted.
- T030: populated PASS, unprepared PASS, directional identity PASS after runner correction.
- T031: 10 dimensions PASS; deliberate divergence RED; rollback cleanliness PASS. CI/restore/retirement not fully closed.
- T039: backend unit 4/0; integration 2/0; builds clean.
- T040: targeted progression 138/147/168/173/199/201/209/218; authoring+architecture 291/0 then 304/0; narrow ratchet 240/0 then 253/0; TS clean. Full frontend once 481 pass / 3 fail / 484 total / 206 suites / ~279s; 3 were known JourneyRail baseline.
- T041/T042: early browser 13 pass / 3 fail while incomplete; later targeted PageBuilder + TS + architecture + browser lifecycle PASS.
- T044 prior: 301,560 expected vs 50,000 cap discovered/fixed; 16 widgets, 8 PASS + 8 advisory + 0 FAIL, five-run deterministic; commit `54ee883b`.
- T045 older Pack A: DB/profile PASS; provenance unknown; 9 mismatches; SQL800 replay/convergence; app build/D1 probes green; commit `6f424969`.
- Assistant T071/72/73 latest inherited automated evidence: 22 PASS / 0 FAIL; 5 browser checks were outstanding in prior handover unless later explicitly closed elsewhere.

## 8.2 Current-session Worker-2 evidence

- T-047 Pack A: backend build green; architecture green; Npgsql translation 3/3; frontend targeted 22/22; architecture ratchets 4/4.
- T-047 Pack B: backend build; architecture; Npgsql 3/3; TypeScript; 27 targeted frontend; 4 architecture ratchets — all green; commit `00745822`.
- T-047 C2 scatter: Npgsql 3/3, Parameter Deep Analysis complete; commit `124276dc`.
- T-047 Pack D: Npgsql 4/4 + build/arch/ts/frontend/ratchets green; commit `413df51a`.
- T-045-R1-A: 15/15 anchors; build clean; readiness 7/7; full Analytics.Core green; architecture green; commit `c56008c0`.
- T-045-R1-D: build clean; architecture green; initial PG source comparison 4/4; all existing native-source regressions green; commit `283aae2c`. Later populated recertification proved on 630 downtime events.
- M1 populated-data PG recertification: 17/17 PASS on non-empty `ppiq_presentation` populations; counts listed in §6.6.

## 8.3 Current-session Worker-3 evidence sequence

| Task | Final known suite evidence | Commit(s) |
|---|---|---|
| T-168 | Python 34 + C# protocol 35; full Backend after W3-016 green/no skips | 16ea041a, 94ab4e40, 723b9434, 6aef283f; W3-016 ef588f0c |
| T-169 | 81 Python OK | a887c554 |
| T-171 | 24 Capability Profiler targeted + existing architecture gates | 95cc019c (Pack2) |
| T-177 | 52 targeted: 9 parity + 13 task + 30 P06 | 502f8ca3, 502e0da9, bb4c5686 |
| T-175 | 141 Python OK / 0 skipped | 2413c6e1 |
| T-176 | 141→195→230; final 230 OK / 0 skipped | 08b54b61 + 0a61ccfb |
| T-173 | 297 OK / 0 skipped | 00a14286 |
| T-174 | 340 green | db033796 |
| T-170 | 402 OK / 0 skipped | 5a216483 |
| T-172 | 448 OK / 0 skipped | d4321856 |
| T-178 base | 501 OK / 0 skipped; 512 combinations exhaustively classified | b8792516 |

### Test-design lessons discovered

- A green terminal-state test can be **vacuous** if source population is empty. Certification claims must prove non-vacuity separately.
- Guards matching prose/docstrings instead of executable behavior caused repeated false alarms. Strip comments/docstrings and guard behavior/literals, not English words.
- A test should print what it observed on failure; the first R1-C test lost a run because it failed without source/state counts.
- Build first is valuable: compiler found a missed readiness consumer that grep did not reach.
- Npgsql translation must be proven against a real database for expressions whose translation is uncertain; compile success is insufficient.
- Serialized model bytes are not necessarily deterministic; define the real required invariant (logical identity/numeric reproducibility tolerance).

---

# 9. DEBUGGING / IMPLEMENTATION TIPS THAT SAVED TIME

1. Query the actual current schema/entity/constructor — never infer column names or method arity from memory.
2. Defined DI registration ≠ effective startup graph. Trace the actual root registrations.
3. Defined mapper ≠ mapped route.
4. Count ≠ provenance; job status ≠ provenance; reference population ≠ eligible denominator.
5. Zero finding ≠ no relationship; method gap ≠ data defect.
6. Fix definition/binding/chart/source before regenerating Fleet data. Fleet-v2 is frozen evidence, not a screenshot beautifier.
7. Aggregate before applying raw row caps; time-filter before cap.
8. Use canonical-key deterministic tie-breakers, not display labels.
9. Column roles bind by **name**, never array index.
10. Live DB semantic fix is incomplete until tracked SQL/rebuild authority reproduces it.
11. In PowerShell 5.1, native command output can pollute assignments; route transcripts with `| Out-Host` when reading `$LASTEXITCODE`.
12. PowerShell array/scalar traps: single-row results need explicit array handling; do not index a scalar/string by accident.
13. `Invoke-RestMethod` can hide 4xx body; catch and read response stream when debugging API refusal.
14. Windows download suffix `(1)` can cause `Move-Item` to move the old pack. Version pack filenames or remove old copies deliberately.
15. A pack must reproduce the tested payload byte-for-byte/logically; Worker 3 added falsifiers after here-string/newline and PowerShell 5.1 generator bugs.
16. Do not use `git add .` or `git add -A`; exact-file staging only.
17. If index is non-empty before staging, abort rather than “cleaning” it.
18. A full suite passing does not prove architecture interpretation. Compare to frozen task and falsify the candidate itself.
19. Internal API health + authenticated external reachability are different from public unauthenticated `/health` behavior.
20. `SOURCE PASS != RUNTIME PASS != VISUAL PASS != PRODUCTION CERTIFICATION`.

---

# 10. DEPLOYMENT / SERVER / PIPELINE HANDOVER

## 10.1 This 15-Aug session made no new server/pipeline change

Do not invent a current CI/deploy green claim from the M1/ML work above. The following is inherited historical knowledge from earlier handovers and repository audits.

## 10.2 Historical deployment success

Earlier project evidence recorded Jenkins deployment green, app URL reachable, sysadmin provisioned and enterprise licence activated; a 26-Jun reference had build #96 green. August sessions did not continuously reverify live server. Treat this as **historically working**, not current production certification.

## 10.3 Root fixes that historically made the App URL/deployment work

- Caddy had wrong upstream `plantprocess-app-web`; actual service was `plantprocess-web`. Network alias restored route; permanent config should use canonical service name.
- Compose projects were permanently separated: app=`ppiq-app`, infra=`plantprocessiq`; this prevents `remove-orphans` from reaping proxy/CI containers.
- Canonical DB configuration key is `ConnectionStrings__PlantProcessDb`, not `DefaultConnection`.
- Vite startup positional `localhost 5173` corrected to explicit `--host --port`.
- Smoke credential placeholder that caused 401 loop moved to protected runtime config; secret omitted.
- Signing key moved to minimum/protected runtime handling.
- Server env must be preserved with PG volume; changing password/env without matching volume state produced historical PostgreSQL `28P01` auth failures.
- UTF-8 client/script discipline.
- Caddy long-lived infra owns 80/443; app overlay joins edge network.
- Health-gated deploy tags current images `:previous`; internal health probe retries and rolls back previous images automatically on failure.
- External protected `/health` returning 401 does not prove process is down; use internal health plus external authenticated reachability.
- Jenkins Docker-outside-Docker lesson: build/test containers are siblings; Alpine curl scripts use `sh`; permissions/root can matter for protected env.

## 10.4 Historical green deployment sequence

1. Preserve private runtime env/Caddy before hard checkout/reset; restore afterward.
2. Generate runtime env with host URLs/CORS/runtime config.
3. Sweep stale processes/locks.
4. Blocking backend tests through sibling .NET SDK container because Jenkins host itself has no dotnet.
5. Frontend `npm ci`/tests through sibling Node container.
6. E2E according to current truthful gate.
7. DB order: PostgreSQL up → EF migrations → numbered SQL → presentation seeds/registry.
8. Tag current image previous; build; compose up/remove-orphans.
9. Internal API health retry; rollback if failed.
10. Authenticated presentation smoke/licence entitlement.

## 10.5 Remaining pipeline debt — do not call CI perfect

- `validate-real-ui-gates.cjs` historically orphaned; Jenkinsfile lacked the three npm test commands its guard expected.
- `post-deploy-smoke.sh` referenced a stage 5b that did not exist.
- `STATIC_AUDIT.md` historically carried 5 CRITICAL + 4 HIGH but producing script exited 0, so it was not a real gate.
- Committed Caddy source historically drifted from live targets; do not reconstruct persistent config blindly.
- A phase56 script historically patched `--list` into Jenkins; `--list` can enumerate tests and falsely look green without executing them.
- T-150 remains owner for false-green/swallowed/orphan gates; T-113 prod/demo separation; T-112 RLS; T-031 CI/restore/source retirement; M3 capacity/HA/DR/customer production acceptance.

Frozen pipeline principle: **inventory gate → execute it directly → prove it can fail → repair/retire → only then wire it into CI.** Never wire a collection of unvalidated gates in bulk.

---

# 11. REALISATION SCOREBOARD AT END OF THIS SESSION

These are **engineering assessments**, not formal backlog percentages. They distinguish visible M1 maturity from production integration. The weakest production dimension still controls go-live confidence.

| Dimension | Current assessment | Why / remaining gap |
|---|---:|---|
| Product identity / architecture clarity | ~92% | Layer A/B, genericity, Assistant law, final ML boundaries and hand-offs are now unusually well defined. |
| M1 BI truth / analytical semantics | ~85% | T-046 closed; T-047 Packs A-E; non-vacuous PG recertification; R1-C/B + positional/chemistry/renderer final gaps remain. |
| No-code/Page Builder shell | ~85% | Shared shell, page lifecycle, layout/publish and metadata foundations are proven; post-M1 true Qlik/PowerBI-level genericity still must be audited against actual M1 code. |
| M1 seven-page presentation | ~78-82% | Parameter page complete; truthful Production/Quality/Equipment/Risk surfaces advancing; final blockers explicitly bounded; screenshots/common-language final certification not done. |
| Assistant presentation | ~75% | Strong automated citation/refusal evidence inherited; prior handover still had 5 browser checks outstanding. |
| AI/ML SAFE-NOW foundation | ~85% of isolated foundation | T-168/169/170/171/172/173/174/175/176/177/178-base exist and are heavily falsified. This is **not** production integration. |
| Canonical M2a production data authority | ~25-35% readiness | Target design strong, but ppiq_app final schemas/definitions/relationships/onboarding path remain future M2a work. |
| Production ML/Assistant integration | ~15-25% | Kernels exist; manifests/snapshot materialiser/sequence persistence/model registry/scheduler/canonical frontend cutovers remain T-183..T-193. |
| Infrastructure / CI production certification | ~45% | Historical deployment mechanics are strong but current pipeline truth/capacity/HA/DR not re-certified. |
| Overall production go-live maturity | **floor ≈45%** | Do not average away infrastructure/security/site certification. M1 presentation maturity is much higher than production maturity. |

### How the product improved in this session

- M1 charts moved from “renders rows” toward semantic chart truth: histogram, box plot, scatter, stacked series and exact data-shape compatibility.
- Readiness transport now preserves the evidence it was judged on.
- Equipment stoppage vs production impact became two independent governed quantities instead of a potentially conflated story.
- Presentation PG proofs are now non-vacuous and target the correct populated DB explicitly.
- Risk current truth is measured rather than inferred: all 500 current rows synthetic, contributor arrays present, one scoring period → no fake temporal trend.
- Worker 3 advanced from architecture-only to a substantial isolated ML/runtime foundation with 501-test lane depth, without touching presentation or production DBs.
- A dangerous T-178 semantic fallback ambiguity was found before integration and converted into a bounded corrective.

---

# 12. IMPORTANT FINDINGS / OPEN PROBLEM REGISTER

## Immediate blockers

- **W2:** T-045-R1-C v3 not yet committed; then R1-B.
- **W3:** T-178 W3-023 corrective not yet committed; do not start T-179 first.

## M1 bounded gaps after T-045-R1

- T-044 positional defect materialisation.
- T-044 grade-specification materialisation.
- T-046-R1 Heatmap renderer.
- T-046-R1 Combo/paired renderer.
- T-047 final seven-page binding/screenshots/design-language certification.

## Carried findings

- W3-020 generated pack backup/trx directories need eventual root ignore rule; shared-root hygiene owner later.
- W2-PACK-LE01 pack writer can normalize line endings and create whole-file Git churn; preserve current newline style in future packs.
- Assistant retrieval relevance floor gap: topK can return irrelevant chunks because refusal only when zero chunks; future measured/configurable relevance floor, no guessed threshold.
- Pipeline T-150 false-green/orphan gate debt remains.
- T-031 restore/CI truth/source retirement inherited debt.

---

# 13. WHAT NOT TO RE-INVESTIGATE

Unless new evidence directly contradicts it:

- Do not start/rebuild T-175, T-176, T-173, T-174, T-170, T-172 or T-178 base.
- Do not reopen T-046 wholesale.
- Do not invent new T-047 Packs F/G/H.
- Do not regenerate Fleet-v2 to make charts prettier.
- Do not call zero correlations “no relationship exists”.
- Do not infer model/rule/seed provenance from job status or row count.
- Do not create a Risk temporal trend from the one-day / ~27-second current population.
- Do not change the global integration DB resolver for M1 certification; override target only for the specific gate.
- Do not treat `ppiq_presentation` as final production data authority. It is the M1 presentation profile; `ppiq_app` becomes real product path after M1/M2 convergence.
- Do not modify M2a-P1/P2 scope or add P1-G01/P1-G02 before M1 closes and the Generic BI Product Contract is frozen.
- Do not re-troubleshoot old Caddy/Vite root causes if public URLs are currently healthy; verify first.
- Do not call CI fully truthful before T-150/direct gate-failure proofs.
- Do not treat SAFE-NOW ML kernels as customer-data production integration.

---

# 14. NEXT-SESSION EXECUTION PLAYBOOK

## 14.1 If continuing Worker 2

1. Read §6.7 only; do not rerun the 17/17 PG recertification.
2. Confirm shared index/worktree safety.
3. Cut/apply T-045-R1-C v3 with identical reviewed production design; only PG gate target/non-vacuity changes.
4. Build + 19 known-answer kernel tests (from previous failed v2 run), architecture, populated PG Risk proof, Risk/native regressions.
5. Exact-stage/commit R1-C and report hash.
6. Start R1-B correlation bounded certification, then close T-045-R1.
7. Move through T-044 remainder → T-046-R1 → T-047 final. No detours.

## 14.2 If continuing Worker 3

1. **Do not read/start T-175 as current work.** It is history/closed.
2. Confirm index/worktree safety.
3. Apply W3-023 precedence correction exactly as §7.12, no predicate/can_accept redesign.
4. Rerun 512-combination proof + full ML suite; exact-stage corrective files only; commit.
5. Mark W3-023 resolved; T-178 permanently closed.
6. Read authoritative T-179 backlog entry directly and start deterministic Assistant planner.
7. Preserve zero presentation/DB/DI interference.

## 14.3 Historical T-175 knowledge key (only if the new session needs to understand how we got here)

```text
T175-HISTORY-ONLY
2413c6e1
PriorBaseline + LightGBM
sealed T169 artifact
fixture OutcomeDefinition
leakage cutoff enforced
141 tests OK / 0 skipped
no promotion; T176 owned selection/explanation
```

This is a history lookup key, **not an instruction to execute T-175 again**.

---

# 15. SOURCE / EVIDENCE FILE MAP FOR THE NEXT SESSION

Use these as supporting source material if the handover lacks a verbatim implementation detail:

### Latest backlog / design
- `PPIQ_Backlog_v2_10_1_12Aug2026.md/.xlsx` — authoritative task decomposition.
- `PPIQ_AI_ML_LLM_Target_Architecture_Optimisation.md` — frozen optimisation direction.
- `PPIQ_Layer_B_Architecture_Design_Pack*.md` — detailed Layer-B architecture/gates.
- `PPIQ_Chapter3_General_Technical_Function_Description_RevisionNext.md` and Chapter 4 RevisionNext — target product/engine semantics.
- `PPIQ_Final_Synchronisation_Ledger.md` — document/architecture synchronization evidence.

### Previous handovers
- `PPIQ_SESSION_HANDOVER_11Aug2026.md` — deep M1/BI debugging + pipeline historical caveats.
- `PPIQ_SESSION_HANDOVER_12Aug2026(1).md` — prior consolidated handover before current T-175+ progress.
- `PPIQ_Worker1_FULL_HANDOVER_09Aug2026.md` and Worker2 handover for older Assistant/PageBuilder evidence.

### Latest implementation audits
- `00_Master_Index_12Aug2026_164748.txt`
- `01_Backend_Core_12Aug2026_164748.txt`
- `02_Backend_Database_12Aug2026_164748.txt`
- `03_Backend_Tests_12Aug2026_164748.txt`
- `03A_ML_Runtime_12Aug2026_164748.txt`
- `03B_ML_DotNet_Bridge_12Aug2026_164748.txt`
- `04_Frontend_App_12Aug2026_164748.txt`
- `06_Infrastructure_12Aug2026_164748.txt`
- `07_Tools_Validation_Misc_12Aug2026_164748.txt`
- `10_Audit_Signals_12Aug2026_164748.txt`

### Raw current-session evidence
The `Pasted markdown/text(20260815-...)` files contain the exact console transcripts for T-175/T-176/T-173/T-174/T-170/T-172/T-178 and Worker-2 T-047/R1-A/R1-D/PG recertification. Use them only when exact raw output is required; do not replay the tests just to reconstruct the result.

---

# 16. FULL BACKLOG TASK SNAPSHOT — 193 TASKS

The table below reproduces the v2.10.1 task inventory compactly. **Current-session overrides are shown in the “Handover current state” column** because the static backlog file naturally predates some 15-Aug commits.

| Task | Phase | Hrs | Backlog status | Handover current state | Title |
|---|---|---:|---|---|---|
| T-001 | M1 / M1-P1 | 8 | Done | Done | Build the six-beat Design Traceability Matrix |
| T-002 | M1 / M1-P1 | 8 | Done | Done | Audit every presented route and control against the Chapter 3 page inventory |
| T-003 | M1 / M1-P1 | 4 | Done | Done | Lock the presentation profile as a data profile, not a branch |
| T-004 | M1 / M1-P1 | 4 | Done | Done | Create the M1 acceptance checklist and evidence folder |
| T-005 | M1 / M1-P1 | 6 | Done | Done | Rebuild ppiq_presentation into scratch and diff against live |
| T-006 | M1 / M1-P1 | 8 | Done | Done | Convert every diff finding into a seed or migration script |
| T-007 | M1 / M1-P1 | 10 | Done | Done | Presentation Phenomena and Widget Coverage Matrix, |
| T-008 | M1 / M1-P1 | 10 | Done | Done | Presentation Phenomena and Widget Coverage Matrix, part 2: map, classify, close |
| T-009 | M1 / M1-P1 | 6 | Done | Done | Downtime two-quantity contract: final schema and domain slice |
| T-010 | M1 / M1-P1 | 8 | Done | Done | Run the canonical semantic path end to end through the M1 compatibility boundaries |
| T-011 | M1 / M1-P1 | 6 | Done | Done | Establish and fix the architecture test pool reliability |
| T-012 | M1 / M1-P1 | 6 | Done | Done | Canonicalise the JourneyRail to J1 to J15 |
| T-013 | M1 / M1-P1b | 8 | Done | Done | Three-way source reconciliation: KEEP, EXTEND or ADD |
| T-014 | M1 / M1-P1b | 8 | Done | Done | Capture the current source-shaped donor schemas in a committed generator |
| T-015 | M1 / M1-P1b | 8 | Done | Done | Presentation Fleet v2 target specification |
| T-016 | M1 / M1-P1b | 10 | Done | Done | Extend the generator: defect catalogue and chemistry elements |
| T-017 | M1 / M1-P1b | 8 | Done | Done | Extend the generator: grade specification, and shift as BEHAVIOUR |
| T-018 | M1 / M1-P1b | 6 | Done | Done | Extend the generator: downtime two quantities and buffer posture |
| T-019 | M1 / M1-P1b | 6 | Done | Done | Shift and crew operating-practice regimes |
| T-020 | M1 / M1-P1b | 6 | Done | Done | Post-maintenance recovery and campaign-ageing regimes |
| T-021 | M1 / M1-P1b | 6 | Done | Done | Equipment personality and temporal regime changes |
| T-022 | M1 / M1-P1b | 8 | Done | Done | Merge the best existing material into one Fleet v2 truth |
| T-023 | M1 / M1-P1b | 6 | Done | Done | Scale Fleet v2 to the target plant size |
| T-024 | M1 / M1-P1b | 8 | Done | Done | Emit and populate the presentation canonical operational entities |
| T-025 | M1 / M1-P1b | 8 | Done | Done | Compute and populate the presentation analysis entities with the real engines |
| T-026 | M1 / M1-P1b | 6 | Done | Done | Phenomenon test harness: manifest schema and runner |
| T-027 | M1 / M1-P1b | 6 | Done | Done | Populate the manifest and prove every phenomenon |
| T-028 | M1 / M1-P1b | 2 | Done | Done | Verify the confounded correlation and the insufficient-support refusal |
| T-029 | M1 / M1-P1b | 4 | Done | Done | Five-layer realism audit of the emulated plant |
| T-030 | M1 / M1-P2 | 8 | Done | Done | Emit and populate the presentation staging representation, source-shaped |
| T-031 | M1 / M1-P2 | 10 | Done | Done | Certify cross-layer consistency and retire the obsolete donor state |
| T-032 | M1 / M1-P2 | 12 | Done | Done | Shared Authoring Shell, part 1: the shell contract and the four regions |
| T-033 | M1 / M1-P2 | 12 | Done | Done | Shared Authoring Shell, part 2: relational block grammar on the board |
| T-034 | M1 / M1-P2 | 10 | Done | Done | Registry-driven schema, table and attribute tree |
| T-035 | M1 / M1-P2 | 8 | Done | Done | Compiled-SQL pane and debug log with rows and cost |
| T-036 | M1 / M1-P2 | 12 | Done | Done | SQL mode: safe editor, run test, returned columns and the reconstructability rule |
| T-037 | M1 / M1-P2 | 3 | Done | Done | Certify returned-column role mapping inside the S2 shell |
| T-038 | M1 / M1-P2 | 12 | Done | Done | Add Widget and Edit Widget open the shared shell in S2 mode |
| T-039 | M1 / M1-P2 | 12 | Done | Done | Final definition service interface with a compatibility adapter |
| T-040 | M1 / M1-P2 | 8 | Done | DONE / frozen. Golden Gate authoring/RTL/keyboard/error wording; do not reopen without regression. | Authoring states, keyboard path, RTL and error wording |
| T-041 | M1 / M1-P3 | 6 | Done | DONE / frozen. Page Builder create-page/shared-shell contract. | D2 Page Builder, part 1: create a page and reach the shared shell |
| T-042 | M1 / M1-P3 | 6 | Done | DONE / frozen. Arrange/save/reload/publish lifecycle. | D2 Page Builder, part 2: arrange, save layout and publish |
| T-043 | M1 / M1-P3 | 12 | Done | DONE / frozen. D1 workspace final anatomy. | Bring the workspace to the final D1 anatomy |
| T-044 | M1 / M1-P3 | 8 | Done | Backlog DONE, but a bounded corrective remainder is still planned after T-045-R1: positional defect materialisation + grade-spec materialisation only; no broad reopen. | Certify the three operational dashboards and fix their bindings |
| T-045 | M1 / M1-P3 | 6 | In Progress | IN PROGRESS. R1-A closed c56008c0; R1-D closed 283aae2c; populated-data PG recertification GREEN 17/17; R1-C v3 is the immediate Worker-2 task; R1-B correlation is last. | Certify the analysis and model dashboards and choose the six shown |
| T-046 | M1 / M1-P3 | 8 | Not Started | Original T-046 CLOSED at fc146483b1132a89d181d6508f495a5ddeb08765. A bounded T-046-R1 remains later for Heatmap + Combo/paired renderer only, after T-044 corrective remainder. | Register the final chart grammar and implement the presentation subset |
| T-047 | M1 / M1-P3 | 10 | Not Started | IN PROGRESS / final blocked intentionally. Packs A-E accepted; Pack B 00745822, C1 dc0b0e16, C2 124276dc, D 413df51a, E a16b7b31. Final seven-page certification waits on T-045-R1 -> T-044 remainder -> T-046-R1. | Give the seven pages distinct visual grammars from the registered grammar |
| T-048 | M1 / M1-P3 | 4 | Not Started | Not Started | Associative model, part 1: the alternative state and registry-driven fields |
| T-049 | M1 / M1-P3 | 4 | Not Started | Not Started | Certify layout drag, resize, save, reload and responsive behaviour |
| T-050 | M1 / M1-P3 | 6 | Not Started | Not Started | Drill to population, provenance and evidence |
| T-051 | M1 / M1-P3 | 6 | Not Started | Not Started | Widget failure isolation and the seven states |
| T-052 | M1 / M1-P3 | 4 | Not Started | Not Started | Remove the hardcoded parameter default from the API client |
| T-053 | M1 / M1-P4 | 4 | Not Started | Not Started | Reduce the demonstration navigation and add the inventory ratchet |
| T-054 | M1 / M1-P4 | 4 | Not Started | Not Started | J4 Connections: read-only proof and load budget made visible |
| T-055 | M1 / M1-P4 | 6 | Not Started | Not Started | J5 and J6 Dataset registry browse and watermark suggestion |
| T-056 | M1 / M1-P4 | 4 | Not Started | Not Started | J6 Import progress visibility |
| T-057 | M1 / M1-P4 | 10 | Not Started | Not Started | J7 Relationship model vertical slice, part 1: publish one relationship |
| T-058 | M1 / M1-P4 | 10 | Not Started | Not Started | J7 Relationship model vertical slice, part 2: one resolver consumer |
| T-059 | M1 / M1-P4 | 4 | Not Started | Not Started | Associative model, part 2: cross-source state through the published relationship |
| T-060 | M1 / M1-P4 | 4 | Not Started | Not Started | C6 Relationship Browser, minimal read-only slice |
| T-061 | M1 / M1-P4 | 6 | Not Started | Not Started | C2 Mapping Health, part 1: the typed issue contract and the reprocess API |
| T-062 | M1 / M1-P4 | 4 | Not Started | Not Started | C2 Mapping Health, part 2: the final visible page |
| T-063 | M1 / M1-P4 | 10 | Not Started | Not Started | C5 Genealogy: converge the legacy workbench onto the final two-state surface |
| T-064 | M1 / M1-P4 | 8 | Not Started | Not Started | Add job_definitions.target_definition_id and the JB error codes |
| T-065 | M1 / M1-P4 | 12 | Not Started | Not Started | J12 Analysis authoring: converge onto D3 Analysis Toolbox in S3 mode |
| T-066 | M1 / M1-P4 | 6 | Not Started | Not Started | One visible readiness authority on Home and Analysis |
| T-067 | M1 / M1-P4 | 8 | Not Started | Not Started | Findings evidence panel, registry-driven throughout |
| T-068 | M1 / M1-P4 | 6 | Not Started | Not Started | Retire the hardcoded outcome and grain arrays |
| T-069 | M1 / M1-P5 | 8 | Done | Done | Website, part 1: the five-product information architecture |
| T-070 | M1 / M1-P5 | 6 | Done | Done | Website, part 2: polish the presentation routes |
| T-071 | M1 / M1-P5 | 8 | Done | Done | Build the G1 persistent assistant dock |
| T-072 | M1 / M1-P5 | 8 | Done | Done | Page and widget context envelope |
| T-073 | M1 / M1-P5 | 8 | Done | Done | Add the page and widget chunk family to the retrieval corpus |
| T-074 | M1 / M1-P5 | 4 | Done | Done | Registry-typed quantity guard on assistant answers |
| T-075 | M1 / M1-P5 | 4 | Done | Done | Citation chips, evidence strip and suggested questions |
| T-076 | M1 / M1-P5 | 4 | Not Started | Not Started | Certified question pack and offline fallback |
| T-077 | M1 / M1-P5 | 6 | Not Started | Not Started | One Playwright journey covering all six beats |
| T-078 | M1 / M1-P5 | 4 | Not Started | Not Started | Execute visual regression and accessibility on the presented routes |
| T-079 | M1 / M1-P5 | 3 | Not Started | Not Started | Failure injection suite |
| T-080 | M1 / M1-P5 | 4 | Not Started | Not Started | Capture the Customer Contract Continuity snapshots |
| T-081 | M1 / M1-P5 | 6 | Not Started | Not Started | Write the screen-by-screen demonstration script |
| T-082 | M1 / M1-P5 | 4 | Not Started | Not Started | Presentation environment preparation and clean-start verification |
| T-083 | M1 / M1-P5 | 6 | Not Started | Not Started | Three rehearsals, hostile hands and the fallback package |
| T-084 | M2a / M2a-P1 | 10 | Not Started | Not Started | Emit the frozen Fleet v2 into native customer-source fixtures |
| T-085 | M2a / M2a-P1 | 10 | Not Started | Not Started | Clean-room rebuild of the Fleet v2 emulator sources from source control |
| T-086 | M2a / M2a-P1 | 8 | Not Started | Not Started | Freeze and certify Fleet v2 as the M2 reference validation dataset |
| T-087 | M2a / M2a-P1 | 12 | Not Started | Not Started | Physical three-schema migration |
| T-088 | M2a / M2a-P1 | 12 | Not Started | Not Started | Canonical migration order and legacy script archival |
| T-089 | M2a / M2a-P1 | 12 | Not Started | Not Started | definition_store, definition_versions and definition_dependencies |
| T-090 | M2a / M2a-P1 | 12 | Not Started | Not Started | Move all five definition kinds onto the store |
| T-091 | M2a / M2a-P1 | 12 | Not Started | Not Started | Impact preview, export and import |
| T-092 | M2a / M2a-P1 | 12 | Not Started | Not Started | Registry authority: dimensions and measures as rows |
| T-093 | M2a / M2a-P1 | 6 | Not Started | Not Started | Plant-vocabulary sweep, part 1: build the term list and the architecture test |
| T-094 | M2a / M2a-P1 | 8 | Not Started | Not Started | Plant-vocabulary sweep, part 2: clear the violations and rename the canonical grain |
| T-095 | M2a / M2a-P2 | 12 | Not Started | Not Started | Relationship members, cardinality, grain conversion and preferred paths |
| T-096 | M2a / M2a-P2 | 8 | Not Started | Not Started | Path resolver, part 1: resolver core and the first eight consumers |
| T-097 | M2a / M2a-P2 | 6 | Not Started | Not Started | Path resolver, part 2: the remaining eight consumers and the regression suite |
| T-098 | M2a / M2a-P2 | 12 | Not Started | Not Started | Relationship Browser page and path evidence |
| T-099 | M2a / M2a-P2 | 8 | Not Started | Not Started | Quarantine, part 1: the table, the reprocess API and the first eight PV classes |
| T-100 | M2a / M2a-P2 | 6 | Not Started | Not Started | Quarantine, part 2: the remaining seven PV classes and per-class tests |
| T-101 | M2a / M2a-P2 | 12 | Not Started | Not Started | Quarantine retry, reprocess and Mapping Health completion |
| T-102 | M2a / M2a-P2 | 8 | Not Started | Not Started | Identity resolution across sources |
| T-103 | M2a / M2a-P2 | 6 | Not Started | Not Started | Genealogy bidirectional walk hardening and weight proof |
| T-104 | M2a / M2a-P2 | 8 | Not Started | Not Started | Projection through the versioned mapping, with version stamping |
| T-105 | M2a / M2a-P2 | 6 | Not Started | Not Started | Idempotent reprojection and mapping-version regression |
| T-106 | M2a / M2a-P3 | 12 | Not Started | Not Started | Job target version policy and dependency DAG |
| T-107 | M2a / M2a-P3 | 12 | Not Started | Not Started | Dual-predicate admission control and the three ML execution lanes |
| T-108 | M2a / M2a-P3 | 8 | Not Started | Not Started | stage_watermarks and delta-scoped projection |
| T-109 | M2a / M2a-P3 | 6 | Not Started | Not Started | Delta-scoped feature refresh and analysis, with telemetry and ML hand-off |
| T-110 | M2a / M2a-P3 | 12 | Not Started | Not Started | Chunk manifests, checkpoint, resume and deterministic merge |
| T-111 | M2a / M2a-P3 | 12 | Not Started | Not Started | Scan budget and the Scan Amplification metric |
| T-112 | M2a / M2a-P3 | 12 | Not Started | Not Started | Force RLS on every tenant-owned table with an architecture test |
| T-113 | M2a / M2a-P3 | 8 | Not Started | Not Started | Secret and configuration hygiene |
| T-114 | M2a / M2a-P3 | 6 | Not Started | Not Started | Tenant keys, tenant-aware uniqueness and canonical namespace on new APIs |
| T-115 | M2a / M2a-P3 | 4 | Not Started | Not Started | Fresh-install Rule 2 acceptance test, ephemeral |
| T-116 | M2a / M2a-P3 | 8 | Not Started | Not Started | API namespace migration, part 1: map the 92 prefixes onto the 27 domains and stand up dual-serve |
| T-117 | M2a / M2a-P3 | 6 | Not Started | Not Started | API namespace migration, part 2: migrate the clients and add the token gate |
| T-118 | M2a / M2a-P4 | 12 | Not Started | Not Started | J1 to J3 commissioning built for real |
| T-119 | M2a / M2a-P4 | 12 | Not Started | Not Started | Eight-role catalogue with three enforcement layers |
| T-120 | M2a / M2a-P4 | 12 | Not Started | Not Started | Users and Roles administration surface |
| T-121 | M2a / M2a-P4 | 12 | Not Started | Not Started | Licence and entitlement enforcement |
| T-122 | M2a / M2a-P4 | 12 | Not Started | Not Started | Container architecture and configuration profiles, including isolated ML runtimes |
| T-123 | M2a / M2a-P4 | 12 | Not Started | Not Started | Install package, migration runner, upgrade and rollback |
| T-124 | M2a / M2a-P4 | 12 | Not Started | Not Started | Backup with a tested restore acceptance procedure |
| T-125 | M2a / M2a-P4 | 12 | Not Started | Not Started | Minimum monitoring, health and alerting |
| T-126 | M2a / M2a-P4 | 12 | Not Started | Not Started | Support runbook and UAT dataset and configuration import |
| T-127 | M2a / M2a-P4 | 12 | Not Started | Not Started | Canonical journey regression and the Continuity comparison |
| T-168 | M2b / M2b-P0A | 12 | Not Started | CLOSED. Python runtime 16ea041a; C# protocol 94ab4e40; real C#→Python→C# 723b9434; closure evidence 6aef283f. W3-016 CI dependency correction ef588f0c. Full Backend regression after dependency defect: green/no skips. | Versioned C#↔Python ML job protocol and isolated runtime harness |
| T-169 | M2b / M2b-P0A | 10 | Not Started | CLOSED a887c554. Typed Parquet/Arrow artifacts; logical hash format-independent, byte hash physical; B-03 hook; 81 Python tests green. | Typed columnar training-artifact library and B-03 harness |
| T-170 | M2b / M2b-P0A | 10 | Not Started | CLOSED 5a216483. Chunked immutable sequence artifacts, bounded/mmap loader, per-chunk + payload hashes, B-04; 402 Python tests green. | Chunked sequence-artifact library and bounded loader |
| T-171 | M2b / M2b-P0A | 10 | Not Started | CLOSED. Commit guard Pack 1 completed; Capability Profiler Pack 2 at 95cc019c; 24 targeted tests. Available/Degraded/Unavailable; no DB access. | Capability Profiler and eligibility/refusal kernel |
| T-172 | M2b / M2b-P0A | 12 | Not Started | CLOSED d4321856. Real PyTorch 2.13 temporal-convolution ProcessEncoder, channel-set identity, reproducibility tolerance 1e-5, B-05; 448 Python tests green. | MF-01 Process Encoder runtime behind a replaceable contract |
| T-173 | M2b / M2b-P0A | 12 | Not Started | CLOSED 00a14286. VectorSimilarityIndex, ExactFlat oracle + approximate candidate, recall falsification, second unrelated population; 297 Python tests green. | MF-02 VectorSimilarityIndex with exact-Flat recall baseline |
| T-174 | M2b / M2b-P0A | 10 | Not Started | CLOSED db033796. Robust-deviation baseline + neighbour-density candidate using T-173; refusal semantics; 340 Python tests green. | MF-03 novelty-model runtime and honest refusal semantics |
| T-182 | M2b / M2b-P0A | 10 | Not Started | NOT STARTED. Common B-01..B-09 benchmark harness; measurement machinery only. | Benchmark harness and result manifest for B-01..B-09 |
| T-137 | M2b / M2b-P0B | 10 | Not Started | Not Started | ModelServingRuntime and model-gateway adapter, isolated from the presentation dock |
| T-175 | M2b / M2b-P0B | 12 | Not Started | CLOSED 2413c6e1. MF-04 supervised runtime: mandatory PriorBaseline + LightGBM candidate, leakage gate, binary/multiclass/ordinal/continuous, shared holdout. 141 Python tests green. DO NOT START THIS TASK AGAIN. | MF-04 supervised-outcome training runtime and mandatory simple baseline |
| T-176 | M2b / M2b-P0B | 12 | Not Started | CLOSED 08b54b61 + 0a61ccfb. Three-dimensional promotion kernel + real LightGBM TreeSHAP provider; exact raw-output reconstruction; 230 Python tests green. | Calibration, explanation stability and three-dimensional promotion kernel |
| T-177 | M2b / M2b-P0B | 10 | Not Started | CLOSED. 52 targeted tests (Parity 9/9, T-177 13/13, P06 30/30); commits 502f8ca3, 502e0da9, bb4c5686. Statistical kernel with Numeric×Categorical and parity/source trace. | Production statistical-method kernel, including Numeric×Categorical |
| T-178 | M2b / M2b-P0B | 8 | Not Started | BASE IMPLEMENTATION CLOSED b8792516 with 501 Python tests, BUT W3-023 corrective is still PENDING at this handover. Correct precedence totalisation must be committed before T-179. | Pure remediation eligibility and can_accept decision kernel |
| T-179 | M2b / M2b-P0B | 10 | Not Started | NOT STARTED. Next W3 task only AFTER T-178 W3-023 corrective commit. Deterministic Assistant tool planner; no LLM tool selection. | Deterministic Assistant tool planner |
| T-180 | M2b / M2b-P0B | 12 | Not Started | NOT STARTED. Permission-first hybrid retrieval/evidence packer; later after T-179. | Permission-first hybrid retrieval and evidence packer |
| T-181 | M2b / M2b-P0B | 12 | Not Started | NOT STARTED. Deterministic answer verifier + Q-01..Q-11 harness. | Deterministic answer verifier and Q-01..Q-11 evaluation harness |
| T-128 | M2b / M2b-P1 | 10 | Not Started | Not Started | Feature store, outcome store and immutable snapshot metadata |
| T-129 | M2b / M2b-P1 | 12 | Not Started | Not Started | Compute runs, findings and common evidence persistence |
| T-130 | M2b / M2b-P1 | 12 | Not Started | Not Started | Model registry, serving identity, activation and fallback |
| T-131 | M2b / M2b-P1 | 12 | Not Started | Not Started | Practice signature, windowing, context and cohorts |
| T-132 | M2b / M2b-P1 | 12 | Not Started | Not Started | Support, confidence, back-off ladder and tolerance sensitivity |
| T-135 | M2b / M2b-P1 | 10 | Not Started | Not Started | Tenant-aware uniqueness across the intelligence tables |
| T-183 | M2b / M2b-P1 | 12 | Not Started | Not Started | Semantic Contract Manifest persistence, resolver and G-55 coverage |
| T-184 | M2b / M2b-P1 | 12 | Not Started | Not Started | Snapshot Materialiser: seal feature state into typed artifacts and enforce G-48 |
| T-133 | M2b / M2b-P2 | 12 | Not Started | Not Started | Practice persistence, drift and canonical D10 Practice Insights |
| T-134 | M2b / M2b-P2 | 12 | Not Started | Not Started | Bindable intelligence registry and evidence handles |
| T-136 | M2b / M2b-P2 | 10 | Not Started | Not Started | Incremental practice recomputation |
| T-185 | M2b / M2b-P2 | 12 | Not Started | Not Started | sequence_manifests persistence and object-storage sequence path |
| T-186 | M2b / M2b-P2 | 12 | Not Started | Not Started | Persist capability profiles, prediction points and the model-count governor |
| T-187 | M2b / M2b-P2 | 12 | Not Started | Not Started | Production training/index integration: snapshots → ML lanes → registry activation |
| T-188 | M2b / M2b-P2 | 10 | Not Started | Not Started | Canonical D4 Findings and D8 ML Readiness/Models cutover |
| T-139 | M2b / M2b-P3 | 12 | Not Started | Not Started | prediction_runs, predictions and prediction_current |
| T-140 | M2b / M2b-P3 | 12 | Not Started | Not Started | Prediction drivers and comparables, persisted |
| T-141 | M2b / M2b-P3 | 12 | Not Started | Not Started | Actionable deadline and latency health |
| T-142 | M2b / M2b-P3 | 8 | Not Started | Not Started | Remediation candidate generation from the customer's own history |
| T-143 | M2b / M2b-P3 | 8 | Not Started | Not Started | Integrate the nine-check remediation gate, can_accept and suppression |
| T-144 | M2b / M2b-P3 | 8 | Not Started | Not Started | Accept, Reject and Defer with action recording |
| T-145 | M2b / M2b-P3 | 8 | Not Started | Not Started | Outcome capture, evaluation and escalation |
| T-189 | M2b / M2b-P3 | 12 | Not Started | Not Started | Canonical D5 Risk and D9 Early Warning cutover |
| T-190 | M2b / M2b-P3 | 12 | Not Started | Not Started | Canonical D6 Suggestions and decision-action cutover |
| T-191 | M2b / M2b-P3 | 12 | Not Started | Not Started | D11 Scenario Simulation: governed read-only modelled comparison |
| T-138 | M2b / M2b-P4 | 12 | Not Started | Not Started | Canonical Assistant runtime cutover: planner, retrieval, evidence packing, serving and verifier |
| T-146 | M2b / M2b-P4 | 10 | Not Started | Not Started | Converge the production statistical engine onto the certified method kernel |
| T-147 | M2b / M2b-P4 | 12 | Not Started | Not Started | Fix the outcome namespace, grain assignment and ordinal loader |
| T-148 | M2b / M2b-P4 | 8 | Not Started | Not Started | Map the 108 page files onto the 40 target pages |
| T-149 | M2b / M2b-P4 | 6 | Not Started | Not Started | Delete the legacy redirects and re-verify continuity |
| T-150 | M2b / M2b-P4 | 12 | Not Started | Not Started | Complete the test gates |
| T-192 | M2b / M2b-P4 | 10 | Not Started | Not Started | Target-architecture gate pack G-48..G-55 |
| T-193 | M2b / M2b-P4 | 10 | Not Started | Not Started | Drift Supervisor and governed model-action proposals |
| T-151 | M3 / M3-P1 | 12 | Not Started | Not Started | Site defect burn-down |
| T-152 | M3 / M3-P1 | 12 | Not Started | Not Started | Customer data edge cases |
| T-153 | M3 / M3-P1 | 12 | Not Started | Not Started | Connector certification against real sources |
| T-154 | M3 / M3-P1 | 12 | Not Started | Not Started | Query plans, indexes and partition boundaries |
| T-155 | M3 / M3-P1 | 12 | Not Started | Not Started | B-01/B-02 capacity tuning, scan amplification and model-serving memory |
| T-156 | M3 / M3-P1 | 12 | Not Started | Not Started | Customer definitions built through the product |
| T-157 | M3 / M3-P1 | 12 | Not Started | Not Started | Practice, prediction and model calibration on real data |
| T-158 | M3 / M3-P1 | 12 | Not Started | Not Started | Remediation validation against real process constraints |
| T-159 | M3 / M3-P2 | 12 | Not Started | Not Started | C1 to C4 capacity certification |
| T-160 | M3 / M3-P2 | 12 | Not Started | Not Started | HA, DR and restore rehearsal |
| T-161 | M3 / M3-P2 | 12 | Not Started | Not Started | SSO and identity integration |
| T-162 | M3 / M3-P2 | 12 | Not Started | Not Started | Site security hardening and sign-off |
| T-163 | M3 / M3-P2 | 12 | Not Started | Not Started | Monitoring, SLOs and support escalation |
| T-164 | M3 / M3-P2 | 12 | Not Started | Not Started | The Value Engine |
| T-165 | M3 / M3-P2 | 12 | Not Started | Not Started | Commercial capacity finalisation and the sales calculator |
| T-166 | M3 / M3-P2 | 12 | Not Started | Not Started | Five-product website production completion |
| T-167 | M3 / M3-P2 | 12 | Not Started | Not Started | Documentation, training and production acceptance |

---

# 17. FINAL ONE-PARAGRAPH HANDOFF

PlantProcess IQ ends this session with M1 presentation truth substantially stronger and Worker-3 isolated AI/ML foundations far beyond the 12-Aug checkpoint. The immediate job is **not** T-175: T-175 is closed at `2413c6e1`. Worker 2 must finish T-045-R1-C v3, then R1-B, then the bounded T-044/T-046 corrections and T-047 final visual certification. Worker 3 must finish the **one-table T-178 W3-023 precedence correction** before starting T-179. The populated M1 PostgreSQL recertification is already 17/17 green on meaningful `ppiq_presentation` populations and should not be repeated merely for orientation. After M1 closes, create the stable `Presentation` branch and continue `main` as the real generic product; only then reopen the saved Generic BI/Qlik-PowerBI-level authoring discussion and decide whether M2a-P1/P2 need scope changes or new tasks. Preserve evidence hierarchy, exact-file Git isolation, honest refusal, generic cross-industry semantics and the distinction between isolated kernel completion and production integration.

**Resume key:** `RESUME-PPIQ-15AUG-CURRENT-STATE`
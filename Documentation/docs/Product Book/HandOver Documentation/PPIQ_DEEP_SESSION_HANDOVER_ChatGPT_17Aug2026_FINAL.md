# PlantProcess IQ — Deep Technical / Product / Programme Session Handover
## 17-Aug-2026, 14:25 +03:00 — FINAL continuation handover for the next ChatGPT session

**Repository:** `C:\Workspace\PlantProcess-IQ`  
**Latest shared committed HEAD proven in this session:** `964608045942527c281ae32a05484d64ffaf8103`  
**Latest authoritative backlog:** `PPIQ_Backlog_v2_10_4_16Aug2026_Three_AI_Agent_Orchestration.xlsx`  
**Static backlog size:** **200 tasks / 1806 h**  
**Static backlog statuses:** **71 Done / 1 In Progress / 128 Not Started**  
**Local main DB:** native PostgreSQL 16, `127.0.0.1:5432`  
**Presentation DB:** `ppiq_presentation`  
**Development/default integration DB:** `ppiq_app`  
**API:** `http://localhost:5063`  
**Frontend:** `http://localhost:5173`

> **PURPOSE.** This file is intentionally much deeper than a normal session summary. It preserves the previous deep handover, the 17-Aug implementation delta, exact commits, runtime/database discoveries, worker orchestration, anti-repeat test ledger, Identity/Topology, roadmap/backlog, deployment/server/pipeline history, and the exact next execution point. The next session should use this document to **continue**, not re-investigate already proven facts.

> **SECURITY.** Do not copy passwords, signing keys, tokens or private credentials into chat, scripts or handovers. Load them from the committed profile mechanism or protected runtime/server secret store. This handover records names/ports/topology but deliberately omits secret literals.

---

# 0. ABSOLUTE RESUME POINT — READ THIS FIRST

## 0.1 Resume key

Paste this into the first message of the next session if context looks stale:

```text
RESUME-PPIQ-17AUG-1425
HEAD=964608045942527c281ae32a05484d64ffaf8103

W1=T057-RUNTIME-CERT-THEN-COMMIT
W2=T050-STEP2B-IMPLEMENT-NOW
W3=T064-VERIFY-FAILED-V3-DB-RESIDUE-THEN-FIX-REPLAY-GATE

T049=CLOSED-cc4d8844
T051=CLOSED-6c99e091
PR05001=CLOSED-96460804
T064-PARITY-CORRECTIVE=CLOSED-b811696b

SQL824=IMMUTABLE
SQL825=IMMUTABLE
SQL826=RESERVED-W3-T064-TARGET-PARAMETERS
SQL827=W1-T057-RELATIONSHIP-COMPATIBILITY-ON-DISK-AND-APPLIED

DO-NOT-RERUN-PROVEN-GATES-FOR-ORIENTATION
DO-NOT-CLEAN-OTHER-WORKERS-WORKTREE
DO-NOT-REOPEN-FROZEN-CONTRACTS-WITHOUT-CONTRADICTION
```

## 0.2 Exact last point reached

### Worker 1 — T-057
**State:** implementation is on disk, **UNCOMMITTED**.

Already proven:
- relationship contracts/service/store/endpoints created;
- `827_t057_relationship_compatibility_persistence.sql` created;
- `Program.cs` wired;
- self-check green;
- solution build green;
- scoped warning gate green;
- Application test gate **665/665** green in that run;
- SQL 827 applied cleanly to `ppiq_presentation`;
- SQL 827 replayed cleanly.

Still missing:
- **runtime certification of the publication/read-back cycle**;
- exact staging;
- commit;
- T-057 closure;
- then start T-058.

Do **not** redesign T-057. Do **not** add a public `POST /api/relationships`. Relationship records are emitted from definition/transformation publication, not authored as a separate public resource.

### Worker 2 — T-050
**State:** presentation half is committed; provenance half Step 1 + Step 2a are applied and **UNCOMMITTED**; Step 2b has not started.

Already proven:
- presentation half commit `d6870d0aac4719918864dd44798939a4980707b3`;
- authoritative source-row identity preserved through chart transformations;
- `drilldownRowIdentity` **8/8**;
- dashboard regression **39/39**;
- T-051 Playwright regression **4/4**;
- PR-050 frontend contract mirror + evidence helper;
- Step 1 + 2a focused tests **15/15**;
- TypeScript gate green.

Next:
- **start code immediately** for Step 2b:
  - drawer population from backend `rowPopulations`;
  - preserve the exact request snapshot associated with the render;
  - exactly one opt-in evidence re-execution;
  - complete `executionIdentity`;
  - resolve via existing T-073 `assistantApi.getWidgetResultEvidence(handle.id)`;
  - distinguish producer-unavailable vs resolver-null/404 vs transport failure;
  - stale-context race test A→B;
  - final unit/Playwright/build;
  - exact-stage;
  - one coherent T-050 provenance commit;
  - close T-050;
  - start T-052.

### Worker 3 — T-064 target_parameters
**State:** core and EF/runtime parity are committed; target_parameters completion is **OPEN**. Latest v3 applied/falsified correctly, then auto-reverted source artifacts at Step 11.

Already proven in v3:
- malformed-JSON half-write defect was real;
- correct fix is validation **before the first target assignment** in both `JobDefinition.AssignTargetDefinition` and `JobRunHistory.RecordResolvedTarget`;
- target persistence round-trip:
  - `PARAMS 7`;
  - `NULL PRESERVED`;
  - `EMPTY DISTINCT`;
  - `ORPHAN ENFORCED`;
- build clean;
- targeted tests **36/36**, 0 skip;
- post-fix Application regression **665/665**;
- Domain **11/12**, one carried `F-DEFVER-01`;
- Architecture **181/181**;
- migration `T064TargetParametersParity` contained **only** `target_parameters` Add/Drop on `job_definitions` and `job_run_histories`;
- `has-pending-model-changes = ZERO`;
- fresh EF migration chain green.

Latest failure:
- replaying immutable T-064 SQL scripts added the two raw-SQL operational indexes:
  - `ix_job_definitions_target_definition`
  - `ix_job_run_histories_target_definition`
- the pack incorrectly required replay to produce zero index delta relative to pure EF shape.
- This is a **gate-definition defect**, not a target_parameters product contradiction.

**CRITICAL before rerun:** v3 applied 826 to live `ppiq_presentation` before the later failure, but its printed auto-revert did not prove that those live physical DB changes were rolled back. Therefore the next session must **inspect live DB residue first**. If target_parameters columns exist while 826/migration/history are absent and values are all NULL, restore only the failed-v3 residue. If any non-null values or migration-history row exist, STOP and report rather than deleting.

Then correct Step 11:
- 824 may add exactly its own declared raw-SQL operational indexes;
- 824 must not mutate the EF-owned target columns/types unexpectedly;
- 825 must be managed-schema no-op on already-correct fresh EF;
- 826 must be no-op after the EF target_parameters migration;
- final **fresh EF + 824 + 825 + 826** must exactly match `ppiq_presentation` for governed T-064 columns + indexes in both directions.

Then history reconciliation → EF update no-op → runtime `/api/version` 200 → exact-stage → commit → **T-064 CLOSED**.

## 0.3 Preferred serialization of runtime/DB gates

Workers may code in parallel, but `ppiq_presentation` and port 5063 are shared state. Do not let multiple workers run DB/runtime certification blindly at the same time.

Recommended order:

```text
W2 code/unit work in parallel (frontend-only)
        |
W1 T-057 runtime publish/read-back
→ exact-stage → commit → CLOSE T-057
        |
W3 verify/repair failed-v3 DB residue
→ corrected T-064 final pack
→ runtime → commit → CLOSE T-064
        |
W2 final Playwright against stable API
→ commit → CLOSE T-050 → START T-052
```

Before starting API:
```powershell
Get-NetTCPConnection -LocalPort 5063 -State Listen -ErrorAction SilentlyContinue
```

If a process already owns 5063, identify it before killing or reusing it. A stale API answered from an old DLL earlier in this session and caused misleading runtime observations.

---

# 1. AUTHORITY HIERARCHY AND CENTRAL TECH-LEAD LAW

Use this evidence hierarchy:

```text
executed runtime / database / browser proof
> executed tests
> current source trace
> exact commit/closure evidence
> handover notes
> backlog/design intent
```

Planned text is not implementation evidence.

## 1.1 User's central management doctrine — frozen

1. **COMPLETE ≠ BIGGER.** Complete = written scope + truth prerequisites + acceptance evidence.
2. Every finding is classified:
   - current mandatory scope;
   - smallest prerequisite;
   - existing future owner;
   - parking-lot/refactor.
3. No bare “later”: defer with reason + owner task + trigger.
4. Investigation starts at authority:
   `producer → persistence → consumer → first missing/refusing layer`.
5. Ask one executable question and stop once answered.
6. Green → exact-stage → commit → close → move.
7. Never weaken acceptance to close.
8. Never reopen frozen decisions absent concrete contradiction.
9. Full suites are phase/gate evidence, not ritual after every edit.
10. Parallel workers own exact files/subsystems; file lock beats frontend/backend labels.
11. One product task = one accountable owner until closure.
12. Never consume another agent's **uncommitted** work as a dependency.
13. No `git clean -fd`, `git reset --hard`, `git restore .`, `git add .`, `git add -A`.
14. Fix pack/tooling defects as tooling defects; do not turn them into product scope.
15. STOP only for:
    - missing prerequisite/producer;
    - ownership/file-lock collision;
    - live source contradicts frozen public contract;
    - required change outside owned subsystem / accepted semantics.
16. Session fatigue/confidence is not an engineering STOP. Opening a fresh session is fine, but the fresh session resumes at implementation, not redesign.
17. Prefer **one bounded pack per remaining task**, not endless A/B/C consultation loops.
18. Tooling guards should assert positive facts in their owned region; broad negative scans are fragile and often self-match.
19. Once design is frozen: bounded source verification → implementation → tests → commit.
20. Exact-file staging is mandatory in the shared worktree.

## 1.2 Task lock template

```text
TASK LOCK
Current task:
Accepted base commit:
Allowed paths/subsystem:
Forbidden/reopened tasks:
Dependency gate:
```

Close with:

```text
CLOSE LOCK
Task:
Commit:
Acceptance:
Next task:
```

---

# 2. PRODUCT IDENTITY AND NON-NEGOTIABLE PRODUCT LAW

PlantProcess IQ is a **generic, cross-industry manufacturing BI + deterministic analytics + governed intelligence platform**.

Fleet-v2 / steel is reference/demo data, not product identity.

The same product must onboard steel, aluminium, tyres, food, bottling, paper, pharma, cement, chemicals/refining and future industries through:
- data;
- metadata;
- mapping;
- relationship model;
- registry;
- authoring;

not by customer-specific React/C#/SQL branches.

## 2.1 Truth layers

**Layer A — exact BI/facts**
- counts;
- sums;
- deterministic grouped/filtered values;
- exact business facts.

**Layer B — learned/statistical**
- correlation;
- similarity;
- novelty;
- risk;
- prediction;
- contribution;
- practice/remediation evidence.

Never use ML to approximate an exact Layer-A fact.

## 2.2 Assistant law

```text
engines calculate
→ governed evidence/results
→ permission/tenant-scoped tool/retrieval
→ LLM explains/qualifies/cites
→ deterministic verification
```

LLM must not:
- choose arbitrary hidden tools;
- invent plant figures;
- erase engine refusal;
- upgrade association to causality;
- replace readiness/data authority.

## 2.3 Human/read-only law

- Customer source systems are read-only for PPIQ.
- No autonomous control/writeback.
- Recommendations/remediation are evidence-backed candidates for **human decision**.
- Accept/Reject/Defer is governed later by server authority.

## 2.4 Generic BI target — saved for after M1

After M1, freeze the Generic BI Product Contract. Target authoring flexibility is comparable in spirit to Qlik Sense / Power BI:
- pages/sheets;
- arbitrary registered widgets;
- dimensions/measures/calculated expressions;
- filters;
- hierarchies;
- relationships;
- sorting/grouping/formatting;
- drill;
- selections;
- bookmarks/saved views;
- customer content = metadata/configuration, not product code.

Code-owned governance remains:
- security/RLS;
- expression grammar;
- query limits;
- chart-renderer implementations;
- relationship/cardinality rules;
- permissions;
- ML authority;
- read-only controls.

Do **not** create new M2 tasks for this until M1 is closed and final M1 code is compared to the frozen Generic BI contract.

---

# 3. TWO-TRACK PRODUCT / RELEASE CONCEPT — FROZEN

## 3.1 M1 Presentation track

Purpose: truthful, polished enterprise presentation on `ppiq_presentation`.

Allowed:
- presentation/staging/canonical operational data may be prepared/materialised from the governed reference plant;
- visible contracts must be final enough to survive M2;
- compatibility persistence may exist behind final external/service contracts;
- analytical outputs must come from real engines, not authored fake result rows.

Presentation release should ultimately be frozen as immutable release/tag/profile/database evidence, not a divergent forever-branch.

## 3.2 M2+ real customer track

Fresh customer install:
- metadata/config seeded appropriately;
- staging starts empty;
- plant operational/analysis stores start empty;
- acquisition/import/jobs/engines populate them;
- generic metadata-driven behavior;
- server-enforced RBAC/licensing;
- signed/offline-capable licence;
- downgrade restricts capability, never deletes data;
- performance claims require measured certification;
- ML/AI called intelligence platform components, not “18 models”;
- Assistant answers supported evidence or refuses.

### Milestones
- **M2a:** customer pilot RC; canonical data/definition/relationship/job/security/deployment foundation.
- **M2b:** full production intelligence/prediction/remediation integration.
- **M3:** second-site/real-data stabilisation, HA/DR/capacity/production certification and commercial completion.

---

# 4. IDENTITY & TOPOLOGY

## 4.1 Local laptop

| Item | Current authority |
|---|---|
| Repo | `C:\Workspace\PlantProcess-IQ` |
| Main PostgreSQL | Native Windows PostgreSQL 16, `127.0.0.1:5432`; **not Docker** |
| Dev DB | `ppiq_app` |
| Presentation DB | `ppiq_presentation` |
| API | `http://localhost:5063` |
| Frontend | `http://localhost:5173` |
| Marketing website historical local | `5080` |
| Backend | .NET 9 |
| Frontend | React/Vite, Vitest 4.1.6 |
| Python ML | Separate `ML/` project; no product `.py` under Backend/tools |

**Never invent a connection string.** Load `ConnectionStrings__PlantProcessDb` from `env\profiles\local.env`, `presentation.env`, etc.

Important certification law:
```text
generic integration correctness
→ ppiq_app default is fine

M1 populated presentation proof
→ explicitly point the relevant test/runtime gate to ppiq_presentation
```

Do not globally rewrite the integration-test resolver to make presentation certification convenient.

## 4.2 Demo/customer source emulators

Six DB + two file sources:
- Meltshop PostgreSQL;
- Caster Oracle;
- HSM Oracle;
- PKL MSSQL;
- Downtime MySQL;
- Parsytec/MySQL inspection;
- Excel Yard;
- Excel QA.

Rules:
- first creation `docker compose ... up -d`, not `start`;
- use `stop`, not `down -v`, unless deliberately destroying fixtures/volumes;
- source schemas should look like customer systems;
- do not rename external source schemas into internal `ppiq_*` structures;
- sources can stay off during ordinary M1 dashboard/engine work and come on for connector/import/J4–J15 rehearsal.

## 4.3 Server / release topology — historical working reference, NOT current re-certification

Historical host: `178.105.152.180`

Historical routes:
- App: `https://app.178.105.152.180.sslip.io`
- API: `https://api.178.105.152.180.sslip.io`
- Website: `https://website.178.105.152.180.sslip.io`
- Jenkins: `https://jenkins.178.105.152.180.sslip.io`

Permanent two-project topology:
- `plantprocessiq` = long-lived infra / Caddy / Jenkins / backup edge;
- `ppiq-app` = application deployment.

**Do not merge these compose projects.**

Only Caddy should expose public 80/443. App/API/Postgres remain internal/private/loopback.

Server main PostgreSQL is Dockerized (`plantprocess-postgres`) unlike the laptop native DB.

Server secrets/env historically preserved at:
`/var/lib/ppiq-preserve/.env`

Do not delete/regenerate that env independently of the existing PostgreSQL volume. Password/env and volume state are coupled; changing one without the other caused historical PostgreSQL `28P01`.

Historical release proof:
- Jenkins `plantprocessiq-deploy` build **#96** green;
- commit `94b8fb4f`;
- frontend follow-up `ec165699`;
- app UI reachable;
- permanent `sysadmin` provisioned;
- Enterprise licence activated.

August sessions did **not** continuously re-certify the live server. Say **historically working**, not “currently production-certified”, until reverified.

---

# 5. CURRENT IMPLEMENTATION / AUDIT SNAPSHOT

Latest local Ultimate Audit loaded in this session:
`00_Master_Index_16Aug2026_123644.txt`

Generated 16-Aug-2026 12:43:49:
- **2,474 files**
- **415,299 lines**
- **28.736 MB** audited text
- Backend Core: 672 files / 96,813 lines
- Backend Database: 149 / 37,530
- Backend Tests: 229 / 25,400
- ML Runtime: 108 / 17,658
- Frontend App: 602 / 87,336
- Frontend Misc: 105 / 7,998
- Infrastructure: 8 / 856
- Tools/Validation/Docs/Misc: 501 / 77,377
- Demo SQL/Data seed: 14 / 56,146
- Website: 86 / 8,185

This is a **source inventory**, not automatic defect truth.

The audit signal summary included hits such as:
- frontend tests enumerated instead of executed;
- `catchError`-success patterns;
- hardcoded server IP;
- wrong connection-string key;
- bootstrap admin markers;
- TODO/FIXME/HACK.

Many signals can occur in backup/reference files. Verify the actual current file before opening product work from a signal.

---

# 6. ROADMAP / BACKLOG AUTHORITY — v2.10.4

The authoritative workbook now contains:

- **200 tasks**
- **1806 programme hours**
- static statuses:
  - **71 Done**
  - **1 In Progress**
  - **128 Not Started**

This supersedes the older 167-task / 193-task snapshots for planning.

## 6.1 Phase summary

| Phase | Milestone | Title | Tasks | Hours | Static open h | AI bottleneck / ruling |
|---|---|---|---:|---:|---:|---|
| M1-P1 | M1 | Presentation Truth and Dataset Foundation | 12 | 84 | 0 | Closed / no remaining AI load. |
| M1-P1b | M1 | Presentation Fleet v2 - capture, reconcile, enhance, scale, materialise canonical, prove | 17 | 114 | 0 | Closed / no remaining AI load. |
| M1-P2 | M1 | No-Code Authoring Shell - wiring, SQL and widget authoring | 11 | 107 | 0 | Closed / no remaining AI load. |
| M1-P3 | M1 | BI Workspace and the Seven Showcase Pages | 12 | 80 | 20 | Worker 2 is the guarded load bottleneck; follow Task Map gates. |
| M1-P4 | M1 | Journey J4 to J15 and the Engine Slice | 16 | 106 | 106 | Worker 1 is the guarded load bottleneck; follow Task Map gates. |
| M1-P5 | M1 | Assistant Dock and Presentation Certification | 15 | 83 | 37 | Worker 2 is the guarded load bottleneck; follow Task Map gates. |
| M2a-P1 | M2a | Canonical Schema Authority and the Unified Definition Store | 11 | 114 | 114 | Worker 1 is the guarded load bottleneck; follow Task Map gates. |
| M2a-P2 | M2a | Permanent Relationship Model and Projection Quarantine | 12 | 100 | 100 | Worker 1 is the guarded load bottleneck; follow Task Map gates. |
| M2a-P3 | M2a | Job Runtime, Delta Propagation and Security Hardening | 12 | 106 | 106 | Worker 1 is the guarded load bottleneck; follow Task Map gates. |
| M2a-P4 | M2a | Commissioning, Roles, Licence and the On-Site Package | 15 | 170 | 170 | Worker 1 is the guarded load bottleneck; follow Task Map gates. |
| M2b-P0A | M2b | Parallel ML Runtime and Data-Artifact Foundations | 8 | 86 | 0 | Closed / no remaining AI load. |
| M2b-P0B | M2b | Parallel Model, Statistical and Assistant Kernels | 8 | 86 | 0 | Closed / no remaining AI load. |
| M2b-P1 | M2b | Canonical Intelligence Persistence and Practice Core | 9 | 102 | 102 | Worker 3 is the guarded load bottleneck; follow Task Map gates. |
| M2b-P2 | M2b | Model Integration, Intelligence Binding and Canonical Readiness | 7 | 80 | 80 | Worker 3 is the guarded load bottleneck; follow Task Map gates. |
| M2b-P3 | M2b | Prediction, Remediation and Canonical Intelligence Surfaces | 10 | 104 | 104 | Worker 3 is the guarded load bottleneck; follow Task Map gates. |
| M2b-P4 | M2b | Assistant Cutover, Engine Convergence and Gate Closure | 8 | 80 | 80 | Worker 3 is the guarded load bottleneck; follow Task Map gates. |
| M3-P1 | M3 | Site Stabilisation and Real-Data Performance | 8 | 96 | 96 | Worker 1 is the guarded load bottleneck; follow Task Map gates. |
| M3-P2 | M3 | Production Certification, Enterprise Operations and Commercial Completion | 9 | 108 | 108 | Worker 1 is the guarded load bottleneck; follow Task Map gates. |

## 6.2 M1 strict closure estimate with live overrides

Workbook phase summary records:
- total required to Presentation = **574 h**
- strict static Done = **411 h**
- static Done % ≈ **71.6%**

This session proved two static-status overrides that add strict completed hours:
- T-049 = 4 h, now CLOSED;
- T-051 = 6 h, now CLOSED.

Therefore a conservative live strict-Done estimate is at least:

```text
421 / 574 h = ~73.3%
```

Do **not** count T-050, T-057 or T-064 as Done until their frozen acceptance closes; partial work contributes zero strict closed hours.

---

# 7. MAJOR COMPLETED / RECENT IMPLEMENTATION HISTORY

## 7.1 M1 populated-data recertification

`ppiq_presentation` was recertified with **17/17 PostgreSQL tests green**, and the proofs were explicitly non-vacuous.

Representative populated counts:
- ParameterObservations: **301,560**
- RiskScores: **500**
- DowntimeEvents: **630**
- CrewSteps: **3,780**
- QualityEvents: **7,844**
- GradedMaterials: **35,915**
- OverlappingPairs: **160**

**Do not rerun this merely to learn the dataset.** Rerun only if data producers/schema relevant to that certification change.

## 7.2 T-045 and R1 remediation

T-045 original closure: `5f2c9b49`.

R1 deliverables:
- R1-A Readiness: `c56008c0`
- R1-B Canonical correlation execution: `dd9a6b04`
- R1-C Risk evidence: `39ce59ef`
- R1-D Equipment: `283aae2c`

R1 is considered complete. Canonical certification includes governed outcomes where honest abstention is correct:
- readiness gate may abstain for defect class/position/rate-per-m2;
- defect severity can be NoData.

Important design principle reinforced:
**a correct refusal/abstain is a product result; do not invent a chart to make the demo look full.**

## 7.3 T-046 chart grammar

Key implemented rules:
- chart compatibility is governed, not a free dropdown;
- `No` = structurally incompatible;
- `NoForThisQuery` = compatible in principle but current query state cannot support it;
- Heatmap structural requirements clarified;
- temporal/shape compatibility must be validated;
- unavailable chart types should be explained, not silently hidden or allowed to fail.

Early commits included:
- `b6ddf390`
- `9659b0e0`

## 7.4 T-047 final page bindings

Final certification commit:
`4b431463`

Important content:
- positional heatmap bound to canonical positional facts;
- specification limits use real canonical ProductSpecifications with nulls preserved;
- equipment pair uses real downtime measures/events;
- unsupported correlation/risk/model findings remain refusal/insufficient-history rather than fake visible charts.

## 7.5 T-048 associative state

Closed:
`b687cba4788b7f3b51ae68d3b559f2d7180dde61`

Implemented:
- fourth associative state `ALTERNATIVE`;
- registry-driven associative field set;
- excluded decided before alternative;
- no hardcoded eight-field product list.

## 7.6 T-049 layout persistence

Closed:
`cc4d88444a81b714436aa3f377c2042bb8212bbb`

Proved:
- layout drag;
- resize;
- save;
- hard reload;
- responsive behavior across three viewports;
- snapshot/restore of original;
- PATCH authority and reload convergence.

Known deferred data hygiene:
`PRODUCTION_OVERVIEW` persisted `lg` layout had been damaged by early unhydrated T-049 runs (one grid row tall). This must be repaired before visual-regression baselining under T-078/T-080; do not mix it into unrelated current tasks.

## 7.7 T-051 widget isolation / seven canonical states

Closed:
`6c99e0911b91a60c767c4db27d08fa9ccee28af1`

Accepted evidence:
- `WidgetStatePanel.test.tsx` 7 tests;
- `hasEffectiveFilter.test.ts` 3 tests;
- total unit/component **10/10**;
- Playwright `t051-widget-isolation.spec.ts` **4/4**;
- `tsc -b` green;
- Vite production build green.

Frozen states:
```text
empty
loading
populated
filtered-empty
blocked
refused
failed
```

Boundary is inside a grid cell; sibling widgets survive.

Stale roles rule:
- stale + usable bindings → advisory banner + normal render;
- stale + required bindings unusable → blocked.

## 7.8 PR-050-01 governed execution evidence prerequisite

Closed:
`964608045942527c281ae32a05484d64ffaf8103`

8 exact files committed.

Live certification v3:
- A ordinary read side-effect free;
- C evidence requested without complete identity → no write/handle + explicit warning;
- B explicit request returns WidgetResult handle;
- I handle resolves through existing T-073 HTTP authority;
- D real filter context persisted;
- E deterministic reuse;
- F changed context → distinct evidence identity/fingerprint;
- population descriptor truthfulness;
- multi-row distinct descriptors;
- no grouped row-count masquerading as population count.

Representative live evidence:
- rows before 66;
- ordinary read 66→66, handle null;
- explicit evidence returned id `be7dcdda-c958-49f1-a656-df103559d85d`;
- existing handle resolved at `/api/assistant/evidence/widget-result/{id}`;
- real filter context stored:
  `{"toUtc":"2026-06-30T23:59:59.0000000Z","fromUtc":"2026-01-01T00:00:00.0000000Z"}`;
- populationCount example 16701;
- multi-row subject 5 rows / 5 descriptors / 5 distinct fingerprints / no rowCount substitution.

Do not rerun PR-050 certification for orientation.

## 7.9 T-064 core and EF/runtime repair

Core:
`fb575a147d9edc60415e4d3235bf86ba77a0b2da`

Core includes:
- target definition id/kind;
- pinned/current-published policy;
- resolver/job-class target semantics;
- JB01–JB04;
- definition/version run snapshot;
- compatibility persistence;
- immutable script `824_t064_job_target_definition.sql`.

Original omission: mandatory `target_parameters`.

Core introduced EF model change without generated migration parity and fresh API startup failed with `PendingModelChangesWarning`.

Tech-lead corrective:
`b811696be688fcb613bfc986ff9b3729bcc86398`

Corrective proved:
- generated `T064JobTargetDefinitionParity`;
- migration scope only `job_definitions` and `job_run_histories`;
- pending model changes = zero;
- fresh DB full EF chain;
- immutable 824 replay;
- physical convergence `target_definition_kind text → varchar(64)`;
- EF composite index convergence;
- exact managed schema parity;
- migration history reconciled only after parity;
- EF database update physical no-op;
- Presentation API `/api/version` HTTP 200;
- exact four-artifact commit.

Added immutable:
`825_t064_target_definition_kind_varchar64_convergence.sql`

T-064 stayed OPEN for target_parameters.

---

# 8. CURRENT WORKER 2 — T-050 DEEP STATE

## 8.1 Frozen T-050 provenance contract

Clicked point carries two distinct truths:

1. **Population descriptor**
   - semantic identity of the returned/grouped population;
   - dimensions/values;
   - measure;
   - parameter where present;
   - effective filter fingerprint;
   - truthful populationCount only when genuinely known.

2. **Execution evidence**
   - `ProvenanceHandleRef`;
   - kind `WidgetResult`;
   - exact widget execution evidence;
   - not physical source-row lineage.

Never present WidgetResult as row lineage.

PR-050 request additions:
```text
options.includeExecutionEvidence?: boolean
executionIdentity?: {
  pageCode,
  widgetCode,
  widgetDefinitionId
}
```

Response additions:
```text
rowPopulations?: [{
  rowIndex,
  rowFingerprint,
  dimensionBindings,
  measureCode,
  parameterCode,
  filterContextFingerprint,
  populationCount
}]

executionEvidenceHandle?: {
  kind,
  id,
  detail?
}
```

`rowPopulations` is always computed and side-effect free.
`executionEvidenceHandle` is opt-in.

## 8.2 Guard 1 — authoritative row identity

Do **not** assume visual point index == backend row index.

Implemented primitive:
- stamp `__ppiqSourceRowIndex` onto rows at source-result construction;
- non-mutating;
- chart datum carries the original backend index through reorder/sort/slice/projection;
- click reads it;
- `populationForRow` matches descriptor by `descriptor.rowIndex`, not by array position.

Tests **8/8**:
- source stamping non-mutating;
- reorder;
- sort by value;
- 50-row slice (first visual row can represent backend row 50);
- projection;
- missing/malformed stamps return null;
- descriptor matched by rowIndex;
- no identity → no population, never guessed.

Regression:
- dashboard tests **39/39**;
- T-051 Playwright **4/4**.

## 8.3 Step 2a — contract/evidence helper

Frontend dashboard type contract mirrors PR-050 fields:
- includeExecutionEvidence;
- executionIdentity;
- executionEvidenceHandle;
- rowPopulations;
- populationCount nullable;
- filterContextFingerprint.

Evidence helper keeps three outcomes separate:
1. producer warning `execution_evidence_unavailable`;
2. resolver returns null/404;
3. transport/request failure.

Focused Step1+2a tests:
**15/15**
TypeScript: green.

Important pack lesson:
an anchor that searched for a closing `}` accidentally matched the `}` inside a type alias ending `};`. The correct fix was to anchor the **whole interface**, not keep making broader text guesses.

## 8.4 Step 2b — exact implementation next

Fresh Worker-2 session must start with code.

Required chain:
```text
render result
→ sourceRowIndex
→ populationForRow(result.rowPopulations, sourceRowIndex)
→ drawer population immediately
→ reuse SAME rendered query request snapshot
→ includeExecutionEvidence=true
→ complete executionIdentity
→ executionEvidenceHandle
→ assistantApi.getWidgetResultEvidence(handle.id)
→ render evidence / honest gap
```

Critical stale-context test:
```text
render under effective context A
UI/global filters later change to B
click a point from old render A
evidence request MUST still execute A
```

The request snapshot should travel with the rendered point/context; do not rebuild from mutable filters at click time.

Do not:
- request evidence on ordinary render;
- generate evidence identity in frontend;
- create another endpoint/store;
- call execution evidence physical lineage;
- replace null populationCount with `rows.length`, chart count or series count.

Final T-050 gate should include:
- focused tests;
- row identity;
- dashboard regression;
- T-050 Playwright;
- T-051 Playwright regression;
- `npm run build`;
- exact staging;
- one provenance-half commit;
- T-050 CLOSED;
- T-052 start.

---

# 9. CURRENT WORKER 1 — T-057 DEEP STATE

## 9.1 Backlog/frozen ruling

F-052:
M1 cannot honestly create final canonical relationship tables because final `ppiq_meta` + `definition_store` authority is M2a-owned.

Therefore:
- T-057 external/service relationship contract is final;
- M1 persistence may be compatibility storage;
- no fake FK to a target not yet canonical;
- T-095 owns final `ppiq_meta.plant_relationships/_members/_paths` convergence after T-089/T-090;
- T-058/T-059/T-060 consume the service/API, never compatibility table names.

Frozen refusal catalogue:
- RL01 ambiguous path;
- RL02 unproven relationship used by automated consumer;
- RL03 no path between entities;
- RL04 retirement blocked by active dependent.

T-057 does not own full T-058 resolver behavior.

## 9.2 Current on-disk T-057 implementation

Created:
- `Backend/PlantProcess.Application/Relationships/RelationshipContracts.cs`
- `Backend/PlantProcess.Application/Relationships/IRelationshipService.cs`
- `Backend/PlantProcess.Application/Relationships/RelationshipService.cs`
- `Backend/PlantProcess.Infrastructure/Relationships/NpgsqlRelationshipStore.cs`
- `Backend/PlantProcess.Infrastructure/Relationships/RelationshipInfrastructureExtensions.cs`
- `Backend/PlantProcess.Api/Endpoints/Relationships/RelationshipEndpoints.cs`
- `Backend/database/scripts/827_t057_relationship_compatibility_persistence.sql`
- `Backend/tests/PlantProcess.Application.UnitTests/Relationships/T057RelationshipContractTests.cs`

Modified:
- `Backend/PlantProcess.Api/Program.cs`

Current implementation is **not committed**.

## 9.3 T-057 gates already green

Pack v2 SHA:
`386B142F9AE165DB48816EBD1177F7ECB2CA2BE0FCB734C37761EAD4026A149F`

Proved:
- 827 free before creation;
- exact new paths;
- Program.cs anchors unique;
- no authoring endpoint;
- compatibility table names hidden above persistence adapter;
- no FK to `definition_store`;
- no `ppiq_meta` canonical claim;
- full RL refusal catalogue present;
- build clean;
- zero scoped warnings;
- tests **665/665**;
- 827 applied cleanly;
- 827 replayable.

Still outstanding:
**runtime publication/read-back**.

Acceptance must prove through service/internal publication seam + read API:
- publish one relationship;
- source definition identity/version preserved;
- members/cardinality preserved;
- publication state preserved;
- published read-back succeeds;
- unpublished relationship excluded from consumer reads.

Do not certify by direct SQL insertion.
Do not invent public POST authoring.

## 9.4 T-057 tooling lesson

The first pack falsely detected forbidden terms because the negative scan read its own header comments explaining “no FK to definition_store / no ppiq_meta”.

Fix:
- comment-strip before absence scans.

But comment stripping itself is textual and can false-negative if a needle appears in a string around comment-like syntax. Long-term better rule:
**prefer positive structural assertions and owned-region checks over broad negative text scanning.**

---

# 10. CURRENT WORKER 3 — T-064 target_parameters DEEP STATE

## 10.1 Mandatory remaining semantics

Must exist through the whole target flow:
- `JobDefinition.TargetParametersJson`
- `JobRunHistory.TargetParametersJson`
- `JobTargetReference.ParametersJson`
- `ResolvedJobTarget.ParametersJson`

Frozen semantics:
- valid JSON accepted;
- malformed JSON refused **before assignment/persistence**;
- null remains null;
- null != `"{}"`;
- exact supplied payload preserved;
- run history is immutable snapshot;
- later definition edits cannot rewrite a historical run;
- no per-job parameter schema invented.

## 10.2 Real defect discovered and corrected in candidate implementation

Initial validation placement was after several target fields had already been assigned.

Bad state:
```text
TargetDefinitionKind = ...
TargetDefinitionId = ...
TargetVersionPolicy = ...
... JobTargetParameters.Require throws ...
```

Result:
a rejected assignment left the entity half-written.

The test caught:
`Assert.False(job.HasTargetDefinition)` → expected False, actual True.

Correct invariant:
**validate all inputs, including parameters, before the first mutation**.

Same issue existed in `JobRunHistory.RecordResolvedTarget`; strengthen its test to assert **no target field** was recorded on malformed parameters.

## 10.3 Latest v3 evidence

Preflight:
- base `b811696b` ancestor of HEAD `96460804`;
- index clean;
- presentation connection correct;
- Worker1/2 dirty files preserved.

Baseline:
- Application: **664 passed / 1 failed** — malformed-parameter test at baseline;
- Domain: **11/12**, carried F-DEFVER-01;
- Architecture: **181/181**.

After candidate correction:
- build green;
- targeted **36/36**;
- Application **665/665**;
- Domain **11/12**, same carried failure only;
- Architecture **181/181**;
- introduced regression failures = 0.

DB 826 proof:
- `PARAMS 7`
- `NULL PRESERVED`
- `EMPTY DISTINCT`
- `ORPHAN ENFORCED`
- probe transaction rolled back.

EF migration:
`20260817094454_T064TargetParametersParity`

Generated Up:
- add nullable jsonb `target_parameters` to `job_run_histories`;
- add nullable jsonb `target_parameters` to `job_definitions`.

Down:
- drop those same two columns.

No foreign columns.
Pending model changes = ZERO.

## 10.4 Latest Step-11 failure classification

Pure EF-created T-064 target shape had 14 entries.

Replaying 824/825/826 added:
- `ix_job_definitions_target_definition`
- `ix_job_run_histories_target_definition`

The pack treated any replay index delta as failure.

This is wrong because immutable raw-SQL 824 may carry operational indexes intentionally outside EF model.

Correct gate:
1. Compare EF-owned columns before/after each replay.
2. 824 may add only the exact index definitions declared in immutable 824.
3. 825 should converge/no-op on correct fresh EF-managed shape.
4. 826 should be no-op after EF target_parameters migration.
5. Final combined physical schema must match presentation exactly in both directions.

## 10.5 CRITICAL cleanup concern

The v3 run applied `826_t064_target_parameters.sql` to **live** `ppiq_presentation` before Step 11.

On later failure, printed cleanup showed:
- migration files removed;
- snapshot restored;
- 8 source/test files restored;
- 826 source file removed;
- throwaway DB dropped.

It **did not print proof that the live `ppiq_presentation` target_parameters physical change was reverted**.

Therefore next action is not “generate v4 and run”.
First inspect live DB:

```sql
SELECT table_name, column_name, data_type, is_nullable
FROM information_schema.columns
WHERE table_schema='public'
  AND table_name IN ('job_definitions','job_run_histories')
  AND column_name='target_parameters';

SELECT COUNT(*) FILTER (WHERE target_parameters IS NOT NULL)
FROM public.job_definitions;

SELECT COUNT(*) FILTER (WHERE target_parameters IS NOT NULL)
FROM public.job_run_histories;
```

Also inspect:
- any constraint/index referencing target_parameters;
- migration-history row for `T064TargetParametersParity`.

If residue is exactly empty nullable columns from failed v3 and no history row/non-null data, remove **only that failed-run residue** before rerun.

Never touch Worker1's 827 relationship objects while repairing W3 residue.

## 10.6 SQL numbering authority right now

```text
824  T-064 core target-definition compatibility         COMMITTED / IMMUTABLE
825  T-064 target_definition_kind varchar64 convergence COMMITTED / IMMUTABLE
826  RESERVED Worker 3 T-064 target_parameters          currently source-absent after v3 auto-revert; LIVE DB residue must be checked
827  Worker 1 T-057 relationship compatibility          source on disk, applied to ppiq_presentation, UNCOMMITTED
999  highest numbering region already exists for runtime grants; do not use “highest + 1”
```

Reservation beats naive “next free numeric script”.

---

# 11. TEST / EVIDENCE LEDGER — ANTI-REPEAT REGISTER

The point of this section is to stop the next session from burning time rerunning tests merely to discover state.

| Area / Gate | Result | Re-run rule |
|---|---|---|
| M1 populated PostgreSQL recertification | **17/17 GREEN**, non-vacuous | Do not rerun for orientation. Re-run only if relevant producers/data/schema changed. |
| T-045-R1 | R1-A/B/C/D complete | Preserve closure unless regression contradicts it. |
| T-047 final page binding | commit `4b431463` | Do not reopen absent page-binding regression. |
| T-048 associative | commit `b687cba4` | Do not reopen absent regression. |
| T-049 layout | closed `cc4d8844...` | No rerun unless layout implementation changes; damaged presentation lg data is separate deferred hygiene. |
| T-051 units | **10/10** | T-050 changes adjacent dashboard code, so final T-050 should rerun relevant regression once. |
| T-051 Playwright | **4/4** | Same: rerun once at T-050 final closure. |
| T-051 build | `tsc -b` + Vite green | No standalone rerun; final T-050 build will supersede. |
| PR-050 live certification | all A/C/B/I/D/E/F + population gates PASS | Do not rerun unless backend contract changes. |
| T-064 parity corrective | pending=0, fresh DB, parity, API 200, commit `b811696b` | Do not rerun corrective standalone. Final T-064 closure runs equivalent current-state proof. |
| W2 row identity | **8/8** | Preserve; final T-050 suite includes it. |
| W2 dashboard regression | **39/39** | Preserve until final T-050 regression. |
| W2 Step1+2a focused | **15/15** | Preserve; final T-050 suite includes it. |
| W1 T-057 build | GREEN | Runtime cert only is missing unless code changes. |
| W1 T-057 tests | **665/665** in W1 run | Do not rerun merely for orientation. Runtime cert may expose a real defect. |
| W1 SQL 827 | apply GREEN, replay GREEN | Do not rewrite SQL; runtime publication proof missing. |
| W3 target params targeted | **36/36** in candidate v3 | Candidate was auto-reverted; rerun is required as part of corrected final pack, not separately for discovery. |
| W3 Application post-fix | **665/665** | Candidate auto-reverted; final pack must reproduce. |
| W3 Domain | **11/12**, one carried F-DEFVER-01 | Do not fix under T-064. Owner T-089/T-090. |
| W3 Architecture | **181/181** | Final pack may reproduce delta gate; no investigation needed. |
| W3 migration | only target_parameters; pending=0 | Final pack should reproduce after re-apply. |
| T-178 corrective ML | **503 tests**, zero skipped; exhaustive 512 combos | Closed `b5dbf232`; do not reopen. |
| T-177 | 52 tests total (Parity 9, T177 13, P06 30) | Closed; production cutover later. |
| T-171 capability profiler | 24 tests | Closed. |
| T-179 planner | closed `d2bf1834` | Do not reopen. |
| T-180 retrieval/packer | closed `87428c51` | Do not reopen. |
| T-181 verifier | closed `eaa82b8b`; **58/58** | Do not reopen. |

### Known evidence conflict to interpret correctly
The latest W3 v3 baseline observed Application **664/1** before the candidate correction, then **665/0** after it. Because v3 auto-reverted candidate files, do not casually declare the current shared worktree full Application suite green. The open malformed-parameters defect is part of unfinished T-064 and closes only when the final candidate commits.

---

# 12. TROUBLESHOOTING / DEBUGGING KNOWLEDGE — DO NOT RELEARN

## 12.1 PowerShell / pack engineering

1. `"$Label: ..."` is invalid interpolation in PowerShell because `$Label:` is parsed like scoped/drive syntax. Use `"${Label}: ..."`.
2. `Start-Process -ArgumentList` can flatten an array and destroy quoting for SQL like `-c "CREATE DATABASE ..."`. Safer:
   - robust `ProcessStartInfo` argument quoting;
   - or SQL file + `psql -f`;
   - or `createdb.exe` / `dropdb.exe` for DB lifecycle.
3. Global `dotnet-ef` may be newer than project EF. T-064 hit global **10.0.8** vs project **9.0.4**. Pin private `dotnet-ef 9.0.4`; leave global untouched.
4. EF migration design-time commands need the correct profile connection string. Missing `ConnectionStrings__PlantProcessDb` is a legitimate preflight failure.
5. Inspect **every AddColumn/DropColumn/AlterColumn name**, not only names matching `target_*`. A migration could otherwise silently drop an unrelated column and still pass a weak report.
6. Once a numbered SQL script is committed, treat it immutable; convergence is a new additive script.
7. Migration-history baselining occurs **only after physical parity is proven**.
8. A failed pack that changed the live DB must reverse the live DB mutation, not merely delete the source SQL/migration file.
9. Broad negative scans often match comments explaining what is forbidden. Prefer positive assertions; if negative checks remain, strip comments intentionally and document limitations.
10. A filename collision in `Downloads` can move the wrong old pack while the command looks right. Use distinct revision names and SHA256.
11. Windows LF→CRLF warnings do not automatically indicate content corruption. Judge actual diff/stat.
12. A pack should fail on a silent no-op when a required mutation was expected.
13. Avoid generating a “pack fix project”. Fix one concrete anchor/runner bug and continue the product task.

## 12.2 API/runtime

1. A stale process can survive while new DLLs are built. Always verify PID/start time/path or restart deliberately before treating runtime as current.
2. Earlier T-064 runtime failure was real: API died in `MigrateAsync()` on `PendingModelChangesWarning`.
3. After parity corrective, startup log proved:
   `No migrations were applied. The database is already up to date.`
4. A second API launch failed with `address already in use` because the corrective deliberately left the first process running. Port collision is not a migration defect.
5. `/api/version` body may show `"commit":"unknown"` even when startup log reports assembly version with commit. For the T-064 runtime gate the requirement was HTTP 200/stable startup, not build-metadata cleanup.
6. `Invoke-RestMethod`/`Invoke-WebRequest` debugging: 4xx bodies may need explicit catch/response-stream reading.
7. Internal health + authenticated external reachability is distinct from an unauthenticated public `/health` response.

## 12.3 PostgreSQL / EF

1. Main laptop DB is native Windows PostgreSQL, not containerized.
2. Presentation certification must explicitly target `ppiq_presentation`.
3. Raw SQL scripts may intentionally carry operational indexes not modeled in EF. Do not require “pure EF schema == after raw SQL” unless the frozen contract says so.
4. Better convergence model:
   - verify EF-owned columns are not mutated unexpectedly by replay;
   - allow exact declared raw-SQL additions;
   - compare final combined physical schema to live target.
5. Before narrowing `text → varchar(64)`, measure max existing data length. Never truncate silently.
6. Null and empty JSON object are semantically distinct for target_parameters.
7. JSON validation belongs before mutation; rejected commands must not leave half-written aggregate state.
8. Existing migration history must not be fabricated merely to silence EF. Baseline only after schema equivalence.

## 12.4 Frontend / provenance

1. Visual point order can differ from backend result-row order due to:
   - sort;
   - slice;
   - projection;
   - series transformation.
   Never use visual position as population identity.
2. Preserve original backend row index in datum metadata.
3. Match population descriptor by `rowIndex` property, not descriptor array position.
4. `populationCount` may be null and is never equivalent to number of chart rows.
5. Ordinary dashboard rendering must remain side-effect free; evidence write is drilldown opt-in only.
6. Evidence re-execution must use the exact request snapshot from the render. A stale-context race must not attach context B evidence to a context A point.
7. T-073 resolver 404/null means evidence unavailable, not transport failure.
8. Keep:
   - `execution_evidence_unavailable`;
   - resolver-null;
   - transport failure
   distinct.
9. Do not reuse correlation-specific `EvidencePanel` for WidgetResult evidence if its contract is wrong; use existing WidgetResult resolver directly.
10. A text anchor looking for `}` can match inside `};`. Anchor the full syntactic/semantic region.

## 12.5 Parallel worktree

At any moment:
```powershell
git status --short
git diff --cached --name-status
git log --oneline -12
```

Non-empty index before staging = collision signal.
Never “clean the tree” by wiping other workers.

---

# 13. DEPLOYMENT / SERVER / PIPELINE — DEEP HANDOVER

## 13.1 Historical green release chain

The deployed server architecture has worked end-to-end historically.

Permanent sequence:
1. preserve runtime `.env`/Caddy before checkout;
2. checkout;
3. restore protected files;
4. `ensure-runtime-env.sh`;
5. sweep stale processes/workspace locks;
6. blocking backend tests;
7. blocking frontend tests;
8. E2E under current truthful policy;
9. app DB: PostgreSQL → EF migrations → post-EF numbered SQL → seed;
10. demo-source migration/seed where enabled;
11. build/recreate canonical stack;
12. internal health gate;
13. rollback to previous image on failure;
14. presentation defaults / licence / authenticated smoke.

## 13.2 Root fixes that historically made pipeline green / App URL work

- **Caddy upstream mismatch:** config referenced `plantprocess-app-web`; real service was `plantprocess-web`. Runtime network alias restored reachability. Permanent configs should use canonical service naming.
- **Compose project separation:** `plantprocessiq` infra vs `ppiq-app` app. Prevents app `remove-orphans` from deleting Caddy/Jenkins/infra containers.
- **DB config key:** canonical `ConnectionStrings__PlantProcessDb`, not `DefaultConnection`.
- **Vite startup:** positional `localhost 5173` was invalid on modern Vite; use explicit `--host --port`.
- **Smoke credential handling:** placeholder baked into bundle caused 401 auto-login loop; smoke secret moved to protected runtime configuration.
- **Signing key:** moved to protected/minimum-length runtime handling.
- **Server env + PostgreSQL volume:** must be preserved together; regenerating password/env while keeping old volume produced `28P01`.
- **UTF-8 discipline:** script/client encoding problems previously created false failures/corruption risk.
- **Ingress ownership:** long-lived Caddy owns public 80/443; app joins edge network.
- **Health-gated rollback:** current image tagged `:previous`; bad deploy rolls back.
- **DooD/Jenkins:** Jenkins agent itself may not have dotnet/node. Build/test containers are sibling containers sharing Jenkins workspace volumes.
- **Alpine curl:** use `sh`, not assumptions about bash.
- **External `/health` 401:** does not prove process down; distinguish protected external endpoint from internal service health.

## 13.3 Current Jenkinsfile truth from repository audit

Current canonical Jenkinsfile inspected in the loaded infrastructure audit:
- backend `dotnet test Backend --nologo` is executed in .NET 9 SDK sibling container;
- frontend runs `npm ci; npm run test` in Node 24 container;
- E2E stage calls `deploy/scripts/ci-e2e-stack.sh`;
- tests are textually before DB migrate/seed/deploy;
- app DB step brings Postgres up, waits for readiness, then `migrate-and-seed.sh --app-only`;
- demo sources are mode-gated;
- deploy calls `deploy/scripts/deploy-canonical.sh`;
- presentation smoke runs in curl sibling container on the app private network;
- post failure reports pipeline red; post success reports deployed commit.

Do not infer current Jenkins defects solely from audit hit counts because the audit includes backup Jenkinsfiles and rule text.

## 13.4 Remaining deployment owners / debt

Do not claim production CI perfection until final owners close:
- T-150: false-green / swallowed / orphan gate truth;
- T-113: production/demo separation;
- T-112/T-114: RLS/tenant security;
- T-031: CI/restore/source retirement intersections;
- M3: capacity/HA/DR/restore/customer production acceptance;
- production Ed25519/customer signing authority;
- mail/Spamhaus remediation;
- PgBouncer/performance/partitioning where justified by measured load;
- final restore rehearsal and operational telemetry.

Permanent principle:
```text
SOURCE PASS != RUNTIME PASS != VISUAL PASS != PRODUCTION CERTIFICATION
```

---

# 14. REALISATION SCOREBOARD — END OF 17-AUG SESSION

These percentages are **engineering assessments**, not formal backlog arithmetic.

| Dimension | Assessment | Current evidence / gap |
|---|---:|---|
| Product identity / architecture clarity | ~95% | Genericity, Layer A/B, Assistant law, two-track M1/M2 and ownership boundaries are unusually explicit. |
| M1 presentation data / analytical truth | ~93% | 17/17 populated recertification; T-045-R1, T-047 final; honest refusal preserved. |
| BI workspace / visible dashboard contract | ~92% | T-048/T-049/T-051 closed; T-050 provenance final half still open. |
| No-code authoring / metadata-driven direction | ~88% | Shared shell and authoring foundation strong; full post-M1 Generic BI contract audit still deferred intentionally. |
| Provenance / evidence UX | ~82% | PR-050 backend closed; W2 row identity + contract helpers green; drawer Step 2b remains. |
| J7 relationship model M1 slice | ~75% | W1 implementation/build/tests/DB green; runtime publish/read-back + commit missing. |
| Job target/version semantics | ~82% | Core + parity committed; target_parameters candidate proved semantically but not committed/finally certified. |
| Assistant evidence / retrieval foundations | ~90% | T-071/73 evidence chain and later isolated planner/retrieval/verifier kernels strong; production cutover later. |
| Isolated ML/statistical kernels | ~93% | T-168..T-181 family largely/fully closed; production persistence/cutover still later M2b. |
| Production ML/AI integration | ~40% | Canonical persistence/jobs/security/cutover intentionally waits M2a/M2b owners. |
| Local deployment reproducibility | ~85% | profiles/scripts/migrations heavily exercised; shared-db/runtime serialization remains important. |
| Server/release deployment confidence | ~70% historical / lower current-certification confidence | Historical green #96 + live URL; August did not re-certify server end-to-end. |
| Overall M1 strict backlog closure | **>=73.3%** | 421/574 h conservative strict Done after T049/T051 live overrides; partial tasks count zero. |

---

# 15. OPEN PROBLEMS / DEFERRED FINDINGS / OWNERS

## 15.1 Current blockers / immediate
- W1 T-057 runtime publication/read-back missing.
- W2 T-050 drawer Step 2b missing.
- W3 T-064 target_parameters final persistence/runtime closure missing.
- Potential W3 failed-v3 `ppiq_presentation` residue must be inspected before rerun.

## 15.2 Explicitly deferred / different owner
- `F-DEFVER-01`: Domain architecture BaseEntity issue → T-089/T-090, not T-064.
- T-106 owns canonical physical T-064 FK/check/trigger convergence after definition-store authority.
- T-095 owns canonical relationship-table convergence.
- T-067 waits T-050/T-065/T-066.
- W2-GRID-DEFAULTS-01: nine hardcoded `defaultLayouts` keys merged into every persisted layout — deferred.
- damaged `PRODUCTION_OVERVIEW` `lg` presentation layout — repair before T-078/T-080 visual baseline.
- Assistant relevance-floor gap: Search topK historically lacked measured relevance floor; future owner should benchmark/configure threshold, not guess.
- CI truth/deploy production hardening remains under later owners.
- Generic BI/Qlik-PowerBI-level contract review waits until M1 closure.

---

# 16. COMMITS / ARTIFACTS QUICK LEDGER

```text
T-045 original                  5f2c9b49
T-045-R1-A                     c56008c0
T-045-R1-B                     dd9a6b04
T-045-R1-C                     39ce59ef
T-045-R1-D                     283aae2c

T-047 final                    4b431463
T-048                          b687cba4788b7f3b51ae68d3b559f2d7180dde61
T-049                          cc4d88444a81b714436aa3f377c2042bb8212bbb
T-050 presentation half        d6870d0aac4719918864dd44798939a4980707b3
T-051                          6c99e0911b91a60c767c4db27d08fa9ccee28af1
PR-050-01                      964608045942527c281ae32a05484d64ffaf8103

T-064 core                     fb575a147d9edc60415e4d3235bf86ba77a0b2da
T-064 EF/runtime corrective    b811696be688fcb613bfc986ff9b3729bcc86398

T-178 corrective               b5dbf232132c2abb145c59ca997b4997dfa3c8eb
T-179                          d2bf1834
T-180                          87428c51
T-181                          eaa82b8b
```

Current shared HEAD remains `964608045942527c281ae32a05484d64ffaf8103` because W1/W2 current work is uncommitted and W3 v3 auto-reverted.

---

# 17. CURRENT WORKTREE OWNERSHIP — DO NOT CROSS-CONTAMINATE

## Worker 1 owned/current
- `Backend/PlantProcess.Api/Program.cs`
- Relationship contract/service/store/DI/endpoints files
- T057 tests
- `827_t057_relationship_compatibility_persistence.sql`

## Worker 2 owned/current
Modified:
- `Frontend/PlantProcess.Web/src/api/product-core/dashboard-widget-types.ts`
- `Frontend/PlantProcess.Web/src/components/charts/InteractiveCharts.tsx`
- `Frontend/PlantProcess.Web/src/components/dashboard/SavedDashboardWidget.tsx`
- `Frontend/PlantProcess.Web/src/state/DashboardSelectionContext.tsx`

New/current primitives/tests include:
- `src/state/drilldownRowIdentity.ts`
- `src/state/drilldownRowIdentity.test.ts`
- `src/state/drilldownEvidence.ts`
- `src/state/drilldownEvidence.test.ts`

Step 2b will likely own drawer/query-integration files, but the fresh session must inspect before editing.

## Worker 3 owned/current
Final T-064 target_parameters candidate will touch:
- job targeting contracts/resolver;
- `JobDefinition`;
- `JobRunHistory`;
- job-target policy helper/validation;
- EF configurations;
- JobTargetResolver tests;
- migration/snapshot;
- SQL 826.

Latest failed v3 restored source files; **do not assume live DB 826 residue was restored**.

## Shared non-task dirt
- backlog workbook has been modified during planning/orchestration and must never be accidentally staged with product task commits;
- `tools/packs/` contains working artifacts/logs/backups;
- handover docs may be untracked.

---

# 18. COMPLETE STATIC BACKLOG REGISTER — v2.10.4 (200 TASKS)

**Interpretation:** “Static” is the workbook's 16-Aug status. The Live override column records proven changes from this session. For tasks without an override, use the workbook unless later executed evidence supersedes it.

| Task | Phase | Pri | h | Static | AI owner | Task | Live override / handover note |
|---|---|---|---:|---|---|---|---|
| T-001 | M1-P1 | Critical | 8 | Done | CLOSED | Build the six-beat Design Traceability Matrix |  |
| T-002 | M1-P1 | Critical | 8 | Done | CLOSED | Audit every presented route and control against the Chapter 3 page inventory |  |
| T-003 | M1-P1 | Critical | 4 | Done | CLOSED | Lock the presentation profile as a data profile, not a branch |  |
| T-004 | M1-P1 | Important | 4 | Done | CLOSED | Create the M1 acceptance checklist and evidence folder |  |
| T-005 | M1-P1 | Critical | 6 | Done | CLOSED | Rebuild ppiq_presentation into scratch and diff against live |  |
| T-006 | M1-P1 | Critical | 8 | Done | CLOSED | Convert every diff finding into a seed or migration script |  |
| T-007 | M1-P1 | Critical | 10 | Done | CLOSED | Presentation Phenomena and Widget Coverage Matrix, 
part 1: inventory and the 36-chart blueprint |  |
| T-008 | M1-P1 | Critical | 10 | Done | CLOSED | Presentation Phenomena and Widget Coverage Matrix, part 2: map, classify, close |  |
| T-009 | M1-P1 | Critical | 6 | Done | CLOSED | Downtime two-quantity contract: final schema and domain slice |  |
| T-010 | M1-P1 | Critical | 8 | Done | CLOSED | Run the canonical semantic path end to end through the M1 compatibility boundaries |  |
| T-011 | M1-P1 | Very Important | 6 | Done | CLOSED | Establish and fix the architecture test pool reliability |  |
| T-012 | M1-P1 | Critical | 6 | Done | CLOSED | Canonicalise the JourneyRail to J1 to J15 |  |
| T-013 | M1-P1b | Critical | 8 | Done | CLOSED | Three-way source reconciliation: KEEP, EXTEND or ADD |  |
| T-014 | M1-P1b | Critical | 8 | Done | CLOSED | Capture the current source-shaped donor schemas in a committed generator |  |
| T-015 | M1-P1b | Critical | 8 | Done | CLOSED | Presentation Fleet v2 target specification |  |
| T-016 | M1-P1b | Critical | 10 | Done | CLOSED | Extend the generator: defect catalogue and chemistry elements |  |
| T-017 | M1-P1b | Critical | 8 | Done | CLOSED | Extend the generator: grade specification, and shift as BEHAVIOUR |  |
| T-018 | M1-P1b | Critical | 6 | Done | CLOSED | Extend the generator: downtime two quantities and buffer posture |  |
| T-019 | M1-P1b | Very Important | 6 | Done | CLOSED | Shift and crew operating-practice regimes |  |
| T-020 | M1-P1b | Very Important | 6 | Done | CLOSED | Post-maintenance recovery and campaign-ageing regimes |  |
| T-021 | M1-P1b | Important | 6 | Done | CLOSED | Equipment personality and temporal regime changes |  |
| T-022 | M1-P1b | Critical | 8 | Done | CLOSED | Merge the best existing material into one Fleet v2 truth |  |
| T-023 | M1-P1b | Critical | 6 | Done | CLOSED | Scale Fleet v2 to the target plant size |  |
| T-024 | M1-P1b | Critical | 8 | Done | CLOSED | Emit and populate the presentation canonical operational entities |  |
| T-025 | M1-P1b | Critical | 8 | Done | CLOSED | Compute and populate the presentation analysis entities with the real engines |  |
| T-026 | M1-P1b | Critical | 6 | Done | CLOSED | Phenomenon test harness: manifest schema and runner |  |
| T-027 | M1-P1b | Critical | 6 | Done | CLOSED | Populate the manifest and prove every phenomenon |  |
| T-028 | M1-P1b | Critical | 2 | Done | CLOSED | Verify the confounded correlation and the insufficient-support refusal |  |
| T-029 | M1-P1b | Very Important | 4 | Done | CLOSED | Five-layer realism audit of the emulated plant |  |
| T-030 | M1-P2 | Critical | 8 | Done | CLOSED | Emit and populate the presentation staging representation, source-shaped |  |
| T-031 | M1-P2 | Critical | 10 | Done | CLOSED | Certify cross-layer consistency and retire the obsolete donor state |  |
| T-032 | M1-P2 | Critical | 12 | Done | CLOSED | Shared Authoring Shell, part 1: the shell contract and the four regions |  |
| T-033 | M1-P2 | Critical | 12 | Done | CLOSED | Shared Authoring Shell, part 2: relational block grammar on the board |  |
| T-034 | M1-P2 | Very Important | 10 | Done | CLOSED | Registry-driven schema, table and attribute tree |  |
| T-035 | M1-P2 | Very Important | 8 | Done | CLOSED | Compiled-SQL pane and debug log with rows and cost |  |
| T-036 | M1-P2 | Critical | 12 | Done | CLOSED | SQL mode: safe editor, run test, returned columns and the reconstructability rule |  |
| T-037 | M1-P2 | Important | 3 | Done | CLOSED | Certify returned-column role mapping inside the S2 shell |  |
| T-038 | M1-P2 | Critical | 12 | Done | CLOSED | Add Widget and Edit Widget open the shared shell in S2 mode |  |
| T-039 | M1-P2 | Critical | 12 | Done | CLOSED | Final definition service interface with a compatibility adapter |  |
| T-040 | M1-P2 | Very Important | 8 | Done | CLOSED | Authoring states, keyboard path, RTL and error wording |  |
| T-041 | M1-P3 | Critical | 6 | Done | CLOSED | D2 Page Builder, part 1: create a page and reach the shared shell |  |
| T-042 | M1-P3 | Critical | 6 | Done | CLOSED | D2 Page Builder, part 2: arrange, save layout and publish |  |
| T-043 | M1-P3 | Critical | 12 | Done | CLOSED | Bring the workspace to the final D1 anatomy |  |
| T-044 | M1-P3 | Critical | 8 | Done | CLOSED | Certify the three operational dashboards and fix their bindings |  |
| T-045 | M1-P3 | Critical | 6 | Done | CLOSED | Certify the analysis and model dashboards and choose the six shown |  |
| T-046 | M1-P3 | Critical | 8 | Done | CLOSED | Register the final chart grammar and implement the presentation subset |  |
| T-047 | M1-P3 | Very Important | 10 | Done | CLOSED | Give the seven pages distinct visual grammars from the registered grammar |  |
| T-048 | M1-P3 | Very Important | 4 | Done | CLOSED | Associative model, part 1: the alternative state and registry-driven fields |  |
| T-049 | M1-P3 | Important | 4 | In Progress | Worker 2 | Certify layout drag, resize, save, reload and responsive behaviour | LIVE CLOSED — `cc4d88444a81b714436aa3f377c2042bb8212bbb`. |
| T-050 | M1-P3 | Very Important | 6 | Not Started | Worker 2 | Drill to population, provenance and evidence | LIVE IN PROGRESS / PARTIAL. Presentation half committed `d6870d0aac4719918864dd44798939a4980707b3`. Provenance Step 1 + Step 2a are applied and uncommitted; Step 2b is next. |
| T-051 | M1-P3 | Critical | 6 | Not Started | Worker 2 | Widget failure isolation and the seven states | LIVE CLOSED — `6c99e0911b91a60c767c4db27d08fa9ccee28af1`. |
| T-052 | M1-P3 | Critical | 4 | Not Started | Worker 2 | Remove the hardcoded parameter default from the API client |  |
| T-053 | M1-P4 | Critical | 4 | Not Started | Worker 2 | Reduce the demonstration navigation and add the inventory ratchet |  |
| T-054 | M1-P4 | Very Important | 4 | Not Started | Worker 1 | J4 Connections: read-only proof and load budget made visible |  |
| T-055 | M1-P4 | Very Important | 6 | Not Started | Worker 1 | J5 and J6 Dataset registry browse and watermark suggestion |  |
| T-056 | M1-P4 | Important | 4 | Not Started | Worker 1 | J6 Import progress visibility |  |
| T-057 | M1-P4 | Critical | 10 | Not Started | Worker 1 | J7 Relationship model vertical slice, part 1: publish one relationship | LIVE IN PROGRESS. Implementation applied but UNCOMMITTED. Build/warnings/tests/827 apply+replay are green; runtime publish/read-back certification remains. |
| T-058 | M1-P4 | Critical | 10 | Not Started | Worker 1 | J7 Relationship model vertical slice, part 2: one resolver consumer |  |
| T-059 | M1-P4 | Very Important | 4 | Not Started | Worker 2 | Associative model, part 2: cross-source state through the published relationship |  |
| T-060 | M1-P4 | Very Important | 4 | Not Started | Worker 1 | C6 Relationship Browser, minimal read-only slice |  |
| T-061 | M1-P4 | Critical | 6 | Not Started | Worker 1 | C2 Mapping Health, part 1: the typed issue contract and the reprocess API |  |
| T-062 | M1-P4 | Critical | 4 | Not Started | Worker 1 | C2 Mapping Health, part 2: the final visible page |  |
| T-063 | M1-P4 | Very Important | 10 | Not Started | Worker 1 | C5 Genealogy: converge the legacy workbench onto the final two-state surface |  |
| T-064 | M1-P4 | Critical | 8 | Not Started | Worker 3 | Add job_definitions.target_definition_id and the JB error codes | LIVE IN PROGRESS / PARTIAL. Core committed `fb575a147d9edc60415e4d3235bf86ba77a0b2da`; EF/runtime parity corrective committed `b811696be688fcb613bfc986ff9b3729bcc86398`; target_parameters completion remains open and latest v3 auto-reverted after a replay-gate false positive. |
| T-065 | M1-P4 | Critical | 12 | Not Started | Worker 2 | J12 Analysis authoring: converge onto D3 Analysis Toolbox in S3 mode |  |
| T-066 | M1-P4 | Critical | 6 | Not Started | Worker 2 | One visible readiness authority on Home and Analysis |  |
| T-067 | M1-P4 | Very Important | 8 | Not Started | Worker 3 | Findings evidence panel, registry-driven throughout | Still BLOCKED until T-050 + T-065 + T-066 are all closed. |
| T-068 | M1-P4 | Critical | 6 | Not Started | Worker 2 | Retire the hardcoded outcome and grain arrays |  |
| T-069 | M1-P5 | Critical | 8 | Done | CLOSED | Website, part 1: the five-product information architecture |  |
| T-070 | M1-P5 | Important | 6 | Done | CLOSED | Website, part 2: polish the presentation routes |  |
| T-071 | M1-P5 | Critical | 8 | Done | CLOSED | Build the G1 persistent assistant dock |  |
| T-072 | M1-P5 | Critical | 8 | Done | CLOSED | Page and widget context envelope |  |
| T-073 | M1-P5 | Critical | 8 | Done | CLOSED | Add the page and widget chunk family to the retrieval corpus |  |
| T-074 | M1-P5 | Very Important | 4 | Done | CLOSED | Registry-typed quantity guard on assistant answers |  |
| T-075 | M1-P5 | Very Important | 4 | Done | CLOSED | Citation chips, evidence strip and suggested questions |  |
| T-076 | M1-P5 | Very Important | 4 | Not Started | Worker 3 | Certified question pack and offline fallback |  |
| T-077 | M1-P5 | Critical | 6 | Not Started | Worker 2 | One Playwright journey covering all six beats |  |
| T-078 | M1-P5 | Very Important | 4 | Not Started | Worker 2 | Execute visual regression and accessibility on the presented routes |  |
| T-079 | M1-P5 | Very Important | 3 | Not Started | Worker 2 | Failure injection suite |  |
| T-080 | M1-P5 | Critical | 4 | Not Started | Worker 2 | Capture the Customer Contract Continuity snapshots |  |
| T-081 | M1-P5 | Critical | 6 | Not Started | Worker 2 | Write the screen-by-screen demonstration script |  |
| T-082 | M1-P5 | Very Important | 4 | Not Started | Worker 2 | Presentation environment preparation and clean-start verification |  |
| T-083 | M1-P5 | Critical | 6 | Not Started | Worker 2 | Three rehearsals, hostile hands and the fallback package |  |
| T-084 | M2a-P1 | Critical | 10 | Not Started | Worker 1 | Emit the frozen Fleet v2 into native customer-source fixtures |  |
| T-085 | M2a-P1 | Critical | 10 | Not Started | Worker 1 | Clean-room rebuild of the Fleet v2 emulator sources from source control |  |
| T-086 | M2a-P1 | Critical | 8 | Not Started | Worker 1 | Freeze and certify Fleet v2 as the M2 reference validation dataset |  |
| T-087 | M2a-P1 | Critical | 12 | Not Started | Worker 1 | Physical three-schema migration |  |
| T-088 | M2a-P1 | Very Important | 12 | Not Started | Worker 1 | Canonical migration order and legacy script archival |  |
| T-089 | M2a-P1 | Critical | 12 | Not Started | Worker 1 | definition_store, definition_versions and definition_dependencies |  |
| T-090 | M2a-P1 | Critical | 12 | Not Started | Worker 1 | Move all five definition kinds onto the store |  |
| T-091 | M2a-P1 | Very Important | 12 | Not Started | Worker 1 | Impact preview, export and import |  |
| T-092 | M2a-P1 | Critical | 12 | Not Started | Worker 1 | Registry authority: dimensions and measures as rows |  |
| T-093 | M2a-P1 | Very Important | 6 | Not Started | Worker 1 | Plant-vocabulary sweep, part 1: build the term list and the architecture test |  |
| T-094 | M2a-P1 | Very Important | 8 | Not Started | Worker 1 | Plant-vocabulary sweep, part 2: clear the violations and rename the canonical grain |  |
| T-095 | M2a-P2 | Critical | 12 | Not Started | Worker 1 | Relationship members, cardinality, grain conversion and preferred paths |  |
| T-096 | M2a-P2 | Critical | 8 | Not Started | Worker 1 | Path resolver, part 1: resolver core and the first eight consumers |  |
| T-097 | M2a-P2 | Critical | 6 | Not Started | Worker 1 | Path resolver, part 2: the remaining eight consumers and the regression suite |  |
| T-098 | M2a-P2 | Very Important | 12 | Not Started | Worker 2 | Relationship Browser page and path evidence |  |
| T-099 | M2a-P2 | Critical | 8 | Not Started | Worker 1 | Quarantine, part 1: the table, the reprocess API and the first eight PV classes |  |
| T-100 | M2a-P2 | Critical | 6 | Not Started | Worker 1 | Quarantine, part 2: the remaining seven PV classes and per-class tests |  |
| T-101 | M2a-P2 | Very Important | 12 | Not Started | Worker 2 | Quarantine retry, reprocess and Mapping Health completion |  |
| T-102 | M2a-P2 | Very Important | 8 | Not Started | Worker 1 | Identity resolution across sources |  |
| T-103 | M2a-P2 | Very Important | 6 | Not Started | Worker 1 | Genealogy bidirectional walk hardening and weight proof |  |
| T-104 | M2a-P2 | Critical | 8 | Not Started | Worker 1 | Projection through the versioned mapping, with version stamping |  |
| T-105 | M2a-P2 | Critical | 6 | Not Started | Worker 1 | Idempotent reprojection and mapping-version regression |  |
| T-106 | M2a-P3 | Critical | 12 | Not Started | Worker 3 | Job target version policy and dependency DAG |  |
| T-107 | M2a-P3 | Critical | 12 | Not Started | Worker 3 | Dual-predicate admission control and the three ML execution lanes |  |
| T-108 | M2a-P3 | Critical | 8 | Not Started | Worker 1 | stage_watermarks and delta-scoped projection |  |
| T-109 | M2a-P3 | Critical | 6 | Not Started | Worker 3 | Delta-scoped feature refresh and analysis, with telemetry and ML hand-off |  |
| T-110 | M2a-P3 | Critical | 12 | Not Started | Worker 1 | Chunk manifests, checkpoint, resume and deterministic merge |  |
| T-111 | M2a-P3 | Very Important | 12 | Not Started | Worker 1 | Scan budget and the Scan Amplification metric |  |
| T-112 | M2a-P3 | Critical | 12 | Not Started | Worker 1 | Force RLS on every tenant-owned table with an architecture test |  |
| T-113 | M2a-P3 | Very Important | 8 | Not Started | Worker 1 | Secret and configuration hygiene |  |
| T-114 | M2a-P3 | Very Important | 6 | Not Started | Worker 1 | Tenant keys, tenant-aware uniqueness and canonical namespace on new APIs |  |
| T-115 | M2a-P3 | Very Important | 4 | Not Started | Worker 1 | Fresh-install Rule 2 acceptance test, ephemeral |  |
| T-116 | M2a-P3 | Very Important | 8 | Not Started | Worker 1 | API namespace migration, part 1: map the 92 prefixes onto the 27 domains and stand up dual-serve |  |
| T-117 | M2a-P3 | Very Important | 6 | Not Started | Worker 1 | API namespace migration, part 2: migrate the clients and add the token gate |  |
| T-118 | M2a-P4 | Critical | 12 | Not Started | Worker 1 | J1 to J3 commissioning built for real |  |
| T-119 | M2a-P4 | Critical | 12 | Not Started | Worker 1 | Eight-role catalogue with three enforcement layers |  |
| T-120 | M2a-P4 | Very Important | 12 | Not Started | Worker 2 | Users and Roles administration surface |  |
| T-121 | M2a-P4 | Critical | 12 | Not Started | Worker 1 | Licence and entitlement enforcement |  |
| T-122 | M2a-P4 | Critical | 12 | Not Started | Worker 1 | Container architecture and configuration profiles, including isolated ML runtimes |  |
| T-123 | M2a-P4 | Critical | 12 | Not Started | Worker 1 | Install package, migration runner, upgrade and rollback |  |
| T-124 | M2a-P4 | Critical | 12 | Not Started | Worker 1 | Backup with a tested restore acceptance procedure |  |
| T-125 | M2a-P4 | Very Important | 12 | Not Started | Worker 1 | Minimum monitoring, health and alerting |  |
| T-126 | M2a-P4 | Very Important | 12 | Not Started | Worker 1 | Support runbook and UAT dataset and configuration import |  |
| T-127 | M2a-P4 | Critical | 12 | Not Started | Worker 2 | Canonical journey regression and the Continuity comparison |  |
| T-128 | M2b-P1 | Critical | 10 | Not Started | Worker 3 | Feature store, outcome store and immutable snapshot metadata |  |
| T-129 | M2b-P1 | Critical | 12 | Not Started | Worker 3 | Compute runs, findings and common evidence persistence |  |
| T-130 | M2b-P1 | Critical | 12 | Not Started | Worker 3 | Model registry, serving identity, activation and fallback |  |
| T-131 | M2b-P1 | Critical | 12 | Not Started | Worker 3 | Practice signature, windowing, context and cohorts |  |
| T-132 | M2b-P1 | Critical | 12 | Not Started | Worker 3 | Support, confidence, back-off ladder and tolerance sensitivity |  |
| T-133 | M2b-P2 | Very Important | 12 | Not Started | Worker 3 | Practice persistence, drift and canonical D10 Practice Insights |  |
| T-134 | M2b-P2 | Critical | 12 | Not Started | Worker 3 | Bindable intelligence registry and evidence handles |  |
| T-135 | M2b-P1 | Very Important | 10 | Not Started | Worker 3 | Tenant-aware uniqueness across the intelligence tables |  |
| T-136 | M2b-P2 | Important | 10 | Not Started | Worker 3 | Incremental practice recomputation |  |
| T-137 | M2b-P0B | Very Important | 10 | Done | CLOSED | ModelServingRuntime and model-gateway adapter, isolated from the presentation dock |  |
| T-138 | M2b-P4 | Critical | 12 | Not Started | Worker 3 | Canonical Assistant runtime cutover: planner, retrieval, evidence packing, serving and verifier |  |
| T-139 | M2b-P3 | Critical | 12 | Not Started | Worker 3 | prediction_runs, predictions and prediction_current |  |
| T-140 | M2b-P3 | Critical | 12 | Not Started | Worker 3 | Prediction drivers and comparables, persisted |  |
| T-141 | M2b-P3 | Very Important | 12 | Not Started | Worker 3 | Actionable deadline and latency health |  |
| T-142 | M2b-P3 | Critical | 8 | Not Started | Worker 3 | Remediation candidate generation from the customer's own history |  |
| T-143 | M2b-P3 | Critical | 8 | Not Started | Worker 3 | Integrate the nine-check remediation gate, can_accept and suppression |  |
| T-144 | M2b-P3 | Critical | 8 | Not Started | Worker 3 | Accept, Reject and Defer with action recording |  |
| T-145 | M2b-P3 | Critical | 8 | Not Started | Worker 3 | Outcome capture, evaluation and escalation |  |
| T-146 | M2b-P4 | Very Important | 10 | Not Started | Worker 3 | Converge the production statistical engine onto the certified method kernel |  |
| T-147 | M2b-P4 | Critical | 12 | Not Started | Worker 3 | Fix the outcome namespace, grain assignment and ordinal loader |  |
| T-148 | M2b-P4 | Very Important | 8 | Not Started | Worker 2 | Map the 108 page files onto the 40 target pages |  |
| T-149 | M2b-P4 | Very Important | 6 | Not Started | Worker 2 | Delete the legacy redirects and re-verify continuity |  |
| T-150 | M2b-P4 | Critical | 12 | Not Started | Worker 1 | Complete the test gates |  |
| T-151 | M3-P1 | Critical | 12 | Not Started | Worker 1 | Site defect burn-down |  |
| T-152 | M3-P1 | Critical | 12 | Not Started | Worker 1 | Customer data edge cases |  |
| T-153 | M3-P1 | Critical | 12 | Not Started | Worker 1 | Connector certification against real sources |  |
| T-154 | M3-P1 | Very Important | 12 | Not Started | Worker 1 | Query plans, indexes and partition boundaries |  |
| T-155 | M3-P1 | Very Important | 12 | Not Started | Worker 3 | B-01/B-02 capacity tuning, scan amplification and model-serving memory |  |
| T-156 | M3-P1 | Very Important | 12 | Not Started | Worker 2 | Customer definitions built through the product |  |
| T-157 | M3-P1 | Very Important | 12 | Not Started | Worker 3 | Practice, prediction and model calibration on real data |  |
| T-158 | M3-P1 | Very Important | 12 | Not Started | Worker 3 | Remediation validation against real process constraints |  |
| T-159 | M3-P2 | Critical | 12 | Not Started | Worker 3 | C1 to C4 capacity certification |  |
| T-160 | M3-P2 | Critical | 12 | Not Started | Worker 1 | HA, DR and restore rehearsal |  |
| T-161 | M3-P2 | Very Important | 12 | Not Started | Worker 1 | SSO and identity integration |  |
| T-162 | M3-P2 | Critical | 12 | Not Started | Worker 1 | Site security hardening and sign-off |  |
| T-163 | M3-P2 | Very Important | 12 | Not Started | Worker 1 | Monitoring, SLOs and support escalation |  |
| T-164 | M3-P2 | Critical | 12 | Not Started | Worker 1 | The Value Engine |  |
| T-165 | M3-P2 | Very Important | 12 | Not Started | Worker 1 | Commercial capacity finalisation and the sales calculator |  |
| T-166 | M3-P2 | Important | 12 | Not Started | Worker 2 | Five-product website production completion |  |
| T-167 | M3-P2 | Critical | 12 | Not Started | Worker 2 | Documentation, training and production acceptance |  |
| T-168 | M2b-P0A | Critical | 12 | Done | CLOSED | Versioned C#↔Python ML job protocol and isolated runtime harness |  |
| T-169 | M2b-P0A | Critical | 10 | Done | CLOSED | Typed columnar training-artifact library and B-03 harness |  |
| T-170 | M2b-P0A | Critical | 10 | Done | CLOSED | Chunked sequence-artifact library and bounded loader |  |
| T-171 | M2b-P0A | Critical | 10 | Done | CLOSED | Capability Profiler and eligibility/refusal kernel |  |
| T-172 | M2b-P0A | Very Important | 12 | Done | CLOSED | MF-01 Process Encoder runtime behind a replaceable contract |  |
| T-173 | M2b-P0A | Critical | 12 | Done | CLOSED | MF-02 VectorSimilarityIndex with exact-Flat recall baseline |  |
| T-174 | M2b-P0A | Very Important | 10 | Done | CLOSED | MF-03 novelty-model runtime and honest refusal semantics |  |
| T-175 | M2b-P0B | Critical | 12 | Done | CLOSED | MF-04 supervised-outcome training runtime and mandatory simple baseline |  |
| T-176 | M2b-P0B | Critical | 12 | Done | CLOSED | Calibration, explanation stability and three-dimensional promotion kernel |  |
| T-177 | M2b-P0B | Critical | 10 | Done | CLOSED | Production statistical-method kernel, including Numeric×Categorical |  |
| T-178 | M2b-P0B | Critical | 8 | Done | CLOSED | Pure remediation eligibility and can_accept decision kernel |  |
| T-179 | M2b-P0B | Critical | 10 | Done | CLOSED | Deterministic Assistant tool planner |  |
| T-180 | M2b-P0B | Critical | 12 | Done | CLOSED | Permission-first hybrid retrieval and evidence packer |  |
| T-181 | M2b-P0B | Critical | 12 | Done | CLOSED | Deterministic answer verifier and Q-01..Q-11 evaluation harness |  |
| T-182 | M2b-P0A | Very Important | 10 | Done | CLOSED | Benchmark harness and result manifest for B-01..B-09 |  |
| T-183 | M2b-P1 | Critical | 12 | Not Started | Worker 3 | Semantic Contract Manifest persistence, resolver and G-55 coverage |  |
| T-184 | M2b-P1 | Critical | 12 | Not Started | Worker 3 | Snapshot Materialiser: seal feature state into typed artifacts and enforce G-48 |  |
| T-185 | M2b-P2 | Critical | 12 | Not Started | Worker 3 | sequence_manifests persistence and object-storage sequence path |  |
| T-186 | M2b-P2 | Critical | 12 | Not Started | Worker 3 | Persist capability profiles, prediction points and the model-count governor |  |
| T-187 | M2b-P2 | Critical | 12 | Not Started | Worker 3 | Production training/index integration: snapshots → ML lanes → registry activation |  |
| T-188 | M2b-P2 | Critical | 10 | Not Started | Worker 3 | Canonical D4 Findings and D8 ML Readiness/Models cutover |  |
| T-189 | M2b-P3 | Critical | 12 | Not Started | Worker 3 | Canonical D5 Risk and D9 Early Warning cutover |  |
| T-190 | M2b-P3 | Critical | 12 | Not Started | Worker 3 | Canonical D6 Suggestions and decision-action cutover |  |
| T-191 | M2b-P3 | Very Important | 12 | Not Started | Worker 3 | D11 Scenario Simulation: governed read-only modelled comparison |  |
| T-192 | M2b-P4 | Critical | 10 | Not Started | Worker 3 | Target-architecture gate pack G-48..G-55 |  |
| T-193 | M2b-P4 | Very Important | 10 | Not Started | Worker 3 | Drift Supervisor and governed model-action proposals |  |
| T-194 | M2a-P4 | Very Important | 6 | Not Started | Worker 1 | Retire legacy API dual-serve aliases after canonical-client continuity proof |  |
| T-195 | M2b-P1 | Critical | 10 | Not Started | Worker 3 | Canonical readiness thresholds, governed change history and fail-closed readiness API |  |
| T-196 | M2a-P2 | Very Important | 8 | Not Started | Worker 2 | C4 Plant Model Explorer canonical route and structural evidence surface |  |
| T-197 | M2a-P4 | Very Important | 10 | Not Started | Worker 2 | E6 Alert Routing and Escalation with delivery, retry and dead-letter evidence |  |
| T-198 | M2a-P4 | Critical | 12 | Not Started | Worker 2 | F5 Logging and Audit plus F6 Log Channel Configuration |  |
| T-199 | M2a-P4 | Critical | 12 | Not Started | Worker 2 | F9 Log Retention and Archival with dry-run, legal hold and no-delete-on-archive-failure |  |
| T-200 | M2a-P4 | Very Important | 10 | Not Started | Worker 2 | F7 System Settings and F8 Translation/Language canonical administration |  |

---

# 19. SOURCE / EVIDENCE MAP FOR THE NEXT SESSION

## 19.1 Latest backlog / architecture
- `PPIQ_Backlog_v2_10_4_16Aug2026_Three_AI_Agent_Orchestration.xlsx`
- `PPIQ_Chapter1_Marketing_and_Sales(2).md`
- `PPIQ_Chapter2_Technical_Overview(2).md`
- `PPIQ_Chapter3_General_Technical_Function_Description(2).md`
- `PPIQ_Chapter4_Specific_Technical_Function_Description(2).md`
- `PPIQ_Chapter5_Tutorial_User_Journey(2).md`
- `PPIQ_Chapter6_Infrastructure_Website_Administration(2).md`
- `PPIQ_Layer_B_Architecture_Design_Pack(10).md`
- `PPIQ_Layer_B_Design_Pack_Batch_Mode_Order(1).md`

## 19.2 Latest implementation audit snapshot
- `00_Master_Index_16Aug2026_123644.txt`
- `01_Backend_Core_16Aug2026_123644.txt`
- `02_Backend_Database_16Aug2026_123644.txt`
- `03_Backend_Tests_16Aug2026_123644.txt`
- `03A_ML_Runtime_16Aug2026_123644.txt`
- `04_Frontend_App_16Aug2026_123644.txt`
- `05_Frontend_Misc_16Aug2026_123644.txt`
- `manifest_16Aug2026_123644.csv`
- `manifest_16Aug2026_123644.json`

The developer-uploaded 11-Aug audit remains useful for historical infrastructure/current Jenkinsfile snapshots:
- `06_Infrastructure_11Aug2026_232203(1).txt`
- `07_Tools_Validation_Misc_11Aug2026_232203(1).txt`
- `08_Website_11Aug2026_232203(1).txt`
- `10_Audit_Signals_11Aug2026_232203(1).txt`

## 19.3 Raw recent console evidence
- `Pasted text(20260817-111244).txt` — latest Worker3 T-064 v3 run through Step 11 failure.
- `Pasted markdown(20260817-074040).md` — Worker2 session handover around T049/T050/T051.
- `Pasted text(20260816-063836).txt` — T047 final commit.
- other `Pasted text/markdown(20260815-...)` and `(20260816-...)` files preserve exact output.

## 19.4 T-064 corrective scripts retained as troubleshooting references
- `Fix-T064-EfMigrationParity-TECHLEAD.ps1`
- `Fix-T064-EfMigrationParity-TECHLEAD-v2.ps1`
- `Fix-T064-EFParity-And-Convergence-FINAL.ps1`
- `Fix-T064-EFParity-And-Convergence-FINAL-v2.ps1`

The final successful corrective logic is represented by the `FINAL-v2` lineage and commit `b811696b`.

## 19.5 Historical proven T044 migration runner
- `Fix-T044R1-EfMigrationParity-CERTIFIED.ps1`

Important proven technique inherited:
robust native command argument construction; keep SQL as one native argument or use safe file/createdb/dropdb execution; fresh-DB chain before baselining.

---

# 20. SOU COMPANY PROFILE SIDE-WORK COMPLETED IN THIS SESSION

Latest reviewed:
- `SOU_Industrial_Software_Company_Profile (6).docx`
- `SOU_Industrial_Software_Company_Profile (4).pdf`

Current judgment:
- advanced professional CEO-facing industrial company profile;
- ~9.3/10 current positioning;
- 19 pages;
- now frames SOU positively rather than defensively as a startup.

Strong cover/positioning:
- 14 years industrial engineering experience;
- 5 specialised industrial software products;
- 8 industrial companies' real production data;
- Düsseldorf + Alexandria operations being established.

Approved narrative:
- do **not** hide startup status;
- do not make startup status the document's problem;
- sell founder experience, five-product portfolio, industrial validation, engineering discipline and low-risk pilot;
- do not claim “100% mature”;
- use capability/production-readiness language instead.

Recommended final polish:
1. small Founder/Leadership block;
2. concise Mission/Vision;
3. legal/company facts when registration completes;
4. optional one-page visual portfolio stack;
5. replace “in days” change-request wording with a non-SLA executive formulation;
6. fill customer/date/contact placeholders before sending.

Latest strong phrases:
- “The in-house expert that learns your plant's own fingerprint — and stays.”
- “with every governed figure backed by resolvable evidence and provenance.”
- “This architecture sharply reduces the control-system risk surface and simplifies OT security approval.”

---

# 21. EXACT NEXT SESSION OPENING MESSAGE

Recommended first user message in the new session:

```text
RESUME-PPIQ-17AUG-1425.

Read the attached 17-Aug deep handover first. Do not re-investigate or rerun proven tests just to reconstruct state.

Act as central Tech Lead / Product Owner / PM.

Current shared HEAD: 964608045942527c281ae32a05484d64ffaf8103.

Worker 1: T-057 implementation/build/tests/827 are green and uncommitted. Run the missing runtime publish/read-back certification, then exact-stage/commit/close T-057 and start T-058.

Worker 2: T-050 Step 1 + 2a are already applied/uncommitted and green. Fresh session starts with Step 2b CODE, not analysis: drawer population + same-render request snapshot + one opt-in evidence execution + T-073 resolver + stale-context race test, then final gates/commit/close and start T-052.

Worker 3: T-064 target_parameters candidate semantics/tests/migration were green through Step 10. Step 11 failed only because the pack treated 824 operational indexes as illegal replay delta. Before rerun inspect possible failed-v3 target_parameters residue in ppiq_presentation. Then fix the replay gate, reproduce the candidate, certify, commit, close T-064.

Serialize DB/API runtime certification; do not let W1/W3 mutate ppiq_presentation simultaneously.
```

---

# 22. FINAL HANDOFF — ONE PARAGRAPH

PlantProcess IQ ends 17-Aug-2026 with the presentation/BI contract materially stronger than the 15-Aug handover: T-047, T-048, T-049 and T-051 are closed; PR-050-01 governed WidgetResult evidence is closed at `96460804`; T-064's core EF/runtime parity is repaired and committed at `b811696b`; Worker 2 has already proven authoritative backend row identity and mirrored the evidence contract for T-050 but still must wire the drawer/evidence re-execution Step 2b; Worker 1 has a fully built/tested/replayable T-057 relationship compatibility slice on disk but still owes the runtime publish/read-back proof and commit; Worker 3 has proven the missing target_parameters semantics, JSON/null/history invariants, migration shape, zero pending EF changes and regression health, but its latest candidate auto-reverted because the replay gate incorrectly rejected two immutable-824 operational indexes, and the next session must first verify whether failed-v3 left target_parameters columns behind in `ppiq_presentation`. Preserve SQL 824/825 immutability, W3 reservation 826 and W1 relationship 827, exact-file Git ownership, honest refusal/evidence semantics, the M1-vs-M2 two-track contract, and the historical server/pipeline truth distinction. Start from these three active tasks; do not start from greenfield.

---

# APPENDIX A — PREVIOUS 15-AUG DEEP HANDOVER PRESERVED VERBATIM

The following prior handover is intentionally embedded so that no older troubleshooting, ML/AI evidence, pipeline history, task ledger or management ruling disappears merely because this continuation handover focuses on 17-Aug deltas.

---

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

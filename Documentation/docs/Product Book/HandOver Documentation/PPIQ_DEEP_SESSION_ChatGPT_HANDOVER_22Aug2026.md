# PlantProcess IQ — Deep Technical / Product / Programme Handover
## 22-Aug-2026 — Emergency BI Stabilisation State (R5 prepared, NOT YET EXECUTED)

**Repository:** `C:\Workspace\PlantProcess-IQ`  
**Current proven HEAD at the latest emergency preflight:** `1d27d1997801ffa18cacfafcfef115302c0cf748`  
**Branch:** `main`  
**Authoritative backlog:** `Documentation/docs/Product Book/PPIQ_Backlog_v2_10_4_16Aug2026_Three_AI_Agent_Orchestration.xlsx`  
**Current local main DB topology:** native Windows PostgreSQL 16 on `127.0.0.1:5432`  
**Development DB:** `ppiq_app`  
**Presentation / populated certification DB:** `ppiq_presentation`  
**API:** `http://localhost:5063`  
**Frontend:** `http://localhost:5173`  
**Current emergency repair pack:** `Apply-PPIQ-CustomerDemo-Emergency-R5.ps1`  
**R5 SHA256:** `10185F871843BE263573ED4ABA196ADD46D6E91D4C7CCDE7BF62351127FA5BFA`

> **Critical state boundary:** This handover is written **after R5 was generated and hash-verified, but before R5 was executed**. R1, R3 and R4 were executed and stopped safely at guarded failures; R2 was superseded before final execution. Do not assume R5 is green. The very first continuation action is to execute/finish R5 or inspect its output if the user already ran it after this handover was created.

> **Security:** no real passwords, private keys, access tokens, signing keys or protected runtime secrets are copied here. Use protected environment/profile files. Do not paste bearer tokens from browser DevTools into chat or source control.

---

# 0. ABSOLUTE RESUME POINT — READ THIS FIRST

The project is **not in normal backlog execution mode**. The user explicitly declared an **emergency** after browser inspection showed that the BI layer was not customer-demo ready despite many unit/integration/E2E gates being green.

The management ruling is:

```text
ALL NORMAL WORKER LANES STOPPED.

ONE emergency owner only may touch Frontend + Backend + presentation dashboard data
until the customer-facing BI workspace is stable and browser-certified.

Do NOT resume:
- Worker 1 T-060
- Worker 3 T-065 consumer
- T-066
- T-067
- marketing/commercial polish
- unrelated M2 work

until the emergency BI release gate is closed.
```

The current emergency implementation state is:

```text
HEAD                     = 1d27d1997801ffa18cacfafcfef115302c0cf748
T-065 backend producer   = committed and green
T-068                    = closed and green
BI browser acceptance    = RED
Emergency R3 Frontend    = partially applied to worktree, uncommitted
Emergency R4             = stopped in Section 5 before DB calibration
Emergency R5             = prepared, hash 10185F..., NOT YET RUN
Current worktree         = intentionally dirty; DO NOT CLEAN
```

## 0.1 The exact first question/action in the next session

First ask/establish only this:

```text
Did R5 run after this handover was created?

If NO:
  run R5 exactly once and inspect the first failing section or final replay.

If YES:
  do NOT rerun it.
  read the R5 console output / REPORT.txt / population-calibration.txt /
  saved-widget-replay.txt and continue from the first remaining red proof.
```

Do **not** begin with a generic repo audit, full test run, backlog re-read, or fresh browser investigation. Those activities have already consumed a large amount of time and are precisely what this handover is intended to prevent.

---

# 1. EXECUTIVE STATE SUMMARY

PlantProcess IQ is architecturally much stronger than it was at the beginning of the August M1 work. The deterministic analytics/evidence contracts, governed target/version semantics, registry-driven analysis options, relationship resolver, presentation data recertification, chart grammar, provenance, and major M1 plumbing have all moved substantially forward and many are formally closed.

However, customer-browser validation on 19–20 Aug exposed a different class of release problem:

1. **Persisted BI widget contracts no longer all match the current backend dimension/measure/source capability model.**
2. **Some valid aggregate widgets are still affected by completeness/raw-population guards.**
3. **The associative view was issuing broad requests using one generic measure (`observationCount`) for dimensions that do not support that measure.**
4. **Persisted dashboard layouts had ghost/default IDs and degenerate `1x1` geometry, causing pill-like, overlapping, hanging, or near-invisible widgets.**
5. **Browser presentation quality had defects that unit/integration tests did not certify: huge associative area, raw GUID-like values, authoring chrome, ugly chart focus/selection frame, overlapping cards, error toasts.**
6. **The global frontend test suite itself has a known hang/non-termination problem, so “test green” and “customer demo green” are not equivalent.**

This is why the emergency track exists. It is a release-correction track, not a redesign of the whole platform.

---

# 2. PRODUCT IDENTITY — DO NOT LOSE THIS IN EMERGENCY FIXES

## 2.1 Company / portfolio identity

SOU Industrial Software has **five independently useful/sold industrial products**:

1. **PlantProcess IQ** — Plant intelligence — flagship.
2. **MES** — Plant execution.
3. **QES** — Quality execution.
4. **Yard and Warehouse Management** — Material flow.
5. **Energy Management System** — Resource efficiency.

PlantProcess IQ is **not** a parent/container that turns the other four into modules. They are sibling products.

Commercial identity currently used:

- Company: **SOU Industrial Software**
- Flagship: **PlantProcess IQ**
- Engineering experience: **14 years**
- Evidence/data story: **real production data from 8 industrial companies** in the presentation/demo material
- Locations: **Düsseldorf, Germany · Alexandria, Egypt**
- Website: `souindustrial.com`
- Founder title: **Founder and Chief Product Architect**
- Public email selected: `info@souindustrial.com`
- Tagline: **“Connect your plant data. Understand your process.”**
- Strong positioning line: **“The in-house expert that learns your plant's own fingerprint — and stays.”**

## 2.2 PlantProcess IQ product definition

PlantProcess IQ is a **generic, cross-industry manufacturing BI + deterministic analytics + governed intelligence platform**.

The real product must not become a steel demo application.

Steel/fleet-v2/current demo vocabulary is presentation/reference content, not product authority.

Core architectural separation:

```text
Layer A = exact / deterministic facts
Layer B = statistical / learned intelligence
LLM     = explanation, orchestration, retrieval and citation
```

An ML or LLM layer must never approximate an exact plant fact that Layer A can calculate deterministically.

Assistant principle:

```text
engines calculate
    ↓
governed evidence exists
    ↓
tenant/permission-aware retrieval
    ↓
LLM explains and cites
    ↓
deterministic verification
```

The LLM may not invent figures, erase a governed refusal, or upgrade association to causality.

PlantProcess IQ customer-source posture is **read-only by default**. It does not silently write back to PLC/MES/customer sources and does not become autonomous plant control.

---

# 3. IDENTITY / TOPOLOGY / TWO-TRACK ROADMAP

## 3.1 Local laptop topology

```text
Repository              C:\Workspace\PlantProcess-IQ
Main PostgreSQL         native PostgreSQL 16, 127.0.0.1:5432
Development DB          ppiq_app
Presentation DB         ppiq_presentation
API                     http://localhost:5063
Frontend                http://localhost:5173
Demo source emulators   may be Docker containers
Main laptop DB          NOT Docker
```

Important test rule discovered earlier:

```text
generic integration correctness
    -> ppiq_app is a valid default

populated M1 presentation certification
    -> explicitly target ppiq_presentation
       through PPIQ_TEST_CONNECTION_STRING or the relevant profile
```

Do not globally change the integration resolver just to make presentation tests convenient.

## 3.2 Server topology — historical truth, not automatic current certification

Historically used URLs:

```text
Host      178.105.152.180
App       https://app.178.105.152.180.sslip.io
API       https://api.178.105.152.180.sslip.io
Website   https://website.178.105.152.180.sslip.io
Jenkins   https://jenkins.178.105.152.180.sslip.io
```

Long-lived infra compose project: `plantprocessiq`  
Application compose project: `ppiq-app`

Canonical deployment principles:

- Caddy owns public ports `80/443`.
- API/web/PostgreSQL are not directly exposed publicly.
- Server main PostgreSQL is Dockerized.
- Customer topology is not hardcoded: managed DB, VM DB, Docker DB, Kubernetes service, etc. remain possible.
- Real runtime secrets are private environment files.
- Never claim the historical URLs are currently production-certified without rechecking them.

## 3.3 Two-track architecture/roadmap ruling

After M1, maintain two distinct concepts:

### Track 1 — Presentation / demo baseline
A stable customer-facing presentation baseline with populated, credible data and a polished journey.

### Track 2 — Real generic customer product
Continue on `main` toward canonical production architecture and a generic cross-industry BI contract.

The agreed intent was to preserve a stable Presentation branch/baseline after M1 rather than freezing future generic work into demo-specific assumptions.

Before expanding M2a with ad-hoc tasks, freeze the **Generic BI Product Contract** and classify requirements as:

- already implemented;
- implemented but acceptance weak;
- genuine scope expansion;
- genuine new task.

Do not create duplicate authorities simply because the presentation layer has a temporary compatibility store.

---

# 4. USER / TECH-LEAD OPERATING RULES THAT MUST SURVIVE THE NEW SESSION

The user expects ChatGPT to act as the **central Tech Lead + Product Owner + Product Manager** across workers.

These are hard rules, not style preferences:

## 4.1 Scope and worker control

- Every work package has one owner and one bounded objective.
- Prevent scope drift, task drift, same-file ownership overlap and “helpful” cross-worker contamination.
- Do not reopen a closed task without evidence of a regression.
- Do not pull later M2 work into an M1 fix.
- Avoid spending major time on presentation-only polish unless it is needed for acceptance/customer demonstration.
- When a worker finds unrelated debt, record it and continue unless it is a genuine prerequisite.
- Do not let ordinary build/test/tooling defects become multi-day design discussions.
- A worker should fix normal scripting/tooling mistakes autonomously instead of stopping for permission after every defect.

## 4.2 Emergency override

During the current emergency:

```text
normal 3-worker parallelism is suspended.
Only one repair lane should mutate BI Frontend/Backend/dashboard presentation state.
Other workers remain stopped.
```

This was introduced because parallel changes were making a fragile presentation worktree harder to certify.

## 4.3 Shared worktree Git safety

Never use:

```text
git clean -fd
git reset --hard
git restore .
git add .
git add -A
```

Exact staging only.

Before mutation/staging:

```powershell
git status --short
git diff --cached --name-status
git log --oneline -12
```

A non-empty index is a collision signal.

Do not “clean the tree” by wiping foreign work.

## 4.4 Build/runtime serialization

Full Backend builds share `bin/obj`; serialize them.

`dotnet clean` is allowed only when no other Backend lane is active and only when required.

Database/API runtime certification must also be serialized when two tasks could mutate `ppiq_presentation`.

## 4.5 Validation philosophy

A build is not task completion.

A task is closed only when:

```text
scope implemented
+ prerequisites respected
+ acceptance proof non-vacuous
+ relevant regression green
+ exact ownership/staging correct
```

A skipped test is not a passing test.

A test that only proves “every input ends in some state” is not a correctness proof.

Browser visual/customer acceptance is separate from unit/integration correctness.

---

# 5. AUTHORITATIVE BACKLOG / ROADMAP STATE

Current backlog authority is:

`PPIQ_Backlog_v2_10_4_16Aug2026_Three_AI_Agent_Orchestration.xlsx`

At the 17-Aug handover the workbook contained approximately:

```text
200 tasks
1806 total planned hours
71 Done
1 In Progress
128 Not Started
```

Those static workbook status cells became stale as tasks closed on 17–19 Aug. **The live evidence/commit ledger in this handover overrides stale workbook status cells.**

Important dependency corrections frozen in v2.10.x:

- `T-068 -> T-065` so authoring consumes a registry-driven outcome/grain contract from day one.
- T-064 provides the M1 semantic target/version contract; the final physical FK/dependency DAG belongs to T-106 after canonical definition authority exists.
- T-066 must consume the stable readiness facade, with canonical threshold persistence later (T-195).
- T-067 waits for provenance + T-065 + T-066.
- T-098 must complete the existing Relationship Browser in place, not create a second C6 page.

---

# 6. MAJOR TASK LEDGER — CURRENT LIVE STATUS

This section is the current working truth, not merely the static backlog wording.

## 6.1 Presentation/BI chain already closed

| Task | Current state | Key evidence |
|---|---|---|
| T-045-R1 | CLOSED | R1-A Readiness `c56008c0`; R1-B canonical correlation `dd9a6b04`; R1-C Risk `39ce59ef`; R1-D Equipment `283aae2c` |
| T-044-R1 | major corrective applied | source/join/positional truth restored; guard v2 prevented parameter-definition mutation |
| T-046 | closed base packs | semantic grammar + metadata surface; 82/86-test-era packs; no hardcoded grain authority intended |
| T-047 | CLOSED | final page binding/visual contract, earlier commit `4b431463...` |
| T-048 | CLOSED | associative contract, earlier commit `b687cba4...` |
| T-049 | CLOSED | `cc4d884...` |
| T-050 | CLOSED | provenance/evidence closure `4fdd311...` |
| T-051 | CLOSED | `6c99e091...` |
| T-052 | CLOSED | final `24868e3944e48dea99ac5501345b570972200fa3`; an earlier corrective `228adbda...` removed hardcoded `CastingSpeed` default |
| T-057 | CLOSED | `47ee7075...` |
| T-058 | CLOSED | `c3573ae7115788f1b0015c958d50d44d6977f89a` |
| T-064 | FORMALLY CLOSED | `02518934f19ca1a59d5aa61fa755f431a7e9048d` |
| T-068 | FORMALLY CLOSED | base `3d8c706f...`, corrective `d4af982ebaf55f2db0b7951ea81ec4681be1c0b6` |

## 6.2 T-060 Relationship Browser

**State:** implementation pack v4 ready but not certified/landed in the emergency worktree.

Important history:

- v4 hash: `F3875B6491FDD81C3802EC0087A34C8CE66465C734BB8A459CFB00A1289B22FB`.
- Source/self-check design had converged.
- Prior production build failure was caused by Worker 3-owned `advancedAnalysis.ts` TS7006 errors, not Relationship Browser code.
- Those TS7006 errors were later repaired under T-068.
- Do not create v5 just because time passed.
- Do not run T-060 during the current emergency.
- When emergency closes, re-evaluate current source anchors and apply/certify v4 intent without redesign.

T-098 later owns canonical Relationship Browser hidden-authority/path-evidence convergence, not a second page.

## 6.3 T-064 governed job target/version contract

**FORMALLY CLOSED.**

Semantic contract:

```text
target_definition_kind
target_definition_id
target_definition_version
target_version_policy
target_parameters
executed target definition/version history
JB01-JB04 governed refusal/retirement semantics
```

Important architecture ruling:

- No fake FK in M1.
- Physical canonical FK/check/trigger/DAG convergence belongs to T-106 after T-089/T-090.
- `current_published` and `pinned` are stored vocabulary.
- Null parameters and `{}` are different and must round-trip differently.
- Unknown target/job class must be refused, never silently mapped to `Custom`.

## 6.4 T-068 registry-driven Analysis Toolbox

**CLOSED** at corrective commit:

`d4af982ebaf55f2db0b7951ea81ec4681be1c0b6`

What became true:

```text
options route         /api/analysis-jobs/definition-options
engineOutcomes        normalized at adapter boundary
retired client        src/api/mlFoundation.ts deleted
consumer path         no /ml/foundation outcome route
physical table        not known by React consumer
no hardcoded outcome catalogue
no grain catalogue
no "coil" fallback
missing grain         remains missing
empty/error registry  disables run honestly
```

Acceptance:

```text
typecheck             clean
focused T-068         16/16
production build      clean (~16s)
relevant tests        35/35
introduced failures   0
carried failures      0 in targeted set
```

Global full frontend suite was **not executable** in that session because a clean baseline ran hundreds of tests and failed to terminate at the 300-second ceiling without producing a report. This is test-infrastructure debt, not a T-068 failure.

## 6.5 T-065 — distinguish producer bridge from full visible consumer task

This distinction is essential.

### Backend producer bridge — COMPLETE and COMMITTED

Commit:

`1d27d1997801ffa18cacfafcfef115302c0cf748`

Commit message:

`T-065 bridge analysis jobs to governed target contract`

Files: 12  
Diff: `2175 insertions, 43 deletions`

Key files:

```text
Backend/database/scripts/828_t065_analysis_job_target_compatibility.sql
Backend/PlantProcess.Application/Jobs/Targeting/AnalysisJobClass.cs
Backend/PlantProcess.Application/Jobs/Targeting/JobTargetVersionPolicyCodec.cs
Backend/PlantProcess.Application/DependencyInjection.cs
Backend/PlantProcess.Infrastructure/DependencyInjection.cs
Backend/PlantProcess.Infrastructure/Jobs/AnalysisAwareJobTargetLookup.cs
Backend/PlantProcess.Infrastructure/Persistence/Configurations/Integration/JobDefinitionConfiguration.cs
Backend/PlantProcess.Api/Endpoints/Analytics/AnalysisJobDefinitionEndpoints.cs
Backend/tests/PlantProcess.Application.UnitTests/Jobs/T065AnalysisJobClassTests.cs
Backend/tests/PlantProcess.Application.UnitTests/Jobs/T065JobTargetVersionPolicyCodecTests.cs
Backend/tests/PlantProcess.Api.IntegrationTests/Jobs/T065AnalysisJobTargetBridgeTests.cs
Backend/tests/PlantProcess.Api.IntegrationTests/Jobs/T065AnalysisJobTargetApiTests.cs
```

Final producer acceptance:

```text
Application unit       731 passed, 0 introduced failures
Architecture           181 passed
Pack A integration     9/9, 0 skipped
Pack B integration     21/21, 0 skipped
```

Important implementation truths:

- migration 828 adds five nullable TEMP-ADAPTER target columns to `inspection_jobs`;
- zero fake FK;
- database coherence constraints mirror T-064 rules;
- `AnalysisJobClass` maps exact catalogue `job_type`;
- unknown/unmapped does not silently become `Custom`;
- `JobTargetVersionPolicyCodec` gives one explicit vocabulary authority;
- `AnalysisAwareJobTargetLookup` preserves JB04 by combining canonical job definitions and compatibility analysis jobs;
- target identity is independent per analysis job even when engine job code is shared;
- no target identity hidden in `rule_json`/population filters.

### Frontend T-065 consumer — NOT CLOSED

Worker 3 had read-only mapped the landing before emergency:

- `/analysis/toolbox` currently runs direct correlation and did not originally persist an Analysis Job.
- `/investigate/analysis-jobs` has the persisted create/update/run lifecycle.
- T-065 requires lifecycle transplant/convergence onto `/analysis/toolbox`, not just moving a selector.
- Code/name/defectType/parameter/outcome/engine job/target/version policy must be authored on D3.
- old `/investigate/analysis-jobs` presentation route should be retired from navigation.
- readiness/result status must show honest `Completed / Blocked / Failed`.

This consumer work was **paused by emergency**. Do not describe T-065 overall as fully closed merely because its backend producer commit exists.

## 6.6 T-066

**QUEUED / not started in current emergency timeline.**

Required visible contract:

- one readiness authority;
- same endpoint on Home + Analysis;
- five dimensions;
- measured value beside threshold;
- state + reason;
- no static/fake thresholds;
- compatibility facade during M1; canonical persisted thresholds later T-195.

## 6.7 T-067

**QUEUED / blocked.**

Depends on T-050 + full T-065 + T-066.

Required findings evidence:

- method;
- population;
- effect size;
- BH q-value;
- survived stratification;
- source-row path;
- order by absolute effect size;
- initial outcome/parameter registry-driven.

## 6.8 All normal lanes are currently paused

Emergency takes precedence over the sequence above.

---

# 7. M1 POPULATED DATA — PROVEN NON-VACUOUS STATE

The important M1 populated PostgreSQL recertification was green:

```text
17/17 PostgreSQL tests
5 suites
0 vacuous proofs
```

Key populated counts from the certification period:

```text
ParameterObservations   301,560
RiskScores                  500
DowntimeEvents              630
CrewSteps                 3,780
QualityEvents             7,844
GradedMaterials          35,915
OverlappingPairs            160
```

These counts prove the demo is not a tiny empty-fixture environment.

Important later browser observation:

```text
Parameter Deep Analysis "Observations" KPI showed 17,010
```

That is consistent with the scale of a single parameter slice (e.g. FDT_C) but the exact current DB count should be taken from R5's own query, not assumed from the UI screenshot.

Earlier source inventory also established substantial staged/source populations (example historical measurements):

```text
src_caster_oracle_shape.cast_pieces                5,670
src_caster_oracle_shape.cast_sequence                630
src_hsm_oracle_shape.hsm_coils                     5,670
src_hsm_oracle_shape.hsm_pass_measurements        39,690
src_inspection_mysql_shape.downtime_events           210
src_inspection_mysql_shape.parsytec_surface_defects 1,987 (later larger canonical defect population)
src_meltshop_pg.heats                                630
src_meltshop_pg.lf_treatment                         630
src_pkl_mssql_shape.pickle_orders                  5,670
src_pkl_mssql_shape.qa_lab_results                17,010
```

Do not fabricate values when source NULLs exist. Canonical materialisation must preserve source truth.

---

# 8. T-044 / T-045 IMPORTANT DATA-MODEL LEARNINGS

T-044-R1 source truth used:

```text
src_meltshop_pg.grade_specification
src_inspection_mysql_shape.parsytec_surface_defects
public.quality_events
public.parameter_definitions
```

Positional defect facts are three separate truths and must remain separate:

```text
start_m
end_m
width_position_mm
```

Validated join dependency:

```text
quality_events.source_record_id
    ↔
parsytec_surface_defects.defect_row_id
```

The guard had to be corrected because an early validator produced false positives.

Final guard rules included:

- forbid INSERT/UPDATE of `parameter_definitions`;
- do not fabricate `expected_min/expected_max`;
- map source chemistry names such as `Al -> ALUMINIUM_PCT` explicitly;
- precondition the six canonical parameters;
- prove `paramsAfter == paramsBefore`.

Pack evidence included:

```text
Baseline defects            5961
Canonical SurfaceDefect     5961
Quality events              7844
Specifications                36
Parameter definitions         48
Canonical parameters           6
Anchors                      8/8
```

General lesson: presentation fixes must not mutate canonical definition authority just to make a chart convenient.

---

# 9. RELATIONSHIP / JOB TARGET ARCHITECTURE LESSONS

## 9.1 Relationship resolver (T-058)

T-058 closed at `c3573ae7...`.

Runtime proofs covered:

- publish → resolve/plan;
- reverse traversal;
- ordered multi-hop;
- tenant isolation;
- preferred path;
- ambiguity refusal;
- no-path refusal;
- publish → unpublish → restore.

Known scope ruling:

- exploration may traverse an unproven relationship;
- automated consumers must refuse inappropriate unproven relationships;
- do not add a validate/promote lifecycle that M1 does not actually own.

## 9.2 T-065 bridge incident — why direct engine-job linkage was rejected

Existing `inspection_jobs` and `job_definitions` did not have a safe 1:1 link.

Candidate chain:

```text
inspection_jobs.rule_json.engineJobCode
    -> ml_learning_job_catalog_v1.job_code
    -> governed engine mapping
    -> job_definitions.job_code
```

This is shared/many-to-one and cannot store independent analysis targets safely.

Therefore independent target identity was persisted on `inspection_jobs`.

Catalogue values were measured rather than guessed:

```text
ML_PROCESS_VS_DEFECT    -> MlParamsVsDefects
ML_PROCESS_VS_DOWNTIME  -> MlParamsVsDowntime
ML_PROCESS_VS_KPI       -> MlParamsVsKpis
ML_WEEKLY_OVERALL       -> MlWeeklyFull
```

This prevented a classic bug: unit tests feeding one spelling while the real DB uses another.

---

# 10. T-065 PACK A / PACK B TROUBLESHOOTING HISTORY — DO NOT REPEAT IT

A large amount of time was spent fixing the **pack tooling itself**, not product architecture.

This history matters because the next session must not repeat it.

## 10.1 Pack A defects encountered

- missing integration-test namespace/import;
- warning gate demanded absolute zero on a pre-existing modified file instead of warning delta;
- `Start-Process -Wait` with redirected streams hung because MSBuild child handles kept pipes open;
- rival-build guard was not before every build;
- process `ExitCode` was unavailable after changing process orchestration and was mistakenly treated as failure;
- strict-mode `.Count` assumptions failed on scalar objects;
- test fixture violated existing target-version coherence;
- later generated test code had a bad `Code` symbol and wrong `CleanAsync` overload.

Final Pack A v14 was green:

```text
Build clean
created-file warnings          0
modified-file warning delta    0
828 apply                      green
828 replay                     green
Application unit               700/700 at Pack A point
Integration                    9/9
```

## 10.2 Pack B defects encountered

Earlier B runs had:

- wrong solution path (`Backend\PlantProcessIQ.sln` is correct);
- TRX `LogFileName` path misuse;
- API integration tests initially skipped because host flags were unset;
- test result collection mixed command output with returned path;
- API response/round-trip bugs then exposed and fixed.

Final v6 acceptance:

```text
Application unit   731/731
Architecture       181/181
Pack A             9/9
Pack B             21/21
zero skipped
```

Then the exact 12 files were committed at `1d27d199...`.

**Do not rerun the T-065 producer packs for orientation.** The producer is committed.

---

# 11. WHY “GREEN TESTS” DID NOT MEAN “CUSTOMER BI IS GREEN”

The user correctly challenged this.

The answer discovered through browser inspection is:

## 11.1 Unit tests were too local

They proved individual helpers/contracts, not the exact current persisted dashboard corpus.

## 11.2 Integration tests did not replay every active saved widget

Representative payloads passed, but customer pages load whatever is currently persisted in:

```text
dashboard_definitions
dashboard_widget_definitions
```

A stale dimension/measure/parameter combination can therefore be invisible to a small hand-picked test set.

## 11.3 E2E was not a full populated-data visual acceptance

Previous E2E gates did not guarantee:

- every active widget reaches HTTP 200;
- no `Did not complete`;
- no incompatible dimension/measure pair;
- no cards collapse to 1x1;
- no overlap;
- no huge empty associative surface;
- no raw GUID labels;
- no dirty chart focus frame;
- no error toast on first load.

## 11.4 The global frontend suite has infrastructure debt

A clean baseline ran hundreds of tests and did not terminate at 300 seconds, producing no report.

Therefore the emergency acceptance must use:

```text
targeted source/build gates
+ exact current persisted-widget API replay
+ browser customer acceptance
```

rather than assuming a single monolithic Vitest command is authoritative.

---

# 12. CUSTOMER BROWSER BUG REGISTER — THE EMERGENCY CHECKLIST

This is the checklist to use after R5. Do not replace it with a new generic checklist.

## P0-A — Layout / rendering corruption

Symptoms observed:

- widgets collapsed into tiny pill/card shapes;
- two widgets rendered on top of/above each other;
- large empty space with widgets hanging at edges;
- persisted `1x1` geometry;
- ghost/default widget IDs polluting loaded layouts;
- Reset could reproduce bad defaults.

Pages visibly affected included:

- Command Dashboard;
- Correlation Findings Board;
- Data Quality;
- some generated workspace pages.

Desired rule:

```text
only real rendered widget IDs survive
no foreign/default ghost IDs are serialized
no card below certified usable size
reset reflows real widgets deterministically
healthy authored layouts are not rewritten unnecessarily
```

Emergency source already contains a Worker-1/R3-compatible grid correction. R1/R3 adopted rather than blindly overwrote it.

## P0-B — Backend query failures

Visible messages:

```text
Did not complete
This widget's query did not complete.
Check that the API is running, then try it again.
```

But DevTools proved the API was running. Many responses were explicit HTTP `400 Bad Request`.

Two main backend refusal families were seen:

### Incompatible measure/dimension

Toast:

```text
This measure cannot be broken down by this dimension.
POST /analytics/dashboard/widgets/query
```

### Completeness/raw population guard

Toast included:

```text
aggregate_population_limit_exceeded:
this aggregate was not computed because completeness could not be guaranteed.
...
The engine caps the raw fact population before aggregating,
so a result over this limit would be a lower bound presented as a total.
No partial value is returned.
```

The refusal itself is **correct/honest**. The problem is that valid customer widgets are reaching a legacy/incomplete execution path or carrying a too-small persisted raw limit.

## P0-C — Associative request storm / incompatible enumeration

The associative view was opening live and generating broad queries for many dimensions.

Bad generic examples:

```json
{"dimensionCode":"shiftCode","measureCode":"observationCount","rawRowLimit":500}
{"dimensionCode":"defectType","measureCode":"observationCount","rawRowLimit":500}
{"dimensionCode":"riskClass","measureCode":"observationCount","rawRowLimit":500}
```

One generic `observationCount` measure is not a valid dimension-enumeration authority.

Emergency direction:

```text
shiftCode   -> materialCount
defectType  -> defectCount
riskClass   -> riskScore
plant/material dimensions -> materialCount
```

This is a compatibility choice for associative enumeration, not a new business metric definition.

## P0-D — Parameter Deep Analysis

Observed page:

- `Observations` KPI rendered `17,010`.
- `Average Value` failed.
- old network still contained invalid associative requests.
- `day + avgParameterValue + FDT_C` failed.
- KPI `avgParameterValue + FDT_C` had also failed in earlier captures.

Known payload:

```json
{
  "widgetType":"chart",
  "chartType":"line",
  "dimensionCode":"day",
  "measureCode":"avgParameterValue",
  "parameterCode":"FDT_C",
  "options":{
    "maxRows":50,
    "rawRowLimit":1000,
    "sortDirection":"desc",
    "includeWarnings":true
  }
}
```

This is semantically valid and should not be “fixed” by changing the metric.

## P0-E — Correlation Explorer

Known failing valid payload:

```json
{
  "widgetType":"chart",
  "chartType":"bar",
  "dimensionCode":"equipment",
  "measureCode":"avgParameterValue",
  "parameterCode":"FDT_C",
  "options":{
    "maxRows":20,
    "rawRowLimit":1000,
    "sortDirection":"desc",
    "includeWarnings":true
  }
}
```

Also suffered from associative bad requests on load.

## P0-F — Model Insights

The Analysis Readiness table rendered meaningful data.

`Defect Mix by Material Type` failed:

```json
{
  "widgetType":"chart",
  "chartType":"donut",
  "dimensionCode":"materialUnitType",
  "measureCode":"defectCount",
  "parameterCode":null,
  "options":{
    "maxRows":50,
    "rawRowLimit":1000,
    "sortDirection":"desc",
    "includeWarnings":true
  }
}
```

This should aggregate against the full current defect population, not return a partial count.

## P0-G — Command Dashboard

Observed:

- Material Units chart rendered in a weak “one point/KPI category” form.
- Process Observations rendered as a one-segment pie.
- Quality Events failed.
- Defect Rate failed.
- page-level error toast appeared.

Additional exact emergency probes were planned for:

```text
COMMAND.qualityEvents
COMMAND.defectRate
```

## P0-H — Correlation Findings Board

Observed two widgets compressed/overlapping instead of a professional two-panel layout.

Likely shared layout corruption rather than findings engine logic.

## P0-I — Data Quality

Observed two `Did not complete` error cards, visually overlapping/stacked badly.

## P0-J — Material Investigation Launcher

One later screenshot showed a real `Material Count by Type` chart rendering:

- Coil
- Slab
- Heat
- Slab tooltip approximately `17011`

Earlier page load still produced a query error toast, proving “one widget works” is not the same as “page works”.

## P0-K — Chart selected-state visual defect

Selecting a bar created an ugly white focus/frame rectangle around the bar.

Emergency CSS/chart helper work exists in the dirty worktree and must be browser-certified.

## P0-L — Presentation chrome / information architecture

Customer normal view should not unnecessarily show authoring controls such as Save/Reset unless in edit mode.

Emergency pack attempts to preserve:

```text
Edit layout
Refresh widgets
```

while hiding authoring-only actions in normal view where current source anchors permit.

---

# 13. CURRENT DIRTY WORKTREE — DO NOT WIPE IT

At the R4 preflight, `git status --short` showed **23 changes**.

Tracked modifications:

```text
M .gitignore
M Backend/PlantProcess.Application/Dashboarding/Services/Queries/DashboardAggregateExecutor.cs
M Backend/PlantProcess.Application/Dashboarding/Services/Queries/DashboardWidgetQueryService.cs
M Frontend/PlantProcess.Web/src/components/charts/InteractiveCharts.tsx
M Frontend/PlantProcess.Web/src/components/dashboard/AssociativePanel.tsx
M Frontend/PlantProcess.Web/src/components/dashboard/ChartExtras.tsx
M Frontend/PlantProcess.Web/src/components/dashboard/DashboardGridLayout.tsx
M Frontend/PlantProcess.Web/src/components/dashboard/LiveWidgetChart.tsx
M Frontend/PlantProcess.Web/src/pages/Dashboard/WorkspaceHeader.tsx
M Frontend/PlantProcess.Web/src/pages/Dashboard/__tests__/workspaceHeader.test.tsx
M Frontend/PlantProcess.Web/src/state/AssociativeContext.tsx
M Frontend/PlantProcess.Web/src/state/DashboardGridLayoutContext.implementation.tsx
M Frontend/PlantProcess.Web/src/state/associativeFields.ts
M Frontend/PlantProcess.Web/src/styles/components/dashboard-components.css
```

Untracked:

```text
?? Backend/tests/PlantProcess.Application.UnitTests/Dashboarding/DashboardDimensionSourceCapabilityTests.cs
?? Frontend/PlantProcess.Web/e2e/m1p3-consolidated-acceptance.spec.ts
?? Frontend/PlantProcess.Web/src/components/charts/__tests__/chartCursor.test.ts
?? Frontend/PlantProcess.Web/src/components/charts/chartCursor.ts
?? Frontend/PlantProcess.Web/src/components/dashboard/__tests__/associativePanelPresentation.test.ts
?? Frontend/PlantProcess.Web/src/state/__tests__/dashboardLayoutPollution.test.ts
?? Frontend/PlantProcess.Web/src/state/__tests__/layoutCompletion.test.ts
?? tools/.ppiq-restore/
?? tools/packs/
```

Some of these changes pre-date the ChatGPT emergency packs and came from Worker 1's active repair. The emergency packs intentionally adopted compatible changes rather than treating every dirty file as foreign corruption.

**Do not use any destructive Git clean/reset.**

---

# 14. EMERGENCY REPAIR PACK TIMELINE — R1 THROUGH R5

## 14.1 R1

File: `Apply-PPIQ-CustomerDemo-Emergency-R1.ps1`

Purpose:

- grid/ghost correction;
- associative lazy/collapsed;
- raw GUID/display cleanup;
- presentation chrome;
- chart selection cleanup;
- stale widget contract normalization;
- large-population backend remediation;
- exact saved-widget replay.

R1 execution:

```text
Preflight            passed
DB backup            captured
Grid source          compatible Worker-1 fix detected/adopted
Section 2            FAILED
Reason               Associative FieldAssoc exact anchor changed
```

This was a pack assumption failure. It stopped before product repair continued.

## 14.2 R2

R2 was generated to tolerate the changed associative source shape.

Before relying on it, new browser payload evidence arrived and the repair strategy was revised. R2 became superseded.

## 14.3 R3

R3 was run.

What it successfully changed before stopping:

```text
AssociativeContext initial enabled true -> false
dimension loading already guarded by enabled
AssociativePanel already collapsed
panel toggle wired to engine setEnabled
associativeFields gained per-dimension measure authority
AssociativeContext accepted per-field measure
dashboard CSS got chart selection override
```

It then reached Backend remediation and stopped:

```text
[FAIL] Aggregate-family enum anchor changed.
```

Meaning: the dirty Backend aggregate source had diverged from the archived assumption. R3 correctly refused a blind C# transplant.

## 14.4 R4

R4 intentionally stopped attempting to rewrite the dirty Backend lane and moved to presentation-data/current-population calibration.

It reached Section 5 and failed in **pack SQL wrapper tooling**, not product data:

```text
ERROR: syntax error at or near ";"
```

Root cause:

`Invoke-PsqlCsv` constructed:

```sql
COPY (
    SELECT ...
    ORDER BY ...;
) TO STDOUT WITH (...);
```

A trailing `;` inside the `COPY(subquery)` is invalid PostgreSQL syntax.

No population calibration or final replay completed.

## 14.5 R5 — CURRENT PACK

File:

`Apply-PPIQ-CustomerDemo-Emergency-R5.ps1`

SHA256:

`10185F871843BE263573ED4ABA196ADD46D6E91D4C7CCDE7BF62351127FA5BFA`

R5 fixes `Invoke-PsqlCsv` generically:

- trim outer query whitespace;
- strip only trailing statement terminators;
- never rewrite SQL semantics;
- print generated SQL on failure;
- self-test the wrapper before the real calibration.

First expected Section 5 proof:

```text
[OK] CSV wrapper proved with a trailing-semicolon query.
```

R5 is **not yet executed at handover creation**.

---

# 15. R5 DESIGN — WHAT IT WILL DO AND WHAT IT WILL NOT DO

## 15.1 It will not blindly rewrite the dirty Backend aggregate engine

The backend files:

```text
DashboardAggregateExecutor.cs
DashboardWidgetQueryService.cs
```

were already dirty from prior emergency work.

R3 proved archived enum/method anchors no longer matched.

R5 therefore preserves the current running API and uses exact HTTP probes for correctness.

This is an emergency containment choice, not the final generic scaling architecture.

## 15.2 Presentation full-population calibration

R5 measures actual current populations in `ppiq_presentation`.

Supported population measurements include:

```text
materialCount           -> material_units
defectCount             -> quality_events
defectRate              -> max(material_units, quality_events) source requirement
avg/max/min parameter   -> parameter_observations joined to parameter_definitions
observationCount        -> parameter_observations
downtimeMinutes         -> downtime_events
riskScore               -> risk_scores
processStepDuration     -> process_step_executions
dataQualityIssueCount   -> data_quality_issues
```

For parameter metrics with `parameterCode`, the count is scoped to that parameter.

It sets persisted `rawRowLimit` only when:

```text
target = current full population + 1
target <= existing absolute safety ceiling (250000)
```

This avoids presenting a sampled/lower-bound total as a full total.

It does **not** globally disable the completeness guard.

If a legacy measure genuinely exceeds the ceiling and cannot be served truthfully, R5 fails instead of lying.

## 15.3 Associative contract after R3/R5

The intended current source contract is:

```text
Associative panel starts collapsed/lazy.
No eager query storm on first page load.

Per-dimension measure authority:
  site/area/equipment/source/materialUnitType -> materialCount
  defectType                                  -> defectCount
  riskClass                                   -> riskScore
  shiftCode                                   -> materialCount

Current-demo enumeration raw window -> 50,000
```

The new session must verify actual compiled source and browser Network after R5, not merely trust this intended mapping.

## 15.4 Exact R5 probes

R5 contains nine exact probes:

```text
COMMON.shiftCode
COMMON.defectType
COMMON.riskClass
CORRELATION.avgByEquipment
MODEL.defectByMaterialType
PARAM.kpiAvg
PARAM.dayAvg
COMMAND.qualityEvents
COMMAND.defectRate
```

Expected output:

```text
PROBE <name> -> HTTP 200 ...
```

or one concise named failure.

## 15.5 Exact saved-widget replay

After normalization/calibration R5 reloads every active persisted widget and POSTs its actual contract to the API.

Expected final report:

```text
Saved widgets replayed : N
Succeeded              : N
Failed                 : 0
```

Report path under:

```text
tools\packs\.logs\PPIQ-CustomerDemo-Emergency-R5-<timestamp>\
```

Key artifacts:

```text
REPORT.txt
population-calibration.txt
saved-widget-replay.txt
layout-audit.txt / related layout report
web.stdout.log
web.stderr.log
```

---

# 16. R5 RUN COMMAND — ONLY IF IT HAS NOT ALREADY BEEN RUN

Do not rerun if the user has post-handover R5 output.

```powershell
cd C:\Workspace\PlantProcess-IQ

Move-Item `
  "$env:USERPROFILE\Downloads\Apply-PPIQ-CustomerDemo-Emergency-R5.ps1" `
  ".\tools\packs\Apply-PPIQ-CustomerDemo-Emergency-R5.ps1" `
  -Force

Unblock-File `
  ".\tools\packs\Apply-PPIQ-CustomerDemo-Emergency-R5.ps1"

$errors = $null
$tokens = $null

[System.Management.Automation.Language.Parser]::ParseFile(
    (Resolve-Path ".\tools\packs\Apply-PPIQ-CustomerDemo-Emergency-R5.ps1"),
    [ref]$tokens,
    [ref]$errors
) | Out-Null

if (@($errors).Count -gt 0) {
    $errors | Format-List
    throw "STOP: R5 parser errors."
}

$expected = "10185F871843BE263573ED4ABA196ADD46D6E91D4C7CCDE7BF62351127FA5BFA"

$actual = (
    Get-FileHash `
      ".\tools\packs\Apply-PPIQ-CustomerDemo-Emergency-R5.ps1" `
      -Algorithm SHA256
).Hash

if ($actual -ne $expected) {
    throw "STOP: R5 SHA256 mismatch."
}

powershell `
  -NoProfile `
  -ExecutionPolicy Bypass `
  -File ".\tools\packs\Apply-PPIQ-CustomerDemo-Emergency-R5.ps1"
```

If it fails, send/read only the first failing section through the exception. Do not start over from Section 0 unless the source/data changed and the pack must legitimately rerun.

---

# 17. BROWSER ACCEPTANCE AFTER R5 — REQUIRED RELEASE GATE

Only after R5 completes its build/restart/replay should the browser be used as acceptance.

Hard refresh:

```text
Ctrl+F5
```

Priority pages:

```text
/dashboard
/workspace/PARAMETER_DEEP_ANALYSIS
/workspace/CORRELATION_EXPLORER
/workspace/MODEL_INSIGHTS
/workspace/DATA_QUALITY
/workspace/MATERIAL_INVESTIGATION_LAUNCHER
/workspace/RISK_DASHBOARD
/workspace/RISK_INTELLIGENCE
/workspace/EQUIPMENT_OPERATIONS
/workspace/CORRELATION_FINDINGS_BOARD
```

For each page, record:

```text
[ ] no initial error toast
[ ] no non-200 widget/query request
[ ] no "Did not complete"
[ ] no "measure cannot be broken down" for persisted widgets
[ ] no aggregate_population_limit_exceeded
[ ] no widget overlap
[ ] no pill/1x1 widgets
[ ] no hanging cards
[ ] no huge accidental blank space caused by layout
[ ] associative panel does not explode into query storm
[ ] user-facing labels are professional, no raw IDs where a label exists
[ ] selected chart has no dirty white frame
[ ] normal presentation view is not cluttered with authoring-only controls
[ ] chart data is meaningful, not a one-point placeholder caused by wrong dimension
```

Do not mark emergency complete based on one page.

---

# 18. TEST / EVIDENCE ANTI-REPEAT LEDGER

The purpose of this table is to stop the next session from spending tokens repeating already-proven tests merely to reconstruct state.

| Area | Proven result | Re-run rule |
|---|---|---|
| M1 populated PostgreSQL recertification | 17/17, non-vacuous | Do not rerun for orientation |
| T-045-R1 | A/B/C/D closed | Reopen only with regression evidence |
| T-047 | closed | Do not rerun absent page regression |
| T-048 | closed | Do not rerun absent associative regression; current emergency changes may require targeted regression only |
| T-049 | `cc4d884...` | Emergency layout code changed adjacent area, so current browser/layout certification supersedes historical proof |
| T-050 | `4fdd311...` | Preserve provenance closure |
| T-051 | prior 10/10 + 4/4 Playwright-era proofs | Do not rerun old isolated pack; emergency acceptance is broader |
| T-052 | closed `24868e39...` | no rerun for orientation |
| T-057 | closed | no rerun unless changed source |
| T-058 | closed `c3573ae7...`; unit 683-era green + runtime 8/8 | no rerun |
| T-064 | formally closed `02518934...` | no rerun |
| T-068 | typecheck clean; 16/16 focused; 35/35 relevant; production build clean | no rerun unless its files are changed |
| T-065 producer | 731 unit, 181 architecture, 9/9 Pack A, 21/21 Pack B | do not rerun producer packs |
| global frontend full suite | hangs/non-terminates at 300s on clean baseline | do not use as emergency orientation gate |
| Emergency R1 | failed pack anchor before completion | obsolete |
| Emergency R3 | partial frontend changes, failed Backend enum anchor | do not rerun |
| Emergency R4 | failed CSV wrapper syntax in Section 5 | do not rerun |
| Emergency R5 | not yet run at handover | **this is the next gate** |

---

# 19. FRONTEND TEST-INFRASTRUCTURE DEBT

Historical finding:

```text
F-FE-SUITE-01
frontend Vitest full suite did not terminate on a clean tree
```

At an earlier clean baseline there were also four carried Journey/design-system failures, but later targeted T-068 regression had zero carried failures in its relevant set.

Do not confuse:

```text
pre-existing global-suite infrastructure problem
```

with:

```text
current dashboard runtime/browser failures
```

They are separate.

A future dedicated task should make the full suite terminate deterministically and emit a machine-readable report, but **not during customer demo emergency unless it blocks targeted certification**.

---

# 20. REALIZATION SCOREBOARD — END OF THIS SESSION

These percentages are release-oriented assessments, not static backlog percentages.

| Area | Score | State | Key reason |
|---|---:|---|---|
| Product identity / architecture coherence | 94% | Done-level | generic product contract, Layer A/B, governed evidence, two-track rulings strong |
| Canonical/presentation data foundation | 91% | Done-level | populated non-vacuous DB and R1 corrective evidence |
| Deterministic analytics / evidence governance | 90% | Done-level | major M1 contracts closed |
| Relationship resolution | 88% | Done-level base | T-058 closed; T-060 visible browser still pending |
| Governed job target/version semantics | 94% | Done-level | T-064 + T-065 producer bridge green |
| Analysis outcome/grain registry authority | 95% | Done-level | T-068 closed, no literal catalogue/coil fallback |
| Analysis authoring visible convergence | 65% | Mostly done | backend producer ready; T-065 D3 consumer still pending |
| Readiness visible authority | 55% | Partially done | engine/readiness exists, T-066 visible convergence pending |
| Findings evidence UX | 50% | Partially done | provenance exists; T-067 pending |
| BI runtime correctness across saved widgets | 55% | **Release blocker** | current browser has 400s and completeness failures |
| BI layout / customer visual quality | 55% | **Release blocker** | overlap, ghost geometry, oversized/empty areas observed |
| Dashboard browser certification | 35% | **Not release-ready** | broad manual pass is red |
| Assistant governed explanation/evidence | ~85% | Done-level base / final release cert pending | strong evidence architecture, later frozen question pack still follows M1 freeze |
| ML foundation | ~80% | Mostly done | strong architecture/test base; later M2 canonical convergence still planned |
| Security/auth/topology architecture | ~85% | Mostly done | strong design/historical proof; current server re-certification not performed here |
| Deployment pipeline historical capability | ~82% | Mostly done | historical green chain exists; current production not re-certified in this session |
| Marketing website/company profile | ~85% | Mostly done | implementation strong; final commercial browser/email-routing proof still matters |

## 20.1 Release verdict

**Current version is NOT customer-demo certified at the handover boundary.**

Reason:

```text
R5 has not yet completed
+ browser still shows real widget/query failures
+ persisted-widget full replay has not yet reached Failed=0
```

The architectural platform may be implementation-advanced, but release certification is blocked by the BI runtime/visual layer.

---

# 21. PIPELINE / SERVER / DEPLOYMENT — DEEP KNOWLEDGE TRANSFER

## 21.1 Historical green release sequence

A historically working release chain was:

1. preserve runtime `.env` and Caddy configuration before checkout;
2. checkout source;
3. restore protected files;
4. run runtime-env validation/creation;
5. clear stale process/workspace locks safely;
6. blocking Backend tests;
7. blocking Frontend tests;
8. E2E according to current truthful policy;
9. DB:
   - PostgreSQL up;
   - EF migrations;
   - post-EF numbered SQL;
   - seed;
10. demo-source migration/seed when enabled;
11. build/recreate app stack;
12. internal health gate;
13. rollback to previous image on failure;
14. presentation defaults/licence/authenticated smoke.

## 21.2 Important fixes that made pipeline / App URL work historically

### Caddy upstream mismatch
Caddy referenced one service name while compose used another.

A network alias restored reachability. Permanent configuration should use canonical service naming.

### Compose project separation
Keep:

```text
plantprocessiq = infrastructure
ppiq-app       = application
```

This prevents application `remove-orphans` from deleting Caddy/Jenkins/backups.

### Canonical DB configuration key

Use:

```text
ConnectionStrings__PlantProcessDb
```

not an invented/default key.

### Vite CLI
Do not use positional `localhost 5173`.

Use explicit:

```text
--host
--port
```

### Smoke credentials
Do not compile placeholder credentials into the bundle. Use protected runtime configuration.

### Signing key
Protected runtime secret with required minimum length.

### PostgreSQL env/volume coupling
Do not regenerate DB password/env while retaining a volume initialized with an old password; that produced `28P01`.

### UTF-8 / line-ending discipline
Several scripts/patches experienced encoding or line-ending hazards. Avoid line-ending churn; pack validators often assert CRLF because the Windows repo historically uses it.

### Public ingress
Caddy only on 80/443. App/API/DB remain internal/private.

### Health-gated rollback
Tag/retain previous image and roll back if the new deployment health gate fails.

## 21.3 Current server truth limitation

No current server production certification was executed during this emergency browser session.

Therefore:

- topology/history are known;
- present public reachability/health is not automatically proven.

---

# 22. WEBSITE / COMPANY PROFILE / EMAIL SIDE LANE

Worker 2 previously delivered website implementation commit:

`40b56d99`

Important ruling:

- implementation commit accepted;
- final package closure requires current source validator + browser commercial acceptance;
- stale tests asserting old “one PPIQ core/capability pack” architecture must be corrected, not satisfied by regressing product identity.

Exactly three frozen commercial deliverables were required:

1. Long Company Profile.
2. Short Executive Profile.
3. PowerShell website modification/alignment + exact apply/validate commands.

Optional Sales Deck was useful but not required for frozen closure.

Cloudflare email:

```text
info@souindustrial.com -> Gmail destination Verified
```

but routing was previously not operationally proven because Cloudflare Email Routing/DNS was disabled/not configured and testing from the same destination mailbox is weak proof.

Do not claim customer email routing production-ready until external sender → `info@souindustrial.com` → destination is proven.

---

# 23. COMMERCIAL / DEMO PRESENTATION PRINCIPLES

Customer-facing BI should look like a professional industrial intelligence layer, not a developer diagnostic canvas.

Acceptable:

- credible populated KPIs;
- coherent charts;
- governed refusal when data is truly unavailable;
- concise evidence/readiness explanation;
- meaningful labels;
- deliberate whitespace;
- clear edit vs presentation modes.

Unacceptable for the customer demo:

- raw GUID lists dominating the page;
- “unknown” repeated as primary business labels when a display label exists;
- widgets as tiny pills;
- overlapping cards;
- error toasts caused by persisted misconfiguration;
- generic `Did not complete` when the platform itself has a known contract bug;
- 200+ network requests caused by an eager associative query storm;
- one-point placeholder charts masquerading as business analysis.

---

# 24. ROOT-CAUSE THINKING LEARNED DURING THE EMERGENCY

## 24.1 Do not “fix” a governed refusal by weakening the guard

`aggregate_population_limit_exceeded` is evidence that the backend is trying not to present a lower bound as a total.

Correct solution options:

- use complete relational aggregation;
- or ensure the legacy path sees the complete current population within an existing safety ceiling.

Wrong solution:

- disable completeness guard;
- return partial count silently;
- just set an arbitrary huge global limit.

## 24.2 Persisted data is part of runtime architecture

Current pages are not defined only by React source.

They are a composition of:

```text
Frontend renderer
+ dashboard definition
+ persisted widget definition
+ display options
+ backend metadata/capabilities
+ populated DB
```

Therefore source-only unit tests can all pass while the customer page fails.

## 24.3 Browser Network payload is more valuable than a generic toast

The toast “Did not complete” is low information.

The actual request:

```text
dimensionCode
measureCode
parameterCode
rawRowLimit
```

plus HTTP status and response body usually identifies the authority mismatch.

## 24.4 Tool self-tests matter

Several pack failures were caused by pack infrastructure assumptions.

A gate that measures something must prove it can distinguish success from failure.

Examples learned:

- process exit-code availability;
- TRX path resolution;
- skipped integration host;
- CSV wrapper semicolon behavior;
- warning baseline/delta;
- output scalar vs array under StrictMode.

## 24.5 Exact anchors are good until the source intentionally diverges

Once another worker has legitimately changed a file, an old exact text anchor should stop the pack.

The next step should be:

- inspect/adopt current semantics;
- use a new bounded semantic anchor;
- not force the old string back into the file.

This is exactly what happened with `FieldAssoc` and the Backend aggregate enum.

---

# 25. ADVICE FOR THE NEW SESSION — HOW TO THINK, NOT JUST WHAT TO RUN

1. **Start from the live stop point, not the backlog beginning.**
2. **Do not rerun proven task packs.**
3. **Treat R5 as the current experiment/gate.**
4. If R5 fails, fix only the failing mechanism; do not create R6 with unrelated redesign.
5. Once R5 reaches replay, use the exact failing widget list as the work queue.
6. Do not hide a true backend incompatibility by changing chart semantics arbitrarily.
7. For each failing widget classify:
   - persisted bad contract;
   - valid contract but legacy executor;
   - missing data;
   - layout-only defect;
   - frontend stale bundle/state.
8. After API replay is zero-failure, do one controlled browser walk through the explicit page checklist.
9. Commit the emergency repair only after:
   - source gates green;
   - exact widget replay green;
   - browser critical pages green;
   - no unrelated files staged.
10. Then restore normal task sequencing.

---

# 26. POST-EMERGENCY TASK ORDER

Only after the BI emergency is customer-green:

```text
1. freeze/commit emergency repair with exact evidence
2. T-065 frontend D3 consumer convergence
3. T-066 visible readiness authority
4. T-067 findings evidence panel
5. T-060 Relationship Browser certification/landing
6. remaining M1 release certification/frozen question pack
7. freeze Presentation baseline/tag/branch
8. return to Track 2 / M2a canonical generic product
```

If business/demo priorities require T-060 before T-065 consumer, make that a conscious product decision; do not run both in the same files concurrently.

---

# 27. PREVIOUS HANDOVER KNOWLEDGE PRESERVED

The previous comprehensive source is:

`PPIQ_DEEP_SESSION_HANDOVER_ChatGPT_17Aug2026_FINAL.md`

It already contains the embedded 15-Aug handover and should be treated as historical authority for:

- detailed M1 data/provenance progression;
- T-175/T-178 ML history;
- older Assistant evidence;
- T-057/T-064 pre-closure troubleshooting;
- server/pipeline history;
- source-file maps;
- backlog task inventory.

Key 17-Aug facts that remain relevant:

- repo `C:\Workspace\PlantProcess-IQ`;
- local native PostgreSQL vs server Docker PostgreSQL distinction;
- ppiq_app vs ppiq_presentation certification distinction;
- SQL reservation discipline;
- no fake migration history;
- row identity/provenance rules;
- exact-file shared-worktree discipline;
- historical pipeline truth must not be described as current certification.

This 22-Aug handover supersedes the **resume point** from 17-Aug. Do not start at the old Worker 1/2/3 tasks listed there.

---

# 28. IMPORTANT HISTORICAL ML / AI KNOWLEDGE

Although the current emergency is BI, do not lose the ML architecture already settled.

- ML project is separate under `ML/`; do not put product Python files under Backend/tools.
- MF-06 was C#-first where appropriate.
- Capability profiler states: `Available / Degraded / Unavailable`.
- T-178 corrective produced 503 tests / 0 failures at one point.
- 512-combination precedence model:
  - actionable 1;
  - evidence_only 7;
  - exploratory 248;
  - suppressed 256.
- Do not implement learned target tables without canonical input/output identity.
- M2a canonical definition/relationship authority must precede later ML execution convergence.
- ML prediction, statistical findings and assistant explanations must remain bound to provenance/relationship authority, not ad-hoc joins.

Historical T-175 is CLOSED and must not be resurrected.

---

# 29. IMPORTANT ASSISTANT / PROVENANCE KNOWLEDGE

The assistant/provenance work established:

- evidence snapshot persistence;
- stable IDs;
- retrieval/citation discipline;
- unknown evidence ID => unavailable, not fabricated;
- no echoing a fabricated marker;
- `populationCount` is not equivalent to chart row count;
- visual order can differ from backend row order;
- preserve original backend row identity;
- evidence execution should use the exact render request snapshot;
- stale-context races must not attach evidence from context B to point A;
- resolver-null, evidence-unavailable and transport failure are distinct states;
- normal rendering must remain side-effect free; evidence write/re-execution is opt-in.

These principles remain valid during BI repair.

---

# 30. MIGRATION / DATABASE NUMBERING RULES

Do not choose a migration number by “highest file + 1”.

Historical critical slots:

```text
824 T-064 core target-definition compatibility      immutable
825 T-064 kind/varchar convergence                  immutable
826 T-064 target_parameters                         used/closed under final T-064 chain
827 T-057 relationship compatibility                used/closed
828 T-065 analysis-job target compatibility         committed
999 high-number runtime/grant region already exists
```

Reservation/ownership beats numeric convenience.

Migration replay must be explicitly proven when the task requires replayability.

Never fabricate `__EFMigrationsHistory` rows to silence EF. Baseline only after actual schema equivalence.

---

# 31. CURRENT EMERGENCY FILE / ARTIFACT MAP

In the repo after the user moves it:

```text
tools\packs\Apply-PPIQ-CustomerDemo-Emergency-R5.ps1
```

Expected logs:

```text
tools\packs\.logs\PPIQ-CustomerDemo-Emergency-R5-<timestamp>\
```

Backups:

```text
tools\.ppiq-restore\PPIQ-CustomerDemo-Emergency-R5-<timestamp>\
```

R5 itself is also preserved with this handover as a generated artifact in the ChatGPT session in which this file was created. The user should keep a local copy because a future ChatGPT sandbox is not guaranteed to contain prior generated files.

---

# 32. EXACT NEW-SESSION OPENING MESSAGE

Copy/paste this with the handover:

```text
RESUME-PPIQ-22AUG-EMERGENCY-R5.

Read PPIQ_DEEP_SESSION_HANDOVER_22Aug2026_EMERGENCY_R5.md first.
Do not re-investigate from greenfield and do not rerun already-proven task packs.

Act as central Tech Lead / Product Owner / PM.

Repository: C:\Workspace\PlantProcess-IQ
Current proven HEAD before emergency source changes: 1d27d1997801ffa18cacfafcfef115302c0cf748
Backlog: PPIQ_Backlog_v2_10_4_16Aug2026_Three_AI_Agent_Orchestration.xlsx
ppiq_presentation is the populated customer-demo DB.
API 5063, Frontend 5173.

All normal workers are STOPPED. One emergency BI repair lane only.

T-068 is closed at d4af982e.
T-065 backend producer is committed at 1d27d199 with:
731 Application unit, 181 Architecture, Pack A 9/9, Pack B 21/21, zero skipped.
T-065 frontend D3 consumer is NOT closed.
T-066 and T-067 are queued.
T-060 is paused.

Browser acceptance is RED because of:
- incompatible dimension/measure persisted requests;
- aggregate completeness/raw population failures;
- layout ghost/1x1/overlap defects;
- associative query storm;
- presentation visual defects.

Emergency R3 partially changed Frontend then stopped on changed Backend enum anchor.
R4 stopped in Section 5 because Invoke-PsqlCsv put a trailing semicolon inside COPY(subquery).
R5 fixes that wrapper generically and is current.

R5 file:
Apply-PPIQ-CustomerDemo-Emergency-R5.ps1
SHA256:
10185F871843BE263573ED4ABA196ADD46D6E91D4C7CCDE7BF62351127FA5BFA

FIRST determine whether R5 was already run after handover creation.
If it was run, inspect its output/logs and DO NOT rerun.
If it was not run, execute it once.
Continue from the first failing section or, if it reaches replay, from saved-widget-replay.txt.

Do not use git clean/reset hard/restore dot/add dot/add -A.
Do not resume normal workers until widget replay and browser acceptance are green.
```

---

# 33. FINAL HANDOFF — SINGLE-PARAGRAPH VERSION

PlantProcess IQ enters the next session with its core M1 architecture materially advanced—T-058 relationship resolution, T-064 governed target/version semantics, T-068 registry-driven Analysis Toolbox options, and the T-065 backend target bridge are all evidence-backed and committed, with T-065 producer acceptance at 731 Application tests, 181 Architecture tests, 9/9 bridge integrations and 21/21 API integrations—but the customer BI release is still blocked by live browser defects in persisted dashboard/widget contracts, completeness-limited aggregates, associative enumeration, and layout/presentation state. All normal worker lanes were intentionally stopped and one emergency lane took control. R3 already applied bounded frontend changes for lazy associative behavior, per-dimension measure authority and chart visual cleanup, then stopped safely when the dirty Backend aggregate enum no longer matched an archived anchor; R4 avoided the Backend rewrite but stopped before calibration because its CSV helper generated an invalid semicolon inside `COPY(subquery)`; R5 fixes that helper, self-tests it, measures full current presentation populations, truthfully calibrates persisted widget raw limits within the existing 250k ceiling, normalizes saved-widget contracts/layouts, rebuilds/restarts the Frontend, runs nine exact known-failure API probes and replays every active saved widget. R5 SHA256 is `10185F871843BE263573ED4ABA196ADD46D6E91D4C7CCDE7BF62351127FA5BFA` and, at this handover boundary, it has **not yet been executed**. The next session must start there—not at the backlog, not at Worker 1, and not with a fresh test sweep—and must not release or resume T-065 consumer/T-066/T-067/T-060 until exact widget replay plus browser acceptance are green.

---

# 34. CHECKLIST BEFORE DECLARING THE EMERGENCY CLOSED

```text
[ ] R5 completes without pack/tooling failure
[ ] CSV self-test green
[ ] current presentation population calibration completes
[ ] source invariants green
[ ] frontend typecheck green
[ ] frontend production build green
[ ] frontend restarted; browser is not using stale bundle
[ ] all 9 exact probes green
[ ] saved-widget replay Failed = 0
[ ] /dashboard browser clean
[ ] Parameter Deep Analysis clean
[ ] Correlation Explorer clean
[ ] Model Insights clean
[ ] Data Quality clean
[ ] Material Investigation Launcher clean
[ ] Risk Dashboard clean
[ ] Risk Intelligence clean
[ ] Equipment and Operations clean
[ ] Correlation Findings Board layout clean
[ ] no query-error toast on initial page load
[ ] no 1x1/pill/overlap widget
[ ] no dirty white chart selected frame
[ ] no accidental associative query storm
[ ] exact files staged only
[ ] emergency commit hash recorded
[ ] Presentation baseline frozen/tagged after acceptance
[ ] normal worker lanes explicitly released
```

---

# 35. SOURCE / EVIDENCE REFERENCES FOR FUTURE DEEP LOOKUP

When an exact historical detail is needed, look up these before rerunning anything:

```text
PPIQ_DEEP_SESSION_HANDOVER_ChatGPT_17Aug2026_FINAL.md
PPIQ_DEEP_SESSION_HANDOVER_17Aug2026_FINAL.md
PPIQ_Backlog_v2_10_4_16Aug2026_Three_AI_Agent_Orchestration.xlsx
PPIQ_Chapter2_Technical_Overview*.md
PPIQ_Chapter3_General_Technical_Function_Description*.md
PPIQ_Chapter4_Specific_Technical_Function_Description*.md
PPIQ_Layer_B_Architecture_Design_Pack*.md
PPIQ_Engine_ML_Onboarding_Brief_AR*.md
00_Master_Index*
01_Backend_Core*
02_Backend_Database*
03_Backend_Tests*
04_Frontend_App*
06_Infrastructure*
07_Tools_Validation_Misc*
10_Audit_Signals*
```

Useful commit/evidence anchors:

```text
T-045 R1-A    c56008c0
T-045 R1-B    dd9a6b04
T-045 R1-C    39ce59ef
T-045 R1-D    283aae2c
T-049         cc4d884...
T-050         4fdd311...
T-051         6c99e091...
T-052         24868e3944e48dea99ac5501345b570972200fa3
T-057         47ee7075...
T-058         c3573ae7115788f1b0015c958d50d44d6977f89a
T-064         02518934f19ca1a59d5aa61fa755f431a7e9048d
T-068 base    3d8c706f...
T-068 corr.   d4af982ebaf55f2db0b7951ea81ec4681be1c0b6
T-065 bridge  1d27d1997801ffa18cacfafcfef115302c0cf748
Emergency R5  SHA256 10185F871843BE263573ED4ABA196ADD46D6E91D4C7CCDE7BF62351127FA5BFA
```

---

**END OF HANDOVER**

# PPIQ — WORKER 1 SESSION HANDOVER
### 17–19 August 2026 · Core Platform + Data/Configuration Vertical

**Read this file completely before touching anything.** It exists so the next session does not
re-investigate, re-measure, or re-run what has already been proven here. Every number in it was
observed in a console; nothing is inferred. Where something is unknown, it says so.

---

## 0. THE ONE-PARAGRAPH VERSION

Four tasks closed with commits (PR-050-01, T-057, T-058, and the T-064 handoffs consumed).
T-060 is implemented and blocked on a frontend lint reality, not on design. T-065 Pack A is
**green and frozen on disk but NOT committed**; Pack B is the only thing standing between
Worker 3 and unblocking. Fifteen defects were found and fixed, of which **thirteen were in the
pack tooling, not the product**. The single most important lesson of the session is at §11.

**START HERE NEXT SESSION: §12.**

---

## 1. WHERE THE TREE IS RIGHT NOW

### 1.1 Commit line

```
02518934f19ca1a59d5aa61fa755f431a7e9048d   T-064 CLOSED            (Worker 3)
b811696be688fcb613bfc986ff9b3729bcc86398   T-064 corrective / 825   (Worker 3)
fb575a147d9edc60415e4d3235bf86ba77a0b2da   T-064 core               (Worker 3)
96460804 5942527c281ae32a05484d64ffaf8103  PR-050-01 CLOSED         (Worker 1)
4fdd311a                                    T-050                    (Worker 2)
47ee7075b22d4b861597c695b665835a64bfdbd9   T-057 CLOSED             (Worker 1)
c3573ae7115788f1b0015c958d50d44d6977f89a   T-058 CLOSED             (Worker 1)
6c99e091                                    T-051                    (Worker 2)
```

### 1.2 Uncommitted state at handover

**T-065 Pack A is applied on disk and green. It must NOT be reverted.** Its files and hashes:

```
58150A0A3280ED4EA76B0D344E5FE2B56ADC739B184824093A21D0574EA638AD  Backend\database\scripts\828_t065_analysis_job_target_compatibility.sql
17A684C0384C0914F359B9F80ED3C7567F7C4B493AA2152FFA726D3C660A350C  Backend\PlantProcess.Application\Jobs\Targeting\AnalysisJobClass.cs
46806D850FB19BB7DF3ADE27E0743E5C70F483F0FDD7D63B5DCC0F7F9621FE53  Backend\PlantProcess.Infrastructure\Jobs\AnalysisAwareJobTargetLookup.cs
DD04942EEDD767514AAE094BDC8E91776A62AA9DF462795E88FFD0500D34E1DE  Backend\tests\PlantProcess.Application.UnitTests\Jobs\T065AnalysisJobClassTests.cs
18CAF1824DC90374B89C4D49C49ACD653E5AEF7A1D56D3CFC225FEA7ADF5F8D2  Backend\tests\PlantProcess.Api.IntegrationTests\Jobs\T065AnalysisJobTargetBridgeTests.cs
5CF24E3D2AA6A5E01BAEEBA56F58741F674CBCA0FE57332E93A42C4434DBA69D  Backend\PlantProcess.Application\DependencyInjection.cs
67B67659820A55504703A855AFF8002CFCD738837AF87DB2267A05384334B8E3  Backend\PlantProcess.Infrastructure\DependencyInjection.cs
```

Recovery copies of the two modified composition roots:
`C:\Workspace\PlantProcess-IQ\tools\.ppiq-restore\T065A-20260819-100834`

**Foreign uncommitted work in the shared worktree — never stage, never revert:**

```
 M .gitignore
 M Documentation/docs/Product Book/PPIQ_Backlog_v2_10_4_...xlsx
 M Website/PlantProcess.Website/**            (7 files, Worker 2 commercial lane)
?? Documentation/docs/CompanyProfile/
?? Documentation/docs/Product Book/HandOver Documentation/*.md
?? Frontend/PlantProcess.Web/e2e/m1p3-consolidated-acceptance.spec.ts
?? tools/packs/
```

### 1.3 Database state — `ppiq_presentation`

`828` **is applied**. Verified by direct query at 19 Aug 10:08:

```
column:target_definition_id          constraint:ck_inspection_jobs_target_identity_complete
column:target_definition_kind        constraint:ck_inspection_jobs_target_version_coherent
column:target_definition_version     constraint:ck_inspection_jobs_target_version_policy
column:target_parameters             index:ix_inspection_jobs_target_definition
column:target_version_policy
```

`827` (T-057 relationships) is also applied and replayed clean.
No stale `T065_%` test rows remain — the pack's precondition probe reported
`No inspection_jobs row carries a target policy yet`.

---

## 2. TASK-BY-TASK RECORD

### 2.1 PR-050-01 — Widget-row provenance producer · **CLOSED `96460804`**

**What it does.** A dashboard widget execution can now be handed to the existing T-073
`WidgetResult` evidence authority, and every returned row carries a truthful population
descriptor.

**The authority check that shaped it.** T-073 already existed and already had
`ProvenanceKind.WidgetResult`. What did **not** exist:

| Gap | Reality found in source |
|---|---|
| G1 | The only writer was `WidgetResultChunkProducer` (Assistant reindex), and it hardcoded `filterContextJson = "{}"`. Any filtered dashboard widget had no matching snapshot at all. |
| G2 | `population_count` is `ObservationCountTotal`, which the source itself says is *not* a population. No row-level identity exists. |
| G3 | The table is `canon.assistant_widget_result` — Worker 3's namespace. |

**Karim's Ruling C resolved it:** Population Descriptor (what the point represents) is a
different fact from Execution Evidence (which run produced it). Never conflate them.

**Delivered in three packs, one commit:**

- `IWidgetResultEvidenceWriter` (Application/Provenance) + `NpgsqlWidgetResultEvidenceWriter`
  — the T-073 write extracted from the Assistant producer, so both callers share one
  determinism rule. Solved a dependency cycle: the producer depends on the query service, so
  the query service could not depend on the producer.
- `DashboardPopulationDescriptor` — pure, IO-free. **Filter fields are discovered by
  reflection, not listed**, so the engine carries no plant vocabulary (Rule 1) and a change to
  the filter contract cannot silently drop a filter out of the evidence identity.
- Common `DecorateResultMetadataAsync` — the Class-2 early return no longer leaves the method.
  **15 native sources were not touched**: every new DTO member is positional with a `= null`
  default, so all existing construction sites still compile.

**Design decisions worth preserving:**

- Evidence writing is **opt-in** (`IncludeExecutionEvidence`). An ordinary render is a read; a
  dashboard that wrote a row on every refresh would turn an evidence store into an event log.
- Writing requires `PageCode` + `WidgetCode`. Without them T-073 renders
  `"On page , widget  shows..."` into the Assistant retrieval corpus. Missing identity ⇒
  values returned, handle withheld, explicit `execution_evidence_unavailable` warning.
- `RowFingerprint` is **nullable**. When no categorical binding can be derived from a multi-row
  result, or two rows would collide, the identity is withdrawn. A shared identity sends a
  drill-down to the wrong population; absence is honest.

**Live certification (ppiq_presentation, all green):**

```
A  ordinary read side-effect free     64 -> 64, handle null
C  evidence without identity          64 -> 64, explicit refusal text
B  explicit evidence request          64 -> 65, kind=WidgetResult
I  resolves through T-073 HTTP        GET /api/assistant/evidence/widget-result/{id}
D  real filter context persisted      {"toUtc":"...","fromUtc":"..."}   (not {})
E  deterministic reuse                same id, 65 -> 65 (no new row)
F  changed window                     distinct id, distinct queryFingerprint
   populationCount = 16701 on ONE row (ObservationCount, never row count)
   multi-row subject: rows=5 descriptors=5 fingerprints=5 distinct=5
```

**Recorded findings, not fixed:**

- `PR050-F01` — T-073 persistence is Assistant-named while its use is product-wide → **T-129**
- `PR050-F02` — `IProvenanceResolver` existence resolution is not tenant-scoped
  (`WHERE id = @id`; content reads are safe via `IWidgetResultEvidenceReader`) → **T-112/T-114**
- `PR050-F03` — `ProvenanceHandleRef` declared 3× in the frontend in 2 shapes → Worker 2
- `PR050-F04` — `DashboardWidgetFiltersDto` carries `DefectType`, `RiskClass`, `ShiftCode`,
  `MaterialCode` — domain vocabulary in a generic product contract
- `PR050-F06` — no retention/lifecycle for WidgetResult evidence → **T-129**
- `PR050-F07` — `CS8629` at `DashboardWidgetQueryService.cs(840,43)`; `git blame` proves it is
  `7b8d6e8e8` (11 Aug, T-045-R1-D downtime predicate). **Not ours. Do not fix.**

---

### 2.2 T-057 — Relationship contract, part 1 · **CLOSED `47ee7075`** (10 files, 1569 insertions)

**Starting position: complete greenfield.** The whole tree contained no relationship entity, no
service, no table, no endpoint. Only `docs/m1/checklists/S07_RelationshipBrowser.md` and
`ParameterRelationshipNpgsqlTests.cs` (which is parameter↔outcome correlation, unrelated).

**Storage ruling.** Canonical is `ppiq_meta.plant_relationships` with
`source_definition_id → definition_store(id)`. Neither exists (T-087 / T-089). M1 therefore uses
hidden compatibility persistence following the precedent of `public.ppiq_definition_versions`:

```
public.ppiq_plant_relationships
public.ppiq_plant_relationship_members
public.ppiq_plant_relationship_paths     (created, unused; T-058 owns it)
```

`source_definition_id` / `_version` are **plain columns, zero FK**. T-095 owns convergence.

**No EF entities — deliberate.** The store is raw Npgsql (T-073 precedent). Result:
`PlantProcessDbContext`, `IPlantProcessDbContext` and EF configurations were never touched, and
no EF entity leaks a table name into the domain.

**No authoring endpoint — deliberate.** Chapter 3 has none: relationships are *emitted* by
publishing a transformation definition. A `POST /api/relationships` would be a temporary M1
contract M2 must delete. Publication is an internal seam (`IRelationshipPublicationService`).

**Frozen refusal catalogue (all four, even though T-057 exercises none):**

```
RL01 ambiguous path      RL02 unproven relationship used by an automated consumer
RL03 no path             RL04 retirement blocked by an active dependent
```

**Runtime certification** publishes through the seam and reads back through the service — never
by SQL insert. 4/4 on `ppiq_presentation`, cleaned up after itself.

**Defects found and fixed inside T-057:**

- `W1-T057-01` — a guard read the SQL header comment that *explains* why there is no FK to
  `definition_store`, and reported the explanation as the violation. Fixed by stripping comments
  before absence checks.
- `W1-T057-02` — **real product defect, found only at runtime.** `ReadPublishedAsync` bound its
  optional entity filter with `AddWithValue` and a `DBNull`. `AddWithValue` sends no type, and
  `(@entity IS NULL OR ...)` gives PostgreSQL no column to infer one from → `42P08`, every
  unfiltered read failed. **665 unit tests could not catch it** because the fake store filters in
  memory and no SQL is ever planned. Fixed with `NpgsqlDbType.Text`.
- `W1-T057-03` — pack generator wrote injected blocks with LF while writing new files with CRLF.

---

### 2.3 T-058 — Relationship resolver consumer · **CLOSED `c3573ae7`** (7 files, 1161 insertions)

**Chain:** `RelationshipJoinPlanner → RelationshipResolver → IRelationshipService → published model`.
Nothing above the persistence port names a table; the pack fails if `NpgsqlConnection`,
`NpgsqlDataSource`, `IRelationshipStore`, `SELECT `, or a table name appears in the resolver,
planner or tests.

**Semantics that matter:**

- **RL01 collects every shortest path, not the first found.** Finding one and stopping is how a
  two-path model quietly becomes a one-path answer. Ordering is (hop count, then relationship
  codes) so the refusal text never depends on dictionary iteration order.
- **A preferred path is one where every hop is preferred.** Two preferred paths are still
  ambiguous — marking both is the same decision twice, not a decision.
- **Reverse traversal swaps the columns.** A relationship is declared once, left→right; keeping
  the declared order while travelling back compares left key to left key — still rows, wrong
  rows. Dedicated test.
- **A refusal carries zero steps.** Half a join still executes and still returns numbers that
  look like an answer.
- Max 6 hops. Self-join (`from == to`) resolves with an empty path — says so rather than
  claiming a path exists.

**Runtime certification: 8/8 on ppiq_presentation**, including tenant isolation and
publish → unpublish → restore.

**`T058-F01` — DOWNSTREAM CONSTRAINT, MUST REACH WORKER 2:**

> A newly published relationship is `unproven`, and **M1 has no control that promotes it to
> `validated`** — that is the C6 validate action, which is not built. Therefore in M1 **every
> published relationship is unproven, and every automated consumer is refused by RL02.** Only
> `explore` may traverse. This is the contract working as frozen.
>
> Worker 2 building a cross-source consumer in T-059 with an automated purpose will meet RL02 on
> every relationship. He must know before he starts.

**RL04 is frozen in the catalogue and NOT implemented** — dependency-aware retirement needs a
dependent registry that does not exist.

**Defects inside T-058:** `W1-T058-01` (`CS8604`, `RefusalMessage` is `string?` passed to
`Assert.Contains`, found by reading the payload while waiting, not by running);
`W1-T058-02` (`$owned` vs `$Owned` — PowerShell variable names are case-insensitive, so the
warning loop emptied the owned-file list and the final report printed nothing);
`W1-T058-BUILD-01` (shared-worktree build collision, environmental).

---

### 2.4 T-060 — Relationship Browser · **IMPLEMENTED, NOT COMMITTED, BLOCKED**

Four files ready and proven through section 3 of the pack every run:

```
NEW  Frontend/PlantProcess.Web/src/api/relationships/relationships.api.ts
NEW  Frontend/PlantProcess.Web/src/pages/Relationships/RelationshipBrowserPage.tsx
NEW  Frontend/PlantProcess.Web/src/pages/Relationships/__tests__/RelationshipBrowserPage.test.tsx
MOD  Frontend/PlantProcess.Web/src/App.tsx   (one lazy import + one route)
```

Latest pack: `Apply-T060-RelationshipBrowser-v4.ps1`
SHA256 `F3875B6491FDD81C3802EC0087A34C8CE66465C734BB8A459CFB00A1289B22FB`
Source fragments are byte-identical across v1→v4; only gates changed.

**Anchors (verified):** `App.tsx` SHA256 `0F1A4AE16DA1CF0A268DE7D1FB7C46B30A5FB49DB87A3AC468C22B0F45936231`,
895 lines. Lazy anchor `const SourceImportPrepPage = lazy(() =>`; route anchor is the
`/workspace/:dashboardCode` line at 479.

**House conventions measured:** `apiClient.get<T>(path, params?, options?)` from `@/api/http`
(module is `src/api/http/apiClient.ts`, not `src/api/http.ts`); tests are vitest +
testing-library with `vi.mock` on the api module.

**Read-only by construction:** the api client has only `get` (self-check fails on
`apiClient.post/put/delete`), and a test asserts by role that no publish/validate/promote/
retire/delete/edit/save/create button exists.

**T058-F01 is displayed honestly** — one sentence: published, followable while exploring,
refused to automated consumers under RL02. No promote button, no invented state.

**Blockers and findings:**

- `T060-F01` — `src/hardening/routeContracts.ts` declares `workflowRouteContracts` and **nothing
  imports it**; all five matches in the tree are inside the file itself. `/relationships` was
  deliberately NOT registered there: an entry in a list nobody reads looks like coverage while
  providing none.
- `T060-F02` — **`npm run lint` is not a gate anyone can pass.** The tree carries 54 pre-existing
  eslint errors. `scripts/lint-ratchet.mjs` is the right instrument and prints
  `RATCHET NOT ARMED` because `lint-budget.json` does not exist; arming it commits a repo-wide
  budget, which is a frontend-owner policy decision. v4 replaced it with: zero for the three new
  files, delta for `App.tsx` (measured baseline `0 errors / 1 warning`).
- **Last observed blocker:** `npm run build` failed on `src/api/advancedAnalysis.ts` (three
  `TS7006`) — Worker 3's T-068 lane, mid-edit. It was fixed once and broke again within minutes.
  v4 has never been run to completion.

---

### 2.5 T-065 bridge — Pack A **GREEN, FROZEN, UNCOMMITTED** · Pack B **NOT WRITTEN**

#### The structural discovery that defined the whole task

```
/api/analysis-jobs        →  public.inspection_jobs
T-064 target columns      →  public.job_definitions + job_run_histories  (824/825/826)
```

The two tables share **no column, no FK, no link**. The only bridge is
`inspection_jobs.rule_json.engineJobCode` → `ml_learning_job_catalog_v1.job_code` → a hardcoded
`CASE` inside `ppiq_ml_run_learning_job_governed_v1` → `job_definitions.job_code`.

**That linkage is many-to-one and therefore unusable for per-job target state.** The handler
defaults `engineJobCode` to `"ML_PROCESS_VS_DEFECT"` when unspecified, so every analysis job that
does not name one lands on the same shared row. Karim's live query confirmed
`ML_PROCESS_VS_DEFECT | 3`. Writing target state there would let one analysis job overwrite
another's target.

**Ruling:** `inspection_jobs` gets M1 compatibility columns. No FK. T-106 owns convergence.

#### Pack A contents (all green)

**`828_t065_analysis_job_target_compatibility.sql`** — five nullable columns
(`target_definition_kind varchar(64)`, `target_definition_id uuid`,
`target_definition_version integer`, `target_version_policy varchar(20)`,
`target_parameters jsonb`), **zero FK**, plus three CHECK constraints restating the T-064
coherence rules in the database, plus a partial index for the JB04 guard. Replayable.

**`AnalysisJobClass.cs`** — exact, case-sensitive match from catalogue `job_type` to
`JobDefinitionType`. **Unknown ⇒ `null` ⇒ named refusal, never `Custom`.** A silent fallback
looks harmless today because every class is `Unconstrained`, and stops being harmless the moment
any class gains a rule. Deliberately not `Enum.TryParse` — a loose parse accepts `"99"` as a class.

**`AnalysisAwareJobTargetLookup.cs`** (Infrastructure) — the JB04 composite. The original
`JobTargetLookup` still answers for `job_definitions`; this adds `inspection_jobs` beside it and
unions distinct + ordinal-ordered. `JobTargetResolver.AssertNotTargetedByJobsAsync` is
**untouched** — it still calls one `IJobTargetLookup`, still raises the same JB04 error, and the
analysis job codes appear in the sentence it already builds.

*Why Infrastructure:* `IPlantProcessDbContext` exposes exactly one member —
`DbSet<JobDefinition> JobDefinitions` — with **no raw connection**, and `inspection_jobs` has no
EF entity. Reaching the compatibility table from Application would have meant widening the
persistence contract for a bridge T-106 deletes.

**DI composition (Karim's Ruling C):**

```
Application/DependencyInjection.cs      remove ONLY  services.AddScoped<IJobTargetLookup, JobTargetLookup>();
Infrastructure/DependencyInjection.cs   JobTargetLookup             → concrete scoped
                                        AnalysisAwareJobTargetLookup → concrete scoped
                                        IJobTargetLookup            → the composite
```

One runtime authority, no registration-order trick, Application never names an Infrastructure type.

#### Pack A final result (19 Aug 10:08)

```
tooling self-tests          GREEN
native runner both ways     success exit=0, failure exit=1
baseline build              --no-incremental, saidSucceeded=True
APPLY + semantic self-check GREEN
post build                  saidSucceeded=True
warning delta               baseline 1, post 1, introduced 0
828 apply / replay          exit 0 / exit 0
Application unit            700 / 700
T065 integration            9 / 9, zero skipped
```

Pack file: `Apply-T065-TargetBridge-A-v14.ps1`, SHA256
`179421C63EB42E02D472E4A435598F20DA777B22A63768C75A5E769C0B3452DF`
(v13 is `DC6A35E35DD40496282C72300A58CA061CD1B1A7E9C8489ADA0C793008EA13CB`; v14 added three
self-checks after a fixture-shape correction).

---

## 3. MEASURED FACTS — DO NOT RE-INVESTIGATE

### 3.1 Frozen persisted vocabulary

```
current_published        pinned
```

Sources: `824_t064_job_target_definition.sql`, `826_t064_target_parameters.sql`, and the EF
converter in `JobDefinitionConfiguration.cs`. **828 now matches exactly.**
The C# enum members remain `CurrentPublished` / `Pinned`.

### 3.2 Live catalogue values (queried 19 Aug)

```
ML_PROCESS_VS_DEFECT    -> MlParamsVsDefects
ML_PROCESS_VS_DOWNTIME  -> MlParamsVsDowntime
ML_PROCESS_VS_KPI       -> MlParamsVsKpis
ML_WEEKLY_OVERALL       -> MlWeeklyFull
```

`AnalysisJobClass.FromCatalogJobType` matches these exactly. **Frozen — do not change.**

### 3.3 T-064 contract surface

```csharp
JobTargetReference   { Kind, DefinitionId, VersionPolicy, PinnedVersion?, ParametersJson? }  + Validate()
ResolvedJobTarget    { Kind, DefinitionId, ResolvedVersion, PolicyApplied, ParametersJson? }
JobTargetOutcome     { NoTargetDeclared = 1, Resolved = 2 }        // no third state
IJobTargetResolver.ResolveAsync(JobDefinitionType jobClass, JobTargetReference? target, ct)
IJobTargetResolver.AssertNotTargetedByJobsAsync(DefinitionKind kind, Guid definitionId, ct)
IJobTargetLookup.JobCodesTargetingAsync(string targetDefinitionKind, Guid definitionId, ct)
JobTargetErrorCodes  JB01 JB02 JB03 JB04
JobTargetParameters.Normalise / IsValid / Require       // null and "{}" stay different
```

**JB01 and JB02 cannot fire today.** `DeclaredJobTargetClassPolicy` declares all nine
`JobDefinitionType` members `Unconstrained` (`RequiresTarget = false`, `PermittedKinds = null`),
and the file states why: which classes require a target is a product ruling nobody has made.
**Do not invent one to manufacture a test case.** JB03 is real and fires on: pinned version
absent from history, or present and unpublished.

**JB03 reading (from the resolver's own comment):** a pinned version that still exists and is
still published keeps resolving after a later version is published. Refusing it as "superseded"
would make pinning meaningless.

### 3.4 EF converter defect — Karim's ruling for Pack B

```csharp
value => value == "pinned" ? Pinned : CurrentPublished     // silently maps ANY unknown to CurrentPublished
```

Contradicts the frozen T-065 rule *unknown = refuse*. Ruling: extract **one** strict codec
(`Application/Jobs/Targeting/JobTargetVersionPolicyCodec.cs`), make the existing EF converter
delegate to it. Authority convergence, not a T-064 redesign.

### 3.5 Analysis Job endpoint anchors

`Backend\PlantProcess.Api\Endpoints\Analytics\AnalysisJobDefinitionEndpoints.cs`,
839 lines, SHA256 `81793024FBE00608E81CDC93B250C90D814BFECE49440B8CA7237263A2E4BC38`
— **verified unchanged since the 16 Aug export**, so an export copy is a valid anchor source.

Regions (line numbers relative to the file start):

```
 59  MapPost("/",  CreateDefinitionAsync)        225  CreateDefinitionAsync   255 INSERT
 62  MapPut("/{code}", UpdateDefinitionAsync)    290  UpdateDefinitionAsync   307 UPDATE
 65  MapPost("/{code}/run", RunDefinitionAsync)  339  RunDefinitionAsync      480 UPDATE (post-run)
589  DefinitionSelectSql (17 columns)            632  ReadDefinition (16 args)
791  AnalysisJobDefinitionRow (16 members)       813  CreateAnalysisJobDefinitionRequest (12)
827  UpdateAnalysisJobDefinitionRequest (11)
```

`RunDefinitionAsync` parses `rule_json` for `windowDays`, `engineOutcomeKey`, `engineJobCode`
(default `"ML_PROCESS_VS_DEFECT"`), `grain`, then calls
`public.ppiq_ml_run_learning_job_governed_v1(@jobCode, @windowDays, 20, false)`.
**The resolver must be called before that governed call.**

### 3.6 Other measured facts

- `definition-options` **already exposes** `outcomeKey`, `displayName`, `outcomeType`, `grain`
  from `ml_outcome_definitions`. `/api/ml/foundation/outcomes` returns six more
  (`outcome_group`, `unit`, `normalization`, `taxonomy_json`, `version`, `status`).
  **Karim's ruling: the four are sufficient; Part A is a Worker-3 consumer swap, zero backend change.**
- Integration tests resolve `PPIQ_TEST_CONNECTION_STRING` first and **default to `ppiq_app`**.
  Every certification must inject the presentation connection string and assert
  `conn.Database == "ppiq_presentation"`.
- Frontend `tsconfig.json` is solution-style (`"files": []` + references), so plain
  `npx tsc --noEmit` checks **zero files** and always returns zero errors. Use `npx tsc -b`.
- `inspection_jobs` full column list (no target columns before 828): `id`,
  `inspection_job_code`, `inspection_job_name`, `inspection_type`,
  `source_correlation_run_id`, `parameter_code`, `defect_type`, `site_id`, `equipment_id`,
  `rule_json`, `schedule_expression`, `is_enabled`, `honest_state`, `last_run_at_utc`,
  `last_run_status`, `last_result_json`, `description`, `is_synthetic`, `source_system`,
  `source_record_id`, `created_at_utc`, `updated_at_utc`, `is_deleted`, `deleted_at_utc`,
  `deleted_reason`.

---

## 4. EVERY TEST RUN — DO NOT RE-RUN

| Suite | Result | When |
|---|---|---|
| Application unit (PR-050-01 A) | build clean, no owned warnings | 17 Aug |
| Application unit (PR-050-01 B) | 665 / 665 | 17 Aug |
| Application unit (PR-050-01 C) | 637 / 637 | 17 Aug |
| PR-050-01 live certification v3 | 13 / 13 checks green on ppiq_presentation | 17 Aug |
| Application unit (T-057) | 656 / 656 (incl. 18 T-057) | 17 Aug |
| T-057 runtime certification | 4 / 4 on ppiq_presentation | 17 Aug |
| Application unit (T-058) | 683 / 683 (incl. 18 T-058) | 18 Aug |
| T-058 runtime certification | 8 / 8 on ppiq_presentation | 18 Aug |
| T-060 vitest | **never reached** — blocked at `npm run build` | 18 Aug |
| Application unit (T-065 A) | **700 / 700** | 19 Aug |
| T-065 integration (Pack A) | **9 / 9**, zero skipped | 19 Aug |
| 828 apply / replay | exit 0 / exit 0 | 19 Aug |

**Unit count drift is normal and explained:** 637 → 656 → 683 → 700 as each task adds tests, plus
one historical 665 reading taken while another lane's test files were transiently present in the
shared worktree. That 665 delta was **never proven** and Karim ruled it must not be attributed.

---

## 5. THE FIFTEEN DEFECTS — AND WHAT EACH ONE TEACHES

**Thirteen were in pack tooling. Two were in product code.** Both product defects were found only
by running against a real database.

### Product

| ID | Defect | Why unit tests missed it |
|---|---|---|
| `W1-T057-02` | `AddWithValue` + `DBNull` on an optional filter ⇒ `42P08`, every unfiltered read failed | the fake store filters in memory; no SQL is ever planned |
| `W1-T065-14` | 828 persisted `CurrentPublished`/`Pinned` while `job_definitions` persists `current_published`/`pinned` | the canonical CHECK constraint was the only thing that could see both stores |

`W1-T065-14` is the more serious of the two: two compatibility stores spelling one concept two
ways means the retirement guard compares them and **never matches** — reporting no dependents
while a dependent exists. A protection that is present and wrong is harder to notice than one
that is absent.

### Tooling — the recurring pattern

Every one of these is the same mistake: **a gate that assumed something about its own instrument
without proving it.**

| ID | Assumption that was false |
|---|---|
| `W1-T057-01` | a text scan reads code (it read the comment explaining the scan) |
| `W1-T057-03` | injected blocks inherit the file's line endings |
| `W1-T058-01` | `RefusalMessage` is non-nullable |
| `W1-T058-02` | `$owned` and `$Owned` are different variables |
| `W1-T060-01` | absence of `validated"` means no invented state (it hit `case "validated":`) |
| `W1-T060-02` | `$Args` is available as a parameter name; a `try` around apply protects later stages |
| `W1-T060-03` | `Undo-All` called twice is harmless (noisy) |
| `W1-T060-04` | `npm run lint` can pass |
| `W1-T065-01` | a base class needs no import assertion |
| `W1-T065-02` | absolute zero is right for a file that already had warnings |
| `W1-T065-03` | `Start-Process -Wait` waits for the process (it waits for the **pipes**, which MSBuild worker nodes hold open) |
| `W1-T065-04` | only the post-apply build needs a rival check |
| `W1-T065-05` | `-PassThru` without `-Wait` still surfaces `ExitCode` |
| `W1-T065-06` | a `HashSet` survives a PowerShell `return` (it unwraps: `$null` / scalar / `Object[]`) |
| `W1-T065-07` | process presence means build activity (six nodes had been idle for hours) |
| `W1-T065-08` | an incremental baseline emits the same warnings as a full build |
| `W1-T065-09` | `Start-Process` gives psql a usable exit code |
| `W1-T065-11` | **a log with no error lines means a clean build** |
| `W1-T065-12` | a revert with a missing backup can skip silently |
| `W1-T065-13` | `.backup` and `.logs` can live inside the folder the operator cleans |

**`W1-T065-11` is the most dangerous of the entire session.** The log directory was deleted
mid-run, the build log was empty, and the gate reported `Build clean` and `introduced 0` **from
nothing**. It was only caught because psql happened to trip over the same missing folder. Had the
folder been deleted a minute later, the pack would have proceeded to commit on a build nobody read.

---

## 6. PACK FRAMEWORK — THE RULES THAT NOW EXIST

Carry every one of these into the next pack. They were each bought with a failed run.

**Structure**

- Section 0: **tooling self-test before any expensive work.** Prove the warning-signature
  machinery (0/1/2 entries, `Contains` works, line/column are not identity) and prove the native
  runner in **both** directions (success on a working query, non-zero on a missing table).
- Ancestry gate by SHA256 for every file whose prior content is known; anchor-uniqueness for
  files another lane may have moved.
- Anchors must verify both that the anchor is unique **and that the text being inserted is not
  already present**.
- Deletion by verified span: both boundaries unique, the span must contain a token proving what
  it is, and its length inside a measured envelope.
- `Undo-All` is idempotent, names every file it could not restore, prints the exact
  `git checkout HEAD -- "<file>"` for each, and throws.
- Backups live **outside** `tools\packs\` — at `tools\.ppiq-restore\` — and the path is announced
  before the first write.
- Every stage (apply, gates, tests) sits inside a `try` that calls `Undo-All`.

**Judgement**

- **Files created ⇒ absolute zero warnings. Files modified ⇒ delta against a baseline measured
  minutes earlier on this tree.** Warning identity is `file | code | message | project` —
  never line or column, because an insertion legitimately moves an existing warning.
- Both baseline and post builds use `--no-incremental`. Two measurements taken different ways
  are not a delta.
- A build is clean only when the log **says** `Build succeeded`. No verdict in the text is
  UNKNOWN and **fails closed**.
- For native executables (`psql`), `$LASTEXITCODE` is the authority and an absent code fails
  closed. Never infer native success from the absence of MSBuild-style error lines.
- TRX is the authoritative test result. **A skipped proof is not a proof** — assert a minimum
  executed count and require `other = 0`.
- Guards assert **positive facts** about owned regions. Negative text scans run against
  comment-stripped text, and two negative scans in one guard set is itself the warning sign.

**Execution**

- Never `Start-Process -Wait` with redirected streams. Use `-PassThru` +
  `WaitForExit(timeoutMs)`, set `MSBUILDDISABLENODEREUSE=1`, and give every tool a ceiling
  (build 25 min, tests 25, psql 5).
- Rival-build detection measures **CPU delta over 3 seconds**, not process count. Wait for an
  active rival, then STOP. **Never kill another worker's build.**
- All payloads normalised to CRLF on write, and verified (`LF count == CRLF count`).
- Avoid PowerShell automatic variable names as parameters: `$Args`, `$Profile`, `$Error`, `$Host`.
- No `git add .` / `git add -A` / `git clean` / `git reset --hard` / broad `git restore`.
  No auto-commit, ever. `dotnet clean` only on a proven stale-artifact condition.

---

## 7. KARIM'S RULINGS AND OPERATING PRINCIPLES — CARRY THESE FORWARD

**Product law**

- Layer A answers deterministic BI exactly; Layer B learns estimates. Never blurred.
- The aggregate engine carries **no** dashboard, widget or industry vocabulary.
- M1 may change **where** prepared rows come from; it must not change **what the product means.**
- The Presentation must not rely on fabricated provenance, fake job results, fake intelligence,
  fake security, or **temporary public contracts that M2 must replace**.
- One evidence authority. One relationship authority. One target authority. One lookup authority.
  A second one is always the wrong answer.
- Provenance of an aggregate ≠ a list of physical row IDs. It is the population predicate that
  produced it, plus the exact execution, plus deeper lineage when it genuinely exists.
- Frontend hiding is not enforcement.
- On licence downgrade: disable capability, **never silently delete customer data**.

**Process law**

- One task = one exclusive owner, from start to commit.
- File/subsystem lock beats frontend/backend labels.
- **Never build on an unstable prerequisite** — the T-064 lesson. A fast implementation that must
  be rewritten is not acceleration.
- Commit is the hand-off boundary. Never consume another agent's uncommitted worktree.
- Tests cannot certify a moving target.
- Findings are classified A (current mandatory) / B (smallest prerequisite) / C (existing future
  owner) / D (parking lot). **Never absorb C or D because you are already in the area.**
- STOP only for: missing final authority, prerequisite owned by a later task, completing now
  creates rework, files owned by another agent, a required fake contract, or unavailable
  canonical persistence. **A correct STOP beats a fast wrong implementation.**
- Design complete ≠ implementation complete ≠ runtime certified ≠ production certified.
- Ordinary compiler/test/tooling defects are **yours to fix autonomously** — do not return for
  permission.
- Do not send a design-only message when implementation was asked for.
- Serialized shared resources: `ppiq_presentation`, API `:5063`, **and the Backend `bin/obj`**.
- Evidence hygiene: an unproven attribution must be recorded as unproven, not asserted.

**Reporting**

- Commit hashes and file counts appear in a closure report only when Karim supplies them.
- Never fabricate a hash. Never claim a result that was not observed.
- Two lettered options when threads multiply; one next action otherwise.

---

## 8. BACKLOG POSITION (v2.10.4)

**Worker 1 M1 queue:** `PR-050-01 → T-057 → T-058 → T-060 → T-061 → T-062 → T-054 → T-055 →
T-056 → T-063`, then M2a canonical critical path after Presentation freeze.

| Task | Status |
|---|---|
| PR-050-01 | **CLOSED** `96460804` |
| T-057 | **CLOSED** `47ee7075` |
| T-058 | **CLOSED** `c3573ae7` |
| T-060 | implemented, uncommitted, blocked on the frontend build lane |
| T-065 bridge | Pack A green on disk, **uncommitted**; Pack B not written |
| T-061 / T-062 | not started — Mapping Health contract + page |
| T-054 / T-055 / T-056 | not started — Connections, dataset registry, import progress |
| T-063 | not started — genealogy surface convergence |

**Cross-agent edges live right now:** `W1 T-065 producer commit → W3 T-065 → T-066 → T-067`.
Worker 3 is blocked on nothing else.

**SQL script numbering:**

```
824 T-064 job target definition          825 T-064 kind varchar(64) convergence
826 T-064 target parameters              827 T-057 relationship compatibility
828 T-065 analysis job target            829 NEXT FREE
```

---

## 9. DEPLOYMENT, SERVER AND PIPELINE — **NOT WORKED ON**

**Nothing in this session touched deployment, the Hetzner server, Cloudflare, or Jenkins.**
No pipeline was made green. No app URL was fixed. Anyone reading points 9 and 10 of the handover
request should know this plainly rather than find an invented section.

What was **observed** (read-only, from the audit report and the tree):

- The audit report claims 77 signals / 20 CRIT. **Roughly 16 of the 20 CRIT are self-referential:**
  three copies of `GeneratePlantProcessIQ_UltimateAudit*.ps1` (v1, v2_2, v2_3) live in `tools\`
  and the scanner matches its own regex table three times over. `validate-real-ui-gates.cjs`
  hits are a deny-list (inverse polarity), and `CiPipelineTruthGateTests.cs:63` is the guard
  test's own method name.
- **Two CRIT signals are real:** `Frontend\...\tools\phase56\apply-phase5-phase6-full-ui-migration.cjs:74-76`
  is an in-tree generator that writes a Jenkinsfile stage containing exactly the three `--list`
  commands `tools\ci\validate-real-ui-gates.cjs` forbids; and `package.json:84`
  `"phase9:matrix": "... --list"` is an orphan script referenced by no Jenkinsfile.
- Dev-seed endpoints are **correctly gated** — verified at `Program.cs:41077-41083`,
  `if (app.Environment.IsDevelopment()) { app.MapDevSeedEndpoints(); }`, plus a release stub and
  a guard test.
- The 17 hardcoded-IP hits (`178.105.152.180`) are `${VAR:-default}` fallbacks or docs — config
  hygiene owned by **T-113**, not security.
- `env\profiles\presentation.env` sets `Users__0__IsBootstrapAdmin=true` with
  `ForcePasswordChangeOnFirstLogin=false`. This is the profile the Presentation Release runs
  under. **A product ruling is still owed on whether the customer demo runs as bootstrap admin.**
- The audit tool should exclude itself and the two older copies should be retired; otherwise
  every future run's numbers are non-comparable.
- `tools/packs/` is untracked and growing; `deploy/.ppiq-backups/` and 19 `playwright-report*`
  entries appear in the export (relates to open finding **W3-020**).

**Standing open items from before this session, untouched:** Hetzner VPS rebuild, GitHub PAT and
deploy-key rotation, `.de` domain registration pending the German employment-contract review.

---

## 10. WHAT PACK B STILL NEEDS — AND THE ONE OPEN DECISION

### 10.1 Scope (Karim's frozen ruling, already issued)

```
1  JobTargetVersionPolicyCodec  — ToStorage / FromStorage, unknown ⇒ explicit failure.
                                  Update the T-064 EF converter to DELEGATE to it.
2  Five fields on CreateAnalysisJobDefinitionRequest, UpdateAnalysisJobDefinitionRequest,
   AnalysisJobDefinitionRow  (TargetDefinitionKind, TargetDefinitionId,
   TargetDefinitionVersion, TargetVersionPolicy, TargetParameters)
3  DefinitionSelectSql 17→22 columns; ReadDefinition 16→21 args; INSERT and UPDATE
4  RunDefinitionAsync: load definition → engineJobCode → catalogue job_type →
   AnalysisJobClass → JobTargetReference → ResolveAsync → ONLY THEN the governed engine call
5  Executed identity recorded (see 10.2)
6  14 focused tests, Pack A's 9 stay green
```

Add fields as **trailing positional members with defaults** — this is what let PR-050-01 extend
`DashboardWidgetQueryResultDto` without touching 15 native sources.

### 10.2 THE OPEN DECISION — needs Karim before Pack B can be finished

**Requirement 8 says the run must record "which exact definition/version actually ran".
There is nowhere to put it.**

- `828` added columns for the **requested** target only.
- The run path updates `last_run_at_utc` / `last_run_status` on `inspection_jobs` and nothing else.
- `job_run_histories` has the T-064 executed columns but **is never written by the analysis-job
  path** — there is no link between an `inspection_jobs` row and a `job_run_histories` row.

Three options, all Karim's to choose:

**(a)** executed-identity columns on `inspection_jobs` — widens 828's scope
**(b)** write a `job_run_histories` row — creates a new link between the two stores
**(c)** report executed identity in the run response only, no persistence

Inventing a fourth is exactly what this bridge exists to prevent.

### 10.3 Also still needed

The tail of `RunDefinitionAsync` from its final `Results.Ok(...)` to the end of the method —
needed to attach executed identity to the response. Not yet read.

---

## 11. THE LESSON OF THIS SESSION

The product work was largely right the first time. Fifteen revisions were spent on gates, and the
same mistake produced almost all of them:

> **I kept choosing a measurement without first proving that the measurement measures what I think.**

`tsc --noEmit` returned zero because it checked zero files.
`npm run lint` could never pass because 54 errors predate everyone.
`Start-Process -Wait` waited on pipes, not processes.
An incremental baseline emitted no warnings for a project it skipped.
An empty log looked exactly like a clean build.

The rule that came out of it, in three parts:

> **Any measurement that returns zero must first prove it can return non-zero.
> Any gate must prove it can pass.
> Any verdict must prove it read something.**

The section-0 self-test in the T-065 pack is the first place all three are enforced, and it is the
only gate that worked correctly on its first attempt. **Every future pack should open that way.**

---

## 12. START HERE

```
1. Read §1.2 — Pack A is applied and uncommitted. DO NOT revert it. DO NOT rerun it.
   Verify with the seven SHA256 values in §1.2 before doing anything else.

2. Ask Karim to settle §10.2 (executed identity: columns / job_run_histories / response only).

3. Read the tail of RunDefinitionAsync from its final Results.Ok to the end of the method.

4. Build Pack B per §10.1 on top of the current green tree.
   Open it with the §6 section-0 self-test.

5. Combined certification: build → warning delta → 828 replay → 700+ unit →
   Pack A 9/9 → Pack B integration → architecture tests. Zero skipped.

6. Exact-stage Pack A + Pack B files only, git diff --cached --check, ONE commit,
   report the hash, release the Backend lane. Worker 3 is waiting on nothing else.

7. Then T-060: rerun Apply-T060-RelationshipBrowser-v4.ps1 when
   Frontend/PlantProcess.Web/src/api/advancedAnalysis.ts compiles (npx tsc -b, not --noEmit).

8. Then the M1 queue continues: T-061 → T-062 → T-054 → T-055 → T-056 → T-063.
```

**Do not re-run any test in §4. Do not re-investigate any fact in §3. Both were measured here.**

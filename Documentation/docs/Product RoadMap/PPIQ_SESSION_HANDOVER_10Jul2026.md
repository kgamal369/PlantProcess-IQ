# PlantProcess IQ — COMPLETE SESSION HANDOVER
### 09–10 July 2026 · "M1 execution + the assistant excavation"

**Author:** AI assistant (Claude), end of session
**For:** Karim Elsayed, solo founder/dev, SOU Industrial Software (Düsseldorf)
**Predecessor doc:** `PPIQ_Deploy_Pipeline_Handover.md` (26-Jun) — deployment/pipeline knowledge lives there and is **inherited, not re-verified** in this session. See §9/§10.

---

## 0. HOW TO USE THIS DOCUMENT

This exists so a new session starts **where we ended**, not on green field.

**If you are tempted to re-investigate, re-run tests, or re-query the database: STOP and read the relevant section first.** Every diagnostic below was actually executed and its output is recorded verbatim or faithfully summarised. Re-running them costs money and tells you nothing new.

**The single most important sentence in this document:**

> **In this codebase, an artefact existing — even with passing tests — implies nothing about it being reachable, registered, or ever executed. Grep for the *registration*, never for the class.**

Four separate proofs of this are recorded in §5.

---

## 1. WHAT THIS SESSION DELIVERED (all gated, all committed unless noted)

| # | Work | Outcome |
|---|---|---|
| 1 | **M1-08** global phase-token eradication | 12 canonical routes renamed + reverse redirects; nav, labels, loading strings, lazy consts; strict no-allowlist gate |
| 2 | **Step 1** repo-wide mojibake repair (frontend) | 119 lines / 13 files; `noMojibake` gate created |
| 3 | **Step 2** deep de-phase of the 8 customer-facing pages | `pages/Phase15` → `pages/Advisory`; `phase15Advisory.ts` → `advisoryApi.ts`; 47 `P15*` types stripped; titles/subtitles/test-ids/kickers |
| 4 | **Step 3a** `StandardPageHeader` + `StandardStatGrid` | Root cause of "Median107" found and fixed; `Mode` card dropped; one headline stat promoted per page |
| 5 | **Step 3b** honesty pass | Sample-data disclosure badge; `demo-approver` → logged-in user; `/license` demo route + page removed; kickers stripped from 5 more pages; gate widened repo-wide |
| 6 | **Gate-hole fix** | `<strong>P08</strong>` and `aria-label="Phase 7 and Phase 8…"` removed; gate now scans rendered text and accessible names |
| 7 | **M1-06 Option A** Data Integration IA | `/data-integration/*` layout route + 5 children; Connector Truth stopped fabricating rows; Admin slimmed; `?adminTab=` redirects |
| 8 | **M1-11** one assistant | `AssistantChat` mounted into the routed `AssistantRuntimePage`; orphan `GroundedAssistantPage` deleted; citations clickable |
| 9 | **M1-10** LogPanel | Server-side `q=` word filter; new `/admin/system-logs` hourly tail with path-traversal guards; source toggle |
| 10 | **roleAccess repair** | Map repointed to canonical routes; fail-open default → explicit deny; gate for phase tokens in string literals |
| 11 | **Phase3 fixture chain** | Four independent defects fixed; 3/3 green; **B1.3 key normalisation works for the first time** |
| 12 | **M1-07** RBAC matrix | `/api/assistant` registered with `assistant.use` |
| 13 | **M1-09** backend mojibake | 3 shipping strings repaired; `noMojibake` gate extended to `.cs` |
| 14 | **The assistant excavation** | Traced end-to-end. **It has never been able to answer.** See §6. |
| 15 | **Backlog v18 → v19 → v20** | v20 is current. M1 = 30 tasks / 128h / 18 Critical |

**NOT done, deliberately:** M1-09 (i18n, 14h). Untouched. It is a repo-wide extraction of exactly the strings this session repaired; doing it tired is how `t("Loading demo request...")` ships as a translation key.

**NOT done, and this is the headline:** *nothing built on 09-Jul has been opened in a browser.* Every pack was proven by `tsc` + `vitest` + `dotnet build`. Gates cannot see layout, colour, focus order, or whether a fetch returns. **Backlog v20 M1-23 exists for this and blocks the dress rehearsal.**

---

## 2. ENVIRONMENT TRUTHS (these contradict the docs — trust this section)

### 2.1 The local application database is NOT a container

```
ppiq_app  =  NATIVE Windows service  postgresql-x64-16  on port 5432
```

- `Get-Service | Where Name -like "*postgre*"` → `postgresql-x64-16  Running`
- `Test-NetConnection localhost -Port 5432` → `TcpTestSucceeded: True`
- The container **`ppiq-app-db` has been `Exited (255)` for weeks** while everything worked. It is a red herring. Ignore it.
- `docker ps` shows only the **six source emulators**: `ppiq-src-meltshop-postgres`, `ppiq-src-caster-oracle`, `ppiq-src-parsytec-mysql`, `ppiq-src-downtime-mysql`, `ppiq-src-hsm-oracle`, `ppiq-src-pkl-mssql` (+ `-init`).

### 2.2 Credentials (from `env/profiles/local.env`)

```
POSTGRES_HOST=127.0.0.1     POSTGRES_PORT=5432
POSTGRES_DB=ppiq_app        POSTGRES_USER=ppiq_dev
POSTGRES_PASSWORD=ppiq_dev_local_only
ConnectionStrings__PlantProcessDb=Host=127.0.0.1;Port=5432;Database=ppiq_app;Username=ppiq_dev;Password=ppiq_dev_local_only
PPIQ_SMOKE_USERNAME=e2eadmin    PPIQ_SMOKE_PASSWORD=E2EAdmin123!
```

**The DB user is `ppiq_dev`, NOT `ppiq`.** I wasted four of Karim's commands assuming otherwise.

### 2.3 Use `127.0.0.1`, never `localhost`

`psql -h localhost` resolves to `::1` (IPv6) and fails `pg_hba`. Always:

```powershell
$env:PGPASSWORD = "ppiq_dev_local_only"
psql -h 127.0.0.1 -U ppiq_dev -d ppiq_app -c "SELECT 1;"
```

`PGPASSWORD` is lost when you open a new PowerShell window. Set it again.

### 2.4 `ppiq.ps1 up` lies about readiness

It launches `dotnet run` in a **separate console window** and returns immediately, printing `API pid NNNN -> http://localhost:5063`. The API is not listening for another **~8 seconds** (plus ~36s of build). Foreground startup sequence, timed:

```
StartupConfigurationValidator passed  →  CORS bound  →  Auth bound (1 user, signingKeyLen=52)
→  "Applying pending EF Core migrations..."  (≈4s, even with nothing to apply)
→  "No migrations were applied. The database is already up to date."
→  Stuck-run reaper active (MaxRuntime=30min Interval=5min)
→  "Now listening on: http://localhost:5063"
```

**Two full sessions were lost treating this race as a startup crash.** `dotnet build` passing does NOT mean the host starts — minimal-API endpoints can throw at *map* time. **Always run `cd Backend\PlantProcess.Api ; dotnet run` in the foreground before declaring the API broken.** Backlog v20 **M1-11** fixes `ppiq.ps1` with a readiness poll.

### 2.5 `ppiq.ps1 down` removes the source containers

It does not just stop the API/web. Restart them with `ppiq.ps1 up-sources`.

### 2.6 The RBAC matrix is deny-by-default and runs BEFORE endpoint authorization

`Backend/PlantProcess.Api/Security/PlantAccessControl.cs`, static `Matrix` array, **longest-prefix match**, read once at startup.

- Unmapped path + POST → `403 {"message":"Endpoint is not mapped in the P01/P02 permission matrix."}`
- Unmapped path + GET → **falls through to `("/", GET, "anonymous", true)` and is served anonymously.** That asymmetry explains a whole class of confusing bugs.
- **Three endpoints were bitten by this in one session:** `/api/analysis-jobs`, `/analytics/phase2`, `/api/assistant`.
- `("/admin", All(), "tenant.admin", false)` covers `/admin/*` by prefix — a new admin endpoint needs **no** new matrix line.
- **If you see a 403 you don't understand, read the response body.** It names the matrix. It is not your `[Authorize]` attribute.

Existing permissions (do not invent new ones): `anonymous`, `license.admin`, `tenant.admin`, `source.configure`, `job.manage`, `analysis.execute`, `page.design`, `assistant.use`, `report.export`.

### 2.7 PowerShell / execution

- Execution policy blocks unsigned scripts. Always: `Unblock-File .\X.ps1` then `powershell -ExecutionPolicy Bypass -File .\X.ps1`.
- Karim's shell is **PS 5.1**. Pure ASCII, UTF-8-no-BOM via `[System.IO.File]::WriteAllText`, CRLF, no `&&`, cuddled `} else {`.

---

## 3. THE WORKING METHOD (keep this — it works)

All code changes are delivered as **"apply packs"**: self-contained PowerShell scripts written by the assistant, placed in outputs, run by Karim. Every pack:

1. **Preflight** — verifies every anchor string exists. One miss → **zero changes**, exit 1.
2. **Backup** — copies each target to `deploy\.ppiq-backups\<pack>-<stamp>\`.
3. **Apply** — anchored string replacement, never blind regex over whole files.
4. **Self-check** — asserts the intended change happened and nothing else did.
5. **Gate** — `dotnet build` and/or `npx tsc -b` and/or `npx vitest run src/test/architecture/`.
6. **Auto-revert** on any gate failure.

Read-only **collector scripts** (`Collect-*.ps1`) bundle source files into one uploadable `.txt` so the assistant reads real code instead of guessing. This is what prevented most of the damage. **Use them.**

---

## 4. EVERY TEST RUN AND ITS RESULT (do not re-run these)

### 4.1 Frontend architecture suite (`npx vitest run src/test/architecture/`)

| When | Files | Tests | Result |
|---|---|---|---|
| after M1-08 global | 10 | 26 | **green** |
| after Step 1 (mojibake) | 11 | 27 | **green** |
| after Step 2 (de-phase) | 11 | 31 | **green** (1 red first attempt — see §5.2) |
| after Step 3a | 11 | 31 | **green** (3 red attempts first — see §5.3) |
| after Step 3b | 11 | 36 | **green** |
| after gate-hole fix | 11 | 38 | **green** (1 red first — found `DemoAnalyticsPages`) |
| after M1-06 IA | 12 | 44 | **green** first run |
| after M1-11 assistant | 13 | 49 | **green** first run |
| after M1-10 LogPanel | 13 | 49 | **green** |
| after roleAccess | 14 | 55 | **green** |
| **full frontend suite** | **55** | **232** | **green** |

### 4.2 Backend

| Command | Result |
|---|---|
| `dotnet test ... --filter Phase3GoldenThread` | **3/3 green** (after four fixes, §5.6) |
| `dotnet test Backend\tests\PlantProcess.Api.IntegrationTests` | **665 total, 0 failed, 590 succeeded, 75 skipped** |
| `dotnet build PlantProcess.Api` | green (19–21 warnings, pre-existing) |

**Note on the 590 vs 587:** before the Phase3 fix the suite was `587 passed / 3 failed`. After: `590 / 0`. The two tests I briefly feared were "new failures" (`ppiq_validate_genealogy_graph`, `MappingLifecycleProof`) were pre-existing, unmasked by a chain that had never run far enough to reach them, and both resolved once `690` was applied. **Do not re-investigate them.**

### 4.3 Live database queries actually executed (results are facts, not guesses)

```sql
-- B1.3 key normalisation — WORKS, for the first time
SELECT ppiq_normalize_business_key('coil','C-0044170');  -- 44170
SELECT ppiq_normalize_business_key('coil','44170');      -- 44170

-- alias collisions: none
SELECT normalized_alias_code, count(DISTINCT material_unit_id)
FROM material_aliases WHERE COALESCE(is_deleted,false)=false
GROUP BY 1 HAVING count(DISTINCT material_unit_id) > 1;   -- 0 rows

-- material_aliases is tiny
SELECT count(*) FROM material_aliases;                     -- 21 rows, 0 to backfill

-- the plant timezone is unanimous
SELECT plant_time_zone_id, plant_utc_offset_minutes, count(*)
FROM material_units GROUP BY 1,2;                          -- Europe/Berlin | 60 | 38345

-- material_units NOT NULL without default (what any INSERT must supply)
id, material_code, material_unit_type, site_id,
plant_time_zone_id, plant_utc_offset_minutes,
created_at_utc, is_synthetic, is_deleted

-- canonical genealogy: 35,906 edges / 38,346 materials
SELECT * FROM ppiq_validate_genealogy_graph();             -- 865 rows

-- the transition coil, as it really is
C-0044170 : HeatToCoil 0.70 (transition) + HeatToCoil 0.30 (transition) = 1.0   -- NO slab edge
C-0044171 : HeatToCoil 1.00 = 1.0
S-0044170 : no edges

-- the assistant's evidence base
SELECT to_regclass('canon.assistant_chunk');               -- exists
SELECT is_synthetic, is_stale, count(*) FROM canon.assistant_chunk GROUP BY 1,2;  -- 0 rows

-- the mapping proof has been mutating for a long time
SELECT status, count(*) FROM ppiq_mapping_versions GROUP BY 1;  -- Published 37, RolledBack 31
```

### 4.4 The assistant probe (the decisive result)

```powershell
POST http://localhost:5063/api/assistant/ask   (as e2eadmin, after M1-07)
→ 500
  "No service for type 'PlantProcess.Application.Assistant.AssistantService' has been registered."
```

**The stack trace proves M1-07 worked**: the request traversed `AccessControlMiddleware.InvokeAsync` (`PlantAccessControl.cs:303`) and reached the handler. No 403. The 500 is the *next* defect. See §6.

---

## 5. ROOT-CAUSE FINDINGS — THE EXPENSIVE KNOWLEDGE

### 5.1 "Median107" was never a CSS bug — the UI was never built

Every one of the 8 advisory pages rendered its stats as:

```jsx
<div key={card.label}>
  <span>{card.label}</span><strong>{card.value}</strong>
</div>
```

Bare elements, **no class, no style** — while `cardStyle` and `buttonStyle` consts sat declared at the top of each file and were **applied zero times**. Grep proved it: `0` occurrences of `style={` across `pages/Phase15/`.

Fix: `StandardStatGrid` (`<dl>`, label above value, tabular numerals, one promoted headline figure) + `StandardPageHeader`. Both live in `src/components/standard/`.

### 5.2 A `count=1` replace leaves the second occurrence

`ScenarioSimulationPage.tsx` carried `Pack G · T-096` **twice** — a file-header comment and the JSX kicker. Step 2's regex was line-anchored and replaced once. The gate caught it. **When eradicating a token, replace *all* occurrences and scan comments too.**

Related: some pages inline the kicker **inside the tag** (`<p className="phase9-eyebrow">P09 · T-051…</p>`), so `^[ \t]*P09` never matches. Match text nodes, not lines.

### 5.3 The four "guard matches its own prose" failures

**This bit me four times in two days and it is the single most instructive pattern here.**

| Where | The trap |
|---|---|
| Step 3a | Self-check asserted `-not Contains('StandardStatGrid')` — but the pack had just injected `<StandardStatGrid …/>`, so the guard was always false and the **import merge was skipped**. 16 × `TS2304`. |
| Phase3 seed | Self-check asserted `-not Contains('SlabToCoil')` — the explanatory comment the pack inserted *contains* the word `SlabToCoil`. Correct edit, reverted by its own prose. |
| M1-06 | The new `ConnectorTruthPage` comment quoted `"MeltShop PostgreSQL"` verbatim; the gate greps that directory for exactly that string. |
| M1-11 | The page comment named `/api/assistant/ask`; the gate greps `src/` for that literal — and then the **gate file itself** matched. |

**Rule:** a guard that can be satisfied by the fix's own text is not a guard. Build assertion strings from fragments (`"/api/" + "assistant/ask"`), test the *SQL row* or the *import statement* rather than the word, and exclude `src/test/architecture/` from scans it performs.

### 5.4 PowerShell traps that cost real time

| Trap | Symptom | Fix |
|---|---|---|
| **Variables are case-insensitive** | `$role = ReadAllText($Role)` clobbers the path in `$Role`; `Copy-Item` then treats TypeScript source as a drive name | Suffix content vars (`$roleSrc`) |
| **`Set-StrictMode -Version 2.0`** makes property access on an **empty array** a hard error | `$unsafe.File` throws when `$unsafe = @()` | `@($unsafe \| ForEach-Object { $_.File })` |
| **`Get-ChildItem -Recurse -Include *.ts`** silently returns **nothing** unless the path ends `\*` | a *false clean* scan | filter on `$_.Extension`, and hard-fail if 0 files scanned |
| **`[regex] '^…$'` is not line-anchored** in .NET without `Multiline` | preflight never matches | `[regex]::new(pattern, [RegexOptions]::Multiline)` |
| `$last = Matches('^import .*$')` | matches `import {` — the **first line of a multi-line import** — and splices text inside the braces | `^import[^;]*;` (imports contain no `;` before their terminator) |
| **`.NET` cp1252 maps the 5 undefined bytes to C1 controls; Python's strict cp1252 refuses** | a mojibake "repair" that *succeeds* and produces different junk | see 5.5 |

### 5.5 The mojibake repairer can lie

Algorithm: `cp1252-encode → utf8-decode`, repeated until "clean". Loop exit condition was **"no markers remain"** — a *negative* test, run on its **own output**.

`Program.cs:775` held a single em-dash re-encoded **six or seven times** into ~2,000 characters. The repairer round-tripped it into *different* junk (`ÔÇÖ┬á┬á…`) that contained none of the `Ã/Â/â` markers, declared victory, and wrote it. **The gate — same regex, run against the file on disk — caught it immediately.**

- **129 dirty lines in the frontend: 116 round-tripped cleanly, 13 were lossy** and *all 13 were decorative comment banners*. Zero lossy lines in code or JSX. Those 13 were rewritten as ASCII banners.
- **Backend: 755 `.cs` files scanned.** Substance: **two SQL identifier exception messages** (`MySqlConnector`, `MsSqlConnector` — these land in `job_log` and logs) and **one API response row** (`"Let's Encrypt certificates issued"` in `Phase2PilotReadinessEndpoints.cs`). The comments were noise; those three were the point.
- **`Phase2PilotReadinessEndpoints.cs` has MIXED line endings** (mostly LF, one CRLF). Three revisions of my pack damaged it three times: split-and-rejoin flipped ~300 lines; "surgical" replace still rewrote terminators; the assertion I added to catch that used a `(?<=\n)` lookbehind that cannot distinguish `\r\n` from `\n`. **Hand-edit files with mixed endings. The pack should refuse them.**
- The repo has **no `.gitattributes`**, so `core.autocrlf` decides per-machine. That is *why* the file drifted. One commit, its own diff. **Do not smuggle it into an encoding fix.**

### 5.6 The Phase3 fixture chain — four defects, each masking the next

`EnsurePhase3Sql` applies `320`, `321`, `322` and `seed/010`. All four were broken.

1. **`310` and `320` both `CREATE TABLE IF NOT EXISTS public.ppiq_business_key_definitions`** — with **zero overlapping columns**. `310` = a business-key *dictionary* (`key_code`, `entity_scope`, `version_number`, read by `312`/`313`). `320` = key *normalisation rules* (`key_type`, `strip_alpha_prefix`, …). `310` runs first; `320`'s `CREATE` silently no-ops; its `INSERT` dies `42703` thirteen lines later. **Fix:** renamed `320`'s table to `ppiq_business_key_rules` (5 refs, **no data migration — it had never existed**).
2. **`320`'s DDL takes `ACCESS EXCLUSIVE` unconditionally.** `ALTER TABLE … ADD COLUMN IF NOT EXISTS` locks *before* checking existence. It blocked against the test suite's own connections → 30s Npgsql timeout. (Backlog M2 has the lock-light fix.)
3. **`seed/010` never learned about two `NOT NULL` columns** added later to `material_units`: `plant_time_zone_id`, `plant_utc_offset_minutes` → `23502`. Values: `'Europe/Berlin'`, `60` — *measured*, not chosen.
4. **The seed's `ed002` edge made a transition coil's provenance 2.0.** The `550_v5_p06_blended_provenance` trigger groups by `child_material_unit_id` **alone** and correctly refused it.

**THE ARCHITECTURAL FACT recovered — put this in Identity & Topology:**

| Layer | Purpose | Weights? | Trigger? |
|---|---|---|---|
| `genealogy_edges` | **weighted provenance ledger** | yes | yes — `sum = 1.0` per child, **across all relationship types** |
| `canonical_genealogy_edges` | **structural graph** (the walk) | no | no |

`ppiq_golden_thread` / `ppiq_walk_genealogy` read **only the canonical layer** (`321`'s header says so). A transition coil is 70/30 from two heats and carries **no `SlabToCoil` edge**; its coil→slab hop lives in the canonical layer (`ce0002`). *Structure and attribution are different statements about the same steel.* The schema knew that; the fixture didn't.

**Consequence worth stating:** the golden thread and 70/30 attribution have been *demoed as verified* while CI could not verify them. The product code was correct throughout. The fixture chain was not.

### 5.7 `690` was never the loaded definition

`ppiq_validate_genealogy_graph` is defined in **`312`** (uncast, `m.material_key`) **and `690`** (correct, `m.material_key::text`). `migrate` applies `scripts/*.sql` sorted by **name**, so `690` should win. The live DB had the **uncast** body → `42804` on the first returned row → **865 diagnostics nobody had ever seen**. Applying `690` by hand fixed it. *Why it hadn't applied is unexplained.*

`ON_ERROR_STOP=1` stops the **file**, not the **run**. `migrate` prints "applying N migrations", one aborts halfway, the rest still run, the output scrolls. **A broken migration is indistinguishable from a working one unless you go looking.**

### 5.8 Demo machinery found *inside* the product (Golden Rule violations)

| Where | What |
|---|---|
| `/license` | `<h1>Phase 10 License Runtime Demo</h1>` — a **duplicate** of the real `/commercial/license`. **Removed.** |
| `/suggestions` (dead route) | Declared twice in `App.tsx`; the second was unreachable and rendered `DemoAnalyticsSuggestionsPage`. **Removed.** |
| `pages/Assistant/GroundedAssistantPage.tsx` | A page, tested, **routed by nothing**. **Deleted.** |
| `AdminPageContent.tsx` | Rendered `DemoAnalyticsWorkflowTruthPage` **as a tab inside Administrator** |
| **Connector Truth** | Called the real API and, when it returned nothing, **rendered hardcoded rows**: `"MeltShop PostgreSQL"`, `schemaFingerprint: "pending"`, `driftStatus: "Tracked"`. **Fabricated plant status, on a tab named *Truth*, inside the page you demo.** Replaced with an honest empty state. |
| `RecommendationsPage` | `approverUserId: "demo-approver"` — **an approval audit trail recording a fabricated approver.** Now `user?.userName ?? "unknown"`. |
| Value Realisation / Recommendations | "Reload demo request" fetches the page's **only data**. Could not be deleted; **relabelled "Load sample request" + an amber SAMPLE DATA badge.** |

### 5.9 The role-access map was fiction

`src/security/roleAccess.ts` keyed on `/phase8/suggestions`, `/phase8/assistant-config`, `/connectors`, `/mapping`, `/material-investigation` — **routes that no longer exist, and two that never did.** And:

```ts
const capability = routeCapabilityMap[path] ?? "QualitySummaryView";
```

**An unmapped path fell back to a capability every role and every tier holds.** Unknown route → allowed to everyone.

**But I checked before I claimed:** `routeDecision` has exactly two consumers — `PersonaAccessMatrixPage` (a read-only display) and its test. **Nothing enforces it.** Real authorization is `AccessControlMiddleware`. So it was **not a live security hole** — it was a page showing a customer an access matrix describing routes that don't exist, defaulting unknown ones to "allowed". Fixed: keys repointed, deny-by-default, page now states enforcement is server-side.

**Do not "fix" this by wiring `routeDecision` into route guards without deciding to.** That's a feature (M2-15).

---

## 6. THE ASSISTANT — READ THIS BEFORE TOUCHING ANYTHING

### 6.1 The finding

**`POST /api/assistant/ask` has never worked. Not once. It could not have.**

```
[nothing]  →  AssistantRetrievalIndexBuildService  →  ReindexAsync  →  canon.assistant_chunk (0 rows)
                ↑ never called                                                 ↓
                                                            SearchAsync returns []
                                                                     ↓
                                              AssistantService refuses every question
```

Four separate breaks, discovered in this order:

1. **`AddAssistant()` is never called.**
   `Backend/PlantProcess.Infrastructure/Assistant/AssistantInfrastructureExtensions.cs` defines it and registers *everything*: `IEmbedder → LocalSemanticEmbedder`, `IRetrievalIndex → NpgsqlRetrievalIndex`, three `ITool`s (`FetchFindingTool`, `OpenSuggestionTool`, `RunKpiTool`), `ToolRegistry`, `IAssistantModel` (real if `Top15ModelEndpointConfig.FromEnvironment().IsConfigured`, else `ExtractiveAssistantModel`), and `AddScoped<AssistantService>()`.
   `Select-String "AddAssistant\("` across `Backend` returns **one hit: the definition.** Not `Program.cs`, not a test.
   Meanwhile `Program.cs:1078` maps the endpoint. → `500: No service for type 'AssistantService' has been registered.`

2. **The chunk producer does not exist.**
   `NpgsqlRetrievalIndex.ReindexAsync(request, ct)` takes `request.Chunks` as **input** and writes them. Its only caller, `AssistantRetrievalIndexBuildService.RefreshAsync(tenantId, canonicalChunks, …)`, **also takes them as input**. *Its* only caller is `P4_ProductionAssistantTests.cs:31`.
   **Nothing in the backend reads canonical data and emits `RetrievedChunk`.** The doc comment says *"The caller supplies canonical chunks from findings, KPI definitions, reports or docs."* **There is no caller.**

3. **Nothing triggers a reindex.** `AssistantEndpoints.cs`'s own summary says *"grounded ask + admin reindex"*. It maps **only** `/ask`.

4. **Three assistant surfaces ship in parallel:**
   ```
   Program.cs:1077  app.MapV5AssistantGatewayEndpoints();     → V5AssistantGatewayService  (registered :718, mapped)
   Program.cs:1078  app.MapAssistantEndpoints();              → AssistantService           (NOT registered)
   Program.cs:1079  app.MapPhase8AssistantRuntimeEndpoints(); → ?
   ```
   **The frontend calls the second.** The old backlog card said *"wire V5AssistantGateway to ONE live provider"* — that would have left the chat exactly as dead.

### 6.2 How the retrieval actually works (so you don't guess)

```sql
SELECT id, source_kind, source_ref, content, embedding_json, scope_role
FROM canon.assistant_chunk
WHERE tenant_id = @t AND is_synthetic = false AND is_stale = false
```

- Then **cosine similarity in C#** against `LocalSemanticEmbedder.Embed(question)`. **No pgvector index, no `ORDER BY` in SQL.** Every non-stale chunk for the tenant is pulled into memory per question. Fine for hundreds; catastrophic for hundreds of thousands.
- **Role scoping** is enforced twice: SQL `scope_role`, and C# `RoleRank = viewer < operator < engineer < admin`. A chunk whose `scope_role` outranks the caller is dropped. **Getting `scope_role` wrong is a data-leak bug, not a UX bug.**
- `HandleFor(kind)` recognises exactly: `"finding"` → `ProvenanceHandle.Finding`, `"report"`/`"doc"` → `DocumentSection`, **anything else → `Dataset`**.
- `IsSynthetic` chunks are filtered out **twice** (in `RefreshAsync` before insert, and in `SearchAsync` SQL). Mark chunks synthetic "just for the demo" and you'll spend a day proving retrieval is broken. It isn't; you told it to hide them.
- Nothing ever **deletes** stale rows (there's a purge command at `NpgsqlRetrievalIndex:93` — check whether it is reachable). Left alone the table grows one dead generation per reindex.

### 6.3 The response contract (frontend depends on it exactly)

`AssistantEndpoints.cs` returns:

```json
{ "isRefusal": bool, "refusalReason": string, "text": string,
  "citations": [{ "kind": "...", "id": "...", "detail": "..." }],
  "blocked": ["..."] }
```

`AssistantChat.tsx` renders precisely these. Types now live **only** in `src/api/assistantApi.ts` (M1-11 removed the duplicates), and `assistantChain.test.ts` **fails the build** if anything outside `assistantApi.ts` fetches the assistant endpoint.

### 6.4 What happens the moment you add `AddAssistant()`

`/api/assistant/ask` returns **200 with `isRefusal: true` on every question**, because `canon.assistant_chunk` is empty. `ExtractiveAssistantModel` answers **only from retrieved evidence** — no LLM, no egress, no fabrication.

That is **honest, on-message for an evidence-grade product, and indistinguishable from a working assistant that lacks evidence.** A CEO would ask three questions, get three polite refusals, and conclude the product can't answer anything.

**This is a decision, not a task:** demo the assistant as an abstaining surface (with framing), or cut it from the rehearsal. Recorded in Backlog v20's change log.

### 6.5 Sequence (Backlog v20)

| ID | Task | Est |
|---|---|---|
| M1-06 | `builder.Services.AddAssistant();` | **0.5h** |
| M1-07 | **Build the canonical chunk producer** (dataset + doc chunks are available *today*; finding chunks are blocked behind M1-02) | **10h** |
| M1-08 | Admin reindex endpoint + refresh after each analysis run | 2.5h |
| M1-09 | Consolidate to ONE assistant surface | 2h |
| M1-24 | End-to-end proof: cited answer, honest refusal, blocked claim, role-scoping leak test | 2.5h |

Each carries **five numbered notes** in the backlog description. Read them.

---

## 7. IDENTITY & TOPOLOGY — CORRECTIONS OWED (doc is due v5)

1. **`ppiq_app` is a native Windows service (`postgresql-x64-16`), not a container.** `ppiq-app-db` has been `Exited(255)` for weeks. The docs imply Docker.
2. **DB creds are `ppiq_dev` / `ppiq_dev_local_only`.**
3. **`genealogy_edges` vs `canonical_genealogy_edges`** — weighted ledger vs structural graph. §5.6.
4. **`("/api/assistant", All(), "assistant.use", false)`** added to the Matrix table.
5. `migrate`'s `ON_ERROR_STOP` stops the *file*, not the *run*.
6. **`PPIQ-DEMO-023` is used as the id of two different `DeploymentChecklistRow`s** in `Phase2PilotReadinessEndpoints.cs`. A copy-paste bug. If anything keys on that id, one row shadows the other.

**Roadmap v7** was the starting point. This session executed M1-08, M1-06(A), M1-10, M1-11, M1-07, M1-09, the 8-page UI rebuild and the Phase3 chain — i.e. most of M1-P2's *user-visible* surface. It did **not** touch M1-P1's keystone (generic canonical projector, 16h) or i18n (14h). **M1-P1 grew from 46h to 54.5h** because the assistant moved into it.

---

## 8. BACKLOG STATUS (v20 is current — `PPIQ_Product_Backlog_v20.xlsx`)

| Milestone | Tasks | Hours | Critical |
|---|---|---|---|
| **M1** | **30** | **128.0** | **18** |
| M2 | 60 | 321.5 | 19 |
| M3 | 27 | 234.1 | 3 |
| M4 | 25 | 218.1 | 4 |

**M1 phases:** P1 = 11 tasks / 54.5h / 9 Critical · P2 = 11 / 47h / 4 · P3 = 8 / 26.5h / 5

**Sorted** Backend → Frontend → Testing → Docs, criticality within, so a file is touched once.

### Removed as done in v20
- `M1-07` RBAC matrix (v19) — proven by the 500's own stack trace
- `M1-09` backend mojibake (v19)

### Rewritten
- **`M1-06`** was *"API does not answer on :5063."* **It answers fine.** Now a 0.5h `ppiq.ps1` readiness poll.
- **Old `M1-08`** — *"wire V5AssistantGateway to ONE live provider, 3h."* **Every clause false.** Replaced by four tasks (15h).
- `M1-26` grounding eval — now depends on the assistant chain.

### The three CI truth-gates this repo lacks (already M2-10/11/12; **M2-11 extended to functions**)
1. Every seed `INSERT` covers its table's `NOT NULL`-without-default columns. *(Would have caught defect 3.)*
2. **No two numbered scripts may `CREATE` the same table OR FUNCTION.** *(Would have caught defects 1 and §5.7.)*
3. `migrate` must fail **loudly, per file**, with an OK/FAIL summary.

### Open decisions (nobody has ruled)
- **`src/phase11/`** — four tested modules imported by nothing. `phase11UiState` duplicates `StateRenderer`; `phase11StandardControlContract` duplicates the T11 gate's regex as a typed contract. **Wire or delete. Do not rename** — renaming makes the duplication tidier.
- **`DemoAnalyticsPages`** — still lazily imported by `App.tsx` for three other pages; `DemoAnalyticsPages.tsx` is a thin re-export that `noThinReExports` currently passes.
- **Three assistant surfaces.** Which is the product?
- **`Phase1WorkflowTruthEndpoints.*` — 22 files**, one per method (`…Helpers.022.NormalizeCode.cs`). Almost certainly generated, or a response to `largeFileBoundaries`. **A gate you wrote to keep files readable produced 22 files harder to read than one.** Understand *why* before renaming.
- Backend `// P6-01`, `// P6-03`, `PPIQ-DEMO-*` — the naming sweep is wider than `pages/Phase8`.
- **`.gitattributes`** — the repo has none.

---

## 9. DEPLOYMENT / SERVER / PIPELINE — **INHERITED, NOT VERIFIED THIS SESSION**

> **I did no deployment or pipeline work in this session.** Nothing below was re-tested on 09/10-Jul. It is carried forward from `PPIQ_Deploy_Pipeline_Handover.md` (26-Jun) so it isn't lost. **Treat it as last-known-good, not current.**

### 9.1 Last-known state (26-Jun)
- Jenkins job `plantprocessiq-deploy` reached **`PIPELINE GREEN` / `Finished: SUCCESS`** on build **#96** (commit `94b8fb4f`), again after frontend fixes (`ec165699`).
- Stage flow: checkout + ensure-env → sweep → backend tests → frontend tests → e2e (gated off) → app DB migrate+seed → demo sources (gated off) → build + recreate stack + health gate → presentation smoke (sysadmin login + Enterprise activation).
- Health gate: internal `GET http://plantprocess-api:5063/health` → 200 → `== DEPLOY GREEN ==`.
- UI live at `https://app.178.105.152.180.sslip.io`, sysadmin auto-login working.

### 9.2 Two Docker Compose projects — **NEVER MERGE**
- **`plantprocessiq`** = INFRASTRUCTURE (sacred): `ppiq-jenkins`, `ppiq-caddy` (binds `0.0.0.0:80/443`), backup.
- **`ppiq-app`** = APPLICATION deploy: `plantprocess-postgres` (volume `ppiq-app_plantprocess-postgres-data`), api, web; network `ppiq-app_plantprocess-private`; api+web also joined to `ppiq-edge` → `plantprocessiq_ppiq-net` so infra Caddy can reach them.
- **Why the rename mattered:** when the app deploy used project name `plantprocessiq`, the deploy reaped infra containers.

### 9.3 Known tech debt (as of 26-Jun)
- The live `ppiq-caddy` Caddyfile still routes `app.*` → **`plantprocess-app-web`** and `website.*` → **`plantprocess-website`** — **stale container names.** The Caddyfile is a read-only bind-mount whose host source does not exist on disk. **Pending:** re-establish a persistent host bind and correct the targets.
- `https://api.…sslip.io/health` returns **401 externally** while internal `/health` is 200. Expected, not a bug.

### 9.4 The rule that must never be violated
**Do not delete `/var/lib/ppiq-preserve/.env`.** The generator reuses it to keep the Postgres password stable. Deleting it forces a new password that will not match the existing volume → `28P01 password authentication failed`. If you must regenerate `.env`, wipe `ppiq-app_plantprocess-postgres-data` **in the same operation**.

### 9.5 Jenkins agent (DooD)
Jenkins runs **inside** `ppiq-jenkins`. The agent has **no dotnet, no node, no npm.** It runs toolchains as sibling containers:
```
docker run --rm --volumes-from $(cat /etc/hostname) -w "${PWD}" <image> sh -lc "..."
```
Bind-mount sources resolve on the **host** daemon; container paths like `/var/jenkins_home/...` do not exist there.

### 9.6 Sysadmin credentials
`sysadmin` password is generated per fresh `.env`, stored in `/var/lib/ppiq-preserve/FIRST_LOGIN.txt` inside `ppiq-jenkins`, and as `PPIQ_SMOKE_PASSWORD` in `deploy/compose/.env`.

### 9.7 What this session's work implies for the pipeline (untested)
- **`M1-07` changed `PlantAccessControl.cs`.** The Matrix is read at startup — a redeploy is required for `/api/assistant` to be reachable on the server.
- **`M1-10` added `GET /admin/system-logs`**, which reads `AppContext.BaseDirectory/logs/systemlog_YYYYMMDDHH.log`. **In the container, that directory must exist and be writable, and the API must have been running long enough to produce a file.** Not verified on the server.
- **New frontend routes** (`/data-integration/*`, `/assistant`, `/advisory/*`) and **removed** routes (`/license`). Any e2e or smoke step that hits an old path will fail. **Backlog M1-25 (E2E realignment) covers this.**
- The mojibake and `.gitattributes` situation means a **Linux CI checkout may normalise line endings differently from Karim's Windows box.** Watch for spurious diffs.

---

## 10. WHAT WAS DONE TO MAKE THE PIPELINE GREEN / THE APP URL WORK

**Nothing, this session.** That work was completed in the 26-Jun session and is documented in `PPIQ_Deploy_Pipeline_Handover.md`. Its summary: the green pipeline came from (a) separating the two compose projects so the deploy stopped reaping infra, (b) baking `VITE_*` vars into the frontend **build** rather than runtime, (c) correcting the API base URL derivation, (d) allowing the sslip host in CORS, and (e) gating the Ed25519 dev license key behind `PPIQ_PRESENTATION=on`.

**If the next session is asked to touch the pipeline: read that document first, and re-verify §9.3's stale Caddy routes before trusting any URL.**

---

## 11. RULES, MANDATES AND WAYS OF THINKING (Karim's — honour these)

1. **Zero preamble. No flattery. Honest defect surfacing. Never claim done when not done.**
2. **Solution Doctrine:** permanent, committed, generic, product-grade fixes only. Never temp workarounds. Never per-machine env vars to boot. **Never skip or loosen an assertion to go green.**
3. **Autonomous Generic-Fix Mandate:** diagnose and fix every bug at source, generically, without asking permission. *"Make it green"* is forbidden.
4. **Preventive-Maintenance Mandate:** never wait for a failure. Read the entire path up front, enumerate every stage, surface **all** defects in one pass before running.
5. **Naming Golden Rule:** never use phase/task/version/pack codes in artifact names — descriptive only. Numeric ordering prefixes for SQL migrations are *functional tokens* (preserve); phase labels are not (strip).
6. **PPIQ is ONE generic, industry-agnostic product.** No demo pages. No hardcoded dataset. Demo = emulated external sources only. **A page that fabricates data when the API is empty is the worst violation of this rule, and one shipped.**
7. **Two admin types, never conflated:** `sysadmin` (SOU internal, auto-provisioned, undeletable, customer never sees) vs **Customer Admin** (manual commissioning, never auto-created).
8. **Two Docker Compose projects on the server are deliberate.** Never merge.
9. **Evidence before cure.** Karim's own instinct, and it was right every time: he refused to commit red, he stopped at a `RESET` prompt that would have dropped 38,345 `material_units`, and he spotted the stale `/phase8/` keys in `roleAccess.ts` that my gate could not see.
10. **`-WhatIf` / dry runs before writes.** Every destructive pack got one.

---

## 12. FOR THE NEXT SESSION — DO THESE, IN THIS ORDER

1. **`git status`.** Confirm `M1-09`'s hand-fixes to `Program.cs` and `Phase2PilotReadinessEndpoints.cs` are committed and the `noMojibake` gate is green.
2. **`builder.Services.AddAssistant();`** in `Program.cs` (v20 **M1-06**, 0.5h). Converts `/assistant` from a DI stack trace into an honest refusal. **Do this before the browser render check**, or M1-23 measures a 500.
3. **v20 M1-23 — browser render verification.** Nine surfaces. *Nothing built on 09-Jul has been seen.* This blocks the dress rehearsal.
4. Then the assistant chain (M1-07 chunk producer, 10h) **or** cut the assistant from the 23-Jul demo. **That decision cannot be deferred much longer.**

### Things you must NOT do
- Do not re-run the frontend or backend suites to "check" — the results are in §4.
- Do not re-investigate `ppiq_validate_genealogy_graph` or `MappingLifecycleProof`. §4.2.
- Do not `reset-app-database.ps1` without knowing whether anything regenerates the **38,345 `material_units`**. It probably does not.
- Do not chase `V5AssistantGateway` for the chat. §6.1.
- Do not rename `pages/Phase8` / `phase8-ai.css` separately — the className is bound to the stylesheet; both move in one commit (M2-32).
- Do not "fix" the empty assistant by seeding `canon.assistant_chunk` by hand. **A chunk with no canonical row behind it is a fabricated citation** — the exact thing Connector Truth was doing.

---

## 13. SCORECARD — HONEST STATE AT END OF SESSION

| Area | Before | After | Note |
|---|---|---|---|
| Phase/pack tokens on the demo path | everywhere | **eradicated + 5 gates** | internal dirs/CSS remain (M2-32) |
| Mojibake | 119 frontend + 6 backend files | **0, gated on `.ts/.tsx/.css/.cs`** | repairer can still lie (§5.5) |
| 8 customer-facing pages | unstyled, squashed, phase-named, fabricating | **StandardStatGrid, honest, disclosed** | none rendered in a browser |
| Data Integration IA | a tab inside Administrator | **its own area, 5 routes, redirects** | `/prepare` still outside the layout |
| Connector Truth | **invented `"MeltShop PostgreSQL"`** | honest empty state | |
| Approval trail | `demo-approver` | **logged-in user** | |
| Assistant | UI + endpoint + client + tests | **still cannot answer** | 15h of real work, §6 |
| Golden thread / 70/30 attribution | live but **unverified by CI** | **3/3 green** | four fixture defects |
| B1.3 key normalisation | **did not exist** | `C-0044170` → `44170` | first time ever |
| Genealogy diagnostics | **never returned a row** | 865 diagnostics visible | triage = M1-10 |
| Browser verification | — | **zero** | **the biggest open risk** |
| Deployment / pipeline | green (26-Jun) | **untouched, unverified** | §9 |

**The gates caught six of my own mistakes across two days.** Not one reached the working tree. That is the argument for tightening gates, never loosening them — and for the fact that **a checker which validates its own output is not a checker.**

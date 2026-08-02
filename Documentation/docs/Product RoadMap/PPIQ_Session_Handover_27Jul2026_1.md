# PPIQ — SESSION HANDOVER
## 27 July 2026 · the working session that closed M1-04 and rebuilt the backlog

**Read this before doing anything. It exists so the next session does not re-investigate what is already settled, does not re-run tests that already have answers, and does not repeat four specific mistakes that each cost a full gate cycle.**

---

## 0. PROVENANCE — HOW MUCH TO TRUST EACH CLAIM

| Tag | Meaning | Trust |
|---|---|---|
| `[RUN]` | A command was executed in this session and its output is quoted or summarised | **Highest.** This happened. |
| `[REPO]` | Read directly from the 27-Jul 09:13 audit snapshot, or from a file Karim pasted | **High**, but the snapshot is from 09:13 and the tree changed after |
| `[DOC]` | From one of Karim's governing documents | **High within its date.** Later date always wins |
| `[INFERRED]` | A conclusion drawn here by cross-reading evidence. Reasoning always shown | Check the reasoning |
| `[UNVERIFIED]` | Nobody has confirmed this | **None.** Ask or verify |

**The single most important framing rule, given by Karim and it governs everything:**

> Documents are a **learning curve**, not a contradiction. A 21-Jul document describing a design, and a 25-Jul document describing a different one, are not in conflict — the first produced a bug, the second records the fix. **Read by date. The later date wins. Never quote an earlier document against a later one.**

I broke that rule once in this session: I corrected the backlog using v29, when the Authoring Layer Specification of 27-Jul had already superseded the clause I was citing. Do not repeat it.

---

## 1. WHAT THIS SESSION ACTUALLY DID

Eight apply packs, all gated green, all committed by Karim. In order:

| # | Pack | What it did | Gate |
|---|---|---|---|
| 1 | `Apply-PpiqRefactorRegistry-Retire-T036.ps1` | Removed a dead registry entry blocking Pack B | tsc + 253 tests ✅ |
| 2 | `Apply-PpiqWidgetBuilder-Delete-B.ps1` *(pre-existing, unblocked by #1)* | Deleted the 15-file retired wizard tree | tsc + arch suite ✅ |
| 3 | `Apply-PpiqWidgetAuthoring-C2.ps1` | Client method for `/widgets/execute`, result mapping, CSS fix | tsc + 253 ✅ |
| 4 | `Apply-PpiqWidgetExpression-Persist.ps1` | Backend contracts + service so a query expression persists and validates | dotnet build ✅ |
| 5 | `Apply-PpiqWidgetExpression-SaveAndDraw.ps1` | Frontend: save the expression, reload it on edit, draw from it | tsc + 253 ✅ |
| 6 | `Apply-PpiqCanvas-RefuseIllegalWiring.ps1` | **M1-04** — five refusals at drop + one at run time, each a sentence | tsc + 253 ✅ |
| 7 | `Invoke-PpiqWidgetRowCensus.ps1` | The measuring instrument, not a change | — |
| 8 | `Apply-PpiqWidgetMeasureTruth.ps1` | Implemented `observationCount`, fixed 30 literals, added the executable-measure guard | dotnet build ✅ |

Plus one hand-run `UPDATE` against `ppiq_presentation` (5 rows) and one call to the template repair endpoint (20 rows).

**Net outcome: widgets went from 10 of 38 silently empty to 38 of 38 carrying data.**

---

## 2. THE BIGGEST FINDING — READ THIS EVEN IF YOU READ NOTHING ELSE

### 2.1 "Roughly half the widgets return no data" was never a data problem

It was **three code defects**, none of which any test could see, **because all three answered HTTP 200 with an empty result set**.

**Defect A — a measure published with no implementation.** `[RUN]`
`DashboardMetadataCodes.Measures.ObservationCount = "observationCount"` was declared, published by the metadata endpoint, accepted by the validator and offered in the authoring panel. The executor's switch had **ten arms and it was not one of them**. The only reference to that constant in the entire backend was its own declaration. It fell to `_ => Array.Empty<DashboardAggregateRow>()`. **Five widgets.**

> The 21-Jul validation recorded it as *"registered: CODE-VERIFIED and RUNTIME-VERIFIED, all 200s."* Both were true. **Neither means it returns data.** This is the most instructive sentence in the whole project.

**Defect B — the validator and the executor disagreed about case.** `[RUN]`
Canonical codes are camelCase (`materialCount`, `riskScore`, `dataQualityIssueCount`). `EnsureSystemTemplatesAsync` seeded them **PascalCase**. A C# string switch is case-sensitive; the validator is case-**insensitive**. So the request was accepted (200) and then matched nothing. **Five more widgets.**

**Defect C — the repair path confirmed the defect.** `[REPO]`
`EnsureTemplateAsync` decided whether to repair using `StringComparison.OrdinalIgnoreCase`, under which `MaterialCount` **equals** `materialCount`. The idempotent repair already in the product looked at every broken row, judged it correct, and moved on. **Fixing the literals alone would have fixed nothing already in the database.**

**And the file that caused it:** `Backend/database/scripts/070_fix_system_template_widget_codes.sql` — a script whose name says it *fixes* the widget codes and which **writes the broken PascalCase values**. Three places agreed on the wrong answer.

### 2.2 What fixed it, in order `[RUN]`

1. Pack 8 replaced 30 literals in the seeder with the canonical constants, made the repair comparison `Ordinal`, implemented `ExecuteObservationCountAsync`, and added an `ExecutableMeasures` guard that returns a **named 400** instead of an empty 200.
2. `POST /analytics/dashboard/definitions/system-templates/repair` → **`repaired: 20`**
3. A hand `UPDATE` on `ppiq_presentation` → **`UPDATE 5`** (the non-system dashboards the C# repair never touches, because `GetDashboardsAsync` excludes system templates by default)
4. Census → **38 OK, 0 EMPTY, 0 REJECTED**

### 2.3 THE OPEN LOOP — DO THIS FIRST `[UNVERIFIED]`

**`070_fix_system_template_widget_codes.sql` still writes the broken codes.** A clean rebuild reinstalls all five defects and the census goes red. It is `M1-07` in Backlog v31, 1h, and it must also be **renamed** (a file that claims to fix what it breaks is persona A1 criterion 4).

---

## 3. EVERY TEST RUN AND ITS RESULT — DO NOT RE-RUN THESE

### 3.1 The widget row census `[RUN]` — four runs, and the sequence is the evidence

| Run | Time | OK | EMPTY | REJECTED | ERROR | What changed before it |
|---|---|---:|---:|---:|---:|---|
| 1 | 18:27 | 28 | 10 | 0 | 0 | baseline |
| 2 | 20:22 | 33 | 0 | **5** | 0 | measure-truth pack + repair endpoint |
| 3 | 20:26 | **38** | **0** | **0** | **0** | the hand `UPDATE` on 5 rows |

Run 2 is worth understanding: EMPTY fell to zero and REJECTED rose to five. **That is the new guard working** — five widgets that had been silently empty for weeks became a named failure in one run.

**The by-measure rollup at run 3:** `dataQualityIssueCount` 2/0, `riskScore` 6/0, `observationCount` 5/0, `avgParameterValue` 3/0, `defectRate` 5/0, `defectCount` 8/0, `materialCount` 9/0 — **all healthy.**

**Row counts worth carrying forward** `[RUN]`: 97 is the common trend length. `PO_KPI_DEF` 98. `PA_*` group at 29 and 19. `EO_OBS` 8, `PO_KPI_OBS` 36. **Seven widgets return exactly 1 row** — `DQ_BY_SOURCE`, `DQ_BY_TYPE`, `EO_EQDEF`, `QM_SEV`, `RI_KPI`, `RI_TREND`, `RI_EQUIP`. That is `M1-12` in v31: a one-bar bar chart reads as broken even when honest.

### 3.2 Build and test gates `[RUN]`

| Gate | Result | Note |
|---|---|---|
| `npm run test` | **62 files, 253 tests, all passing** — run 5 times today | Duration varied 203s → 888s → 227s. Same tests. Machine load, but a 15-minute gate stops being run |
| `node node_modules/typescript/bin/tsc -b` | Clean after every pack | One genuine failure, see 4.3 |
| `dotnet build` | Succeeded. **27 warnings on a full build, 4 on incremental** | All pre-existing: `CS0618` obsolete `CorrelationService`, `CS8603` ×3 in `AuthLifecycleTests` |
| Architecture suite | 15 files, 56 tests, green | Ran standalone during Pack B |

### 3.3 Facts established by query, not by assumption `[RUN]`

- `POST /auth/login` with `{userName, password}` returns `accessToken`. Credentials live in `env/profiles/presentation.env` as `PPIQ_SMOKE_USERNAME=e2eadmin` / `PPIQ_SMOKE_PASSWORD=E2EAdmin123!`. **`admin` with an empty password returns 400** — that is the frontend's fallback, not the API's account.
- `GET /analytics/dashboard/definitions` returns **12 dashboards, 38 widgets**, and **excludes system templates by default** (`GetDashboardsAsync(includeSystemTemplates: false)`). This is why the C# repair fixed 20 rows the census could not see.
- **`MODEL_INSIGHTS` EXISTS and works** — `MI_RATE` 97 rows, `MI_SEV` 4 rows. `[REPO]` It appears in **no committed script**: zero occurrences across the seed, the database scripts and the backend source, and `EnsureSystemTemplatesAsync` creates five templates without it. **So the type-3 page works today and would vanish on a clean rebuild.** I earlier told Karim it did not exist; that was wrong and is corrected here.
- `[REPO]` **Two surfaces import ReactFlow**, not one: `VisualJoinCanvasPage` and `AnalysisToolboxPage`.
- `[REPO]` **11 C3/C4 facades exist**; Pack B deleted 2, leaving 9. `largeFileBoundaries.test.ts` asserts `facades.length > 0`, so Pack B was two files away from a red suite for a non-obvious reason. **Checked before running it.**
- `[REPO]` **318 of 2102 files** carry a phase/task/version token in their name, plus **16 directories**.
- `[REPO]` `shiftCode` **is** a registered dimension. The group-by change should work. **Never tested.**

---

## 4. THE FOUR MISTAKES THAT COST A GATE CYCLE EACH — DO NOT REPEAT

These are the highest-value paragraphs in this document. Each was a real failure in this session.

### 4.1 A guard must be scoped to the region it judges

Pack 8's self-check asserted the new `ExecuteObservationCountAsync` did not exclude null values. It scoped from the method name **to the end of the file** — and matched `ExecuteParameterAggregateAsync` immediately below, which contains `NumericValue != null` **legitimately**, because an average must exclude nulls.

The code was correct. The guard failed it. **A guard whose scope is too wide judges its neighbour's code.**

**Rule to add to the pack contract:** find the region's start, find the next declaration, judge only what is between them.

### 4.2 An anchor must be unique, and a longer string is not the fix

Pack 5's first run refused: the anchor `sourceSystem?: string | null;\n  sourceRecordId?: string | null;` appeared **three times** — `DashboardDefinitionRecord`, `DashboardWidgetDefinitionRecord`, `CreateDashboardWidgetDefinitionPayload`.

**The fix was structural, not longer text:** find the interface by name, find its closing brace, insert before it. A shared tail cannot confuse that.

**And the failure bought a defect.** It exposed that `CreateDashboardWidgetDefinitionPayload` needed the field too — and it **would have compiled clean without it**, because `dashboardingApi` delegates through `(...args: any[])` which type-checks nothing. The interface would have been a lie no gate could catch.

### 4.3 Hoisting an object literal loses TypeScript's contextual typing

Pack 5's gate failed with `TS2322`. Moving `options` out of the call site widened `sortDirection: "desc"` from the literal type to `string`, and `SortDirection` is `"asc" | "desc"`. Inline it was fine; hoisted it was not. **Fix: `"desc" as const`**, with the reason in a comment so nobody re-widens it.

### 4.4 A preventable condition must be caught in preflight, not by the gate

Pack 4's first run applied its changes, then failed `dotnet build` with **MSB3027 file locks** — the API was running. It reverted correctly, but it threw away a change the gate never actually judged.

**Fixed:** preflight now checks for a running `PlantProcess.Api` process **and** independently opens the built DLLs with `FileShare.None`, before backup and before any file is touched.

### 4.5 Two smaller ones worth carrying

- **A log that reports a suite as green when it never ran is the same disease as `--list`.** A gate line read `GATE RED (tsc=2 vitest=0)` — vitest never executed. It now says `vitest=not run`.
- **A diagnostic that hides the server's error message is useless.** The census reported "400 Bad Request" and nothing else, sending Karim guessing at credentials. It now prints the response body and exits rather than continuing unauthenticated — because carrying on would have reported every widget as EMPTY, which is a lie about the widgets.

---

## 5. CURRENT IMPLEMENTATION STATE

### 5.1 What is genuinely good `[REPO]` `[RUN]`

- **The preparation canvas** (`VisualJoinCanvasPage.tsx`) — typed ports, three-level tree with key markers, mode toggle, filters, derived columns, dry run, immutable publish with rollback, **and as of today five refusals at drop each writing a sentence plus one at run time naming an unjoined table**.
- **The widget authoring panel** — one surface for add and edit, **zero literals**, every list from `GET /analytics/dashboard/metadata`, compatibility narrowing server-driven, dimension and measure each optional per chart type, catalogue **and** query binding modes.
- **Query binding end to end** — write, run, inspect, save, render. The expression persists, reloads on edit, and the card draws from it.
- **The measure engine is now truthful** — an unservable measure returns a named 400 listing what it can serve, instead of an empty 200.
- **Server-side SQL compilation** — the client never composes SQL.
- **The safe-SQL layer** — parameterised predicates, operator whitelist, column validation, `rejected_by_safe_sql` as a first-class dry-run status.

### 5.2 What is built and dirty

| What | Why |
|---|---|
| `070_fix_system_template_widget_codes.sql` | Writes the broken codes. Name misrepresents content |
| `AnalysisToolboxPage` | Imports ReactFlow to lay out **three fixed blocks driven by dropdowns**. The appearance of an authoring surface with none of the behaviour |
| The widget card infers its category column | `columns.find(c => c.code === dimensionCode)`, then the first column not named `value`. For a three-column query it **guesses** |
| 24 backup folders + 31+ apply packs at the repo root | Persona A1 criterion 3 |
| Registry and executor as independent lists | Fixed for measures by the guard. **Dimensions still have the same shape** |

### 5.3 What does not exist and is claimed by the presentation plan

- **No SQL editor anywhere.** The canvas has a read-only SQL *view* of the server-compiled query.
- **No method palette.** Three fixed blocks, not draggable statistical/ML blocks.
- **No bind step.** A query's columns are never mapped to chart roles.
- **No debug log.** Refusals write to a single-line status element.
- **No S4 (ML authoring) surface at all.**
- **Widget save overwrites rather than versioning** (Specification 17.3).

---

## 6. IDENTITY, TOPOLOGY, ROADMAP

### 6.1 Facts `[DOC]`

| Item | Value |
|---|---|
| Owner | Karim Gamal, SOU Industrial Software, Düsseldorf |
| Repo root | `C:\Workspace\PlantProcess-IQ` |
| Local API | `http://localhost:5063` · frontend `5173` |
| PostgreSQL | `127.0.0.1:5432`, **native Windows service, not a container**. Use `127.0.0.1`, not `localhost` |
| Demo DB | `ppiq_presentation` · Dev DB `ppiq_app` |
| DB creds | `ppiq_dev` / `ppiq_dev_local_only` — **local dev only** |
| Server | `178.105.152.180` (Hetzner), app at `https://app.<ip>.sslip.io`, API at `https://api.<ip>.sslip.io` |
| Start command | `.\scripts\run\start-api.ps1 -Profile presentation -FreePort` |

### 6.2 The three permanent product rules `[DOC]`

1. **Generic only** — no plant, product, routing, schema, defect type or connection target hardcoded.
2. **Starts empty** — all data arrives via DB-link import.
3. **The 15-step journey** — 100% by end of M2.

Plus **Rule 4 step 6.2** (the join is declared once in S1) and **Rule 5.2** (licence tiers must be demonstrable — currently impossible, no tier-to-feature matrix exists).

### 6.3 The schema and data-flow contract — RULED THIS SESSION

Captured in full in `PPIQ_Schema_Topology_and_DataFlow_Contract_v2.md`. **Three rulings changed the architecture:**

1. **The relational model authored on the canvas is permanent.** The joins, FKs and PKs a user declares are the product's model of that plant *for the life of the installation*. Nothing downstream re-derives them. This is why A1.7 exists, stated from the data side.
2. **The customer's engineer authors it, not the vendor** — because he knows his data and the vendor cannot know every customer's schema. This is the strongest argument for the authoring shell and it should be the one used with a buyer.
3. **The engine is two layers with a compounding supervisor** — data-analysis jobs and AI/ML jobs, plus one premade job running nightly/weekly that adjusts coefficients across all of them. *"The jobs are hands and arms; the supervisor is why the product gets better at a plant the longer it runs."*

**Exactly three schemas:** Plant Data (starts empty, generic, holds raw data and engine outputs) · Meta Data (the only schema with non-plant data: layouts, roles, credentials, front-end design data, licensing, job logging; some tables prefilled, declared per table) · Dump Store (as it arrived, uninterpreted).

**The isolation rule:** no analytical surface may display a row that did not come from Plant Data. A widget that could read Dump Store would show a customer their own unmapped source columns and call it intelligence — **and it would look like it was working**.

---

## 7. KARIM'S RULES AND WAY OF WORKING — CARRY THESE

### 7.1 Absolute

- **No commit before he has verified in a browser.** He restated this today. Respect it; do not push a commit line into instructions.
- **Old-concept code is deleted, cleaned or fixed — never built upon.**
- **The headline score is the LOWEST persona score, never an average.**
- **Naming golden rule:** no phase code, task code, version code or bookkeeping label in any artifact name. Version is a separate field.
- **A fix that exists only as data does not exist.** Learned today: pair every data correction with the script change in the same commit.

### 7.2 Delivery mechanics

- **Always a PowerShell apply pack**, including for diagnostics. Never "paste this into DevTools".
- Pure ASCII. UTF-8 **no BOM**. CRLF for `.ps1`/`.cs`; **preserve whatever the file already uses** for `.tsx`/`.css` — forcing CRLF makes git report the whole file changed and buries the real diff.
- **No `&&` in PowerShell.** Cuddled `} else {`. Run from repo root.
- Invocation: `powershell -NoProfile -ExecutionPolicy Bypass -File .\Script.ps1`
- Pack contract: preflight → report (hash, timestamp, non-ASCII, line endings) → **anchor verify with diagnosis** → backup → apply → on-disk self-check → gate → auto-revert. Switches `-ReportOnly` and `-Revert`.
- **Guards:** strip comments before judging · copy the project's own regex verbatim · **scope to the region judged** · **structural anchors where a text tail is shared**.
- Gates: `node node_modules/typescript/bin/tsc -b` (**never npx** — it hangs) **and** `npm run test`. Delete `*.tsbuildinfo` first. `dotnet build` **with the API stopped**.

### 7.3 How he thinks

- **He pushes back on numbers he doesn't believe, and he is usually right.** Give him the decomposition, not the headline.
- **He reasons by analogy and the analogies are sound.** Test your position against them before defending.
- **He wants decisions reduced to a letter** — present exactly two named options and ask for (أ) or (ب). Not three.
- **He asks "what should I do now exactly"** when a response has too many threads. That means you over-delivered.
- **Deliberate cuts are decisions, not omissions** — write them down.
- He writes English with Egyptian-Arabic phrasing. He may switch to Arabic; match him. He said "مش فاهم" once and the right response was to re-explain in Arabic, simply.

---

## 8. BACKLOG STATUS

**`PPIQ_Product_Backlog_v31.xlsx` is authoritative.** 71 tasks, 532.5h, every phase inside the 40–65 band.

### 8.1 What changed v30 → v31

**Deleted:** the zero-row widget task — closed by measurement, 38/38.
**Also deleted:** create-or-cut the type-3 page — `MODEL_INSIGHTS` works; only reproducibility remains, moved to M2-P7.

**Moved M2 → M1** because the stated plan demonstrates them and they do not exist: SQL editor (10h), method palette (10h), bind step (6h). Each carries an explicit **or-cut clause in its own text**.
**Moved M1 → M2:** true XY scatter (10h), associative tri-state (6h) — impress beats, not stated requirements.

**New phase M2-P3 Schema Topology & Data Flow, 64h/5 tasks**, replacing one 16h line. Twelve of fourteen concerns were absent from it.

**Merged:** four verification walks folded into the consolidated pass; the narrative task absorbed both reframes; the cut register absorbed four new entries.

### 8.2 The decision v31 cannot make

**M1-P2 is 55h, of which 34 exist only because the plan needs things that aren't built.** If the presentation is inside a week, **do not build them** — cut the scope to two authoring surfaces and spend the time on M1-P1 and two rehearsals.

**`[UNVERIFIED]` The presentation date is still unknown.** It has been asked for three times. Every priority ordering depends on it.

---

## 9. DEPLOYMENT, SERVER AND PIPELINE

`[REPO]` **All of this was read, none of it was run.** No deployment, no pipeline execution, no server access in this session.

### 9.1 The pipeline

`Jenkinsfile`, 183 lines, **last modified 10 July 2026 12:48**. Nine stages: checkout preserving server secrets → sweep stale → **backend tests (blocking)** → **frontend unit (blocking)** → **e2e (blocking)** → migrate+seed → demo sources → deploy+health gate → presentation defaults. Stages 3–5 carry **no `when {}` clause**, enforced by `E2e_stage_cannot_be_gated_off`.

**Five problems it solves, each a scar:**
1. The agent has no `dotnet`/`node` → sibling containers via `--volumes-from "$(cat /etc/hostname)"` so the workspace appears at the identical path.
2. Backend tests need real Postgres → ephemeral DB on a throwaway network; **only the connection string goes to stdout**, diagnostics to stderr; readiness requires **two consecutive successful probes**.
3. e2e needs a stack → ephemeral `ppiq-ci` project, torn down by `trap`.
4. **Redeploy must not destroy secrets** → `POSTGRES_PASSWORD` is bound to the volume's first init; a stale-key regeneration **harvests** it rather than rotating it. *Never delete `/var/lib/ppiq-preserve/.env` alone.*
5. Bad image must not stay live → every image tagged `:previous`, 45×2s health probe **from inside the network** (the agent cannot reach `127.0.0.1:5063`), automatic rollback.

**Two self-policing test files** parse the Jenkinsfile inside `dotnet test`: `CiPipelineTruthGateTests` (no `--list`, no `catchError SUCCESS`, tests before deploy, e2e ungateable) and `DeployRedPathProofTests`. Their needles are **assembled from string fragments** so a scanner cannot match the guard that forbids them. Both strip comments first — because the original version was satisfied by the Jenkinsfile's own header comment, and deleting stages 3–5 left the suite green.

### 9.2 Known deployment debt `[REPO]`

- **`post-deploy-smoke.sh` says "run by Jenkins stage 5b". There is no stage 5b.** Nothing verifies the public HTTPS surface after a deploy.
- **The committed Caddyfile defines site blocks for `{$SITE_HOST}` and `{$WEBSITE_HOST}` only**, while `VITE_API_BASE_URL` and the acceptance doc address `api.` and `app.` subdomains that no block matches. Most likely the running config is not this file (recorded debt: its host-bound source was deleted). **Confirm what is running before changing either, and do not recreate the proxy container.**
- 15 hardcoded IP references, one of which (`validate-phase01-phase02-gates.mjs:77`) **asserts** the address — parameterising without changing it turns the gate red.
- `validate-real-ui-gates.cjs` — the guard that forbids `--list` — **is invoked by nothing and would fail if it were**. Visual-regression and accessibility suites run in **no pipeline at all**.

---

## 10. PIPELINE GREEN AND APP URL — THE HONEST ANSWER

**No pipeline work and no deployment work was done in this session.** Nothing was changed to make the pipeline green or the app URL work, because neither was touched.

The pipeline was made green earlier — the Jenkinsfile's own last modification is **10 July 2026**, and the three archived backups in `deploy/.ppiq-backups/` are from that same morning (`ci-truth-gate-comment-stripping-20260710-114147`, and two `falsification` runs at 11:46 and 11:48 — the day someone deliberately broke the gate to prove the red path works).

Section 9 is documentation of what exists, **not a record of work done here.** Anyone reading it as "what we changed" will be wrong. The same statement appears in the 25-Jul handover, and it is still true.

---

## 11. WHAT THE NEXT SESSION SHOULD DO

### 11.1 Do not re-investigate — these are settled

| Question | Answer |
|---|---|
| Why were widgets empty? | Three code defects, all returning 200. §2 |
| Are they fixed? | Yes. 38/38 at 20:26. §3.1 |
| Does `MODEL_INSIGHTS` exist? | **Yes**, and it works. But it's in no script |
| Are the credentials `admin`/empty? | **No.** `e2eadmin` / `E2EAdmin123!` from the profile |
| Why did the C# repair fix 20 rows but the census still show 5 broken? | `GetDashboardsAsync` excludes system templates by default |
| Is `shiftCode` a registered dimension? | Yes. The group-by change should work. Untested |
| How many surfaces have a canvas? | **Two** |
| Is there a SQL editor? | **No.** A read-only view of the compiled query |

### 11.2 Ask Karim first

1. **The presentation date.** Asked three times, still unanswered. It decides whether M1-P2 gets built or cut.
2. **Which remedy for the schema names** — display-name registry, prefix filter, or cut sentence.
3. **The result of the assistant's rolling-speed question.** Highest room risk in the product; ten minutes; never run.

### 11.3 The work that is mine and ready

- **`M1-07`** — correct and rename `070_fix_system_template_widget_codes.sql`. **Do this first**; it protects everything achieved today.
- **`M1-05`** — hide the emulated-source schema names (needs Karim's choice).
- **`M1-06`** — the Logout chip. `src/components/AppLayout.tsx`, the user chip in the command header is a `StandardButton` with `onClick={logout}`. **Do not delete it** — it carries the user's name. Render it non-interactive, which also drops it from the tab order. Note `logout` then becomes unused in the `useAuth` destructure and the build fails until handled.
- **`M1-12`** — chart-type review for the seven single-row widgets.
- The **debug log** and the **bind step** are items 2 and 4 in Karim's own build order.

---

## 12. FILES PRODUCED THIS SESSION

| File | What it is |
|---|---|
| `PPIQ_Product_Backlog_v31.xlsx` | **Authoritative backlog.** 71 tasks, 532.5h |
| `PPIQ_Schema_Topology_and_DataFlow_Contract_v2.md` | **The M2 database architecture.** Three schemas, the permanent relational model, the engine's two layers and supervisor, the isolation rule, three page types |
| `PPIQ_Implementation_Review_27Jul2026.md` / `.html` | Scoreboard + six top-N lists + the plan-vs-reality table |
| `Invoke-PpiqWidgetRowCensus.ps1` | **Keep this.** Re-runnable measuring instrument; acceptance evidence for M1-04 and a precondition for any new dashboard |
| 6 apply packs | All applied, gated, committed. Each has `-Revert` |

---

*Compiled 27 July 2026, 21:30. Every `[RUN]` claim in this document happened in this session and its output was read. Where something is unverified it says so. Nothing here is an estimate presented as a measurement.*

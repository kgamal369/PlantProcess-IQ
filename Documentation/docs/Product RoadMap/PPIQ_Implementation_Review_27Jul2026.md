# PPIQ — IMPLEMENTATION REVIEW AND SCOREBOARD
**27 July 2026, 21:00 · against Backlog v30, Constitution v3, Amendment A1, the Presentation Scoreboard**
**Measured, not estimated. Every claim below traces to a run, a file or a line.**

---

## PART 0 — READ THIS FIRST: YOUR PRESENTATION PLAN AGAINST WHAT EXISTS

You described six segments. Four of them assume surfaces that are not built. This is not a judgement about the product — it is very good in places — it is a statement about **what can be shown on a screen this week**.

| # | What you said you will show | Reality | Verdict |
|---|---|---|---|
| 1 | The whole 15-step journey, ~2 min per step | The rail exists and all 15 stages render | **SHOWABLE** |
| 2a | **Five** no-code/low-code UIs | **Two** surfaces carry a real canvas: `VisualJoinCanvasPage` and `AnalysisToolboxPage`. S2 is a form panel. S4 does not exist. S5 is a form, never reviewed | **PARTIAL — say two, not five** |
| 2b | Wire diagram that really works | S1 is genuine: typed ports, minimap, join edges, dry run, publish, and as of today **refusal with a written sentence** | **SHOWABLE — your strongest 90 seconds** |
| 2c | **ETL from a dump file**, join it, load it to the database, through the no-code UI | The canvas joins **already-staged** tables. Getting a dump file into staging is the **import path** — a different set of pages. There is no file-drop onto the canvas | **NOT AS DESCRIBED — two surfaces, not one act** |
| 2d | **One with a SQL editor** | There is **no SQL editor anywhere**. The canvas has a read-only SQL **view** of the query the server compiled. Typing SQL is impossible | **NOT SHOWABLE** |
| 2e | AI+ML and correlation from a **wiring block toolbox** | `AnalysisToolboxPage` is a canvas with **three fixed blocks** configured by form fields — outcome, grain, window. There is no method palette, no draggable statistical blocks, no ML authoring | **NOT AS DESCRIBED** |
| 3a | Dashboards, widgets, edit a widget | Exists. **38 of 38 widgets carry data as of 20:26 today** | **SHOWABLE** |
| 3b | **Modify a widget's SQL** | The panel has a query mode using an **expression DSL**, not SQL. And its **bind step does not exist** — the chart infers its category column | **NOT AS DESCRIBED** |
| 3c | Change group-by from material to **shift** | `shiftCode` **is** a registered dimension. Should work. **Never tested** | **PROBABLE — test before the room** |
| 3d | Empty page, fill it with many widgets, configure them | `createDashboardDefinition` exists in the client. Whether a **create-page button** is reachable in the UI is unverified | **UNVERIFIED — check first** |
| 4 | The website | Exists, scroll animations, ROI sliders | **SHOWABLE, unwalked** |
| 5 | The engine | ~45 runs on record. **Every one Blocked.** No completed correlation exists on this dataset | **SHOWABLE ONLY AS THE ABSTENTION BEAT** |
| 6 | Chatbot, tolerant of imprecision | Grounded questions return cited answers. The predictive question **did not refuse** on 21-Jul and has not been retested | **HIGH RISK — see Gap 1** |

### The single most important sentence in this document

**Your rolling-speed example is the highest-risk moment in the entire presentation, and it is currently untested.**

You said: if it answers 10, 15, 9 or 16 m/s for a 1.2 mm target, fine. If it answers **1000 kg**, that is fatal. That is exactly right, and it is exactly Hostile-Hands line 5.3.

The record from 21 July says the predictive question **did not refuse** — it answered. Nobody has checked whether the answer was in a sane unit. **Ask it that question tonight.** If the unit is wrong, cut the free-question beat and ask only the two prepared grounded questions. That decision costs nothing if made in advance and costs the room if discovered live.

---

## PART 1 — PRESENTATION READINESS

### 1.0 Scoreboard

| Viewpoint | 25-Jul | **27-Jul** | Movement |
|---|---:|---:|---|
| Plant / process engineer | 57 | **68** | +11 — every widget now carries data; the canvas refuses illegal wiring |
| Data / BI engineer | 52 | **60** | +8 — measure engine truthful, authoring panel real |
| Buyer / CEO | 48 | **48** | unchanged — narrative work, no build touched it |
| IT / security | 50 | **50** | unchanged |
| Infrastructure | 45 | **45** | unchanged — nothing touched it since before 22-Jul |
| **HEADLINE (lowest)** | **45** | **45** | |
| **Demo-scope headline** (excluding infrastructure, which is not in the room) | **52** | **60** | **+8 in one day** |

**The honest reading.** The lowest score is unchanged because infrastructure was untouched, and that persona will not be in the room — the demo runs from your laptop. The number that decides the meeting is the process engineer at **68**, and it moved eleven points today for one reason: the widgets stopped lying.

The **48 for the buyer has not moved all week**, and no code will move it. It is the cut register, the rehearsal, and the deck.

### 1.1 TOP 10 GAPS — presentation-critical, ranked by room risk

| # | Gap | Why it matters in the room | Cost |
|---:|---|---|---|
| **1** | **The assistant's unit sanity is untested.** A speed question answered in kilograms destroys the intelligence claim in one sentence | You named this yourself. It is one question, tonight | **10 min** |
| **2** | **No SQL editor exists**, but your plan says you will show one | An engineer who asks to type SQL finds a read-only view. The gap between "SQL mode" and "SQL view" is visible in seconds | Cut sentence, or 10h |
| **3** | **The ML/correlation wiring toolbox does not exist.** Three fixed blocks with form fields is not a method palette | This is segment 2e of your plan. Showing it as a "block toolbox for AI+ML" will not survive one question | Cut sentence, or M2-P5 |
| **4** | **The ETL-from-dump-file act spans two surfaces**, not the canvas | You will open the canvas expecting to load a file and there is no file drop | Reframe the beat |
| **5** | **The bind step is missing.** A query's columns are not mapped to chart roles; the card infers the category column | If you demo query binding and the chart picks the wrong column, it looks broken. It guesses right often, which is worse | 6h |
| **6** | **The engine has never completed a run.** 45 blocked, zero finished | "Show me one that finished" has no answer. Must be pre-scripted as the abstention beat | Rehearsal |
| **7** | **The emulated-source schema names are visible** on the canvas tree — `src_caster_oracle_shape`, `dump_store` | This is the surface your authoring story now OPENS on. Rule 1 breaks in the first minute | 3h (M1-05) |
| **8** | **Logout has no login behind it.** One click ends the demonstration | Not in the nav, so the nav sweep misses it. It is in the chrome on every screen | 1h (M1-06) |
| **9** | **Seven widgets return exactly 1 row.** A one-bar bar chart reads as broken even though the number is honest | `DQ_BY_SOURCE`, `DQ_BY_TYPE`, `EO_EQDEF`, `QM_SEV`, `RI_KPI`, `RI_TREND`, `RI_EQUIP` | Chart-type choice |
| **10** | **`070_fix_system_template_widget_codes.sql` still writes the broken codes.** A clean rebuild reinstalls today's five defects | If you rebuild the demo database before the room, the widgets break again | 1h |

### 1.2 TOP 10 IMPLEMENTED BETTER THAN THE DESIGN ASKED

These are real strengths. Several exceed what the constitution requires, and two are better than anything in the specification.

| # | What | Why it beats the design |
|---:|---|---|
| **1** | **The preparation canvas** | A1.8 called it "the only surface that genuinely carries a node canvas". It now also has a mode toggle, a three-level tree with key markers, filters, derived columns, dry run, immutable publish with rollback, and refusal-with-sentence. **This is a product surface, not a demo** |
| **2** | **Wiring refusal writes a sentence** | A1.5 asked for refusal. The implementation refuses **five distinct cases**, each naming the rule, plus a sixth at run time naming the unjoined table by name |
| **3** | **Server-side SQL compilation** | III.14.4 requires the SQL view be a deterministic view, never a client reconstruction. It is. The client never composes SQL — stronger than most commercial tools |
| **4** | **The widget authoring panel holds zero literals** | Every list — dimensions, measures, chart types, purposes, compatibility rules — comes from `GET /analytics/dashboard/metadata`. Rule 1 enforced by construction, not by discipline |
| **5** | **Chart-type compatibility narrows automatically** | Incompatible options are **absent** from the switcher, not present-and-broken. Hostile-Hands 2.11 asks for exactly this |
| **6** | **`supportsDimension` / `supportsMeasure` per chart type** | The form adapts to the chart rather than assuming both fields. This was not required anywhere; it was noticed and built |
| **7** | **The safe-SQL layer** | Parameterised predicates via Npgsql, operator whitelist, column validation against the catalogue, and `rejected_by_safe_sql` as a **first-class dry-run status** rather than an exception |
| **8** | **The pipeline polices its own definition** | Two architecture tests parse the Jenkinsfile inside `dotnet test`: no `--list`, no `catchError` forcing success, tests textually before deploy, e2e stage cannot be gated off. Needles assembled from fragments so a scanner cannot match the guard that forbids them |
| **9** | **Immutable versioning with rollback on the preparation artifact** | draft → validated → published → rolled_back, with a rollback pointer. The 21-Jul matrix found it matches the spec's immutability rule *verbatim* |
| **10** | **The abstention gate itself** | Four green dimensions with real numbers and one honest red naming 46.5% completeness against an 85% bar. **This is a better artefact than a correlation you engineered the data to produce**, and no competitor shows it |

### 1.3 TOP 5 IMPLEMENTED BADLY — works, but needs rework

| # | What | Why it is dirty | Rework |
|---:|---|---|---|
| **1** | **`070_fix_system_template_widget_codes.sql`** | A file whose name says it *fixes* the widget codes and which **writes the broken values**. Three places agreed on the wrong answer — C# seeder, this script, and a case-insensitive repair confirming both | Correct **and rename**. Criterion 4 |
| **2** | **The measure registry and the measure engine were never bound** | `observationCount` was published, validated, offered in the UI, and had no implementation. Only its own declaration referenced it. Fixed today by a guard, but the underlying pattern — registry and executor as independent lists — persists for dimensions | Bind dimensions the same way |
| **3** | **`AnalysisToolboxPage` uses a canvas to render a form** | It imports ReactFlow and lays out three fixed blocks that are configured by dropdowns. It has the *appearance* of an authoring surface and none of the behaviour. That is the lens-three failure in your own README | Either make it a real palette (M2-25) or stop drawing it as a graph |
| **4** | **The widget card infers its category column** | `columns.find(c => c.code === dimensionCode)`, then falls back to the first column not named `value`. For a three-column query it guesses — and guesses right often enough to hide the bug | The bind step, 6h |
| **5** | **24 backup folders and 31 apply packs at the repository root** | Every pack writes a backup and nothing cleans them. Committed. Persona A1 criterion 3 names orphaned backup folders explicitly | `.gitignore` + move packs to `tools/packs/` |

---

## PART 2 — END-OF-M2 READINESS

### 2.0 Scoreboard against Constitution v3 and Amendment A1

| Constitution clause | Required | Built | State |
|---|---|---|---|
| A1.1 one shell, five purposes | 5 | 2 canvases, 2 forms, 1 absent | **40%** |
| A1.2 mode toggle on every surface | 5 | 1 (S1) | **20%** |
| A1.3 three-level tree | 5 | 1 (S1) | **20%** |
| A1.3 debug log, three severities | 5 | **0** | **0%** |
| A1.3 block palette | 5 | **0** | **0%** |
| A1.4 SQL **editor** | 5 | 0 (a view on S1 only) | **10%** |
| A1.5 wiring legality | 5 | **1 complete (S1, today)** | **20%** |
| A1.6 widget authoring through the shell | 1 | Panel yes, shell no | **50%** |
| A1.7 board semantics by purpose | — | S1 correct; S2–S5 absent | **20%** |
| Rule 1 genericity | — | Strong in the panel; **broken in the canvas tree** | **75%** |
| Rule 2 starts empty | — | Enforced | **95%** |
| Rule 3 15-step journey | 100% | Rail complete, steps unverified | **70%** |
| **M2 authoring layer overall** | | | **≈25%** |

**M2 remaining: 326 hours across 39 tasks.** The authoring shell alone is 60h and it is the phase that makes four of your five no-code surfaces real.

### 2.1 TOP 10 GAPS FOR M2

| # | Gap | Constitution | Est |
|---:|---|---|---:|
| **1** | **The shell foundation** — one surface, two modes, tree, palette, debug log, parameterised by purpose | A1.1–A1.3 | 20h |
| **2** | **The debug log** — three severities each carrying cause and remedy. Everything else is only usable if failures explain themselves | A1.3 | 8h |
| **3** | **SQL editor with the fork contract** — palette hidden entirely, tree kept, forking detaches the graph as read-only history with a warning | A1.4, III.14.4 | 10h |
| **4** | **ML authoring as a shell purpose (S4)** — does not exist at all | A1.1 S4 | 10h |
| **5** | **Wiring legality on S2–S5** — type rejects operation, column absent from prepared dataset, aggregate outside aggregation context | A1.7 | 6h |
| **6** | **The engine has never completed a run** — 45 blocked across four dates | Rule 3 | 10h |
| **7** | **The pipeline model for the generator** — filters are one flat `WHERE`; board nodes would draw a pipeline that does not exist | II.6.5 | 20h |
| **8** | **Tier-to-feature matrix does not exist**, so Rule 5.2 cannot be demonstrated | Rule 5.2 | 6h |
| **9** | **Infrastructure has never been measured** — sizing is estimates, the 100-job claim untested, backup/restore never drilled | A13 | 8h |
| **10** | **CI truth gates** — visual-regression and accessibility suites execute in **no pipeline at all**; the guard that forbids `--list` is invoked by nothing and would fail if it were | A1 criterion 9 | 8h |

### 2.2 TOP 10 STRENGTHS FOR M2

| # | What | Why it is a durable asset |
|---:|---|---|
| **1** | **The domain already models expression versioning** — `QueryExpression`, `ExpressionVersion`, `ExpressionEnabled`, validation triple, EF mapping, index. Someone built this properly before it was needed |
| **2** | **The domain refuses to enable an unvalidated expression.** That invariant handed us the save design for free |
| **3** | **`portsCompatible` existed before anything used it** — the type lattice was designed, written, and waiting |
| **4** | **Two compose projects, permanently separated**, because a shared project name once let orphan removal reap the CI and proxy containers mid-deploy. The fix is structural, not procedural |
| **5** | **Secrets survive redeploy by design** — `POSTGRES_PASSWORD` is bound to the volume's first init, so regeneration harvests it rather than rotating it. The reasoning is written into the script |
| **6** | **Health-gated deploy with rollback** — every image tagged `:previous`, 45×2s health probe **from inside the network**, automatic retag and redeploy on failure |
| **7** | **Ed25519-signed licence tokens with RLS-forced tables**, dev key registered only when `PPIQ_PRESENTATION=on` |
| **8** | **Six emulated source classes** — PostgreSQL, Oracle, SQL Server, MySQL, CSV, Excel — exercising every connector class with no code change. The practical proof of Rule 1 |
| **9** | **167 backend test files**, with genuine depth in tenant isolation, RLS, audit immutability and genealogy attribution |
| **10** | **The backlog's own laws** — only open work exists, no partial status, remainders rewritten with recalculated estimates. Most teams do not have this and it is why v30 could be built honestly |

### 2.3 TOP 5 DIRTY FOR M2

| # | What | Why | Rework |
|---:|---|---|---|
| **1** | **318 of 2102 files carry a phase, task or version token in their name**, plus 16 directories | Golden rule violation at 15% of the tree. `Phase3Phase4`, `phase56`, `Phase45` | Rename map + import rewrites |
| **2** | **`validate-real-ui-gates.cjs` is invoked by nothing and would fail if it were** | The guard that forbids `--list` requires three `npm test` commands in the Jenkinsfile; the Jenkinsfile has none. Two suites run nowhere | M2 CI truth gates |
| **3** | **`post-deploy-smoke.sh` says "run by Jenkins stage 5b". There is no stage 5b** | Nothing verifies the public HTTPS surface after a deploy | Wire it or delete it |
| **4** | **The reverse-proxy config references stale targets and its source file was deleted** | Fragile but functioning. The committed Caddyfile does not match the URLs in use | Do not recreate until a persistent config exists |
| **5** | **`STATIC_AUDIT.md` carries 5 CRITICAL and 4 HIGH and has never been read**, because the script exits 0 | A gate that always passes is not a gate | Read it, then make it fail |

---

## PART 3 — WHAT I WOULD DO WITH THE TIME

**Tonight, 30 minutes, no code:** ask the assistant the rolling-speed question. Press Logout once and see what happens. Open `/prep/canvas` and read the schema names. Those three answers change what you say in the room more than any pack I could write.

**If the presentation is inside 48 hours:** M1-05 and M1-06 are four hours of mine. Everything else is yours — the consolidated pass, the cut register, two timed rehearsals. **Your presentation plan needs editing more than the product needs building.** Say two no-code surfaces and demonstrate them completely, rather than five and defend four.

**If you have a week or more:** the debug log and the bind step, in that order — they are items 2 and 4 in the specification's own build order, and the first makes everything after it diagnosable.

---

*Compiled from measured runs on 27 July: the widget census (38/38 at 20:26), the wiring-refusal delivery, the measure-truth pack, and the repository snapshot of 09:13. Where a claim is unverified it says so.*

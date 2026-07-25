# PPIQ SESSION HANDOVER — 22-Jul to 25-Jul 2026

**Purpose.** This document exists so the next session does **not** re-investigate,
re-scan, re-test or re-discover anything settled here. Read it end to end before
touching the repo. Where something is unverified or unknown, it says so
explicitly — do not treat an absence of evidence as evidence.

**Audience.** A fresh assistant session with no memory of this conversation.

**Owner.** Karim — solo founder, SOU Industrial Software, Düsseldorf.

---

# 0. THE ONE-PARAGRAPH SITUATION

PlantProcess IQ is a read-only, evidence-grade, industry-agnostic
process-to-quality intelligence platform for manufacturing plants. A customer
presentation is imminent. Over this session we did a full static review of the
25-Jul repository snapshot (extracted into a real 1,996-file tree and read, not
grepped), found and fixed a large set of demo-visible defects across demo Scenes
1–9, and closed M1-01 — the wire-up that had left two fully-built features
unreachable for two weeks. **The demo will run on Karim's local laptop, not the
server.** Everything below is local-machine reality.

---

# 1. WORKING AGREEMENTS AND RULES KARIM SET (carry these forward)

These are not preferences to be re-derived. They were stated explicitly.

### 1.1 Delivery format — absolute

- **ALWAYS deliver a PowerShell script.** This covers *diagnostics* too, not just
  fixes. Never ask him to paste JavaScript into browser DevTools, never ask him
  to run ad-hoc commands by hand. He said this after being asked to paste a
  console snippet, and it is a hard rule.
- **NEVER deliver zip files.** Everything is a copy-paste PowerShell apply script
  that performs the full implementation.
- Every pack follows the contract:
  **preflight → report (file hashes + timestamps) → backup → anchored/whole-file
  write → on-disk self-check → gates → auto-revert on any failure.**
  Plus a `-Revert` switch, and usually `-ReportOnly`.
- **Pure ASCII. UTF-8 no BOM** (`[System.IO.File]::WriteAllText` with
  `UTF8Encoding($false)`). **CRLF** for `.ps1`/`.cs`/`.tsx`, LF for `.sh`.
- **No `&&` in PowerShell.** Cuddled `} else {`. Scripts run from repo root.
- **No em-dashes, no curly quotes** in his files.
- Zero preamble, no flattery. Evidence before cure. Surface defects honestly.
  Never claim done when not done.

### 1.2 Execution policy on his machine

Direct `.\script.ps1` invocation is blocked (`PSSecurityException`, unsigned).
**Always give the command in this form:**

```
powershell -NoProfile -ExecutionPolicy Bypass -File .\Script.ps1
```

### 1.3 The reviewing concept he asked for (critical — this shaped the whole review)

He gave three worked examples of what "deep review" means to him. Internalise
these; they are the lens for every future scan:

1. **"Function works, surroundings do not."** The widget renders correct data,
   but clicking the pie does nothing, the max/min buttons misbehave, widget
   orientation is wrong, add/drag-drop is broken. *Check the environment around
   the feature, not just the feature.*
2. **"Function works, styling is dirty."** The left nav lists 30 items and works
   — but flat, ungrouped, unfoldable. It functions and looks unprofessional.
   *Judge whether it reads as advanced, high-tech, professional.*
3. **"Looks fine, deeper look shows it is all wrong."** The connection form
   exists, the text boxes exist, the dropdown exists — but the wiring points
   somewhere wrong, the fields are misaligned, and the dropdown is not
   dynamically populated. *Verify wiring and data-source dynamism, not presence.*

He also said: the customer's eye wanders, and he may be asked to click anywhere.
So every adjacent control on a demoed page matters.

### 1.4 Document status ruling

- `rules.txt` is the **most current and authoritative** document.
- All other documentation is **partially stale**; several documents describe an
  older, less advanced concept than the current build.
- After the demo work, Karim wants a dedicated session to **delete, merge and
  update** his 22 documentation files.
- Practical consequence for the next session: **do not trust a document's status
  claims over the code.** Verify against the tree.

---

# 2. WHERE WE STARTED vs WHERE WE ARE

## 2.1 Identity and topology (from `PPIQ_Identity_and_Topology_v4.md`, dated 26-Jun)

That document is topology, not build state, so it aged well. Key facts:

| Item | Value |
|---|---|
| Local API | `http://localhost:5063` |
| Local frontend | `http://localhost:5173` |
| Local PostgreSQL | `127.0.0.1:5432` |
| Demo database | `ppiq_presentation` |
| Dev database | `ppiq_app` |
| DB role | `ppiq_dev` |
| DB password (LOCAL DEV ONLY) | `ppiq_dev_local_only` |
| Local identity | config-seeded dev users, no `app_users` table |
| Server | `178.105.152.180` (Hetzner) |
| Server app URL | `https://app.178.105.152.180.sslip.io` |
| Server API URL | `https://api.178.105.152.180.sslip.io` |
| Server secrets | `/var/lib/ppiq-preserve/.env` — **git-ignored, never delete** |
| Deploy trigger | Jenkins pipeline on `git push origin main` |

**The server credential is NOT the local one and must never be embedded in a
committable script.**

## 2.2 Roadmap position at session start

- **Backlog v27** is the governing board: M1-P1 Demo Lock & Impress 63h →
  M2-P1 Working Version Core 60h → M2-P2 Enterprise & Infrastructure 56h →
  M2-P3 Catalogue & Canvas Completion 49h → M3-P1 Market Proof 44h.
  31 open tasks, 272 hours.
- **M1-01** (the three wire-ups) was open and had been open since 22-Jul.
- A senior's external assessment scored the product 61/100 evidence-adjusted
  maturity, 45/100 shipping headline (lowest persona = A13 Infrastructure).
  That assessment was reviewed and judged **fair and well-calibrated**, with two
  adjustments: it *under*-stated the CI gap, and was slightly over-cautious on
  security design.

## 2.3 Roadmap position now (end of session)

- **M1-01 is DONE.** Both routes live, endpoint registered.
- Demo Scenes 1–9 reviewed; Scenes 2–9 fixed to the extent listed in section 4.
- **M2 first item is now the auto-deployment infrastructure** (Docker, Jenkins,
  env vars, login provisioning) — promoted by Karim's explicit decision because
  he chose not to risk the server before the demo.

---

# 3. GIT AND COMMIT STATE

| Commit | Content |
|---|---|
| `a8d50163` | fix(dashboard): stop fabricating materialCode filters and size charts on first paint — 4 files, +76/−62 |
| `7adf31a9` | fix(dashboard): kpi tiles, working resize controls, temporal cross-filter — 6 files, +129/−37 |

Later packs (Scenes 2–4, Scenes 5–8, nav fix) were applied and gated; Karim
confirmed committing after the nav pack. **Nothing has been pushed to origin.**
Branch is `main`.

> **Warning that must not be lost:** on 23-Jul at 10:25:38 a bulk revert
> (`git checkout`-shaped, seven files sharing that exact timestamp) destroyed an
> uncommitted batch of work — the newer `InteractiveCharts.tsx`,
> `chartDataPresentation.ts`, and the `test:presentation:unit` script in
> `package.json`. All of it was lost because it had never been committed.
> **Commit after every green pack.**

---

# 4. EVERY MODIFICATION MADE THIS SESSION

Listed in the order applied. Each was gated by `tsc -b` (and `dotnet build` where
backend files changed) and auto-reverted on failure.

## 4.1 Pack: chart first-paint sizing — `Fix-PpiqChartFirstPaint-v3.ps1`

**File:** `src/components/charts/InteractiveCharts.tsx`

**What:** `initialDimension={{ width: 600, height: 300 }}` added to all five
`ResponsiveContainer`s, via a drift-proof regex that matches any prop order and
skips already-patched tags.

**Why:** measured — twelve recharts warnings on `/dashboard`, all
`The width(-1) and height(-1) of chart should be greater than 0`. `-1` is what
ResponsiveContainer reports when it paints before its ResizeObserver has
measured. A single forced viewport resize then made 5 of 6 containers draw
correctly with real geometry (svg 458x243 and 716x415, 50 bars, a line curve,
4 sectors). So charts, data and wrappers were always fine; only the first
measurement was missing. `minHeight` is a CSS floor on the wrapper, not a
measurement, so it could not prevent this.

## 4.2 Pack: selection fabrication — `Fix-PpiqSelectionFabrication.ps1`

**Files:** `state/widgetSelectionMap.ts`, `state/associativeFields.ts`,
`components/dashboard/ChartExtras.tsx`, `components/charts/InteractiveCharts.tsx`

**What:**
- `dimensionToFilterField` returns `null`, never a fabricated field.
- `ChartExtras` `field` defaults to `null` and `toggle` refuses to filter without
  an honest field.
- `associativeFields` drops `materialCode`.
- `InteractiveCharts` `SelectionConfig.field` made nullable; all five
  `applySelection` calls guarded.

**Why:** the URL showed `?materialCode=2026-04-01` and every widget correctly
returned zero rows. `dimensionToFilterField("day")` returns `undefined`, and
`ExtraChart` declared `field = "materialCode"` as a **default parameter** — which
fires on `undefined`. One click on a pareto bar labelled `2026-04-01` set
`materialCode=2026-04-01`, which matches no material, blanking the page.
Simultaneously the associative engine enumerated `materialCode`, which the server
rejects with `400 Unsupported dimension code 'materialCode'`.

## 4.3 Pack: widget surface — `Fix-PpiqWidgetSurface.ps1`

**Files:** `SavedDashboardWidget.tsx`, `ChartExtras.tsx`, `DashboardWidgetCard.tsx`,
`InteractiveWorkspacePage.tsx`, `widgetSelectionMap.ts`, `InteractiveCharts.tsx`

Six defects, labelled D1–D6:

- **D1 — KPI tiles rendered as 50-bar charts.** `SavedDashboardWidget` had no
  `kpi` branch, so `chartType = "kpi"` fell through the final `else` into
  `InteractiveBarChart`. The seeder defaults KPI tiles' dimension to `day`, so
  each tile drew ~50 daily bars. `MetricCard.tsx` already existed and was unused
  for this. **Four of the eight widgets on `/dashboard` were affected.**
  Fix: a `kpi` branch routed to `MetricCard`, with `kpiValue()` — rates, scores
  and averages averaged; max/min take the extreme; everything else sums.
- **D2 — the switcher could never return to KPI.** `extendChartTypes` did not
  offer `"kpi"`. Fix: `"kpi"` prepended to the list.
- **D3 — all three resize buttons were silent no-ops.** react-grid-layout matches
  layout items by the child key, which `InteractiveWorkspacePage` sets to the
  **raw widget id**. `DashboardWidgetCard` receives `saved-<id>` and passed that
  to `expandWidgetToFullRow` / `expandWidgetToHalfRow` / `compactWidget`, so
  `updateWidgetInAllBreakpoints` compared `item.i === "saved-..."` and never
  matched. Fix: `const gridItemId = String(widgetId).replace(/^saved-/, "")`.
- **D4 — one resize button duplicated another.** "Half-row width" called
  `compactWidget` when the widget was a table — identical to the "Compact size"
  button beside it. Fix: the half-row control removed entirely.
- **D5 — two dead menu items.** `InteractiveWorkspacePage` passed
  `onEdit={() => undefined}` and `SavedDashboardWidget` forwarded it to *both*
  `onEdit` and `onRename`. Fix: prop made optional, dead prop removed, so both
  items disappear (the card already renders each conditionally).
- **D6 — time-dimension widgets cross-filtered nothing.** `dimensionToFilterField`
  maps nine dimensions and no temporal one. Fix: `timeDimensionRange()` maps
  `day` / `week` / `month` to `fromUtc`/`toUtc`. ISO-8601 week logic (week 1
  contains 4 January, weeks start Monday) tested against Monday boundaries and
  December rollovers.

## 4.4 Pack: Scenes 2–4 — `Fix-PpiqScenes234-v2.ps1`

- **Scene 3 — `SourceImportPrepPage.tsx` rewritten in full** onto the token set,
  with a new `pages/sourceImportPrep.css`. Removed: six local `CSSProperties`
  objects, thirteen inline styles, one raw `<select>`, four raw `<input>`s, and
  **eight hardcoded hex colours matching nothing else in the product**
  (`#0E2238`, `#27466B`, `#0B1B2E`, `#D7E5F7`, `#8AA3C0`, `#4F9CF9`, `#30C48D`,
  `#E5484D`). Added: numbered step chips, cyan hover and `focus-visible` rings on
  the discovered-table cards, live discovered count. **API wiring untouched** —
  `listSourceTables`, `listSourceColumns`, `registerSourceTable` and the discovery
  defaults are byte-identical, and the self-check aborts if any went missing.
- **Scene 3b — the red banner removed.** `#E5484D` with `role="alert"` became an
  amber notice reading **"Not registered"** plus the reason, with `role="status"`.
  A discovery miss is information, not a failure, and the demo contract makes a
  red banner an automatic fail.
- **Scene 2 — one copy fix.** The empty state told the customer to go use a
  different screen; it now states the position and the next action.
- **Scene 4 — `WidgetScriptBuilderPanel` removed** from
  `MaterialInvestigationPage`. A widget SQL builder with a raw textarea was
  rendering in the middle of the customer-facing genealogy page.

## 4.5 Pack: Scenes 5–8 — `Fix-PpiqScenes5678-v2.ps1` (nine files)

- **M1-01 wire-up (Scenes 7 + 8).** Two lazy imports and two routes in `App.tsx`
  (`/prep/canvas`, `/analysis/toolbox`), two nav entries under Analytics in
  `AppLayout.tsx` (icons `GitBranch` and `Cpu` — chosen because they were already
  imported), and in `Program.cs` both
  `using PlantProcess.Api.Endpoints.Prep;` **and** `app.MapVisualMapperEndpoints();`.
  The access-matrix row `("/api/prep/visual-mapper", All(), "analysis.execute", false)`
  already existed in `PlantAccessControl.cs`.
- **Scene 8 — the parity panel made real.** `AnalysisToolboxPage` previously
  aliased one payload to the other, so the panel compared an object with itself
  and could never report DIFFERS. `formPayload` is now assembled independently
  from the raw field values. Added: readiness-gate summary via the existing
  `getAnalysisReadinessGates`, the **run id** printed beside the result, and
  errors rendered through the error style.
- **Scene 5 — Parameter filter.** `All parameters` entry added, hardcoded
  `?? "CastingSpeed"` display default removed, and the three industry-specific
  fallback options (`CastingSpeed`, `Superheat`, `RollingForce` — none of which
  exist in the data) replaced by a disabled `Parameter catalogue unavailable`.
- **Scene 5 — drilldown drawer** given a 220ms `translateX` slide with a
  `prefers-reduced-motion` guard, in `styles/phase56/legacy-chunks/legacy-004.css`.
- **Scene 5 — the two Reset buttons merged** into one control in
  `SelectionBreadcrumb.tsx`.
- **Scene 6 — associative engine.** Full active filter set now forwarded minus
  the field's own selection and minus pagination keys (previously only the eight
  associative keys, dropping `fromUtc`/`toUtc`); zero-row dimensions degrade to
  `n/a` instead of rendering an empty titled column; the eight fields load via
  `Promise.all` instead of a sequential `await` loop.
- **Scene 6 — `associative.css` rewritten** on the contract palette (was eight
  off-contract colours), duplicate dead rule set removed, eight-column grid,
  focus ring, reduced-motion guard.

## 4.6 Pack: nav + widget SQL — `Fix-PpiqNavAndWidgetSql.ps1`

- **`useWorkspaceLinks` rewritten to use `apiClient`.** It had contained the
  **only raw `fetch` in the entire src tree**, failing three ways at once.
- **The Workspaces block converted to a real `NavGroup`** with an `emptyHint`
  ("No workspaces published yet"), so it folds like its four neighbours and
  matches their indent.
- `AppLayout.css` gained a `.piq-nav-group__empty` rule.

## 4.7 Database repairs (SQL, `ppiq_presentation`)

| Widget | Was | Now |
|---|---|---|
| `QM_SEV` | dim `severity` (unsupported → 400) | `equipment`, retitled **Defects by Equipment** |
| `MI_SEV` | dim `severity` (unsupported → 400) | `materialUnitType`, retitled **Defect Mix by Material Type** |
| `PA_TABLE` | `avgParameterValue` + NULL parameter | `observationCount`, retitled **Observations by Parameter** |
| `CORR_PARAMETER_AVG_BY_EQUIPMENT` | `avgParameterValue` + NULL parameter | `parameter_code = rolling.cooling_rate` |

**Both proof counts now read 0.** Every seeded widget definition is valid against
the API registry.

---

# 5. EVERY TEST AND MEASUREMENT RUN — DO NOT REPEAT THESE

## 5.1 Browser diagnostic (`Invoke-PpiqChartDiagnostic.ps1`, 23-Jul 10:18)

A Playwright probe against the running app. **Results:**

- 6 recharts containers on `/dashboard`.
- All six `div.chart-box` wrappers measured **243px or 415px** — never zero.
  All six `.chart-box` CSS rules were loaded. **The wrappers were never the
  problem.**
- **Twelve** `width(-1) and height(-1)` warnings.
- After one forced viewport resize: Material Units 50 bars, Quality Events
  50 bars, Defect Rate 5 bars, Production Volume Trend 1 line curve, all with
  9–13 axis ticks and correctly sized SVGs.
- **Material Mix (donut) returned a 14x14 surface even after the resize**, while
  its wrapper measured 415px. **Still uninvestigated.** Re-run the diagnostic and
  check whether the first-paint fix resolved it.
- **Widget query ground truth:** the measure is typed `value:number`, e.g.
  `{"day":"2026-07-02","dimensionLabel":"2026-07-02","value":29,"observationCount":29,"secondaryCount":0}`.
  **This killed the string-measure theory** — a coercion pack was built, applied,
  gated green, proven a no-op, and reverted.
- **Roughly half of all `widgets/query` responses returned `rows=0`.** Those
  widgets are honestly empty. **This is a data/filter question and is still open.**

## 5.2 recharts behaviour, tested in a Node/jsdom harness (my environment)

Installed React 19.2.6 with recharts **3.8.1 and 3.10.0** and rendered the exact
component patterns:

| Case | Result |
|---|---|
| Pie, numeric measure | 6 sectors, 3 legend items |
| Pie, **string** measure | **0 sectors**, 3 legend items |
| Bar / Line, string measure | renders fine, identical path geometry |
| Pie, string measure after coercion | sectors restored |

**Lesson recorded:** the recharts `Legend` is an **HTML div outside the SVG**. A
rendered legend proves nothing about whether the SVG has size. A string measure
and a zero-sized surface produce an identical symptom. This cost one wrong
diagnosis; do not repeat it.

## 5.3 Time-range helper, unit-tested in Node

`day`, `week` (ISO-8601), `month` — verified across Monday boundaries, week 01,
week 53, December rollovers, and invalid inputs returning `null`.
`2026-W19` → `2026-05-04T00:00:00.000Z .. 2026-05-10T23:59:59.999Z`, start
weekday 1 (Monday). Correct.

## 5.4 Build gates

| Gate | Result |
|---|---|
| `tsc -b` after chart fixes | clean, ~10s incremental / ~33s full |
| `dotnet build` after Program.cs v1 | **failed** — `CS1061 MapVisualMapperEndpoints` (missing `using`) |
| `dotnet build` after Program.cs v2 | **clean** |
| `tsc -b` after Scenes 5–8 | clean |
| `tsc -b` after nav fix | clean |

Pre-existing backend warnings (23 of them: `CS8604` nullability, `CS0162`
unreachable code in `V5EnterpriseSsoScimEndpoints.cs:694`, and
`CS1998 VisualMapperEndpoints.cs:133` async-without-await) are **unrelated to our
changes** and were present before.

## 5.5 Karim's own deep sweep (`PPIQ_Deep_PreCertification_Sweep_V4_FINAL.ps1`)

Reported 13 passed / 9 failed. **Recounted:**

- **Three were pure false failures** caused by a stderr trap:
  `frontend-build` (the build SUCCEEDED — 2519 modules, full dist listing — and
  failed only on vite's chunk-size warning going to stderr),
  `frontend-openapi-drift` (informational skip), `frontend-bundle-size` (report
  printed in full, then a blank stderr line).
- **Three were real gate failures masked by the same trap:** `standard-imports`
  (116 findings / 21 native controls / 95 inline styles), `action-buttons`
  (1 finding), `fields` (21 findings). Pre-existing frozen UI debt.
- **The browser route sweep tested 1 route out of 38.** The generated config is
  fail-fast; `/dashboard` failed and Playwright aborted. **37 routes never ran.**
  Re-run with `--max-failures=0`.
- **The 12 visual-regression "failures" were baseline creation on first
  execution** (`A snapshot doesn't exist ... writing actual`). That suite had
  genuinely never executed before because CI only ever `--list`ed it.
- `backend-full-test-suite` reported PASS with **Failed 0, Passed 66, Skipped 91,
  Total 157** — 58% skipped, including the entire ConnectorTruthContract family.
- `static-contract-audit` reported PASS while carrying **5 CRITICAL and 4 HIGH**
  findings, because the audit script exits 0. **Contents of `STATIC_AUDIT.md`
  never reviewed — still open.**

**Root cause of the false failures:** the sweep runs
`& $Command @Arguments 2>&1 | Tee-Object` under `$ErrorActionPreference = "Stop"`,
so any tool writing to stderr raises a terminating `NativeCommandError`.
**One-line fix: `Continue` inside the runner, gate on `$LASTEXITCODE` only.**
This has not been applied.

## 5.6 Audit signal report (25-Jul, unchanged from 22-Jul)

54 signals: 12 CRIT / 35 WARN / 7 INFO. **Triaged — most are false:**

- `catchError forcing SUCCESS` (3): two are the detector's own regex table, one
  is a test *method name* asserting the opposite. `Jenkinsfile` has zero
  `catchError`. **Clean.**
- `dev seed endpoint` (15): `Program.cs:1005` wraps `app.MapDevSeedEndpoints()`
  in `if (app.Environment.IsDevelopment())`, enforced by
  `ProductionDevEndpointGuardTests`. All 15 hits are the guard, the release stub,
  the packer, or docs. **Clean.**
- `wrong connection-string key` (1): the auditor's own regex. **Clean.**
- `--list` (8): six are forbidden-string literals inside the guard that bans
  `--list`. **The one genuine item is `package.json` `phase9:matrix`.**
- `hardcoded IP` (15) and `bootstrap admin` (2): real, low severity, M2 hygiene.

**Audit tool defect:** `GeneratePlantProcessIQ_UltimateAudit.ps1` does not exclude
itself; 6 of 54 signals including 4 of 12 CRIT are the detector matching its own
pattern table. Needs a `$PSCommandPath` skip.

**Also found, and the audit missed it:** `tools/ci/validate-real-ui-gates.cjs`
(PPIQ-T016) is **invoked by nothing** and **would fail if it were** — it requires
the Jenkinsfile to contain `npm run test:visual`, `test:phase56:e2e` and
`test:a11y`, and the Jenkinsfile contains none of the three. Stage 5 runs
`deploy/scripts/ci-e2e-stack.sh`, which ends at `npm run e2e`. The
visual-regression and a11y suites exist and never execute in any pipeline.

---

# 6. FINDINGS DISCOVERED BUT **NOT** FIXED — the next session's queue

## 6.1 Scene 9 — the demo path breaks between Scene 8 and Scene 9 **[HIGHEST PRIORITY]**

**Four outcome-key defaults across the app, three different values:**

| Surface | Default outcome |
|---|---|
| `pages/Analytics/AdvancedAnalysisPage.tsx:30` (findings) | `defect.edge_crack_rate` |
| `pages/Phase8/SuggestionRecommendationPage.tsx:10` | `defect.edge_crack_rate` |
| `pages/Analysis/AnalysisToolboxPage.tsx` `OUTCOMES[0]` | `defect.class` |
| `pages/AnalysisJobConfigPage.tsx` | `defect.rate_per_m2` |

Scene 8 runs an analysis on `defect.class`. Scene 9 then opens findings filtered
to `defect.edge_crack_rate` — **which is not even in the toolbox's list**, so
there is no way to run an analysis that populates the findings page's default
view.

**This is very likely the real cause of the "Findings page is empty while the DB
says 320 rows" symptom**, which has been attributed to an RLS tenant mismatch.

**The two-minute check that settles it, and it was never run:**
open `/investigate/advanced?outcomeKey=defect.class` and see whether findings
appear. **Do this first.**

Same page also disagrees on `windowDays` (30, vs the toolbox's 3650) and
hardcodes `grain = "coil"` (steel-specific, Rule-2 issue).

## 6.2 Not covered at all — no deep pass yet

- **Scene 10** — supervisor page.
- **Scene 11** — assistant (`components/assistant/AssistantChat.tsx`, 178 lines;
  `pages/Phase8/AssistantRuntimePage.tsx`).
- **Scene 12** — everything except the contact address: scroll-drawn SVGs,
  integration ecosystem, ROI sliders, CTA scroll behaviour.
  (Contact address **is** `info@plantprocessiq.com`, in
  `Website/.../content/phase1WebsiteProof.ts:172` and the footer — real-looking,
  but Karim must confirm the mailbox is monitored.)

## 6.3 Known open items, ranked

1. **Add-widget entry on the workspace page.** Deliberately excluded from the
   Scenes 5–8 pack: mounting `WidgetBuilderWizard` requires reading its data
   contract end to end, and guessing at a wizard before a demo is the wrong risk.
   Doctrine S6 (low-code page authoring) has no click path without it.
2. **Material Mix donut, 14x14 surface** after resize. Re-measure first.
3. **~Half of widget queries return `rows=0`.** Which widgets, and why.
4. **`STATIC_AUDIT.md`** — 5 CRITICAL and 4 HIGH never read.
5. **The sweep's stderr trap** — one line, turns six false failures into passes.
6. **`phase9:matrix --list`** in `package.json` — the one genuine `--list` CRIT.
7. **`validate-real-ui-gates.cjs`** unwired and would fail.
8. **Outcomes/grains have no server registry endpoint.** Making those dropdowns
   dynamic is backend work, not a demo patch. They are now declared once and
   exported in `AnalysisToolboxPage`, so it becomes a two-line change when the
   endpoint lands.
9. **Presentation certification layer** (`Install-PpiqPresentationCertification.ps1`)
   was delivered but **never installed** — Playwright suites for Qlik
   interaction, no-code surfaces, assistant contract and golden journey.

## 6.4 Accepted for this demo (Scene 1 cut)

Karim decided **not to show a login page**. Therefore accepted as-is:

- **There is no login screen.** Zero `type="password"` inputs in the entire src
  tree. `AuthContext` auto-authenticates on boot via
  `apiClient.login(DEMO_USER, DEMO_PASS)` from `VITE_SMOKE_USERNAME` /
  `VITE_SMOKE_PASSWORD` (defaults `admin` / empty string).
- **`VITE_*` variables are compile-time inlined by Vite**, so that credential is
  readable in `dist/assets/index-*.js` and in DevTools → Sources. **Real finding,
  deferred, must be closed before any customer install.**
- **A Logout button exists** (`AppLayout.tsx:315`) with no way back in except a
  page reload. **Do not click it during the demo.**

---

# 7. DEPLOYMENT, SERVER AND PIPELINE — HONEST STATUS

**Read this carefully: almost nothing here was verified this session.**

### What was decided

Karim will **present from his local laptop.** He explicitly declined to touch the
server before the demo, to avoid spending time on Docker, Jenkins, environment
variables and logins. **The first item of M2 is now the auto-deployment
infrastructure.**

### What we know (from documents, not from testing)

- Deployment is Jenkins-driven, triggered on `git push origin main`.
- Server is `178.105.152.180`; app and API on `sslip.io` subdomains behind Caddy.
- Server secrets live in `/var/lib/ppiq-preserve/.env`, git-ignored.
- `Jenkinsfile` has **no `catchError`**; stages 3/4/5 are blocking and textually
  ordered ahead of every migrate/seed/deploy stage. That structure is sound.
- Stage 5 runs `deploy/scripts/ci-e2e-stack.sh`, which ends at `npm run e2e`.
- Hardcoded IP `178.105.152.180` appears in 15 places across deploy scripts and
  docs — M2 config hygiene.
- `env/profiles/local.env` and `presentation.env` both set
  `PlantProcess__Auth__Users__0__IsBootstrapAdmin=true` — correct for the demo
  instance, **must be disabled in any customer deploy**.

### What was NOT done — do not assume otherwise

- **No deployment was run.**
- **No pipeline was run.**
- **No server access of any kind.**
- **No app-URL verification.**
- **No modifications were made to make the pipeline green or the app URL work.**
  Karim's request item 10 has no content because no such work was performed.
- **The server is running older code.** Nothing from this session has been pushed.

### The consequence that must not be forgotten

If anyone later demos from `https://app.178.105.152.180.sslip.io`, **none of this
session's work exists there.** No KPI tiles, no canvas route, no toolbox, red
banner still on Prepare Import, empty WORKSPACES group, parity tautology intact.

---

# 8. TIPS, TRICKS AND HARD-WON LESSONS

## 8.1 Pack-authoring failures I made — do not repeat them

1. **Never build code lines by PowerShell string concatenation inside an array
   literal.** `'    // ' + $Marker + ': text'` inside `@(...)` emitted **three
   separate array elements**, splitting one comment across three lines and
   producing a bare `:` that broke `ChartExtras.tsx` with `TS1128`. The anchor
   log reported all five matches correctly while the bytes on disk were wrong.
   **Use literal single strings, or write the file whole.**
2. **Never quote, in a code comment, a literal token that a self-check guard also
   searches for.** This caused **three** false rollbacks in one day: `#E5484D`
   in a comment tripped "the red hex survived"; `const formPayload = canvasPayload`
   tripped the parity guard; `Reset grid` in a comment tripped the duplicate-button
   guard. **Simulate every guard against the embedded content before shipping.**
   That practice caught the third one before it reached Karim.
3. **Never use `npx` in a gate.** `npx tsc -b` hung for over ten minutes — when
   npx cannot resolve a binary locally it contacts the registry and may prompt
   for input that never arrives. **Always invoke
   `node node_modules/typescript/bin/tsc -b` directly.**
4. **Never capture external command output when a gate might be slow.** A captured
   gate is indistinguishable from a hung one. Stream it live.
5. **`Start-Process -PassThru` + `WaitForExit(ms)` does not reliably populate
   `.ExitCode`.** It returned `$null`, and `$null -eq 0` is false, so a clean
   build was reported as a failure. **Use the call operator and `$LASTEXITCODE`.**
6. **`$ErrorActionPreference = "Stop"` turns any stderr write from a native
   command into a terminating error.** A *missing npm script* destroyed a
   compiling change. Wrap external calls in `Continue` and judge by exit code.
7. **`tsc` caches errors in `*.tsbuildinfo` and replays them.** A second run
   producing identical errors in 2.9s instead of 33.4s means it re-reported
   cached errors and the file never changed. **Delete `*.tsbuildinfo` before any
   gate whose result you intend to trust.**
8. **Some source files carry existing non-ASCII characters** (`App.tsx` has 1,
   `DashboardFilterBar.tsx` has 206 — em-dashes and box-drawing). Those files
   **cannot** be embedded whole in an ASCII-only PowerShell here-string. Edit them
   by anchor and add a mojibake guard that compares the non-ASCII count before
   and after.

## 8.2 Investigation lessons

- **The recharts `Legend` is HTML, outside the SVG.** A rendered legend says
  nothing about SVG size. This produced one wrong diagnosis.
- **`res.ok` can be `true` for a completely failed API call.** Vite's history
  fallback answers unknown paths with `index.html` and HTTP 200. Any relative
  fetch from the frontend origin will appear to succeed.
- **Files can change between reading them and shipping a pack.**
  `InteractiveCharts.tsx` changed twice in one hour. **Always print file hash and
  timestamp before editing, and anchor on structure rather than exact prop
  strings.** The drift-proof regex approach (match any prop order, skip
  already-patched) is the pattern that survived.
- **Read the snapshot as a real tree.** Extracting the concatenated snapshot into
  1,996 real files and scanning it found things grep could not — dead pages, nav
  gaps, the single raw `fetch`, design-system violations by file.
  **The extraction script pattern is worth rebuilding:** the metadata wrapper
  lines are LF-terminated while the file bodies are CRLF, so split on `\n` and
  strip trailing `\r`.

## 8.3 Product-shaped lessons

- **Default parameters fire on `undefined`.** `field = "materialCode"` combined
  with a lookup that returns `undefined` is how a fabricated filter reached the
  URL. Prefer `null` and explicit guards.
- **A guard satisfiable by its own prose is worse than no guard.** The parity
  panel compared an object with itself and displayed a green IDENTICAL. Karim's
  own constitution forbids exactly this.
- **Retitle when you repoint.** A widget whose title says one thing while it plots
  another is worse than a broken one.
- **Honest empty beats false content.** Zero-row associative fields now say `n/a`;
  a missing parameter catalogue says so; an empty nav group says so.

---

# 9. REALIZATION SCOREBOARD — end of session

Persona scores are the senior's external assessment, adjusted for what this
session changed. Anything not runtime-verified is marked.

| Area | Start of session | Now | Basis |
|---|---|---|---|
| M1-01 wire-up | open since 22-Jul | **DONE** | routes + registration, both gates clean |
| Scene 1 Login | broken | **cut by decision** | no login page exists |
| Scene 2 Connections | copy defect | **fixed** | wiring was already sound |
| Scene 3 Prepare Import | worst page on the path | **fixed** | full restyle, red banner gone |
| Scene 4 Genealogy | dev tool on the page | **fixed** | panel removed |
| Scene 5 Workspace | 6 defects | **fixed** except Add-widget | D1–D6 + filter + drawer + reset |
| Scene 6 Associative | 5 defects | **fixed** | filters, n/a, parallel, palette, grid |
| Scene 7 Canvas | **unreachable** | **live** | best-built surface in the product |
| Scene 8 Toolbox | **unreachable + tautology** | **live + real parity** | gates and run id added |
| Scene 9 Findings | not reviewed | **defect found, NOT fixed** | outcome-key mismatch |
| Scenes 10–12 | not reviewed | **not reviewed** | — |
| Widget definitions | 4 returning 400 | **0 invalid** | both proof counts 0 |
| Infrastructure (A13) | 45/100 Critical | **unchanged** | nothing deployed or measured |

**Everything in the "fixed" column is code-verified and gate-verified. Almost
none of it is runtime-verified in a browser.** That distinction matters and Karim
holds to it: the consolidated pass has still not been walked.

**Honest headline:** the demo-path surfaces are in materially better shape than
at session start, and two headline features moved from unreachable to live. The
infrastructure persona is untouched and remains the lowest score, which by
Karim's own scoring law is still the shipping headline.

---

# 10. WHAT THE NEXT SESSION SHOULD DO, IN ORDER

1. **Run the two-minute outcome-key check** — `/investigate/advanced?outcomeKey=defect.class`.
   If findings appear, cut a pack that puts all four surfaces on one shared
   exported constant. This may close the "empty findings page" problem that has
   been mis-attributed to RLS.
2. **Re-run `Invoke-PpiqChartDiagnostic.ps1`** and confirm the size-warnings block
   reads `none` and Material Mix is no longer 14x14.
3. **Deep pass on Scenes 10, 11 and the rest of 12** using the three-lens concept
   in section 1.3.
4. **Walk the consolidated pass in a browser.** Nothing is runtime-verified.
5. **Commit after every green pack.** Non-negotiable — see section 3.

**Ask Karim to re-upload the current repository snapshot at the start.** The tree
has changed materially since 25-Jul 09:45 and every file hash in this document is
now historical.

---

*Written at the end of the 25-Jul session. Every claim here is either measured,
quoted from a tool's output, or explicitly marked as unverified. Where this
document says something was not done, it was not done.*

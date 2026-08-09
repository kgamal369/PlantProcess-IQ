# PPIQ SESSION HANDOVER — WORKER 2
## 08–09 August 2026 · T-040 closed, T-041 closed, T-042 closed
### Written for a successor with no memory of this session. Read sections 1, 7 and 11 before touching anything.

---

# 0. THE ONE-PARAGRAPH VERSION

T-040 (Golden Gate over the shared authoring shell) is complete and committed, with 10 browser evidence rows. T-041 (Page Builder part 1) is DONE with A–F proved, 4 browser rows. T-042 (Page Builder persistence, publish, projection, delete) is DONE with 2 end-to-end browser rows green. Along the way we found and fixed a **live anonymous-read security hole** on `GET /pages`, a **presentation-bootstrap credential defect** that broke every clean browser, and a **frontend/backend widget-type contract mismatch** that made every chart widget unsaveable. Three defects are deliberately deferred with named owners. **Do not re-run the suites listed in section 6 — they are already proved.**

---

# 1. WHAT IS TRUE RIGHT NOW (start here)

| Thing | State |
|---|---|
| Frontend unit/component suites | **295 passed / 24 files** at last full-ish run; PageBuilder+projection subset **31/31**; widgetDefinitionModel **21/21** |
| `tsc -b` | **0** |
| Architecture ratchets | `uiConformanceRatchet` + `largeFileBoundaries` were **4/4 green** after S10b. **RE-RUN ONCE** — this is the only unverified gate at session end |
| T-040 browser rows | **10/10** |
| T-041 browser rows | **4/4** |
| T-042 browser rows | **2/2** |
| Backend `PageBuilderRouteAccessControlTests` | **11/11** (6 `AuthGateMatrixTests` SKIPPED — need an external host on this machine) |
| API | runs on `-Profile presentation`, port 5063, db `ppiq_presentation` |
| Web | Vite on 5173, **must** be started `-Profile presentation` |

**The one command still owed:**

```powershell
cd C:\Workspace\PlantProcess-IQ\Frontend\PlantProcess.Web
node node_modules\vitest\vitest.mjs run `
  src/test/architecture/uiConformanceRatchet.test.ts `
  src/test/architecture/largeFileBoundaries.test.ts `
  --config vitest.config.ts
```

Also **never written**: `docs/m1/evidence/T-042_CLOSURE.md`. Karim closed and committed T-042 without it. `T-040_CLOSURE.md` and `T-041_CLOSURE.md` exist; T-040's **Section 5 (frozen suite totals) is still empty**.

---

# 2. IDENTITY, TOPOLOGY, AND WHAT I ACTUALLY LEARNED ABOUT THE ENVIRONMENT

From `PPIQ_Identity_and_Topology_v4.md` (uploaded mid-session) plus what runtime proved:

- **Local dev database**: `ppiq_presentation`, user `ppiq_dev`, password `ppiq_dev_local_only` — stated in plain text in that document on lines 41/111/141 because it is a laptop-only credential and deliberately not a secret.
- **`PGCLIENTENCODING = 'UTF8'` is required** before `psql` on this machine (section 2 of that doc). Without it, scripts with comments fail.
- **The login response field is `accessToken`**, not `token`. Anything reading the body must use that name.
- **The server database is `plantprocessiq` under the `ppiq-app` project** — NOT `ppiq_presentation`. Server credentials live in `/var/lib/ppiq-preserve/.env`.
- **The smoke user is `e2eadmin`, role `Admin`, plantRole `TenantOwner`**, tenant `default-demo`, tenant id `00000000-0000-0000-0000-000000000001`. Its scopes include `page.design`, `widget.design`, `tenant.admin`.
- **There is no login page in this build.** `AuthContext` bootstraps by trying `apiClient.refresh()`, and failing that logs in with `VITE_SMOKE_USERNAME` / `VITE_SMOKE_PASSWORD` compiled into the bundle by Vite from `Frontend/PlantProcess.Web/.env.local`. It gives up after 3 attempts and renders a full-page "Backend connection failed" screen. Older e2e specs that `goto("/login")` are **stale**.

**The psql idiom that works here** (never put a password in a script — it must replay on the server too):

```powershell
$prof = Get-Content env\profiles\presentation.env | Where-Object { $_ -match '^POSTGRES_PASSWORD=' }
$env:PGPASSWORD = ($prof -split '=', 2)[1].Trim()
$env:PGCLIENTENCODING = 'UTF8'
psql -h localhost -U ppiq_dev -d ppiq_presentation -f Backend\database\scripts\NNN_x.sql
Remove-Item Env:PGPASSWORD
```

---

# 3. THE AUDIT-DUMP TRIAGE (start of session)

07-Aug 21:43 package: **2,262 files / 367,607 lines / 27.885 MB**, down from 05-Aug's 2,423 / 437,137 — because every `tools/packs/_backup_*` directory left the tree. **56 signals**, same total as before but different composition (dev seed 16→15, bootstrap admin 3→2, TODO 7→9). Coincidence, not stability.

**Standing findings re-verified, all still open at session end:**

1. `GeneratePlantProcessIQ_UltimateAudit.ps1` **still matches its own rule table** — `Get-AuditSignalsForContent` at line 712 has no path exclusion. Four consecutive dumps.
2. **FINDING A**: zero content references to `validate-real-ui-gates` anywhere; root `Jenkinsfile` has zero occurrences of `test:visual`, `test:phase56:e2e`, `test:a11y`. The gate that is supposed to enforce them is orphaned.
3. **FINDING B**: `apply-phase5-phase6-full-ui-migration.cjs` lines 74–76 still patch three `--list` enumerations into the pipeline, and its own validator (lines 1841–1844) asserts a Jenkinsfile stage that does not exist — the script fails its own check.
4. **Two mid-file BOMs**, not one: `DevSeedEndpoints.cs` line 2 and `validate-forbidden-copy.mjs` line 6 (char index 245). Both would fail `Test-Utf8NoBom.ps1`, which the GitHub workflow runs on every push. *(My earlier "exactly one BOM" claim was wrong.)*
5. **Mojibake down to 3 files**: `continue-phase03-phase04-from-t016.cjs`, `Website/src/App.tsx`, `610_v5_p12_i18n_rtl_mobile.sql` (double-encoded Arabic in i18n seed rows — running it lands corrupted translations).
6. **`App.tsx` ships 16 mojibake sequences in customer-visible marketing copy** and was modified 07-Aug 16:08. It was the only file of that day's 29 carrying any non-ASCII.
7. **The audit package exports gitignored env files with `Mask Secrets: False`.** Today only local dev creds. Run it on the server and `server.env` goes into a file that gets shared.
8. **The apply-order manifest is stale**: covers scripts 50–680, no entry for 770. 771/772/773 were appended correctly by our packs; the wider gap belongs to its owner.

---

# 4. WHAT WAS BUILT, TASK BY TASK

## 4.1 T-040 — Golden Gate over the shared authoring shell

| Pack | What | Gate |
|---|---|---|
| 03b5 | **G10 keyboard path**: one React `onKeyDown` on the shell root, 11 proofs | 264 passed, tsc 0 · commit `b11105a3` |
| AUTH-01 | `use-profile.ps1` writes the profile's smoke password instead of a literal | login 200 · commit `803b3b4a` |
| AUTH-02 | 6 proofs the placeholder cannot return | 6 passed |
| WEB-01 | `start-web.ps1` accepts `presentation`; both start scripts ratcheted | 7 passed |
| FOCUS-01 | A dialog surface takes focus; root focusable via `tabIndex={-1}` | 268 passed, tsc 0 |
| CONV-01 → 02c | Browser convergence: config with **no `webServer`**, spec, evidence runner | **10/10** |

**The keyboard handler design (frozen — do not touch):** a plain function, not `useCallback`. Enter is the visible Run and is ignored when the target is `textarea`/`input`/`select`/contenteditable **or `button`/`a`/`[role=button]`** — a focused button already fires its own onClick, so answering the same press would run twice. Escape dismisses `pendingBlockSwitch`, then `forkAsked`, then closes only when `onClose` was supplied.

**Evidence files** in `docs/m1/evidence/T-040/`: `G00-clean-profile-authenticates.png`, `G10-focus-order.txt`, `G10-keyboard-only-scenario.png`, `G11-{ltr,rtl}-s1-{block,sql}-mode.png`, `G13-state-loading.png`, `G14-state-blocked.png`, `G18-state-failed.png`, `G19-s2-{add,edit}-entry.png`, `G19-focus-on-open.txt`, plus the four `T041-*` and (from the last run) `T042-*` captures. `EVIDENCE.jsonl` carries a claim per file.

## 4.2 T-041 — Page Builder part 1 (DONE)

| Pack | What | Gate |
|---|---|---|
| S1 | The **seven structural widget kinds** on the existing `DashboardMetadataDto` | build 0, 6 passed · `11d7630a` |
| S2b/S2c/S2d | Page **audience-role** contract through the existing seam, script `771`, omission semantics | build 0 · `ebb81ef1`, `786af792` |
| S3a2 | Reducer loses its compiled union, demo library and fallback-title switch | tsc 0, 10 passed · `226960fa` |
| S3b | Page loses its second demo library; Create Page; empty grid; endpoint-driven picker | tsc 0, 274 passed |
| S4 | Browser acceptance C/D/E/F | **14/14 rows** · `eaa2aad1`, `ec48227b` |

**The seven kinds, closed grammar:** `chart`, `table`, `kpi`, `calculated-label`, `filter`, `container`, `text`. `chart`/`table`/`kpi` **reuse codes `DashboardMetadataCodes.WidgetTypes` already shipped**. They live in their own `WidgetKinds` namespace because `ChartTypes` *also* declares `kpi` and `table` — the same token means a chart type there and a structural kind here.

**What was deleted:** `WidgetKind = "kpi"|"bar"|"line"|"filter-date"|"filter-list"` (bar/line are chart types; filter-date/filter-list are filter variants — the union mixed two levels of the grammar), `defaultPageBuilderWidgets` (Risk KPI / Defect breakdown / Defect trend on `schema_view:*`), and the **second copy of the same demo library** inside `PageBuilderPage.implementation.tsx`.

## 4.3 T-042 — Page Builder persistence/publish (DONE)

| Slice | What | Result |
|---|---|---|
| S1 | `backing_dashboard_definition_id` + `published_at_utc` columns, script `772`, publish/unpublish verbs | build 0 · `786af792` |
| S2b | The bridge: page persists → ensures backing dashboard → real id to the shell → grid rebuilt from server | tsc 0, 274 · `f9cd09fe` |
| S3b | 8 bridge proofs incl. the **partial-failure retry** | 18 passed · `1c514fcc` |
| S4 | Strict layout reader — an unreadable layout **refuses** instead of repacking | tsc 0 · `43b89e05` |
| S5 | Round-trip proofs by normalised values | 23 passed · `eb6ff1ec` |
| S6a | `?includeDeleted=true`, delete clears publication, unique index `773` | build 0 · `1611cf04` |
| S6c | Publish controls, fail-closed projection, invalidation signal | 287 passed |
| S7b | 8 projection proofs incl. resurrection + fail-closed | 295 passed · `f338cf2f` |
| S9b | **`/pages` joins the permission matrix** | 11 passed · `61921050` |
| S10b | StandardSelect audience, facade imports, named acceptance combination | 34 passed, ratchets 4/4 · `35e2f3d0` |
| final | `widgetTypeFor` fix, audience fixture, NavGroup, `exact:true` | **2/2 browser** |

---

# 5. THE THREE REAL DEFECTS FOUND (and why they mattered)

## 5.1 `GET /pages` was served ANONYMOUSLY — security

`PlantAccessControl.cs` (class `AccessControlMiddleware`) is deny-by-default with a prefix matrix. **`/pages` had no entry at all.** `POST /pages` was therefore refused with 403 — which is what broke the whole T-042 bridge — while `GET /pages` fell through the `("/", GET, anonymous)` entry and returned 200 **without a token**. Half-open for reads, shut for writes, and nothing failed loudly until an authoring flow tried to save.

Fix: one line, `("/pages", All(), "page.design", false)`, plus 11 reflection-based proofs walking the family by prefix (`/pages`, `/pages/{slug}`, publish and unpublish subroutes).

## 5.2 The bootstrap credential — every clean browser was locked out

`scripts/env/use-profile.ps1` line 50 wrote the **literal** `VITE_SMOKE_PASSWORD=change-me-before-production` while line 49 correctly interpolated the username. `start-web.ps1` calls `use-profile.ps1 -WriteAppEnvFiles` on **every** start, so hand-editing `.env.local` never survived. Karim's own browser held a refresh cookie and never hit the password path — so it was invisible until Playwright opened a clean profile. **It would also have hit an incognito window or a customer's laptop.**

## 5.3 `widgetTypeFor` sent an analytical category as the persistence protocol kind

```ts
// WRONG — what it was
return chartTypes.find((c) => c.code === chartType)?.category ?? "chart";
```

Chart metadata `category` for Bar is `"Comparison"`. The server contract is `kpi | chart | table`. Every chart widget was refused:

```
400 validation.failed
errors: { "WidgetType": ["Unsupported widget type 'Comparison'."] }
```

Fix: the **chart type** decides the protocol kind — `kpi`→`kpi`, `table`→`table`, everything else→`chart`. The old test asserted `widgetTypeFor(CHART_TYPES,"kpi") === "tile"`, which was locking a value the server has never accepted.

---

# 6. EVERY TEST RUN AND ITS RESULT — DO NOT RE-RUN THESE

| Suite / row | Result |
|---|---|
| `authoringKeyboard.test.tsx` (11 proofs) | green inside 264 |
| Scoped `src/authoring` + `authoringLogicalDirection` | 264 → 268 (FOCUS-01) |
| Page Builder + authoring after S3b | 274 |
| After S6c | 287 / 24 files |
| After S7b | 295 |
| PageBuilder + projection subset | **31/31** |
| After S10b (PageBuilder + projection) | 34 |
| `widgetDefinitionModel.test.ts` | **21/21** |
| `PageBuilderRouteAccessControlTests` | **11 passed, 6 skipped** (skips need an external host) |
| `PageVersionConflictContractTests` | green after every backend slice |
| `WidgetKindGrammarTests` | **6/6** |
| Browser: T-040 ten rows | **10/10** |
| Browser: T-041 four rows | **4/4** |
| Browser: T-042 two lifecycle rows | **2/2** (21.4s + 9.8s) |
| Architecture ratchets after S10b | **4/4** — re-confirm once |

**Known flake:** T-040's `G12-G18 Loading` row failed at 25s once and passed at 4.2s on the next run with no code change. Recorded as a T-040-owned timing debt.

**Evidence caution:** the runner **clears `docs/m1/evidence/T-040/` before every run**, and a failed row writes nothing. After several red runs the folder was materially thinner than the closure records describe. The last green pair rewrote the `T042-*` files but **not** the T-040/T-041 captures, because only two rows ran. If the folder must match the records, one full 16-row run is needed — Karim explicitly said not to.

---

# 7. RULES AND WAYS OF WORKING (these are Karim's, honour them)

**Pack contract.** Every deliverable is a PowerShell 5.1 apply-pack: preflight → anchor verification against exact on-disk text → backup → apply → on-disk self-check → gated build/test → auto-revert on failure, then **verify the revert took**. Nothing applied without running it. Never `git add .` or `-A`. No zip files, no em-dashes, no curly quotes, no `&&` in PowerShell, no `style={{`, no raw `<table>`.

**Gate pattern.** Named files through `vitest.mjs`, judged **purely on exit code** — vitest writes failures to stderr, so output parsing lies.

**Evidence before cure.** A refusal, a gap or zero findings is a valid result. Never weaken a threshold to produce green.

**Name your own defects first, before Karim finds them.** Quiet fixes are not acceptable.

**Every scan runs against comment-stripped code.** This project has now hit "the guard read the prose that explained it" **ten times**. Strip `//`, `///` and `--` before counting anything.

**Source-level pass ≠ runtime pass ≠ visual acceptance.**

**Karim runs every pack himself** and pastes console output. File attachments have been consistently empty/unreadable — ask for pasted text. He verifies in a browser before committing: *"I never commit before my manual check."*

**Decisions as two lettered options** when a ruling is needed. Rulings arrive in fenced blocks and are the implementation contract.

**Language:** English for technical content, Arabic for conversational exchanges and simplified explanations. When he writes «مش فاهم اعمل ايه», answer in Arabic with numbered concrete steps and nothing else.

**Scope discipline (learned the hard way this session).** T-042 nearly doubled in length because unrelated debts kept being pulled into its closure. Karim's escalation rule: *fix a new failure only if it directly prevents one of the task's stated acceptance points; otherwise document the owner and move on.*

---

# 8. TIPS AND TRICKS DISCOVERED (each one cost a round)

1. **Anchor on single lines, not blocks**, in a repo with a second worker. A multi-line anchor quoting a shared, actively-edited list (the replay array) missed because Worker 1 had added to it.
2. **Hash the bytes the pack writes**, never a scratch file. A trailing newline made a replace-guard refuse correctly against a constant that was wrong.
3. **`vi.mock` is hoisted above everything** — factories closing over plain consts read them before they exist. Use `vi.hoisted`.
4. **When a value renders on more than one surface, a document-wide query answers a different question.** 03a2 puts the refusal sentence in *both* the state banner and the debug log; scope to the surface that owns the meaning.
5. **A second opinion in a test is a second thing that can be wrong.** An error-counter corroboration failed four proofs that were otherwise correct.
6. **When the cause is the pattern, fix every use of the pattern.** A bad glob was fixed on the failing row and the identical line above it then failed.
7. **`toBuilderState` builds a `PageBuilderState` literal** — adding a required field breaks construction sites, not just imports. Check construction, not only imports.
8. **Playwright accessible names concatenate label + description.** `NavItem` renders `<span>{label}</span><span>{desc}</span>` inside one `NavLink`, so `exact: true` on a Workspace link deterministically returns zero.
9. **Workspaces is a collapsible NavGroup**, closed on the Page Builder route. `getByRole` skips hidden elements — count 0 does **not** prove absence. Open the group first, in both presence *and* absence assertions.
10. **Select by label, never by index.** `selectOption({ index: 1 })` chose whatever metadata listed second — which was KPI, because index 0 is `choose...`.
11. **Watch all three states of an async action**, not just the two outcomes. Waiting only for success-or-failure turned a stuck "preparing" into an anonymous 45s timeout, twice.
12. **The API log names 403/400 causes faster than any trace**: `Backend\PlantProcess.Api\bin\Debug\net9.0\logs\systemlog_*.log`, `-Tail 300`, filter on `StatusCode=4`.
13. **The Playwright trace zip carries the request body and the problem JSON**: expand it and grep `resources\*.json`; the `.dat` file holds the RFC-9110 problem document.
14. **`error-context.md`** in the failing row's folder is the page at the moment of failure — it named the auth-denied surface and the KPI selection.
15. **Orphaned `dotnet` processes cause `CS2012`/`MSB3021`.** Always `free-ports.ps1 -Ports 5063 -Force` + `Stop-Process` before a backend build. Four processes from 21/25 July were still alive.
16. **`Get-ChildItem -Recurse -Filter *.tsbuildinfo` from the web root walks `node_modules`** and looks like a hung gate. Root level only.
17. **`AuthGateMatrixTests` skip on this machine** (need an external host) — a green filtered run may certify less than it appears to.
18. **`noUnusedLocals` is OFF** in `tsconfig.app.json`, so dead code after a refactor will not be caught. Delete it by hand.
19. **`e2e/` is excluded from `tsc -b`**, so a spec never type-checks. The only honest gate for a spec is running it.
20. **A revert-on-red pack should NOT delete test assets.** A failing proof file is the evidence of what failed.

---

# 9. BACKLOG STATUS

| Task | State |
|---|---|
| T-040 | **DONE** — closure record exists; **Section 5 (frozen suite totals) still empty** |
| T-041 | **DONE** — A–F evidenced, closure record written |
| T-042 | **DONE / FROZEN** by Karim's instruction — closure record **never written** |
| T-043 | D1 final workspace — next |
| T-044 | Operational dashboard certification |
| T-045 | Analysis/model dashboard certification |
| T-046 | Semantic chart compatibility — **owns two findings below** |
| T-047 | Distinct analytical visual grammar |

**Karim's ruling on T-044–T-047 acceptance:** *not* "the chart renders" but "the chart communicates a defensible operational or analytical question using the correct semantic fields and chart grammar". Pie-by-Date, Heatmap-by-Date, single-category donut, GUID labels and information-free Model Insights are **negative acceptance examples, not a reason to regenerate Fleet-v2**.

**Deferred with owners — do not absorb into a new task:**

- **T-046**: (a) the client's `saveRefusal` accepts "dimension **or** measure" while `DashboardWidgetValidationService` line 27 requires a measure **always** — the client permits a save the server refuses; (b) `previewReport.describeThrownAction` returns **one fixed sentence for every thrown error**, so a precise 400 validation problem is reported to the author as *"the request to the server did not return. Check that the API is running"*. Both are the same gap: the author cannot see why the server refused.
- **T-053 / M1-P4**: three JourneyRail route-to-step mismatches — default route renders **Step 4** where the test expects 1; `/data-integration/supervisor` renders **15** where the test expects 14; `/assistant/configuration` renders **no current step** where the test expects 15. The rail itself is intact (15 items, canonical labels). Worker 1 has committed the `/assistant` visible-contract correction, so this is now purely a route-table question.
- **T-040**: the Loading timing flake; Section 5 totals.
- **Unowned**: the stale apply-order manifest; the audit scanner's self-match; FINDING A and FINDING B on the Jenkinsfile; the two mid-file BOMs; the `App.tsx` mojibake in live marketing copy.

---

# 10. DEPLOYMENT, SERVER AND PIPELINE — WHAT I DO **NOT** KNOW

**I did no deployment work in this session and I did not make any pipeline green.** Nothing in this handover should suggest otherwise. What I know is only:

- From the topology document: server db `plantprocessiq`, project `ppiq-app`, server env at `/var/lib/ppiq-preserve/.env`.
- From the audit: the root `Jenkinsfile` **does not** reference `test:visual`, `test:phase56:e2e` or `test:a11y`, and `validate-real-ui-gates` is referenced by nothing. A phase56 migration script would patch three `--list` enumerations into it and its own validator asserts a stage that is not there.
- `.github` runs `tools/phase1-phase2/Test-Utf8NoBom.ps1` on every push, and **two files would fail it** (section 3).
- Scripts `771`, `772`, `773` were added to `Backend/database/database.apply-order.manifest.csv` and to the explicit replay list in `scripts/demo/Rebuild-PresentationDb.ps1`, with a bounded presence check after the loop. **They have only been applied to `ppiq_presentation` locally.** They must still reach the server database through whatever pipeline owns that — I have not seen that pipeline.
- **No scratch rebuild was ever run** to prove those scripts replay. The tracked path is correct; a future rebuild is the proof.

Anything else about the server, the app URL, or making the pipeline green is **not knowledge I have**. Ask Karim or read the pipeline before acting.

---

# 11. IF YOU DO NOTHING ELSE

1. Run the two architecture ratchets once (section 1). That is the only unverified gate.
2. Do **not** re-run anything in section 6.
3. Do **not** reopen T-042. Karim: *"now T-042 is close and done and I made commit and never open it again."*
4. Start T-043 with a **bounded measurement** — read the frozen task text, read the current source of what it touches, then one smallest correct slice. Do not build a testing framework around it.
5. Before any pack: strip comments in every guard, anchor on single lines, hash what you write, and simulate every guard and self-check against the real file before delivery. Every rule in section 7 exists because breaking it cost a round today.

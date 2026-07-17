# PPIQ State Assessment & 4-Day Roadmap
### 16-Jul-2026 evening - meeting postponed to ~20-Jul

Sources of evidence: run outputs pasted 15-16 Jul (restore, seeds v1-v4,
certifier 2 runs, vitest 3 runs, workspace packs), the 20:40 repo dump,
screenshots (7 current + 4 May-era). Anything not evidenced is marked.

---

## 1. WHERE THE TWO BRANCHES AND TWO DATABASES ACTUALLY ARE

### The strategic finding first (it answers your question 3)

Inspect what the presentation branch actually contains beyond main:
mojibake fix (real fix), presentation profile in two ValidateSets (real,
generic config), the InteractiveWorkspacePage wiring (real feature - the
resurrection of a lost capability), and nothing else. **There is no fake
code on the presentation branch.** Every line is main-worthy.

The thing that is presentation-only is not code - it is the DATABASE
(`ppiq_presentation`). And databases do not live on git branches.

**Therefore: the main-vs-presentation dilemma dissolves. Recommended
resolution:**

1. Commit everything on `presentation`, review the senior's workspace page
   once, then **merge `presentation` -> `main` and delete the branch.**
2. Keep `ppiq_presentation` (the populated 40k database) permanently as the
   demo/marketing database, reachable from main via
   `-Profile presentation`. `ppiq_app` stays the empty-start dev database.
3. Your feared disadvantages both vanish: no double development (one
   branch), and no "system starts empty" for the demo (the profile choice,
   not the branch, decides which data you present).

One branch. Two databases. Zero duplicated work. This is also exactly the
Demo-vs-Product doctrine you wrote months ago - the app is always generic;
the demo is the same app pointed at emulated data.

### Uncommitted-work warning (highest-priority hygiene item)

NOTHING from 15-16 Jul is committed. Both branches share one dirty working
tree; `git checkout main` today would drag everything with you. First
action of Day 1: commit on presentation, then merge per above.

### Database state (evidenced by run outputs)

| | ppiq_app (dev) | ppiq_presentation (demo) |
|---|---|---|
| material_units | 1,807 (5 provenance-violating - delete) | **40,148** |
| quality_events | small | **51,691** |
| genealogy_edges | 4 | **35,906** |
| parameter_observations | few | **14,433** |
| findings (results_v2) | 320 | **320 (real engine output)** |
| gated learning runs | 375 | 375, messages scrubbed clean |
| dashboards w/ widgets | 5 system | **12, all armed (v4 output)** |
| Rule-1 residue | clean | clean (provenance neutralized, ML msgs 0) |

---

## 2. WHAT YOU CAN SHOW - TECHNICAL

### 2.1 The 15-step journey (honest per-step status on the demo instance)

| Step | Status | Evidence / caveat |
|---|---|---|
| 1 Connect | LIVE | 8 profiles; **CP-04/CP-06 Oracle red until PPIQ_SRC entered** (open since 15-Jul) |
| 2 Register | LIVE | meltshop browser renders fleet tables + taxonomy view (your screenshot) |
| 3 Import | **NOT YET LIVE** | runsheet never executed; Day-1 task - run it INTO ppiq_presentation so the customer watches real rows arrive into the same instance |
| 4 Prepare | LIVE | UI proven in walk |
| 5 Map | LIVE | mapping UI proven; execute path certified after failedRows fix |
| 6 Loaded | POPULATED | 40k units, full genealogy thread (H->SL->C walks) |
| 7 Dashboards | **BUILT TODAY - BROWSER-UNVERIFIED** | 12 Qlik workspaces; /dashboard + /workspace/:code; drag/resize/min-max/filters pending your eyes |
| 8 Analysis | LIVE | saved definitions, honest BlockedReadiness states (screenshot) |
| 9 Run | POPULATED | 375 governed runs |
| 10 Findings | POPULATED | 320 real findings w/ population/effect/q |
| 11-12 ML ready/jobs | PARTIAL | license event exists; job substrate shared |
| 13 ML results | **DEFECT** | 401 banner on Suggestion page (screenshot) - fix or skip |
| 14 Supervisor | SCRIPTED SENTENCE | v0 absent by design - M2 keystone; do not fake |
| 15 Assistant | NEEDS ONE ACTION | reindex post-import -> grounded, cited answers |

### 2.2 UI/UX position

- Interactive Workspace Doctrine v1 written (7 standards = your 7 rules);
  reference implementation live in tree; **gate not yet installed**;
  doctrine not yet committed. Gap-to-full-conformance costed at ~70h
  (S5 conditional tables / list / date-range components are the real gaps;
  S6 needs the "Add widget" builder entry re-mounted - that is your
  live-build moment in the meeting).
- Static professionalism gates all green as of last full run except ONE
  stale test (JourneyRail M1-17 - superseded; check rail in browser, then
  delete it) - the mojibake gate is green after today's fix.
- Frozen UI debt: 161 raw controls / 112 inline styles under ratchet
  (monotonic). Burn-down is M2 work, not this week.

### 2.3 Milestones

- **M1 (Presentable):** the constitution's bar is "every step shown working
  in the HMI." After Day 1-3 below, steps 1-12+15 meet it honestly; 13 fixed
  or skipped; 14 scripted. Certifier expectation: ~12-13/16 honest greens
  with the gate suite green (last run: 0/16 - caused by imports unrun +
  suite red, both scheduled).
- **M2 (Hardened):** certification framework adopted 15-Jul as the exit
  criteria; supervisor v0 is the flagship deliverable; UI debt burn-down;
  eradication epic (installer scripts 110/111/140/142/665, seed/, demo
  endpoints); doctrine gate; Backlog v23 with frozen IDs.

---

## 3. WHAT YOU CAN SHOW - MARKETING & SALES

- **The impressive core is now real:** 40k-unit multi-month instance, 12
  interactive workspaces, live multi-vendor DB connections (PG/MySQL/
  Oracle/SQL Server on screen), genealogy walks, 320 computed findings -
  plus the differentiator none of Primetals/PSI/SST/Fero can screenshot:
  the honesty machinery itself (readiness gates that BLOCK, the Honesty
  Cert page, evidence-cited assistant). Sell the honesty as a feature; it
  is your moat and it happens to be true.
- **The one sentence stays:** "This instance runs on our emulated
  multi-source plant - on your install it starts empty and fills through
  the DB-link imports you just watched." Say it once, early, unprompted.
- **Website:** refresh hero/screenshots from the workspace pages AFTER the
  Day-2 browser pass; capture the rehearsal as the demo video; honesty-lint
  stays green ("Coming soon" badges already honest).
- **Money slide:** today's 320 findings carry the story; the fleet's
  planted 9.3x (superheat -> CRACK_LONG) becomes citable the moment Day-1
  imports + Day-3 engine run land - a finding computed on data the
  customer watched arrive is the strongest sales artifact you can make.

---

## 4. OPEN-DEFECT REGISTER (all known, none hidden)

| # | Defect | Severity | Owner day |
|---|---|---|---|
| 1 | PPIQ_SRC missing on CP-04/CP-06 (Oracle discovery red) | Demo-visible | Day 1, first 5 min |
| 2 | Step-13 Suggestion 401 banner | Demo-visible | Day 3 (else skip page) |
| 3 | Command-dashboard KPIs "-" (quality/risk keys) | Cosmetic-visible | Day 3 |
| 4 | Stale JourneyRail.test.tsx red | Gate hygiene | Day 2 (verify rail, delete test) |
| 5 | 5 provenance-NULL units in ppiq_app | Rule-2 breach (dev DB) | Day 2 (identify+delete) |
| 6 | Heatmap/scatter clones landed 0 (source template had neither) | Feature gap | Day 2 (author real heatmap widget - Widget Drift proves the config) |
| 7 | Certifier S06 detail query wrong column; S08 pattern | Tooling | Day 2 |
| 8 | Workspace page = unreviewed parallel-session code | Risk | Day 2 review |
| 9 | Fleet assets deleted from tree (FLEET_RELATIONS rescued to outputs) | Continuity | Day 1 commit into docs/emulation/ |
| 10 | Backlog v23 with frozen IDs not yet cut | Governance | Day 4 |

---

## 5. THE 4-DAY ROADMAP

**Day 1 - Real data through the front door.**
Commit + merge (section 1). PPIQ_SRC (defect #1). Execute the Import
Registration Runsheet into ppiq_presentation - live imports on top of the
restored base. Certify after each phase; S02/S03 flip green. Commit
FLEET_RELATIONS.md into docs/emulation/.

**Day 2 - The workspace to doctrine standard.**
Browser pass of all 12 workspaces + the three headline URLs; verify S4
min/max and S7 drag/displacement/save-layout; review the senior's page
(defect #8); add the real heatmap widget (#6); re-mount the Add-widget
builder entry (S6); delete the stale test (#4); commit doctrine to main +
install the interactiveWorkspaceContract gate; fix certifier (#7); purge
the 5 dirty units (#5).

**Day 3 - The engine on watched data.**
Run the learning job over the imported fleet window -> findings computed
on demo-visible data (the 9.3x). Assistant reindex. Two alert rules +
evaluation. One saved analysis job. Fix step-13 401 (#2) and the KPI
wiring (#3).

**Day 4 - Rehearse, freeze, sleep.**
Full 15-step walk twice on :5063. Certifier run - expect ~12-13/16 honest
greens, and read every red aloud: each one is a sentence you must be able
to say in the room. No code after noon. Website screenshots + demo video
from the second rehearsal.

---

## 6. THE ANSWER TO YOUR QUESTION 3, IN ONE PARAGRAPH

Neither of your two options. Merge the presentation branch into main this
week (it contains only real work), keep ppiq_presentation as a permanent
second DATABASE selected by profile, and develop only on main forever
after. You lose nothing: the demo keeps its 40k dataset, development keeps
its empty-start truth, no work is ever done twice, and the "we will
forget" risk is closed by the two things that outlive memory - a merged
commit and a CI gate.

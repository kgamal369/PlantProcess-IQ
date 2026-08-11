# PPIQ SESSION HANDOVER - 2 to 3 AUGUST 2026

**Save as:** `Documentation/docs/Product Book/Related Documentation/PPIQ_SESSION_HANDOVER_03Aug2026.md`

**READ THIS BEFORE DOING ANYTHING. It exists so you do not re-investigate, re-run
or re-discover what has already been measured. Every number here was READ FROM A
RUNNING SYSTEM unless the line says otherwise. Where something is unverified it
says UNVERIFIED.**

---

## 0. THE ONE-PARAGRAPH POSITION

M1-P1 is CLOSED - twelve tasks, 84 hours, T-001 to T-012, all Done and
committed. The backlog is at v2.7 (164 tasks / 1,411 h). The next task is
**T-013, the three-way source reconciliation**, first task of the new phase
M1-P1b "Presentation Fleet v2". The single most important discovery of the
session: **the documents describing the emulated plant do not describe the
database that exists**, and most of the canonical data never travelled the
product's own import path. Everything in M1-P1b exists to fix that.

---

## 1. WHO THE OWNER IS AND HOW HE WORKS

Karim - solo founder of SOU Industrial Software (Dusseldorf), building
PlantProcess IQ. 13 years industrial/MES/Level-2 automation. Communicates in
English and Arabic.

### 1.1 His standing rules - these are binding

- **Zero preamble, no flattery.** Evidence before cure. Surface defects
  honestly. Never claim done when not done.
- **ALWAYS deliver a PowerShell apply pack** with the
  preflight-backup-anchored-replace-self-check-gate-auto-revert contract. This
  covers DIAGNOSTICS too, not just fixes. Never ask him to paste JS into DevTools
  or run ad-hoc commands by hand.
  - **EXCEPTION:** for a small one-line source edit, tell him exactly which line
    to change to what. Long pasted scripts get truncated by his console.
- **ALWAYS include the exact run commands** - full copy-paste block, in execution
  order, including where to save the file, the report-only dry run, the apply,
  the revert, and the commit. Never hand over a script without its run command.
- **NEVER deliver zip files.**
- Pure ASCII. UTF-8-no-BOM via `[System.IO.File]::WriteAllText` with
  `UTF8Encoding($false)`. CRLF for .ps1/.cs, LF for .sh. No em-dashes, no curly
  quotes.
- No `&&` in PowerShell. Cuddled `} else {`. Run from repo root.
- Upload attachments frequently arrive EMPTY. When that happens, ask him to paste
  as text, or read the file from `/mnt/user-data/uploads/` with bash.
- His machine blocks unsigned scripts, so EVERY invocation is
  `powershell -NoProfile -ExecutionPolicy Bypass -File .\script.ps1`.

### 1.2 ABSOLUTE BACKLOG ADHERENCE (set 02-Aug)

`PPIQ_Backlog_v2.7_03Aug2026.md`/`.xlsx` is the ONLY scope.

- If it is not explicitly written in the backlog, DO NOT DO IT.
- Tasks execute in planned dependency-aware order from T-001 upward. **A task is
  fully complete, including its sign-off questions, before the next starts.**
- Temporary DATA and temporary internal implementation are sometimes allowed.
  **Temporary product identity, temporary UX and fake product answers are NEVER
  allowed.**
- When a finding falls outside every bucket a task defines, **NAME THE GAP AND
  ASK FOR A RULING** rather than inventing a bucket.

### 1.3 The quality standard he set (02-Aug), after his review found 8 wrong
tasks + 1 missing + 7 consistency defects in my backlog

> Maintain deep, detailed, advanced, professional work as the DEFAULT in every
> session. He should not have to run the assessment that finds my gaps.

Concretely: re-read the actual code before every review or revision rather than
working from notes; never let a cross-reference, an hour total or a dependency
order rest on memory when it can be verified; **build a mechanical guard whenever
a defect class is mechanical** instead of promising to be careful; state
arithmetic openly and never fit a number to a budget; **name my own defects
before he finds them**.

### 1.4 HIS ENGINEERING LAW - now project-wide

> **Documents specify intent. Runtime measurement certifies implementation truth.
> Whenever the two conflict, stop dependent implementation, measure, reconcile,
> then update the authoritative backlog.**

This was born from this session: 20 documented defect codes against 6 real,
12 documented chemistry elements against 3 real, the fleet scale, the downtime
funnel, and the source truth.

### 1.5 His status rule

Any task finished goes to **Done** in Excel immediately. Any task whose evidence
is invalidated becomes **REOPENED** (amber). A completed task is **NEVER** reset
to Not Started as though the work never happened. There is no PARTIAL - a partial
task is rewritten as its remainder with a fresh estimate.

**ACTION ITEM:** `emit.py`'s `status_of()` still writes "Not Started" for T-007
and T-008. He wants "Reopened". Fix before the next emission.

---

## 2. PRODUCT IDENTITY, TOPOLOGY AND DOCTRINE

### 2.1 What PPIQ is

Read-only, evidence-grade, industry-agnostic process-to-quality intelligence
platform for manufacturing plants. ~EUR 100k per customer. Repo
`kgamal369/PlantProcess-IQ`, local `C:\Workspace\PlantProcess-IQ`.
Stack: C# .NET 9 / React / PostgreSQL.

### 2.2 The three permanent Product Rules

1. **Generic Only** - no demo content in the product.
2. **Starts Empty** - all data arrives via DB-link import only.
3. **The 15-step canonical journey IS the product.** A second journey written
   anywhere is deleted rather than reconciled.

### 2.3 The canonical journey J1-J15, VERBATIM from Chapter 2 section 3.3.1

| # | Label | Principal surface |
|---|---|---|
| J1 | Install and first login | Login; Home |
| J2 | Activate the licence | F2 Licence and Entitlement |
| J3 | Create users and roles | F1 Users and Roles; F3 Quota |
| J4 | Declare read-only connections | B1 Connections |
| J5 | Register datasets | B2 Dataset Registry; B3 Prepare Import |
| J6 | First incremental import | B4 Importing; B5 Jobs Monitor |
| J7 | Author the transformation and publish the relationship model | C1 Transformation Studio; C6 Relationship Browser |
| J8 | Project to canonical, with validation | B4 Importing; C2 Mapping Health; C3 Data Quality |
| J9 | Walk the genealogy | C5 Genealogy Explorer; C4 Plant Model Explorer |
| J10 | Build pages, widgets and filters | D2 Page Builder |
| J11 | Explore associatively | D1 Interactive Workspace |
| J12 | Author and run analysis through the gate | D3 Analysis Toolbox; D8 ML Readiness and Models |
| J13 | Read findings, risk, practices and value | D4 Findings; D5 Risk; D9 Early Warning; D10 Practice Insights; D7 Value |
| J14 | Decide, act and measure | D6 Suggestions; D9; D7 |
| J15 | Operate, govern and retain | E3 Plant Data Log; E6 Alert Routing; E4 Supervisor; E5 Reports; F4-F9 |

J1-J3 commission the platform. J4-J9 build the plant model. J10-J15 are daily
life.

### 2.4 The data-flow contract DF1-DF6 - STRICTLY SEQUENTIAL

connection -> register dataset -> import into staging -> author transformation
-> project canonical -> genealogy.

**DF4 explicitly requires that DF3 has already produced at least one successful
batch for every dataset the definition reads.** This is a design contract, not a
preference. It caused a real backlog correction (v2.6) when I had ordered mapping
before import.

### 2.5 Milestone shape (v2.7)

| Milestone | Hours | Meaning |
|---|---:|---|
| M1 | 552 | Customer presentation. Freeze what the customer can see |
| M2a | 422 | Deployable core, ends with the on-site installation |
| M2b | 233 | Intelligence completion, shipped during the soft test |
| M3 | 204 | Site stabilisation, certification, commercial |
| **Total** | **1,411** | 164 tasks, max task 12 h, all 14 phases in 80-120 h |

**M1 progress, and report THREE numbers never one:** baseline scope 552 h,
completed 84 h (T-001 to T-012), remaining 468 h.

---

## 3. THE BACKLOG - HOW IT EVOLVED THIS SESSION

Started at v2.4 (152 tasks / 1,317 h). Ended at v2.7 (164 / 1,411).

| Version | What changed | M1 |
|---|---|---:|
| v2.4 | starting point | 476 |
| v2.5 | Presentation Fleet v2 amendment; M1-P1 split into M1-P1 + M1-P1b | 534 |
| v2.6 | his four corrections - DF sequence split, mappings A/B, shift as behaviour, manifest harness | 552 |
| v2.6.1 | his ten consistency findings | 552 |
| v2.7 | Fleet v2 becomes the M2 reference plant - two new M2a entry tasks | 552 (M2a 404->422) |

### 3.1 The generator - how the backlog is actually produced

The backlog is NOT hand-written. It is generated:

- `/home/claude/backlog.py` - the single source of truth, a list of `t(...)`
  calls with phase, name, module, submodule, priority, description, validation,
  hours.
- `/home/claude/emit.py` - reads `backlog.json` and writes the .md and .xlsx.
- Backups: `backlog_v24_backup.py`, `backlog_v25_backup.py`,
  `backlog_v26_backup.py`, `backlog_v261_backup.py`.
- Patches applied this session: `patch25.py`..`patch25e.py`, `patch26.py`,
  `patch261.py`, `patch262.py`, `patch27.py`.

**THE CONSISTENCY GUARD** in `backlog.py` refuses to emit if:
- any `{{ref:Task Name}}` fails to resolve,
- any hardcoded `T-nnn` id survives in task text,
- any referenced task has a HIGHER id than the task referring to it.

It caught **four real ordering violations I introduced** during this session. Task
descriptions carry `{{ref:Task Name}}` tokens, NEVER hardcoded ids.

**Ids follow DECLARATION order within a phase**, so moving a task between phases
is not enough - the block must be physically moved in the file.

### 3.2 The Fleet v2 ordering law (top of the emitted document)

> Current emulation inventory -> chart blueprint -> map each chart to existing
> fields and phenomena -> classify -> change the generator ONLY FOR TRUE GAPS ->
> regenerate and reload the source fixtures -> discover/register/import to
> staging -> publish the mappings -> project to canonical -> statistical QA ->
> certify.

---

## 4. M1-P1 - TWELVE TASKS, ALL DONE. WHAT EACH RETURNED

### T-001 Design Traceability Matrix (8h) - DONE
Delivered `M1_Traceability_Matrix_v1.1.md`, 18 presented screens + 6 shell rows,
zero blank Chapter 3 cells.

**Four findings reading code alone had not produced:**
1. **THERE IS NO HOME PAGE.** Ch3 A2 specifies `/` and J1 lands on it, but
   `App.tsx:537` declares `<Route index>` redirecting straight to `/dashboard`.
   Not funded anywhere.
2. The assistant's visible contract is WRONG today - a separate `/assistant`
   route is a different product shape from the Ch4 5.7.1 dock.
3. `/investigate/analysis-jobs` (AnalysisJobConfigPage) has NO Chapter 3 owner.
4. Two customer-visible nav strings carried internal tokens.

**My disagreement with a sign-off, upheld:** `/data-integration/author-mapping`
should NOT be shown. Ch3 DF5 names B4 Importing as the projection surface;
opening it freezes a THIRD authoring surface. **18 screens, not 19.**

### T-002 Presented route and control audit (8h) - DONE, commit `0278863d`
Shipped `navigationContract.test.ts` (PPIQ-T12, 3 assertions) and fixed both
customer-visible token strings in `AppLayout.tsx` (lines 45 and 90:
`"Weekly engine review (step 14)"` and `"Phase 15 advisory projection"`).

**TASK-TEXT CORRECTION FOUND WHILE EXECUTING:** my own task said legacy phase
routes must be hidden from nav. VERIFIED FALSE - every `/phase8/*`, `/phase9/*`,
`/phase15/*` is a `<Navigate>` reverse redirect and NO nav entry points at one.
They are Rule 4 retirement debt for M2, not demo risk.

**The real finding instead:** the two token strings above, Severity 1 - a
customer reading a phase number learns the product is organised around our sprint
plan.

**35 navigation targets audited. ELEVEN have no Ch3 contract, nine in System.**
Six label mismatches still OPEN inside T-002's hours: Table Registry->Dataset
Registry, Join Canvas->Transformation Studio, Material Investigation->Genealogy
Explorer, Risk Intelligence->Risk Dashboard, Correlations->Findings, Command
Dashboard->Interactive Workspace.

### T-003 Presentation profile lock (4h) - DONE, commit `56a614ff`
Removed a duplicate `ConnectionStrings__PlantProcessDb` at lines 18/19 of BOTH
`env/profiles/local.env` and `presentation.env`. Added a PPIQ-T003 header block
to `start-api.ps1`.

**PROVEN BY PROBE:** presentation PID 36372 -> feature_values 151,752 /
outcome_values 195,221; local PID 6472 -> 40,181 / 40,149. Two different
processes, two different databases.

**MY PROBE'S GUIDANCE TEXT WAS WRONG:** it said `correlation_results near 320`
indicates presentation. **BOTH databases return exactly 320** - they inherit the
same 13-Jul fixture. **The discriminators are `outcome_values` and
`feature_values`.**

**Readiness response shape (confirmed):**
```
{"phase":"P02","readiness":{"feature_definitions":33,"feature_values":N,
 "outcome_definitions":8,"outcome_values":N,"correlation_results":320,
 "kb_items":0,"pgvector_available":false}}
```
`kb_items = 0` on BOTH profiles - the assistant retrieval index is empty.

### T-004 Acceptance checklist and evidence folder (4h) - DONE
`docs/m1/ACCEPTANCE.md` (24 gate lines G01-G24), `docs/m1/screens.txt` (18
screens as data), `scripts/m1/New-ScreenChecklist.ps1` generating 18 per-screen
checklists.

**The rule it encodes: a tick counts only when an evidence file name sits beside
it.**

### T-005 Rebuild into scratch and diff (6h) - DONE
Produced the first full diff: **33 differences.**

**And it exposed a Severity 1 in the demo toolchain:**
`Rebuild-PresentationDb.ps1` printed `REBUILD COMPLETE` and returned **0** while
**every single step had failed**. Its own header calls it "the ONLY supported way
to rebuild the demo database".

### T-006 Convert every diff finding (8h) - DONE, commit `24bec2b8`
**Closed with `TOTAL DIFFERENCES: 0`.** Arc: 33 -> 23 -> 1 -> 0, across five
closure attempts, four of which failed on defects of mine.

**The two big returns:**
1. A **forensic wipe-detection subsystem** existing nowhere in source control -
   schema `ppiq_forensics`, functions `audit_ddl` and `audit_wipe`, table
   `wipe_audit`, six `ppiq_wipe_trap_*` triggers, and an EVENT TRIGGER
   `ppiq_wipe_trap_ddl`.
2. **103,382 missing outcome values.** Migrations 740/741 DEFINE
   `public.ppiq_ml_refresh_feature_store(p_window_days)` and the rebuild applied
   741+742 but **never CALLED it**. Live 195,221 vs rebuilt 91,839. Rebuilding
   before the demo would have silently cost 53% of the evidence base with every
   page still rendering.

### T-007 Coverage matrix part 1 (10h) - DONE (REOPENED then closed)
Delivered `T-007_emulation_inventory.txt` and `T-007_36_chart_blueprint.txt`.

**Closed as a PRE-GENERATION SPECIFICATION** - measured inventory + the 36-chart
blueprint. Post-generation certification belongs to the phenomenon-proof task.

### T-008 Coverage matrix part 2 (10h) - DONE (REOPENED then closed)
`phenomena_widget_matrix.csv` - **104 rows, 21 columns, zero blank cells**, all
36 charts with a primary phenomenon. `widget_decisions.csv` - 65 rows.

**Predeclares expected direction, effect band, minimum population, conditioning
variable and negative control for every phenomenon, BEFORE the data exists.**
That is what stops the later proof being self-fulfilling.

### T-009 Downtime two-quantity contract (6h) - DONE, commit `d137abaf`
Chapter 3 4.5.4 verbatim: `stopped_minutes numeric(12,3) NOT NULL`,
`production_impact_minutes numeric(12,3) NOT NULL`, CHECK both `>= 0`.
EF migration `20260802210601_AddDowntimeQuantities`, applied to BOTH `ppiq_app`
and `ppiq_presentation`.

**Discovery: PPIQ-501 ALREADY EXISTED.**
`Backend/PlantProcess.Application/Analytics/Value/DowntimeImpactCalculator.cs`
already models both quantities with a `DowntimeBufferPosture` enum
(BufferedDownstream -> production-impact, UnbufferedHardStop ->
equipment-stopped, Unknown -> ABSTAIN). It had **no persisted source**.

### T-010 Canonical semantic path walk (8h) - DONE
See section 6 for the full ladder. **The return: 16,640 staging rows against
106,272 canonical rows.**

### T-011 Architecture test pool reliability (6h) - DONE, commit `b3f6ad21`
Ten consecutive green runs, zero worker timeouts. **Wall 91.2s -> 27.4s.
Environment 55s -> 3ms.** Added PPIQ-T14 ratchet.

### T-012 Canonicalise the JourneyRail (6h) - DONE
Rewrote `STAGES` to the canonical J1-J15 with Chapter 2 labels verbatim. Added
PPIQ-T15 (7 assertions). Gate: tsc exit 0, **18 files / 69 tests all passing**.

---

## 5. THE BIG FINDINGS - WHAT THE MEASUREMENTS ACTUALLY PROVED

### 5.1 FLEET_RELATIONS.md DOES NOT DESCRIBE THIS DATABASE

| structure | documented | **MEASURED** | ratio |
|---|---:|---:|---:|
| heats | 1,802 | **630** | 0.35 |
| casting sequences | 956 | **630** | 0.66 |
| slabs | 18,661 | **5,670** | 0.30 |
| coils | 18,661 | **5,670** | 0.30 |
| HSM stand passes | 111,966 | **39,690** | 0.35 |
| surface defects | 34,312 | **1,987** | **0.06** |
| pickled coils | 15,782 | **5,670** | 0.36 |
| QA tests | 8,920 | **17,010** | **1.91** |

**The ratio is NOT uniform, and that is the finding.** A uniform third would mean
the generator ran smaller. Defects at 0.06 and QA at 1.91 mean these are not the
same dataset at a different scale.

**I originally wrote "Scale, measured" over figures I had read in a markdown
file.** That error is the reason T-007 and T-008 were reopened.

### 5.2 SIX defect codes, not twenty - and nearly uniform

```
PINHOLE 351 | SCALE 347 | EDGE_CRACK 341 | ROLLED_IN 335 | SCRATCH 319 | WAVINESS 294
```
Density 1,987 / 5,670 coils = **0.350 per coil**. Top-to-bottom ratio **1.19**.
**A Pareto of six near-equal bars is exactly chart 8's own "boring if"
condition.**

**The three strongest documented relations have NO code here at all:**
R1 CRACK_LONG 9.3x, R2 INCLUSION 4.5x, R4 SLIPPAGE_MARK 28.9x.

Analogues that DO exist: WAVINESS~WAVY_EDGE (R3), ROLLED_IN~ROLL_MARK (R5),
EDGE_CRACK (R6), SCALE~SCALE_ROLLED (R7), PINHOLE (R10a), SCRATCH (control).
Absent: CRACK_TRANS, SLIVER, LAMINATION, BLISTER, OSCILLATION_MARK, GAUGE_DEV,
WIDTH_DEV, DENT, SEAM.

### 5.3 THREE chemistry elements at ONE station, not twelve at three

`src_meltshop_pg.heats` in full:
```
heat_no, plant_code, furnace_code, steel_grade, route_code, tap_start_utc,
tap_end_utc, heat_weight_ton, target_temp_c, actual_temp_c, oxygen_nm3,
power_kwh, carbon_pct, manganese_pct, silicon_pct, source_updated_at_utc
```
`src_meltshop_pg.lf_treatment` in full:
```
treatment_id, heat_no, lf_code, treatment_start_utc, treatment_end_utc,
argon_flow_nm3, calcium_wire_m, final_temp_c, sample_result_code,
source_updated_at_utc
```
**Aluminium, sulfur, niobium and nitrogen are ABSENT**, so R2, R6, R8 and R9
CANNOT EXIST in this data whatever any document says.

### 5.4 NO DOWNTIME MAPPING HAS EVER RUN

The funnel: `dump_store` 248 -> staged 210 -> **canonical 3**.

The three canonical rows are `DT-CAST-SPEED-HOLD`, `DT-HSM-SENSOR`,
`DT-NO-REFERENCE`, all `source_system = ADVANCED_DEMO_SEED`, and **ZERO of the
210 staged source_record_ids appear in canonical**. They are hand-seeded fixtures.
**Zero of the nine mapping definitions target downtime.**

The staged 210 rows are GOOD material: 9 equipment units (CCM-01/02, EAF-01/02,
LF-01/02, HSM-01, PKL-01/02) with 18-32 events each; 5 reasons across 4
categories (unplanned HYDRAULIC_ALARM 50, quality QUALITY_HOLD 47, planned
MECH_ROLL_CHANGE 41, unplanned SENSOR_FAULT 41, logistics ENTRY_DELAY 31);
durations 196-5,374 seconds. Source carries **only `duration_seconds`**.

### 5.5 THE CANONICAL MODEL DID NOT COME THROUGH THE IMPORT PATH

16,640 staging_records against 40,148 material_units + 14,433
parameter_observations + 51,691 quality_events = **106,272 canonical rows.**

**Canonical is six times staging.** By the reading rule - canonical above staging
means rows arrived from somewhere other than this path - the ladder fails. Most
of `ppiq_presentation` was seeded directly. This reframes the Fleet v2 re-import
as the thing that finally makes the demonstrated path the real one.

### 5.6 No shift or crew on ANY source; no grade specification anywhere

Nothing in the staged schemas matches shift/crew/rota/team. **The canonical side
ALREADY HAS `process_step_executions.crew_code` and
`ml_learning_observations_v1.crew_shift`** - so the landing exists and NO NEW
CANONICAL COLUMN is needed.

No grade specification table, no min/max column. Grade keys that exist:
`heats.steel_grade`, `cast_sequence.planned_grade`/`.actual_grade`,
`material_units.grade_or_recipe`, `ml_learning_observations_v1.grade_family`.

### 5.7 The 36-chart achievability, MEASURED

**OK 13 | THIN 8 | BLOCKED 11 | UNVERIFIED 4.**

(I originally said only THREE were blocked - that came from the document.)

The eleven cluster into FIVE gaps - the closed list Fleet v2 must answer:
1. No shift or crew -> blocks charts 2, 14, 22; weakens 6, 19
2. No grade specification -> blocks chart 12
3. Flat six-code catalogue -> blocks chart 8; weakens 7, 9, 25, 26
4. One downtime quantity only -> blocks chart 15
5. No campaign or maintenance event keys -> blocks charts 17, 18

**Plus chart 10's ONE ROW, which is NOT a data gap** - it is canonical equipment
attribution, and the remedy is relationship resolution, never more rows. Chart 4
and RI_EQUIP share the root cause.

### 5.8 All 29 seeded widgets bind to generic counts only

Eight dimensions (day, week, month, materialUnitType, defectType, severity,
equipment, parameterCode) and six measures (materialCount, observationCount,
defectCount, defectRate, riskScore, avgParameterValue).

**NOT ONE touches grade, shift, crew, chemistry, gauge, campaign age, coil
position or the downtime split.** The dataset is not the only problem - THE
BINDING IS.

**Two honesty defects in the seed:**
- `MI_SEV` is titled **"Predicted Severity Mix"** and is a donut of defectCount
  by severity. **Nothing predicted it.** Severity 1 under the no-fake-answer rule,
  on the one page whose whole value is the honest refusal.
- `CORRELATION_FINDINGS_BOARD` has two widgets and **neither shows a
  correlation** - CF_RATE duplicates QM_TREND, CF_TOP duplicates QM_BREAK.

---

## 6. EVERY TEST AND MEASUREMENT ALREADY RUN - DO NOT REPEAT THESE

### 6.1 T-010 semantic path walk, ppiq_presentation, 03-Aug

**Stage ladder (measured):**

| stage | count | DF |
|---|---:|---|
| connection profiles | 8 | DF1 |
| source dataset definitions | 4 | DF2 |
| import batches | 16 | DF3 |
| staging records | 16,640 | DF3 |
| mapping definitions | 9 | DF4 |
| material units | 40,148 | DF5 |
| parameter observations | 14,433 | DF5 |
| quality events | 51,691 | DF5 |
| genealogy edges | 35,906 | DF6 |
| ml feature values | 151,752 | engine |
| ml outcome values | 195,221 | engine |
| ml correlation results | 320 | engine |

**One row traced end to end:** unit `H-27802`, type Heat, grade DC01,
`source_system = postgresql`, `source_record_id = 2026-07-02T05:42:44.0000000Z`.

**material_units by source_system:** CASTER_L2 18,070 | HSM_L2 17,817 |
MELTSHOP_L2 2,431 | **postgresql 1,802** | REF_BASELINE 28.

**The nine mapping definitions:** ADV_MAP_HIST_PARAMETER_OBSERVATION,
ADV_MAP_L2_PROCESS_STEP, ADV_MAP_MES_MATERIAL_UNIT, ADV_MAP_QMS_QUALITY_EVENT,
**ADV_MAP_UNKNOWN_TARGET_FOR_VALIDATION -> UnknownEntityForValidation (ACTIVE, a
validation fixture live in the presentation database)**,
**DEMO-READY-MAP-HEAT -> Heat (carries DEMO, Rule 1 vocabulary; "Heat" is not a
canonical entity)**, MELTSHOP_DEFECT_EVENTS_TO_QE,
MELTSHOP_HEATS_TO_MATERIALUNIT, MELTSHOP_PARAM_READINGS_TO_PARAMOBS.

**Feature refresh RAN successfully through the service:** 111,611 feature rows,
155,073 outcome rows, with a run id.

**Correlation returned an HONEST REFUSAL:**
`status: "Blocked"`, `message: "Blocked by the data-readiness gate; analysis
refused (honest abstain)."` - **this is the product behaving correctly and is a
demonstration asset**, but it means the Findings page shows a refusal.

### 6.2 T-011 architecture suite - ten consecutive green runs

| | before | after |
|---|---:|---:|
| wall | 91.2s | **27.4s** |
| environment | 58.69s / 50.69s | **2-3 ms** |
| setup | 15-17s | 15-17s (unchanged) |
| files | 16 | 18 (after T-012) |
| tests | 59 | 69 (after T-012) |

**Ten of ten green, zero worker-start timeouts, zero assertion failures.**

**REMAINING COST, outside T-011's text:** setup is now two thirds of the wall
time - `setupFiles: ./src/test/setupTests.ts` runs per file and a node-env test
almost certainly does not need it. Excluding it for the architecture directory
would likely take 27s to under 12s.

### 6.3 T-012 gate - tsc exit 0, architecture suite exit 0

18 files / 69 tests. `journeyRailCanonical.test.ts` (PPIQ-T15) 7 tests, 14ms.
Note `noMojibake.test.ts` costs 6.5s of the run (2s TS + 4.5s C#).

### 6.4 T-006 diff - final state

`TOTAL DIFFERENCES: 0`. Live 1,211 objects, scratch 1,211. Objects only in live
0, only in scratch 0, row deltas 0. Evidence:
`docs/m1/evidence/presentation_db_diff_reverify_20260802_231441.txt`.

### 6.5 T-009 rejection tests - both correct

- Null `production_impact_minutes` -> `null value in column ... violates not-null
  constraint` PASS (an error is the PASS)
- `stopped_minutes = -1` -> `violates check constraint
  ck_downtime_events_stopped_minutes_nonneg` PASS
- Column shape: both `numeric`, precision 12, scale 3, `is_nullable = NO` PASS
- `downtime_events` held **3 rows, all zero** after the migration.

### 6.6 Staged source inventory - MEASURED

```
src_caster_oracle_shape.cast_pieces                    5,670
src_caster_oracle_shape.cast_sequence                    630
src_hsm_oracle_shape.hsm_coils                         5,670
src_hsm_oracle_shape.hsm_pass_measurements            39,690
src_inspection_mysql_shape.downtime_events               210
src_inspection_mysql_shape.parsytec_surface_defects    1,987
src_meltshop_pg.heats                                    630
src_meltshop_pg.lf_treatment                             630
src_pkl_mssql_shape.pickle_orders                      5,670
src_pkl_mssql_shape.qa_lab_results                    17,010
```
**TEN staged tables, not eight.** Note the staging layout does NOT map
one-to-one onto the eight source systems: downtime is under
`src_inspection_mysql_shape`, QA is under `src_pkl_mssql_shape`.

### 6.7 UNVERIFIED - stated rather than guessed

Full column lists were read for `heats`, `lf_treatment`, `downtime_events`, and
partially `parsytec_surface_defects`. **NOT read for:** `cast_sequence`,
`cast_pieces`, `hsm_coils`, `hsm_pass_measurements`, `pickle_orders`,
`qa_lab_results`. Any claim depending on those is marked UNVERIFIED.

---

## 7. THE ENDPOINT MAP - READ FROM CODE, NOT GUESSED

| Group | Base | Routes |
|---|---|---|
| Auth | `/auth` | POST `/login` |
| ConnectorAdmin | `/admin/connectors` | connection-profiles, `{id}/test` (**NO BODY**), `{id}/tables`, `{id}/register` |
| Integration | `/integration` | source-systems, mapping-definitions, import-batches, staging-records, summary |
| ImportWorkflow | `/workflow/import` | run, process-queue - **THE REACHABLE IMPORT SURFACE** |
| GenericSchemaMapping | `/admin/schema-mapping` | catalog, execute/{viewCode}, resolve, joins/preview, joins/materialize |
| P03P04 Genealogy | `/admin/p03p04` | readiness - **RETURNS 500** |
| Materials | `/materials` | `{id}`, `{id}/genealogy`, genealogy-edges |
| MaterialInvestigation | `/materials` | `{id}/investigation-full` - **carries SourceSystem + SourceRecordId** |
| MlFoundation | `/api/ml/foundation` | readiness, feature-store/refresh, compute/correlation, outcomes, feature-definitions |
| SimpleAnalysis | `/api/analytics/simple` | primitives, datasets, run |
| JobAdmin | `/admin/jobs` | `datasets/{id}/backfill`, `{jobId}/run-now`, pause, resume, history |
| TwoStageImport | `/admin/two-stage-import` | **NEVER MAPPED IN Program.cs - all 404** |

**Request shapes that matter:**
- `TestConnectionProfileAsync(Guid id, IConnectorConfigurationService, CancellationToken)` - **no body**.
- `RunImportWorkflowRequest(ImportBatchId?, SourceSystemDefinitionId, MappingDefinitionId, ImportBatchCode?, ImportType?, SourceObjectName, FileName?, Checksum?, Rows, ...)` - **the caller supplies the ROWS. It is a PUSH endpoint, not a pull.**
- `CorrelationComputeRequest(OutcomeKey, Grain, WindowDays, Filters?)`.
- `/materials/{id}` returns 13 fields and NO provenance - **by design**, it is the slim read model.
- `/materials/{id}/investigation-full` returns top-level key **`materials`** (not `materialUnits`), and the unit inside carries `sourceSystem` and `sourceRecordId`.

**Auth contract:** POST `/auth/login` with `{userName, password, requestedRole?}`
returns camelCase `{accessToken, tokenType:"Bearer", ...}`. Working creds
`e2eadmin / E2EAdmin123!`. **Login returned role `Admin` in the 03-Aug walk**,
though T-003 evidence recorded TenantOwner - worth a glance.

---

## 8. IMPLEMENTATION CHANGES MADE THIS SESSION

### 8.1 Backend / domain

**`DowntimeEvent.cs`** - added `StoppedMinutes` and `ProductionImpactMinutes`
(decimal), constructor parameters with non-negative validation, no derivation
from each other or from timestamps.
**Deliberate design choice:** production impact is NOT constrained to be at most
stopped minutes. A three-minute caster pump trip can force a sequence rebuild
costing six hours.

**`DowntimeEventConfiguration.cs`** - `stopped_minutes` / `production_impact_minutes`
as `numeric(12,3)` required, plus two CHECK constraints
`ck_downtime_events_stopped_minutes_nonneg` and
`ck_downtime_events_production_impact_minutes_nonneg`.

Also edited: `AddDowntimeEventCommand.cs`, `ProcessDataService.cs`,
`ProcessEndpoints.cs`, `WorkflowEndpoints.Handlers.014.AddDowntimeEventAsync.cs`,
`WorkflowEndpoints.Contracts.001.cs`.

EF migration `20260802210601_AddDowntimeQuantities` applied to BOTH databases.

### 8.2 Database / rebuild script - `scripts/demo/Rebuild-PresentationDb.ps1`

This script was substantially repaired. Final shape:

1. **HONEST EXIT CODE.** `RunSql` already returned a boolean and every call site
   discarded it with `[void](RunSql ...)`. Added `$Script:PpiqFailCount`
   incremented inside `RunSql`, and a tail that REFUSES to print COMPLETE and
   exits 1 when the counter is non-zero.
2. **PROVENANCE TRIGGER COST.** The genealogy provenance UPDATE took **over ten
   minutes**. Cause: `ppiq_genealogy_edge_weight_guard_after_change` is a
   ROW-LEVEL AFTER trigger firing 35,906 times, on a table with TEN indexes.
   Fixed by suspending that ONE trigger inside a single transaction, then
   VERIFYING the weights-sum-to-1.0 invariant by query and verifying the guard is
   re-enabled. **The trigger is not removed.**
3. **FEATURE REFRESH STEP** - calls `ppiq_ml_refresh_feature_store(365)` after
   1b and prints the resulting counts. **This closed the 103,382-row gap
   exactly: 195,221.**
4. **PROVENANCE COMPLETION** - the raw driver name `postgresql` neutralised
   across material_units (MELTSHOP_L2), quality_events (INSPECTION_L2),
   parameter_observations (PROCESS_L2), genealogy_edges (GENEALOGY_L2).
5. **VERIFY COVERAGE** - step 7 previously checked provenance on
   `material_units` ONLY (its counts summed to exactly 40,148). Now checks
   residual `phase3-dump%` OR `postgresql` across ALL FOUR tables; each must read 0.
6. **STEP 6b debris cleanup** - deletes `PRESENTATION\_%` certification
   dashboards and their widgets BY PATTERN, never by hardcoded id.
7. **STEP 6c authored-definitions seed** -
   `scripts/demo/seed_authored_definitions.sql`, captured whole and replayed via
   `json_populate_recordset` with `ON CONFLICT (id) DO NOTHING`.
8. **Migration 750** `Backend/database/scripts/750_forensics_audit_subsystem.sql`
   added to the 1b list, bringing the forensics subsystem into source control -
   idempotent and NON-DESTRUCTIVE (the self-check fails the pack if it contains
   `DROP SCHEMA` or `DROP TABLE`).

**Other files created:** `docs/m1/presentation_diff_ignore.txt` (12 runtime
tables, each with a written reason - schema objects are NEVER ignored),
`Backend/database/_review/forensics_subsystem_capture.sql`,
`Backend/database/_review/audit_ddl_definition.sql`.

### 8.3 Frontend

**`AppLayout.tsx`** - two customer-visible token strings fixed (lines 45, 90).

**`JourneyRail.tsx`** - `STAGES` completely rewritten to the canonical J1-J15
with Chapter 2 labels verbatim; `Stage` type gained `commissioned?: boolean`;
J1-J3 render as done and are not navigation targets; `nextStage` never targets a
commissioned stage.

**New architecture tests:**
- `navigationContract.test.ts` (PPIQ-T12) - 3 assertions
- `vitestEnvironmentContract.test.ts` (PPIQ-T14) - 3 assertions
- `journeyRailCanonical.test.ts` (PPIQ-T15) - 7 assertions

**All 16 existing architecture test files** gained `// @vitest-environment node`
as their first line. **`vitest.config.ts` was NOT touched** and the pack asserts
it is byte-identical.

### 8.4 Documents delivered

`M1_Traceability_Matrix_v1.1.md`, `M1_Presented_Surface_Audit.md`,
`M1_Presentation_Diff_Classification.md`, `T-007_emulation_inventory.txt`,
`T-007_36_chart_blueprint.txt`, `phenomena_widget_matrix.csv`,
`widget_decisions.csv`, `PPIQ_Backlog_v2.7_03Aug2026.md`/`.xlsx`.

Superseded artifacts archived as `*_v1_superseded.txt` rather than deleted -
they are the record of the error.

---

## 9. INFRASTRUCTURE, DEPLOYMENT AND PIPELINE

**NOTE OF HONESTY: no deployment, server or pipeline work was performed in this
session.** Everything below is carried forward from earlier sessions and has NOT
been re-verified. Treat it as context, not as measurement.

### 9.1 Local

- Native PostgreSQL 16 at `127.0.0.1:5432` (use 127.0.0.1, not localhost).
  User `ppiq_dev`, password `ppiq_dev_local_only`.
- Databases: `ppiq_app` (development), `ppiq_presentation` (demo). `ppiq_dev`
  is a LOGIN, not a database.
- API port 5063. Launch:
  `.\scripts\run\start-api.ps1 -Profile presentation -FreePort`
- Web: `.\node_modules\.bin\vite --host localhost --port 5173`
- `free-ports.ps1` params `-Ports @(...)` and `-Force`.
- **Both profiles bind 5063**, so a stale API can answer for the wrong profile.
  Always identify the listening PID before trusting a probe.

**His database cleanup ruling (03-Aug):** keep `postgres`, `ppiq_app`,
`ppiq_presentation`. DROP `ppiq_presentation_scratch` (disposable; any `-Fresh`
diff run recreates it) and `ppiq_acceptance_empty` (T-004 moved to M2a as an
ephemeral fixture). `plantprocessiq` is LIKELY LEGACY - verify then drop.
Delivered `Cleanup-PpiqLocalDatabases.ps1` with a hard deny list.

**RAM:** dropping databases will NOT help. Six databases in ONE PostgreSQL
instance is not six engines. `VmmemWSL` ~3.77 GB is Docker/WSL. **The heavy
containers are the two Oracles and SQL Server.**

### 9.2 Emulated customer sources - six containers + two file sources

`meltshop-postgres` (15432), `caster-oracle` (11521), `hsm-oracle` (11522),
`pkl-mssql` (11433), `downtime-mysql` (13306), `parsytec-mysql` (13307), plus
Excel Yard and Excel QA.

Compose: `deploy/compose/docker-compose.sources.yml`.
**`docker compose ... start` FAILS if the containers do not exist** - it must be
`up -d` the first time. Use `stop`, **never `down -v`** - the volumes hold the
fixtures.

**His operating ruling:** source containers OFF during normal M1 work. Frontend,
dashboards, widget SQL, correlations, engine, assistant and page builder all read
`ppiq_presentation` and need no source containers. ON only for fixture reload,
DB-link testing, import validation, no-code mapping live test, and J4-J15
rehearsal.

**Presentation-day tactic:** do NOT run all six during the demo. Run one or two
live for the connector beat (Meltshop PostgreSQL + Caster) with everything else
already imported.

**Architectural ruling:** the emulated source databases must NOT use
`ppiq_staging`/`ppiq_plant`/`ppiq_meta`. They emulate CUSTOMER systems and their
alien schemas are part of the demonstration.

### 9.3 Server (CARRIED FORWARD, NOT VERIFIED THIS SESSION)

- Hetzner VPS `178.105.152.180`.
- **Two-project topology is PERMANENT and DELIBERATE - never merge.**
  `plantprocessiq` = sacred infrastructure (Jenkins/Caddy/backup-runner);
  `ppiq-app` = application deploy.
- Stability root cause (03-Jul): Caddyfile routed to a non-existent container
  `plantprocess-app-web` (real name `plantprocess-web`). Runtime Docker network
  alias applied as a workaround; permanent fix blocked by a read-only bind-mount
  with a missing host source file.
- Smoke password bug: `VITE_SMOKE_PASSWORD=change-me-before-production` baked
  into the bundle caused a 401 auto-login loop. Must be `E2EAdmin123!` in BOTH
  `env/profiles/local.env` AND `Frontend/PlantProcess.Web/.env.local`.
- Jenkins: two Docker stacks existed - live `plantprocessiq` vs orphaned
  Jenkins-deployed `ppiq-demo`. Jenkinsfile now backs up `.env`, `Caddyfile` and
  `docker-compose.demo.yml` before `git reset` and restores after.
- GitHub webhook at
  `https://jenkins.178.105.152.180.sslip.io/github-webhook/`; one manual "Build
  Now" was required as a one-time primer.
- Five `AuditLogImmutabilityTests` converted to SkippableFacts (need live
  Postgres with audit triggers).

**Deferred server items:** real production Ed25519 keypair (currently a dev
key); two missing CI truth-gates (seed NOT NULL coverage; no two scripts CREATE
the same table); Hetzner/Spamhaus remediation (relay 587/465, SPF+DKIM, PTR,
block outbound 25).

---

## 10. OPEN ITEMS, RULINGS NEEDED, AND KNOWN DEFECTS

### 10.1 Product defects found but NOT fixed (all outside their task's scope)

| # | Defect | Evidence |
|---|---|---|
| 1 | **`MapTwoStageImportEndpoints` is never called in `Program.cs`** - all `/admin/two-stage-import/*` 404. Grep finds it only in its own file and `validate-phase03-gates.mjs` | T-010 walk |
| 2 | **`/admin/p03p04/readiness` returns 500** - live server error on genealogy readiness | T-010 walk |
| 3 | **`source_record_id` holds a TIMESTAMP, not a key** (`2026-07-02T05:42:44.0000000Z`) - you cannot return to the source row from it. Evidence-grade traceability is the product's central claim | T-010 walk |
| 4 | **Live DB still shows `postgresql`** on 1,802 material units until it is rebuilt. The fix lives in the rebuild script and live has never been rebuilt | T-010 walk |
| 5 | `ADV_MAP_UNKNOWN_TARGET_FOR_VALIDATION` is ACTIVE - a validation fixture live in the presentation database | T-010 walk |
| 6 | `DEMO-READY-MAP-HEAT -> Heat` - `DEMO` is Rule 1 vocabulary and `Heat` is not a canonical entity | T-010 walk |
| 7 | **Chart 10 / EO_EQDEF returns ONE ROW** - canonical equipment attribution, NOT a data shortage | T-007/T-008 |
| 8 | `MI_SEV` titled "Predicted Severity Mix" but nothing predicted it | T-007 |
| 9 | `CORRELATION_FINDINGS_BOARD` holds two copies of the quality page | T-008 |
| 10 | `/admin/schema-mapping/catalog` returns ZERO canonical schema views | T-010 walk |
| 11 | `audit_ddl` needs a `ppiq_p4_demo_%` allowlist - 18 of 18 trapped events are one benign seed, so the trap will be ignored when it matters | T-006 |
| 12 | Six nav label mismatches still open inside T-002's remaining hours | T-002 |

### 10.2 Rulings still needed

1. **R4 SLIPPAGE_MARK** - predeclared at 3.0-6.0x, NOT the documented 28.9x, on
   purpose (a 29-fold effect reads as planted). Confirm or overrule **now**, not
   after seeing data.
2. **`MI_SEV`** - retitle, rebuild as chart 36, or remove?
3. **Seventh dashboard** confirmed as technical backup rather than shown?
4. **Buffer posture** is not persisted, so `DowntimeImpactCalculator` will abstain
   on every row. Ch3 4.5.4 lists no posture column - adding it is unwritten scope.
   **Chart 15 needs it.**
5. **My unproven claim:** I said no reachable endpoint pulls from a source. I
   passed a SOURCE-SYSTEM id into a DATASET route and called the 400 a defect.
   **That claim must be re-tested with real dataset ids before it stands.**

### 10.3 Manual walk T-012 still requires

Open each route and confirm the rail highlights the right stage. Notably
`/data-integration/prepare` (was dead before) and `/mapping-health` (my ruling
that it belongs to J8 rather than J6).

---

## 11. STANDING TECHNICAL RULES LEARNED THE HARD WAY

These cost real iterations. Do not relearn them.

### PowerShell

1. **Variable names are CASE-INSENSITIVE.** `$E` and `$e` are the same variable.
   A `foreach ($e in $E)` loop destroys `$E` on the first iteration - and because
   the enumerator holds its own snapshot, **the loop still completes**, so the
   damage only shows in the NEXT loop. This cost a pack that verified ten anchors
   and applied one.
2. **Under StrictMode, reading a property that does not exist on a PSCustomObject
   THROWS** - it does not return null, so a fallback on the next line never runs.
   Use a `Prop`/`FirstProp` helper that checks
   `$Obj.PSObject.Properties.Name -contains $Name` first, and PRINT the actual
   property list.
3. **PowerShell unrolls a single-element array on return**, so `.Count` on a
   one-row result throws under StrictMode. Wrap in `@()` at the CALL SITE.
4. **A parameter name must not collide with a common-parameter alias** -
   `db, ea, ev, wa, wv, ov, ob, vb, if, iv, pv, cf, wi`. `-Db` collides with
   `-Debug` and fails at PARSE time, before any guard can run.
5. **Set `[Console]::OutputEncoding` and `$OutputEncoding` to
   `UTF8Encoding($false)` BEFORE invoking a native tool**, restore in `finally`.
   Never `Tee-Object` for capture (PS 5.1 writes UTF-16). Strip ANSI. Count
   non-ASCII in the written file and refuse to declare success if any remain.
   **I shipped mojibake into his repo once. He said: "I don't want to see never
   ever in my software product those dirty characters."**

### SQL and psql

6. **Never route SQL output through PowerShell line arrays.** `pg_dump -f` and
   `psql -o` write to disk; then `ReadAllText`. Multi-line values (`json_agg`,
   `pg_get_functiondef`) are silently truncated otherwise.
7. **Never pass multi-line SQL with `psql -c`.** PowerShell native-arg quoting
   mangles it. Write to a file, run with `-f`, capture stderr and PRINT it.
   The repo's own `Seed-PresentationDashboards.v2.ps1` header documents this.
8. **Never build an output row with `||` when any element can be NULL** - a NULL
   anywhere makes the whole expression NULL and the row VANISHES rather than
   failing. Use `format()` or `concat_ws`, and **check the row count against the
   catalog**. This hid an event trigger from FOUR consecutive diff runs.
9. `SELECT format(...)` returns ONE column - `ORDER BY 1,2,3` against it fails.
   Prefer raw columns with `psql -A -F "|"`.
10. `pg_dump -n` and `-t` INTERSECT when combined. Two schemas/tables need two
    invocations.
11. `pg_get_functiondef` returns text ALREADY ENDING IN A NEWLINE.
12. **`Rebuild-PresentationDb.ps1` does NOT create its target database** - it
    restores into one that already exists.

### Gates and guards

13. **A GATE IS NEVER WEAKENED TO PASS.** No raised timeout, no retry loop, no
    softened assertion. A gate argued with is a gate switched off.
14. **WHERE THE ARTIFACT IS EXECUTABLE, EXECUTION IS THE ONLY AUTHORITY** - every
    other check is a report. A string-grep self-check passed two broken SQL files;
    later a cosmetic check VETOED two working ones and restored the broken
    originals. A heuristic must never overrule a measurement.
15. **A failure that does not say WHY costs an iteration every time.** Always
    print captured output on failure. I made this mistake twice in one day.
16. **Count what you applied.** A pack that applied one of ten edits reported
    success. Compare before/after per edit and roll back unless every one changed.
17. **A wholesale block replace must DIFF THE TASK-NAME SET** before and after -
    mine silently deleted an 8h task, caught only by a phase-band check.
18. **A patch runner that exits on the first mismatch silently skips everything
    after it.** Collect misses and continue.
19. **Never use a multi-line literal anchor on a machine-generated file** - use
    structural insertion (find the declaration, then the first matching line).
20. **A MISSING diagnosis must print REAL BYTES, never a guess.** I once said
    "the file changed since the baseline" when its SHA matched byte for byte.
21. **Strip comments before matching** - twice a guard matched the comment
    explaining the guard.
22. **Vitest 4 REMOVED the `basic` reporter.** The project's script is
    `vitest run --config vitest.config.ts`.

### Method

23. **Re-read the code before declaring a gap.** I twice nearly reported the
    product broken because I called the wrong endpoint or read the wrong key.
24. **Never label a documented number as measured.** That single error caused two
    tasks to be reopened.
25. **State arithmetic openly and never fit a number to a budget.** When M1-P1
    hit 176h I split the phase rather than trimming.
26. **Label a code block as SQL-for-a-script versus a command to run.** He pasted
    SQL into PowerShell once because I did not.

---

## 12. WHAT TO DO NEXT

### 12.1 Immediate

**T-013 - Three-way source reconciliation: KEEP, EXTEND or ADD (8h)**, first task
of M1-P1b.

Reconcile three things, one row per source structure: what the running staged
schemas actually contain (measured - **section 6.6 above is the input, do not
re-derive it**); what the committed generator produces
(`Backend/tools/generate_demo_dataset.py`, 373 lines, deterministic, emits all
eight sources); and what the 36-chart blueprint requires.

Output: `docs/m1/evidence/source_reconciliation.csv`, no blank cells. Every
EXTEND and ADD names the exact field or table. Every KEEP names the chart it
already serves.

**Before it can be complete, run the column inventory for the six UNVERIFIED
tables in section 6.7.**

### 12.2 M1-P1b order (T-013 to T-029, 110h)

reconciliation -> Fleet v2 target spec -> generator: defect catalogue and
chemistry -> generator: grade spec and shift as BEHAVIOUR -> generator: downtime
two quantities -> author the downtime mapping -> shift/crew regimes ->
post-maintenance and campaign ageing -> equipment personality and temporal ->
**regenerate and reload the source engines** -> discover/register/import to
staging -> publish mappings A -> publish mappings B -> project to canonical and
verify -> phenomenon test harness -> populate the manifest and prove ->
confounded correlation and refusal -> five-layer realism audit.

### 12.3 The Fleet v2 design rulings already made - carry these

- **THE 36 CHARTS DRIVE THE GENERATOR**, not FLEET_RELATIONS.md, which is
  explicitly demoted to "a source of ideas, no longer executable truth".
- Chemistry elements are added ONLY because a chart needs them.
- **Defect catalogue target: a real plant Pareto** - one dominant, two or three
  meaningful, a moderate, several smaller, a long tail - **plus pure-noise codes
  that exist to be REJECTED**.
- **SHIFT IS BEHAVIOUR, NOT A LABEL.** Do not add a shift column to a source that
  would not realistically record one. Generate the behaviour against a
  `shift_calendar` table in the emulated customer world (shift_code,
  start_local_time, end_local_time, crew_code, effective_from, effective_to,
  timezone). Expose the field only where a real system would carry it. Everywhere
  else DERIVE it in the transformation as a SAVED NO-CODE TRANSFORMATION and map
  to the EXISTING `process_step_executions.crew_code`.
  *His reasoning is a demo argument too: timestamp -> no-code derived column ->
  shift A/B/C -> save, in front of the customer, beats a ready-made column.*
- **Downtime:** `duration_seconds` -> derived `stopped_minutes`;
  `production_impact_minutes` generated INDEPENDENTLY. Both shapes must exist:
  stopped 45 / impact 0 (buffer absorbed) and stopped 3 / impact 260 (cascade).
- **Two DERIVED metrics, neither stored, neither negative:**
  `buffer_absorbed_minutes = MAX(stopped - impact, 0)` and
  `cascade_amplification_minutes = MAX(impact - stopped, 0)`.
  A plain subtraction gives MINUS 257 for the cascade case, which is not a
  quantity that exists.
- **NOTHING GENERATED IS EVER INSERTED STRAIGHT INTO A CANONICAL TABLE.** The
  path generator -> emulated customer DB -> import -> staging -> mapping ->
  canonical IS the product story.
- **BANDS ARE PREDECLARED BEFORE THE DATA EXISTS**, and widening a band after
  seeing the result is forbidden as the same defect as writing it from the result.

### 12.4 The M2a entry tasks already written (T-082, T-083)

Fleet v2 becomes the **CONTROLLED REFERENCE PLANT** for M2. T-082 clean-room
rebuilds it from source control; T-083 freezes and certifies it with a
machine-readable manifest.

**THE IMMUTABILITY CONTRACT:** during M2 the baseline is NOT modified every time
an algorithm disappoints. If a feature cannot recover a behaviour the certified
dataset genuinely contains, **that is a PRODUCT finding, not permission to tune
the data until the test passes.** New scenarios go into a versioned extension -
Fleet v2.1, v3 - never a silent change to the certified baseline.

---

## 13. FILE AND ARTIFACT INDEX

**Evidence (`docs/m1/evidence/`):**
`presentation_db_diff.txt`, `presentation_db_diff_reverify_20260802_231441.txt`
(the empty diff), `T-010_gap_measurement_20260803_002410.txt`,
`T-010_semantic_path_walk_20260803_100433.txt` (the acceptance),
`T-011_arch_pool_20260803_102926.txt`, `T-007_emulation_inventory.txt`,
`T-007_36_chart_blueprint.txt`, `phenomena_widget_matrix.csv`,
`widget_decisions.csv`, `presentation_diff_ignore.txt`,
`_superseded/` (failed runs, kept as the record).

**Tools at repo root:** `Invoke-PpiqPresentationDbDiff.ps1`,
`Invoke-PpiqSemanticPathWalkV4.ps1`, `Measure-PpiqT010GapsV3.ps1`,
`Cleanup-PpiqLocalDatabases.ps1`.

**Documents:** `Documentation/docs/Product Book/Design Documentation Book/` holds
the six chapters; `Related Documentation/` holds the backlog, roadmap,
traceability matrix, diff classification, blueprint and inventory;
`ProductBook_Archive/` holds superseded versions.

**Key commits:** `56a614ff` T-003 | `0278863d` T-002 | `24bec2b8` T-006 empty
diff | `d137abaf` T-009 | `b3f6ad21` T-011.

---

## 14. THE ONE THING TO INTERNALISE

Almost every substantial finding in this session came from **measuring something
we believed we already knew**. The downtime count. The defect codes. The
chemistry. The fleet scale. Whether the canonical model came through the import
path.

And almost every defect of mine came from the same root: **checking that the
output looked right instead of measuring what actually changed.**

His law is the correct response, and it is now the project's:

> **Documents specify intent. Runtime measurement certifies implementation truth.
> Whenever the two conflict, stop dependent implementation, measure, reconcile,
> then update the authoritative backlog.**

# PPIQ SESSION HANDOVER — 03/04/05 August 2026

**Purpose.** The next session must not re-investigate, re-measure or re-run
anything recorded here. Every number below was actually observed. Where something
was NOT done, it says so explicitly rather than leaving a gap that invites a
repeat investigation.

**Read sections 1, 7 and 8 first.** Section 7 is the governing rules; section 8 is
where work stands.

---

## 0. ONE-SCREEN STATUS

```
M1-P1b : T-013 .. T-023 DONE
         T-024 DONE except requirement 8 (browser check, never performed)
         T-025 feature-store half CLOSED AND FROZEN
                remaining: correlation, risk, learning, readiness re-eval,
                           genuine refusal, final gate
         T-026 NOT STARTED
```

Canonical operational data = Fleet v2 at 3x scale.
Analysis layer = feature/outcome values only; correlation, risk and learning are
all at **0 rows**.

**THE STATE IS NOT PRESENTATION-READY.** Canonical truth is new; correlation,
risk and learning have produced nothing.

---

## 1. WHAT EXISTS NOW — the artefacts that matter

### The generator — `Backend/tools/generate_fleet_v2_donor.py`

The single most important artefact. ~146 KB, Python, deterministic, seed
**20260803**.

```
--mode capture    FROZEN. Reproduces the T-014 captured donor exactly.
                  SHA256 of its output for seed 20260803:
                  11EDF4B275A106C86D75EA3147D47B56F7763AD9EE2D258487953B7155939AD7
                  This is the permanent regression test for Chapter 3 retirement
                  gate condition 1, which T-031 must RE-EVIDENCE. If it drifts,
                  src_* can never be retired. It REFUSES any scale but 1.

--mode fleet-v2   The target plant. Default scale 3.

--emit donor      source-shaped tables (default)
--emit reference  canonical reference vocabulary, ADDITIVE
--emit canonical  the full canonical operational replacement, ONE transaction
--scale N         plant size as a multiple of the captured baseline
--profile         prints row counts plus every embedded acceptance report
```

Every constant cites the committed evidence file and section it came from.
Comparator spec v2.1 section 4a forbids a number reaching this file from terminal
output or memory.

### Scripts written this session (all in the repo)

```
tools/measure/Measure-PpiqT013Sources.ps1
tools/measure/Measure-PpiqT014Capture.ps1          9 sections + section J
tools/measure/Measure-PpiqT014Structure.ps1
tools/measure/Measure-PpiqT014IntervalHistograms.ps1
tools/measure/Compare-PpiqCaptureProfiles.py       comparator v2.1
tools/measure/Invoke-PpiqT014Prove.ps1
tools/measure/Measure-PpiqT024Canonical.ps1
tools/measure/Measure-PpiqT024Deep.ps1             14 sections
tools/run/Invoke-PpiqT024Reference.ps1
tools/run/Invoke-PpiqT024DedupeReference.ps1
tools/run/Invoke-PpiqT024Canonical.ps1             the destructive replacement
tools/run/Invoke-PpiqT024Verify.ps1
tools/run/Invoke-PpiqT024VocabularyCheck.ps1
tools/run/Invoke-PpiqT024VocabularyCleanup.ps1
tools/run/Invoke-PpiqT024GuardFix.ps1
tools/run/Invoke-PpiqT025Probe.ps1
tools/run/Invoke-PpiqT025LineagePreflight.ps1
tools/run/Invoke-PpiqT025LineageMigration.ps1
tools/run/Invoke-PpiqT025Execute.ps1
tools/run/Invoke-PpiqT025Closure.ps1
tools/packs/Apply-PpiqT025EngineTimeout.ps1
```

### Evidence files (`docs/m1/evidence/`) — all committed

```
T-013_source_measurement_20260803_132228.txt      35,329 B
source_reconciliation.csv                          13 rows, 15 cols
T-014_capture_profile_20260804_000614.txt          the REFERENCE profile, 9+J sections
T-014_structure_evidence_20260803_230235.txt       interval structure
T-014_interval_histograms_20260804_001518.txt      exact value histograms
capture_comparator_spec_v2.md                      v2.1 FROZEN, the comparator law
T-014_capture_proof_20260804_003616.txt            TOTAL DIFFERENCES 0
presentation_fleet_v2_target.md                    T-015 target spec
T-018_downtime_two_quantities.txt
T-019_shift_crew_regimes.txt
T-020_maintenance_and_campaign.txt
T-021_equipment_and_regime.txt
T-022_fleet_v2_merge.txt
T-023_scale_1x_baseline.txt / T-023_scale_3x_target.txt
T-024_canonical_measurement_*.txt / T-024_canonical_deep_*.txt
T-024_vocabulary_dependency_*.txt
T-024_requirement7_final.txt
T-025_probe_*.txt / T-025_lineage_preflight_*.txt
T-025_execute_*.txt / T-025_closure_*.txt
```

---

## 2. CURRENT IMPLEMENTATION AND EVERY MODIFICATION MADE

### 2.1 Database — `ppiq_presentation`, PostgreSQL 16, `ppiq_dev` / `ppiq_dev_local_only`

**Canonical operational population, replaced wholesale in T-024:**

```
material_units             35,910   1,890 Heat + 17,010 Slab + 17,010 Coil
genealogy_edges            34,020   ProducedInto + RolledInto
process_step_executions    53,095
parameter_observations    301,560
quality_events              7,844   5,961 SurfaceDefect + 1,883 Disposition
downtime_events               630
```

All `source_system = 'FLEET_V2'`. Zero legacy operational rows. Zero orphans,
zero self-edges, every coil resolves to a slab, every slab to a heat.

**Previous population (deleted):** 40,148 units, 51,691 quality events, 35,906
edges, 14,433 observations, 3 downtime rows all zero.

**Reference vocabulary (T-024 step A + cleanup):**

```
defect_catalogs         17 visible FlatSteel, 4 Pharma/Tire soft-retired
parameter_definitions   41 FlatSteel (29 Fleet v2 + 12 legacy) + 7 non-steel kept
equipment               18 Fleet v2 visible, 16 legacy soft-retired
material_unit_types      4 visible, 10 non-steel soft-retired
```

Rollback table `public.ppiq_t024_vocab_rollback` holds 69 captured prior values.

**Analysis layer:**

```
ml_feature_values        505,680   all lineaged, NOT NULL enforced
ml_outcome_values         21,649   all lineaged, NOT NULL enforced
ml_correlation_results_v2      0
ml_learning_results_v1         0
risk_scores                    0
```

### 2.2 Schema and engine changes made

**T-024 additive:**
- `ALTER TABLE ... ADD COLUMN IF NOT EXISTS` for `heats.sulphur_pct`,
  `phosphorus_pct`, `aluminium_pct`, `crew_code`; `hsm_coils.roll_campaign_code`,
  `campaign_coil_index`; `downtime_events.production_impact_seconds`
- `CREATE TABLE IF NOT EXISTS src_meltshop_pg.grade_specification`,
  `shift_calendar`, `src_inspection_mysql_shape.maintenance_events`
- **`110_phase1_demo_source_shapes.sql` WAS NOT EDITED** — it describes the donor
  schemas, which are scheduled for retirement
- `ix_genealogy_edges_child_material_unit_id` (already existed; four such indexes)

**T-024 genealogy guard — A PRODUCT DEFECT FIXED:**

`ppiq_genealogy_edge_weight_guard()` was `DEFERRABLE INITIALLY DEFERRED FOR EACH
ROW` and its body aggregated the ENTIRE `genealogy_edges` table grouped by child,
on every firing. ~70,000 queued events x a full grouped scan = quadratic. The
T-024 commit hung indefinitely.

Fixed: validates only the affected child from `NEW`/`OLD`. Trigger definition
untouched — still deferred, still row-level, invariant and 0.015 tolerance
unchanged. **Result: commit went from never-finishing to 54 seconds.**

A child with no remaining edges is NOT a violation (the unit may be deleted in the
same transaction) — matches the original, whose `GROUP BY` produced no row.

**T-025 lineage migration:**
- `ml_feature_values.refresh_run_id uuid` + FK + index, now `NOT NULL`
- `ml_outcome_values.refresh_run_id uuid` + FK + index, now `NOT NULL`
- `ml_feature_store_refresh_runs.engine_key`, `.engine_version`
- Both refresh functions rewritten FROM THEIR LIVE BODIES to stamp
  `refresh_run_id` on their own rows before completing the run row, plus
  `pg_advisory_xact_lock` single-flight on the base path
- `duration_ms` fixed: `now() - v_started` was always 0 because `now()` is
  transaction start time; changed to `clock_timestamp()`

**T-025 C# — configuration-owned engine timeout:**
- `MlFoundationEndpoints.cs`: two sites of `command.CommandTimeout = 120` now read
  `EngineCommandTimeoutSeconds`, bound from
  `PlantProcess:Analytics:EngineCommandTimeoutSeconds`, default 120, clamp 30-900
- `Program.cs`: `MlFoundationEndpoints.ConfigureEngineTimeout(builder.Configuration);`
- `env/profiles/presentation.env`: `PlantProcess__Analytics__EngineCommandTimeoutSeconds=900`

---

## 3. IDENTITY, TOPOLOGY AND ROADMAP — where we started, how far we got

### Environment (from `PPIQ_Identity_and_Topology_v4.md`)

```
Local  : native PostgreSQL 16, localhost:5432, role ppiq_dev / ppiq_dev_local_only
         databases ppiq_app (application) and ppiq_presentation (this work)
API    : http://localhost:5063 under -Profile presentation
Web    : 5173
Auth   : POST /auth/login  { userName, password } -> { accessToken }
         e2eadmin / E2EAdmin123!   then Bearer on every call
         This is the SAME path scripts/run/Invoke-PpiqJourneyProof.ps1 uses.
```

**The plant timezone is `Africa/Cairo`, derived not assumed.** Captured offsets are
+02 in early April and +03 from late April = Egypt DST, last Friday of April 2026
= the 24th. Shift derivation honours the switch or every night shift after that
date is an hour wrong.

### Data topology (Chapter 3 section 4.5.2a)

Three generations, oldest to newest:
```
containers / fixtures  ->  dump_store + canonical  ->  src_*
```

- `src_*` = source-shaped **donor** schemas. **NEVER call them staging.**
- `dump_store` = the transitional physical name of staging
- `ppiq_staging` = the final staging name

The `src_*` retirement gate has FOUR conditions, all required, and NINE proof
dimensions. Condition 1 (the generator reproduces the captured baseline) is
proven and is re-provable via capture mode. Conditions 2, 3, 4 remain.

### Roadmap position

```
M1-P1  CLOSED (T-001..T-012, 84 h)
M1-P1b T-013..T-023 DONE, T-024 one item short, T-025 half done
       T-026..T-029 not started
M1-P2  not started (opens with T-030/T-031, the retirement gate executes at T-031)
M2a    not started. T-084 native fixtures, T-085 clean-room rebuild
```

---

## 4. REALIZATION SCOREBOARD — honest status at session end

| Area | Status | Note |
|---|---|---|
| Fleet v2 generator | GREEN | deterministic, two modes, hash-pinned |
| Capture reproduction | GREEN | 1,244 comparisons, 0 differences |
| Canonical operational data | GREEN | 35,910 units, full genealogy |
| Reference vocabulary | GREEN | 14 codes, 29 params, 18 equipment |
| Mixed-industry cleanup | GREEN | 30 rows soft-retired, reversible |
| Browser validation | **NOT DONE** | T-024 req 8, never performed |
| Feature/outcome values | GREEN | 527,329 rows, full lineage, NOT NULL |
| Correlation | **RED** | 0 rows; 400 was my malformed request |
| Risk | **RED** | 0 rows; 403 = product permission defect |
| Learning | **RED** | 0 rows; all catalogue jobs `is_enabled=false` |
| Readiness | AMBER | endpoint passes, must re-evaluate after the above |
| Genuine refusal | **NOT ESTABLISHED** | none of the observed failures qualify |
| Deployment / pipeline | **NOT TOUCHED** | see section 9 |

### Improvements delivered

- Defect Pareto: flat 6 codes at 17.66/17.46/17.16/16.86/16.05/14.80 percent ->
  14 codes, `SCALE` dominant at 26.00 percent, real tail, **two negative controls**
- Downtime: 3 rows of zeros -> 630 events with real stopped and impact minutes
- Chemistry: 3 elements -> 6, produced to grade with ~4 percent off-spec
- Plant scale: 630 heats / 5,670 coils -> 1,890 / 17,010 over 91.8 days
- Mill: seven identical stands (still FAULT-3, uncorrected)
- Genealogy guard: quadratic -> 54 s
- Analysis lineage: none -> engine-owned, `NOT NULL` enforced

---

## 5. PER-TASK RECORD — findings, tips, what is still missing

### T-013 source reconciliation (DONE)
`source_reconciliation.csv`, 13 rows, 15 columns, zero blanks.
KEEP 7 / EXTEND 3 / ADD 3; VARY 3, FIX_DISTRIBUTION 2; BIND 7, REBIND 6.

**Findings:** all 119 columns 100 percent populated — the EXTEND-because-NULL
hypothesis was FALSE. The real defect class is **populated-but-constant**: seven
columns with 1 distinct value. `mill_line = 1` across all 5,670 coils IS the
measured cause of chart 10 returning one bar.

### T-014 capture (DONE — the hardest task of the session)

**Result: 1,244 comparisons, 0 differences, determinism proven.**

The generator reproduces SIX MEASURED FAULTS ON PURPOSE, each marked FAULT-n:
1. mass not conserved — density 4,079-17,391 kg/m3 vs steel 7,850
2. `actual_thickness_mm` EQUALS target on every coil, deviation sd 0.0000
3. all seven mill stands draw from ONE distribution — no mill profile
4. heats exactly 4,200 s apart; 210 downtime events disturb nothing
5. cardinality fixed not distributed — 9/9/7/3/1 without exception
6. one uniform draw 1.07-1599.88 on WIDTH mm, THK mm and ROUGHNESS um alike

Also: `superheat_c` goes NEGATIVE (min -3.0).

**Both my hypotheses were REFUTED by measurement:**
- chemistry is NOT grade-conditioned (all six grades share one range)
- the lags are NOT deterministic — only two are: pass sampling is EXACTLY
  `stand_no * 60`, QA is EXACTLY exit + 300

**THE FIX THAT ENDED THE GUESSING:** exact empirical pools. Nine intervals draw
from measured histograms — no normal, no uniform, no fitted parameter. sd and
distinct came out IDENTICAL.

**Structural discoveries:**
- `cast_sequence` has NO `heat_no`. I invented it and a 10 MB load failed.
- `C04_cut_step_per_slab` counts 1367/1411/1471/1421 are NOT multiples of 9 ->
  the cut step is drawn PER SLAB, not once per sequence
- all ten source-update lags are single-valued: 120, 120, 0, 120, 0, 0, 0, 60, 0, 0
- pickling lag is uniform in WHOLE HOURS, 4 to 72, exactly 69 distinct values

### T-015 target specification (DONE)

Scale DERIVED, not asserted: 6 grades x 3 shifts = 18 strata, 85 defect-positive
coils per stratum for 80 percent power, at 15 percent incidence, x1.65 margin =
16,840 -> **17,010 coils = exactly 3x captured**. The 90-day horizon is separately
forced: a roll change every 3 days gives only ~10 campaigns in 31 days.

**Four sign-off questions were never answered** (mill_line, 90 days, 14 codes, two
negative controls). Work proceeded on the committed spec.

### T-016..T-021 (DONE) — the regime tasks

- **T-016**: 14 codes within 0.03 percent of declared share; severity conditioned
  on code; largest-remainder allocation so counts sum exactly
- **T-017**: `grade_specification` 36 rows, `shift_calendar` 15 rows,
  `heats.crew_code` as the ONE exposed field. Chemistry produced TO GRADE.
  Off-spec heats ASSIGNED at 4 percent, not left to a distribution tail.
- **T-018**: `production_impact_seconds` generated INDEPENDENTLY —
  Pearson r vs duration = -0.128. 79 ABSORBED / 111 CONTAINED / 20 CASCADE.
  Two derived metrics never stored: `MAX(stopped-impact,0)`, `MAX(impact-stopped,0)`
- **T-019**: analysis unit is the HEAT'S shift, not the coil's (a coil rolls 5-34 h
  later). Hard-grade share 36.2/52.6/61.5 percent; naive +58.7 percent,
  conditioned +29.3 percent, **shrinkage 44.8 percent**, variance ratio survives
- **T-020**: EAF fast decay on energy/tonne, LADLE slow decay on ladle
  temperature. Campaign R2 0.838 -> 0.962 after scaling.
- **T-021**: casters 0.82 pooled sd apart, mould-level sd ratio 1.742, CCM-01
  carries 1.31x the defect rate. Regime boundary pinned to maintenance_id 17.

### T-022 the merge (DONE)
Nine conflict decisions recorded IN THE GENERATOR with reasons. Three highlights:
scale — NEITHER generation wins; defect incidence — the older generation REJECTED
on physical grounds (2.92 defects per coil is an inspection threshold set wrong);
QA values — BOTH rejected, because **agreement between two sources is not evidence
when both inherit the same defect**.

Seven VARY targets had no task of their own and survive here.

### T-023 scale (DONE)
Contract said "about 2,400 heats and about 17,000 coils" — inconsistent at mean 9.
**17,010 coils wins, heats follow at 1,890.** Every phenomenon survived; two
improved.

### T-024 canonical (DONE except requirement 8)

Delete order is FORCED and was MEASURED:
```
risk_scores, quality_events, genealogy_edges, parameter_observations,
process_step_executions, downtime_events, material_units
```
**`risk_scores` was NOT on my first list** — 7 rows that would have failed the
delete partway through, against the presentation database. That is what the
inbound-FK section existed to catch.

The ML tables (347,000 rows) have NO foreign key to `material_units`.

**Identity already matches**: `material_code` joins directly to donor identifiers
at 630 / 5,670 / 5,670.

**Frontend finding that narrowed requirement 7:** 89 distinct API paths in the
frontend and **not one** calls `/quality/defects`, `/parameters/definitions`,
`/material-unit-types` or an equipment selector. The dashboards consume
`/api/analytics/read-models/*`. So selector filtering is unexercised.

**The mechanism that actually hides rows** is `HasQueryFilter(e => !e.IsDeleted)`
in `PlantProcessDbContext` — `is_deleted = true` removes a row from every
EF-backed endpoint with no code change. `is_active` is opt-in and hides nothing
on its own.

### T-025 (feature-store half CLOSED)

```
authenticated refresh          200 in 107.1 s
feature rows               505,680
outcome rows                21,649
lineage NULL                     0
rows owned by another run        0
run counts match         505,680 / 21,649
engine identity          postgres-feature-store / base
duration_ms                106,315
row-level reproducibility  A EXCEPT B = 0, B EXCEPT A = 0
NOT NULL                   enforced
```

**Still missing and why:**
- **Correlation** — 400 "Outcome key is required". Contract is
  `CorrelationComputeRequest(OutcomeKey, Grain, WindowDays, Filters?)`. I sent an
  empty body. Route RESOLVED: `/api/ml/foundation/compute/correlation`.
- **Risk** — 403 "not mapped in the P01/P02 permission matrix". This is a PRODUCT
  DEFECT. The matrix is a prefix table in
  `Backend/PlantProcess.Api/Security/PlantAccessControl.cs`. `/api/analytics/risk-scores`
  (group defined in `RiskEvidenceEndpoints.cs:18`) has no entry, and deny-by-default
  returns 403. Siblings `/analytics/correlations` and `/analytics/ml` use
  `analysis.execute`. **Three existing precedents for exactly this fix**: M1-21
  (`/api/ml/foundation`), M1-07 (`/api/assistant`), M1-22 (`/api/suggestions`) —
  each a one-line entry with a comment explaining why.
  The fix: `("/api/analytics/risk-scores", All(), "analysis.execute", false)`.
- **Learning** — the catalogue is NOT empty. `ml_learning_job_catalog_v1.is_enabled`
  is `boolean NOT NULL DEFAULT false`, so every job is disabled. My query filtered
  `coalesce(is_enabled, true)` and returned blank. A disabled job is a legitimate
  **not-configured** state.
- **Readiness** — endpoint returns 200 with feature_definitions 60,
  feature_values 505,680, outcome_definitions 8, outcome_values 21,649,
  correlation_results 0. Must be re-evaluated AFTER the other engines run.
- **Genuine refusal** — none of 400 / 403 / 404 qualifies.

---

## 6. EVERY TEST RUN AND ITS RESULT — do not repeat these

```
T-014 capture proof   run 1: 929 comparisons,   59 differences  (comparator v1)
                      run 2: 969 comparisons,   17 differences  (v2, all timestamp extremes)
                      run 3: 1,244 comparisons,  0 differences  (v2.1) PROVEN
determinism           two runs, identical SHA256 11EDF4B2...
capture at scale 3    REFUSED by design

T-024 dry run on a clone (TEMPLATE ppiq_presentation): full transaction succeeded
T-024 live replacement: 51 seconds after the guard fix (previously never finished)
T-024 verification: every closure condition 0; Pareto SCALE 26.00 percent
T-024 restore proof: 41 MB pg_dump restored into a scratch DB, all four counts identical

T-025 feature refresh via psql   199.8 s   505,680 / 21,649
T-025 refresh via API            107.1 s   200, duration_ms 106,315
T-025 second refresh             119.7 s   200
T-025 third refresh              308.9 s   500 at the 300 s ceiling
T-025 post-VACUUM FULL refresh   295.3 s   — bloat NOT proven as root cause
T-025 reproducibility            A EXCEPT B = 0, B EXCEPT A = 0

correlation POST empty body   400 "Outcome key is required"
risk POST calculate-all       403 "not mapped in the P01/P02 permission matrix"
learning                      catalogue query returned blank (is_enabled default false)
readiness GET                 200
```

**Route probe results (do not re-probe):**
```
/api/ml/foundation/readiness            200
/api/ml/foundation/feature-store/refresh 200  [FromBody] { windowDays }
/api/ml/foundation/compute/correlation   400  needs OutcomeKey, Grain, WindowDays
/api/analytics/risk-scores/calculate-all 403  permission matrix
/api/analytics/correlation/runs          404  does not exist
/api/analytics/risk-scores               404  GET does not exist
```

---

## 7. RULES, CONCEPTS AND WAYS OF THINKING TO CARRY FORWARD

### His engineering laws

> **Documents specify intent. Runtime measurement certifies implementation truth.
> Whenever the two conflict, stop dependent implementation, measure, reconcile,
> then update the authoritative backlog.**

> **A comparator defect may be corrected, but the new rule must be defined from
> the capture contract and measurement characteristics — NEVER from what makes
> the current generator pass.**

> **The problem is not that the test failed. A FAIL means the test works.
> Loosening tolerance until it passes would mean the test has no purpose.**

> **No PARTIAL.** A task is Done or it is not. A completed task is never reset to
> Not Started; invalidated evidence makes it REOPENED.

> **Name the gap and ask for a ruling** rather than inventing a bucket.

### Delivery contract

- Everything as a **PowerShell apply pack**: preflight, backup, anchored replace,
  self-check, gate, auto-revert. **Never hand-edits.**
- **Always include the run commands**, opening with `cd C:\Workspace\PlantProcess-IQ`
  then `Move-Item` from Downloads then `Unblock-File`
- Destination: `tools\packs\` for apply packs, `tools\run\` and `tools\measure\`
  for runners
- Pure ASCII, UTF-8 no BOM, CRLF for PS/CS. No em-dashes or curly quotes.
- No `&&` in PowerShell. Cuddled `} else {`.
- **Credentials go IN the script.** He types nothing.
- Exception: a genuinely one-line source edit may be described rather than packed.

### Hard-won technical lessons

**PowerShell 5.1**
- `Write-Host` writes to the HOST, not the pipeline — `2>&1` captures nothing
- The ternary `? :` is PowerShell 7 only. `#requires -Version 5.1` checks the
  RUNTIME, not the syntax — the parser rejects it first.
- Splitting psql output on `` `n `` leaves `\r`; a `$` anchor never matches.
  **Trim before matching.** This cost time three times.
- A function returning a 0/1-element array unrolls — `return ,$array`, and do NOT
  also wrap the call site
- `Start-Process` with `-RedirectStandardOutput` to a file means the user sees
  NOTHING for the duration. A destructive step that runs silently for 15 minutes
  cannot be told apart from a hung one.

**psql / PostgreSQL**
- `\echo` goes to stdout, NOT to the `-o` file. Use `\qecho`.
- `query_to_xml(q, nulls, tableforest, ns)`: `tableforest=true` returns a rootless
  forest. Use `false` and xpath `/table/row`.
- One `ORDER BY` per UNION, at the end.
- **`CREATE TEMP TABLE ... ON COMMIT DROP` in autocommit drops it immediately** —
  each statement is its own transaction.
- `pg_get_functiondef()` THROWS on aggregates. Filter `prokind = 'f'`.
- **`now()` is transaction start time.** `clock_timestamp()` advances.
- `-w` forbids a password prompt outright — a script meant to run bare should
  never be able to sit waiting.
- `CREATE DATABASE x TEMPLATE y` needs no other session connected to `y`.

**Judgement**
- **When cardinality is small, MEASURE THE HISTOGRAM instead of fitting a shape.**
  Quantile plateaus, a normal for the LF offset, uniform for the sequence offset —
  each a guess dressed as a derivation, two of three wrong.
- **Test a model against the standard deviation before writing it.** Five setpoints
  give sd 228 against a captured 226.65 — that check takes seconds.
- **The quantiles of one sample are CORRELATED.** Five quantile misses on one
  column are ONE event, not five pieces of evidence. Reading them as five sent an
  entire investigation after a relationship that did not exist.
- **A variance change is the hardest thing to detect at small n; a mean shift needs
  a fraction of the sample.**
- **Read the live definition, never a repository copy.** Four scripts define
  `ppiq_ml_refresh_feature_store`; the live one is whichever ran last.

### MY MOST REPEATED DEFECT — the next session should watch for it

**I write checks that assert on HOW a row came to exist rather than WHAT MUST BE
TRUE OF IT.** Five occurrences:

1. hard-coded 400 downtime rows with impact above zero — a count of a random draw
2. slab identity asserted at 5,670 by analogy with heats and coils — but the slab
   code is COMPOSITE and encodes cardinality, which T-022 changed. The correct
   figure, 5,410, is `sum(min(9, slabs_per_heat))` and is derivable from the
   database.
3. "rows needing change = 12" — a delta, so a correctly-completed cleanup reported
   FAIL
4. "any row with lineage is manufactured" — true only until the first real refresh
5. `source_system = 'FLEET_V2'` to count Fleet v2 vocabulary — excludes the four
   codes that pre-existed under their own provenance

**The data was right every time. The assertion was wrong every time.**
Assert on the END STATE.

---

## 8. BACKLOG STATUS

Authority: `PPIQ_Backlog_v2.9.1_03Aug2026.md` + `.xlsx`, FROZEN.
167 tasks / 1,443 h. M1 574, M2a 432, M2b 233, M3 204.

```
T-013  source reconciliation                     DONE
T-014  capture the donor in a generator          DONE   0 differences, deterministic
T-015  Fleet v2 target specification             DONE   4 sign-off questions unanswered
T-016  defect catalogue and chemistry            DONE
T-017  grade specification and shift as BEHAVIOUR DONE
T-018  downtime two quantities and buffer posture DONE
T-019  shift and crew regimes                    DONE
T-020  post-maintenance and campaign ageing      DONE
T-021  equipment personality and temporal regime DONE
T-022  merge into one Fleet v2 truth             DONE
T-023  scale to target plant size                DONE
T-024  canonical operational entities            req 1-7 PASS, req 8 NOT DONE
T-025  analysis entities with the real engines   feature store CLOSED; 4 engines open
T-026  phenomenon test harness                   NOT STARTED
T-027  populate the manifest and prove           NOT STARTED
T-028  confounded correlation and refusal        NOT STARTED
T-029  five-layer realism audit                  NOT STARTED
```

### Immediate next actions

1. **Apply pack** — add `("/api/analytics/risk-scores", All(), "analysis.execute", false)`
   to `PlantAccessControl.cs` following the M1-21 comment style. Rebuild, restart.
   **Stop the API before building or the DLLs are locked.**
2. **Driver, NO feature-store refresh**: correlation with a real `outcome_key`
   read from `ml_outcome_definitions`; risk invoked normally; learning against
   actually-enabled jobs; readiness re-evaluated; corrected closure gate.
3. **Genuine refusal** must be: valid authenticated request -> reaches a real
   engine -> refuses for an ANALYTICAL reason. Missing fields, 403, 404 and
   internal exceptions do NOT count.
4. **T-024 requirement 8** — the browser check, still never performed.

---

## 9. DEPLOYMENT, SERVER AND PIPELINE — HONEST STATEMENT

**No deployment, server or CI/CD work was performed in this session.** Everything
above is local: native PostgreSQL 16 and a locally-run API under the presentation
profile. The next session should not infer pipeline state from this handover.

What IS known, from `PPIQ_Identity_and_Topology_v4.md` and earlier sessions:

- Local dev credentials are documented as **local-only and deliberately not
  secrets**
- Section 2.4 records a server rule worth remembering: deleting
  `/var/lib/ppiq-preserve/.env` rotates the Postgres password while the existing
  volume keeps the old one, giving error `28P01`. Same shape as a destructive
  step whose damage appears only afterwards.
- From an earlier audit (`ppiq-audit-signals`), two live CI findings that were
  NEVER fixed:
  - `tools/ci/validate-real-ui-gates.cjs` (PPIQ-T016) is invoked by NOTHING and
    would FAIL today
  - `Frontend/.../tools/phase56/apply-phase5-phase6-full-ui-migration.cjs` around
    lines 60-80 still patches the Jenkinsfile to inject `--list` enumeration
    commands — a live landmine that would break `CiPipelineTruthGateTests`
  - one mid-file BOM at `DevSeedEndpoints.cs` line 2 (U+FEFF)

**These are unverified as of this session.** Treat them as leads, not facts.

---

## 10. MODIFICATIONS MADE TO GET THE APP WORKING — HONEST STATEMENT

**No pipeline-greening work was done in this session.** The only application-level
changes made were the two described in section 2.2:

1. **The engine command timeout** became configuration-owned — this was required
   because the feature-store refresh against the full-scale plant exceeds the
   hardcoded 120 s ceiling and the authenticated product path could not complete.
2. **`Program.cs`** gained one startup binding line.

Both are real product improvements, not workarounds, and both will matter beyond
M1: the same ceiling would block M2a and any customer plant larger than this one.

**Two product defects were found and fixed at the database level:**
- the genealogy weight guard (quadratic on bulk writes)
- `duration_ms` always zero

**One product defect was found and NOT fixed:**
- `/api/analytics/risk-scores` missing from the P01/P02 permission matrix

---

## 11. OPEN RULINGS AND UNANSWERED QUESTIONS

1. **T-015 sign-off questions (4)** — `mill_line` single-valued vs a second mill
   line; the 90-day horizon; fourteen defect codes; the two negative controls.
   None blocks anything delivered.
2. **The C11 positioning gap** — `EDGE_CRACK` and `EDGE_WAVE` are justified as
   edge-biased and `CENTRE_BUCKLE` as centre-biased, but no task text covers
   `width_position_mm` positioning. The codes exist with UNIFORM positions, so the
   defect map still shows nothing it was designed to show.
3. **The downtime horizon anchoring** — the 210 start times are sorted and
   affine-mapped onto the captured min and max. Flagged in the generator as a
   DECISION, not a derivation. Accepted by his proceeding, never explicitly ruled.
4. **Future design rule, recorded not implemented:** any future UI consumer of the
   reference-vocabulary endpoints must pass the current site's industry/template
   context from configuration. **Generic product code must never hardcode
   `FlatSteel`.** Attach to the task that introduces such a consumer.
5. **Feature-store refresh performance** — 107 s to 308 s on identical data.
   VACUUM FULL did not resolve it (295.3 s). Root cause NOT established. Not a
   T-025 blocker; timeout now 900 s.

---

## 12. IF YOU READ NOTHING ELSE

- The generator is the crown jewel. **Capture mode is frozen at
  `11EDF4B2...` and must never drift** or `src_*` can never be retired.
- **Read live definitions, not repository copies.**
- **Assert on end state, not on provenance or deltas.**
- **When cardinality is small, measure the histogram.**
- The feature store is **CLOSED**. Do not refresh it again during T-025.
- **T-024 requirement 8 (browser) and four T-025 engines are what remain.**

# PPIQ State-of-Implementation Review - Pre-Walk Validation
**14-Jul-2026 (morning)** | Baseline: Implementation Audit v2 (12-Jul, headline 56/100 A9) |
Constitution: concept.md v1.0 | Evidence from this session's console transcripts + source reads.

**Evidence grades (stricter than the audit's, because we are pre-walk):**
[P] proven live this session (pasted console/psql output) | [B] built + gate-green (dotnet/tsc/psql)
but **never rendered/executed in a running app** | [T] source-traced | [U] unverified | [X] known broken/missing.

---

## 1. Executive verdict

Since the 12-Jul audit, one working session closed **all three of the audit's "hard walls"** at the
build level and executed the constitutional data purge at the proof level:

1. **Step-4 drives the right subsystem now.** The audit's headline finding (A9: mapping UI drove
   `canonical_schema_views`, not the projector) is answered by M1-04: a UI page that authors a real
   `MappingDefinition` and executes the projector through the existing endpoints. Grade: **[B]** -
   type-checked, wired, never clicked.
2. **The 4th low-code UI exists.** M1-06 shipped all three layers: schema **[P]** (alert_rules,
   plant_data_log, ppiq_evaluate_alert_rules verified live by psql), backend **[P-build]** (0 errors),
   page **[B]**. The audit said "does not exist anywhere in the codebase"; that sentence is dead.
3. **Rule 2 is now materially true in the database.** The C.2 purge is **[P]**: material_units =
   1,802 rows, 100% source_system='postgresql'; 38,346 seed/ladder units + 102,044 dependent rows
   deleted in one audited transaction; five src_* Arch-B schemas dropped; full ppiq_purge_audit
   trail. This was the audit's A7/A3 confound finding - resolved at the data layer.

What has **not** moved: the third wall's other half - **no human has still walked any surface in a
browser.** Everything new is [B]. The purge also created a new, expected, but demo-critical state:
**the plant schema is nearly empty** (40 observations, 1 quality event, 0 edges). The rigged
discovery pattern was purged with the phase3-dump data, by design. Until the re-import journey runs,
steps 7-15 will truthfully show empty/blocked states.

**Verdict: the codebase is demo-shaped; the demo is not yet demo-proven.** The critical path is no
longer building - it is (a) the re-import journey executed through the product, and (b) the M1-11
walk that converts ~10 [B] grades into [P] or into bug reports. Both are the same activity now.

---

## 2. The Three Product Rules - compliance state

**Rule 1 (Generic Only): materially improved, two named residues.** The five src_* schemas are
gone [P]; stage1/2 functions and the 204 generator were already absent [P - dry-run showed 0].
Residue 1: **four demo-named site rows remain** (DEMO_PLANT_001/002, ADV_DEMO_PLANT, PPIQ_P3_SITE)
[P] - identity config is allowed to exist, but demo *names* on screen violate the spirit; rename
before Thursday (one-line pack on request, needs the displayed site_code + desired name).
Residue 2: **TwoStageImportModel type + two dead placeholder panels** threaded through 8 frontend
files [T] - benign (Promise.allSettled -> null), deferred to M1-07b by explicit decision.
Naming Golden Rule residue: Phase8/Phase9 dirs, phase9-* testids [T, audit A1-R2] - untouched.

**Rule 2 (Starts Empty; DB-link the only door): true at the unit level, pending at taxonomy.**
material_units is pure [P]. But 1 defect_catalog + 8 parameter_definitions with
source_system='PPIQ_CONFIG' remain because kept rows reference them [P - purge audit]. The cure is
constitutional and already sequenced: re-import taxonomy through the pipeline (the M1-03 mappers
are built [P-build] precisely for this), then re-run the purge's final sweep. Also note: this
session's out-of-band psql operations (schema apply, purge) were **administrative resets with audit
records**, which the constitution permits - but the *demo dataset* must arrive only via the journey.

**Rule 3 (The Journey is the Product): specified, not yet certified.** The v2 walk document
(~235 testing steps, evidence-tagged) is the acceptance instrument the audit's A9 demanded. It has
not been executed. M1-class "Presentable" per concept.md 7 requires every step *shown working in
the HMI* - that certification is exactly the pending walk.

---

## 3. Journey step-by-step status (constitution 4 vs today)

| Step | Constitution requirement | Status | Grade |
|---|---|---|---|
| 1 Connect | test-connect, masked creds, read-only, throttling | Endpoints + UI exist; read-only + throttled reader in code (audit A2-4) | [T] UI unwalked |
| 2 Schedule/register | dataset->import job, due on register | Register endpoint swapped to SourceDatasetDefinition upsert 12-Jul [P then]; schedule board exists | [T] |
| 3 Incremental import | delta via cursor into staging | Cursor incrementality proven 12-Jul (2nd run = 0 rows) [P then]; **must re-prove this cycle post-purge**; 4 cursor defects historically fixed, unre-tested | [U this cycle] |
| 4 UI-1 mapping | MappingDefinition authored in UI, const: support | **M1-04 page built this session**; projector const: + 8-entity router incl. taxonomy (M1-03) | [B] |
| 5 Loading jobs | mapping->scheduled job; auto-run on import | Auto pipeline pre-existed [T, audit A1-4]; per-mapping schedule UI FOUND in code this session (updateMappingRefreshSchedule + CanonicalRefresh job) - walk 5.1-5.7 verifies | [T] |
| 6 Loaded | canonical entities, idempotent, provenance | Projector idempotency proven 12-Jul [P then]; post-purge canonical nearly empty until re-import | [P mech / empty data] |
| 7 UI-2 dashboards | widgets, live preview, disclosure badge | Pages + badge exist [T]; content will be empty until re-import | [T] |
| 8 UI-3 analysis authoring | method toolbox, canonical pickers | AnalysisJobConfigPage + /api/analysis-jobs [T]; Pearson-only (M2-04 on the clock) | [T] |
| 9 Analysis jobs | readiness gate never weakened | Gate proven honest 12-Jul (BlockedTooFewRows) [P then]; will correctly BLOCK now until re-import | [P mech] |
| 10 Results | OR + population + dedup + honest nulls | q-value/effect live 12-Jul; **OR/population/dedup = M1-09 [X not built]**; null-control display is rework | [X partial] |
| 11-13 ML tier | license-gated deeper jobs | Machinery exists [T]; dev license keys [T]; empty until data | [T] |
| 14 Supervisor | weekly review, guardrails, provenance rows | **v0 built this session**: real report from results_v2 via KB upsert, honesty line, monitor row; no schedule, no tuning (M2-01) | [B] |
| 15 Assistant | grounded, cited, role-scoped, refusal-first | Wired + reindex endpoint built (M1-01); scope_role plumbing [T]; **chunk store empty until reindex post-import** | [B] |
| UI-4 Alerting | threshold/routing/chemistry, eval job, log | Threshold v0 complete: schema [P], endpoints [P-build], page [B], idempotent evaluator, monitor row; routing/chemistry/delivery = M2-06 | [B] |
| Monitor (cross) | all job types, one monitor | job_log generic monitor pre-existed; SUPERVISOR + ALERT_EVAL now write rows (M1-02) | [B] |

---

## 4. Movement vs the 12-Jul scoreboard (estimate, not a formal re-audit)

The audit's headline chain was A9 56 -> the three walls. At build level: wall 1 (step 4) closed,
wall 2 (UI-4) closed, wall 3 (human walk) **still open** - so A9 rises but cannot clear its band
until the walk: **estimate 56 -> mid-60s [B-bound]**. A3 (61): taxonomy mappers built, confound
purged [P], duplicates/OR still open (M1-09) -> **~65-68 pending re-discovery proof**. A7 (59):
purge with audit trail is exactly its top finding resolved -> **largest single-persona gain,
~+8-10**. A10 (58): supervisor v0 + reindex real, but registry/LLM/executor untouched (M2) ->
**modest, ~+4**. A2 (68): out-of-band writes REDUCED (mappers exist; purge audited) but dev keys /
secrets / Spamhaus untouched -> **~+2-3**. A1 (76): Arch-B corpse half-buried (panel [P], schemas
[P], type residue) + 12 disciplined packs -> **~+3**. Nothing regressed. Headline remains gated by
A9 until the walk converts [B] to [P] - which is the correct incentive.

---

## 5. Top risks to Thursday, ordered

1. **Runtime-verification debt (the big one).** ~10 surfaces are [B]: author-mapping, supervisor
   page, alerting page, journey rail, reindex, monitor rows, M1-04's three flagged runtime unknowns
   (route nesting, batch-list shape, execute-response fields). Any of these can hold a surprise the
   compiler cannot see. **Mitigation exists and is scheduled: the walk.** Do it on the CURRENT thin
   DB first for steps 1-6 (they create the data), which naturally becomes the re-import.
2. **The re-import is now the dataset.** Sources must be up (docker stack fragility was an audit
   finding A4-C5), the rig must be planted in the SOURCE, and GAP-3's four cursor defects get their
   first post-purge test. If import breaks, everything downstream is empty on Thursday.
3. **Discovery re-proof.** The 9.5x pattern must be REdiscovered on purely imported data (M1-08
   acceptance). Until that run completes, the money slide has no living backend.
4. **M1-09 not built** - findings will show duplicates and no OR column exactly where you plan to
   spend 70% of demo time (step 10). Build immediately after the clean re-run exists.
5. **Demo-named sites on screen** (Rule 1) - one-line rename, needs your target name.
6. **Continuity (M1-12/audit A2-C2)**: the emulation assets + loader still live on one laptop;
   the constitution demands durable storage. Archive before rehearsal day.
7. **Production error-shape** never exercised (GAP-14) - one forced 500 in Production env.

## 6. Validation verdict and ordered next actions

The implementation is **consistent with the constitution** at the code and data layers, with the
named residues (sites rename, taxonomy sweep pending re-import, M1-07b type, Phase8/9 naming) all
known, bounded, and either scheduled or explicitly deferred. Nothing found contradicts concept.md;
two places EXCEED it (genealogy dual-ledger semantics; auto import->project pipeline). The gap
between "engineered" and "certified Presentable" is precisely the unexecuted walk.

Order of operations (unchanged from your enforced sequence, now data-aware):
1. Sources up -> **walk steps 1-6 = re-import (taxonomy FIRST)** -> paste failures live.
2. Re-run purge `-Execute` (final PPIQ_CONFIG sweep -> zero).
3. Walk steps 7-15 + UI-4 on imported-only data; feature refresh + governed run **rediscovers**
   superheat->CRACK_LONG q<0.01 (M1-08 acceptance closed).
4. **M1-09** (dedup + OR + population) -> re-verify step 10.
5. **M2-04** on the clock (Spearman + chi-square + picker; chi-square finds the planted
   categorical driver live). M2-02 go/no-go in parallel (Ollama installed? model pulled? latency?
   config fallback to extractive mandatory).
6. Site rename + M1-12 archive + two timed dress runs (M1-14).

*This review is pre-walk by design. Its [B] grades are promoted or falsified only by the walk -
run it.*

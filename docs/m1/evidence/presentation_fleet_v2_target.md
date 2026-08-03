# PRESENTATION FLEET v2 TARGET SPECIFICATION

**T-015 | 4 August 2026 | M1-P1b**

**Authority.** This document states the dataset target the generator must reach.
It is written **from the 36-chart blueprint**, not from `FLEET_RELATIONS.md`.
That document was wrong three times in one day and is a source of ideas, nothing
more.

**The rule this document obeys.** *No figure appears without the chart or
phenomenon that needs it.* Where a number has no chart behind it, it is not here.

**Committed inputs, and nothing else:**

| Input | What it supplies |
|---|---|
| `docs/m1/evidence/widget_decisions.csv` | the 36 blueprint charts and their required fields |
| `docs/m1/evidence/source_reconciliation.csv` | the KEEP / EXTEND / ADD decisions of T-013 |
| `docs/m1/evidence/T-014_capture_profile_20260804_000614.txt` | the captured baseline, proven reproducible |
| `docs/m1/evidence/T-014_structure_evidence_20260803_230235.txt` | interval structure, grade-conditioning result |
| `docs/m1/evidence/T-014_interval_histograms_20260804_001518.txt` | exact interval distributions |

**What this document does not do.** It does not change the generator. T-014
captured the current state and proved it reproducible; T-016 onward moves the
generator toward this target, one declared change at a time, so captured work
stays separable from designed work.

---

## 1. IS THE CURRENT SCALE SUFFICIENT? NO, AND HERE IS THE BINDING CONSTRAINT

The captured plant is 630 heats and 5,670 coils over 31 days.

The binding requirement is **not** any single chart's readability. It is the
population needed by the conditioned correlation charts, C25 and C26, and by the
nine-check gate behind them.

The derivation, stated openly:

```
strata required by C21 and C22        6 grades x 3 shifts        = 18
coils per stratum to detect a
  moderate association at 80 percent
  power                                                          = 85 defect-positive
target coil-level defect incidence                               = 15 percent
coils per stratum                      85 / 0.15                 = 567
total coils                            567 x 18                  = 10,206
margin for the conditioning of C29
  and the refusal cases of C28         x 1.65                    = 16,840
TARGET COILS, rounded to a clean
  multiple of the captured scale       5,670 x 3                 = 17,010
```

**17,010 coils is three times the captured scale**, which is also close to the
17,817 of the older imported generation. That coincidence is not the reason for
the number; it is a check that the number is not eccentric.

At the captured cadence and nine coils per heat this is **1,890 heats over
approximately 90 days**. The 90-day horizon is separately required:

| Requirement | Needs |
|---|---|
| C18 roll campaign ageing | at least 8 to 10 complete campaigns; a roll change every 3 days gives 30 in 90 days, and only 10 in 31 |
| C17 post-maintenance recovery | at least 15 maintenance events with enough production after each to show recovery |
| C01 production over time | more than one month, or "over time" is a single bar |
| C13 to C16 downtime | 630 events rather than 210, so a Pareto of reasons has a tail |

**The 31-day horizon is the reason C17 and C18 cannot be drawn today**, not the
absence of a campaign column alone.

---

## 2. TARGET SCALE PER STRUCTURE

Every row names the chart that justifies its figure.

| Structure | Captured | Target | Cardinality | Justified by |
|---|---:|---:|---|---|
| `heats` | 630 | **1,890** | 21 heats/day over 90 days | C25, C26 stratified population; C03 output by grade |
| `lf_treatment` | 630 | **1,890** | 1 per heat | C19 to C23, a second parameter stage |
| `cast_sequence` | 630 | **1,890** | 1 per heat | C03, C06 |
| `cast_pieces` | 5,670 | **17,010** | **7 to 11 per heat, mean 9** | C05, C06, C27, C28; cardinality varies because fixed 9 is captured FAULT-5 |
| `hsm_coils` | 5,670 | **17,010** | 1 per piece | C01, C02, C04, C05, C06, C07, C10, C18 |
| `hsm_pass_measurements` | 39,690 | **119,070** | 7 per coil | C19 to C23 |
| `pickle_orders` | 5,670 | **15,300** | **90 percent of coils** | C19, C20, C22, C23; 10 percent take a route that skips pickling, which is what makes `route_code` vary |
| `qa_lab_results` | 17,010 | **45,900** | 3 per pickled coil | C19, C20, C23 |
| `parsytec_surface_defects` | 1,987 | **5,600** | 15 percent of coils affected, mean 2.2 each | C07 to C11, C18, C23, C27, C28 |
| `downtime_events` | 210 | **630** | same rate over 90 days | C13 to C16 |
| `grade_specification` | 0 | **36** | 6 grades x 6 elements | C12, the only chart with no source structure at all |
| `shift_calendar` | 0 | **3 shifts x crew rotation** | 3 shifts, 4 crews | C02, C06, C14, C22 |
| `maintenance_events` | 0 | **120** | 30 roll campaigns + 90 planned and unplanned | C17, C18 |

**Defect incidence changes shape, not only volume.** Captured is 17.6 percent of
coils affected with a flat 0 / 1 / 2 / 3 ladder. Target is 15 percent affected
with a realistic tail: most affected coils carry one defect, a minority carry
several, and a small number carry many. C07 and C11 both read wrong when every
affected coil carries at most three.

---

## 3. THE DEFECT CATALOGUE - THE HEADLINE OF THIS DOCUMENT

The captured catalogue is six codes at 17.66, 17.46, 17.16, 16.86, 16.05 and
14.80 percent. **That is not a Pareto, it is a uniform draw with six labels**,
and it is the exact condition C08 was written against.

The target, with a role for every code:

| Code | Target share | Role | Why this code exists in the target |
|---|---:|---|---|
| `SCALE` | 26.0 | **dominant** | C08 needs one code that owns the chart; scale is the classic hot-mill dominant |
| `EDGE_CRACK` | 15.0 | meaningful | C11 defect map needs an edge-biased code, so `width_position_mm` is not uniform |
| `ROLLED_IN_SCALE` | 12.0 | meaningful | C23 and C27 need a code that responds to a rolling parameter |
| `SLIVER` | 9.0 | meaningful | C09 needs a code whose rate differs by grade |
| `INCLUSION` | 7.0 | moderate | C09 and C12 need a code traceable to meltshop chemistry |
| `PINHOLE` | 6.0 | moderate | C28 needs a code responding to casting speed and superheat |
| `SCRATCH` | 5.0 | rare | C08 tail |
| `WAVINESS` | 4.0 | rare | C08 tail; shape class, so `defect_class` is not surface-only |
| `CENTRE_BUCKLE` | 3.5 | rare | C11 needs a centre-biased code opposite `EDGE_CRACK` |
| `EDGE_WAVE` | 3.0 | rare | C08 tail, shape class |
| `ROLL_MARK` | 2.5 | rare | C18 needs a code that rises with roll campaign age |
| `LAMINATION` | 2.0 | rare | C08 long tail |
| `OIL_SPOT` | 3.0 | **negative control** | generated INDEPENDENTLY of every parameter; C25, C26 and C28 must REJECT it |
| `SENSOR_ARTEFACT` | 2.0 | **negative control** | independent of everything; if a correlation page reports it, the page is broken |

Shares sum to 100.0. Top code 26 percent, top three 53 percent, top six 75
percent, and a tail of eight codes below 5 percent each.

**The two negative controls are the most important rows in this table.** A
correlation surface that finds only true associations has proved nothing; it must
be seen rejecting something. At 5,600 defects the smaller control still carries
112 events, which is enough to be visibly rejected rather than absent.

**Severity stops being uniform.** Captured has all 18 code-and-severity
combinations between 90 and 131 events. Target: severity is conditioned on code -
`EDGE_CRACK` and `LAMINATION` skew high, `OIL_SPOT` and `SCRATCH` skew low.

---

## 4. CHEMISTRY - SIX ELEMENTS, EACH BECAUSE A CHART NEEDS IT

Captured carries carbon, manganese and silicon. C12 draws a conditional-format
conformance grid, and three elements make a grid too small to read as one.

| Element | Added because |
|---|---|
| `C` | already present; C12 primary axis |
| `Mn` | already present; C12 |
| `Si` | already present; C12 |
| `S` | C12 needs an element with a **maximum only** and no minimum, so the conditional format has two rule shapes rather than one |
| `P` | C12, same reason as sulphur; and C09, where phosphorus plausibly drives `EDGE_CRACK` |
| `Al` | C12; the IF-LOW-C and DP600 specifications constrain aluminium, so a grade-specific band exists |

**Niobium, vanadium, titanium, nitrogen and the rest are NOT added.** No chart in
the blueprint needs them. Twelve elements would look impressive and serve nothing.

`grade_specification` therefore holds 6 grades x 6 elements = **36 rows**, with
`min_value`, `target_value`, `max_value` and `unit`. At least one heat per grade
falls outside its band and at least one sits inside, so C12 shows both states.

---

## 5. THE VARY TARGETS - COLUMNS THAT ARE POPULATED BUT CARRY ONE VALUE

T-013 recorded three VARY rows. Each becomes a target here, and one of them
changes approach with a reason.

| Column | Captured | Target | Justified by |
|---|---|---|---|
| `heats.target_temp_c` | 1650, single | **per grade**, 5 distinct | C12 and the target-versus-actual story; a plant does not tap every grade to the same temperature |
| `hsm_coils.target_fdt_c` | 875, single | **per grade**, 5 distinct | finishing temperature is a grade property |
| `hsm_coils.target_ct_c` | 610, single | **per grade**, 5 distinct | coiling temperature is a grade property |
| `heats.route_code` | 1 route | **3 routes** | 90 percent full route, 10 percent skipping pickling, a small number direct-ship; this is what makes `pickle_orders` cover 90 percent rather than 100 |
| `cast_sequence.sequence_status` | Completed, single | **4 statuses** | a minority aborted or shortened, so C03 and C06 show a real operation |
| `cast_sequence.planned_grade` vs `actual_grade` | identical in all 630 | **deviation on 3 to 5 percent** | a grade-deviation story exists in every real caster and none exists here |
| `parsytec.inspection_device` | 1 device | **2 devices** | C10 and the equipment comparison |

### The one that changes approach, and it needs a ruling

**`hsm_coils.mill_line` stays single-valued.** T-013 recorded VARY. I am
proposing not to.

A plant with one hot strip mill is entirely realistic, and inventing a second
mill to make a chart draw is the fabricated convenience the v2.6 shift ruling
already forbids. C04 output by equipment and C10 defects by production unit are
better served through **genealogy**: a coil resolves to its caster (2), its
furnace (2), its LF (2) and its pickling line (2), and those already vary.

This makes C04 and C10 charts of the **plant**, not of the mill, which is what
the chart titles actually say. If you would rather have two mill lines, say so
and this row becomes a VARY like the others.

---

## 6. THE FIX_DISTRIBUTION TARGETS

| Item | Captured | Target | Justified by |
|---|---|---|---|
| QA `measured_value` | one draw 1.07 to 1599.88 across WIDTH mm, THK mm and ROUGHNESS um | **per test code, in physical range**: width around its order, thickness around its target, roughness 0.5 to 4.0 um | C19, C20, C23; a thickness of 1,599 mm is spotted instantly |
| Defect Pareto | six codes within a three-point spread | section 3 of this document | C08 |
| Defect severity | uniform across all 18 combinations | conditioned on code | C08, C09 |
| Defects per coil | flat 0 / 1 / 2 / 3 ladder | long-tailed, most affected coils carry one | C07, C11 |

---

## 7. THE THREE ADDED STRUCTURES

**`shift_calendar`** - `shift_code`, `start_local_time`, `end_local_time`,
`crew_code`, `effective_from`, `effective_to`, `timezone`. Three shifts, four
crews on rotation. Per the v2.6 ruling, shift is **behaviour**: day runs
conservatively with lower parameter variance, evening sits between, night carries
wider variance. The field is exposed only where a source would realistically
record it; everywhere else it is derived in the transformation from the local
timestamp plus this calendar.

**`grade_specification`** - 36 rows as section 4.

**`maintenance_events`** - `equipment_code`, `maintenance_start_utc`,
`maintenance_end_utc`, `maintenance_type`, and a campaign boundary flag. 30 roll
campaigns over 90 days plus 90 other events. C18 ages defect rate within a
campaign; C17 shows recovery after maintenance.

---

## 8. THE THREE EXTEND TARGETS

| Structure | Field | Justified by |
|---|---|---|
| `heats` | `sulphur_pct`, `phosphorus_pct`, `aluminium_pct` | C12 |
| `hsm_coils` | `roll_campaign_code`, `campaign_coil_index` | C18 |
| `downtime_events` | `production_impact_seconds` | C15; T-009 closed the two-quantity contract in canonical and no source field feeds the second quantity |

---

## 9. WHAT IS DELIBERATELY NOT CHANGED

**Chemistry is not conditioned on grade beyond its specification band.** Section
G of the structure evidence measured carbon, manganese and silicon per grade and
found all six grades sharing one range. The target adds per-grade **bands** in
`grade_specification` because C12 needs them, and heats will sit inside or
outside their own band - but no hidden grade-conditioned distribution is invented
beyond what the specification implies.

**The six captured faults are corrected here as targets, not silently.** Mass
conservation, the zero thickness deviation, the flat mill profile, the production
metronome, fixed cardinality and the QA distribution each appear above with the
chart that needs them fixed. None was corrected in T-014, and each becomes a
declared change in T-016 onward.

**Scale is a T-023 task.** This document states 17,010 coils and 90 days and
justifies both; it does not scale anything.

---

## 10. SIGN-OFF QUESTIONS

1. **`mill_line`** - accept the genealogy approach of section 5, or make it a
   second mill line?
2. **90 days** - accepted, or is a shorter horizon preferred with fewer campaigns
   and a weaker C17 and C18?
3. **Fourteen defect codes** - accepted, or is the tail too long for a
   demonstration audience?
4. **Two negative controls** - accepted? They are the only way a correlation
   surface can be seen rejecting something.

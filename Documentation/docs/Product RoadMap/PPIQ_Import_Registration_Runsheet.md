# PPIQ Import Registration Runsheet - Fleet Sources
**15-Jul-2026** | Walk steps 1-6, click-by-click | All column names verified live (SourceTaxonomyViews reports 14:26 + 14:32)
Chain proven: `H-26014 -> SL-60105 -> C-700394 -> parsytec` | Money slide: **R1 superheat -> CRACK_LONG = 9.3x** (SCRATCH = 1.0x control)

---

## 0. Preconditions (once)

1. **CP-04 + CP-06 -> Edit -> Schema Name = `PPIQ_SRC` -> Save -> Test-connect -> Discover.**
   This is the discovery-400 fix. Expect CASTER: CC_HEATS / CC_SEQUENCES / CC_SLABS / V_PARAMETER_DEFINITIONS; HSM: HSM_COILS / HSM_PASSES / V_PARAMETER_DEFINITIONS.
2. CP-01 discovery should now show 11 tables (10 + `v_parameter_definitions`). CP-03 shows 3 (2 + `v_defect_definitions`).
3. **DO NOT register:** `meltshop_defect_events`, `meltshop_param_readings` (legacy fixture tables coexisting in the container - not part of the fleet; drop from the source post-Thursday), `ms_equipment_counters`, `ms_additives`, `cc_sequences` (breadth, not demo path).

---

## PHASE A - TAXONOMY (must complete before any facts)

| # | Profile | Dataset | Target entity | Field map | Cursor |
|---|---|---|---|---|---|
| A1 | CP-01 Meltshop | `v_parameter_definitions` | **ParameterDefinition** | parameter_code -> code; parameter_name -> name; value_type -> value type; parameter_group -> group/unit context | `last_seen_utc` |
| A2 | CP-06 Caster | `V_PARAMETER_DEFINITIONS` | **ParameterDefinition** | same shape (4 rows: superheat_c, tundish_temp_c, avg_cast_speed_mpm, avg_mould_width_mm) | `LAST_SEEN_UTC` |
| A3 | CP-04 HSM | `V_PARAMETER_DEFINITIONS` | **ParameterDefinition** | same shape (7 rows incl. lube_viscosity_cst - the R4 28.9x driver) | `LAST_SEEN_UTC` |
| A4 | CP-03 Parsytec | `v_defect_definitions` | **DefectCatalog** | defect_code -> DefectCode; defect_name -> DefectName; defect_category -> DefectCategory | `last_seen_utc` |

**Verify after Phase A (Rule-2 proof):** `parameter_definitions` gains ~37 rows and `defect_catalogs` gains 20 rows, every one with import-batch provenance and connector source_system. This also completes the taxonomy sweep: the 8+1 residual `PPIQ_CONFIG` rows can now be retired at the final sweep.

---

## PHASE B - UNITS + GENEALOGY (the spine)

| # | Profile | Dataset | Target entities | Field map | Cursor |
|---|---|---|---|---|---|
| B1 | CP-06 | `CC_SLABS` | **MaterialUnit** | SLAB_ID -> business key; STEEL_GRADE -> grade; CUT_UTC -> produced at; WEIGHT_T/LENGTH_M/THICKNESS_MM -> dims; `const:SLAB` -> unit type | `CUT_UTC` |
| B2 | CP-06 | `CC_SLABS` (2nd mapping, same dataset) | **GenealogyEdge** | HEAT_ID -> parent key; SLAB_ID -> child key; `const:1.0` -> weight (trigger: weights sum to 1.0 per child - single parent, so 1.0) | `CUT_UTC` |
| B3 | CP-04 | `HSM_COILS` | **MaterialUnit** | COIL_ID -> business key; STEEL_GRADE -> grade; ROLLED_UTC -> produced at; COIL_WEIGHT_T/COIL_LENGTH_M/TARGET_GAUGE_MM -> dims; `const:COIL` -> unit type | `ROLLED_UTC` |
| B4 | CP-04 | `HSM_COILS` (2nd mapping) | **GenealogyEdge** | SLAB_ID -> parent key; COIL_ID -> child key; `const:1.0` -> weight | `ROLLED_UTC` |

Heats already exist as material_units (1,802 rows, meltshop import). If the projector requires parents to pre-exist, B1/B2 order per batch is heats(existing) -> slabs -> coils, which this sequence satisfies.

**Verify after Phase B:** material_units ~ 1,802 + 18,661 + 18,661; genealogy_edges ~ 37,322; `ppiq_walk_genealogy` on any coil returns coil -> slab -> heat; Material Investigation page: search a coil, thread renders three generations. **That thread IS journey step 6's screenshot.**

---

## PHASE C - OBSERVATIONS (the X axis of the money slide)

| # | Profile | Dataset | Target | Field map | Cursor |
|---|---|---|---|---|---|
| C1 | CP-06 | `CC_HEATS` | **ParameterObservation** | HEAT_ID -> material business key; `const:superheat_c` -> parameter code; SUPERHEAT_C -> value; START_UTC -> observed at | `START_UTC` |
| C2 | CP-04 | `HSM_COILS` | **ParameterObservation** (per-column mappings as the UI allows: one mapping per parameter or multi-map) | COIL_ID -> material key; `const:finish_temp_c` / FINISH_TEMP_C; `const:coiling_temp_c` / COILING_TEMP_C; `const:lube_viscosity_cst` / LUBE_VISCOSITY_CST; `const:campaign_wear_idx` / CAMPAIGN_WEAR_IDX | `ROLLED_UTC` |
| C3 *(optional / overnight)* | CP-04 | `HSM_PASSES` | **ParameterObservation** | COIL_ID -> material key; per-column const codes (entry/exit temp, reduction_pct, rolling_force_kn) | `PASS_UTC` |

**Timing discipline:** C3 is 111,966 rows through a throttled reader - do NOT start it in the demo window. C1 (1,802) and C2 (18,661) are minutes. C3 runs tonight as breadth; if it isn't done by morning, it isn't in the demo.

---

## PHASE D - QUALITY EVENTS (the Y axis)

| # | Profile | Dataset | Target | Field map | Cursor |
|---|---|---|---|---|---|
| D1 | CP-03 | `parsytec_surface_defects` | **QualityEvent** | COIL_ID -> material business key; defect_code -> defect (resolves against Phase-A catalog); detected_at_utc -> event time; severity -> severity; position_start_m/position_end_m/width_mm -> extent | `detected_at_utc` |

34,312 rows - tens of minutes throttled; start it before a coffee, not before the meeting.

**Verify after D:** quality_events ~ 34k; a coil in Material Investigation shows defects on the thread; `SELECT source_system, COUNT(*) FROM quality_events GROUP BY 1` shows only connector provenance.

---

## PHASE E - THE ENGINE RUN (steps 8-10)

1. Feature refresh, then the governed correlation run (AnalysisJobConfig / analysis job with readiness gate).
2. Gate math: superheat is heat-level, CRACK_LONG is coil-level - the genealogy walk attributes heat parameters to coils. Expected: **CRACK_LONG ~ superheat_c ranks top, q < 0.01, effect ~ 9.3x; SCRATCH shows no driver** (the honest-null slide). If C2 ran, WAVY_EDGE ~ rolling force (R3, 9.5x) and SLIPPAGE_MARK ~ lube viscosity (R4, 28.9x) appear as supporting findings.
3. Then step 15: `/api/assistant/reindex` -> ask "which process parameter drives CRACK_LONG?" -> cited answer; ask something with no evidence -> refusal.

---

## Post-run sweeps (already scripted, run after Phase D)
1. Final taxonomy sweep: retire the 9 residual PPIQ_CONFIG rows (now superseded by imported taxonomy).
2. Re-run `Report-Rule1-ResidueA3.v2.ps1` - D9 must be connector-only, D10 populated.

## Rule-2 sentence for the room (verbatim, if asked)
"Every row you see arrived through the DB-link import you just watched - the plant schema was empty this morning, and every unit, observation, and defect carries the import batch it came from."

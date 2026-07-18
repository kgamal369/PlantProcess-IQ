# PPIQ Realistic Fleet - Planted Relations Catalog (v1, 3 months, seed=42)
Scale: 1,802 heats / 956 sequences / 18,661 slabs / 18,661 coils / 111,966 stand passes /
34,312 surface defects (20 codes) / 15,782 pickled coils / 8,920 QA tests / downtime with
equipment-vs-production stoppage semantics (TF buffer absorption, breakout cascades).

GENEALOGY: heat_id -> cc_slabs.slab_id -> hsm_coils.coil_id -> parsytec/pkl/QA/yard (grade inherited).
CONSERVATION: coil_width = slab_width - 2..6mm; coil_weight = slab_weight*0.985;
coil_length = slab_length * thickness_ratio * 0.985.

CAUSAL RELATIONS (validated effect sizes, exposed vs baseline defect rate per coil):
 R1 CRACK_LONG   ~ peritectic C-band x superheat x cast speed ......... 9.3x
 R2 INCLUSION    ~ scrap/DRI ratio + low Al + tundish age ............. 4.5x
 R3 WAVY_EDGE    ~ rolling force per gauge + roll wear ................ 9.5x
 R4 SLIPPAGE_MARK~ lubrication viscosity out of 33-50 cSt window ...... 28.9x
 R5 ROLL_MARK    ~ roll campaign wear (age since roll change) ......... 3.8x
 R6 EDGE_CRACK   ~ sulfur content + low finishing temperature
 R7 SCALE_ROLLED ~ high finishing temperature + thick gauge
 R8 CRACK_TRANS  ~ peritectic + Nb microalloying
 R9 SLIVER/LAMINATION/BLISTER ~ scrap ratio / nitrogen (meltshop origin)
 R10 PINHOLE/OSCILLATION_MARK ~ superheat / cast speed
 R11 GAUGE_DEV   ~ slippage events; WIDTH_DEV ~ campaign wear
 R12 oxygen_ppm  ~ scrap ratio (meltshop-internal)
 R13 electricity_mwh ~ scrap ratio + cold furnace + bucket count
 R14 power_on_min ~ buckets + scrap ratio + furnace state (maintenance reheat)
 R15 breakout probability ~ superheat + scrap ratio -> 4-6h production stoppage
 R16 HSM cobble downtime ~ slippage (TF buffer absorbs first 45 min: equipment != production stop)
 R17 pkl line_speed ~ inverse gauge;  QA yield/UTS ~ grade family
 CONTROLS (must NOT correlate): SCRATCH (1.0x), DENT, SEAM ~ pure noise.
Plus systemic structure: chemistry-by-grade across 12 elements x 3 sample stations (EAF/LF trim),
additive quantities by grade, sequence grouping by grade family, crew rotation, ladle reline counters.

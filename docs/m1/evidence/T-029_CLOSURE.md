# T-029 CLOSURE RECORD - Five-layer realism audit of the emulated plant

Milestone / Phase : M1 / M1-P1b
Database          : ppiq_presentation
Recorded          : 2026-08-05

--------------------------------------------------------------------------------
1. LAYER RESULTS
--------------------------------------------------------------------------------

  STRUCTURAL    PASS            8 checks, zero offending rows
  PHYSICAL      FAIL            1,167 offending rows, 3 parameters
  DENSITY       NOT COMPUTABLE  2 of 4 required inputs absent
  TEMPORAL      PASS            5 checks, zero offending rows
  STATISTICAL   FAIL            9 parameters with a uniform-random signature
  ANALYTICAL    PASS            4 checks, zero offending rows
  TRIGGER       PASS            the weight guard fired by name, then rolled back

A LAYER THAT COULD NOT BE COMPUTED IS NOT A PASS. The density cross-check is
recorded as unmet so that a later reader cannot mistake an absent check for a
clean one.

--------------------------------------------------------------------------------
2. WHAT PASSED, AND WHAT THAT MEANS
--------------------------------------------------------------------------------

STRUCTURAL. Heat to slab to coil resolves end to end. Zero coils and zero slabs
without a genealogy parent, zero dangling edges in either direction, zero quality
events or parameter observations with an unresolved material, zero material units
without a site, and zero coils that fail to resolve to a heat through the lineage
view. Population: 1,890 heats, 17,010 slabs, 17,010 coils.

Five source-shaped schemas exist and were discovered rather than assumed:
src_meltshop_pg, src_caster_oracle_shape, src_hsm_oracle_shape, src_pkl_mssql_shape
and src_inspection_mysql_shape. The eight declared sources also name a Downtime
MySQL, a Yard file and a QA file; three of the eight have no schema here. That is
recorded, not treated as a structural failure, because continuity across the
canonical chain is complete.

TEMPORAL. No step precedes its predecessor. Zero children produced before their
genealogy parent, zero units ending before they started, zero quality events
before their material existed, zero parameters observed before their material
existed, zero process steps ending before they started.

ANALYTICAL. Every population the manifest depends on is present and non-empty,
and zero analysis rows exist without a compute run identity.

TRIGGER. The validation says the genealogy weight check is already enforced by a
database trigger and asks for confirmation that it fires. Every trigger on the
material and genealogy tables was enumerated from pg_trigger rather than inferred
from the repository, which matters because
ppiq_genealogy_edge_weight_guard_after_change is live and was not found in the
tracked scripts searched. It is a CONSTRAINT TRIGGER, DEFERRABLE INITIALLY
DEFERRED, so it normally fires at COMMIT - and a transaction that rolls back never
reaches that point. SET CONSTRAINTS ... IMMEDIATE was used so it fires on the
statement, which is the only way to confirm firing AND still roll back. It raised
its own message:

  Genealogy contribution weights must sum to 1.0 per child.
  child=6b3b5826-99e4-5020-82a4-b2b96f1315b5, sum=0.500000

After ROLLBACK, zero children were off the 1.0 weight sum. The confirmation
required the trigger's OWN message, so a different error raising would have been
reported as a false positive rather than as the guard firing.

--------------------------------------------------------------------------------
3. FINDING ONE - TWO GENERATOR DISTRIBUTIONS HAVE NO PHYSICAL FLOOR
--------------------------------------------------------------------------------

Every bound below is the parameter's own expected_min_value and
expected_max_value. No range was invented by the audit.

  parameter     declared     observed        offending   share    worst breach
                range        range                                as pct of range
  SUPERHEAT_C   10 .. 60     -2.712 .. 51.030      834   4.903%          25.42%
  CARBON_PCT    0.010..0.250  0.000 ..  0.213      332  17.566%           4.17%
  POWER_KWH     0 ..100000   69048.5..101071.9       1   0.053%           1.07%

  Total 1,167, reconciled independently against the audit's own count.

SUPERHEAT_C GOES NEGATIVE. Superheat is the excess of liquid steel temperature
above its liquidus. A negative value describes steel being cast below its own
melting point, which is not a tail but an impossibility. 834 observations breach,
and the observed maximum of 51.0 against a declared 60 shows the whole
distribution sitting low with its lower tail drawn straight through zero.

CARBON_PCT REACHES EXACTLY ZERO. 332 of 1,890 heats breach a declared floor of
0.010, and the observed minimum is 0.000. Carbon of exactly zero is not steel.
The hard 0.000 floor with a modest 4.17 percent worst breach is the signature of
a distribution clipped at zero rather than at its declared minimum.

POWER_KWH IS NOT A DEFECT. One observation of 1,890, 1.07 percent past the
declared maximum. That is an ordinary tail and is recorded as such rather than
being bundled with the other two to inflate a count.

OWNER AND REMEDY. This is a generator change, not an audit or a threshold change.
The declared ranges are correct and the draws should respect them. ESTIMATE: 1
hour to add a physical floor to both distributions and regenerate. NO RANGE WAS
WIDENED TO MAKE THIS LAYER PASS - widening expected_min_value until the data fits
would delete the finding rather than fix the plant.

--------------------------------------------------------------------------------
4. FINDING TWO - NINE PARAMETERS ARE DRAWN UNIFORM RANDOM
--------------------------------------------------------------------------------

The task requires natural variation, noise, outliers and shifts RATHER THAN
UNIFORM RANDOM. A uniform variable has an interquartile range of almost exactly
half its full range; a naturally varying one is materially narrower. The test is
IQR divided by range, and the uniform signature is 0.48 to 0.52.

  UNIFORM SIGNATURE          natural cohort, for contrast
  OXYGEN_NM3       0.5135    THICKNESS_MM     0.4070
  LF_ARGON_NM3     0.5117    SILICON_PCT      0.2926
  BATH_TEMP_C      0.5028    CARBON_PCT       0.2404
  LINE_SPEED_MPM   0.5023    CT_C             0.2210
  LF_CALCIUM_M     0.5015    FDT_C            0.2171
  QA_THK_MM        0.4974    SUPERHEAT_C      0.2101
  QA_WIDTH_MM      0.4963    ROLL_FORCE_KN    0.1887
  QA_ROUGHNESS_UM  0.4951    ROLL_TEMP_C      0.1727
  ACID_CONC_PCT    0.4842

Nine of twenty-nine measured parameters cluster tightly around 0.50 while the
rest sit between 0.17 and 0.41. That separation is not marginal. The nine are
concentrated in two families - the ladle-furnace additions (oxygen, argon,
calcium) and the pickling line and QA measurements (bath temperature, line speed,
acid concentration, QA thickness, width and roughness).

WHY IT MATTERS BEYOND REALISM. A uniform variable carries no natural clustering,
so it cannot exhibit a regime, a shift or an outlier. Any phenomenon declared on
one of these nine is undiscoverable by construction, whatever the analysis engine
does.

OWNER AND REMEDY. Generator change. ESTIMATE: 2 hours to replace nine uniform
draws with distributions carrying natural central tendency and tails, and to
re-run this layer. Zero parameters have zero spread, so nothing is constant.

--------------------------------------------------------------------------------
5. THE DENSITY CROSS-CHECK IS NOT COMPUTABLE
--------------------------------------------------------------------------------

The validation requires derived volume times steel density compared against
stated weight within a stated tolerance. It needs four inputs:

  WIDTH_MM      present
  THICKNESS_MM  present
  LENGTH_MM     ABSENT - no such parameter code
  WEIGHT_KG     ABSENT - no such parameter code

Two of the four do not exist in the canonical parameter set, so the check cannot
run. It is recorded as UNMET rather than substituting a proxy and calling the
result a density test.

The check exists to catch exactly the inconsistency the Fleet v2 generator
already documents in its own header as FAULT-1: weight_kg is drawn independently
of width, thickness and length, so implied density is not physical. The fault is
real and donor-side; canonical does not currently carry the columns to expose it.

REMEDY. Emit LENGTH_MM and WEIGHT_KG as canonical parameter observations, then
this layer becomes computable and will very likely fail on FAULT-1 - which is the
correct outcome, because the check would then be doing its job. ESTIMATE: 1 hour
to emit, plus whatever fixing FAULT-1 itself is separately estimated at.

--------------------------------------------------------------------------------
6. MY OWN DEFECTS IN THIS TASK
--------------------------------------------------------------------------------

6.1 I guessed the genealogy_edges column names. v1 used parent_unit_id and
    child_unit_id; the real names are parent_material_unit_id and
    child_material_unit_id. Two layers errored mid-run. v2 asserts the columns in
    preflight and prints the real list, so a wrong name becomes a refusal rather
    than two dead layers.

6.2 The v1 trigger test used those same wrong names, so its
    PPIQ-T029-TRIGGER-FIRED notice was a FALSE POSITIVE - a column error read as
    the guard firing. v2 requires the raised message to contain the guard's own
    wording and reports anything else as a false positive in those words.

6.3 A patch of mine silently did not apply. The per-parameter breakdown was
    anchored on a two-line string using LF against a CRLF file, so it matched
    nothing and failed without an error, leaving 1,167 as a bare total. Delivered
    separately as Invoke-PpiqT029RangeBreakdown.ps1.

--------------------------------------------------------------------------------
7. A CORRECTION TO A T-027 STATEMENT
--------------------------------------------------------------------------------

The T-027 status record says the canonical parameter set contains no caster
variable. THAT IS WRONG. This audit measured SUPERHEAT_C, CASTING_SPEED_MPM and
MOULD_LEVEL_AVG, each with 17,010 observations.

What actually differs is naming. The phenomena matrix declares
casting_speed_m_min; the plant emits CASTING_SPEED_MPM. superheat_c matches the
emitted code exactly. So some rows classified SOURCE_VARIABLE_NOT_MATERIALISED
are VOCABULARY MISMATCHES rather than missing data.

T-027's zero-executable conclusion is unaffected - R1's assertion is a rate ratio
and unsupported by the frozen harness regardless of variable availability - but
the stated reason was wrong, and "the matrix was declared against a plant richer
than the one that was generated" is too strong. T-027 is not reopened. This is an
input to the future matrix work.

--------------------------------------------------------------------------------
8. STATUS
--------------------------------------------------------------------------------

  T-029 implementation / execution   COMPLETE
  T-029 acceptance                   BLOCKED

  BLOCKER: two of the five layers return offending rows and the density
  cross-check cannot run. All three are generator and emission shortfalls whose
  remedy the frozen task itself assigns to a small generator change with its own
  estimate, not to a threshold or tolerance change.

  Estimates recorded: 1 hour physical floors, 2 hours uniform distributions,
  1 hour emit LENGTH_MM and WEIGHT_KG.

No range was widened, no tolerance was loosened, no threshold was touched and no
layer that could not run was reported as passing.

--------------------------------------------------------------------------------
9. EVIDENCE
--------------------------------------------------------------------------------

  docs/m1/evidence/T-029_realism_audit_v2_20260805_182726.txt
  docs/m1/evidence/T-029_range_breakdown_20260805_183020.txt
  tools/run/Invoke-PpiqT029RealismAudit-v2.ps1
  tools/run/Invoke-PpiqT029RangeBreakdown.ps1
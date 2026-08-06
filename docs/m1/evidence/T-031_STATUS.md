# T-031 STATUS

Task     : T-031 - Certify cross-layer consistency and retire the obsolete donor state
Milestone: M1 / M1-P2
Recorded : 2026-08-06

--------------------------------------------------------------------------------
T-031 CORE IMPLEMENTATION COMPLETE
--------------------------------------------------------------------------------

Certification              PASS
10 dimensions              PASS
Divergence detection       PASS
Rollback cleanliness       PASS

Deferred closure items:
- unconditional CI truth-gate integration
- backup/restore proof
- final schema-qualified src_* dependency check
- src_* retirement

THE DEFERRED ITEMS ARE NEITHER COMPLETED NOR WAIVED. They return later as one
compact T-031 closure bundle, after the higher-priority M1 work converges.

--------------------------------------------------------------------------------
WHAT THE CERTIFICATION MEASURED
--------------------------------------------------------------------------------

Subject   : dump_store STAGING versus canonical PLANT.
            src_* is the DONOR and is never a side of the comparison
            (Chapter 3 section 4.5.2a rule 4).
Direction : gated STAGING -> CANONICAL. The reverse is reported, never gated.
Counts    : never compared across layers.

All ten dimensions PASS:
  grades, equipment identities, defect vocabulary, downtime semantics,
  chemistry vocabulary, QA definition set, QA units, genealogy, time horizon,
  planted phenomena.

Injected divergence : PROVEN RED. The assertion lives in the database - a DO
block measures the baseline, injects one defect code absent from canonical,
re-measures the same expression and RAISES an exception if the count did not
increase, so psql exits non-zero and the transaction aborts. Correctness does
not depend on parsing stdout.
Rollback residue    : 0.

Fleet v2 identity coverage is complete: canonical coils absent from staging 0,
staging coils absent from canonical 0, staging coils absent from the donor 0.

--------------------------------------------------------------------------------
THE DEFECT THIS TASK FOUND AND CORRECTED
--------------------------------------------------------------------------------

The donor was a CAPTURE-mode emission at scale 1 while canonical was a
FLEET-V2 emission at scale 3 - one generator flag apart, two different plants.
That single cause produced all three certification reds: the legacy ROLLED_IN
defect code, a flat six-code Pareto against the fleet Pareto, and 5,452 coils
whose canonical parent edge pointed at a different slab.

Corrected by re-emitting the donor with --mode fleet-v2 --scale 3 --seed
20260803, then regenerating dump_store from it through the existing stage-1
import. Canonical was never touched, so T-024 and T-025 stand and no analytical
refresh was required.

Retirement-gate conditions 1, 2 and 3 are evidenced. Condition 4, one backup
taken AND RESTORED SUCCESSFULLY, is among the deferred items above.

--------------------------------------------------------------------------------
EVIDENCE
--------------------------------------------------------------------------------

docs/m1/evidence/T-031_donor_reemission_20260806_101714.txt
docs/m1/evidence/T-030_staging_reset_20260806_101847.txt
docs/m1/evidence/T-031_certification_20260806_102449.txt
docs/m1/evidence/T-031_certification_20260806_102942.txt

scripts/demo/Reemit-Fleetv2Donor.ps1
scripts/demo/Reset-PresentationStaging.ps1
tools/run/Invoke-PpiqT031Certification.ps1

NEXT: M1-P5.
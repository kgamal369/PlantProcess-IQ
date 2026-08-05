# T-027 STATUS - Populate the manifest and prove every phenomenon

Milestone / Phase : M1 / M1-P1b
Recorded          : 2026-08-05
Backlog authority : PPIQ_Backlog_v2.9.1_03Aug2026 (FROZEN)

--------------------------------------------------------------------------------
STATUS
--------------------------------------------------------------------------------

  T-027 implementation / execution   COMPLETE
  T-027 acceptance                   BLOCKED

  BLOCKER: the historical predeclaration and the generator coverage do not
  provide an executable phenomenon compatible with the current measurement
  contract.

No false PASS is claimed. The frozen validation contains execution clauses that
cannot be honestly satisfied from the authoritative inputs, and satisfying them
would require manufacturing the exact evidence the task forbids.

--------------------------------------------------------------------------------
THE AUTHORITATIVE MEASURED STATE
--------------------------------------------------------------------------------

Read-only run 2026-08-05 15:02:09. The available-identifier set was QUERIED from
parameter_definitions and information_schema - 141 identifiers, of which 48 are
parameter codes - never typed by hand.

  matrix rows accounted for                   104 / 104
  DECLARATION_NOT_NUMERIC                      24
  SOURCE_VARIABLE_NOT_MATERIALISED             34
  GENERATOR_SCOPE_NOT_IMPLEMENTED              46
  genuinely executable predeclarations          0
  charts backed by proven phenomena             0 / 36
  negative controls measured as silent          0 / 17

  declared statistic, as originally written: 78 unclassified, 9 rate ratio,
  7 threshold membership, 4 FDR significance, 3 variance comparison,
  3 step change.

--------------------------------------------------------------------------------
WHY ACCEPTANCE IS BLOCKED, CLAUSE BY CLAUSE
--------------------------------------------------------------------------------

The frozen validation reads: "Every phenomenon in the matrix has a manifest row
and a result. The reopened parts one and two of the coverage matrix are rewritten
against measured reality and closed. Every one of the 36 charts references at
least one phenomenon that the harness proves. Every negative control is silent."

  SATISFIED   every phenomenon has a row and a result. A refusal or a gap IS a
              result for coverage purposes, and all 104 rows carry an explicit
              classification and reason.

  BLOCKED     "every one of the 36 charts references at least one phenomenon that
              the harness proves". Nothing is proven, so no chart qualifies.

  BLOCKED     "every negative control is silent". Silence is a MEASURED property.
              All 17 controls classify as non-executable, so none has been shown
              silent. Recording them as silent would manufacture evidence.

--------------------------------------------------------------------------------
WHAT THE ZERO ACTUALLY MEANS
--------------------------------------------------------------------------------

This is NOT a harness failure. The harness is proven able to return PASS, FAIL,
INSUFFICIENT and a correlating-negative-control failure - see T-026_CLOSURE.md.

81 of the 104 rows fail on VARIABLES before any statistic is reached. The STRONG
class is built on superheat_c, casting_speed_m_min and tundish_age_min - caster
variables - and no caster variable exists in the canonical parameter set. The
matrix was declared against a plant richer than the one that was generated.

Because variables fail first, MEASURE_UNSUPPORTED never fires. The statistic
mismatch is real - 9 rate-ratio declarations cannot be expressed as a Spearman
band - but it is not the binding constraint.

--------------------------------------------------------------------------------
WHAT WAS DELIBERATELY NOT DONE
--------------------------------------------------------------------------------

  - No band was retrofitted from observed Fleet v2 results.
  - No rate ratio was reinterpreted as a Spearman coefficient.
  - No prose assertion was converted into a numeric band after the data existed.
  - The frozen eight manifest columns were not widened.
  - The generator was not modified from T-027.
  - T-026 was not reopened.
  - The three T-026 manifest entries are HARNESS DEMONSTRATIONS and are labelled
    as such. Two of their bands were drawn around measured values, so they prove
    that the harness returns a verdict and nothing about the plant.

--------------------------------------------------------------------------------
FOLLOW-UP REQUIREMENT
--------------------------------------------------------------------------------

A numeric or statistical predeclaration - the statistic, its expected direction
and its acceptable band - must be authored BEFORE the next independent validation
population is generated or observed. A band written after the data exists cannot
be labelled a T-008 or T-015 predeclaration and must never be presented as one.

--------------------------------------------------------------------------------
EVIDENCE
--------------------------------------------------------------------------------

  docs/m1/phenomena/T-027_coverage_ledger.csv
  docs/m1/evidence/T-027_coverage_report_20260805_150209.txt
  docs/m1/evidence/T-027_ledger_run_20260805_150209.txt
  Backend/tools/t027_coverage_ledger.py
  tools/run/Invoke-PpiqT027Ledger.ps1

This blocker does not block the remaining M1-P1b database work.
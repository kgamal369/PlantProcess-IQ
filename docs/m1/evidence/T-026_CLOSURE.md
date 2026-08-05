# T-026 CLOSURE RECORD - Phenomenon test harness: manifest schema and runner

Milestone / Phase : M1 / M1-P1b
Database          : ppiq_presentation
Closed            : 2026-08-05
Backlog authority : PPIQ_Backlog_v2.9.1_03Aug2026 (FROZEN)

--------------------------------------------------------------------------------
1. WHAT THE TASK REQUIRED
--------------------------------------------------------------------------------

Build the harness rather than hand-prove roughly 104 phenomena. Manifest columns:
phenomenon_id, population_query, expected_direction, minimum_population,
expected_effect_band, conditioning_variable, expected_after_conditioning,
negative_control. The runner walks the manifest and reports per row: population
met or not, direction matched, effect inside its band, conditioned result inside
its band, and negative control silent. A NEGATIVE CONTROL THAT STARTS CORRELATING
IS A FAILURE, NOT A CURIOSITY.

Validation: the harness runs against a manifest of at least three hand-checked
phenomena and produces a pass, a fail and a refusal, all three demonstrated. A
phenomenon whose population is below its minimum reports INSUFFICIENT rather than
passing quietly. The runner exits non-zero if any row fails.

--------------------------------------------------------------------------------
2. VALIDATION, MEASURED
--------------------------------------------------------------------------------

Run 2026-08-05 14:05:07, evidence T-026_harness_20260805_140507.txt

  phenomenon_id                              verdict       population   effect   conditioned
  PPIQ-COIL-COILING-TEMP-DEFECTS             PASS               17010  -0.1035        0.0004
  PPIQ-DOWNTIME-IMPACT-SCALES-WITH-STOPPAGE  FAIL                 630  -0.0257             -
  PPIQ-SENSOR-ARTEFACT-COILING-TEMP          INSUFFICIENT         117        -             -

  a pass, a fail and a refusal            ALL THREE DEMONSTRATED
  population below minimum reports        INSUFFICIENT, not a quiet pass
  runner exits non-zero when a row fails  exit 1

The self-test additionally forces every verdict the engine can produce, on
fixtures with no database: PASS, FAIL, INSUFFICIENT, a correlating negative
control, and an undefined statistic. Each is asserted against its declared
expected verdict, so a harness that could not produce a FAIL would fail its own
self-test rather than quietly making every future manifest meaningless.

--------------------------------------------------------------------------------
3. THE FINDING THIS RUN PRODUCED
--------------------------------------------------------------------------------

THE COILING-TEMPERATURE EFFECT IS CONFOUNDING, NOT A PROCESS RELATIONSHIP.

Across the pooled fleet, mean CT_C per coil against catalogued defect count gives
Spearman -0.1035 over 17,010 coils. At that population the standard error of rho
is about 0.0077, so the pooled effect is roughly thirteen standard errors from
zero and is certainly not noise.

Conditioned on grade_or_recipe - six strata of roughly 2,800 coils each - the
same effect is 0.0004. It disappears entirely. Grades differ both in their
coiling temperature and in their defect rate, and correlating across the pooled
population measures that mix rather than any process effect. Within any single
grade there is no relationship at all.

The row is a correct PASS against what it declared, and it is NOT a process
phenomenon. Both statements are true and neither cancels the other.

THE CONSEQUENCE FOR T-027, WHICH OWNS MANIFEST POPULATION: a raw fleet-level
correlation is not a phenomenon until it survives conditioning. A manifest
populated from pooled correlations would be populated with grade-mix artefacts
that pass their bands and mean nothing.

This is what conditioning_variable exists for, and it earned its place on the
first row it was pointed at.

--------------------------------------------------------------------------------
4. THE THREE SEEDS, AND WHY EACH BAND SAYS WHAT IT SAYS
--------------------------------------------------------------------------------

Hand-checked means MEASURED FIRST and declared afterwards. A band declared before
measurement and then passed proves nothing, because the band was fitted to an
expectation rather than to the plant. Every band below was written around a
measured value with room either side, never fitted tight to it.

4.1 PPIQ-COIL-COILING-TEMP-DEFECTS - expects PASS
    direction negative, minimum_population 5000 against an actual 17,010,
    band -0.1600..-0.0500 around a measured -0.1035.
    expected_after_conditioning -0.0500..0.0500 around a measured 0.0004. That
    band is roughly six standard errors wide either side, and declaring it turns
    the confounding into a re-measured expectation: if a later fleet change gives
    coiling temperature a genuine within-grade effect, this row starts failing,
    and that is the signal.

4.2 PPIQ-DOWNTIME-IMPACT-SCALES-WITH-STOPPAGE - expects FAIL
    The expectation is the natural engineering one, written before looking: a
    longer stoppage should cost proportionally more production. direction
    positive, band 0.4000..1.0000. Measured -0.0257 over 630 events, with
    stopped_minutes spanning 3.70 to 89.38 and production_impact_minutes 0.00 to
    294.80, and zero rows where the two are identical. They are genuinely
    different quantities and genuinely unrelated, exactly as T-018 recorded.
    This FAIL is honest, not manufactured, and it documents a real property of
    the fleet.

4.3 PPIQ-SENSOR-ARTEFACT-COILING-TEMP - expects INSUFFICIENT
    117 coils carry a SENSOR_ARTEFACT. Detecting an effect the size of the
    fleet-level magnitude of about 0.10, at 80 percent power and alpha 0.05,
    needs roughly 780 pairs. minimum_population 780 therefore comes from the
    power requirement, not from a number chosen to force a refusal. The harness
    refuses to judge rather than reporting a meaningless coefficient.

--------------------------------------------------------------------------------
5. THE CONTRACT AS BUILT
--------------------------------------------------------------------------------

FROZEN COLUMNS. The eight named in the backlog, and no others. A ninth column is
rejected rather than absorbed, because widening the contract needs a ruling.

RESULT SHAPE. Every population_query returns a column x and a column y, plus a
column named exactly the conditioning_variable when one is set. Rows with a null
x or y are dropped BEFORE the population is counted, so a query returning ten
thousand nulls reports INSUFFICIENT rather than passing on volume.

EFFECT MEASURE. Spearman over average ranks. One measure for every row, because
the frozen columns cannot declare a method. A constant x or y yields an undefined
statistic and is reported as undefined, never as an effect of zero. The
conditioned effect is the population-weighted mean of the within-stratum effect,
skipping strata below eight pairs rather than letting noise dominate the average.
The candidate scan computes the identical statistic in SQL, so a band declared
from a scan means the same thing when the harness re-measures it.

VERDICTS. PASS, FAIL, INSUFFICIENT, ERROR. Exit 1 on any FAIL or ERROR.
INSUFFICIENT does not fail the run - it is a refusal to judge - but it is counted
and printed separately and can never be mistaken for a pass.

READ ONLY, PROVEN NOT ASSERTED. The connection sets
default_transaction_read_only, and the runner asks the server SHOW
transaction_read_only and stops with exit 2 unless the answer is on. An
environment variable the server ignored would otherwise leave every manifest
query running writable while the banner claimed otherwise.

--------------------------------------------------------------------------------
6. DEFECTS FOUND IN THIS TASK, INCLUDING MY OWN
--------------------------------------------------------------------------------

6.1 The first runner assigned to $pid, which is an automatic read-only variable
    in PowerShell holding the process ID. The throw skipped the assignment of
    $code, so the two exit lines then threw on an undefined variable and buried
    the real error under two false ones. Renamed; $code now initialises to 3,
    meaning "did not reach a verdict", before the try, so an abort can never
    exit 0. Every delivered script was swept for assignments to the automatic
    variable list.

6.2 The first runner wrapped each query in BEGIN READ ONLY ... COMMIT. psql
    prints a command tag per statement, so the word BEGIN became the first line
    of the CSV and therefore the header row, and all three phenomena returned
    ERROR. Read-only moved to the connection, which emits nothing.

6.3 The candidate scan tested 14 parameters, not 26. The twelve chemistry and
    steelmaking parameters - carbon, manganese, sulphur, phosphorus, silicon,
    aluminium, tap temp, LF final temp, argon, calcium, oxygen, power - sit on
    HEAT material units, and the scan joined parameter_observations directly to
    coils, so they never entered it. Given section 3, this matters more than it
    first appeared: heat-level chemistry is where a within-grade metallurgical
    effect would live, and the only pooled survivor turned out to be confounded.
    Widening the search across the genealogy join belongs to T-027.

--------------------------------------------------------------------------------
7. KNOWN LIMITS CARRIED FORWARD TO T-027
--------------------------------------------------------------------------------

  - Campaign ageing and defect positioning CANNOT be expressed as a
    population_query against canonical. Verified against the full 147-column
    inventory: material_units has no campaign key and quality_events has no
    defect position. They are donor-only concepts. If a predeclared phenomenon
    needs them, that is a ruling to raise, not a query to write.
  - product_family has a single level and is useless as a conditioning variable.
    grade_or_recipe has six, downtime reason_code five, downtime_type four,
    quality_events severity three.
  - Heat-grain parameters are unscanned, per 6.3.
  - A pooled correlation is a candidate, not a phenomenon, per section 3.

--------------------------------------------------------------------------------
8. FILES
--------------------------------------------------------------------------------

  Backend/tools/phenomenon_harness.py
  tools/run/Invoke-PpiqPhenomenonHarness.ps1
  tools/run/Invoke-PpiqT026CandidateScan.ps1
  docs/m1/phenomena/manifest.csv

  docs/m1/evidence/T-026_candidate_scan_20260805_135708.txt
  docs/m1/evidence/T-026_harness_20260805_140507.txt
  docs/m1/evidence/T-026_harness_20260805_140507.json

--------------------------------------------------------------------------------
9. STATUS
--------------------------------------------------------------------------------

T-026 = DONE.

The harness exists, it is proven able to produce every verdict it can report, and
its first real run produced a finding about the fleet rather than a green tick.
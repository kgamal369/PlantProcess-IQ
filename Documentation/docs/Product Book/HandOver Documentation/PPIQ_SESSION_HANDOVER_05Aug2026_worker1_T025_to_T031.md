# PPIQ SESSION HANDOVER - 05 August 2026 - worker 2 (database / M1-P1b and M1-P2 data track)

Written for the NEXT session so it starts from here, not from zero.
Repository : C:\Workspace\PlantProcess-IQ   (github kgamal369/PlantProcess-IQ)
Database   : ppiq_presentation on 127.0.0.1:5432, user ppiq_dev
Backlog    : PPIQ_Backlog_v2.9.1_03Aug2026.md / .xlsx  (FROZEN - the only scope)

================================================================================
SECTION 0. READ THIS FIRST - HOW TO USE THIS FILE
================================================================================

READ ORDER FOR A FRESH SESSION:
  1. Section 7  - the rules and rulings. They govern everything else.
  2. Section 8  - backlog status. Where we are.
  3. Section 6  - every test already run. DO NOT RE-RUN THESE.
  4. Section 5  - per-task discoveries, tips and traps.
  5. Sections 1-4, 9, 10 as needed.

WHAT THIS SESSION WAS. It began as a continuation of the worker-1 T-025
handover and ran through T-025 to T-031. It is a DATABASE / DATA track. A
parallel worker owns the M1-P2 frontend authoring shell (T-032, T-033).

THE SINGLE MOST USEFUL THING IN THIS FILE is section 5.9, the recurring defect
classes. Six times in one session an assertion of mine named the right idea in
the wrong shape and produced a wrong verdict. Read it before writing any check.

WHAT IS NOT IN THIS FILE, STATED SO NOBODY LOOKS FOR IT:
  - No deployment, server or pipeline work was performed this session. What
    section 9 and 10 contain is INHERITED KNOWLEDGE and AUDIT OBSERVATIONS, not
    work done here. Anything presented as "modifications made to turn the
    pipeline green" would be invented. See section 9.0.

================================================================================
SECTION 1. WHAT HAPPENED, IN ORDER, WITH THE FINDINGS AT EACH STEP
================================================================================

1.1 THE STARTING POSITION
--------------------------------------------------------------------------------
Inherited from worker 1's handover: T-025 feature store CLOSED and frozen
(505,680 feature values, 21,649 outcome values, full lineage, row-level
reproducibility zero both directions, NOT NULL enforced). Four analysis engines
outstanding: correlation, risk, learning, readiness.

The handover proposed a permission-matrix fix and a driver. Both needed
correction before use - see 1.2.

1.2 T-025a - THE RISK ROUTE. The handover's diagnosis was WRONG.
--------------------------------------------------------------------------------
It said POST /api/analytics/risk-scores/calculate-all returns 403 because
/api/analytics/risk-scores has no permission-matrix entry, and proposed adding
that prefix.

MEASURED FROM SOURCE:
  - That route DOES NOT EXIST anywhere in the solution.
  - The real batch route is POST /risk-scores/calculate-all
    RiskScoreEndpoints.cs:18  MapGroup("/risk-scores")
    RiskScoreEndpoints.cs:29  MapPost("/calculate-all")
    Program.cs:969            app.MapRiskScoreEndpoints()
  - RiskEvidenceEndpoints.cs:18 DOES declare MapGroup("/api/analytics/risk-scores")
    but MapRiskEvidenceEndpoints IS NEVER CALLED in Program.cs. Dead code.

So the proposed entry would have mapped a prefix serving nothing and risk would
still have returned 403.

CORRECT FIX, APPLIED: add ("/risk-scores", All(), "analysis.execute", false) to
the PlantAccessControl Matrix. AccessControlMiddleware matches LONGEST PREFIX
FIRST and denies by default.

A SECOND GATE THE HANDOVER MISSED: /risk-scores also carries
.RequireLicenseFeature(LicenseFeature.RiskDashboardView), which is Pro tier. A
403 AFTER the matrix fix means the licence, not the matrix. The two are
distinguished by the response body.

SECURITY OBSERVATION, RECORDED NOT ACTED ON: the matrix entry
("/", GET, anonymous, true) means EVERY unmapped GET in the API is served
without a token. The M1-21 comment in the file already acknowledges this. Five
/api/analytics/* groups (risk-calibration, risk-scores, read-models, advanced,
simple) and /api/ml/providers are unmapped and in the same position.

1.3 T-025 ENGINE RUN - engines reachable, analysis layer still empty
--------------------------------------------------------------------------------
Risk went 403 -> 200: 500 scores calculated and stored in 28 s.
Correlation reached the engine on all 8 outcome definitions.
Learning: 4 jobs catalogued, 0 enabled = NOT CONFIGURED.
BUT ZERO FINDINGS. 7 of 8 outcomes "Blocked by the data-readiness gate".

TWO DEFECTS OF MINE IN THAT DRIVER:
  - The gate was `if (results > 0 -or refusals > 0) { PASS }` - a universal
    refusal passed a task whose purpose is to populate findings.
  - The correlation-results check was `count(*) AS found, count(*) AS required`
    - a tautology that can never fail.

1.4 THE READINESS DIAGNOSIS - two passes, and my first hypothesis was wrong
--------------------------------------------------------------------------------
PASS A found: 5 of 8 outcome definitions have ZERO ROWS. Only defect.class
(7,844), defect.rate_per_m2 (7,844) and defect.severity (5,961) are
materialised, all at grain coil.

Traced to source: 740 and 741 produce class and severity; 201 produces
rate_per_m2. THE OTHER FIVE HAVE NO PRODUCER IN THE REFRESH PATH AT ALL. They
appear only in 204_phase04_phase05_ml_learning_core.sql, built from a synthetic
series (gs % 5, gs % 22) - a demo fixture, not the canonical population.

SO THE PRODUCT PRINTED "honest abstain" FIVE TIMES FOR OUTCOMES IT COULD NEVER
COMPUTE. That message makes a missing pipeline look like statistical integrity.

Pass A also KILLED my alignment hypothesis: 4,528 outcome sample keys, all 4,528
present on the feature side, zero orphans, every feature offering 7,244-7,844
aligned pairs. MinPairs is 8. Alignment was never the problem.

PASS B found the three real producer defects - see 1.5.

MY DIAGNOSTIC GAP: pass A measured numeric_pairs on the FEATURE side only and
never checked whether the OUTCOME rows carry values. That is why a second pass
was needed.

1.5 THE THREE PRODUCER DEFECTS (all in 741 / the live base function)
--------------------------------------------------------------------------------
D1  defect.rate_per_m2 IS THE LITERAL 1.0. The insert hardcodes
    `qe.event_at_utc, 1.0,` and leaves normalization_denominator NULL. Measured
    min 1 / max 1 across 7,844 rows. A CONSTANT OUTCOME makes every correlation
    undefined - that is the 26-excluded / 0-findings result.
    MY HYPOTHESIS THAT THE VALUES WERE NULL WAS REFUTED. They were present and
    all identical.

D2  defect.severity WRITES TO A COLUMN THE LOADER NEVER READS. The producer put
    qe.severity into severity_value; NpgsqlFeatureVectorLoader reads only
    numeric_value and category_value. Measured 0 and 0 non-null across 5,961
    rows. One empty class makes MinorityFraction hit its `g.Count < 2 -> 0.0`
    branch, below the 0.03 floor.

D3  defect.class IS CONTAMINATED. COALESCE(dc.defect_code, dc.defect_category,
    qe.event_type) lets an event with no catalogue fall through to its event
    type. 1,883 of 7,844 rows carried the class "Disposition". Removing them put
    SCALE at 1,550 of 5,961 = 26.002 percent, the declared T-015 share to two
    decimals - which is how we knew the correction was right.

1.6 D4 - THE SCHEMA-VERSUS-PRODUCER CONTRACT BREAK
--------------------------------------------------------------------------------
After T-025b was applied, BOTH refreshes failed:
  23502: null value in column "refresh_run_id" of relation "ml_feature_values"

ROOT CAUSE: the producers INSERT value rows WITHOUT lineage and a LATER UPDATE
stamps them. 741's body contains no refresh_run_id at all. Section C of the
09:46 closure run then enforced refresh_run_id NOT NULL and printed "FEATURE
STORE IS CLOSED" - and THE LAST SUCCESSFUL REFRESH WAS SECTION A OF THAT SAME
RUN, BEFORE section C added the constraint. From that moment PostgreSQL rejected
the row before the stamp could run.

BOTH ATTEMPTS ROLLED BACK ATOMICALLY, so the refresh budget was NOT spent.

FIX (T-025c): supply refresh_run_id at INSERT time in all five live value
INSERTs - three in the base using v_run_id, two in v6 using v_base.run_id.
THE PROOF OF INSERT-TIME OWNERSHIP IS THE REFRESH SUCCEEDING AT ALL under NOT
NULL. No row that was not owned at insert could survive the constraint.

1.7 THE CORRECTIVE RUN - it worked, and then the real limit appeared
--------------------------------------------------------------------------------
Refresh A 200 in 81.5 s, refresh B 200 in 67 s. Reproducibility A EXCEPT B = 0
and B EXCEPT A = 0 over 517,602 rows. Every prediction I stated in advance
landed: Disposition 0, SCALE 26.002 percent, defect.class minority 1.996
percent, severity three levels at 28.552 / 31.773 / 39.675.

AND STILL ZERO FINDINGS. defect.severity cleared the readiness gate and then
excluded all 26 parameters.

1.8 THE DECISIVE FINDING - THE METHOD MATRIX HAS NO NUMERIC x CATEGORICAL TEST
--------------------------------------------------------------------------------
Backend/PlantProcess.Analytics.Core/Methods/MethodSelector.cs covers exactly:
    Numeric     x Numeric      -> Spearman, or MutualInformation if nonlinear
    Binary      x Numeric      -> PointBiserial
    Categorical x Categorical  -> CramersV
    anything else              -> NotApplicable, IsApplicable = false

MapOutcome sends BOTH multinomial AND ordinal to VariableType.Categorical. Every
materialised outcome is Categorical and all 26 features are Numeric. Numeric x
Categorical has no entry, so every parameter is excluded.

NO PAIRING AVAILABLE IN THE CURRENT CANONICAL POPULATION CAN PRODUCE A FINDING,
REGARDLESS OF READINESS. The missing test is the first one an industrial user
asks - does tap temperature relate to defect severity - i.e. one-way ANOVA,
eta-squared or Kruskal-Wallis.

AND THE RECORDED REASON IS MISLEADING:
    method = NotApplicable
    reason = "Undefined statistic (constant / zero-variance input)."
Measure returns NaN for an unsupported pairing exactly as for a constant input,
and the exclusion path attributes every NaN to zero variance. The parameters
carry 1,096-4,528 distinct values, so zero variance is demonstrably false. The
ENGINE IS BLAMING THE DATA FOR A MISSING METHOD.

Owner: T216 rigorous statistics. Recorded in
docs/m1/evidence/T216_capability_gap_numeric_x_categorical.md with BOTH defects
- the missing method and the mislabelled reason.

1.9 T-025d - THE DURABILITY DEFECT
--------------------------------------------------------------------------------
tools/packs/ IS GITIGNORED. tools/run/ is not. So every pack this session was an
execution vehicle only. That is fine for packs whose product is a tracked source
file, and NOT fine for packs that changed live database objects.

FOUR THINGS WERE MISSING FROM THE REPLAY CHAIN:
  - refresh_run_id column, FKs, indexes, engine_key/engine_version: lived only
    in tools/run/Invoke-PpiqT025LineageMigration.ps1 - tracked, but a PowerShell
    runner, NOT part of the replay
  - the corrected base producer - live DB only
  - the corrected v6 producer - live DB only
  - refresh_run_id NOT NULL - live DB only

refresh_run_id appeared in ZERO tracked .sql files, while
scripts/demo/Rebuild-PresentationDb.ps1 re-applies 741 on every rebuild (its own
comment: "or the engine re-blinds on every rebuild"). A fresh replay converged on
the OLD semantics against a table with no lineage column.

FIX: Backend/database/scripts/760_t025_lineage_and_outcome_producer.sql now owns
the whole contract, with both function bodies captured VERBATIM from
pg_get_functiondef so parity is by construction. Registered in both replay paths.
The PowerShell runner delegates to it instead of carrying its own SQL.

1.10 THE FALSE ALARM I RAISED, AND WITHDREW
--------------------------------------------------------------------------------
The T-025d parity line said "event_type fallback live 1 / source 1" and I
reported a v6 defect-taxonomy fallback as an executable server regression.

IT WAS WRONG. The single qe.event_type in v6 is
    LEFT JOIN public.quality_events qe ON ... AND lower(qe.event_type) = 'defect'
a JOIN FILTER restricting to defect events - the OPPOSITE of a fallback. My
detector was `qe\.event_type\s*\)` which matched the closing paren of lower().

T-025e was CANCELLED before applying anything. The pack failed closed on its own
anchor check, which is the only reason nothing was damaged. Nothing committed
ever carried the false claim (verified by git grep).

LESSON: I reported a conclusion from a COUNT without ever printing the matched
TEXT. One -ReportOnly would have shown it.

1.11 T-026 THROUGH T-031
--------------------------------------------------------------------------------
Covered per-task in sections 5 and 8. Headlines:
  T-026 harness built and self-proven. Its first real run found that the CT_C
        association is CONFOUNDED by grade (-0.1035 pooled -> 0.0004 within).
  T-027 104/104 phenomena classified, ZERO executable. Acceptance BLOCKED.
  T-028 both required outputs produced. The confound survives thickness
        stratification and dies on grade - which is what makes it a confound.
  T-029 3 layers + the weight guard PASS; 2 generator findings; density check
        NOT COMPUTABLE. Acceptance BLOCKED.
  T-030 staging verified source-shaped and unprepared; identities match;
        staging is EXACTLY one third of canonical. Acceptance BLOCKED on the
        frontend surfaces clause only.
  T-031 STOPPED before any deletion. dump_store holds a DIFFERENT PLANT.

================================================================================
SECTION 2. CURRENT IMPLEMENTATION AND EVERY MODIFICATION MADE THIS SESSION
================================================================================

2.1 SOURCE CHANGES THAT ARE TRACKED AND COMMITTED
--------------------------------------------------------------------------------
Backend/PlantProcess.Api/Security/PlantAccessControl.cs
    ADDED one matrix entry with a 12-line comment:
        ("/risk-scores", All(), "analysis.execute", false)
    Effects: POST /risk-scores/calculate-all now reaches the engine; and the GET
    routes in that group no longer fall through the anonymous ("/", GET) entry.

Backend/database/scripts/760_t025_lineage_and_outcome_producer.sql   NEW
    THE AUTHORITATIVE TRACKED DEFINITION of the complete T-025 durability state:
      - refresh_run_id uuid on ml_feature_values and ml_outcome_values
        (ADD COLUMN IF NOT EXISTS)
      - engine_key / engine_version on ml_feature_store_refresh_runs
      - fk_ml_feature_values_refresh_run, fk_ml_outcome_values_refresh_run
      - ix_ml_feature_values_refresh_run_id, ix_ml_outcome_values_refresh_run_id
      - CREATE OR REPLACE of BOTH producers, captured verbatim from
        pg_get_functiondef of the proven live functions
      - a DO block that verifies ZERO rows with refresh_run_id IS NULL and then
        enforces NOT NULL - RAISING rather than backfilling if any row lacks it
    Carries NO BEGIN/COMMIT so it composes with psql -1 in the rebuild script and
    with the autocommit server replay without warnings.
    Documents which earlier definitions it supersedes: 200, 201, 740, 741.

scripts/demo/Rebuild-PresentationDb.ps1
    760 appended to the named migration list applied after 741/742/750.

deploy/server/apply-server-db-scripts.sh
    760 appended to the ordered script list after 203.

tools/run/Invoke-PpiqT025LineageMigration.ps1
    Its self-assembled DDL block replaced by a read of 760. The runner keeps its
    command surface and becomes an execution vehicle rather than a second
    independent definition of the same contract.

docs/m1/evidence/T-025_CLOSURE.md
    Evidence list corrected: the three gitignored tools/packs entries replaced by
    760, PlantAccessControl.cs and the tracked runners, plus a note that packs
    were execution vehicles.

Backend/tools/phenomenon_harness.py                          NEW  (T-026)
Backend/tools/t027_coverage_ledger.py                        NEW  (T-027)
docs/m1/phenomena/manifest.csv                               NEW  (T-026)
docs/m1/phenomena/T-027_coverage_ledger.csv                  NEW  (T-027)

2.2 LIVE DATABASE CHANGES (all now mirrored in tracked source via 760)
--------------------------------------------------------------------------------
public.ppiq_ml_refresh_feature_store        - corrected, 3 value INSERTs own lineage
public.ppiq_ml_refresh_feature_store_v6     - corrected, 2 value INSERTs own lineage
    defect.class    : qe.event_type fallback REMOVED from the taxonomy COALESCE
    defect.severity : qe.severity now ALSO written to category_value
                      (severity_value preserved - the check requires BOTH)
    defect.rate_per_m2 : the whole false INSERT block REMOVED
    every value INSERT: refresh_run_id supplied at row creation

ml_correlation_results_v2 - 52 historical result rows quarantined (deleted);
    466 compute runs preserved as history.

NOTHING ELSE in the database was changed by this session.

2.3 RUNNERS DELIVERED (tools/run/, tracked)
--------------------------------------------------------------------------------
Invoke-PpiqT025Engines.ps1              engines only, no feature-store refresh
Invoke-PpiqT025Readiness.ps1            readiness diagnosis part A
Invoke-PpiqT025ReadinessB.ps1           part B, two hypotheses with refutation
Invoke-PpiqT025Corrective-v2.ps1        refresh A, verify, correlate, refresh B
Invoke-PpiqT025ExclusionReasons.ps1     the exclusion-reason readout
Invoke-PpiqPhenomenonHarness.ps1        T-026 runner + -SelfTest + -Describe
Invoke-PpiqT026CandidateScan.ps1        measurement before declaration
Invoke-PpiqT027Ledger.ps1               104-row coverage ledger
Invoke-PpiqT028Verification.ps1         confound + insufficient-support refusal
Invoke-PpiqT029RealismAudit-v2.ps1      five layers + trigger exercise
Invoke-PpiqT029RangeBreakdown.ps1       who breaches their declared range
Invoke-PpiqT030StagingVerification.ps1  source-shaped staging verification
Invoke-PpiqT031LayerAndDependencyCheck.ps1   which layer is live
Invoke-PpiqT031DependencyCheckB.ps1          the dependencies that decide deletion

2.4 PACKS DELIVERED (tools/packs/ - GITIGNORED, execution vehicles only)
--------------------------------------------------------------------------------
apply-T-025a-risk-matrix-entry.ps1
apply-T-025b-outcome-producer-correction-v2.ps1
apply-T-025c-insert-time-lineage.ps1
apply-T-025d-durability.ps1
apply-T-025e-v6-eventtype-hotfix.ps1        (CANCELLED - never applied)
apply-T-025-closure-record.ps1
apply-T-024-req8-deferral-record.ps1
apply-T-026-closure-record.ps1
apply-T-027-status-record.ps1
apply-T-028-closure-record.ps1
apply-T-029-closure-record.ps1
apply-T-030-closure-record.ps1

REMEMBER: because tools/packs is gitignored, a pack's WORK must land in a tracked
artifact or it is not durable. That is his new standing rule - section 7.

2.5 COMMITS THIS SESSION
--------------------------------------------------------------------------------
67c7395e  T-025 closure record and T216 numeric x categorical capability gap
8a09e2fb  T-024 requirement 8 recorded as DEFERRED
e270b40d  T-026 phenomenon harness: schema, runner, 3 seeds, closure
8c6d5f6e  T-025d durability: 760 becomes the tracked authority
b5faf96c  T-027: 104-row coverage ledger; complete, acceptance blocked
45dfc0f9  T-028: confounded association proven on grade; refusal named
1ac877db  T-029 five-layer realism audit
cb72a3de  T-030: staging verified; 3:1 population ratio recorded

================================================================================
SECTION 3. IDENTITY, TOPOLOGY AND ROADMAP - WHERE WE STARTED AND HOW FAR WE GOT
================================================================================

3.1 PRODUCT IDENTITY (unchanged, carried forward)
--------------------------------------------------------------------------------
PlantProcess IQ: read-only, evidence-grade, INDUSTRY-AGNOSTIC process-to-quality
intelligence for manufacturing plants, ~EUR 100k per customer. Correlation plus
AI is the differentiator against Primetals TPQC, PSI Metals Quality, Smart Steel
Technologies and Fero Labs.

THREE PRODUCT RULES (permanent doctrine):
  Rule 1  Generic Only    - no demo content in the product
  Rule 2  Starts Empty    - all data via DB-link import only
  Rule 3  the 15-step canonical journey IS the product

DEMO-VS-PRODUCT DOCTRINE: the app is always generic; the demo is the real app
running on EMULATED EXTERNAL SOURCE DATA. That is why src_* exists at all.

3.2 DATA TOPOLOGY AS IT NOW STANDS (measured this session, not assumed)
--------------------------------------------------------------------------------
    Fleet v2 generator (Backend/tools/generate_fleet_v2_donor.py)
        |
        v
    src_* schemas  - 5 schemas, 10 tables, 77,797 rows, ONE-THIRD SCALE
        src_meltshop_pg            heats 630, lf_treatment 630
        src_caster_oracle_shape    cast_sequence 630, cast_pieces 5,670
        src_hsm_oracle_shape       hsm_coils 5,670, hsm_pass_measurements 39,690
        src_pkl_mssql_shape        pickle_orders 5,670, qa_lab_results 17,010
        src_inspection_mysql_shape parsytec_surface_defects 1,987, downtime 210
        |
        |  two-stage delta import (130_phase03), stage 1
        v
    dump_store  - 10 tables, 164,827 rows  ** STALE - A DIFFERENT PLANT **
        last import 2026-07-08, 392 two-stage runs 29-Jun to 08-Jul
        12,147 of its 17,817 coils match NOTHING in current canonical
        |
        |  stage 2 (NOT the path used for the current population)
        v
    public (canonical)  - materialised directly by T-024 from the generator
        35,910 material units  (1,890 Heat / 17,010 Slab / 17,010 Coil)
        301,560 parameter observations, 53,095 process step executions
        34,020 genealogy edges, 7,844 quality events, 630 downtime events
        |
        v
    analysis layer
        505,680 ml_feature_values, 11,922 ml_outcome_values
        ml_correlation_results_v2: 26 exclusion rows, ZERO findings
        risk_scores 500 of 35,910 eligible
        ml_learning_job_catalog_v1: 4 jobs, 0 enabled

OTHER SCHEMAS: acquisition 5 tables ALL EMPTY; canon 16 tables nearly all empty;
ppiq_forensics 1; public 196 tables.

3.3 THE THREE-LAYER PROBLEM T-031 EXISTS TO CATCH, AND IT IS REAL
--------------------------------------------------------------------------------
T-031: "THE CUSTOMER MUST NEVER SEE ONE PLANT IN THE CANVAS AND ANOTHER IN THE
DASHBOARD."

MEASURED: src_* is a consistent 1x subset of canonical (every one of its 5,670
coils resolves). dump_store contains 12,147 coils that exist in NO current layer.
So the divergence T-031's certification is supposed to detect IS ALREADY PRESENT,
and the deliberate injected divergence its validation asks for is not needed to
prove the gate works - the real one is sitting there.

3.4 ROADMAP POSITION AT SESSION END
--------------------------------------------------------------------------------
M1-P1b (database / presentation data): T-013 to T-029.
    T-013 to T-023 were DONE before this session (88 of 114 hours).
    This session took T-025 through T-029.
M1-P2 opens at T-030. This session took T-030 and stopped inside T-031.
The parallel worker owns T-032 and T-033 (frontend authoring shell).
HIS DIRECTION: after T-031, JUMP DIRECTLY TO M1-P5 - do not wait for the
deferred dashboard/browser work on the frontend track.

================================================================================
SECTION 4. REALIZATION SCOREBOARD AT SESSION END
================================================================================

4.1 WHAT IS GENUINELY WORKING AND PROVEN
--------------------------------------------------------------------------------
  Canonical operational population   35,910 units, Fleet v2, identity-consistent
  Feature store                      505,680 + 11,922 values, full lineage
  Insert-time lineage                PROVEN by a refresh succeeding under NOT NULL
  Reproducibility                    A EXCEPT B = 0 both ways over 517,602 rows
  Structural integrity               8 checks, zero offending
  Temporal integrity                 5 checks, zero offending
  Genealogy weight guard             fires by name, rolls back clean
  Source-shaped staging              populated, unprepared, identities match
  Phenomenon harness                 proven able to return every verdict it has
  Durability of the T-025 contract   760 in the tracked replay chain
  Risk engine                        500 real scores, bounded proof

4.2 WHAT IS BLOCKED, AND BY WHAT
--------------------------------------------------------------------------------
  ZERO ANALYTICAL FINDINGS
      Cause: MethodSelector has no Numeric x Categorical test. Owner T216.
      Not a data problem. Not fixable inside a data task.

  T-027 ACCEPTANCE BLOCKED
      104/104 classified, 0 executable, 0/36 charts backed, 0/17 controls
      measured silent. Cause: historical predeclaration is 97 prose assertions
      using statistics the frozen harness does not compute, plus missing or
      differently-named variables.

  T-029 ACCEPTANCE BLOCKED
      PHYSICAL: SUPERHEAT_C goes to -2.712 against a floor of 10 (834 rows);
                CARBON_PCT reaches exactly 0.000 against a floor of 0.010 (332).
      STATISTICAL: 9 parameters drawn uniform-random.
      DENSITY: not computable - LENGTH_MM and WEIGHT_KG not emitted to canonical.
      All three are GENERATOR changes with estimates recorded (1h + 2h + 1h).

  T-030 ACCEPTANCE BLOCKED on the surfaces clause only
      Everything database-verifiable passes.

  T-031 STOPPED before deletion
      dump_store is a stale different plant; precondition 2 is FALSE.

  T-024 REQUIREMENT 8 DEFERRED
      Browser acceptance pending presentation convergence. Not PASS, not FAIL,
      not waived. T-024 therefore does NOT hold a Done status.

4.3 MY HONEST ASSESSMENT AND SUGGESTIONS
--------------------------------------------------------------------------------
THE PRODUCT'S DATA FOUNDATION IS SOUND. Structure, time, genealogy, lineage and
reproducibility all pass on real measurement, not on assertion. That is the
expensive half and it is done.

THE ANALYTICAL HALF IS BLOCKED ON ONE MISSING STATISTICAL METHOD. Adding a
Numeric x Categorical test (one-way ANOVA / eta-squared / Kruskal-Wallis) is the
single highest-value change available to this product right now. Without it, the
correlation engine cannot answer the first question any plant engineer asks. With
it, defect.severity has 5,961 rows, three balanced classes and 26 numeric
parameters waiting.

THE SECOND HIGHEST VALUE IS THE NINE UNIFORM PARAMETERS. A uniform variable has
no natural clustering, so it cannot exhibit a regime, a shift or an outlier - any
phenomenon declared on one of them is UNDISCOVERABLE BY CONSTRUCTION.

THE THIRD IS THE MATRIX VOCABULARY. Some T-027 rows failed on names, not data:
the matrix says casting_speed_m_min, the plant emits CASTING_SPEED_MPM. A naming
reconciliation would move rows out of the gap column cheaply.

WHAT I WOULD NOT DO: chase more findings. Six of my own defects this session were
in the checks, not the code under test. The measurement apparatus is now good;
the remaining work is closing tasks.

================================================================================
SECTION 5. PER-TASK DISCOVERIES, TIPS, TRICKS AND WHAT IS STILL MISSING
================================================================================

5.1 T-025 - ANALYSIS ENGINES
--------------------------------------------------------------------------------
DISCOVERED:
  - The live default correlation engine is DotNetAdvancedCorrelationEngine.
    Analytics:AdvancedEngine:Enabled defaults true. The "managed" keyed
    registration ALSO resolves to it - ManagedStatisticalComputeEngine is not
    what you get.
  - CorrelationComputeRequest(OutcomeKey, Grain, WindowDays, Filters?). An empty
    body returns 400 "Outcome key is required" - that is a MALFORMED REQUEST, not
    a refusal. Do not record it as one.
  - Outcome keys must be READ from ml_outcome_definitions, never hardcoded.
  - ReadinessGate demo thresholds: heats Ready 60 / Partial 30; events 40 / 15;
    minority 0.10 / 0.03; completeness 0.95 / 0.85. Overall = the WORST dimension.
  - MinorityFraction returns 0.5 for a NUMERIC outcome and the smallest category
    share for a CATEGORICAL one, AND returns 0.0 when there are fewer than two
    classes. That last branch is easy to miss and I missed it once.
  - NpgsqlFeatureVectorLoader HARDCODES FreshnessFactor to 0.0, so freshness can
    NEVER block. Completeness measured 1.0. So minority balance is the only
    dimension that can block anything here.
  - IterativeVifExclude returns when fewer than two features remain, so it can
    strip at most n-1. It can NEVER explain "all excluded".
  - NpgsqlAdvancedResultWriter has TWO insert loops into ml_correlation_results_v2.
    The second writes ONE ROW PER EXCLUDED FEATURE with method 'NotApplicable',
    sample_size 0 and evidence_json {"excluded": true}. COUNTING ROWS IN THAT
    TABLE DOES NOT COUNT FINDINGS.

STILL MISSING:
  - Five outcome definitions have no producer: defect.position,
    downtime.cascade_minutes, kpi.energy_per_ton, kpi.prime_yield, kpi.throughput.
  - defect.rate_per_m2 has no honest denominator and is deliberately not
    materialised.
  - The Numeric x Categorical method (T216).

TIP: the base refresh function SELF-CLEARS both value tables at the top
(DELETE ... WHERE source_system = 'PPIQ-ML-Refresh'), so a corrective refresh is
idempotent and needs no manual delete.

5.2 T-026 - PHENOMENON HARNESS
--------------------------------------------------------------------------------
ARCHITECTURE DECISION AND WHY: SQL execution and credentials live in PowerShell
because psql is proven present; the statistics live in Python because Python
syntax can be verified before delivery and PowerShell cannot. The verdict engine
touches no database at all.

MANIFEST COLUMNS ARE FROZEN BY THE BACKLOG (8): phenomenon_id, population_query,
expected_direction, minimum_population, expected_effect_band,
conditioning_variable, expected_after_conditioning, negative_control. The runner
REJECTS a ninth column rather than absorbing it.

RESULT-SHAPE CONTRACT: every population_query returns x and y, plus a column
named exactly the conditioning_variable when one is set. Rows with a null x or y
are dropped BEFORE the population is counted, so ten thousand nulls report
INSUFFICIENT rather than passing on volume.

VERDICTS: PASS / FAIL / INSUFFICIENT / ERROR. Exit 1 on FAIL or ERROR.
INSUFFICIENT does not fail the run but is counted separately.

TWO TRAPS THIS TASK TAUGHT, BOTH COSTLY:
  - $pid IS A POWERSHELL AUTOMATIC READ-ONLY VARIABLE (the process ID).
    Assigning to it throws and, worse, the throw skipped my $code assignment so
    two more false errors buried the real one. $code now initialises to 3
    ("did not reach a verdict") BEFORE the try, so an abort can never exit 0.
  - WRAPPING A QUERY IN BEGIN/COMMIT PUTS THE COMMAND TAG IN THE CSV. psql
    prints a tag per statement, so "BEGIN" became the header row and every
    phenomenon returned ERROR. Read-only now comes from
    PGOPTIONS=-c default_transaction_read_only=on plus psql -q, and the runner
    PROVES it with SHOW transaction_read_only rather than asserting it.

THE THREE SEEDS ARE HARNESS DEMONSTRATIONS, NOT SCIENTIFIC PREDECLARATIONS. Two
of their bands were drawn around measured values. Label them as such wherever
referenced - his explicit ruling.

5.3 T-027 - COVERAGE LEDGER
--------------------------------------------------------------------------------
DISCOVERED:
  - docs/m1/evidence/phenomena_widget_matrix.csv is the T-008/T-015
    predeclaration: 104 rows. Classes 23 STRONG, 25 MODERATE_CONDITIONAL,
    18 REGIME, 17 NEGATIVE_CONTROL, 11 OUTLIER, 10 TEMPORAL. Status 58 EXISTING,
    31 ENRICH, 15 NEW.
  - 97 of 104 assertions are PROSE, not numeric bands. The numeric ones use rate
    ratios, FDR significance, variance comparisons, threshold membership and step
    change - NOT Spearman.
  - The available-identifier set QUERIED from the database is 141 identifiers of
    which 48 parameter codes. MY HAND-WRITTEN LIST WAS WRONG BY 22 CODES, in the
    direction that looks careful. ALWAYS QUERY IT.
  - Test order matters: VARIABLES are checked BEFORE the statistic, because a row
    whose variables do not exist cannot be measured by any statistic. That
    ordering is why zero rows landed in MEASURE_UNSUPPORTED.

STILL MISSING / CORRECTED LATER BY T-029:
  - I stated the canonical parameter set has no caster variable. THAT IS WRONG.
    SUPERHEAT_C, CASTING_SPEED_MPM and MOULD_LEVEL_AVG all exist with 17,010
    observations. The matrix declares casting_speed_m_min; the plant emits
    CASTING_SPEED_MPM. Some SOURCE_VARIABLE_NOT_MATERIALISED rows are VOCABULARY
    MISMATCHES. T-027 is closed and NOT reopened; this is an input to the future
    matrix work.

5.4 T-028 - CONFOUND AND REFUSAL
--------------------------------------------------------------------------------
THE RESULT WORTH REMEMBERING: the CT_C association SURVIVES thickness
stratification (-0.1033 against a naive -0.1035) and DIES on grade (0.0004).
Surviving one conditioning and not the other is what distinguishes a genuine
confound from a fragile effect. Grades differ in BOTH coiling temperature AND
defect rate.

THE BLOCKED OUTCOME, WITH ITS NUMBERS: defect.class blocked on minority-class
balance measured 0.0200 against a Partial threshold of 0.0300, with 1,341
independent heats against a Ready threshold of 60 and 5,961 events against 40.
SUPPORT IS NOT THE CONSTRAINT.

TIP: the engine's own message ("Blocked by the data-readiness gate; analysis
refused (honest abstain)") names NEITHER the value NOR the threshold. Any task
requiring those must recompute the dimensions itself.

DISCOVERED: of the three declared negative controls, DENT and SEAM DO NOT EXIST
in the generated catalogue. SCRATCH exists at 298 events with rho -0.0372 over
17,010 pairs - about five standard errors from zero, so SMALL BUT NOT SILENT.

5.5 T-029 - FIVE-LAYER REALISM AUDIT
--------------------------------------------------------------------------------
DISCOVERED:
  - genealogy_edges columns are parent_material_unit_id, child_material_unit_id,
    contribution_weight, is_transition, provenance_confidence, relationship_type,
    effective_from_utc/to_utc. I GUESSED parent_unit_id / child_unit_id and two
    layers died mid-run.
  - ppiq_genealogy_edge_weight_guard_after_change IS LIVE, defined in
    550_v5_p06_blended_provenance.sql. It raises when contribution weights do not
    sum to 1.0 per child within 0.015.
  - IT IS DEFERRABLE INITIALLY DEFERRED, so it fires at COMMIT and a rolled-back
    transaction NEVER REACHES IT. SET CONSTRAINTS ... IMMEDIATE is the only way to
    confirm firing AND still roll back. An UPDATE of contribution_weight via ctid
    exercises it without needing to know the table's other NOT NULL columns.
  - The uniform-signature test: IQR divided by full range. A uniform variable
    sits at almost exactly 0.50; natural ones sit 0.17 to 0.41.

STILL MISSING:
  - LENGTH_MM and WEIGHT_KG are not emitted to canonical, so the density
    cross-check cannot run. THEY EXIST AT SOURCE - cast_pieces carries length_mm,
    weight_kg, mould_level_avg, casting_speed_avg and superheat_c;
    hsm_coils carries coil_weight_kg populated on all 5,670 rows, 12,508 to
    28,498 kg. Emission estimate 1 hour.
  - Three of the eight declared sources have no schema: a separate Downtime MySQL,
    a Yard file and a QA file. Downtime shares src_inspection_mysql_shape.

5.6 T-030 - SOURCE-SHAPED STAGING
--------------------------------------------------------------------------------
DISCOVERED:
  - 110_phase1_demo_source_shapes.sql already creates the five schemas and ten
    tables. The smallest correct implementation was a VERIFICATION, not a build.
  - "Genuinely unprepared" operationalised as four mechanical tests: no view or
    matview (a view is a pre-join), no generated column, NO FOREIGN KEY FROM A
    SOURCE SCHEMA INTO public (a declared link is a pre-join by another name),
    and no canonical vocabulary BY COLUMN NAME. All four measured zero.
  - Staging is EXACTLY one third of canonical on every shared entity. That is a
    POPULATION difference, not the SHAPE difference the task anticipates. Both
    kinds appear in the same table of numbers and must not be read the same way.

MY OWN ERROR: I gated on the reverse identity direction. The frozen clause is
DIRECTIONAL - "a coil visible HERE is the same coil THERE". Measured in that
direction it is zero offending and the clause PASSES.

5.7 T-031 - CERTIFICATION AND RETIREMENT
--------------------------------------------------------------------------------
DISCOVERED, AND THIS IS THE MOST IMPORTANT THING IN THE SECTION:
  - dump_store holds 17,817 coils. 5,670 match src_* AND canonical. 12,147 MATCH
    NOTHING IN THE CURRENT PLANT. Its last import ran 2026-07-08, a month before
    T-024 replaced canonical on 04-Aug. 392 two-stage runs 29-Jun to 08-Jul.
    dump_store is a faithful accumulation of an OBSOLETE plant.
  - src_* by contrast is a CONSISTENT 1x subset - every one of its 5,670 coils
    resolves to canonical.
  - src_* has live dependents: 6 of 8 connection_profiles name a src_ schema,
    all 10 source_table_dump_registry rows do, and 1 source_dataset_definition.
  - The dump registry maps each source table to its dump table with
    primary_key_columns, last_index_column and last_index_value_text - the delta
    import is INDEX-BASED, so a naive re-run APPENDS rather than replaces.
  - Objects mentioning src_: ppiq_run_stage2_canonical_refresh, three
    v_phase1_* views and three v_phase3_dump_* views. CAUTION: my pattern cannot
    tell a reader of SCHEMA src_meltshop_pg from a reader of TABLE
    dump_store.src_meltshop_pg_heats, because the dump tables carry src_ inside
    their names. The v_phase3_dump_* ones are almost certainly dump readers. A
    schema-qualified pattern is needed before trusting that list.

STILL MISSING - THE FOUR RETIREMENT PRECONDITIONS, NONE EVIDENCED:
  1 the generator reproduces the captured baseline on all nine dimensions
  2 both presentation representations regenerated from it   <-- FALSE TODAY
  3 this certification passed                               <-- test not written
  4 one backup taken AND RESTORED SUCCESSFULLY              <-- not done

AND THE CERTIFICATION IS A TEST THAT FAILS THE BUILD AND RUNS IN CI, NOT A
DOCUMENT. Plus a deliberate injected divergence (one defect code in one layer
only) must make it fail. That is the substantial part of T-031's ten hours and
NONE of it is written.

5.8 THE CAPTURE-MODE HASH - DO NOT BREAK THIS
--------------------------------------------------------------------------------
Backend/tools/generate_fleet_v2_donor.py --mode capture is FROZEN. Its SHA256 for
seed 20260803 is
    11EDF4B275A106C86D75EA3147D47B56F7763AD9EE2D258487953B7155939AD7
That hash is the permanent regression test for retirement-gate condition 1.
OVERWRITING THE GENERATOR WOULD MAKE src_* UNRETIRABLE FOREVER.
Capture mode REFUSES any scale but 1.

5.9 MY RECURRING DEFECT CLASSES - READ THIS BEFORE WRITING ANY CHECK
--------------------------------------------------------------------------------
Six times this session an assertion of mine named the right idea in the wrong
shape. Every one was caught by a fail-closed gate rather than by me. The pattern
is always the same: I wrote a guard against how I EXPECTED the artifact to look
rather than against how it is ACTUALLY WRITTEN.

  1. TAUTOLOGY. `count(*) AS found, count(*) AS required` can never fail.
  2. WRONG-DIRECTION GATE. Gating on both directions of an identity check whose
     contract is one-directional turned an expected difference into a FAIL.
  3. PATTERN NOT ARTIFACT. `'defect.severity' ... THEN qe.severity` also matched
     the untouched severity_value assignment, so a CORRECT edit reported 2
     against a required 1 and auto-reverted.
  4. EQUALITY WHERE A DELTA WAS CORRECT. Removing an INSERT necessarily removes
     one source_system mention; 13 -> 12 was right and equality was wrong.
  5. LITERAL NEEDLE ACROSS A LINE BREAK. A self-check phrase that the document's
     own wrapping had split could never match. FIX: build every needle as
     [regex]::Escape($needle) -replace "\\ ", "\s+" so it is immune to reflowing.
  6. COUNTING ROWS INSTEAD OF ASSERTING TRUTH. 26 exclusion records counted as
     26 findings.
  7. A REGEX THAT MATCHED A CLOSING PAREN. `qe\.event_type\s*\)` matched
     lower(qe.event_type) and produced a FALSE DEFECT REPORT that reached a
     ruling before I caught it.
  8. GUESSED SCHEMA. genealogy_edges columns, connection_profiles.name. Three
     times. There is a -Describe mode built for exactly this; use it.
  9. A MULTI-LINE PATCH ANCHOR USING LF AGAINST A CRLF FILE. It matched nothing
     and FAILED SILENTLY - no error at all. Single-line replacements landed and
     the multi-line one did not.
 10. FORGETTING prokind = 'f'. pg_get_functiondef throws on aggregates. The
     lesson was in the inherited handover, I applied it correctly once, then
     forgot it.

THE RULES THAT FOLLOW FROM THESE:
  - Assert on the END STATE, never on how a row came to exist.
  - A guard must name the EXACT artifact, not a shape that also appears elsewhere.
  - Print the matched TEXT before reporting a conclusion from a COUNT.
  - A check that parses nothing must FAIL, never report zero.
  - Whitespace-insensitive needles for any prose assertion.
  - Query the schema; never type it from memory.
  - I cannot parse PowerShell here, only Python. Anything destructive must
    FAIL CLOSED.

================================================================================
SECTION 6. EVERY TEST RUN AND ITS RESULT - DO NOT RE-RUN THESE
================================================================================
All timestamps 05-Aug-2026. Evidence files are in docs/m1/evidence/.

6.1  T-025 ENGINES                    Invoke-PpiqT025Engines.ps1      10:41
     risk 403 -> 200, 500 scores stored in 28 s, site 09000000-...0001
     correlation: 8 of 8 reached the engine, ALL REFUSED, 0 findings
     learning: 4 jobs, 0 enabled
     compute-run coverage: 4 checks clean
     26 "results" were EXCLUSION rows, not findings
     Evidence T-025_engines_20260805_104103.txt

6.2  READINESS DIAGNOSIS A            Invoke-PpiqT025Readiness.ps1    10:48
     5 of 8 outcomes NOT MATERIALISED; 3 SHOULD HAVE RUN; 0 genuinely thin
     alignment PERFECT: 4,528 outcome keys, all present on the feature side
     feature grains: coil 431,970 / 26 keys / 17,010 sample keys;
                     heat 22,680 / 12; slab 51,030 / 3
     Evidence T-025_readiness_diagnosis_20260805_104841.txt

6.3  READINESS DIAGNOSIS B            Invoke-PpiqT025ReadinessB.ps1   10:53
     defect.rate_per_m2 numeric_value present on all 7,844 - min 1, max 1
     defect.severity: numeric AND category both 0 non-null across 5,961
     defect.class: 15 categories incl. Disposition 1,883 (24.006 pct);
                   smallest share 0.01517 -> BLOCKED
     Evidence T-025_readiness_diagnosis_B_20260805_105315.txt

6.4  T-025a MATRIX PACK               apply-T-025a-risk-matrix-entry  10:39
     anchor 1, line delta 13, parens 150/150, braces 63/63, ASCII 0, BOM False
     APPLIED GREEN. Build succeeded with 21 warnings in 9.4 s.

6.5  T-025b v1                        11:03   AUTO-REVERTED
     Edit was CORRECT; two of my checks were wrong (see 5.9 items 3 and 4).
     Database left in its original state.

6.6  T-025b v2                        11:05   APPLIED GREEN
     11 self-checks green including severity branches total = 2 and
     source_system delta exactly 1.
     NOTE: live definition read 12,974 chars at v1 and 13,205 at v2 with only
     the auto-revert between. The revert round-tripped through
     pg_get_functiondef and came back longer. Anchors all still matched once.

6.7  CORRECTIVE v1                    Invoke-PpiqT025Corrective.ps1   11:10
     REFRESH A AND B BOTH 500 - 23502 refresh_run_id NOT NULL
     BOTH ROLLED BACK ATOMICALLY. Data unchanged. Budget NOT spent.

6.8  T-025c INSERT-TIME LINEAGE       apply-T-025c-insert-time-lineage 11:22
     base: 3 of 3 INSERTs rewritten; v6: 2 of 2
     frozen tokens identical; refresh_run_id NOT NULL still in the catalogue
     APPLIED GREEN.

6.9  CORRECTIVE v2                    Invoke-PpiqT025Corrective-v2    11:23
     refresh A 200 in 81.5 s; refresh B 200 in 67 s
     outcome_rows 11,922 (was 21,649): rate_per_m2 gone, class 7,844 -> 5,961
     defect.severity: 5,961 rows, category populated, 3 levels
         low 1,702 (28.552) / high 1,894 (31.773) / medium 2,365 (39.675)
     defect.class: Disposition 0, 14 classes, SCALE 26.002 pct, minority 0.01996
     defect.rate_per_m2: 0 rows
     A EXCEPT B = 0, B EXCEPT A = 0 over 517,602 rows
     52 historical result rows quarantined; 466 compute runs preserved
     frozen invariants: 5 checks clean
     CURRENT FINDINGS 0; 8 refusals recorded
     Evidence T-025_corrective_v2_20260805_112352.txt

6.10 EXCLUSION REASONS                Invoke-PpiqT025ExclusionReasons 11:38
     defect.severity / NoData / NotApplicable /
     "Undefined statistic (constant / zero-variance input)." / 26 features
     all 26 coil-grain features are value_type numeric
     defect.class multinomial -> Categorical; defect.severity ordinal ->
     Categorical; the other six definitions have 0 rows
     Evidence T-025_exclusion_reasons_20260805_113814.txt

6.11 T-025d DURABILITY                apply-T-025d-durability          ~14:5x
     760 generated at 24,556 chars; both replay paths registered; runner
     delegation applied; 3 dangling evidence refs replaced
     parity: 5 INSERTs / 5 owning lineage / 0 literal-1.0 / 1 severity-in-category
     Commit 8c6d5f6e.

6.12 T-025e v6 HOTFIX                 CANCELLED - failed closed, applied nothing
     Anchor matched 0 times. My finding was a FALSE POSITIVE (see 1.10).

6.13 HARNESS SELF-TEST                Invoke-PpiqPhenomenonHarness -SelfTest
     SELFTEST_PASS -> PASS; SELFTEST_FAIL -> FAIL;
     SELFTEST_INSUFFICIENT -> INSUFFICIENT; SELFTEST_NEGCTL -> FAIL;
     SELFTEST_CONSTANT -> FAIL. All five match their declared expectations.
     NO DATABASE CONTACTED.

6.14 HARNESS -Describe
     141 identifiers available; material_units 35,910; parameter_observations
     301,560; quality_events 7,844. NO campaign key, NO defect position.

6.15 T-026 CANDIDATE SCAN             Invoke-PpiqT026CandidateScan    13:57
     strongest parameter vs defect count: CT_C -0.1035 (n 17,010),
     FDT_C +0.0861, everything else under 0.02
     downtime pair: rho -0.0257, n 630, identical_rows 0,
         stopped 3.70-89.38 (avg 48.12), impact 0.00-294.80 (avg 34.44)
     defect codes: SENSOR_ARTEFACT 119/117 coils, LAMINATION 119/119,
         ROLL_MARK 149/140 ... SCALE 1,550/1,293
     conditioning candidates: grade_or_recipe 6 levels, reason_code 5,
         downtime_type 4, severity 3, product_family ONE LEVEL (useless)
     NOTE: this scan tested 14 parameters, not 26 - the twelve heat-grain
     chemistry parameters need the genealogy join and were never scanned.

6.16 T-026 HARNESS RUN                Invoke-PpiqPhenomenonHarness    14:08
     PPIQ-COIL-COILING-TEMP-DEFECTS     PASS  n 17,010 rho -0.1035 cond 0.0004
     PPIQ-DOWNTIME-IMPACT-SCALES        FAIL  n 630   rho -0.0257
     PPIQ-SENSOR-ARTEFACT-COILING-TEMP  INSUFFICIENT n 117 below 780
     exit 1. Evidence T-026_harness_20260805_140817.txt + .json

6.17 T-027 LEDGER                     Invoke-PpiqT027Ledger.ps1       15:02
     104 / 104 accounted for
     24 DECLARATION_NOT_NUMERIC
     34 SOURCE_VARIABLE_NOT_MATERIALISED
     46 GENERATOR_SCOPE_NOT_IMPLEMENTED
      0 MEASURE_UNSUPPORTED,  0 EXECUTABLE
     declared statistics: 78 unclassified, 9 rate_ratio, 7 threshold_membership,
                          4 fdr_significance, 3 variance_comparison, 3 step_change
     charts referenced 36, backed by a proven phenomenon 0
     negative controls 17, measured silent 0
     Evidence T-027_coverage_report_20260805_150209.txt + the ledger CSV

6.18 T-028 VERIFICATION               Invoke-PpiqT028Verification     17:39
     naive rho -0.1035 (n 17,010)
     by GRADE: DP600 -0.0048, DX51D -0.0062, HSLA-420 -0.0123, IF-LOW-C -0.0148,
               S235JR +0.0344, S355MC +0.0047, WEIGHTED MEAN 0.0004
     by THICKNESS quartile: -0.1211 / -0.0987 / -0.0843 / -0.1091,
               WEIGHTED MEAN -0.1033
     controls: DENT ABSENT, SEAM ABSENT, SCRATCH present 298 events rho -0.0372
     gate: defect.class BLOCKED minority 0.0200 (heats 1,341, events 5,961,
           completeness 1.0000); defect.severity not blocked at 0.2855
     Evidence T-028_verification_20260805_173955.txt

6.19 T-029 REALISM AUDIT v1           18:23   TWO LAYERS ERRORED (my column guess)
6.20 T-029 REALISM AUDIT v2           18:27
     STRUCTURAL  PASS 8 checks 0 offending
     PHYSICAL    FAIL 1,167
     DENSITY     NOT COMPUTABLE - LENGTH_MM and WEIGHT_KG absent
     TEMPORAL    PASS 5 checks 0 offending
     STATISTICAL FAIL 9 uniform-signature parameters
     ANALYTICAL  PASS 4 checks 0 offending
     TRIGGER     PASS - guard fired by name, rolled back, 0 left behind
     uniform: OXYGEN_NM3 0.5135, LF_ARGON_NM3 0.5117, BATH_TEMP_C 0.5028,
              LINE_SPEED_MPM 0.5023, LF_CALCIUM_M 0.5015, QA_THK_MM 0.4974,
              QA_WIDTH_MM 0.4963, QA_ROUGHNESS_UM 0.4951, ACID_CONC_PCT 0.4842
     natural for contrast: THICKNESS_MM 0.4070 ... ROLL_TEMP_C 0.1727
     Evidence T-029_realism_audit_v2_20260805_182726.txt

6.21 T-029 RANGE BREAKDOWN            Invoke-PpiqT029RangeBreakdown   18:30
     SUPERHEAT_C  declared 10..60      observed -2.712..51.030
                  834 offending (4.903 pct), ALL BELOW, worst 25.42 pct of range
     CARBON_PCT   declared 0.010..0.250 observed 0.000..0.213
                  332 offending (17.566 pct), ALL BELOW, worst 4.17 pct
     POWER_KWH    declared 0..100000    observed 69,048..101,072
                  1 offending (0.053 pct), worst 1.07 pct - A TAIL, NOT A DEFECT
     total 1,167 reconciled independently
     Evidence T-029_range_breakdown_20260805_183020.txt

6.22 T-030 STAGING VERIFICATION       Invoke-PpiqT030StagingVerification 18:39
     POPULATED  PASS; UNPREPARED PASS (4 checks 0); IDENTITY - see note
     staging: heats 630, lf_treatment 630, cast_sequence 630, cast_pieces 5,670,
              hsm_coils 5,670, hsm_pass_measurements 39,690, pickle_orders 5,670,
              qa_lab_results 17,010, parsytec 1,987, downtime 210
     staging coils with no canonical match 0; staging heats 0
     canonical coils with no staging match 11,340; heats 1,260  (EXACT 3:1)
     coil_weight_kg populated on all 5,670 rows, 12,508.1 to 28,498.4 kg
     NOTE: my runner reported IDENTITY FAIL 12,600. THAT VERDICT WAS WRONG - the
     clause is directional and PASSES. Corrected in T-030_CLOSURE.md section 4.
     Evidence T-030_staging_verification_20260805_183901.txt

6.23 T-031 LAYER CHECK                Invoke-PpiqT031LayerAndDependencyCheck 18:47
     dump_store 164,827 rows / 10 tables vs src_* 77,797 / 10
     acquisition 5 tables ALL EMPTY; canon 16 tables nearly all empty
     6 of 8 connection_profiles name src_; all 10 dump-registry rows do;
     1 source_dataset_definition does; mapping_definitions 0
     section 3a ERRORED - pg_get_functiondef on aggregates, needs prokind='f'
     Evidence T-031_layer_dependency_check_20260805_184705.txt

6.24 T-031 DEPENDENCY CHECK B         Invoke-PpiqT031DependencyCheckB 18:49
     objects mentioning src_: ppiq_run_stage2_canonical_refresh,
       v_phase1_kpi_quality_temperature_window, v_phase1_material_genealogy_join,
       v_phase1_surface_defect_join, v_phase3_dump_kpi_quality_temperature_window,
       v_phase3_dump_material_genealogy_join, v_phase3_dump_surface_defect_join
     dump coils also present in src_*      5,670
     dump coils with NO canonical match   12,147     <-- A DIFFERENT PLANT
     canonical coils with no dump match   11,340
     registry: all 10 rows is_active=t, stage1 and stage2 Ok, last import
       2026-07-08; 392 two-stage runs 29-Jun to 08-Jul; import_batches 23-Jun
       to 13-Jul, one Failed and one still Running
     section 2 ERRORED - connection_profiles has no "name" column
     Evidence T-031_dependency_check_b_20260805_184958.txt

6.25 TESTS NOT RUN, AND WHY
--------------------------------------------------------------------------------
  - T-024 requirement 8 browser walk: DEFERRED by ruling.
  - No frontend build, no full test suite, no CI pipeline run.
  - New-AcceptanceEmptyDb.ps1 -Execute: OFFERED as the existing empty-database
    replay proof for 760 but NEVER RUN. Still available and still cheap.
  - fc /n on the two T-025b _original.sql backups: superseded once the false v6
    alarm was withdrawn; git grep confirmed nothing committed carried it.

================================================================================
SECTION 7. HIS RULES, RULINGS AND WAYS OF THINKING - CARRY ALL OF THESE
================================================================================

7.1 STANDING DELIVERY CONTRACT
--------------------------------------------------------------------------------
  - ALWAYS deliver a PowerShell script - diagnostics included. NEVER ask him to
    paste JS into DevTools or run ad-hoc commands by hand.
  - CREDENTIALS GO IN THE SCRIPT. He types nothing. I broke this once with a
    bare `psql -c` and it prompted for a password. "It is only one query" is
    exactly how the exception gets in.
  - EVERY run block OPENS with the two lines that put the file where it runs
    from: cd C:\Workspace\PlantProcess-IQ, then Move-Item from Downloads, then
    Unblock-File. Destination: tools\packs\ for packs, tools\run\ for runners.
  - Never hand over a script without its run command - report-only dry run
    first, then apply, then revert, then the commit step.
  - NEVER deliver zip files.
  - Pure ASCII, UTF-8 no BOM, CRLF for .ps1/.cs, LF for .sh. No em-dashes, no
    curly quotes. No && in PowerShell. Cuddled } else {.
  - Apply-pack contract: preflight, backup, anchored replace, self-check, gate,
    auto-revert.
  - EXCEPTION: for a small one-line source edit he prefers being told exactly
    which line to change to what, because long pasted scripts get truncated by
    the console.
  - Uploads frequently arrive empty; paste output as text instead.

7.2 HIS NEW STANDING RULE FROM THIS SESSION
--------------------------------------------------------------------------------
  "A pack may mutate a live database for execution, but NO PRODUCT-SEMANTIC
   DATABASE CHANGE IS COMPLETE UNTIL THE EQUIVALENT PERMANENT DEFINITION OR
   MIGRATION EXISTS IN TRACKED SOURCE CONTROL."
  Because tools/packs is gitignored, this is not optional.

7.3 ABSOLUTE BACKLOG ADHERENCE
--------------------------------------------------------------------------------
  - PPIQ_Backlog_v2.9.1_03Aug2026 .md and .xlsx are THE ONLY SCOPE. If it is not
    written there, DO NOT DO IT.
  - Tasks execute in dependency order from T-001 upward, sequentially. A task is
    fully complete, including sign-off questions, before the next starts.
  - Temporary DATA and temporary internal implementation are sometimes allowed.
    Temporary PRODUCT IDENTITY, temporary UX and fake product answers NEVER are.
  - When a finding falls outside every bucket a task defines, NAME THE GAP AND
    ASK FOR A RULING rather than inventing a bucket.
  - Six chapters (PPIQ_Chapter1..6) are "my bible - follow 100 percent".

7.4 DEPTH AND HONESTY MANDATE
--------------------------------------------------------------------------------
  - Re-read the ACTUAL CODE before every review or backlog revision rather than
    working from notes.
  - Never let a cross-reference, an hour total or a dependency order rest on
    memory when it can be verified or machine-checked.
  - Build a MECHANICAL GUARD whenever a defect class is mechanical, instead of
    promising to be careful.
  - State arithmetic openly and never fit a number to a budget.
  - NAME MY OWN DEFECTS BEFORE HE FINDS THEM.

7.5 THE SPEED RULING (current operating mode)
--------------------------------------------------------------------------------
  For each task: read the frozen text, use existing evidence, implement the
  SMALLEST PERMANENT CORRECTION, run its targeted acceptance, record evidence,
  close or record the precise blocker, continue immediately.

  DO NOT introduce: discovery pack -> ruling -> second discovery -> performance
  investigation -> broad audit.

  OPTIMISE FOR CLOSING BACKLOG TASKS, NOT FOR MAXIMISING FINDINGS PER TASK.

  COME BACK ONLY FOR:
      an architecture contradiction
      an irreversible or destructive choice
      a cross-worker collision
      a literal acceptance condition that cannot be met without stealing another
      task's scope
  Otherwise choose the smallest permanent correct implementation and continue.

7.6 EVIDENCE AND HONESTY RULINGS
--------------------------------------------------------------------------------
  - A REFUSAL OR GAP IS A REAL RESULT for coverage purposes. It is NEVER silently
    converted into PASS or FAIL.
  - A LAYER OR CHECK THAT COULD NOT BE COMPUTED IS NOT A PASS.
  - DO NOT retrofit bands from observed data. Writing the band from the data and
    then testing the data against it is a self-fulfilling test.
  - DO NOT reinterpret one statistic as another to make a row measurable.
  - DO NOT enable a job, widen a range, weaken a threshold or create a trigger
    merely to turn something green.
  - DO NOT call a missing variable a scientific FAIL.
  - A missing honest outcome is preferable to a fabricated analytical metric.
  - No false PASS. Record COMPLETE + BLOCKED with the precise blocker instead.
  - Do not describe a bounded sample as full coverage.

7.7 HIS CLOSING JUDGEMENT ON T-025, WORTH KEEPING
--------------------------------------------------------------------------------
  On accepting zero findings and recording the T216 gap:
      "this is a very correct ending for T-025 - it discovered a real limitation
       in the product, and that is better than hiding it."

7.8 SEQUENCING HE HAS SET
--------------------------------------------------------------------------------
  T-024 requirement 8 is DEFERRED, not waived, pending presentation convergence.
  Do NOT open work on /dashboard, /analytics-widgets, /correlations, /correlation,
  /risk or the material-investigation presentation just because the eventual walk
  will inspect them. If a page is owned by a backlog task, THAT task implements it.
  After T-031: JUMP DIRECTLY TO M1-P5 and start its first outstanding task.
  Do not stop to remediate the T-027 or T-029 generator findings unless a later
  frozen task explicitly owns them.

================================================================================
SECTION 8. BACKLOG TASK STATUS
================================================================================

8.1 M1-P1b - DATABASE / PRESENTATION DATA
--------------------------------------------------------------------------------
T-013 .. T-023   DONE before this session (88 of 114 hours)
T-024            requirements 1-7 PASS, requirement 8 DEFERRED
                 -> T-024 does NOT hold a Done status
T-025  8h        DONE. Commit 67c7395e.
T-025a/b/c/d     corrective sub-work, all applied and committed
T-025e           CANCELLED - my finding was a false positive
T-026  6h        DONE. Commit e270b40d.
T-027  6h        implementation COMPLETE, acceptance BLOCKED. Commit b5faf96c.
T-028  2h        DONE. Commit 45dfc0f9.
T-029  4h        implementation COMPLETE, acceptance BLOCKED. Commit 1ac877db.

8.2 M1-P2 - opens at T-030
--------------------------------------------------------------------------------
T-030  8h        implementation COMPLETE, acceptance BLOCKED on the surfaces
                 clause only. Commit cb72a3de.
T-031 10h        IN PROGRESS, STOPPED before any deletion. Two read-only
                 dependency passes done. See 8.4 for exactly what remains.
T-032, T-033     OWNED BY THE PARALLEL FRONTEND WORKER. Do not touch.

8.3 WHY THREE TASKS ARE "COMPLETE BUT BLOCKED"
--------------------------------------------------------------------------------
In each case the implementation and its acceptance run are finished and
evidenced, and a clause of the frozen validation cannot be honestly satisfied
from the authoritative inputs. Recording COMPLETE + BLOCKED with the precise
blocker is his ruled treatment. It is NOT a partial status and NOT a false pass.

  T-027  no executable predeclaration exists for the current measurement contract
  T-029  two generator distributions and one missing emission
  T-030  a frontend surfaces clause on a deferred track

8.4 T-031 - EXACTLY WHAT REMAINS
--------------------------------------------------------------------------------
DONE:      two read-only passes establishing the layer topology and the
           dependency surface. Nothing deleted, nothing written.

NOT DONE, IN ORDER:
  A. THE CERTIFICATION TEST. A NAMED TEST that asserts every consistency
     dimension - same grades, equipment identities, defect vocabulary, downtime
     semantics, chemistry vocabulary, QA definitions and units, genealogy, time
     horizon, planted phenomena - across staging, canonical and analysis. It must
     FAIL THE BUILD and RUN IN CI. Row counts across layers are explicitly NOT
     compared; what is compared is the PLANT UNIVERSE.
  B. A DELIBERATE INJECTED DIVERGENCE (one defect code present in one layer only)
     must make it fail, proving the gate is switched on.
     NOTE: a REAL divergence already exists - dump_store's 12,147 orphan coils -
     so the certification should fail on first run without any injection.
  C. The generator version and seed behind all three layers recorded as identical.
  D. THE FOUR RETIREMENT PRECONDITIONS, in order. Precondition 2 is FALSE today.
  E. Only then: delete src_*, remove stale registered datasets, dead import
     batches and validation-fixture / DEMO-vocabulary mappings that now have
     replacements. Proof is a query returning zero matching schemas.

THE OPEN QUESTION HE HAS NOT YET RULED ON:
  dump_store is stale and is itself arguably part of the "obsolete parallel data
  world" the task says must not remain active. Two candidate paths:
    (i)  regenerate dump_store from the current src_* through the two-stage
         import - but the import is DELTA-BASED on last_index_value_text, so a
         re-run may APPEND rather than replace and the 12,147 orphans would need
         clearing first;
    (ii) retire dump_store alongside src_* since canonical is materialised
         directly by the generator under the M1 fast path.
  I did not choose. It is a destructive-choice decision.

ALSO UNRESOLVED: my src_ dependency pattern cannot distinguish a reader of
SCHEMA src_meltshop_pg from a reader of TABLE dump_store.src_meltshop_pg_heats.
A schema-qualified pattern is needed before that list can be trusted.

8.5 CARRIED-FORWARD ITEMS NOT OWNED BY ANY CURRENT TASK
--------------------------------------------------------------------------------
  T216   Numeric x Categorical statistical method MISSING, plus the mislabelled
         exclusion reason. Recorded in
         docs/m1/evidence/T216_capability_gap_numeric_x_categorical.md
  GEN-1  SUPERHEAT_C and CARBON_PCT need physical floors. 1 hour.
  GEN-2  Nine uniform-random distributions need natural shape. 2 hours.
  GEN-3  Emit LENGTH_MM and WEIGHT_KG to canonical. 1 hour. They EXIST at source.
  MAT-1  Matrix vocabulary reconciliation - casting_speed_m_min vs
         CASTING_SPEED_MPM and similar.
  MAT-2  A versioned future matrix must carry NUMERIC predeclarations authored
         BEFORE the next validation population is generated or observed.
  T-015  four sign-off questions still unanswered: mill_line, the 90-day horizon
         (implemented at 91.8 days), fourteen defect codes, two negative controls.
  C11    width_position_mm positioning was never implemented, so the defect map
         shows nothing it was designed to show.
  T-024  the mixed-industry vocabulary is still customer-visible: 7 defect
         catalog rows, 19 parameter definitions, 16 equipment rows and 10
         material unit type definitions outside flat steel.

================================================================================
SECTION 9. DEPLOYMENT, SERVER AND PIPELINE
================================================================================

9.0 STATE THIS PLAINLY
--------------------------------------------------------------------------------
NO DEPLOYMENT, SERVER OR PIPELINE WORK WAS PERFORMED IN THIS SESSION. Nothing was
deployed, no Jenkins job was run, no server was touched, no pipeline was made
green. Everything below is INHERITED KNOWLEDGE plus OBSERVATIONS from the audit
signal report. Do not read any of it as work completed here.

9.1 SERVER TOPOLOGY (inherited)
--------------------------------------------------------------------------------
Hetzner VPS 178.105.152.180.
TWO-PROJECT TOPOLOGY IS PERMANENT AND DELIBERATE - NEVER MERGE THEM:
    plantprocessiq  = sacred infrastructure (Jenkins, Caddy, backup-runner)
    ppiq-app        = the application deploy
Public hosts use sslip.io:
    https://app.178.105.152.180.sslip.io
    https://api.178.105.152.180.sslip.io
    https://jenkins.178.105.152.180.sslip.io

9.2 KNOWN SERVER HISTORY (inherited, unverified this session)
--------------------------------------------------------------------------------
  - Stability root cause 03-Jul: the Caddyfile routed to a non-existent container
    plantprocess-app-web; the real container is plantprocess-web. A runtime
    Docker network alias was applied as a WORKAROUND. The permanent fix is
    blocked by a read-only bind-mount with a missing host source file.
  - Smoke-password bug: VITE_SMOKE_PASSWORD=change-me-before-production was baked
    into the bundle and caused a 401 auto-login loop.
  - Two Docker stacks existed: the live serving stack (plantprocessiq) and an
    orphaned Jenkins-deployed stack (ppiq-demo).
  - The Jenkinsfile backs up .env, Caddyfile and docker-compose.demo.yml before a
    git reset and restores them after.
  - GitHub webhook at https://jenkins.178.105.152.180.sslip.io/github-webhook/;
    one manual "Build Now" was needed as a one-time primer.

9.3 DEPLOYMENT AND REPLAY MECHANISMS THAT MATTER TO THIS TRACK
--------------------------------------------------------------------------------
THE AUTHORITATIVE REBUILD PATH IS THE NUMBERED SQL REPLAY CHAIN,
Backend/database/scripts/*.sql in name order. There are NO EF migration artifacts
anywhere in the repository. scripts/db/New-AcceptanceEmptyDb.ps1 states the chain
in its own header and refuses any target whose name lacks "acceptance".

The numbered scripts are MUTABLE definitions, not immutable history:
SqlScriptHygieneApplyTests.cs checks only non-emptiness, BOM, function presence
and password masking. Nothing enforces a checksum or ordering immutability.

TWO REAL REPLAY PATHS, BOTH NOW CARRY 760:
    scripts/demo/Rebuild-PresentationDb.ps1
        pg_restore of a fixture dump, then a NAMED list - 741, 742, 750, and now
        760. Its own comment: "or the engine re-blinds on every rebuild".
    deploy/server/apply-server-db-scripts.sh
        ordered list 200, 201, 202, 203, and now 760. It ends by invoking
        ppiq_ml_refresh_feature_store_v6(3650) directly as a proof step.

9.4 CI OBSERVATIONS FROM THE AUDIT SIGNAL REPORT (05-Aug, 56 signals)
--------------------------------------------------------------------------------
These are OBSERVATIONS, not work done. The 05-Aug package was 2,195 files /
375,759 lines / 27.62 MB, with 56 signals against 62 on 03-Aug - and THE ENTIRE
DROP IS PACK-BACKUP CHURN, not remediation. Every real finding is unchanged at
the same file:line.

  CRIT  CI: frontend tests enumerated, not executed (--list)   8 hits
        Frontend/PlantProcess.Web/package.json:84 phase9:matrix uses --list
        tools/ci/validate-real-ui-gates.cjs:13-15 three --list commands
        Frontend/.../tools/phase56/apply-phase5-phase6-full-ui-migration.cjs:74-76
  CRIT  CI: catchError forcing SUCCESS                         3 hits
  CRIT  Config: wrong connection-string key                    1 hit
  WARN  Security: dev seed endpoint reference                 16 hits
  WARN  Config: hardcoded server IP 178.105.152.180           15 hits
  WARN  Security: bootstrap admin enabled in config            3 hits
        env/profiles/local.env:41 and presentation.env:41
        PlantProcess__Auth__Users__0__IsBootstrapAdmin=true

  TWO LIVE CI FINDINGS CARRIED FORWARD, NEITHER FIXED:
    FINDING A  tools/ci/validate-real-ui-gates.cjs is referenced by NOTHING. It
               is an orphan gate that no pipeline invokes.
    FINDING B  apply-phase5-phase6-full-ui-migration.cjs still INJECTS a Jenkins
               stage whose only test commands are three --list enumerations.
               A --list run enumerates tests; it does not execute them. A green
               stage built on it proves nothing.
  Also: the audit scanner matches ITS OWN RULE TABLE - four self-matches - so the
  one-line RelativePath self-exclusion in GeneratePlantProcessIQ_UltimateAudit.ps1
  was never applied.
  And DevSeedEndpoints.cs still carries a mid-file UTF-8 BOM at line 2.

9.5 LOCAL ENVIRONMENT FACTS CONFIRMED THIS SESSION
--------------------------------------------------------------------------------
  psql    C:\Program Files\PostgreSQL\16\bin\psql.exe
  python  C:\Python313\python.exe
  API     http://localhost:5063, auth e2eadmin / E2EAdmin123!
  DB      127.0.0.1:5432, ppiq_presentation, ppiq_dev / ppiq_dev_local_only
  Machine execution policy blocks unsigned scripts on direct .\script.ps1 in some
  shells - the documented workaround is
      powershell -NoProfile -ExecutionPolicy Bypass -File .\script.ps1
  (this session's direct invocations worked, but keep the workaround in mind)

  API LAUNCH   .\scripts\run\start-api.ps1 -Profile local
  WEB LAUNCH   .\node_modules\.bin\vite --host localhost --port 5173
               (the start-web launcher is broken; Vite 5+ rejects positional args)

================================================================================
SECTION 10. PIPELINE-GREEN AND APP-URL MODIFICATIONS
================================================================================

10.1 THE HONEST ANSWER
--------------------------------------------------------------------------------
THIS SESSION MADE NO MODIFICATIONS TO MAKE THE PIPELINE GREEN AND NONE TO MAKE
THE APP URL WORK. No Jenkinsfile was edited, no CI gate was changed, no Caddy
configuration was touched, no container was restarted, no deployment ran.

Presenting anything here as such work would be inventing it. What follows is the
state as known, and what would have to be done - clearly labelled as NOT DONE.

10.2 WHAT THIS SESSION DID THAT AFFECTS A FUTURE DEPLOY
--------------------------------------------------------------------------------
Indirect but real:
  - Backend/database/scripts/760_... is now in deploy/server/apply-server-db-scripts.sh,
    so a server database replay will now converge on the corrected producers and
    the lineage schema instead of the pre-T-025 semantics. BEFORE this change a
    server replay would have produced value tables with NO refresh_run_id column
    and then failed on the first refresh with 23502.
  - The same script ends by invoking ppiq_ml_refresh_feature_store_v6(3650). That
    call could not have succeeded before T-025c because v6's INSERTs did not
    supply lineage. It can now.
  - PlantAccessControl.cs gained the /risk-scores entry, so the risk batch route
    is reachable on any deployment, and the previously anonymous GETs under that
    prefix now require a token.

  NONE OF THIS HAS BEEN EXERCISED ON THE SERVER. It is source-correct and
  server-untested.

10.3 WHAT WOULD HAVE TO BE DONE - NOT DONE, LISTED FOR THE NEXT SESSION
--------------------------------------------------------------------------------
  P1  FINDING B: remove the --list injection from
      apply-phase5-phase6-full-ui-migration.cjs so the Jenkins stage executes
      tests rather than enumerating them. A green stage built on --list is a
      false green and is the single most misleading item in CI.
  P2  FINDING A: either wire tools/ci/validate-real-ui-gates.cjs into a pipeline
      stage or delete it. An orphan gate is worse than no gate because its
      existence implies coverage.
  P3  The three catchError-forcing-SUCCESS hits.
  P4  The 15 hardcoded 178.105.152.180 occurrences across deploy scripts, README
      and validators - they make the deployment single-host by construction.
  P5  The mid-file BOM in DevSeedEndpoints.cs.
  P6  The audit scanner's own self-match exclusion - one line.
  P7  Verify app.178.105.152.180.sslip.io renders after any deploy, since the
      Caddy container-name workaround is a runtime alias rather than a permanent
      fix.

10.4 CAUTION FOR WHOEVER PICKS THIS UP
--------------------------------------------------------------------------------
The audit signal report is a SIGNAL report, not a defect list. Four of its
entries are the scanner matching its own rule table. Verify each hit at its
file:line before acting - I did that for FINDING A and FINDING B and both are
real, and I did NOT verify the catchError or connection-string hits.

================================================================================
END OF HANDOVER
================================================================================

IMMEDIATE NEXT ACTIONS FOR THE NEW SESSION, IN ORDER:
  1. Read section 7 first. The rules govern everything.
  2. T-031 is in progress and STOPPED before a destructive step. Get his ruling
     on the dump_store question in 8.4 before touching anything.
  3. The certification test (8.4 item A) is buildable NOW, independent of that
     ruling, and is the substantial part of T-031's ten hours.
  4. After T-031 closes, JUMP DIRECTLY TO M1-P5.
  5. Do not re-run anything in section 6.

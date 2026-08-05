# T-025 CLOSURE RECORD - Compute and populate the presentation analysis entities with the real engines

Milestone / Phase : M1 / M1-P1b
Database          : ppiq_presentation
Closed            : 2026-08-05
Backlog authority : PPIQ_Backlog_v2.9.1_03Aug2026 (FROZEN)

--------------------------------------------------------------------------------
1. WHAT THE TASK REQUIRED
--------------------------------------------------------------------------------

Run the product's own statistical, correlation, feature and readiness engines over
the canonical operational entities and persist what they return. THESE ARE
COMPUTED, NEVER AUTHORED. Where an engine legitimately refuses - insufficient
support, readiness not met - THE REFUSAL IS THE RESULT AND IS PERSISTED AS SUCH.

Validation: every analysis entity row carries a compute run identity that names
the engine, its inputs and its version; no analysis row exists without one;
re-running the engines over the same canonical data reproduces the same results;
at least one genuine refusal is present and rendered honestly; no hand-authored
row is present anywhere in the analysis entities.

--------------------------------------------------------------------------------
2. THE FINAL STATE, WITH EACH CATEGORY KEPT SEPARATE
--------------------------------------------------------------------------------

  produced findings              0
  method exclusions             26   defect.severity, one per numeric parameter
  readiness-blocked runs         7   defect.class plus the six unmaterialised outcomes
  learning                       4 catalogued / 0 enabled / NOT CONFIGURED
  bounded risk execution       500 evaluated of 35,910 eligible

AN EXCLUSION IS NOT A FINDING AND IS NOT COUNTED AS ONE. The writer emits one row
into ml_correlation_results_v2 per excluded feature, carrying method
'NotApplicable', sample_size 0 and an evidence_json marked excluded. A finding
carries a method, a coefficient and a sample size above zero. An earlier gate of
mine counted rows in that table and reported PASS on 26 exclusion records; the
corrected gate requires the finding predicate and reports 0.

THE RISK NUMBER IS A BOUNDED ENGINE-EXECUTION PROOF, NOT COVERAGE. 500 of 35,910
material units were evaluated. No customer-visible surface may claim
full-population risk coverage on the strength of it.

THE LEARNING CATALOGUE IS PRESENT AND UNCONFIGURED. Four jobs exist and
ml_learning_job_catalog_v1.is_enabled is NOT NULL DEFAULT false, so all four are
disabled. That is a configuration state, not a missing catalogue. No job was
enabled to manufacture a green result.

--------------------------------------------------------------------------------
3. WHAT WAS PROVEN
--------------------------------------------------------------------------------

  insert-time lineage                  PASS
  NOT NULL invariant                   PASS
  corrective refresh A                 PASS   200 in 81.5 s
  corrective refresh B                 PASS   200 in 67.0 s
  row-level reproducibility            PASS   A EXCEPT B = 0, B EXCEPT A = 0 over 517,602 rows
  corrected outcome materialisation    PASS
  stale-result quarantine              PASS   52 rows removed, 466 compute runs preserved
  risk bounded execution               PASS   500 of 35,910
  genuine readiness refusal            PASS   persisted, on corrected data

INSERT-TIME OWNERSHIP IS PROVEN BY THE REFRESH SUCCEEDING AT ALL. With
refresh_run_id NOT NULL on both value tables, any row not owned at creation would
be rejected by the constraint. The proof is not a reading of the post-insert
stamping UPDATE, which is now a no-op and is no longer relied upon for
correctness.

--------------------------------------------------------------------------------
4. THE THREE PRODUCER DEFECTS CORRECTED, AND HOW EACH WAS MEASURED
--------------------------------------------------------------------------------

4.1 defect.severity was written to a column the loader never reads.
    The producer put qe.severity into severity_value. NpgsqlFeatureVectorLoader
    reads only numeric_value and category_value, so all 5,961 rows arrived with
    both null. A single empty class makes MinorityFraction return 0.0 through its
    g.Count < 2 branch, below the 0.03 readiness floor.
    CORRECTED: severity is now written to category_value as well; severity_value
    is preserved. Measured after: 5,961 rows, three levels -
    low 1,702 (28.552 percent), high 1,894 (31.773), medium 2,365 (39.675).
    Minority fraction 0.28552, readiness Ready.

4.2 defect.class was contaminated by an event_type fallback.
    COALESCE(dc.defect_code, dc.defect_category, qe.event_type) let a quality
    event with no defect catalogue fall through to its event type. Measured
    before: 1,883 of 7,844 rows carried the class "Disposition".
    CORRECTED: the fallback is removed. Measured after: 0 Disposition rows,
    population 5,961, fourteen classes, and SCALE at 1,550 of 5,961 =
    26.002 percent against the 26.0 percent declared in the T-015 catalogue.

4.3 defect.rate_per_m2 was the literal 1.0.
    The insert hardcoded the value and left normalization_denominator NULL.
    Measured before: min 1, max 1 across 7,844 rows. A constant outcome makes
    every correlation against it undefined.
    BOUNDED DENOMINATOR CHECK, PERFORMED BEFORE REMOVING IT: hsm_coils carries
    actual_thickness_mm, actual_width_mm and coil_weight_kg and NO LENGTH. Slabs
    carry length_mm; coils do not. The canonical emit writes exactly four coil
    dimensions - FDT_C, CT_C, THICKNESS_MM, WIDTH_MM - and no LENGTH or AREA
    parameter code exists in the generator. Area could only be reconstructed as
    weight / (thickness x assumed density), and the donor generator records as
    FAULT-1 that weight_kg is drawn independently of the dimensions, so implied
    density is not physical.
    A second, independent corroboration: the v6 producer's own rate_per_m2 insert
    writes normalization_denominator = 1.0 and stamps
    'area_m2_missing_fallback_to_unit' into its provenance JSON. The codebase
    states the gap itself.
    CORRECTED: the outcome is no longer materialised. Measured after: 0 rows. The
    definition row remains and the outcome reports as not materialised, which is
    the honest state. A missing honest outcome is preferable to a fabricated
    analytical metric.

4.4 A fourth defect, in the schema-versus-producer contract.
    Enforcing refresh_run_id NOT NULL made both producers unrunnable, because they
    inserted value rows without lineage and stamped them afterwards. Every refresh
    failed with 23502 until the run identity was supplied at insert in all five
    live value INSERTs - three in the base producer using v_run_id, two in v6
    using v_base.run_id, so base rows and v6 rows share one authoritative run.

--------------------------------------------------------------------------------
5. THE REMAINING ZERO-FINDING CONDITION IS A PRODUCT CAPABILITY GAP
--------------------------------------------------------------------------------

defect.severity now holds valid categorical data and clears readiness. It then
excluded all 26 parameters. The cause is not the data.

Backend/PlantProcess.Analytics.Core/Methods/MethodSelector.cs covers four cases:

  Numeric      x Numeric        -> Spearman, or MutualInformation if nonlinear
  Binary       x Numeric        -> PointBiserial
  Categorical  x Categorical    -> CramersV
  anything else                 -> NotApplicable, IsApplicable = false

MapOutcome maps both 'multinomial' and 'ordinal' to VariableType.Categorical.
Every materialised outcome is therefore Categorical and all 26 features at grain
coil are Numeric. Numeric x Categorical has no entry, so the selector returns
NotApplicable for every parameter, Measure returns NaN, and every parameter is
excluded. NO PAIRING AVAILABLE IN THE CURRENT CANONICAL POPULATION CAN PRODUCE A
FINDING, REGARDLESS OF READINESS.

THE RECORDED EXCLUSION REASON IS MISLEADING AND IS QUOTED HERE VERBATIM SO THAT
NOBODY IS SENT AFTER THE WRONG THING:

  method = NotApplicable
  reason = "Undefined statistic (constant / zero-variance input)."

The method is correct. The reason is not. Measure returns NaN for an unsupported
pairing exactly as it does for a constant input, and the exclusion path attributes
every NaN to zero variance. The parameters in question carry between 1,096 and
4,528 distinct values each over 7,247 to 7,844 aligned pairs, so zero variance is
demonstrably false. The engine is blaming the data for a missing method.

Owner: T216 rigorous statistics. See T216_capability_gap_numeric_x_categorical.md.

--------------------------------------------------------------------------------
6. WHAT WAS DELIBERATELY NOT DONE
--------------------------------------------------------------------------------

  - No ANOVA, eta-squared, Kruskal-Wallis or other Numeric x Categorical method
    was implemented. That is T216 scope and belongs outside a data task.
  - No new numeric outcome was created to force a positive correlation finding.
  - No third Feature Store refresh was performed.
  - No learning job was enabled.
  - NpgsqlFeatureVectorLoader was not modified; the defect was local to the
    producers.
  - NOT NULL was not weakened. The producers were corrected to satisfy it.
  - The v6 rate_per_m2 semantics were left unchanged. Lineage was added there;
    the mislabelled metric is recorded, not silently corrected, because it is
    outside this task and v6 is not the path the API calls.

--------------------------------------------------------------------------------
7. EVIDENCE FILES
--------------------------------------------------------------------------------

  docs/m1/evidence/T-025_readiness_diagnosis_20260805_104841.txt
  docs/m1/evidence/T-025_readiness_diagnosis_B_20260805_105315.txt
  docs/m1/evidence/T-025_corrective_v2_20260805_112352.txt
  docs/m1/evidence/T-025_exclusion_reasons_20260805_113814.txt

  Backend/database/scripts/760_t025_lineage_and_outcome_producer.sql
  Backend/PlantProcess.Api/Security/PlantAccessControl.cs
  tools/run/Invoke-PpiqT025LineageMigration.ps1
  tools/run/Invoke-PpiqT025Engines.ps1
  tools/run/Invoke-PpiqT025Readiness.ps1
  tools/run/Invoke-PpiqT025ReadinessB.ps1
  tools/run/Invoke-PpiqT025Corrective-v2.ps1
  tools/run/Invoke-PpiqT025ExclusionReasons.ps1


  NOTE: tools/packs/* were EXECUTION VEHICLES, not durable evidence. They are
  gitignored, as are the _backup_* directories. The permanent record of every
  T-025 database change is 760_t025_lineage_and_outcome_producer.sql.
--------------------------------------------------------------------------------
8. STATUS
--------------------------------------------------------------------------------

T-025 = DONE.

The Feature Store is closed permanently and is not to be reopened. The outcome
producer is frozen. Zero findings is the measured truth about what this product
can currently compute from this plant, and it is recorded as such rather than
worked around.

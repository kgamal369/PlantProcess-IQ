# T-028 CLOSURE RECORD - Verify the confounded correlation and the insufficient-support refusal

Milestone / Phase : M1 / M1-P1b
Database          : ppiq_presentation
Closed            : 2026-08-05
Evidence          : docs/m1/evidence/T-028_verification_20260805_173955.txt

--------------------------------------------------------------------------------
1. WHAT THE TASK REQUIRED
--------------------------------------------------------------------------------

Both phenomena appear to be present already, so the work is to PROVE both rather
than to plant them. Run the naive analysis, then the conditioned analysis, and
record the difference. Then find an outcome with genuine data but too little
support to pass the readiness gate. DO NOT WEAKEN ANY THRESHOLD to produce either
result.

Validation: a recorded finding that survives naive analysis and is reported as not
surviving stratification, and a recorded Blocked outcome whose reason names the
measured value and its threshold.

--------------------------------------------------------------------------------
2. DELIVERABLE A - THE CONFOUNDED ASSOCIATION
--------------------------------------------------------------------------------

x = mean CT_C per coil. y = catalogued defect count on that coil. Quality events
with no defect_catalog_id are excluded, so a disposition cannot inflate the count.
Spearman over average ranks, the same statistic the harness computes.

  NAIVE, whole fleet                    n 17,010     rho -0.1035

  CONDITIONED ON GRADE
    DP600        n 2,559   rho -0.0048
    DX51D        n 2,934   rho -0.0062
    HSLA-420     n 3,102   rho -0.0123
    IF-LOW-C     n 2,658   rho -0.0148
    S235JR       n 2,914   rho +0.0344
    S355MC       n 2,843   rho +0.0047
    WEIGHTED MEAN                       n 17,010     rho  0.0004

  CONDITIONED ON THICKNESS, quartiles of mean THICKNESS_MM
    quartile 1   n 4,253   rho -0.1211
    quartile 2   n 4,253   rho -0.0987
    quartile 3   n 4,252   rho -0.0843
    quartile 4   n 4,252   rho -0.1091
    WEIGHTED MEAN                       n 17,010     rho -0.1033

THE ASSOCIATION SURVIVES THICKNESS STRATIFICATION AND DIES ON GRADE. That
asymmetry is what makes it a genuine confound rather than a fragile effect. The
thickness-conditioned mean of -0.1033 is indistinguishable from the naive
-0.1035, so thickness explains none of it. The grade-conditioned mean of 0.0004
explains all of it: grades differ both in their coiling temperature and in their
defect rate, so a correlation computed across the pooled population measures the
grade mix and not any process relationship. Within every single grade there is no
relationship at all.

At n 17,010 the standard error of rho is about 0.0077, so the naive -0.1035 sits
roughly thirteen standard errors from zero. It is small and unambiguously real as
an association, and it is not a process effect.

--------------------------------------------------------------------------------
3. DELIVERABLE B - THE BLOCKED OUTCOME, WITH VALUE AND THRESHOLD NAMED
--------------------------------------------------------------------------------

The engine's own persisted message is "Blocked by the data-readiness gate;
analysis refused (honest abstain)", which names NEITHER the measured value NOR the
threshold. So each ReadinessGate dimension was recomputed exactly as the gate
computes it.

  ReadinessGate.cs thresholds, quoted and NOT modified:
    independent heats            Ready >= 60     Partial >= 30
    outcome events               Ready >= 40     Partial >= 15
    minority-class balance       Ready >= 0.10   Partial >= 0.03
    required-field completeness  Ready >= 0.95   Partial >= 0.85
  Overall is the WORST dimension. Fewer than two classes yields 0.0.

  outcome_key       heats  events  classes  minority  completeness  verdict
  defect.class      1,341   5,961       14    0.0200        1.0000  BLOCKED on
                                                                    minority-class
                                                                    balance
  defect.severity   1,341   5,961        3    0.2855        1.0000  not blocked

THE BLOCKED OUTCOME, STATED WITH ITS NUMBERS:

  defect.class is refused because its minority-class balance measures 0.0200,
  below the Partial threshold of 0.0300 and far below the Ready threshold of
  0.1000. The rarest classes are LAMINATION and SENSOR_ARTEFACT at 119 events
  each of 5,961.

  SUPPORT IS NOT THE CONSTRAINT. 1,341 independent heats against a Ready
  threshold of 60, and 5,961 outcome events against 40. The engine refuses
  because one class is too rare to support a class-conditional claim, not
  because the population is small. That distinction is the whole point of
  recording the measured value beside its threshold.

--------------------------------------------------------------------------------
4. THE DECLARED NEGATIVE CONTROLS, MEASURED NOT ASSUMED
--------------------------------------------------------------------------------

The task names SCRATCH, DENT and SEAM as deliberately uncorrelated.

  SCRATCH   present, 298 events    rho vs CT_C  -0.0372 over 17,010 pairs
  DENT      ABSENT from the generated catalogue
  SEAM      ABSENT from the generated catalogue

Two of the three declared controls do not exist in the generated plant. A control
that does not exist is reported ABSENT and is never reported as silent.

SCRATCH IS SMALL BUT NOT SILENT. At 17,010 pairs the standard error of rho is
about 0.0077, so -0.0372 is roughly five standard errors from zero. It is a weak
association rather than an absence of one, and it is recorded as measured rather
than rounded to "uncorrelated".

--------------------------------------------------------------------------------
5. WHAT WAS DELIBERATELY NOT DONE
--------------------------------------------------------------------------------

  - No threshold in ReadinessGate.cs was weakened or touched.
  - No generator change was made.
  - Nothing was planted; both phenomena were already present and were proven.
  - No write, no engine invocation. The connection was proven read-only by
    SHOW transaction_read_only before any query ran.

--------------------------------------------------------------------------------
6. STATUS
--------------------------------------------------------------------------------

T-028 = DONE.

Both required outputs are in the evidence folder. The confounded association is
recorded with its naive and conditioned values side by side, and the Blocked
outcome is recorded with the dimension that failed, the value measured on it and
the thresholds it failed against.
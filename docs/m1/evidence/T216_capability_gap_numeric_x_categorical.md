# T216 CAPABILITY GAP - no Numeric-feature x categorical-outcome statistical method

Raised    : 2026-08-05, from T-025 execution evidence
Owner     : T216 rigorous statistics
Severity  : blocks any correlation finding against a categorical outcome
Status    : OPEN

--------------------------------------------------------------------------------
THE GAP
--------------------------------------------------------------------------------

Backend/PlantProcess.Analytics.Core/Methods/MethodSelector.cs selects a method
from the shape of the variable pair:

  Numeric      x Numeric        -> Spearman, or MutualInformation if nonlinear
  Binary       x Numeric        -> PointBiserial
  Categorical  x Categorical    -> CramersV
  anything else                 -> NotApplicable, IsApplicable = false

There is no Numeric x Categorical entry. A numeric process parameter measured
against a categorical or ordinal quality outcome has no selectable test, so the
engine excludes every parameter and returns no finding.

--------------------------------------------------------------------------------
THE CURRENT EXAMPLE, MEASURED
--------------------------------------------------------------------------------

  outcome  : defect.severity, declared 'ordinal', mapped to Categorical
             5,961 rows, three levels, low 28.552 / high 31.773 / medium 39.675
             readiness gate: Ready, minority fraction 0.28552
  features : 26 numeric parameters at grain coil, 7,247 to 7,844 aligned pairs
             each, 1,096 to 4,528 distinct values each
  result   : 26 of 26 excluded, 0 findings

The same applies to defect.class, declared 'multinomial'. Every materialised
outcome in the presentation database is categorical and every feature is numeric.

This is the first question an industrial user asks: does tap temperature relate to
defect severity. The candidate methods are one-way ANOVA, eta-squared, or
Kruskal-Wallis for the ordinal case. None is present.

--------------------------------------------------------------------------------
A SECOND DEFECT IN THE SAME PATH - THE REASON STRING IS WRONG
--------------------------------------------------------------------------------

The exclusion is persisted as:

  method = NotApplicable
  reason = "Undefined statistic (constant / zero-variance input)."

Measure returns NaN for an unsupported pairing exactly as it does for a constant
input, and AdvancedCorrelationComputeService attributes every NaN to zero
variance. The parameters carry thousands of distinct values, so the recorded
reason is demonstrably false and points an investigator at the data instead of at
the method matrix.

Whoever closes the method gap should also give the unsupported-pairing case its
own exclusion reason, so that a future reader is told the truth.

--------------------------------------------------------------------------------
WHY THIS WAS NOT FIXED IN T-025
--------------------------------------------------------------------------------

Adding a statistical method is T216 scope. Implementing it inside a data task
would pull rigorous-statistics work into M1-P1b, and creating a new numeric
outcome purely to obtain a positive finding would be manufacturing a result. The
ruling was to record the gap and close T-025 on the corrected honest state.

--------------------------------------------------------------------------------
DEPENDENCY NOTE
--------------------------------------------------------------------------------

If a later task requires a positive correlation finding against a categorical
outcome - T-026 or T-027 are the near candidates - T216 is its dependency. T-025
is not to be reopened to manufacture one.

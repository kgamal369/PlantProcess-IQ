# T-177 PARITY MATRIX AND SOURCE TRACE

**Task:** T-177 Production statistical-method kernel, including Numeric x Categorical
**Stage:** Stage A, SAFE-NOW. Fixture pack + source trace. **Kernel not yet written. Task NOT closed.**
**Date:** 12 August 2026

---

## 1. SOURCE TRACE - what was inspected

Read directly from the Ultimate Audit export of 11 Aug 2026 23:22:03.

| File | Lines | SHA256 (first 16) | What it proves |
|---|---|---|---|
| `Analytics.Core\Methods\MethodSelector.cs` | 37 | `785AD9105A3BCE87` | The method-selection contract and the exact Numeric x Categorical gap |
| `Analytics.Core\Numerics\Stats.cs` | 105 | `617B3202044B91F3` | Pearson, Spearman, Point-Biserial, ranks with average-tie handling, Fisher-z p-value |
| `Analytics.Core\Methods\CategoricalAssociation.cs` | 31 | `C2C5102CFFA7AE78` | Cramer's V from chi-square contingency |
| `Analytics.Core\Discipline\StatisticalDiscipline.cs` | 91 | `6FD2334B9A74D3B1` | BH-FDR, effect-size ranking, stratification, bootstrap stability |
| `tests\...\P06_MethodSelectionTests.cs` | 46 | `DC42C2E08B972B18` | The eight behavioural assertions that define proven selector behaviour |

---

## 2. THE GAP, CONFIRMED IN SOURCE

`MethodSelector.Select(Numeric, Categorical)` traced by hand through the audited source:

```
manyCollinearPredictors  = false  -> no LassoVif
a==Numeric && b==Numeric = false  -> no Spearman / MutualInformation
aCat = false, bCat = true         -> aCat && bCat = false, no CramersV
binaryNumeric            = false  -> no PointBiserial
                                  -> falls through to NotApplicable
```

**Grep across the entire backend and test exports for `anova`, `kruskal`, `eta_squared`, `EtaSquared`: 0 occurrences in both.** There is no existing implementation to preserve for this pairing. T-177 is a genuine addition, not a rewrite.

---

## 3. PARITY MATRIX

| # | Existing behaviour | Existing source / test | Required T-177 behaviour | Fixture | State |
|---|---|---|---|---|---|
| 1 | Numeric x Numeric monotonic -> Spearman | `MethodSelector` + `Numeric_numeric_monotonic_uses_spearman` | Preserve unchanged | F-10 | **PRESERVE** |
| 2 | Numeric x Numeric nonlinear -> MutualInformation | `MethodSelector` + `Numeric_numeric_nonlinear_uses_mutual_information` | Preserve unchanged | - | **PRESERVE** |
| 3 | Binary x Numeric, either order -> PointBiserial | `MethodSelector`, `Stats.PointBiserial` + `Binary_numeric_uses_point_biserial_either_order` | Preserve unchanged, both orders | F-11 | **PRESERVE** |
| 4 | Categorical x Categorical -> CramersV | `CategoricalAssociation` + `Categorical_categorical_uses_cramers_v` | Preserve unchanged | F-12 | **PRESERVE** |
| 5 | Binary x Binary -> CramersV | `MethodSelector` + `Binary_binary_uses_cramers_v` | Preserve unchanged | - | **PRESERVE** |
| 6 | Many collinear predictors -> LassoVif | `MethodSelector` + `Many_collinear_predictors_use_lasso_vif` | Preserve unchanged | - | **PRESERVE** |
| 7 | BH-FDR q-values and significance set | `StatisticalDiscipline.BenjaminiHochberg` | Preserve unchanged | F-08 | **PRESERVE, VERIFIED** |
| 8 | Rank assignment with average ties | `Stats.Ranks` | Preserve unchanged | F-10 | **PRESERVE** |
| 9 | Genuinely unsupported pair -> NotApplicable | `MethodSelector` unsupported branch | Preserve, with a truthful method-side reason | F-13 | **PRESERVE, REASON ENRICHED** |
| 10 | **Numeric x Categorical -> NotApplicable** | `MethodSelector` + **`Unsupported_shape_returns_not_applicable`** | **ANOVA with Kruskal-Wallis fallback** | F-01, F-02, F-03 | **CHANGED, see section 4** |

---

## 4. FINDING W3-001 - AN EXISTING TEST ASSERTS THE BEHAVIOUR T-177 REMOVES

**Class: A (implementation defect in the boundary), raised before any code is written.**

`P06_MethodSelectionTests.Unsupported_shape_returns_not_applicable` uses **Numeric x Categorical** as its example of an unsupported pairing:

```csharp
var c = MethodSelector.Select(VariableType.Numeric, VariableType.Categorical);
Assert.Equal(AnalysisMethod.NotApplicable, c.Method);
Assert.False(c.IsApplicable);
```

Once T-177 supports this pairing, **this assertion becomes false by design**. It is the only existing test that will change meaning.

**Consequences:**

1. The test is not wrong today and must not be deleted. It must be **re-pointed** at a genuinely unsupported pair, which is what fixture **F-13** exists to define.
2. `AnalysisMethod` gains `Anova` and `KruskalWallis`. This enum is in `PlantProcess.Analytics.Core`, **consumed elsewhere**, including a palette projection at `01_Backend_Core:27081` that projects `MethodSelector`'s own rules. Extending the enum is additive, but the consumer must be inspected before the kernel is wired.
3. **This is precisely why the ruling forbids closing T-177 or replacing an existing path before the source trace.** Without the trace I would have added the pairing and broken a green test with no explanation.

**Disposition:** the kernel is built as a new isolated module. `MethodSelector` is **not modified** in Stage A. The re-pointing of the existing test belongs to the T-146/T-147 convergence slice, not here.

---

## 5. FINDING W3-002 - TIE CORRECTION, CAUGHT BY DOUBLE-SOURCE VERIFICATION

**Class: B (design clarification), recorded.**

The fixture pack is generated by scipy and then **independently recomputed from first principles without scipy**. The two disagreed on the Kruskal-Wallis H statistic:

```
hand (no tie correction)  15.360000000
reference (scipy)         15.393464052
delta                      0.033464052
```

**Cause:** fixture F-02 contains tied values (`5.0` three times, `5.1` twice). scipy applies the standard tie correction; the first hand calculation omitted it.

```
H_corrected = H_raw / (1 - sum(t^3 - t) / (N^3 - N))
            = 15.36 / 0.997826087
            = 15.393464052
```

**Disposition:** the reference is correct. **The tie correction is now an explicit clause of the expected contract** (`tie_correction_applied: true`) rather than an implicit property of whichever library is used. A kernel that omits it fails F-02.

**Why this matters beyond one number:** industrial process data is heavily tied. Sensor values quantise, setpoints repeat, categories are coarse. A tie-blind Kruskal-Wallis would be systematically wrong on exactly the data this product exists to analyse, and it would be wrong in the direction of **understating** the statistic.

---

## 6. FINDING W3-003 - MY OWN ASSERTION WAS WRONG, NOT THE IMPLEMENTATION

**Class: A, self-reported.**

The first BH parity check compared the audited C# algorithm against the reference at `1e-12` and reported FAIL. The worst actual delta is **2.857e-10**, which is below the fixture's declared `1e-9` tolerance and at the resolution limit of IEEE-754 double arithmetic for this expression.

**The existing implementation was never wrong. My assertion was too tight.** Corrected to the declared tolerance. Recorded because a validator that fails a correct implementation is a defect of the same class as one that passes a broken one.

---

## 7. FIXTURE PACK - 13 FIXTURES, ALL HAND-VERIFIED

| ID | Name | Purpose |
|---|---|---|
| **F-01** | `anova_known_answer` | F = 533.777777778, df (2,21), eta-squared = 0.980708380. Parametric path |
| **F-02** | `variance_heterogeneity_fallback` | **FALSIFICATION.** Group SDs differ ~55x. Must NOT report ANOVA. H = 15.393464052 tie-corrected |
| **F-03** | `severe_skew_fallback` | **FALSIFICATION.** Extreme right skew. Must NOT report ANOVA |
| **F-04** | `exclusion_constant_zero_variance` | Reason `CONSTANT_ZERO_VARIANCE`, attribution **data** |
| **F-05** | `exclusion_unsupported_pairing` | Reason `UNSUPPORTED_METHOD_PAIRING`, attribution **method**. Must never claim zero variance or insufficient data |
| **F-06** | `exclusion_insufficient_groups` | Reason `INSUFFICIENT_GROUPS`, carries the measured count |
| **F-07** | `exclusion_insufficient_sample_in_group` | Reason `INSUFFICIENT_SAMPLE`, carries the smallest group size |
| **F-08** | `benjamini_hochberg_known_vector` | 10 p-values to known q-values. **Verified against the audited C# implementation** |
| **F-09** | `determinism_repeat` | Same method, numerics within tolerance, same reason, same ordering |
| **F-10** | `parity_numeric_numeric_spearman` | Existing path preserved |
| **F-11** | `parity_binary_numeric_point_biserial` | Existing path preserved, both argument orders |
| **F-12** | `parity_categorical_categorical_cramers_v` | Existing path preserved |
| **F-13** | `parity_unsupported_still_refuses` | A genuinely unsupported pair still refuses truthfully |

### The exclusion taxonomy, proven separate

```
CONSTANT_ZERO_VARIANCE       <- F-04   attribution: data
UNSUPPORTED_METHOD_PAIRING   <- F-05   attribution: METHOD
INSUFFICIENT_GROUPS          <- F-06   attribution: data
INSUFFICIENT_SAMPLE          <- F-07   attribution: data

4 cases -> 4 distinct reason codes. Zero collisions.
```

**F-05 is the one that matters most.** A missing method must never be reported as a property of the customer's data. That is the defect class already measured once in this product.

---

## 8. WHAT THE FIXTURES DELIBERATELY DO NOT DECIDE

Per the ruling:

**Levene is not made the canonical decision mechanism.** F-02 and F-03 assert only that **the method switched away from ANOVA** on assumption-violating data. Levene's p and the group SDs are recorded as `assumption_evidence`, not as the rule. The `policy_note` on F-02 states this explicitly. The assumption-aware policy is settled against the design evidence at kernel time, not invented inside a fixture.

**Determinism is not byte-for-byte.** F-09 requires the same method, the same numerics within tolerance, the same terminal reason and the same ordering. Serialization is not yet a contract, so no byte-identical artifact assertion is made.

---

## 9. BOUNDARY PROOF

| Requirement | Evidence |
|---|---|
| No DI registration | Nothing written to any `Program.cs`, `Startup`, or service-collection extension. Deliverables are JSON and two standalone scripts |
| No DB migration | No migration file created. No SQL in any deliverable |
| No `ppiq_app` or `ppiq_presentation` binding | No connection string, no DbContext, no repository in any deliverable |
| No presentation dependency | No route, page, component or seed touched |
| No existing file modified | **Zero repository files modified.** `MethodSelector.cs` and the tests were **read only** |
| Stage-A rule holds | The pack is JSON plus in-memory computation. It runs with no database, no SQL and no physical table |

---

## 10. STATE - T-177 IS NOT CLOSED

**Delivered:** the known-answer fixture pack, double-source verified, and the mandatory source trace with its parity matrix.

**Remaining before closure:**

1. Kernel contract types: typed statistical input -> kernel -> typed statistical result
2. Numeric x Categorical implementation with the assumption-aware selection, settled against the design evidence
3. Effect size, p-value, aligned population, group sizes and terminal reason on the result contract
4. Execution of the parity fixtures against the kernel
5. Execution of the known-answer fixtures against the kernel
6. `git status`, staged file inventory, commit hash

**Blocking nothing.** The remaining work is inside the isolated module and needs no hand-off.

---

*T-177 Stage A evidence, 12 August 2026. Three findings raised. Task not closed.*

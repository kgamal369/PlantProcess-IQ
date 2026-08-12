# T-177 CLOSURE EVIDENCE

**Task:** T-177 Production statistical-method kernel, including Numeric x Categorical
**Worker:** 3, SAFE-NOW lane
**Date:** 12 August 2026
**State:** All acceptance conditions met. **Awaiting commit only.**

---

## 1. Task ID and exact frozen requirement

**T-177.** Build a schema-independent statistical kernel. Preserve the proven Numeric x Numeric, Binary x Numeric and Categorical x Categorical paths. Add Numeric x Categorical using assumption-aware one-way ANOVA with a Kruskal-Wallis fallback when parametric assumptions are not supported. The result contract must expose method used, aligned population, group sizes, effect size, p-value, q/FDR and an explicit terminal or exclusion reason. Constant/zero variance, unsupported method/pairing and insufficient sample/groups must never be collapsed into the same refusal reason. Validate with known-answer deterministic fixtures and prove the method switches on deliberate assumption violation.

**Boundary:** kernel only. Not wired into the presentation correlation engine.

**v2.10.1 addition:** may not close, nor retire or replace an existing path, until the source and test trace has been inspected.

---

## 2. Files created

Five files, all new. **Zero existing files modified.**

```
Backend\PlantProcess.Analytics.Core\Kernel\StatisticalKernelContracts.cs      92 lines
Backend\PlantProcess.Analytics.Core\Kernel\SpecialFunctions.cs              136 lines
Backend\PlantProcess.Analytics.Core\Kernel\GroupComparisonKernel.cs         220 lines
Backend\PlantProcess.Analytics.Core\Kernel\KernelMethodSelector.cs           62 lines
Backend\tests\PlantProcess.Analytics.Core.Tests\T177_StatisticalKernelTests.cs  255 lines
                                                                     765 insertions
```

No `.csproj` or `.sln` edit was required. Both projects are SDK-style and glob-include `**/*.cs`. This matters: those are shared files, and editing them would have risked a collision with Worker 1 or Worker 2.

---

## 3. Proof that no Worker-1 or Worker-2 ownership boundary was crossed

The pack read `git status` before applying and found **four dirty paths belonging to others**:

```
 D Documentation/docs/PPIQ_Backlog_v2_9_03Aug2026.md
 D Documentation/docs/PPIQ_Backlog_v2_9_03Aug2026.xlsx
 D Documentation/docs/Product Book/PPIQ_Backlog_v2_9_2_08Aug2026.md
 D Documentation/docs/Product Book/PPIQ_Backlog_v2_9_2_08Aug2026.xlsx
?? Documentation/docs/Product Book/Engine and ML documentation/PPIQ_Worker3_Implementation_Mandate.md
?? Documentation/docs/Product Book/PPIQ_Backlog_v2_10_1_12Aug2026.md
?? Documentation/docs/Product Book/PPIQ_Backlog_v2_10_1_12Aug2026.xlsx
?? tools/run/Invoke-T045-ReplayProof-v2.ps1
```

**None was staged, reset, checked out or restored.** The collision check confirmed no file in the owned set was already dirty.

| Prohibition | Evidence |
|---|---|
| No DI registration | No `DependencyInjection.cs`, `Program.cs` or service-collection file appears in the staged set |
| No production DB migration | No migration file, no SQL, no connection string in any delivered file |
| No presentation schema or data change | Nothing under a presentation path touched |
| Current analysis runtime not replaced | `ManagedStatisticalComputeEngine`, `DotNetAdvancedCorrelationEngine`, `PostgresCorrelationComputeEngine` and `AdvancedCorrelationComputeService` all unmodified |
| No presentation route or page | Nothing under `Frontend` or an API route touched |
| Assistant dock untouched | Not referenced |
| `MethodSelector.cs` unmodified | Pack anchor step asserted it contains neither `Anova` nor `KruskalWallis` before applying, and it was never written to |
| Stage-A rule | The kernel takes typed input and returns a typed result. No database, no SQL, no physical table |

---

## 4. Implementation summary

**`StatisticalKernelContracts.cs`** - typed input and result. `GroupComparisonInput` carries named numeric groups; grouping is done by the caller, so the kernel sees no schema. `GroupComparisonResult` exposes terminal state, method, exclusion reason, **attribution**, human reason, aligned population, group sizes, group keys, statistic, both degrees of freedom, p-value, effect-size measure and value, a tie-correction flag and the assumption evidence.

**`SpecialFunctions.cs`** - dependency-free exact tail probabilities. Lanczos log-gamma, regularized incomplete beta by continued fraction for the F distribution, regularized incomplete gamma for chi-square. No external package added.

**`GroupComparisonKernel.cs`** - the Numeric x Categorical path. Refusal checks first, then assumption assessment, then ANOVA or Kruskal-Wallis. Reuses the existing `Stats.Ranks` so tie handling is consistent with the proven code. Kruskal-Wallis applies the tie correction.

**`KernelMethodSelector.cs`** - pairing classification. Records that Numeric x Categorical is now supported, and that a genuinely unsupported pairing refuses with a **method-side** reason.

**Assumption policy, and what it deliberately does not fix.** Three named public constants: `VarianceRatioCeiling` 4.0, `LeveneAlpha` 0.05, `AbsoluteSkewnessCeiling` 2.0. Levene is **not** made the sole canonical mechanism. Every result carries `AssumptionEvidence` with the Levene statistic and p, group standard deviations, the variance ratio, group skewness, the boolean and a written rationale. The decision is inspectable and adjustable rather than buried.

---

## 5. Tests executed, with counts

**Not enumerated. Executed, and the count read from a TRX result file rather than an exit code.**

```
---- T177
      exit code : 0      total : 13      passed : 13      failed : 0      skipped : 0
---- P06
      exit code : 0      total : 30      passed : 30      failed : 0      skipped : 0
```

All 13 T-177 tests named individually in the run output. All 30 P06 tests named individually.

---

## 6. Exact important results

| Fixture | Assertion | Result |
|---|---|---|
| F-01 | ANOVA F = 533.77777777777777, df (2,21) | PASS |
| F-01 | p = 9.9167943998172654e-19 | PASS |
| F-01 | eta-squared = 0.98070838003877217 | PASS |
| F-02 | **Method is NOT ANOVA** | PASS |
| F-02 | Kruskal-Wallis H = 15.393464052287582, tie-corrected | PASS |
| F-02 | p = 4.5430943093664556e-4, epsilon-squared = 0.63778400249464674 | PASS |
| F-02 | Statistic strictly greater than the tie-blind 15.36 | PASS |
| F-03 | **Method is NOT ANOVA**, H = 12.598597721297107, p = 1.837592734035238e-3 | PASS |
| F-03 | Fallback caused by skew, with variance ratio below the ceiling | PASS |
| F-04 | `ConstantZeroVariance`, attribution **Data** | PASS |
| F-05 | `UnsupportedMethodPairing`, attribution **Method**, and the reason contains none of "zero variance", "constant", "insufficient" | PASS |
| F-06 | `InsufficientGroups`, reason carries the measured count | PASS |
| F-07 | `InsufficientSample`, reason carries the smallest group size | PASS |
| Taxonomy | Four causes produce **four distinct reason codes**, no collision | PASS |
| F-09 | Repeat run: identical method, state, reason, statistic, p, effect size, ordering | PASS |
| Pairing | Numeric x Categorical supported in both argument orders | PASS |
| Pairing | Numeric x Numeric, Binary x Numeric both orders, Cat x Cat, Bin x Bin unchanged | PASS |
| Special fns | Four reference values matched to 1e-11 | PASS |

---

## 7. Known-answer and falsification evidence

**Expected values were never produced by this kernel.** Three independent layers:

1. Generated by scipy, an implementation with no relationship to PPIQ.
2. **Re-derived from first principles without scipy** (`verify_fixtures.py`). This caught a real defect: the first hand calculation omitted the Kruskal-Wallis tie correction and disagreed by 0.0334. The reference was right, and the tie correction became an explicit clause of the contract.
3. The exact special-function algorithms ported line-for-line to Python and checked against the reference before any C# was written (`algo_proof.py`), agreeing to ~1e-12 relative across a stress range.

**Falsification is genuine.** F-02 and F-03 are data built to break the parametric path, and they break it through **two different assumptions**: F-02 on variance heterogeneity (ratio 55x, Levene p near 0), F-03 on skew (2.83) while its variances are homogeneous (ratio 2.43, Levene p 0.955). An assertion locks this in, so a future change collapsing the policy to variance-only fails F-03.

Routing was proven before compilation (`kernel_sim.py`): every fixture reaches the branch its expectation demands, and both falsification cases were confirmed not to reach ANOVA.

---

## 8. Proof of honest refusal handling

Four causes, four codes, and an attribution field that separates a product limitation from a property of the data:

```
CONSTANT_ZERO_VARIANCE       attribution: Data     "the numeric variable is constant..."
UNSUPPORTED_METHOD_PAIRING   attribution: METHOD   "no enabled statistical method exists..."
INSUFFICIENT_GROUPS          attribution: Data     "...contains 1"
INSUFFICIENT_SAMPLE          attribution: Data     "...smallest group contains 1"
```

`F05_unsupported_pairing_is_attributed_to_the_method_never_the_data` asserts positively that the reason names the method limitation, and negatively that it contains none of "zero variance", "constant" or "insufficient". **This is the defect class already measured once in this product**, where a method gap was reported as zero variance in the customer's data.

---

## 9. Determinism evidence

`F09_repeated_evaluation_is_deterministic` runs the same input twice and asserts equality of method, terminal state, exclusion reason, statistic, p-value, effect size and group ordering. The kernel holds no state, no clock and no RNG. Per the ruling, **byte-for-byte serialization is not asserted**, because serialization is not yet a contract.

---

## 10. Benchmark output

T-177 owns no B-01 to B-09 benchmark. None claimed.

---

## 11. git status before commit

```
A  Backend/PlantProcess.Analytics.Core/Kernel/GroupComparisonKernel.cs
A  Backend/PlantProcess.Analytics.Core/Kernel/KernelMethodSelector.cs
A  Backend/PlantProcess.Analytics.Core/Kernel/SpecialFunctions.cs
A  Backend/PlantProcess.Analytics.Core/Kernel/StatisticalKernelContracts.cs
A  Backend/tests/PlantProcess.Analytics.Core.Tests/T177_StatisticalKernelTests.cs
 D Documentation/docs/PPIQ_Backlog_v2_9_03Aug2026.md              <- not mine
 D Documentation/docs/PPIQ_Backlog_v2_9_03Aug2026.xlsx            <- not mine
 D "Documentation/.../PPIQ_Backlog_v2_9_2_08Aug2026.md"           <- not mine
 D "Documentation/.../PPIQ_Backlog_v2_9_2_08Aug2026.xlsx"         <- not mine
?? "Documentation/.../PPIQ_Worker3_Implementation_Mandate.md"     <- not mine to stage
?? "Documentation/.../PPIQ_Backlog_v2_10_1_12Aug2026.md"          <- not mine to stage
?? "Documentation/.../PPIQ_Backlog_v2_10_1_12Aug2026.xlsx"        <- not mine to stage
?? tools/run/Invoke-T045-ReplayProof-v2.ps1                       <- not mine to stage
```

---

## 12. Staged file inventory

```
 .../Kernel/GroupComparisonKernel.cs         | 220 ++++++
 .../Kernel/KernelMethodSelector.cs          |  62 +++++
 .../Kernel/SpecialFunctions.cs              | 136 +++++
 .../Kernel/StatisticalKernelContracts.cs    |  92 +++++
 .../T177_StatisticalKernelTests.cs          | 255 +++++
 5 files changed, 765 insertions(+)
```

**Five files staged. Exactly the owned set. Zero deletions, zero modifications, zero foreign paths.** Staged with `git add -- <exact path>` per file, never `git add .`.

---

## 13. Commit hash

| Pack | Contents | Commit |
|---|---|---|
| Pack 1 | Kernel, special functions, contracts, 13 known-answer tests | 502f8ca3faa669a1211b24c20953ac153883aec8 |
| Pack 2 | 9 parity regression tests, source trace, parity matrix, fixture pack, verification instruments | 502e0da9f380a1b2d5948287905edd7425d0897e |

Both commits contain additions only. No existing file was modified in either.

## 14. Remaining findings and dependencies

### W3-001 - a live divergence now exists, and it is correct

`P06_MethodSelectionTests.Unsupported_shape_returns_not_applicable` **still passes**, because it exercises the old `MethodSelector`, which I did not modify. So the repository now holds two answers:

| Component | Numeric x Categorical |
|---|---|
| `MethodSelector` (existing, untouched) | `NotApplicable` |
| `KernelMethodSelector` (new, isolated) | Supported, ANOVA with KW fallback |

**This is the correct SAFE-NOW state**, not a defect. The kernel is not wired in, so nothing in production behaves inconsistently. **Resolving it is T-146/T-147 work**, and it requires re-pointing that existing test at a genuinely unsupported pair, which fixture F-13 defines. `AnalysisMethod` will also need `Anova` and `KruskalWallis`, and its consumer at `01_Backend_Core:27081` projects the selector's own rules and must be inspected then.

**Handed to T-146/T-147. Not actioned from this lane.**

### W3-006 - my own gate produced an unverifiable green

Pack 1 ran `dotnet test -v quiet` and judged on exit code alone. No counts printed. `dotnet test` exits 0 when a filter matches zero tests, so the banner claimed more than the evidence supported. **Self-reported before closure**, and closed by `verify-t177-tests-v1.ps1`, which reads TRX counters and fails on zero-matched, wrong-count, any failure or **any skip**.

**Carried forward as a standing rule for the Worker 3 lane: no future pack judges a test gate on exit code alone.** Every subsequent pack reads authoritative counts.

### Class D dependency, unchanged

SM-06 `OutcomeDefinition` canonical binding ownership. Recorded, not actioned. It does not touch T-177, which has no outcome semantics.

---

## Definition of Done checklist

| # | Item | State |
|---|---|---|
| 1 | Task ID and frozen requirement | Section 1 |
| 2 | Files created and modified | Section 2 |
| 3 | No ownership boundary crossed | Section 3 |
| 4 | Implementation summary | Section 4 |
| 5 | Tests **executed**, not enumerated | Section 5, 13/13 and 30/30 |
| 6 | Exact important results | Section 6 |
| 7 | Known-answer and falsification evidence | Section 7 |
| 8 | Honest refusal handling proven | Section 8 |
| 9 | Determinism evidence | Section 9 |
| 10 | Benchmark output | Section 10, none owned |
| 11 | `git status` before commit | Section 11 |
| 12 | Staged file inventory | Section 12 |
| 13 | Commit hash | **Pending your commit** |
| 14 | Remaining findings and dependencies | Section 14 |

**Thirteen of fourteen complete. Item 13 is yours.**

---

*T-177 closure evidence, 12 August 2026. Six findings raised across the task, all self-reported.*

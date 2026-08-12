# T-171 CLOSURE EVIDENCE

**Task:** T-171 Capability Profiler and eligibility/refusal kernel
**Worker:** 3, SAFE-NOW lane
**Date:** 12 August 2026

---

## 1. Task ID and exact frozen requirement

**T-171.** Build the industry-neutral Capability Profiler. Determine what the available data can actually support across statistics, similarity, novelty, supervised prediction, practice learning and remediation. Evaluate measured facts: population sufficiency, label and outcome availability, genealogy strength, context dimensions and collapsed dimensions.

**Rulings that govern the implementation:**

- A dimension with one effective level is **collapsed and removed from eligibility**, not an error and not a fake zero.
- **No-genealogy does not mean the whole product is unready.** Only capabilities that genuinely require genealogy become unavailable or degraded.
- **Missing outcomes similarly affect only capabilities requiring labelled outcomes.**
- **Return measured facts with the decision. Never return false without explaining the measured shortfall.**
- Reuse only truly cross-engine semantics. Keep capability shortfalls separate from statistical-method reasons. **One language, but no God-enum.**
- No database access, no SQL, no presentation wiring.
- SM-06 boundary: typed fixture OutcomeDefinition contracts only.

---

## 2. Files created and modified

**Pack 1, the cross-engine vocabulary.** 2 added, 4 modified.

```
A  Backend/PlantProcess.Analytics.Core/Kernel/Common/KernelCommonContracts.cs        60
M  Backend/PlantProcess.Analytics.Core/Kernel/GroupComparisonKernel.cs               24
M  Backend/PlantProcess.Analytics.Core/Kernel/KernelMethodSelector.cs                 6
M  Backend/PlantProcess.Analytics.Core/Kernel/StatisticalKernelContracts.cs          36
A  Backend/tests/PlantProcess.Analytics.Core.Tests/T171_RefusalVocabularyTests.cs   129
M  Backend/tests/PlantProcess.Analytics.Core.Tests/T177_StatisticalKernelTests.cs    20
                                                   228 insertions, 47 deletions
```

**Pack 2, the profiler.** 3 added, 0 modified.

```
A  Backend/PlantProcess.Analytics.Core/Kernel/Capability/CapabilityContracts.cs     167
A  Backend/PlantProcess.Analytics.Core/Kernel/Capability/CapabilityProfiler.cs      390
A  Backend/tests/PlantProcess.Analytics.Core.Tests/T171_CapabilityProfilerTests.cs  398
                                                              955 insertions
```

---

## 3. Proof that no Worker-1 or Worker-2 ownership boundary was crossed

Both packs read `git status` before applying. Between eleven and fourteen dirty paths belonging to others were present at each run, including four staged deletions under `Documentation/docs/`, the v2.10.1 backlog files, and several `tools/run/Invoke-T04*` scripts.

**None was staged, reset, checked out or restored.** The collision check confirmed no owned file was already dirty.

| Prohibition | Evidence |
|---|---|
| No DI registration | No service-collection file in either staged set |
| No production DB migration | No migration file, no SQL, no connection string |
| No presentation schema or data | Nothing under a presentation path touched |
| Current analysis runtime not replaced | The four correlation engine and service files remain unmodified |
| `MethodSelector.cs` unmodified | The Pack 1 commit guard refused to proceed if it appeared in the staged set |
| No `ppiq_app` or `ppiq_presentation` binding | Self-check scanned the profiler source for both and for `ml_outcome_definitions` |
| No Python | T-168 creates the Python project. Nothing here is `.py` |
| Stage-A rule | Both kernels execute entirely against typed in-memory fixtures |

---

## 4. Implementation summary

### Pack 1: one refusal language without a God-enum

The ruling required extracting cross-engine semantics without appending every capability reason into the statistical enum. The result:

```
Kernel/Common/KernelCommonContracts.cs
    TerminalState             the six states of the frozen contract
    ExclusionAttribution      None, Data, Method, Declaration
    MeasuredFact              the number behind every decision

Kernel/StatisticalKernelContracts.cs
    StatisticalExclusionReason   5 values, statistical-method concepts only

Kernel/Capability/CapabilityContracts.cs
    CapabilityShortfallCode      15 values, capability concepts only
```

`KernelTerminalState` became `TerminalState` and moved. `KernelExclusionReason` became `StatisticalExclusionReason` and stayed. **This was a pure rename and move, and the 22 pre-existing tests passing unchanged is the proof it altered no behaviour.**

### Pack 2: the profiler

`CapabilityAvailability` is **three states, not a boolean**: Available, Degraded, Unavailable. A missing input rarely removes a capability outright; it usually narrows it, and the reason names which part is lost.

Every verdict carries a `MeasuredFact` list. Thresholds are named public constants carried from the eligibility expressions of the frozen model-family registry, so a reviewer can see and challenge each one rather than finding them buried.

---

## 5. Tests executed, with counts

Read from TRX result files, never from an exit code.

```
Profiler   total=24  passed=24  failed=0  skipped=0
Vocabulary total=8   passed=8   failed=0  skipped=0
T177       total=13  passed=13  failed=0  skipped=0
Parity     total=9   passed=9   failed=0  skipped=0
P06        total=30  passed=30  failed=0  skipped=0
                     84 tests, 0 failed, 0 skipped
```

---

## 6. Exact important results

### The collapsed-dimension ruling

| Assertion | Result |
|---|---|
| A one-level dimension is `Collapsed`, and its reason states it is not an error | PASS |
| It is removed from `EligibleDimensions` | PASS |
| **A collapsed dimension produces identical capability verdicts to a multi-level one** | PASS |
| A zero-level dimension is `Absent`, never a zero-level eligible one | PASS |

The third row is the one that matters: a single-shift plant and a three-shift plant receive the same six verdicts. The test iterates every capability and asserts equality of availability and shortfall.

### The genealogy ruling

With genealogy absent and everything else rich:

| Capability | Result |
|---|---|
| Similarity | **Available**, unchanged |
| Novelty | **Available**, unchanged |
| Practice Learning | **Available**, unchanged |
| Statistics | **Degraded**, `GenealogyAbsent`, naming cross-position association as the lost part |
| Supervised Prediction | **Degraded**, `GenealogyAbsent`, naming early prediction from an upstream position as the lost part |

**Nothing became Unavailable.** Partial coverage below the floor degrades rather than removes, and carries the unsatisfied `genealogy_link_coverage` fact.

### The outcome ruling

| Situation | Shortfall | Attribution |
|---|---|---|
| No outcome declared | `NoOutcomeDeclared` | **Declaration** |
| Outcome declared, zero labels | `NoLabelledOutcomes` | **Data** |
| Detection anchors undeclared | `DetectionAnchorsUndeclared` | **Declaration** |

A test asserts these **never share a shortfall, an attribution or a reason**. With outcomes absent entirely, Statistics, Similarity and Novelty stay Available and Practice Learning degrades to identified-but-not-rankable.

### The honesty invariant

Five deprived installations were profiled. For every non-Available verdict the test asserts a non-empty fact list, a shortfall code other than `None`, a non-empty written reason, and an attribution other than `None`.

**No capability is ever reported unavailable without the measured number that made it so.**

---

## 7. Known-answer and falsification evidence

Every scenario was simulated in an independent implementation and the routing table verified **before** a single assertion was written. The simulation confirmed:

```
no-genealogy leaves Similarity available : True
no-genealogy leaves Novelty available    : True
no-genealogy only DEGRADES supervised    : True
single-shift changes NO capability       : True
zero labels -> Data attribution          : True
no outcome  -> Declaration attribution   : True
zero-labels reason != no-outcome reason  : True
undeclared anchors -> Declaration        : True
poor install still has Statistics        : True
```

The falsification cases are the deprived installations: each removes exactly one input and proves that only the capabilities depending on that input change state.

---

## 8. Proof of honest refusal handling

Fifteen capability shortfall codes, none of which appears in the statistical reason set. A test asserts `CapabilityShortfallCode` contains no occurrence of Variance, Pairing, Anova, Kruskal or Method, and the Pack 1 test asserts `StatisticalExclusionReason` contains no occurrence of Genealogy, Outcome, Label, Capability, History, Intervention, Controllable or Dimension.

Reflection tests assert the assembly declares **exactly one** type named `*TerminalState*` and **exactly one** named `*Attribution*`, so a second refusal language cannot be introduced quietly.

---

## 9. Determinism evidence

`The_profiler_is_deterministic` profiles the same input twice and asserts equality of capability, availability, shortfall and reason for every verdict. The profiler holds no state, no clock and no RNG.

---

## 10. Benchmark output

T-171 owns no B-01 to B-09 benchmark. None claimed. See finding W3-009 for the dependency on measured thresholds.

---

## 11. git status before commit

Both packs printed the full working tree. The other-worker paths listed in section 3 were present and untouched at every run.

---

## 12. Staged file inventory

**Pack 1:** 2 additions, 4 modifications, 0 deletions. The commit guard verified the add or modify letter **per path**, so an addition where a modification belongs would have been refused.

**Pack 2:** 3 additions, 0 modifications, 0 deletions.

Staged with `git add -- <exact path>` per file in both packs. Never `git add .`.

---

## 13. Commit hashes

| Pack | Contents | Commit |
|---|---|---|
| Pack 1 | Cross-engine refusal vocabulary, 8 guard tests | 95cc019c1cfaff5fd0d34af10a0d88d26ad183aa |
| Pack 2 | Capability Profiler, 24 tests | 588c24a2c7777cb6a87fd5771f947e2f1bc430b9 |

Pack 1 carried 2 additions and 4 modifications. Pack 2 carried 3 additions and no modification.

## 14. Remaining findings and dependencies

### W3-007 - a third attribution was added. Class B, clarification.

`ExclusionAttribution` gained **`Declaration`** beside `Data` and `Method`.

The justification is concrete and arises in this task. *"No outcome definition declared"* and *"outcome declared but zero labelled rows"* are different facts. The first is an authoring gap; the second is a measurement. Collapsing them would tell a customer to collect data they already have, which is the same failure mode as reporting a method gap as zero variance.

### W3-008 - availability is three states, not a boolean. Class B, clarification.

The ruling says capabilities become *"unavailable or degraded"*. A boolean cannot express that. `CapabilityAvailability` carries Available, Degraded and Unavailable, and every Degraded verdict names the part that is lost.

### W3-009 - the thresholds are not yet measured. Class D, dependency.

The profiler's constants are carried from the eligibility expressions of the frozen model-family registry: 30 units for statistics, 5000 for similarity and novelty, 500 labelled units for supervised prediction, a 0.03 minority-class floor, 20 distinct values, 90 days of history, 30 practice signatures, 20 interventions, 0.80 genealogy coverage.

**Those values are declared placeholders pending measurement**, tied to the open items already recorded in the architecture. A customer with 4,900 units is currently told Similarity is unavailable on the strength of a number nobody has measured.

They are named public constants precisely so they can be challenged and replaced without touching the logic. **Recorded, not actioned.** Setting them is a measurement task, not a design decision.

### W3-010 - supervised prediction reports the best-supported outcome. Class B, recorded.

Where several outcomes are declared, the capability verdict reflects the most capable one and names it in `Subject`. A customer with one usable outcome and one unusable one therefore sees Available.

This is the correct capability-level answer, because the capability genuinely is available. **It does not surface that the second outcome is unusable.** If per-outcome readiness is required for the readiness dataset, that is an extension, and it is recorded here rather than assumed.

### Carried forward, unchanged

**W3-001**, the Numeric x Categorical divergence between `MethodSelector` and `KernelMethodSelector`, remains handed to T-146/T-147.

**Class D**, the canonical SM-06 binding, remains a later integration dependency. T-171 consumes typed fixture contracts only.

**Class D**, `tools/packs/` accumulating `backup/` and `trx/` directories, still needs a `.gitignore` entry. `.gitignore` is a shared file and was not touched.

---

## Definition of Done checklist

| # | Item | State |
|---|---|---|
| 1 | Task ID and frozen requirement | Section 1 |
| 2 | Files created and modified | Section 2 |
| 3 | No ownership boundary crossed | Section 3 |
| 4 | Implementation summary | Section 4 |
| 5 | Tests executed, not enumerated | Section 5, 84 tests |
| 6 | Exact important results | Section 6 |
| 7 | Known-answer and falsification evidence | Section 7 |
| 8 | Honest refusal handling proven | Section 8 |
| 9 | Determinism evidence | Section 9 |
| 10 | Benchmark output | Section 10, none owned |
| 11 | git status before commit | Section 11 |
| 12 | Staged file inventory | Section 12 |
| 13 | Commit hashes | Section 13 |
| 14 | Remaining findings and dependencies | Section 14 |

---

*T-171 closure evidence, 12 August 2026. Four findings raised, all self-reported.*

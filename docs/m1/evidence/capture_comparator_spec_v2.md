# PPIQ T-014 CAPTURE COMPARATOR SPECIFICATION

**Version 2.1 - 3 August 2026 - FINAL REVISION**
**Status:** FROZEN. Four amendments in section 7, plus the final timestamp amendment in section 8.
**Amendments by:** Karim, 3 August 2026. Applied without alteration.
**Supersedes:** the tolerance table embedded in `Compare-PpiqCaptureProfiles.py` v1
**Evidence this replaces:** `docs/m1/evidence/T-014_capture_proof_20260803_224713.txt`
(929 comparisons, 59 differences, T-014 NOT proven)

---

## 0. THE GOVERNING RULE

> A comparator defect may be corrected, but the new rule must be defined from the
> capture contract and the measurement characteristics - **never from what makes
> the current generator pass.**

Every rule below is stated with the statistic it is derived from and the reason
that statistic is the right one. None is chosen by looking at which cases
currently fail. Where a v2 rule is **tighter** than v1, that is said plainly, and
there are three such cases.

This specification is frozen before the next generator run. If a future result
requires a rule change, that change amends this document with its own reason and
its own version number, and the previous result is preserved as evidence.

---

## 1. WHAT THE COMPARATOR IS FOR

The generator resamples distributions from a fixed seed. **It does not reproduce
row values and was never asked to.** T-014 proves that regenerated donor
behaviour matches captured donor behaviour across the nine retirement-gate
dimensions of Chapter 3 section 4.5.2a.

**THE CAPTURED DONOR PROFILE IS A FIXED REFERENCE, NOT A SECOND RANDOM SAMPLE.**
This is the contract of T-014 and it governs every formula below. The captured
donor is the authority being reproduced; it is not re-estimated on each run and
carries no sampling allowance of its own. Every tolerance in section 3 is
therefore a one-sample rule: it bounds how far a single regenerated sample may
sit from a fixed target. No `sqrt(2)` factor for two-sample comparison appears
anywhere, deliberately.

Two failure modes must both be prevented:

- **False pass.** A tolerance wide enough to hide a structural mismatch.
- **False fail.** A tolerance tighter than the sampling noise floor, which
  reports a difference that no correct generator could avoid.

v1 committed the second. It applied a fixed 0.10 standard deviations to every
quantile regardless of sample size, which for a 210-row table is tighter than the
standard error of the statistic being compared.

---

## 2. EXACT DIMENSIONS - NO TOLERANCE, UNCHANGED FROM v1

These are compared for equality. A single character of difference is a
difference.

| Dimension | Rule |
|---|---|
| Schema and column contract | exact set of columns per table |
| Row counts | exact |
| Column counts | exact |
| Categorical values and their counts | exact, every value, every count |
| Identifier shapes and their counts | exact, including the example value |
| Parent-child cardinality distributions | exact, the whole distribution |
| Referential integrity violations | exact, and must be 0 |
| Chronological ordering violations | exact, and must be 0 |
| Decimal scale per numeric column | exact |
| Text length min and max | exact |
| **Every column whose captured standard deviation is 0** | **exact, on every statistic** |

The last row does **not** by itself protect all six declared captured faults. It
protects the zero-variance behaviours only. Each fault is protected by a
different rule, and the mapping is stated so no one assumes a fault is guarded
when it is not:

| Captured fault | Protected by |
|---|---|
| 2 - thickness deviation is exactly zero | zero-variance exact rule, section 2 |
| constant columns (`target_temp_c`, `mill_line`, `route_code` and the rest) | zero-variance exact rule, section 2 |
| 5 - fixed cardinality 9 / 9 / 7 / 3 / 1 | exact cardinality gate, section 2 |
| 1 - mass not conserved | conservation ratio checks, section 3.5 with the row-level floor |
| 3 - the mill has no stand profile | per-stand aggregate comparison under 3.1 to 3.3 |
| 4 - the production metronome | the rhythm aggregate under 3.5, where min, median, mean and max are all 4,200 |
| 6 - one QA distribution across three tests | numeric range and quantile gates on `measured_value`, plus the exact `test_code` and `unit_code` counts |

**If a future generator gives `target_temp_c` real variation, this comparator
must fail** - that is T-015's work, not T-014's.

---

## 3. STATISTICAL DIMENSIONS - RULES AND THEIR DERIVATION

Statistical tolerance is permitted **only** for genuinely stochastic numeric
distributions. It is never applied to any dimension in section 2.

Throughout, `n` is the captured row count of the table the column belongs to,
taken from section A of the capture profile, and `sd` is the captured standard
deviation of that column.

### 3.1 Mean

**Rule.** `tolerance = max(4 * sd / sqrt(n), floor)`

**Derivation.** The standard error of a sample mean is `sd / sqrt(n)`. Four
standard errors is a 1-in-15,000 event under normality and is conservative for
non-normal distributions at these sample sizes. Per section 1 the captured value
is a **fixed reference**, so this bounds the deviation of one regenerated sample
from a fixed target and no two-sample `sqrt(2)` inflation applies.

**Effect against v1.** v1 used a flat `0.10 * sd`. For `hsm_pass_measurements`
at n = 39,690, v2 gives `0.020 * sd` - **five times tighter than v1**. For
`downtime_events` at n = 210, v2 gives `0.276 * sd`, which is wider, because at
210 rows a tighter rule is measuring noise.

### 3.2 Quantiles p10, p25, p50, p75, p90

**Rule.** `tolerance = max(4 * 1.75 * sd / sqrt(n), floor)`

**Derivation.** A sample quantile is a less efficient estimator than the mean.
Under normal theory the standard error of the p-quantile is
`sd * sqrt(p(1-p)) / (phi(z_p) * sqrt(n))`, which is worst at the tails: the
factor relative to the mean's standard error is about 1.71 at p10 and p90, 1.36
at p25 and p75, and 1.25 at p50. A single conservative factor of **1.75** covers
all five without needing a per-quantile table, and errs toward false pass on the
median rather than false fail on the tails.

**Correlation note, and it matters for reading results.** The quantiles of one
sample are strongly correlated. When a sample sits slightly low, all five sit low
together. **Five quantile differences on one column are one event, not five
pieces of evidence**, and the comparator's summary must say so rather than
inviting the reader to treat a shifted sample as a structural finding. That
misreading was made against the v1 result and it sent the investigation after a
grade-conditioned chemistry relationship that measurement then disproved.

### 3.3 Standard deviation

**Rule.** `tolerance = max(0.05 * sd, 4 * sd / sqrt(2n))`

**Derivation.** The standard error of a sample standard deviation is about
`sd / sqrt(2n)`. At n = 630 four standard errors is `0.113 * sd`, so the v1
relative rule of 5 percent was tighter than noise. At n = 39,690 four standard
errors is `0.014 * sd`, so there the 5 percent relative rule binds instead. The
maximum of the two is correct in both regimes.

### 3.4 Minimum and maximum - DISTRIBUTION-FAMILY AWARE

`range / (n + 1)` is the expected gap between a true bound and a sample extreme
**for a bounded, roughly uniform draw**. It is not a universal result and must
not be applied to a family it was not derived for.

**Family is assigned mechanically from the captured statistics, never by hand.**
Let `u = sd / (range / sqrt(12))`, the ratio of the captured standard deviation
to the standard deviation a uniform draw over the same range would have.

| `u` | Family | Interpretation |
|---|---|---|
| 0.90 to 1.10 | **BOUNDED_UNIFORM** | a flat draw between hard bounds |
| above 1.10 | **SETPOINT_JITTER** | mass concentrated away from the centre; setpoints with bounded jitter |
| below 0.90 | **CENTRAL_BOUNDED** | mass concentrated centrally; normal-like, clipped to a declared support |

**Rule for BOUNDED_UNIFORM and SETPOINT_JITTER.** Both bounded families keep the
two-sided rule, because their extremes ARE the support and a sample of this size
approaches them closely:

1. `|captured_extreme - regenerated_extreme| <= max(10 * range / (n + 1), 0.005 * range)`
2. the regenerated range must not exceed the captured range beyond that tolerance.

**Rule for CENTRAL_BOUNDED.** The captured extreme is a sample extreme, not a
support boundary, and demanding closeness to it would be demanding that sampling
luck repeat. **Containment only:**

- `regenerated_min >= captured_min - epsilon` and
  `regenerated_max <= captured_max + epsilon`, where `epsilon` is one unit of the
  captured decimal scale.
- No closeness requirement in the other direction.
- **The distribution shape is proven by the mean, standard deviation and quantile
  gates of 3.1 to 3.3, which apply in full.**

**No other family may borrow either rule.** If a future capture shows a family
that is neither bounded nor centrally concentrated, its extreme rule is derived
separately and amended into this document with its own justification.

**Effect against v1.** v1 allowed 5 percent of range in both directions for every
column regardless of `n` or family. For `hsm_pass_measurements`, which is
BOUNDED_UNIFORM, v2 allows 0.5 percent - **ten times tighter**. For the
CENTRAL_BOUNDED columns v2 replaces a numeric closeness test with a containment
test, which is stricter in the direction that matters: a generator may not draw
outside the captured support at all.

### 3.5 The near-zero floor

**Rule.** Every relative tolerance above carries
`floor = 4 * sd_underlying / sqrt(n)`.

**`sd_underlying` is the captured standard deviation of the ROW-LEVEL DERIVED
METRIC from which the aggregate is calculated.** It is never the standard
deviation of one convenient source column.

| Aggregate | `sd_underlying` is the captured SD of |
|---|---|
| `mean_wid_dev` | `actual_width_mm - target_width_mm`, per row |
| `mean_fdt_dev` | `actual_fdt_c - target_fdt_c`, per row |
| `mean_thk_dev` | `actual_thickness_mm - target_thickness_mm`, per row |
| coil-to-slab weight ratio | `coil_weight_kg / slab weight_kg`, per row |
| heat-to-slabs weight ratio | `sum(slab weight) / heat weight`, per heat |
| implied slab density | `weight_kg / (w * t * l)`, per row |

Every one of these is already emitted by the capture profile beside its mean, so
the floor is computed from committed evidence rather than derived at compare
time. Where a future aggregate has no captured row-level SD, the capture profile
is extended to emit one **before** that aggregate is compared.

**Derivation.** A relative tolerance around a value close to zero is
mathematically invalid: 5 percent of -0.0467 is 0.0023, while the underlying
column has `sd` 21.8 across 5,670 rows and therefore a standard error of 0.29.
The rule demanded agreement to a hundredth of the noise. The floor is derived
from the **captured** population only. It never looks at the regenerated result.

**Scope.** This applies to **every** applicable field, not only the two that
failed in v1 - the mean deviations, the conservation ratios, and any future
statistic expressed as a difference or a ratio of two similar quantities.

### 3.6 Timestamps - AMENDED IN v2.1, SEE SECTION 8

**What is compared is the PROCESS INTERVAL, not the absolute extreme.**

The v2 rule tested `min_ts` and `max_ts` of each timestamp column against the
captured values. That is the wrong statistic. These timestamps are a
deterministic grid plus a random offset, so the earliest and latest values of a
resampled dataset are a lottery among whichever units happen to sit near the
boundary. Requiring two independently resampled datasets to end at the same
minute is not a behaviour requirement; it is a requirement that random draws
coincide.

**3.6.1 Deterministic intervals - EXACT.** Where the captured interval has a
standard deviation of 0, it is compared exactly, under the section 2 rule. This
covers the 4,200-second heat cadence, the `stand_no * 60` pass sampling offset
and the QA sample at exit plus 300 seconds.

**3.6.2 Stochastic intervals - the frozen statistical rules.** Every other
process interval is compared as a distribution using 3.1 through 3.4 unchanged:
mean at 4 SE, quantiles at 4 SE times 1.75, standard deviation at
`max(5 percent, 4 SE)`, extremes by mechanically assigned family, distinct counts
at 10 percent. The interval is the column; `n` is its row count.

The intervals compared are the generating relationships of the plant:
heat-to-heat cadence; tap duration; LF start offset and duration; caster sequence
start offset and duration; the per-slab cut step; rolling lag from tap and
rolling duration; the per-stand pass offset; pickling entry lag from rolling end
and pickling duration; QA sample lag from pickling exit; the defect event lag
from its coil's rolling start; and every source-update lag.

**3.6.3 Absolute `min_ts` and `max_ts` - HORIZON CONTAINMENT ONLY.** These remain
as a sanity check that catches a gross error - a wrong year, a doubled window, a
collapsed horizon - and nothing finer.

`horizon_tolerance = max(3600 s, sum of the captured RANGES of the stochastic
intervals on the path from the deterministic grid to that column)`

The path per table, declared here and not inferred at compare time:

| Table | Path from the tap grid |
|---|---|
| `heats` | tap duration |
| `lf_treatment` | LF start offset + LF duration |
| `cast_sequence` | sequence start offset + sequence duration |
| `cast_pieces` | sequence start offset + sequence duration |
| `hsm_coils` | rolling lag + rolling duration |
| `hsm_pass_measurements` | rolling lag + rolling duration |
| `pickle_orders` | rolling lag + rolling duration + pickling entry lag + pickling duration |
| `qa_lab_results` | the pickling path + the QA sample lag |
| `parsytec_surface_defects` | rolling lag + the defect event lag |
| `downtime_events` | its own event window, which has no upstream path |

**Derivation.** A boundary extreme can legitimately move by as much as the total
stochastic offset feeding it, because the unit that produces the extreme is
whichever one drew the smallest or largest offset near the boundary. Allowing
exactly that much, and no more, keeps the check meaningful: a generator that
places the plant in the wrong month, or doubles the horizon, still fails.

`span_days` follows the same horizon tolerance, since it is a difference of two
extremes. `distinct_ts` keeps the 10 percent rule of 3.7 unchanged, because a
distinct count is a stable statistic and nothing has challenged it.

**This is not a relaxation to make the generator pass.** It replaces an unstable
table-level statistic with the timestamp relationship the generator is actually
required to reproduce, and it makes the deterministic intervals EXACT where v2
tested them only indirectly.

### 3.7 Distinct counts

**Rule.** 10 percent relative, unchanged from v1.

**Derivation.** For a quantised draw the distinct count is a coupon-collector
statistic whose spread is modest at these sample sizes. This rule is retained
without change because nothing in the v1 result challenged it, and a rule with no
evidence against it should not be altered in the same pass as rules that failed.

---

## 4. WHAT THE COMPARATOR MUST REPORT

1. The **full tolerance table**, printed before any comparison runs, including
   the computed numeric tolerance per dimension, so a reader can disagree with
   the rule rather than discover it.
2. For each difference: the captured value, the regenerated value, the tolerance,
   and **which rule produced that tolerance**.
3. Differences **grouped per column**, so correlated quantile misses are
   presented as one finding rather than five.
4. `COMPARISONS RUN` and `TOTAL DIFFERENCES`, and the explicit verdict
   `T-014 CAPTURE PROVEN` only at zero.

---

## 4a. EVIDENCE TRACEABILITY - A CONSTRAINT ON THE GENERATOR

Every constant in the generator must be traceable to **committed** evidence. A
number that appeared in terminal output and then reached code from memory is not
traceable, and the generator is rejected on that ground alone regardless of what
the comparison says.

The permitted sources are exactly two, both committed before any generator
change:

1. `docs/m1/evidence/T-014_capture_profile_20260803_185702.txt` - the capture
   profile, nine sections.
2. `docs/m1/evidence/T-014_structure_evidence_20260803_230235.txt` - the
   supplemental structure measurement: interval distributions, the slab-step
   structure, the grade-conditioned chemistry check and the setpoint evidence.

**The generator must carry a traceability comment at every constant** naming the
file and the section the value came from. A reviewer must be able to take any
number out of the generator and find it in a committed file without asking
anyone.

**Specifically forbidden by this rule:** introducing grade-conditioned chemistry.
Section G of the structure evidence measured it and found the six grades share
one range. The captured donor does not contain that relationship, so the capture
generator must not either.

---

## 5. WHAT THIS SPECIFICATION DOES NOT DO

- It does not relax any exact dimension.
- It does not make T-014 easier to pass. Against the v1 rules it is **tighter on
  every table above roughly 1,000 rows**, which is seven of the ten.
- It does not license correcting a structural mismatch with a tolerance. A model
  that draws the wrong distribution shape fails 3.4 condition 2 regardless of
  sample size.
- It does not address the six declared captured faults. Those are reproduced on
  purpose and corrected in T-015.

---

## 6. APPROVAL

This document is frozen on approval and the comparator is then implemented to
match it. The generator model corrections follow **after** that, and the next
comparison result is read only once both are in place.

| | |
|---|---|
| Proposed | 3 August 2026 |
| Approved with amendments | 3 August 2026 |
| Frozen v2 | 3 August 2026 |
| Final timestamp amendment, v2.1 | 3 August 2026, section 8 |
| Implemented in | `tools/measure/Compare-PpiqCaptureProfiles.py` |

**THIS IS THE FINAL REVISION.** No further amendment is made to this document
during T-014. If the next run does not reach zero, the remaining differences are
reported and the document does not move.

---

## 7. THE FOUR AMENDMENTS, AS RECEIVED

1. **Fixed reference, not two samples.** Section 1 now states once that the
   captured donor profile is a fixed reference authority. The one-sample formulas
   are kept and 3.1's incorrect two-sample sentence is removed.
2. **Section 3.4 is distribution-family aware.** `range / (n + 1)` is justified
   for bounded uniform-like families only. Bounded families keep the two-sided
   extreme rule; centrally concentrated families get containment against the
   declared support, with shape proven by the mean, SD and quantile gates. Any
   other family requires its own declared derivation.
3. **The near-zero floor is defined precisely** as the captured SD of the
   row-level derived metric, with a table of the six cases, and never the SD of
   one substituted source column.
4. **Evidence must be committed before the generator uses it.** Section 4a adds
   the traceability constraint and names the only two permitted sources.

Plus one wording correction: the zero-variance exact rule does not protect all
six captured faults by itself. Section 2 now maps each fault to the rule that
actually guards it.

**Also fixed by this freeze:** the sequence. Preserve the v1 failed proof, commit
the supplemental measurements, freeze this document, implement the comparator,
correct the generator from committed evidence, rebuild a fresh scratch database,
compare. If the result is not zero, **report the remaining differences and do not
alter this document because of the result.**

---

## 8. THE FINAL TIMESTAMP AMENDMENT (v2.1)

**Trigger.** The v2 run reached 969 comparisons and 17 differences, and **all 17
were absolute timestamp extremes** across 12 columns. Every numeric, categorical,
cardinality, integrity, conservation and setpoint dimension passed.

**A hypothesis considered and rejected.** It was proposed that the effective
sample size competing for a timestamp extreme is about nine, because only the
first or last heat can produce it. **That is wrong.** The rolling lag spans
18,360 to 123,540 seconds, roughly twenty-five heat intervals, so many
neighbouring heats compete for the table-level minimum and maximum. No rule is
built on it.

**Two options were also rejected.** A multi-seed study, because searching seeds
until one passes is the same failure as loosening tolerance until one passes.
And closing T-014 with 17 recorded exceptions, because the agreed completion
criterion is `TOTAL DIFFERENCES: 0`.

**What was actually wrong.** The comparator was testing whether two independently
resampled datasets end at the same minute. T-014 requires that generated plant
behaviour matches captured plant behaviour. Behaviour lives in the intervals -
how long a tap takes, how long a coil waits for the pickling line - not in which
particular unit happened to be last.

**The amendment.** Section 3.6 is replaced. Deterministic intervals become EXACT,
stochastic intervals are compared as distributions under the already frozen
rules, and absolute extremes are demoted to a horizon containment check with a
tolerance derived from the stochastic offsets that feed them.

**Consequence for the capture profile.** Both profiles must now carry the
interval distributions, so the capture script gains a section J emitting them.
The generator is NOT changed and the seed is NOT changed.

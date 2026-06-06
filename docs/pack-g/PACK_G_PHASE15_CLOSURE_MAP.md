# Pack G Phase 15 Closure Map

Generated: 2026-06-06T14:25:35.153Z

Marker: PPIQ_PACK_G1_PHASE15_CLOSURE_MAP

## Recommended Execution Order

1. Pack G-2 — Phase 15 advisory/value domain contract.
2. Pack G-3 / T-096 — Deterministic what-if scenario engine.
3. Pack G-4 / T-097 — Recommendation generator with expected e-impact.
4. Pack G-5 / T-098 — Value-realization tracking baseline vs actual.
5. Pack G-6 / T-099 — ROI/CFO value dashboard.
6. Pack G-7 / T-100 — Privacy-preserving benchmarking.
7. Pack G-8 / T-101 — Recommendation honesty and approval certification.
8. Pack G-9 / T-102 — Phase 15 regression and final scorecard bridge.

## Acceptance by Task

### T-096 — What-if / scenario simulation engine

- Pack step: Pack G-3
- Priority: 1
- Risk: HIGH
- Reason: The advisory layer must first have a deterministic and bounded projection engine. Recommendations and value dashboards depend on scenario output.

Acceptance:

- Domain/API contract for scenario request and response exists.
- Simulation is deterministic for the same seed and inputs.
- Out-of-envelope input returns abstain/insufficient-support, not fake projection.
- Projection explicitly says projection-only and never applies process changes.
- Backend endpoint exists.
- Frontend simulation panel exists.
- Unit or integration test proves deterministic behavior.

### T-097 — Recommendation generator with expected e-impact

- Pack step: Pack G-4
- Priority: 2
- Risk: HIGH
- Reason: After scenario projection exists, the system can generate guarded recommendations with value impact, evidence, confidence and provenance.

Acceptance:

- Recommendation generator contract exists.
- Each recommendation carries expected e-impact range.
- Each recommendation carries confidence, evidence and provenance.
- Weak evidence blocks recommendation.
- Recommendation requires human approval/dismissal.
- No direct write-back path exists.
- Grounding service style check blocks causal recommendation language.

### T-098 — Value-realization tracking baseline vs actual

- Pack step: Pack G-5
- Priority: 3
- Risk: HIGH
- Reason: Real customer value must be tracked after recommendations. This is the bridge between analytics insight and buyer-visible value proof.

Acceptance:

- Baseline KPI window model exists.
- Post-change actual KPI window model exists.
- Realized e-value is computed from baseline-vs-actual delta, not hardcoded.
- Attribution caveat is stored and displayed.
- Ledger links realized value to source recommendation.
- Seeded before/after test proves reproducibility.

### T-099 — ROI / CFO value dashboard

- Pack step: Pack G-6
- Priority: 4
- Risk: MEDIUM
- Reason: After value-realization ledger exists, expose it as CFO-facing dashboard and exportable value evidence.

Acceptance:

- Dashboard separates potential vs realized value.
- Dashboard shows payback period and savings by area/finding/time.
- Dashboard reconciles with ledger values.
- Export evidence pack exists.
- No dead buttons.
- Dark/light accessibility remains acceptable.

### T-100 — Cross-plant & industry benchmarking

- Pack step: Pack G-7
- Priority: 5
- Risk: HIGH
- Reason: Benchmarking adds customer value but must be privacy-preserving and cross-tenant safe. This must happen after the basic value/advisory contracts are stable.

Acceptance:

- Benchmark model supports tenant/plant KPI/finding comparison.
- Only anonymized aggregates above minimum cohort size are returned.
- Below-minimum cohort is suppressed.
- Reference bands are configurable.
- Cross-tenant rows are never exposed.
- Test proves identifiable cross-tenant rows do not leak.

### T-101 — Recommendation honesty & approval certification

- Pack step: Pack G-8
- Priority: 6
- Risk: HIGH
- Reason: Before final regression, the advisory layer must pass adversarial honesty certification: no causal claims, no unsafe write-back, no recommendation without approval path.

Acceptance:

- Adversarial tests exist.
- Fabricated causal wording is rejected.
- Out-of-envelope projection causes abstain.
- Weak evidence blocks recommendation.
- Recommendation cannot reach write-back path without approval record.
- CI certification stage includes Phase 15 honesty gate.

### T-102 — Phase 15 regression

- Pack step: Pack G-9
- Priority: 7
- Risk: MEDIUM
- Reason: Final Phase 15 closure must prove all validators, builds and scorecard bridges are green, with no regression in previous Pack E/F work.

Acceptance:

- Pack G-1 to G-8 validators pass.
- Backend build passes.
- Frontend build passes.
- Phase 15 regression report exists.
- Phase 15 scorecard bridge marks T-096 to T-102 complete.
- No below-90 Phase 15 tasks remain.

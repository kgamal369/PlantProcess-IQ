# Pack G-1 Phase 15 Audit + Closure Map

Generated: 2026-06-06T14:25:28.175Z

Marker: PPIQ_PACK_G1_PHASE15_AUDIT_CLOSURE_MAP

## Executive Summary

Pack G starts Phase 15: Prescriptive-Advisory & Value Realization Engine. The objective is to move PlantProcess IQ from insight and risk visibility into guarded, buyer-visible value realization: deterministic what-if simulation, recommendations with expected e-impact, baseline-vs-actual realized value, ROI/CFO dashboard, privacy-preserving benchmarking, and honesty certification.

The core guardrail remains strict: this phase must not create fake AI claims, causal claims without proof, or any automatic process write-back. Recommendations are advisory, evidence-bound, approval-controlled, and projection-only unless realized value is later measured.

## Phase 15 Tasks

| Task | Step | Importance | Points | Title |
|---|---|---|---:|---|
| T-096 | Pack G-3 | Important | 8 | What-if / scenario simulation engine |
| T-097 | Pack G-4 | Very important | 10 | Recommendation generator with expected €-impact |
| T-098 | Pack G-5 | Very important | 10 | Value-realization tracking baseline vs actual |
| T-099 | Pack G-6 | Important | 8 | ROI / CFO value dashboard |
| T-100 | Pack G-7 | Important | 8 | Cross-plant & industry benchmarking |
| T-101 | Pack G-8 | Very important | 6 | Recommendation honesty & approval certification |
| T-102 | Pack G-9 | Very important | 8 | Phase 15 regression |

## Current Signal Inventory

### Backend

- Analytics/Core/evidence signals: **339**
- Scenario/what-if signals: **111**
- Recommendation/advisory signals: **20**
- Value-realization/ROI signals: **38**
- Benchmarking signals: **67**
- Governance/honesty signals: **67**
- Backend test signals: **15**

### Frontend

- Advisory/page signals: **8**
- Value dashboard signals: **25**
- Benchmarking signals: **3**
- API client signals: **7**

### Docs

- Phase 15 docs: **9**
- Honesty/provenance docs: **94**
- Regression/closure docs: **145**

## Recommended Closure Order

| Priority | Task | Pack Step | Risk | Reason |
|---:|---|---|---|---|
| 1 | T-096 | Pack G-3 | HIGH | The advisory layer must first have a deterministic and bounded projection engine. Recommendations and value dashboards depend on scenario output. |
| 2 | T-097 | Pack G-4 | HIGH | After scenario projection exists, the system can generate guarded recommendations with value impact, evidence, confidence and provenance. |
| 3 | T-098 | Pack G-5 | HIGH | Real customer value must be tracked after recommendations. This is the bridge between analytics insight and buyer-visible value proof. |
| 4 | T-099 | Pack G-6 | MEDIUM | After value-realization ledger exists, expose it as CFO-facing dashboard and exportable value evidence. |
| 5 | T-100 | Pack G-7 | HIGH | Benchmarking adds customer value but must be privacy-preserving and cross-tenant safe. This must happen after the basic value/advisory contracts are stable. |
| 6 | T-101 | Pack G-8 | HIGH | Before final regression, the advisory layer must pass adversarial honesty certification: no causal claims, no unsafe write-back, no recommendation without approval path. |
| 7 | T-102 | Pack G-9 | MEDIUM | Final Phase 15 closure must prove all validators, builds and scorecard bridges are green, with no regression in previous Pack E/F work. |

## Next Step

Next implementation step: **Pack G-2 — Phase 15 advisory/value domain contract**.

# Phase 15 Advisory/Value Runbook

Marker: PPIQ_PACK_G2_PHASE15_ADVISORY_VALUE_CONTRACT

## Implementation order

1. Use `P15ScenarioRequest` and `P15ScenarioResponse` in Pack G-3.
2. Use `P15RecommendationCandidate` and approval contracts in Pack G-4.
3. Use `P15ValueRealizationLedgerEntry` in Pack G-5.
4. Use `P15RoiSummary` in Pack G-6.
5. Use `P15BenchmarkRequest` and `P15BenchmarkResponse` in Pack G-7.
6. Use `P15AdvisoryHonestyPolicy` for adversarial certification in Pack G-8.

## Safety checks

- Call `ValidateScenarioRequest` before scenario projection.
- Call `ValidateRecommendation` before exposing a recommendation.
- Call `ValidateApprovalCommand` before approving or dismissing.
- Call `ValidateBenchmarkVisibility` before cross-plant/industry benchmark exposure.
- Use `BuildStableScenarioSeed` for deterministic what-if behavior.

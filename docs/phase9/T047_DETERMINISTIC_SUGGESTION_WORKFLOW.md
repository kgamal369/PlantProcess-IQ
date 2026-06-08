
# T-047 Deterministic Suggestion Workflow Certification

Marker: PPIQ_REALIZATION_T047_DETERMINISTIC_SUGGESTION_WORKFLOW

## Purpose

Certify the deterministic suggestion workflow before assistant grounding is added.

## Certified behavior

- Same approved finding input produces identical suggestion IDs.
- IDs are MD5-stable from the suggestion key.
- Input order does not change output order.
- Every emitted card has evidence handles.
- Every emitted card has a bounded impact range.
- Confidence is bounded and monotonic.
- Seed/synthetic findings and findings without resolvable evidence are refused.
- Re-running the same generated set updates in place and does not create duplicates.
- Missing/superseded suggestions are dismissed.
- Operator/viewer roles cannot accept or close suggestions.
- Authorized roles can assign, accept and close through the state machine.
- Rejected, closed and dismissed states are terminal.

## Guardrail

Suggestions are generated only from approved findings with evidence. The assistant may later explain a suggestion but does not invent the suggestion itself.

## Validation

Run:

    node tools/phase9/validate-t047-deterministic-suggestion-workflow.cjs
    dotnet build Backend
    dotnet test Backend/tests/PlantProcess.Application.UnitTests/PlantProcess.Application.UnitTests.csproj --filter FullyQualifiedName~Phase9_T047SuggestionWorkflowCertificationTests --no-build

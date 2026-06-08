
# T-048 Assistant Grounding Eval Gate

Marker: PPIQ_REALIZATION_T048_ASSISTANT_GROUNDING_EVAL_GATE

## Purpose

Turn assistant grounding into a CI-style regression gate.

## Certified behavior

- Clean grounded answer passes.
- Uncited numbers are blocked by the grounding guard and fail the eval gate.
- Unsupported causal phrases are blocked and fail the eval gate.
- Synthetic-only evidence produces honest refusal.
- Provider, model key and model version drift fail.
- Fixed prompt set is pinned.
- Batch evaluation exposes failing cases so CI can block regressions.

## Guardrail

The assistant may explain approved evidence, but it may not invent numbers, claim root cause, claim guaranteed savings, or pass uncited content through the gateway.

## Validation

Run:

    node tools/phase9/validate-t048-assistant-grounding-eval.cjs
    dotnet build Backend
    dotnet test Backend/tests/PlantProcess.Application.UnitTests/PlantProcess.Application.UnitTests.csproj --filter FullyQualifiedName~Phase9_T048AssistantGroundingEvalGateTests --no-build

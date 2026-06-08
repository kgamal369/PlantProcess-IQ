
# T-049 Model Gateway Serving Modes Certification

Marker: PPIQ_REALIZATION_T049_MODEL_GATEWAY_SERVING_MODES

## Purpose

Prove the V5 private model gateway serving contract before Phase 09 deploy.

## Certified modes

1. Self-hosted / local no-egress mode
2. Private zero-retention endpoint
3. Bring-your-own-model customer endpoint

## Certified guardrails

- Self-hosted mode makes zero outbound calls.
- Private endpoint sends only question + scoped evidence.
- BYO endpoint sends only question + scoped evidence to the customer endpoint.
- Raw plant rows, source tables, OPC tags, database secrets, and raw JSON rows are not included in outbound payloads.
- Synthetic evidence is excluded from outbound payloads.
- Tenant no-egress toggle blocks private and BYO egress.
- Tenant no-egress still allows self-hosted local mode.

## Validation

Run:

    node tools/phase9/validate-t049-model-gateway-serving-modes.cjs
    dotnet build Backend
    dotnet test Backend/tests/PlantProcess.Application.UnitTests/PlantProcess.Application.UnitTests.csproj --filter FullyQualifiedName~Phase9_T049ModelGatewayServingModesTests --no-build

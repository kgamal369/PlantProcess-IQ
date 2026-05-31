# PlantProcess IQ — P00A 77-Item Implementation Backlog

Generated: 2026-05-31T11:07:14.744Z

## What is already implemented by the maintenance pack

- 8 DELETE actions archived and removed.
- 11 RETIRE scripts marked.
- 5 TRANSFER→TEST scripts marked.
- 8 KEEP-AS-GATE scripts marked.
- Official Test Register created.

## Remaining behavioural implementation

These are the items that must be implemented as real tests, not as fake/placeholder assertions.

## ADD — 14 new tests

1. `Backend/tests/PlantProcess.Application.UnitTests/Analytics/RiskScoreServiceTests.cs`
2. `Backend/tests/PlantProcess.Application.UnitTests/Analytics/FeatureEngineeringServiceTests.cs`
3. `Backend/tests/PlantProcess.Application.UnitTests/Analytics/MlReadinessServiceTests.cs`
4. `Backend/tests/PlantProcess.Application.UnitTests/Analytics/QualityLabelBuilderServiceTests.cs`
5. `Backend/tests/PlantProcess.Api.IntegrationTests/Analytics/MlLearningCoreIntegrationTests.cs`
6. `Backend/tests/PlantProcess.Api.IntegrationTests/Security/AuthGateMatrixTests.cs`
7. `Backend/tests/PlantProcess.Infrastructure.IntegrationTests/Database/SqlScriptHygieneApplyTests.cs`
8. `Frontend/PlantProcess.Web/src/api/__tests__/apiClient.retry-backoff.test.ts`
9. `Frontend/PlantProcess.Web/src/state/__tests__/AuthContext.bootstrap.test.tsx`
10. `Frontend/PlantProcess.Web/src/state/__tests__/LicenseContext.gating.test.tsx`
11. `Frontend/PlantProcess.Web/src/components/standard/__tests__/DataFetchBoundary.test.tsx`
12. `Frontend/PlantProcess.Web/src/pages/PageBuilder/__tests__/pageBuilderReducer.test.ts`
13. `Frontend/PlantProcess.Web/e2e/journeys/inspection-to-generated-page.spec.ts`
14. `Frontend/PlantProcess.Web/e2e/journeys/assistant-conversation.spec.ts`

## MODIFY — 31 existing tests / journeys

1. `Backend/tests/PlantProcess.Application.UnitTests/Dashboarding/WidgetQueryExpressionServiceTests.cs`
2. `Backend/tests/PlantProcess.Application.UnitTests/Licensing/LicenseServiceTests.cs`
3. `Backend/tests/PlantProcess.Application.UnitTests/Security/SafeSqlPolicyTests.cs`
4. `Backend/tests/PlantProcess.Api.IntegrationTests/Import/DeltaImportResumabilityTests.cs`
5. `Backend/tests/PlantProcess.Api.IntegrationTests/OpenApi/OpenApiContractTests.cs`
6. `Backend/tests/PlantProcess.Api.IntegrationTests/Security/AuthEndpointTests.cs`
7. `Backend/tests/PlantProcess.Api.IntegrationTests/Security/SchemaConfigurationSafetyEndpointTests.cs`
8. `Backend/tests/PlantProcess.Api.IntegrationTests/Smoke/ApiSmokeEndpointTests.cs`
9. `Backend/tests/PlantProcess.Infrastructure.IntegrationTests/Connectors/CsvConnectorSmokeTests.cs`
10. `Backend/tests/PlantProcess.Infrastructure.IntegrationTests/Connectors/ExcelConnectorSmokeTests.cs`
11. `Frontend/PlantProcess.Web/src/api/legacy/__tests__/plantProcessApi.contract.test.ts`
12. `Frontend/PlantProcess.Web/src/components/__tests__/AsyncState.test.tsx`
13. `Frontend/PlantProcess.Web/src/components/__tests__/LockedFeatureOverlay.test.tsx`
14. `Frontend/PlantProcess.Web/src/components/standard/__tests__/StandardButton.test.tsx`
15. `Frontend/PlantProcess.Web/src/components/standard/__tests__/StandardTable.test.tsx`
16. `Frontend/PlantProcess.Web/src/components/standard/__tests__/StandardTabs.test.tsx`
17. `Frontend/PlantProcess.Web/src/test/integration/mockedApi.integration.test.ts`
18. `Frontend/PlantProcess.Web/e2e/phase1-golden-demo.spec.ts`
19. `Frontend/PlantProcess.Web/e2e/phase1-security-hardening.spec.ts`
20. `Frontend/PlantProcess.Web/e2e/phase2-chart-interaction.spec.ts`
21. `Frontend/PlantProcess.Web/e2e/phase2-backend-outage.spec.ts`
22. `Frontend/PlantProcess.Web/e2e/p1-risk-dataquality-contract.spec.ts`
23. `Frontend/PlantProcess.Web/e2e/dimension2-dimension6-readiness.spec.ts`
24. `Frontend/PlantProcess.Web/e2e/phase1-button-action-matrix.spec.ts`
25. `Frontend/PlantProcess.Web/e2e/nav-graph-refresh-survival.spec.ts`
26. `Frontend/PlantProcess.Web/e2e/p0-auth-pages-contract.spec.ts`
27. `Frontend/PlantProcess.Web/e2e/phase1-toast-mapping.spec.ts`
28. `Frontend/PlantProcess.Web/e2e/page-builder-v7.spec.ts`
29. `Frontend/PlantProcess.Web/e2e/license-gate-ux.spec.ts`
30. `Frontend/PlantProcess.Web/e2e/phase56-primary-flows.spec.ts`
31. `Frontend/PlantProcess.Web/e2e/phase78-workflow-widget.spec.ts`
32. `Frontend/PlantProcess.Web/e2e/api/phase03-two-stage-import.spec.ts`
33. `Frontend/PlantProcess.Web/e2e/api/phase02-data-lifecycle-contract.spec.ts`

## Recommended implementation packs

### Pack B — Backend critical behavioural tests

1. MlLearningCoreIntegrationTests.cs
2. AuthGateMatrixTests.cs
3. SqlScriptHygieneApplyTests.cs
4. RiskScoreServiceTests.cs
5. FeatureEngineeringServiceTests.cs
6. MlReadinessServiceTests.cs

### Pack C — Frontend unit behavioural tests

1. apiClient.retry-backoff.test.ts
2. AuthContext.bootstrap.test.tsx
3. LicenseContext.gating.test.tsx
4. DataFetchBoundary.test.tsx
5. StandardButton / StandardTable / StandardTabs strengthening

### Pack D — E2E consolidation

1. Consolidate refresh-survival specs
2. Upgrade page-builder-v7 journey
3. Merge license-gate-ux into license-and-demo-lifecycle
4. Rename phase-number journeys to behaviour journeys
5. Add inspection-to-generated-page after P06
6. Add assistant-conversation after P09

## Rule

Do not mark P00A as complete only because this document exists. P00A is complete only after:

- Maintenance pack validates
- Backend build passes
- Frontend build passes
- Critical Pack B tests are implemented and passing

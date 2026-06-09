
# Phase 8 T-045 / T-046 / T-047 AI Assistant HMI

Marker: PPIQ_REALIZATION_T045_T046_T047_PHASE8_ASSISTANT_HMI

Implemented scope:

- T-045 Suggestion and Recommendation page
- T-046 Assistant chat with grounding runtime
- T-047 Configure assistant from the HMI

Routes:

- /phase8/suggestions
- /phase8/assistant
- /phase8/assistant-config

Backend APIs:

- GET /api/phase8/suggestions/health
- POST /api/phase8/suggestions/generate
- POST /api/phase8/suggestions/decision
- GET /api/phase8/assistant-config
- PUT /api/phase8/assistant-config
- POST /api/phase8/assistant-config/reset
- POST /api/assistant/ask is mapped for grounded assistant runtime

Validation:

- node tools/phase8/validate-t045-t046-t047-ai-assistant-hmi.cjs
- dotnet build Backend
- dotnet test Backend/tests/PlantProcess.Application.UnitTests/PlantProcess.Application.UnitTests.csproj --filter FullyQualifiedName~Phase8AssistantRuntimeTests --no-build
- npm run test -- src/pages/Phase8/phase8AssistantView.test.ts
- npm run build

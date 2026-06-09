# PPIQ Top15 — Phase 3 + Phase 4 Urgent Closure

Tasks:
- T-209 Wire a real assistant model behind IAssistantModel
- T-210 Retrieval grounding over canonical data
- T-211 Assistant regression eval gate on golden Q&A
- T-212 Phase-3 grounding adversarial regression
- T-213 Value/ROI engine on real ingested outcomes
- T-214 Performance/load proof at Vision scale
- T-215 Observability dashboard + SLO + soak/regression

Important:
- T-209 is not faked. The real-model adapter is optional and activated only when PPIQ_ASSISTANT_MODEL_ENDPOINT is configured.
- Without that endpoint, the existing safe extractive model remains active.
- The model output still passes through the grounding guard, so uncited numbers and causal claims are blocked.
- Unit tests use a scripted model client only to prove the adapter and grounding contract without making network calls.

Environment variables for real model activation:
- PPIQ_ASSISTANT_MODEL_ENDPOINT
- PPIQ_ASSISTANT_MODEL_ENABLED=true
- PPIQ_ASSISTANT_MODEL_PROVIDER
- PPIQ_ASSISTANT_MODEL_KEY
- PPIQ_ASSISTANT_MODEL_VERSION

Validation:
- node tools/top15/validate-phase3-phase4-top15.cjs
- dotnet build Backend
- dotnet test Backend/tests/PlantProcess.Application.UnitTests/PlantProcess.Application.UnitTests.csproj --filter FullyQualifiedName~Top15Phase34RuntimeTests --no-build
- scripts/top15/run-phase3-phase4-regression.ps1
# Phase 9 T-051 / T-052 / T-053 Persona RBAC

Markers:
- PPIQ_REALIZATION_T051_FORMAL_ROLE_ACCESS_MATRIX
- PPIQ_REALIZATION_T052_EXECUTIVE_PERSONA_SCOPE_DASHBOARD
- PPIQ_REALIZATION_T053_PERSONA_PAGE_VISIBILITY_ROUTE_GUARDS

Implemented:
- Formal role/capability matrix
- Effective access = role x tier
- API access matrix and 403 enforcement endpoint
- Executive / CEO decision dashboard
- Frontend persona route visibility helper
- Disabled-with-reason route behavior
- Frontend pages:
  - /phase9/executive
  - /phase9/access

Validation:
- node tools/phase9/validate-t051-t052-t053-persona-rbac.cjs
- dotnet build Backend
- dotnet test Backend/tests/PlantProcess.Application.UnitTests/PlantProcess.Application.UnitTests.csproj --filter FullyQualifiedName~Phase9RoleAccessMatrixTests --no-build
- npm run test -- src/security/phase9RoleAccess.test.ts
- npm run build
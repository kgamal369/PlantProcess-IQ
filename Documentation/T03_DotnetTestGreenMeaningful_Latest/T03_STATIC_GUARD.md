# T03 Static Guard - FAILED

- Test projects discovered: 8
- Findings: 3

- Backend/tests/PlantProcess.Analytics.Core.Tests/Provenance/CorrelationConsolidationArchitectureTests.cs:12 [xunit-skip] xUnit skipped tests are not allowed in T03.
  - [Fact(Skip = "Enable after applying the T-102 delegating shim (PPIQ-T102 note).")]
- Backend/tests/PlantProcess.Analytics.Core.Tests/Provenance/CorrelationConsolidationArchitectureTests.cs:12 [skip-property] Explicit test skip is not allowed in T03.
  - [Fact(Skip = "Enable after applying the T-102 delegating shim (PPIQ-T102 note).")]
- tools/realization/Invoke-AuditImmutabilityCiGate.ps1:14 [filtered-dotnet-test] T03 must run full backend dotnet test without filters.
  - dotnet test ".\Backend" --filter "FullyQualifiedName~AuditLogImmutabilityTests|Name~AuditLogImmutability" --no-restore
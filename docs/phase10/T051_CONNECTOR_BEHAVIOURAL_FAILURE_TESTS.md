# T-051 Connector Behavioural + Failure Tests

Marker: PPIQ_REALIZATION_T051_CONNECTOR_BEHAVIOURAL_FAILURE_TESTS

## Scope

Certified connector set:

- CSV
- Excel
- MSSQL / SQL Server
- MySQL / MariaDB

## Certified behaviours

- Test-before-save is required before a connector profile is accepted.
- Credential read-back is masked.
- Raw secret references are not returned.
- Source-shaped staging preserves original source columns.
- Connector staging does not convert directly into canonical objects.
- Import batch lifecycle records Running, Completed, and FailedRolledBack.
- Bad credentials fail before save and rollback staging.
- Malformed rows fail the batch and rollback staging.

## Validation

Run:

    node tools/phase10/validate-t051-connector-behavioural-failure-tests.cjs
    dotnet build Backend
    dotnet test Backend/tests/PlantProcess.Application.UnitTests/PlantProcess.Application.UnitTests.csproj --filter FullyQualifiedName~Phase10_T051ConnectorBehaviouralFailureTests --no-build
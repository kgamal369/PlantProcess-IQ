
# T-043 Golden Dataset Signal-Recovery Fixture

Marker: PPIQ_REALIZATION_T043_GOLDEN_SIGNAL_RECOVERY_FIXTURE

## Purpose

Certify the advanced correlation engine on a deterministic golden dataset.

## Dataset

Known true drivers:

- param_true_temperature_driver
- param_true_pressure_driver

Injected spurious features:

- param_injected_spurious_alternating
- param_injected_spurious_periodic
- param_injected_spurious_hash

Collinear duplicate:

- param_collinear_temperature_duplicate

## Acceptance

- Recover at least two known true signals.
- Reject all injected spurious features under Benjamini-Hochberg FDR.
- Report bootstrap stability on every emitted finding.
- Reruns are deterministic except RunId.
- VIF removes the collinear duplicate while preserving a representative feature.

## Validation

Run:

    node tools/phase8/validate-t043-golden-signal-recovery.cjs
    dotnet build Backend
    dotnet test Backend/tests/PlantProcess.Application.UnitTests/PlantProcess.Application.UnitTests.csproj --filter FullyQualifiedName~Phase8_T043GoldenSignalRecoveryTests --no-build

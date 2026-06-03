# PlantProcess IQ — Pack B3.5 Mostly-Green Task Closure

## Purpose

This pack closes the mostly-green implementation tasks that were already implemented but still needed stronger closure evidence, validation notes, and a permanent evidence ledger before Pack B4.

## Closed tasks

- P01-T02 Argon2id migration posture
- P01-T03 secret hygiene and fail-fast posture
- P01-T06 production posture regression protection
- P02-T04 hot-path query-plan/index review
- P03-T02 compression/retention/continuous aggregates proof
- P04-T06 assistant gateway validation gate
- P05-T02 join preview and canonical target suggestion
- P05-T05 mapping templates
- P06-T04 schema drift and defect-catalog harmonization
- P06-T05 phase 6 validation
- P09-T03 JIT role-mapping admin proof
- P10-T03 license activation/lifecycle tied to verified Ed25519 source
- P11-T03 closed-loop outcome tracking
- P12-T03 mobile/tablet consume-and-act hardening
- P12-T04 i18n/RTL/mobile validation
- P13-T03 escrow and open-format export
- P13-T04 deployment/DR/export validation gate

## Important exception

P14-T03 is not closed here. It requires real retirement of the legacy frontend API client and phase-named artifacts:

- zero imports of `plantProcessApi.ts`
- deletion/retirement of the legacy client
- naming lint with zero phase-named product artifacts
- frontend/backend builds green afterward

That belongs to Pack C.
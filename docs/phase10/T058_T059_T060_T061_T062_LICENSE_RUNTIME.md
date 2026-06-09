# Phase 10 License Runtime Closure

Markers:
- PPIQ_REALIZATION_T058_LIVE_TIER_TOGGLE_DEMO
- PPIQ_REALIZATION_T059_GRACE_EXPIRY_OVERAGE_FLOWS
- PPIQ_REALIZATION_T060_TIER_BADGES_UPGRADE_PATHS
- PPIQ_REALIZATION_T061_OFFLINE_SIGNED_LICENSE_ACTIVATION
- PPIQ_REALIZATION_T062_PHASE10_LICENSE_E2E_REGRESSION

Implementation:
- Live HMI tier toggle route: /phase10/license
- Enterprise -> Pro hides Enterprise-only features immediately
- Pro -> Enterprise restores them
- Tier badge tokens:
  - Lite = Muted Steel
  - Pro = Amber
  - Pro Plus = Electric Blue
  - Enterprise = Cyan Green
- Expiry/grace/overage lifecycle engine
- Default grace period = 30 days
- Expired and overage states preserve existing dashboard read access
- License flow never deletes customer data
- Offline signed-license activation envelope
- Tampered offline license rejected and audited
- Phase 10 rollup script:
  scripts/phase10/validate-phase10-rollup.ps1
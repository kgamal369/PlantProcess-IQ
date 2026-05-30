# Phase 6 Accessibility Audit — WCAG AA

Scope:

- Dashboard
- Material Investigation and drilldown
- Risk Intelligence
- Data Quality
- Correlations
- ML Readiness
- Demo Lifecycle
- Admin Preview
- Administrator
- Brand

Checks automated in `e2e/a11y/phase56-accessibility.spec.ts`:

- Named buttons
- Labelled form controls
- Visible main content landmark
- Forbidden failure phrase absent

Result expectation:

- 0 Critical blockers
- 0 Serious blockers
- Moderate/minor findings to be documented in this file when discovered

Run:

```powershell
npm run test:a11y
```

# Phase 5/6 Visual Regression Baseline

Scope:

- Phase 5 Analytics pages: Dashboard, Material Investigation, Risk Intelligence, Data Quality, Correlations.
- Phase 6 Intelligence/System pages: ML Readiness, Demo Lifecycle, Admin Preview, Administrator, Brand, Material drilldown.

The Playwright visual manifest defines:

- 15 route states
- 2 themes
- 3 viewports
- 90 expected screenshots

Run:

```powershell
npm run test:visual:update
npm run test:visual
```

CI wiring:

- Jenkinsfile contains the Phase 5/6 UI quality gate.
- The PR/deploy gate lists visual, e2e and accessibility scripts.

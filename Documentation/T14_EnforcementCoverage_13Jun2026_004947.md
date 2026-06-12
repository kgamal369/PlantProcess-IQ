# T14 License Enforcement Coverage - 13Jun2026_004947

## Endpoint-filter paywalls (RequireLicenseFeature)
| Surface | Feature | Mechanism |
|---|---|---|
| `/admin/connectors` | DbLinkConfiguration | endpoint filter (this task) |
| `/admin/connectors` | DbLinkConfiguration | endpoint filter (this task) |
| `/admin/schema-configuration` | SchemaSqlViewBuilder | endpoint filter (this task) |
| `/analytics/phase2` | InvestigationWorkflow | endpoint filter (this task) |
| ml-workspace preview | MlWorkspacePreview | endpoint filter (pre-existing) |
| risk dashboard | RiskDashboardView | endpoint filter (pre-existing) |
| data-quality full scan | DataQualityFullScan | endpoint filter (pre-existing) |

## Service-level enforcement (deliberately NOT double-gated)
- **WidgetScriptLayer**: enforced via `LicenseLimits.AllowsWidgetScriptLayer` flags inside
  the widget expression pipeline (per-tier limit objects). Double-gating at the route would
  block reading widgets that merely CONTAIN scripts for lower tiers.
- **CorrelationScheduledRun**: enforced via `MaxScheduledJobs` / `EnsureJobCountAllowed`
  in the job system - the scheduled-run paywall is a job-count limit, not a route.

## Frontend coverage (LockedFeatureOverlay / LicenseContext)
- **DbLinkConfiguration**: NO FE reference found -> a blocked call currently surfaces as a raw 403.
  Wrap the surface in LockedFeatureOverlay keyed to this feature (pattern: LicenseContext.gating tests).
- **SchemaSqlViewBuilder**: referenced in FE -
  - `src\pages\CommercialLicense\CommercialLicensePage.tsx:16`
- **WidgetScriptLayer**: referenced in FE -
  - `src\api\license\license.api.ts:15`
  - `src\api\license\licenseUsage.api.ts:29`
  - `src\components\dashboard\widget-builder\WidgetScriptBuilderPanel.tsx:17`
  - `src\components\license\LicenseUsagePanel.tsx:100`
- **CorrelationScheduledRun**: referenced in FE -
  - `src\license\phase10License.ts:6`
  - `src\license\phase10License.ts:61`
  - `src\pages\CommercialLicense\CommercialLicensePage.tsx:20`
- **InvestigationWorkflow**: referenced in FE -
  - `src\pages\CommercialLicense\CommercialLicensePage.tsx:21`
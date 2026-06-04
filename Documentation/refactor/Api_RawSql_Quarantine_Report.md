# PlantProcess IQ - API Raw SQL Quarantine Report

Generated at: 2026-06-04 13:56:29

## Result

Pack 4B created a controlled API raw SQL quarantine baseline. This is a refactor-control step, not a behavior change.

## Gate Summary

| Status | Count |
|---|---:|
| OK | 5 |

## Risk Summary

| Risk | Count |
|---|---:|
| MEDIUM_RAW_SQL_TOUCHPOINT | 351 |
| HIGH_DIRECT_API_DB_ACCESS | 191 |
| HIGH_WRITE_OR_DDL_REVIEW | 124 |
| MEDIUM_EF_RAW_SQL | 11 |
| HIGH_DYNAMIC_SQL_REVIEW | 8 |

## Refactor Track Summary

| Track | Count |
|---|---:|
| CanonicalDataAndIntegrationRuntime | 485 |
| AdminDiagnosticsAndConfiguration | 109 |
| AnalyticsAndKpiRuntime | 83 |
| HealthAndReadinessDiagnostics | 8 |

## Category Summary

| Category | Count |
|---|---:|
| SQL_TEXT_INSIDE_API_SURFACE | 274 |
| RAW_COMMAND_EXECUTION_IN_API | 205 |
| DIRECT_NPGSQL_IN_API | 191 |
| EF_RAW_SQL_IN_API | 15 |

## Next Pack

Pack 4C should start with the highest customer-facing runtime track, usually AnalyticsAndKpiRuntime or CanonicalDataAndIntegrationRuntime, and move those query paths behind Application/Infrastructure services.

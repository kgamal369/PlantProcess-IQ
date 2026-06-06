# PlantProcess IQ - Analytics / KPI API Raw SQL Burn-Down

Generated at: 2026-06-05 18:03:25

## Result

Pack 4C measures the reduction of customer-facing analytics/KPI raw SQL inside the API against the Pack 4B baseline. The refactor is applied by hand; this pack proves and gates it.

## Burn-down

| Metric | Value |
|---|---:|
| Baseline (Pack 4B, AnalyticsAndKpiRuntime) | 83 |
| Remaining in API now | 83 |
| Reduced | 0 |
| Percent reduced | 0% |
| Application query types | 5 |
| Infrastructure query types | 1 |
| Integration/query test files | 0 |

## Gate Summary

| Status | Count |
|---|---:|
| OK | 6 |
| WARN | 2 |

## Remaining by Category

| Category | Count |
|---|---:|
| SQL_TEXT_INSIDE_API_SURFACE | 38 |
| RAW_COMMAND_EXECUTION_IN_API | 22 |
| DIRECT_NPGSQL_IN_API | 20 |
| EF_RAW_SQL_IN_API | 3 |

## Manual refactor actions for this track

1. Take the highest-risk rows from Pack4C_AnalyticsKpiRemainingSql for this track.
2. Create Application interfaces for the analytics/KPI/value/risk/dashboard queries.
3. Move SQL execution into Infrastructure query services; keep API endpoints thin.
4. Keep user-defined/KPI SQL-view logic behind SafeSqlValidator.
5. Add integration tests for each moved query path, then delete the endpoint SQL.
6. Re-run this pack and confirm Remaining = 0 before Pack 4D.

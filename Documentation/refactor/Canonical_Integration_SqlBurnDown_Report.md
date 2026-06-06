# PlantProcess IQ - Canonical / Integration API Raw SQL Burn-Down

Generated at: 2026-06-05 18:04:45

## Result

Pack 4D measures the reduction of mapping/canonical/connector/integration raw SQL inside the API against the Pack 4B baseline, and probes for steel-specific literals that would break genericness.

## Burn-down

| Metric | Value |
|---|---:|
| Baseline (Pack 4B, CanonicalDataAndIntegrationRuntime) | 485 |
| Remaining in API now | 485 |
| Reduced | 0 |
| Percent reduced | 0% |
| Application query/repository types | 0 |
| Infrastructure query/repository types | 0 |
| Tenant-aware remaining files | 22 |
| Steel-literal probe hits | 42 |

## Gate Summary

| Status | Count |
|---|---:|
| BLOCKER | 1 |
| OK | 3 |
| WARN | 4 |

## Remaining by Category

| Category | Count |
|---|---:|
| SQL_TEXT_INSIDE_API_SURFACE | 179 |
| DIRECT_NPGSQL_IN_API | 150 |
| RAW_COMMAND_EXECUTION_IN_API | 149 |
| EF_RAW_SQL_IN_API | 7 |

## Manual refactor actions for this track

1. Target CanonicalDataAndIntegrationRuntime rows in Pack4D_CanonicalIntegrationRemainingSql.
2. Move direct SQL into Infrastructure repositories/query services.
3. Keep the canonical model generic; move any steel terms (probe CSV) into seed/demo/config.
4. Ensure tenant context is applied on every moved query.
5. Add tests for mapping/canonical/integration query paths.
6. Re-run this pack and confirm Remaining = 0 before Pack 4E.

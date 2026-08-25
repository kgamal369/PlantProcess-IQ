# Persisted Definition Replay Gate

Backlog origin: T-202 Â· Release: M2 Â· Owner: Worker 2 (Release Truth)
Lock: frontend E2E + dashboard query test harness. No canonical schema edits.

Task IDs are metadata. They appear in this header, in commits and in evidence.
They do not name artifacts.

## Bounded scope

Enumerate every ACTIVE persisted dashboard/widget/page definition in the
resolved release database and prove each replays through the real API path
without a hidden HTTP/API/runtime failure. Nothing else.

Out of scope by design: cross-filter correctness (T-204), route/network/console
invariants (T-203), frontend suite termination (T-205), customer Day-1 flow
(T-247).

## Two release modes, never mixed

| Mode | Profile | Database | Authority |
|---|---|---|---|
| `CurrentRelease` (default) | `local` | canonical generic application DB | **Authoritative for M2 closure** |
| `HistoricalBaseline` | `presentation` | `ppiq_presentation` | Informational M1 frozen-baseline regression only |

`CurrentRelease` refuses `ppiq_presentation`. `HistoricalBaseline` requires it.
Each mode writes its own report file. A HistoricalBaseline PASS can never close
an M2 task.

## Commands

    npm run releasetruth:persisted-definitions
    npm run releasetruth:persisted-definitions:falsify

    .\tests\release-truth\Invoke-PersistedDefinitionReplay.ps1
    .\tests\release-truth\Invoke-PersistedDefinitionReplay.ps1 -ReleaseMode HistoricalBaseline

The runner loads the canonical profile loader, resolves and prints the database,
refuses the wrong one for the mode, then reuses or starts the API.

No new credential convention. The harness consumes what the profile already
defines: `VITE_API_BASE_URL`, `PPIQ_SMOKE_USERNAME`, `PPIQ_SMOKE_PASSWORD`,
`POSTGRES_DB`, `ConnectionStrings__PlantProcessDb`. Nothing is printed.

## API reuse policy

The runner does **not** reuse a running API it did not start, unless
`-ReuseRunningApi` is passed. Both profiles bind the same API port and
`/db-health` returns a hardcoded literal `"plantprocessiq"` rather than the real
database name, so a running API's database cannot be verified from outside.

**Open ask for Worker 1:** make `/db-health` report the actual
`Database.GetDbConnection().Database`. One truthful field removes this entire
class of wrong-database ambiguity for every gate, not just this one.

## Terminal states

| State | Meaning |
|---|---|
| POPULATED | 2xx, recognised result shape, rows > 0 |
| EMPTY | 2xx, recognised result shape, rows == 0 |
| BLOCKED | reserved for an explicitly declared entitlement envelope |
| FAILED | any unexpected status, **including 401/403** |
| UNCLASSIFIED | 2xx whose body is not a recognised result shape - fails the gate |

## Refusal rules

- configuration missing from the profile -> FAIL, never skip
- API unreachable -> FAIL, never skip
- connection-string database != profile `POSTGRES_DB` -> refuse to run
- wrong database for the mode -> refuse to run
- zero definitions or zero active widgets -> FAIL

### On zero definitions in the M2 database

If the canonical M2 database owns no Release-1 persisted definitions, the gate
fails and says so. **That is a real product finding, and it is the correct
result.** It must not be resolved by bulk-copying presentation or Fleet demo
dashboards into the M2 database. The resolutions are: the generic product
already owns appropriate definitions, or the smallest generic Release-Truth
definitions are created through the current product contract, or the gap is
reported as a dependency. M2 must not become a disguised presentation dataset.

## Falsification

The first strategy was wrong. Persisting an unsupported measure/dimension is
correctly rejected with HTTP 400 by write-time validation. That is a good
product result and it is not weakened.

**Part A** - product evidence. An isolated disposable dashboard is created, an
invalid widget contract is attempted, the 400 is recorded as proof that invalid
persisted definitions cannot be created through the product, and the disposable
dashboard is deleted. Skipped entirely in HistoricalBaseline mode. If the API
ever *accepts* the invalid contract, that is reported as a finding.

**Part B** - gate failure-path proof at the correct test layer. A local stub
serves the API contract with one 5xx widget and one 2xx-wrong-shape widget. The
gate must exit non-zero and must name both `FAILED` and `UNCLASSIFIED` in its
manifest. A healthy stub must then return exit zero. No database is involved
and no real definition is touched.

We do not damage working product validation to satisfy a falsification ritual.
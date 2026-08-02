# CORRECTION — 28 July 2026

Two verdicts in the previous README were wrong. I searched by endpoint name and
table name, found nothing, and stopped. Both things exist under different names,
and the tasks were right to assume them. Corrected below, with the evidence.

---

## WRONG: "M1-19 — there is no endpoint that accepts author-written SQL"

**There is.** `POST /admin/p03p04/completion/safe-sql/resolve` takes
`{ Sql, RowLimit, TimeoutMs }` and calls
`public.ppiq_resolve_safe_sql(text, integer, integer)`.

I ran a grep for endpoints named `/query`, `/sql` or `/execute`, saw
`/safe-sql/resolve` in the visual-mapper listing, and did not open it. That was
the error.

**What the function actually enforces** — this is Authoring Layer Specification
12.1 in full, already built:

| Control 12.1 requires | Enforced as |
|---|---|
| Exactly one statement, SELECT only | `MustBeSelectOnly`, `MultipleStatements` |
| Refused by name | every branch returns `error_code` + a sentence |
| Row ceiling, server-applied | clamped 1..1000, returned as `applied_row_limit` |
| Statement timeout | clamped 250..10000 ms |
| No write/DDL/control | `ForbiddenStatement` (insert, update, delete, drop, alter, truncate, grant, revoke, copy, execute, merge, vacuum, call, do, create, replace...) |
| No dangerous functions | `ForbiddenFunction` (pg_sleep, dblink, pg_read_file, lo_import...) |

It also runs `EXPLAIN (FORMAT JSON)` against the real schema, so an unknown view
or column is refused **before execution** as `NoSuchView` / `NoSuchColumn`, and
`CROSS JOIN` is refused as `AmbiguousJoinKey`.

**The one real gap:** it validates, it does not return rows. The editor needs
"Run, see what came back."

**So M1-19 was never 8h of backend.** It is one endpoint that calls the existing
validator and executes only on its approval. That is shipped in `M1-PREREQ`.

---

## WRONG: "M1-20 — there is no method registry"

**There is a method catalogue**, in `Backend\PlantProcess.Analytics.Core\Methods\`:

```csharp
public enum AnalysisMethod { Spearman, MutualInformation, CramersV, PointBiserial, LassoVif, NotApplicable }
public static class MethodSelector { public static MethodChoice Select(...) }
```

Five methods implemented — `MutualInformation.cs`, `CategoricalAssociation.cs`,
`Lasso.cs`, `VarianceInflation.cs`, plus Spearman in the correlation path — and a
deterministic selector that chooses by variable-pair shape **and records why**.

I grepped for `ml_*method*` tables, found none, and concluded no catalogue
existed. Wrong: it exists in code, not in the database.

**What is genuinely missing** is narrower than I said: no client can ask what the
methods are, so the palette has nothing to populate from. `M1-PREREQ` adds
`GET /api/analysis/methods` projecting the enum plus each method's applicability
and the selector's own rationale sentence.

**The honest limit, stated on the wire.** That endpoint reads a C# enum, so
adding a method still means editing code. The acceptance line *"adding a method
to the registry makes it appear in the palette with no code change"* is **not**
satisfied until `ml_method_definitions` replaces the enum — at which point the
client contract does not change, which is why the contract ships first. The
response carries `source: "code"` and says so.

---

## OVERSTATED: "M1-18 — the backend is already complete"

The task's own text says: *"The backend already exposes everything needed: GET
/api/analytics/advanced/readiness/gates returns state, canRun, readyCount..."*

I presented that as a finding. It was a restatement of the task. The task was
correct and I added nothing there.

---

## STANDS

- **M1-16** — `displayOptionsJson` as the persistence site is additive; the task
  said "beside the expression" without naming where. Verified.
- **M1-17** — the dependency argument stands: M1-19 and M1-20 both name the debug
  log in their acceptance criteria.
- **M1-21** — blocked on its own stated dependency, as the task itself says.

---

# M1-PREREQ — what it ships

**`POST /api/prep/sql/run`** — validate through `ppiq_resolve_safe_sql` first,
execute only on approval, return `{status, rowCount, columns, rows, message,
errorCode, sql, appliedRowLimit}`.

Three deliberate decisions:

1. **The validator is called, never re-implemented.** A second implementation of
   a governance rule is what Constitution II.7.6 forbids. Nothing bypasses it.
2. **Wire shape mirrors `DryRunResult`**, so the debug log and the preview table
   need no new branch, and `rejected_by_safe_sql` stays the first-class status it
   already is.
3. **New route, not reuse of the existing one.** The existing caller sits under
   `/admin/p03p04/completion` behind `PlantProcessDataManager`. That route
   carries a phase token, which Chapter B.10 forbids, and an authoring surface
   must not demand an administrative role.

It also distinguishes *refused by the validator* from *passed validation then
failed on execution* — which is what tells an engineer whether to fix his SQL or
file a defect.

**`GET /api/analysis/methods`** — the method catalogue over HTTP, with each
method's applicability and the selector's own rationale, so the palette cannot
offer a method the selector would call `NotApplicable`.

**Gate:** `dotnet build Backend/PlantProcess.Api`, not `tsc`. The generic harness
runs the frontend gate; run this one's build yourself or set `gate` in
`pack.json`.

---

## Remaining, in order

| # | Work | Blocked by |
|---|---|---|
| 1 | M1-16 stale banner on the card | nothing, ~20 min |
| 2 | M1-19 frontend: board becomes editor, palette hidden, tree kept, fork warning | M1-PREREQ applied |
| 3 | M1-20 frontend: palette from `/api/analysis/methods`, drag, wire, run, save | M1-PREREQ applied |
| 4 | `ml_method_definitions` + seed, to satisfy M1-20's registry acceptance line | M1-20 frontend |
| 5 | M1-21 | a completed engine run, per the task |

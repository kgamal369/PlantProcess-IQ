# RETIRED: Seed-PresentationDashboards.ps1 (v1)

**Removed from the active tree on 11 August 2026, T-044.**

The v1 presentation seeder was a superseded generation. It wrote the SAME
widget UUIDs as the current seeders under DIFFERENT widget codes, so running it
would have silently replaced the current widget set with an older one keyed on
the same primary keys.

Thirteen of its twenty-nine widgets collided on code:

| UUID suffix | v1 code | current code |
|---|---|---|
| 000101 | PO_KPI_MATERIALS | PO_KPI_MAT |
| 000103 | PO_KPI_DEFECTS | PO_KPI_DEF |
| 000107 | PO_WEEKLY | PO_WEEK |
| 000202 | QM_BREAKDOWN | QM_BREAK |
| 000203 | QM_SEVERITY | QM_SEV |
| 000301 | EO_EQUIP_DEFECTS | EO_EQDEF |
| 000302 | EO_OBS_TREND | EO_OBS |
| 000303 | EO_EQUIP_TABLE | EO_TABLE |
| 000304 | EO_MONTHLY | EO_MONTH |
| 000402 | CF_TOPDEFECTS | CF_TOP |
| 000501 | PA_KPI_AVG | PA_KAVG |
| 000502 | PA_KPI_OBS | PA_KOBS |
| 000504 | PA_BYPARAM | PA_BYP |

It was found by the T-044 convergence proof, not by review. The proof asserts
that no source in `scripts/` still writes the retired definitions, and this file
was the last producer able to recreate

- `QM_SEVERITY` on the unregistered `severity` dimension, and
- `EO_EQUIP_DEFECTS` as *Quality Events by Equipment*, a question the data
  cannot answer because quality events carry no equipment relationship.

**Recovery is git history.** No runnable copy was kept: an archived `.ps1` under
the active tooling tree is still a runnable producer, and the whole point of the
retirement is that nothing can run it by accident.

**The current authoritative presentation writers are:**

- `Rebuild-PresentationDb.ps1` - the rebuild path
- `Seed-PresentationDashboards.v2.ps1`
- `Insert-Widgets-v4.ps1`
- `Finish-PresentationWorkspace.ps1`

**Open finding, owned outside T-044:**
`SEEDER GENERATION DRIFT - superseded and current widget-code generations write
the same widget UUIDs.` Retiring v1 removed the only proven active instance. The
class remains worth an owner: four separate scripts each carry their own copy of
the same twenty-nine widget rows, and nothing but this proof keeps them equal.
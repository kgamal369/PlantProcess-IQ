# rules.txt (line 662+) vs Implementation - Validation Matrix
**21-Jul-2026 | validates the two binding specs: A) Visual Data Preparation & Transformation Builder (lines 693-1248) and B) Qlik-style Workspace (lines 1249-1535) against the built system. Verdicts: PRESENT (evidence cited) / PARTIAL / ABSENT / UNVERIFIED. Milestone column = where the gap is already owned or newly proposed.**

## PART A - Visual Prep & Transformation Builder (spec SS1-16)

| Spec SS | Mandate | Implementation verdict | Evidence | Milestone |
|---|---|---|---|---|
| SS1 three-layer separation (dataflow DAG / expression / orchestration) | ABSENT frontend (no canvas); backend model is declaratively shaped (mappings+views, no imperative flow) | script 540, MappingDefinition | M2-31 |
| SS2 saved Transformation Definition artifact | **PARTIAL-STRONG**: draft/validated/published/paused_by_drift/rolled_back lifecycle, immutable versions + rollback pointer, templates | 540:35-41,136-147 - matches the spec's immutability rule verbatim | fields gap (definition_hash, output_schema, per-column lineage, source_bindings-by-id) = **M2-39 (new)** |
| SS3 screen layout (toolbox right, tables left, whiteboard center) | ABSENT | no flow lib in package.json | M2-31 |
| SS4 port-type wiring correctness | ABSENT (no canvas) | - | M2-31 |
| SS5 dual-mode toggle (visual/SQL) + honest toggle contract | PARTIAL: SQL half exists (canonical schema views + validation); visual half absent; no toggle | canonical_schema_views + audit table | M2-31 |
| SS6 SQL-mode workflow (write, test, see rows, save) | PARTIAL: definitions + validation + audit exist; in-UI test-run UX to verify | schema_view_definitions, 311 safe-sql | verify in walk; polish M2 |
| SS7 node catalogue | ABSENT | - | M2-31 |
| SS8 expression editor + NULL semantics | ABSENT | - | M2-31 |
| SS9 validation taxonomy | PARTIAL: safe-sql rejection is a first-class dry-run status | 540:133 rejected_by_safe_sql | M2-31 extends |
| SS10 preview/test execution | **PRESENT** (backend): dry-runs table + endpoints | 540:117 | UI surfacing M2-31 |
| SS11 per-column lineage | PARTIAL: batch/source provenance yes; column-level lineage no | canonical tables source_system/batch | M2-39 |
| SS12 governance/access | PARTIAL: roles + deny-by-default matrix | PlantAccessControl | ok |
| SS13 bug/edge register | the spec's anticipated-failure list maps to the live defect register practice | UIUX_Defect_Register | ongoing |
| SS14 dialect/transpile | PARTIAL: per-connector SQL exists; no logical-plan transpiler | Connectors/ | M3 |
| SS15 read-only enforcement | **PRESENT-STRONG**: read-only connectors, safe-sql gate, no write-back path | connector truth contract + 311 | done |
| SS16 MVP build order | maps 1:1 onto M2-31's staging | - | M2-31 |

**Part A honest score: backend artifact+safety layer ~60% of spec; authoring UX (canvas) ~0%; SQL mode ~50%. The spec's hardest invariants (immutability, rollback, read-only, safe-sql) are ALREADY BUILT - what is missing is the visual authoring surface on top.**

## PART B - Qlik-style Workspace (spec SS0-12)

| Spec SS | Mandate | Verdict | Evidence / defect | Milestone |
|---|---|---|---|---|
| SS0 associative selection engine (select->possible/excluded states, green-white-grey) | **ABSENT** - the deepest gap vs "Qlik standard": filters requery, but no associative state model / no possible-vs-excluded coloring | fix 08:41 gives requery-on-select only | **M2-37 (new)** |
| SS1 global filters & page controls | PRESENT (bar populated, clear-all, time range); consumption by widgets = this morning's fix | audit C | verify in pass |
| SS2 chart catalogue | PARTIAL ~7 of the spec's catalogue (bar/line/area/pie/donut/table/kpi); no scatter/heatmap/pareto/box/gauge/waterfall | SavedDashboardWidget branches | **M2-38 (new)** |
| SS3 non-chart widgets/containers | PARTIAL (kpi cards, table; no text/container/tabs) | - | M2-38 |
| SS4 dashboard-level constructs (bookmarks, sheets, alternate states) | ABSENT (12 fixed workspaces; no bookmarks/states) | - | M2-37/38 |
| SS5 page layout system | **PRESENT**: 12-col responsive grid, drag/resize/persist | react-grid-layout + updateDashboardLayout | done |
| SS6 style/design system | PRESENT: dark-industrial token set, shared palette | theme runtime | done |
| SS7 UX standards (tooltips, loading, empty, errors) | PRESENT-mostly: tooltips, honest empty, loading states; toasts on errors | screenshots 20-21 Jul | pass verifies |
| SS8 cross-cutting (export, fullscreen, collapse) | PRESENT: CSV export real, fullscreen+Esc, collapse; Clone/Remove NOT wired (DEF-006); drilldown drawer unmounted (DEF-007) | audit A/C | DEF fixes M2 |
| SS9 evidence-grade + read-only | PRESENT-STRONG: widget safety registry, expression audit, provenance columns | validation service + audit tables | done |
| SS10 performance/scale | UNVERIFIED (no load rig) | - | A13 audit |
| SS11 access/multi-tenant | PARTIAL: matrix yes; results_v2 tenant NULL defect | M2-28 | M2-28/32 |
| SS12 MVP cut | current build ~= spec's MVP cut + persistence | - | - |

**Part B honest score: layout/style/evidence layers ~90%; interaction layer (post-fix, pending your pass) ~70%; associative engine SS0 and full chart catalogue SS2 are the two real distances from "Qlik standard" - both M2, both now named with IDs.**

## Consolidated against your four presentation targets
1. **Qlik pages**: demonstrable surface ~85-90% after this morning's fixes (pass pending); the missing 10-15% is SS0 associative states + catalogue breadth = M2-37/38, covered by one sentence.
2. **4 no-code UIs**: UI-2 near-full; UI-1/UI-3 = strong backend (SS2/SS10/SS15 already built!) + forms; canvas = M2-31 exactly as spec SS16 orders it. The honest room line: "the artifact model, safety gates and versioning of the builder are live - the wiring canvas ships on top of them in the pilot."
3. **Engine 80%**: gates/refusal/store proven; wall-7 read pending -> M1-21.
4. **Chatbot 70%**: one reindex from cited answers -> M1-01.

## New backlog IDs proposed (M2 negotiation list, per your instruction)
- **M2-37** Associative selection engine (Qlik SS0): selection state model, possible/excluded computation, green-white-grey rendering across widgets. Est 16h.
- **M2-38** Chart & widget catalogue completion (SS2/SS3/SS4): scatter, heatmap, pareto, box, gauge, waterfall; text/container widgets; bookmarks. Est 20h.
- **M2-39** Transformation-definition alignment (SSA2/SS11): definition_hash, output_schema, source-bindings-by-stable-id, per-column lineage on the existing versioned artifact. Est 8h.
- (existing) M2-31 canvas foundation now explicitly implements spec-A SS3/4/5/7/8/16.

# Interactive Workspace Doctrine v1 (concept.md Amendment 7)

Status: MANDATORY product law. Applies to every analytics page, dashboard,
widget, and chart - created by the founder, by AI sessions, by customers, or
auto-generated - on every branch.

Derivation: concept.md v1.1 (constitution) -> this Amendment.
Reference implementation: `src/pages/Dashboard/InteractiveWorkspacePage.tsx`.
Enforcement: `src/test/architecture/interactiveWorkspaceContract.test.ts`
(spec in section 5; a doctrine without a gate is a wish).

---

## 1. The Seven Standards

**S1 - Universal interactivity.** Every analytics component participates in
the workspace filter system. No static analytics panels. A component that
cannot react to filters does not ship on an analytics page.

**S2 - Global cross-filtering.** A selection made in ANY visual (click a
material in a table, a grade in a bar, a slice in a donut, a cell in a
heatmap) becomes a workspace-wide filter applied to ALL widgets on the page.
Selections are visible in the SelectionBreadcrumb and individually removable.
Implementation: `DashboardSelectionContext` + `DashboardFilterContext` -
widgets PUBLISH selections and SUBSCRIBE to the merged filter set.

**S3 - One visual language.** Dark-industrial Qlik-class styling from the
standard token set: `journey-professional.css` + `StandardCard` /
`StandardButton` / `StandardP2*` primitives + the shared chart palette.
No page-local color schemes, no inline styles (ratchet-enforced).

**S4 - Widget window controls.** Every widget frame (`DashboardWidgetCard`)
provides: maximize (fills the grid area), minimize/collapse, remove, and
restore. No widget without a frame.

**S5 - The component library.** The workspace component set is:
line / area / bar / pie / donut / scatter / HEATMAP charts; interactive
TABLES with conditional formatting (value-based cell coloring and
thresholds); KPI tiles; FILTER components (dimension pickers); LIST
components (selectable value lists); DATE-TIME RANGE filter. New component
types enter this list by amendment, not ad hoc.

**S6 - Low-code page authoring.** Pages are composed, not coded: drag a
component onto the grid -> click it -> bind dimension / measure / parameter
-> optionally attach a widget script (SQL / low-code grouping, filtering,
linking) through the WidgetBuilderWizard + WidgetScriptBuilderPanel. The
definition persists to `dashboard_definitions` / `dashboard_widget_definitions`.
Nothing about a page lives only in code.

**S7 - True grid behavior.** react-grid-layout semantics exactly: drag-drop
with live displacement of neighbors, resize from edges, responsive
breakpoints, layout persisted per dashboard definition
(`useDashboardLayoutPersistence` -> `layout_json`), Save layout / Reset
layout actions on every workspace page.

---

## 2. The Binding Contract (what "a page" means from now on)

Every analytics page IS an `InteractiveWorkspacePage` instance or composes
the same primitives:

    DashboardFilterProvider + DashboardSelectionProvider   (app level - done)
      InteractiveWorkspacePage(dashboardCode)
        DashboardFilterBar          <- S1/S2 input side
        SelectionBreadcrumb         <- S2 visibility + removal
        DashboardGridLayout         <- S7
          SavedDashboardWidget[]    <- S4/S5, self-querying via widget-query
                                       contract (dimension/measure/parameter/
                                       filters/displayOptions)

Data access is EXCLUSIVELY through the widget-query contract
(`queryDashboardWidget` / widget-script execution). No page-private fetch
logic for analytics visuals.

---

## 3. Gap Matrix (honest state, 16-Jul-2026)

| # | Standard | Exists today | Gap -> M2 backlog |
|---|----------|--------------|-------------------|
| S1 | Filter participation | FilterBar + contexts survive; wired app-level | Audit each widget type honors all filter keys (est. 8h) |
| S2 | Cross-filter publish | SelectionContext + breadcrumb survive | Verify click-publish per chart type incl. heatmap cells + table rows (est. 12h) |
| S3 | Visual language | Tokens + primitives + ratchet live | Palette unification pass across recovered widgets (est. 6h) |
| S4 | Min/max per widget | DashboardWidgetCard frame survives | Verify maximize behavior in current grid; add if dropped (est. 6h) |
| S5 | Component library | line/area/bar/pie/donut/scatter/heatmap/table/KPI live | Conditional-format tables (est. 10h); list component (est. 8h); date-time range filter component (est. 8h) |
| S6 | Low-code authoring | WidgetBuilderWizard + ScriptBuilder survive | Re-mount builder entry on workspace pages ("Add widget") (est. 8h) |
| S7 | Grid semantics | react-grid-layout 2.2.3 + persistence hook live | Browser-verify displacement + save/reload on resurrected page (est. 4h) |

Estimated to full doctrine conformance: ~70h = one M2 epic. The resurrection
pack delivers the skeleton of all seven TODAY; the epic closes the gaps.

---

## 4. Two-Branch Installation (so it is never forgotten)

    # on main - the doctrine is law on the trunk:
    git checkout main
    git add docs/doctrine/Interactive_Workspace_Doctrine_v1.md
    git commit -m "Amendment 7: Interactive Workspace Doctrine v1 (mandatory)"

    # carry it to the presentation branch:
    git checkout presentation
    git cherry-pick main    # or: git checkout main -- docs/doctrine/

Backlog v23 entries this document creates: the seven gap rows above plus
"interactiveWorkspaceContract gate" (6h).

---

## 5. The Gate (specification)

`interactiveWorkspaceContract.test.ts` - architecture suite, runs in CI:

1. Collect `src/pages/**/*.tsx` matching analytics indicators (renders
   charts, tables of measures, or imports recharts) excluding a FROZEN
   allowlist of pre-doctrine legacy pages (snapshot at gate installation).
2. FAIL any non-allowlisted analytics page that does not import from the
   workspace primitives (`InteractiveWorkspacePage` or
   `DashboardGridLayout` + `SavedDashboardWidget`).
3. FAIL if the allowlist GROWS (ratchet semantics: legacy may shrink, never
   grow).
4. FAIL any analytics component fetching chart data outside the
   widget-query contract (regex: `apiClient.(get|post)` inside
   `components/dashboard/` outside the sanctioned API modules).

The allowlist burns down inside the 70h epic; at zero, the doctrine is
total.

---

*Anything not written here does not exist. Anything written here without its
gate is not yet true. - working principle of this repository*

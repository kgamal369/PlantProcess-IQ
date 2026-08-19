# PPIQ — Worker 2 Handover — T-050 provenance half

**Written** 17 Aug 2026. Supplements the frozen T-051 handover; does not replace it.
**Scope** the remaining provenance half of T-050 only.

The next session starts at implementation. The contract below is read from the committed
diff, not inferred.

---

## 1. State

| Task | Status | Commit |
|---|---|---|
| T-049 | CLOSED | `cc4d88444a81b714436aa3f377c2042bb8212bbb` |
| T-050 presentation half | committed | `d6870d0aac4719918864dd44798939a4980707b3` |
| T-051 | CLOSED | `6c99e0911b91a60c767c4db27d08fa9ccee28af1` |
| PR-050-01 (Worker 1) | CLOSED | `964608045942527c281ae32a05484d64ffaf8103` |
| **T-050 provenance half** | **GO, not started** | — |
| T-052 | WAIT | after T-050 closes |

T-051 evidence: unit 10/10, Playwright 4/4, `tsc -b` green, Vite production build green.
The Vite chunk-size warning is pre-existing and outside T-051.

---

## 2. The committed PR-050-01 contract — verified, not assumed

### Request additions

```
DashboardWidgetQueryDto
  + ExecutionIdentity?: DashboardWidgetExecutionIdentityDto   // default null

DashboardWidgetExecutionIdentityDto
    PageCode?: string
    WidgetCode?: string
    WidgetDefinitionId?: Guid

DashboardWidgetQueryOptionsDto
  + IncludeExecutionEvidence?: bool                            // default null
```

### Response additions

```
DashboardWidgetQueryResultDto
  + ExecutionEvidenceHandle?: ProvenanceHandleRefDto           // opt-in
  + RowPopulations?: DashboardWidgetRowPopulationDto[]         // always computed

ProvenanceHandleRefDto
    Kind: string        // ProvenanceKind.WidgetResult.ToString()
    Id: string          // evidence snapshot id
    Detail?: string

DashboardWidgetRowPopulationDto
    RowIndex: int
    RowFingerprint?: string
    DimensionBindings: IReadOnlyDictionary<string, string?>
    MeasureCode: string
    ParameterCode?: string
    FilterContextFingerprint: string
    PopulationCount?: int
```

JSON is camelCase on the wire: `executionIdentity`, `includeExecutionEvidence`,
`executionEvidenceHandle`, `rowPopulations`, `rowIndex`, `rowFingerprint`,
`dimensionBindings`, `measureCode`, `parameterCode`, `filterContextFingerprint`,
`populationCount`.

### Semantics that constrain the integration

- **`rowPopulations` is always computed and writes nothing.** One descriptor per returned
  row, same order as `Rows`, beside the rows and never inside them so chart data stays
  clean. The clicked point's population is therefore already present in the render the
  drawer opens from — no extra call is needed for it.
- **`executionEvidenceHandle` is opt-in.** An ordinary render is a read. A dashboard that
  wrote evidence on every refresh, filter move and auto-refresh would turn the evidence
  store into an event log. Only a drill-down asks.
- **`pageCode` and `widgetCode` are both mandatory when evidence is requested.** T-073
  hashes both into the fingerprint and renders both into the evidence sentence. If either
  is blank the service returns the values, writes nothing, offers no handle, and adds a
  warning beginning `execution_evidence_unavailable:`. Same warning shape when the
  composition has no evidence writer or no resolvable tenant, and when the store neither
  wrote nor returned a snapshot.
- **`RowFingerprint` is semantic identity only** — effective filter context, dimension
  bindings and values, measure, parameter. It excludes the aggregate value, the rendered
  label, the generation time and the row's position, so re-ordering cannot invent
  populations and a changed number cannot silently become a changed population.
- **`PopulationCount` may be null and is NEVER the row count.** Five bars do not mean five
  of anything. The drawer must not substitute one for the other, and must say plainly when
  the count is unavailable.
- **Execution evidence is not physical row lineage.** It identifies the exact widget
  execution that produced these values. No consumer may present it as source-row lineage.
- Both query classes now converge on one post-processing step, so a native-source
  (Class-2) widget also carries descriptors.

---

## 3. Integration shape

```
click a chart point
   ↓ rowIndex
rowPopulations[rowIndex]            <- already in the current render, no call
   ↓
drawer opens with the population descriptor
   ↓ one re-execution: includeExecutionEvidence = true
   ↓                   executionIdentity = { pageCode, widgetCode, widgetDefinitionId }
executionEvidenceHandle
   ↓
existing T-073 evidence resolution   <- EvidencePanel / evidenceHandle path
   ↓
evidence visible in the drawer
```

Forbidden, unchanged from the frozen ruling: a second provenance system, fabricated
source-row ids, a new evidence endpoint, a new execution store, frontend-generated
evidence identity, and presenting execution evidence as row lineage.

---

## 4. Surfaces the integration touches

| surface | change |
|---|---|
| frontend dashboard query types | mirror the four new fields exactly, field for field |
| widget query client | pass `executionIdentity` and `includeExecutionEvidence` on the drill-down call only |
| `DashboardSelectionContext` drilldown state | `{ isOpen, title, subtitle, type, payload }` must also carry `rowIndex` and the widget/page identity |
| the five interactive chart click sites | supply `rowIndex` to `openDrilldown` |
| `DrilldownDrawer.tsx` | render the population descriptor, request evidence, resolve the handle, and surface `execution_evidence_unavailable` honestly |
| T-073 evidence resolution | consume as-is; read it before wiring |

**Not yet read in any session:** the frontend T-073 evidence resolution path
(`EvidencePanel`, `AdvancedResultPanel.evidenceHandle`, `BlendedProvenanceBreakdown`) and
the dashboard query client's TypeScript result type. Read those two first; everything else
above is already established.

No frontend file was touched by `964608045942527c281ae32a05484d64ffaf8103`, so the
TypeScript result type almost certainly still declares only
`{ generatedAtUtc, widget, columns, rows, warnings }`. Extending it is inside the Worker-2
lock and must mirror the DTO exactly.

---

## 5. Acceptance

```
click a chart point
  → the exact population it represents is named
  → an existing ProvenanceHandleRef is returned
  → it resolves through the existing evidence authority
  → the underlying evidence is visible

plus, still holding from the presentation half:
  token compliance, RTL, reduced motion, the existing transition
```

And specifically:

- population comes from `rowPopulations`, matched by `rowIndex`, never recomputed locally
- `populationCount === null` is stated as unknown, never replaced by `rows.length`
- an `execution_evidence_unavailable` warning is shown as an honest gap, not swallowed
- no evidence is requested by an ordinary render — only by the drill-down
- execution evidence is never labelled as source-row lineage

---

## 6. Standing rules carried forward

Findings are triaged, not escalated: resolve anything that stays inside frozen semantics,
Worker-2 ownership and existing public contracts; STOP only for a missing producer, an
ownership conflict, a live contradiction of a frozen public contract, a required change to
accepted product semantics, or work in another worker's subsystem.

Packs assert positive facts about the region they own — pre-state hashes, anchor
uniqueness, encoding, required content, tests passing. No negative text scans: a file that
explains a thing contains the word for that thing, and that mistake cost five runs on T-050
and one on T-051.

Exact-file staging only. The tree carries concurrent Worker-1 and Worker-3 work; never
`git add .`, `-A`, `clean -fd`, `reset --hard`, or `restore .`.

Still open elsewhere: **W2-GRID-DEFAULTS-01** (nine hardcoded `defaultLayouts` keys merged
into every persisted layout), and the `PRODUCTION_OVERVIEW` `lg` layout damaged by early
T-049 runs, which must be repaired before T-078 / T-080 or visual regression will baseline
the damage.

const fs = require("node:fs");
const path = require("node:path");

const root = process.cwd();
const frontendRoot = path.join(root, "Frontend", "PlantProcess.Web");

function writeRepo(relativePath, content) {
  const file = path.join(root, relativePath.replaceAll("/", path.sep));
  fs.mkdirSync(path.dirname(file), { recursive: true });
  fs.writeFileSync(file, content.replace(/\r\n/g, "\n"), "utf8");
}

function writeFrontend(relativePath, content) {
  const file = path.join(frontendRoot, relativePath.replaceAll("/", path.sep));
  fs.mkdirSync(path.dirname(file), { recursive: true });
  fs.writeFileSync(file, content.replace(/\r\n/g, "\n"), "utf8");
}

// ============================================================================
// 1. Page Builder reducer: real pure state transitions.
// ============================================================================

writeFrontend(
  "src/pages/PageBuilder/pageBuilderReducer.ts",
  `export type PageVisibility = "Private" | "Shared" | "Public";

export type WidgetKind = "kpi" | "bar" | "line" | "filter-date" | "filter-list";

export type BuilderWidget = {
  id: string;
  kind: WidgetKind;
  title: string;
  x: number;
  y: number;
  w: number;
  h: number;
  source: string;
};

export type PageBuilderState = {
  title: string;
  slug: string;
  visibility: PageVisibility;
  widgets: BuilderWidget[];
};

export type PageBuilderPayload = {
  slug: string;
  title: string;
  visibility: PageVisibility;
  layoutJson: {
    grid: {
      columns: number;
      rowHeight: number;
    };
    widgets: BuilderWidget[];
  };
  widgetBindingsJson: {
    bindings: Array<{
      widgetId: string;
      source: string;
    }>;
  };
};

export type PageBuilderAction =
  | {
      type: "updateMeta";
      patch: Partial<Pick<PageBuilderState, "title" | "slug" | "visibility">>;
    }
  | {
      type: "addWidget";
      kind: WidgetKind;
      title: string;
      source: string;
      idSeed?: number | string;
    }
  | {
      type: "moveWidget";
      id: string;
      x: number;
      y: number;
    }
  | {
      type: "resizeWidget";
      id: string;
      w: number;
      h: number;
    }
  | {
      type: "removeWidget";
      id: string;
    }
  | {
      type: "reset";
      state?: PageBuilderState;
    };

export const pageBuilderGrid = {
  columns: 12,
  rowHeight: 80,
  minWidgetWidth: 1,
  minWidgetHeight: 1,
  maxWidgetHeight: 12,
} as const;

export const defaultPageBuilderWidgets: BuilderWidget[] = [
  {
    id: "w-risk",
    kind: "kpi",
    title: "Risk KPI",
    x: 0,
    y: 0,
    w: 3,
    h: 2,
    source: "schema_view:risk_summary",
  },
  {
    id: "w-defects",
    kind: "bar",
    title: "Defect breakdown",
    x: 3,
    y: 0,
    w: 5,
    h: 3,
    source: "schema_view:defect_breakdown",
  },
  {
    id: "w-trend",
    kind: "line",
    title: "Defect trend",
    x: 8,
    y: 0,
    w: 4,
    h: 3,
    source: "schema_view:quality_daily",
  },
];

export function normalizePageVisibility(value: string | string[]): PageVisibility {
  const raw = Array.isArray(value) ? value[0] : value;

  if (raw === "Private" || raw === "Shared" || raw === "Public") {
    return raw;
  }

  return "Shared";
}

export function createInitialPageBuilderState(): PageBuilderState {
  return {
    title: "Demo Quality Investigation",
    slug: "demo-quality-investigation",
    visibility: "Shared",
    widgets: cloneWidgets(defaultPageBuilderWidgets),
  };
}

export function pageBuilderReducer(
  state: PageBuilderState,
  action: PageBuilderAction,
): PageBuilderState {
  switch (action.type) {
    case "updateMeta":
      return {
        ...state,
        ...action.patch,
        visibility: action.patch.visibility
          ? normalizePageVisibility(action.patch.visibility)
          : state.visibility,
      };

    case "addWidget": {
      const id = createNextWidgetId(state.widgets, action.idSeed);
      const index = state.widgets.length;

      const widget: BuilderWidget = {
        id,
        kind: action.kind,
        title: action.title.trim() || defaultWidgetTitle(action.kind),
        x: clamp((index * 3) % pageBuilderGrid.columns, 0, pageBuilderGrid.columns - 1),
        y: Math.floor(index / 3) * 3,
        w: action.kind.startsWith("filter") ? 3 : 4,
        h: action.kind === "kpi" ? 2 : 3,
        source: action.source.trim(),
      };

      return {
        ...state,
        widgets: [...state.widgets, widget],
      };
    }

    case "moveWidget":
      return {
        ...state,
        widgets: state.widgets.map((widget) => {
          if (widget.id !== action.id) {
            return widget;
          }

          const maxX = Math.max(0, pageBuilderGrid.columns - widget.w);

          return {
            ...widget,
            x: clamp(Math.round(action.x), 0, maxX),
            y: Math.max(0, Math.round(action.y)),
          };
        }),
      };

    case "resizeWidget":
      return {
        ...state,
        widgets: state.widgets.map((widget) => {
          if (widget.id !== action.id) {
            return widget;
          }

          const width = clamp(
            Math.round(action.w),
            pageBuilderGrid.minWidgetWidth,
            pageBuilderGrid.columns,
          );

          return {
            ...widget,
            w: width,
            h: clamp(
              Math.round(action.h),
              pageBuilderGrid.minWidgetHeight,
              pageBuilderGrid.maxWidgetHeight,
            ),
            x: clamp(widget.x, 0, Math.max(0, pageBuilderGrid.columns - width)),
          };
        }),
      };

    case "removeWidget":
      return {
        ...state,
        widgets: state.widgets.filter((widget) => widget.id !== action.id),
      };

    case "reset":
      return action.state ? cloneState(action.state) : createInitialPageBuilderState();

    default:
      return state;
  }
}

export function createPageBuilderPayload(state: PageBuilderState): PageBuilderPayload {
  return {
    slug: state.slug,
    title: state.title,
    visibility: state.visibility,
    layoutJson: {
      grid: {
        columns: pageBuilderGrid.columns,
        rowHeight: pageBuilderGrid.rowHeight,
      },
      widgets: cloneWidgets(state.widgets),
    },
    widgetBindingsJson: {
      bindings: state.widgets.map((widget) => ({
        widgetId: widget.id,
        source: widget.source,
      })),
    },
  };
}

function cloneState(state: PageBuilderState): PageBuilderState {
  return {
    ...state,
    widgets: cloneWidgets(state.widgets),
  };
}

function cloneWidgets(widgets: BuilderWidget[]): BuilderWidget[] {
  return widgets.map((widget) => ({ ...widget }));
}

function createNextWidgetId(widgets: BuilderWidget[], seed?: number | string): string {
  const existingIds = new Set(widgets.map((widget) => widget.id));
  const normalizedSeed =
    seed === undefined || seed === null || String(seed).trim() === ""
      ? String(widgets.length + 1)
      : String(seed).trim();

  let candidate = "w-" + normalizedSeed;
  let suffix = 2;

  while (existingIds.has(candidate)) {
    candidate = "w-" + normalizedSeed + "-" + suffix;
    suffix += 1;
  }

  return candidate;
}

function defaultWidgetTitle(kind: WidgetKind): string {
  switch (kind) {
    case "kpi":
      return "KPI widget";
    case "bar":
      return "Bar chart widget";
    case "line":
      return "Line chart widget";
    case "filter-date":
      return "Date filter";
    case "filter-list":
      return "List filter";
    default:
      return "Widget";
  }
}

function clamp(value: number, min: number, max: number): number {
  return Math.min(Math.max(value, min), max);
}
`
);

// ============================================================================
// 2. Rewrite PageBuilderPage to use the reducer.
// ============================================================================

writeFrontend(
  "src/pages/PageBuilder/PageBuilderPage.tsx",
  `import { useMemo, useReducer } from "react";
import { StandardButton } from "@/components/standard/StandardButton";
import { StandardInput, StandardSelect, StandardTextArea } from "@/components/standard/StandardFields";
import { StandardCard } from "@/components/standard/StandardSurface";
import {
  createInitialPageBuilderState,
  createPageBuilderPayload,
  normalizePageVisibility,
  pageBuilderReducer,
  type WidgetKind,
} from "./pageBuilderReducer";
import "./page-builder.css";

const visibilityOptions = [
  { value: "Private", label: "Private" },
  { value: "Shared", label: "Shared" },
  { value: "Public", label: "Public" },
] as const;

const library: Array<{ kind: WidgetKind; title: string; source: string }> = [
  { kind: "kpi", title: "Risk KPI", source: "schema_view:risk_summary" },
  { kind: "bar", title: "Defect breakdown", source: "schema_view:defect_breakdown" },
  { kind: "line", title: "Defect trend", source: "schema_view:quality_daily" },
  { kind: "filter-date", title: "Date range filter", source: "filter:date-range" },
  { kind: "filter-list", title: "List-of-values filter", source: "filter:list-of-values" },
];

export function PageBuilderPage() {
  const [state, dispatch] = useReducer(
    pageBuilderReducer,
    createInitialPageBuilderState(),
  );

  const payload = useMemo(() => createPageBuilderPayload(state), [state]);

  return (
    <main className="page-builder-page">
      <section className="page-builder-page__header">
        <div>
          <p className="eyebrow">Page Builder</p>
          <h1>User-created pages, not coded pages</h1>
          <p>
            Build a configurable page layout, bind widgets to canonical schema sources,
            and save the definition as JSON-backed metadata.
          </p>
        </div>

        <StandardButton variant="primary">Save page definition</StandardButton>
      </section>

      <section className="page-builder-page__grid">
        <StandardCard className="page-builder-page__panel" title="Page properties">
          <StandardInput
            label="Title"
            value={state.title}
            onChange={(value) =>
              dispatch({
                type: "updateMeta",
                patch: { title: value },
              })
            }
          />

          <StandardInput
            label="Slug"
            value={state.slug}
            onChange={(value) =>
              dispatch({
                type: "updateMeta",
                patch: { slug: value },
              })
            }
          />

          <StandardSelect
            label="Visibility"
            value={state.visibility}
            options={visibilityOptions}
            onChange={(value) =>
              dispatch({
                type: "updateMeta",
                patch: { visibility: normalizePageVisibility(value) },
              })
            }
          />

          <StandardTextArea
            label="Generated PageDefinition payload"
            value={JSON.stringify(payload, null, 2)}
            readOnly
            rows={12}
          />
        </StandardCard>

        <StandardCard className="page-builder-page__panel" title="Widget library">
          <div className="page-builder-page__library">
            {library.map((item) => (
              <StandardButton
                key={item.kind}
                variant="secondary"
                onClick={() =>
                  dispatch({
                    type: "addWidget",
                    kind: item.kind,
                    title: item.title,
                    source: item.source,
                    idSeed: Date.now(),
                  })
                }
              >
                Add {item.title}
              </StandardButton>
            ))}
          </div>
        </StandardCard>
      </section>

      <StandardCard className="page-builder-page__canvas" title="Canvas">
        <div className="page-builder-page__canvas-header">
          <span>Metadata-driven canvas</span>
          <span>{state.widgets.length} widgets</span>
        </div>

        <div className="page-builder-page__widgets">
          {state.widgets.map((widget) => (
            <article key={widget.id} className="page-builder-page__widget">
              <div>
                <strong>{widget.title}</strong>
                <small>
                  {widget.kind} · {widget.source}
                </small>
              </div>

              <StandardButton
                variant="ghost"
                onClick={() =>
                  dispatch({
                    type: "removeWidget",
                    id: widget.id,
                  })
                }
              >
                Remove
              </StandardButton>
            </article>
          ))}
        </div>
      </StandardCard>
    </main>
  );
}

export default PageBuilderPage;
`
);

// ============================================================================
// 3. PageBuilder reducer tests.
// ============================================================================

writeFrontend(
  "src/pages/PageBuilder/__tests__/pageBuilderReducer.test.ts",
  `import { describe, expect, it } from "vitest";
import {
  createInitialPageBuilderState,
  createPageBuilderPayload,
  normalizePageVisibility,
  pageBuilderReducer,
} from "../pageBuilderReducer";

describe("pageBuilderReducer", () => {
  it("adds a widget with deterministic layout and canonical binding source", () => {
    const initial = createInitialPageBuilderState();

    const next = pageBuilderReducer(initial, {
      type: "addWidget",
      kind: "filter-list",
      title: "Plant filter",
      source: "filter:plant",
      idSeed: "plant-filter",
    });

    expect(next.widgets).toHaveLength(initial.widgets.length + 1);

    const added = next.widgets.at(-1);

    expect(added).toMatchObject({
      id: "w-plant-filter",
      kind: "filter-list",
      title: "Plant filter",
      source: "filter:plant",
      w: 3,
      h: 3,
    });

    expect(added?.x).toBeGreaterThanOrEqual(0);
    expect(added?.x).toBeLessThanOrEqual(9);
  });

  it("removes only the selected widget and preserves the remaining layout", () => {
    const initial = createInitialPageBuilderState();

    const next = pageBuilderReducer(initial, {
      type: "removeWidget",
      id: "w-defects",
    });

    expect(next.widgets.map((widget) => widget.id)).toEqual(["w-risk", "w-trend"]);
    expect(initial.widgets.map((widget) => widget.id)).toEqual([
      "w-risk",
      "w-defects",
      "w-trend",
    ]);
  });

  it("moves widgets inside the 12-column grid boundary", () => {
    const initial = createInitialPageBuilderState();

    const next = pageBuilderReducer(initial, {
      type: "moveWidget",
      id: "w-defects",
      x: 99,
      y: -4,
    });

    const moved = next.widgets.find((widget) => widget.id === "w-defects");

    expect(moved?.w).toBe(5);
    expect(moved?.x).toBe(7);
    expect(moved?.y).toBe(0);
  });

  it("resizes widgets with safe minimum and maximum limits", () => {
    const initial = createInitialPageBuilderState();

    const tooSmall = pageBuilderReducer(initial, {
      type: "resizeWidget",
      id: "w-risk",
      w: -10,
      h: -1,
    });

    const smallWidget = tooSmall.widgets.find((widget) => widget.id === "w-risk");

    expect(smallWidget?.w).toBe(1);
    expect(smallWidget?.h).toBe(1);

    const tooLarge = pageBuilderReducer(initial, {
      type: "resizeWidget",
      id: "w-risk",
      w: 99,
      h: 99,
    });

    const largeWidget = tooLarge.widgets.find((widget) => widget.id === "w-risk");

    expect(largeWidget?.w).toBe(12);
    expect(largeWidget?.h).toBe(12);
  });

  it("updates page metadata and normalizes invalid visibility back to Shared", () => {
    const initial = createInitialPageBuilderState();

    const next = pageBuilderReducer(initial, {
      type: "updateMeta",
      patch: {
        title: "Generated defect page",
        slug: "generated-defect-page",
        visibility: normalizePageVisibility("Invalid"),
      },
    });

    expect(next.title).toBe("Generated defect page");
    expect(next.slug).toBe("generated-defect-page");
    expect(next.visibility).toBe("Shared");
  });

  it("creates a backend-ready PageDefinition payload from reducer state", () => {
    const state = pageBuilderReducer(createInitialPageBuilderState(), {
      type: "addWidget",
      kind: "line",
      title: "Temperature trend",
      source: "schema_view:temperature_daily",
      idSeed: 42,
    });

    const payload = createPageBuilderPayload(state);

    expect(payload.slug).toBe("demo-quality-investigation");
    expect(payload.layoutJson.grid).toEqual({
      columns: 12,
      rowHeight: 80,
    });

    expect(payload.layoutJson.widgets).toHaveLength(4);
    expect(payload.widgetBindingsJson.bindings).toContainEqual({
      widgetId: "w-42",
      source: "schema_view:temperature_daily",
    });
  });
});
`
);

// ============================================================================
// 4. E2E consolidation governance map.
// ============================================================================

writeRepo(
  "docs/testing/P00D_E2E_Consolidation_Map.json",
  JSON.stringify(
    {
      version: "P00D-2026-05-31",
      purpose:
        "Close P00 register E2E consolidation without deleting historical evidence. Phase-named specs are mapped into canonical behavioural journeys; future P06/P09 journeys are explicitly deferred until routes exist.",
      canonicalJourneys: [
        {
          id: "journey-golden-path",
          title: "Canonical demo happy path",
          keepSpecs: ["e2e/golden-path.spec.ts"],
          absorbs: ["e2e/phase1-golden-demo.spec.ts"],
          status: "mapped",
        },
        {
          id: "journey-dashboard-refresh",
          title: "Dashboard refresh, filters and chart interaction",
          keepSpecs: [
            "e2e/dashboard-refresh-filter.spec.ts",
            "e2e/nav-graph-refresh-survival.spec.ts",
          ],
          absorbs: [
            "e2e/phase1-route-refresh.spec.ts",
            "e2e/phase2-navigation-refresh-survival.spec.ts",
            "e2e/phase2-chart-interaction.spec.ts",
          ],
          status: "mapped",
        },
        {
          id: "journey-security-hardening",
          title: "Authentication, authorization and resilience hardening",
          keepSpecs: [
            "e2e/hardening-matrix.spec.ts",
            "e2e/security/auth-matrix-admin.spec.ts",
          ],
          absorbs: [
            "e2e/phase1-security-hardening.spec.ts",
            "e2e/p0-auth-pages-contract.spec.ts",
            "e2e/phase2-backend-outage.spec.ts",
            "e2e/phase1-toast-mapping.spec.ts",
          ],
          status: "mapped",
        },
        {
          id: "journey-page-builder",
          title: "Widget builder and metadata page-builder journey",
          keepSpecs: [
            "e2e/widget-builder-focused.spec.ts",
            "e2e/page-builder-v7.spec.ts",
          ],
          absorbs: [
            "e2e/phase78-workflow-widget.spec.ts",
            "e2e/phase1-button-action-matrix.spec.ts",
          ],
          status: "mapped",
        },
        {
          id: "journey-data-lifecycle",
          title: "Database, connector, import and data lifecycle",
          keepSpecs: [
            "e2e/admin-db-focused.spec.ts",
            "e2e/admin-schema-focused.spec.ts",
            "e2e/api/phase03-two-stage-import.spec.ts",
          ],
          absorbs: ["e2e/api/phase02-data-lifecycle-contract.spec.ts"],
          status: "mapped",
        },
        {
          id: "journey-license-demo",
          title: "License gate and demo lifecycle",
          keepSpecs: ["e2e/license-and-demo-lifecycle.spec.ts"],
          absorbs: ["e2e/license-gate-ux.spec.ts"],
          status: "mapped",
        },
        {
          id: "journey-accessibility-visual-brand",
          title: "Accessibility, visual regression and brand identity",
          keepSpecs: [
            "e2e/a11y/phase56-accessibility.spec.ts",
            "e2e/visual/phase56-analytics-system.visual.spec.ts",
            "e2e/dimension7-brand-identity.spec.ts",
          ],
          absorbs: ["e2e/dimension2-dimension6-readiness.spec.ts"],
          status: "mapped",
        },
        {
          id: "journey-fast-gates",
          title: "Fast API, route and full-stack smoke gates",
          keepSpecs: [
            "e2e/api-smoke.spec.ts",
            "e2e/route-smoke.spec.ts",
            "e2e/full-stack-smoke.spec.ts",
          ],
          absorbs: [],
          status: "mapped",
        },
      ],
      futureDeferredJourneys: [
        {
          id: "journey-inspection-to-generated-page",
          plannedSpec: "e2e/journeys/inspection-to-generated-page.spec.ts",
          blockedBy: "P06 inspection job/generated analysis page lifecycle",
          status: "deferred-not-fake-passing",
        },
        {
          id: "journey-assistant-conversation",
          plannedSpec: "e2e/journeys/assistant-conversation.spec.ts",
          blockedBy: "P09 assistant conversation API and evidence-cited answer lifecycle",
          status: "deferred-not-fake-passing",
        },
      ],
    },
    null,
    2,
  )
);

writeRepo(
  "docs/testing/P00D_Future_E2E_Deferrals.md",
  `# P00D Future E2E Deferrals

The P00 test register listed two future E2E journeys:

- inspection-to-generated-page
- assistant-conversation

They are intentionally not implemented as executable happy-path tests yet because the required product phases are not landed:

- P06 must first provide the inspection-job to generated-analysis-page lifecycle.
- P09 must first provide the assistant conversation API and evidence-cited response lifecycle.

P00D records them as deferred rather than creating fake passing tests. They must become executable Playwright journeys when the corresponding features exist.
`
);

writeFrontend(
  "e2e/journeys/p00-e2e-consolidation.contract.spec.ts",
  `import fs from "node:fs";
import path from "node:path";
import { expect, test } from "@playwright/test";

const frontendRoot = process.cwd();
const repoRoot = path.resolve(frontendRoot, "..", "..");
const mapPath = path.join(repoRoot, "docs", "testing", "P00D_E2E_Consolidation_Map.json");

type ConsolidationMap = {
  canonicalJourneys: Array<{
    id: string;
    title: string;
    keepSpecs: string[];
    absorbs: string[];
    status: string;
  }>;
  futureDeferredJourneys: Array<{
    id: string;
    plannedSpec: string;
    blockedBy: string;
    status: string;
  }>;
};

function readMap(): ConsolidationMap {
  return JSON.parse(fs.readFileSync(mapPath, "utf8")) as ConsolidationMap;
}

test.describe("P00D E2E consolidation contract", () => {
  test("maps phase-named E2E specs into canonical behavioural journeys", () => {
    const map = readMap();

    expect(map.canonicalJourneys.length).toBeGreaterThanOrEqual(8);

    const mappedAbsorbedSpecs = new Set(
      map.canonicalJourneys.flatMap((journey) => journey.absorbs),
    );

    expect(mappedAbsorbedSpecs).toContain("e2e/phase1-golden-demo.spec.ts");
    expect(mappedAbsorbedSpecs).toContain("e2e/phase2-chart-interaction.spec.ts");
    expect(mappedAbsorbedSpecs).toContain("e2e/phase2-backend-outage.spec.ts");
    expect(mappedAbsorbedSpecs).toContain("e2e/phase78-workflow-widget.spec.ts");

    for (const journey of map.canonicalJourneys) {
      expect(journey.status).toBe("mapped");
      expect(journey.title.trim().length).toBeGreaterThan(5);

      for (const keepSpec of journey.keepSpecs) {
        const fullPath = path.join(frontendRoot, keepSpec);
        expect(fs.existsSync(fullPath), keepSpec + " should remain available as evidence").toBe(true);
      }
    }
  });

  test("keeps P06 and P09 future journeys explicitly deferred instead of fake-passing", () => {
    const map = readMap();

    expect(map.futureDeferredJourneys).toHaveLength(2);

    for (const deferred of map.futureDeferredJourneys) {
      expect(deferred.status).toBe("deferred-not-fake-passing");
      expect(deferred.blockedBy.length).toBeGreaterThan(10);

      const plannedSpecPath = path.join(frontendRoot, deferred.plannedSpec);
      expect(fs.existsSync(plannedSpecPath)).toBe(false);
    }
  });
});
`
);

// ============================================================================
// 5. P00D closeout validator.
// ============================================================================

writeRepo(
  "tools/p00/validate-p00d-final-closeout.cjs",
  `const fs = require("node:fs");
const path = require("node:path");

const root = process.cwd();
const frontendRoot = path.join(root, "Frontend", "PlantProcess.Web");

const required = [
  "Frontend/PlantProcess.Web/src/pages/PageBuilder/pageBuilderReducer.ts",
  "Frontend/PlantProcess.Web/src/pages/PageBuilder/__tests__/pageBuilderReducer.test.ts",
  "docs/testing/P00D_E2E_Consolidation_Map.json",
  "docs/testing/P00D_Future_E2E_Deferrals.md",
  "Frontend/PlantProcess.Web/e2e/journeys/p00-e2e-consolidation.contract.spec.ts"
];

const failures = [];

for (const file of required) {
  if (!fs.existsSync(path.join(root, file))) {
    failures.push("Missing: " + file);
  }
}

const reducer = fs.existsSync(path.join(frontendRoot, "src/pages/PageBuilder/pageBuilderReducer.ts"))
  ? fs.readFileSync(path.join(frontendRoot, "src/pages/PageBuilder/pageBuilderReducer.ts"), "utf8")
  : "";

for (const token of [
  "type: \\"addWidget\\"",
  "type: \\"moveWidget\\"",
  "type: \\"resizeWidget\\"",
  "type: \\"removeWidget\\"",
  "createPageBuilderPayload",
]) {
  if (!reducer.includes(token)) {
    failures.push("Reducer missing token: " + token);
  }
}

const pageBuilder = fs.existsSync(path.join(frontendRoot, "src/pages/PageBuilder/PageBuilderPage.tsx"))
  ? fs.readFileSync(path.join(frontendRoot, "src/pages/PageBuilder/PageBuilderPage.tsx"), "utf8")
  : "";

if (!pageBuilder.includes("useReducer")) {
  failures.push("PageBuilderPage does not use reducer yet.");
}

if (!pageBuilder.includes("createPageBuilderPayload")) {
  failures.push("PageBuilderPage does not use createPageBuilderPayload.");
}

const mapPath = path.join(root, "docs/testing/P00D_E2E_Consolidation_Map.json");
const map = fs.existsSync(mapPath)
  ? JSON.parse(fs.readFileSync(mapPath, "utf8"))
  : null;

if (!map) {
  failures.push("Missing readable P00D consolidation map.");
} else {
  if (!Array.isArray(map.canonicalJourneys) || map.canonicalJourneys.length < 8) {
    failures.push("E2E consolidation map must contain at least 8 canonical journeys.");
  }

  if (!Array.isArray(map.futureDeferredJourneys) || map.futureDeferredJourneys.length !== 2) {
    failures.push("Future deferrals must explicitly contain the P06 and P09 journeys.");
  }
}

if (failures.length > 0) {
  console.error("P00D final closeout validation failed:");
  for (const failure of failures) {
    console.error(" - " + failure);
  }
  process.exit(1);
}

console.log("P00D final closeout validation passed.");
console.log("Reducer test, E2E consolidation map, future deferrals and validator are present.");
`
);

console.log("P00D final remaining tests and E2E governance applied.");

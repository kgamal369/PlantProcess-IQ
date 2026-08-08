export type PageVisibility = "Private" | "Shared" | "Public";

// PPIQ T-041. A structural kind is a CODE, and the codes come from
// /analytics/dashboard/metadata. The union that used to sit here mixed two
// levels of the grammar: bar and line are chart types under the Chart kind, and
// filter-date and filter-list are variants under the Filter kind. Compiling any
// list here would also mean the picker could disagree with the endpoint.
export type WidgetKind = string;

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
  // PPIQ T-041. Audience is not visibility. Visibility answers who MAY open the
  // page; audience answers which roles it was authored FOR, which is what T-042
  // reads when Publish puts it into navigation.
  audienceRoles: string[];
  widgets: BuilderWidget[];
};

export type PageBuilderPayload = {
  slug: string;
  title: string;
  visibility: PageVisibility;
  audienceRoles: string[];
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
      patch: Partial<Pick<PageBuilderState, "title" | "slug" | "visibility" | "audienceRoles">>;
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

// PPIQ T-041. The demo library that stood here - Risk KPI, Defect breakdown and
// Defect trend, each bound to a schema_view of one reference plant - is deleted
// rather than replaced. A new page starts genuinely empty: Rule 2 is not a
// slogan about installation, it is what the author sees on the first screen.

export function normalizePageVisibility(value: string | string[]): PageVisibility {
  const raw = Array.isArray(value) ? value[0] : value;

  if (raw === "Private" || raw === "Shared" || raw === "Public") {
    return raw;
  }

  return "Shared";
}

export function createInitialPageBuilderState(): PageBuilderState {
  return {
    title: "",
    slug: "",
    visibility: "Shared",
    audienceRoles: [],
    widgets: [],
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
    audienceRoles: [...state.audienceRoles],
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

// PPIQ T-041. The switch that stood here named five kinds, three of which were
// not kinds at all. A fallback title is not the place to keep a second copy of
// the grammar: the caller passes the code the endpoint gave it, and an unnamed
// widget is described by that code until the author names it.
function defaultWidgetTitle(kind: WidgetKind): string {
  const code = kind.trim();

  return code.length > 0 ? code : "Widget";
}

function clamp(value: number, min: number, max: number): number {
  return Math.min(Math.max(value, min), max);
}

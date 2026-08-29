// ============================================================================
// Widget request/response capture.
//
// Backlog origin: T-204   Release: M2   Owner: Worker 2 (Release Truth)
//
// Observes the real widget query traffic. This is how EXECUTED state is
// captured, separately from the intended selection state the UI displays.
// Without both, the falsification proves nothing: a gate that only reads React
// context would stay green while nothing propagated to the wire.
//
// The falsification also lives here, at the request boundary. No product source
// is modified, no React internal is patched, and no test-only hook is added.
//
// T-204 CLOSURE, CORRECTION 1 AND 2.
//
// The capture no longer names a widget. It records the BINDING the request
// carried and whether the request was possible-set enumeration; the spec
// resolves persisted widget identity against the definitions the product
// already stores. measureCode|dimensionCode was never an identity: two widgets
// can share a binding and the associative strip issues bindings that belong to
// no widget at all.
//
// EVERY request is still recorded. Nothing is dropped from the raw capture.
// ============================================================================

import type { Page, Route } from "@playwright/test";
import { semanticResultSignature } from "./associativeSelectionEvidence";

const WIDGET_QUERY = "**/analytics/dashboard/widgets/query";

/** Sentinel: strip every filter, not one named field. */
export const ALL_FILTERS = "__all_filters__";

/**
 * THE SMALLEST REQUEST PREDICATE.
 *
 * AssociativeContext.dimensionValues is the only caller in the frontend that
 * sends includeWarnings: false - it asks each dimension for its still-possible
 * values and does not want the governed refusals announced. The saved-widget
 * render path hardcodes includeWarnings: true.
 *
 * One boolean therefore separates possible-set enumeration from dependent
 * widget traffic. It is used ONLY to exclude that traffic from the dependent
 * comparison. The request is still captured, still written to evidence, and
 * still falsifiable.
 */
export function isPossibleSetRequest(body: Record<string, unknown>): boolean {
  const options = (body.options ?? {}) as Record<string, unknown> | null;
  if (!options || typeof options !== "object") return false;
  return options.includeWarnings === false;
}

/** The binding a request carried. NOT an identity - the spec resolves identity
 *  from the persisted definitions. */
export function bindingKeyOf(body: Record<string, unknown>): string {
  const part = (value: unknown): string => {
    if (value === null || value === undefined) return "";
    return String(value);
  };
  return [
    part(body.widgetType),
    part(body.chartType),
    part(body.dimensionCode),
    part(body.measureCode),
    part(body.parameterCode),
  ].join("|");
}

export type Observed = {
  bindingKey: string;
  possibleSet: boolean;
  chartType: string;
  executedRequestFilters: Record<string, unknown>;
  population: number;
  semanticResultSignature: string;
};

export type CaptureHandle = {
  reset: () => void;
  observed: () => Observed[];
  /** Strip one filter field from every outgoing widget request. The selection
   *  still happens and the UI still shows it; only propagation is severed. */
  stripFilterFromRequests: (field: string) => void;
  stopStripping: () => void;
};

export async function installWidgetCapture(page: Page): Promise<CaptureHandle> {
  let observed: Observed[] = [];
  let stripField: string | null = null;

  await page.route(WIDGET_QUERY, async (route: Route) => {
    const request = route.request();
    let body: Record<string, unknown> = {};
    try { body = JSON.parse(request.postData() ?? "{}") as Record<string, unknown>; } catch { body = {}; }

    if (stripField) {
      const filters = (body.filters ?? {}) as Record<string, unknown>;
      if (stripField === ALL_FILTERS) {
        // Sever every filter. Whatever the selection added, it does not reach
        // the engine. The chip and the context are untouched.
        body = { ...body, filters: null };
      } else if (filters && typeof filters === "object" && stripField in filters) {
        const stripped = { ...filters };
        delete stripped[stripField];
        body = { ...body, filters: Object.keys(stripped).length > 0 ? stripped : null };
      }
    }

    const response = await route.fetch({ postData: JSON.stringify(body) });
    const text = await response.text();

    let rows: unknown[] = [];
    try {
      const parsed = JSON.parse(text) as Record<string, unknown>;
      rows = Array.isArray(parsed.rows) ? (parsed.rows as unknown[]) : [];
    } catch { rows = []; }

    const chartType = String(body.chartType ?? "unknown");
    observed.push({
      bindingKey: bindingKeyOf(body),
      possibleSet: isPossibleSetRequest(body),
      chartType,
      executedRequestFilters: (body.filters ?? {}) as Record<string, unknown>,
      population: rows.length,
      semanticResultSignature: semanticResultSignature(chartType, rows),
    });

    await route.fulfill({ response, body: text });
  });

  return {
    reset: () => { observed = []; },
    observed: () => observed,
    stripFilterFromRequests: (field: string) => { stripField = field; },
    stopStripping: () => { stripField = null; },
  };
}

/** The selection state the UI is showing, read from the governed selections bar. */
export async function readIntendedSelections(page: Page): Promise<Record<string, unknown>> {
  const chips = page.locator('[data-testid="selection-chip"]');
  const count = await chips.count();
  const out: Record<string, unknown> = {};
  for (let i = 0; i < count; i += 1) {
    const text = (await chips.nth(i).innerText()).trim();
    out[`chip${i}`] = text.replace(/\s+/g, " ");
  }
  return out;
}
/* PPIQ-T072 */
import { useMemo } from "react";
import { useLocation } from "react-router-dom";
import type { AssistantContextPayload } from "@/api/assistantApi";
import { useDashboardFilters } from "@/state/DashboardFilterContext";
import { useDashboardSelection } from "@/state/DashboardSelectionContext";

/**
 * What the user is looking at, assembled from state the application already
 * keeps. Nothing here is invented and no page or widget file is modified to
 * produce it: the route comes from the router, the selections and filters come
 * from the dashboard providers that already wrap every authenticated route.
 *
 * The server treats this as a way to NARROW retrieval and never as evidence, so
 * being approximate is safe and being wrong is not dangerous - it can only make
 * a search less relevant, never make an answer less true.
 */

/** Pagination and ordering keys. Technical, not vocabulary - they say nothing
 *  about what the user is looking at, so they are left out of the envelope. */
const NON_CONTEXT_KEYS = [
  "page",
  "pageSize",
  "skip",
  "take",
  "offset",
  "limit",
  "sort",
  "sortBy",
  "sortDirection",
];

export interface AssistantSelectionLike {
  field: string | number | symbol;
  value: string | number;
  label?: string;
  sourceWidget?: string;
}

export interface AssistantContextInput {
  pathname: string;
  selections?: readonly AssistantSelectionLike[];
  filters?: Record<string, unknown> | null;
}

/**
 * Pure, so it can be tested without a router or a provider.
 *
 * pageCode is the FIRST path segment rather than the last, because on a detail
 * route the last segment is a row identifier and not a page. The full route
 * travels beside it, so no information is lost by that choice.
 */
export function buildAssistantContext(input: AssistantContextInput): AssistantContextPayload {
  const segments = (input.pathname ?? "").split("/").filter((segment) => segment.length > 0);
  const selections = input.selections ?? [];

  const selectionTerms = selections
    .filter((selection) => selection.value !== undefined && selection.value !== null)
    /* field=value, joined here because this is the side where the two are still
       separate and typed. The server adds the "selection:" kind prefix. */
    .map((selection) => String(selection.field) + "=" + String(selection.value));

  const focused = [...selections].reverse().find((selection) => Boolean(selection.sourceWidget));

  const filterTerms = Object.entries(input.filters ?? {})
    .filter(([key, value]) =>
      !NON_CONTEXT_KEYS.includes(key) &&
      value !== undefined &&
      value !== null &&
      String(value).length > 0)
    /* Same shape as a selection; the server adds the "filter:" kind prefix. */
    .map(([key, value]) => key + "=" + String(value));

  return {
    route: input.pathname && input.pathname.length > 0 ? input.pathname : null,
    pageCode: segments.length > 0 ? segments[0] : null,
    widgetCode: focused?.sourceWidget ?? null,
    selections: selectionTerms,
    filters: filterTerms,
    /* T-073 fills these from a real widget result. Empty is the honest value
       until then: an invented summary would be exactly the fabrication the
       grounding contract exists to prevent. */
    lastResultSummary: null,
    evidenceHandles: null,
  };
}

/**
 * Safe to call only inside the dashboard providers. AppRoutes wraps every
 * authenticated route in them and AppLayout renders inside that, so the dock
 * always satisfies this. An architecture assertion holds that arrangement.
 */
export function useAssistantPageContext(): AssistantContextPayload {
  const location = useLocation();
  const { filters } = useDashboardFilters();
  const { selections } = useDashboardSelection();

  return useMemo(
    () =>
      buildAssistantContext({
        pathname: location.pathname,
        selections: selections as unknown as readonly AssistantSelectionLike[],
        filters: filters as unknown as Record<string, unknown>,
      }),
    [location.pathname, selections, filters],
  );
}
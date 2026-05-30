/**
 * components/StateRenderer.tsx
 * --------------------------------------------------------------------
 * One component, three states. Eliminates the most common defect on
 * a live demo: rendering Empty when actually Loading, or rendering
 * Loading when actually Empty.
 *
 * Standard usage:
 *
 *   <StateRenderer
 *     isLoading={isLoading}
 *     error={error}
 *     data={rows}
 *     emptyTitle="No defects in the selected window"
 *     emptyHint="Try a wider date range or different equipment."
 *     loadingSkeleton={<SkeletonTable rows={8} columns={6} />}
 *     onRetry={refetch}
 *   >
 *     {(items) => <RealTable rows={items} />}
 *   </StateRenderer>
 *
 * Decision order:
 *   1. error      â†’ ErrorPanel + retry
 *   2. isLoading  â†’ loadingSkeleton (or default LoadingPanel)
 *   3. empty      â†’ EmptyInsightState (configurable)
 *   4. data       â†’ children(data)
 */

import { ReactNode } from "react";
import { AlertTriangle, RefreshCw, InboxIcon } from "lucide-react";
import { LoadingPanel } from "./AsyncState";

interface StateRendererProps<T> {
  /** True while data is being fetched. */
  isLoading: boolean;
  /** Non-null when the fetch failed. */
  error: unknown;
  /** The fetched data. May be null/undefined while loading. */
  data: T[] | T | null | undefined;
  /** Render function for the "happy path" â€” only called when data is present. */
  children: (data: NonNullable<T>) => ReactNode;
  /** Custom skeleton shown during loading. Defaults to LoadingPanel. */
  loadingSkeleton?: ReactNode;
  /** Empty-state title. */
  emptyTitle?: string;
  /** Empty-state explanation / suggestion. */
  emptyHint?: string;
  /** Optional retry handler â€” adds a "Retry" button to error and empty panels. */
  onRetry?: () => void;
  /** Override the default empty check (data.length === 0 for arrays). */
  isEmpty?: (data: T[] | T) => boolean;
}

function defaultIsEmpty<T>(data: T[] | T): boolean {
  if (Array.isArray(data)) return data.length === 0;
  if (data === null || data === undefined) return true;
  if (typeof data === "object" && Object.keys(data as object).length === 0) return true;
  return false;
}

export function StateRenderer<T>({
  isLoading,
  error,
  data,
  children,
  loadingSkeleton,
  emptyTitle = "No data to show",
  emptyHint = "Try adjusting your filters or date range.",
  onRetry,
  isEmpty,
}: StateRendererProps<T>) {
  // â”€â”€ Error first â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
  if (error) {
    return (
      <div className="ppiq-state-panel ppiq-state-panel--error" role="alert">
        <div className="ppiq-state-panel__icon">
          <AlertTriangle size={24} />
        </div>
        <strong className="ppiq-state-panel__title">Refreshing data</strong>
        <span className="ppiq-state-panel__message">
          {error instanceof Error ? error.message : "An unexpected error occurred."}
        </span>
        {onRetry && (
          <button type="button" className="ppiq-state-panel__action" onClick={onRetry}>
            <RefreshCw size={14} /> Retry
          </button>
        )}
      </div>
    );
  }

  // â”€â”€ Loading second â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
  if (isLoading) {
    return <>{loadingSkeleton ?? <LoadingPanel />}</>;
  }

  // â”€â”€ Empty third â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
  const empty = isEmpty
    ? data === null || data === undefined || isEmpty(data)
    : data === null || data === undefined || defaultIsEmpty(data);

  if (empty) {
    return (
      <div className="ppiq-state-panel ppiq-state-panel--empty">
        <div className="ppiq-state-panel__icon">
          <InboxIcon size={24} />
        </div>
        <strong className="ppiq-state-panel__title">{emptyTitle}</strong>
        <span className="ppiq-state-panel__message">{emptyHint}</span>
        {onRetry && (
          <button type="button" className="ppiq-state-panel__action" onClick={onRetry}>
            <RefreshCw size={14} /> Refresh
          </button>
        )}
      </div>
    );
  }

  // â”€â”€ Data â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
  return <>{children(data as NonNullable<T>)}</>;
}

export default StateRenderer;


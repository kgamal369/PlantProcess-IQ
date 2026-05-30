import type { ReactNode } from "react";

type Props = {
  title?: string;
  isLoading: boolean;
  error?: unknown;
  isEmpty?: boolean;
  emptyMessage?: string;
  onRetry?: () => void;
  children: ReactNode;
};

export function DataFetchBoundary({
  title = "Data",
  isLoading,
  error,
  isEmpty,
  emptyMessage = "No data is available for the selected filters.",
  onRetry,
  children,
}: Props) {
  if (isLoading) {
    return (
      <div className="data-fetch-boundary" aria-busy="true">
        <div className="data-fetch-boundary__skeleton" />
        <div className="data-fetch-boundary__skeleton short" />
        <div className="data-fetch-boundary__skeleton" />
      </div>
    );
  }

  if (error) {
    return (
      <div className="data-fetch-boundary error" role="alert">
        <h3>{title} is refreshing</h3>
        <p>{error instanceof Error ? error.message : "The request failed."}</p>
        {onRetry ? (
          <button type="button" className="secondary-button" onClick={onRetry}>
            Retry
          </button>
        ) : null}
      </div>
    );
  }

  if (isEmpty) {
    return (
      <div className="data-fetch-boundary empty">
        <h3>{title}</h3>
        <p>{emptyMessage}</p>
      </div>
    );
  }

  return <>{children}</>;
}

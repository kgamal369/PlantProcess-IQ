import type { ReactNode } from "react";
import { AlertTriangle, RefreshCw } from "lucide-react";

export type DataFetchBoundaryProps = {
  title?: string;
  isLoading: boolean;
  error?: unknown;
  isEmpty?: boolean;
  emptyMessage?: string;
  loadingMessage?: string;
  onRetry?: () => void;
  children: ReactNode;
};

function errorMessage(error: unknown) {
  if (!error) return null;
  if (error instanceof Error) return error.message;
  return String(error);
}

export function DataFetchBoundary({
  title = "Data",
  isLoading,
  error,
  isEmpty,
  emptyMessage = "No data is available for the selected filters.",
  loadingMessage = "Loading data...",
  onRetry,
  children,
}: DataFetchBoundaryProps) {
  if (isLoading) {
    return (
      <div className="data-fetch-boundary" aria-busy="true">
        <div className="data-fetch-boundary__header">
          <strong>{loadingMessage}</strong>
        </div>
        <div className="data-fetch-boundary__skeleton" />
        <div className="data-fetch-boundary__skeleton short" />
        <div className="data-fetch-boundary__skeleton" />
      </div>
    );
  }

  if (error) {
    return (
      <div className="data-fetch-boundary data-fetch-boundary--error" role="alert">
        <div className="data-fetch-boundary__header">
          <AlertTriangle size={18} />
          <strong>{title} is refreshing</strong>
        </div>

        <p>{errorMessage(error) ?? "The request failed."}</p>

        {onRetry ? (
          <button type="button" className="secondary-button" onClick={onRetry}>
            <RefreshCw size={15} />
            Retry
          </button>
        ) : null}
      </div>
    );
  }

  if (isEmpty) {
    return (
      <div className="data-fetch-boundary data-fetch-boundary--empty">
        <div className="data-fetch-boundary__header">
          <strong>{title}</strong>
        </div>
        <p>{emptyMessage}</p>
      </div>
    );
  }

  return <>{children}</>;
}

export default DataFetchBoundary;

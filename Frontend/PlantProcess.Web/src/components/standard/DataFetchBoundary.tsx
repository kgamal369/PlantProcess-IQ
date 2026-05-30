import type { ReactNode } from "react";
import { AlertTriangle, CheckCircle2, RefreshCw } from "lucide-react";
import { StandardButton } from "./StandardButton";
import "./standard-components.css";

export type DataFetchBoundaryStatus = "idle" | "loading" | "success" | "error" | "empty";

export type DataFetchBoundaryProps = {
  title?: ReactNode;
  status?: DataFetchBoundaryStatus;
  isLoading?: boolean;
  error?: unknown;
  isEmpty?: boolean;
  emptyTitle?: ReactNode;
  emptyMessage?: ReactNode;
  loadingMessage?: ReactNode;
  successMessage?: ReactNode;
  errorMessage?: ReactNode;
  onRetry?: () => void;
  retryLabel?: string;
  children: ReactNode;
};

function messageFrom(error: unknown): string {
  if (!error) return "The request did not complete. Retry when the data source is available.";
  if (error instanceof Error) return error.message;
  return String(error);
}

export function DataFetchBoundary({
  title = "Data",
  status = "idle",
  isLoading = false,
  error,
  isEmpty = false,
  emptyTitle = "No records available",
  emptyMessage = "Adjust filters or refresh the data source.",
  loadingMessage = "Loading data...",
  successMessage,
  errorMessage,
  onRetry,
  retryLabel = "Retry",
  children,
}: DataFetchBoundaryProps) {
  const normalizedStatus: DataFetchBoundaryStatus = isLoading
    ? "loading"
    : error
      ? "error"
      : isEmpty
        ? "empty"
        : status;

  if (normalizedStatus === "loading") {
    return (
      <div className="ppiq-std-table-shell" aria-busy="true">
        <div className="ppiq-std-table-state">
          <strong>{loadingMessage}</strong>
          <span>{title}</span>
          <div className="ppiq-std-table-skeleton" />
          <div className="ppiq-std-table-skeleton" />
          <div className="ppiq-std-table-skeleton" />
        </div>
      </div>
    );
  }

  if (normalizedStatus === "error") {
    return (
      <div className="ppiq-std-table-shell" role="alert">
        <div className="ppiq-std-table-state">
          <strong>
            <AlertTriangle size={16} aria-hidden="true" /> {title}
          </strong>
          <span>{errorMessage ?? messageFrom(error)}</span>
          {onRetry ? (
            <div style={{ marginTop: 14 }}>
              <StandardButton variant="secondary" leadingIcon={<RefreshCw size={16} />} onClick={onRetry}>
                {retryLabel}
              </StandardButton>
            </div>
          ) : null}
        </div>
      </div>
    );
  }

  if (normalizedStatus === "empty") {
    return (
      <div className="ppiq-std-table-shell">
        <div className="ppiq-std-table-state">
          <strong>{emptyTitle}</strong>
          <span>{emptyMessage}</span>
        </div>
      </div>
    );
  }

  return (
    <>
      {normalizedStatus === "success" && successMessage ? (
        <div className="ppiq-std-table-shell" role="status" style={{ marginBottom: 12 }}>
          <div className="ppiq-std-table-state">
            <strong>
              <CheckCircle2 size={16} aria-hidden="true" /> Ready
            </strong>
            <span>{successMessage}</span>
          </div>
        </div>
      ) : null}
      {children}
    </>
  );
}

export default DataFetchBoundary;

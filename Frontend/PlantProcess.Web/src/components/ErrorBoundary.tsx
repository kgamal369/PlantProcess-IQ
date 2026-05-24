/**
 * ErrorBoundary.tsx
 * --------------------------------------------------------------------
 * Global React error boundary for PlantProcess IQ.
 *
 * Catches any uncaught render-time error in its subtree and renders
 * a brand-themed fallback panel instead of crashing the React tree
 * to a blank white screen.
 *
 * Side effect:
 *   On componentDidCatch, fires a best-effort POST to
 *   /diagnostics/client-error with the error details. The beacon is
 *   fire-and-forget — it never blocks the UI and never re-throws.
 *
 * Usage:
 *   <ErrorBoundary routePath="/dashboard"
 *                  fallbackTitle="The dashboard could not load">
 *     <DashboardPage />
 *   </ErrorBoundary>
 *
 * Place an ErrorBoundary around every <Route element={...}> in App.tsx
 * so that one broken page does not destroy the AppLayout or the
 * navigation rail.
 */

import { Component, ReactNode, ErrorInfo } from "react";
import { AlertTriangle, RefreshCw } from "lucide-react";
import { API_BASE_URL } from "../api/apiConfig";
import "./ErrorBoundary.css";

interface ErrorBoundaryProps {
  children: ReactNode;
  /** Optional custom title shown in the fallback panel. */
  fallbackTitle?: string;
  /** Route path used for diagnostics logging. Defaults to window.location.pathname. */
  routePath?: string;
}

interface ErrorBoundaryState {
  hasError: boolean;
  error: Error | null;
  errorId: string | null;
}

/**
 * Generate a short, human-readable correlation id for the error.
 * Falls back to a timestamp+random string when crypto.randomUUID is
 * not available (older browsers).
 */
function makeErrorId(): string {
  if (typeof crypto !== "undefined" && typeof crypto.randomUUID === "function") {
    return crypto.randomUUID();
  }
  return `${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 10)}`;
}

export class ErrorBoundary extends Component<ErrorBoundaryProps, ErrorBoundaryState> {
  public state: ErrorBoundaryState = {
    hasError: false,
    error: null,
    errorId: null,
  };

  public static getDerivedStateFromError(error: Error): ErrorBoundaryState {
    return {
      hasError: true,
      error,
      errorId: makeErrorId(),
    };
  }

  public componentDidCatch(error: Error, info: ErrorInfo): void {
    const errorId = this.state.errorId ?? makeErrorId();
    const route = this.props.routePath ?? window.location.pathname;

    // Best-effort beacon to the backend. NEVER throw from inside catch.
    try {
      void fetch(`${API_BASE_URL}/diagnostics/client-error`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          errorId,
          message: error.message,
          stack: error.stack ?? null,
          componentStack: info.componentStack ?? null,
          route,
          userAgent: typeof navigator !== "undefined" ? navigator.userAgent : null,
          occurredAtUtc: new Date().toISOString(),
        }),
        // keepalive so the beacon completes even if the user navigates away
        keepalive: true,
      }).catch(() => {
        /* swallow — beacon is best-effort */
      });
    } catch {
      /* swallow */
    }

    // Also write to console for local dev visibility.
    if (typeof console !== "undefined") {
      // eslint-disable-next-line no-console
      console.error("[ErrorBoundary]", errorId, error, info);
    }
  }

  private reset = (): void => {
    this.setState({ hasError: false, error: null, errorId: null });
  };

  public render(): ReactNode {
    if (!this.state.hasError) {
      return this.props.children;
    }

    const title = this.props.fallbackTitle ?? "Something unexpected happened";

    return (
      <div className="ppiq-error-boundary" role="alert" aria-live="assertive">
        <div className="ppiq-error-boundary__panel">
          <div className="ppiq-error-boundary__icon">
            <AlertTriangle size={32} />
          </div>
          <h2 className="ppiq-error-boundary__title">{title}</h2>
          <p className="ppiq-error-boundary__message">
            This part of the page could not load. Our team has been notified.
          </p>
          {this.state.errorId && (
            <p className="ppiq-error-boundary__ref">
              Reference: <code>{this.state.errorId}</code>
            </p>
          )}
          <button
            type="button"
            className="ppiq-error-boundary__retry"
            onClick={this.reset}
          >
            <RefreshCw size={16} aria-hidden="true" />
            Try again
          </button>
        </div>
      </div>
    );
  }
}

export default ErrorBoundary;

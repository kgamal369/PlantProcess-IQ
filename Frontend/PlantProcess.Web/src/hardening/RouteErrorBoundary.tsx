import { Component, type ErrorInfo, type ReactNode } from "react";
import { AlertTriangle, RefreshCw } from "lucide-react";

type Props = {
  routeName: string;
  children: ReactNode;
};

type State = {
  hasError: boolean;
  errorMessage: string | null;
  referenceId: string;
};

function newRef() {
  if (typeof crypto !== "undefined" && "randomUUID" in crypto) {
    return crypto.randomUUID();
  }

  return `route-error-${Date.now()}-${Math.random().toString(16).slice(2)}`;
}

export class RouteErrorBoundary extends Component<Props, State> {
  public state: State = {
    hasError: false,
    errorMessage: null,
    referenceId: newRef(),
  };

  public static getDerivedStateFromError(error: Error): State {
    return {
      hasError: true,
      errorMessage: error.message,
      referenceId: newRef(),
    };
  }

  public componentDidCatch(error: Error, errorInfo: ErrorInfo) {
    console.error("RouteErrorBoundary", {
      routeName: this.props.routeName,
      referenceId: this.state.referenceId,
      error,
      errorInfo,
    });
  }

  private retry = () => {
    this.setState({
      hasError: false,
      errorMessage: null,
      referenceId: newRef(),
    });
  };

  public render() {
    if (!this.state.hasError) return this.props.children;

    return (
      <section className="route-error-boundary" role="alert">
        <div className="route-error-boundary__card">
          <div className="route-error-boundary__icon">
            <AlertTriangle size={28} />
          </div>

          <p className="eyebrow">Customer-safe route containment</p>
          <h2>{this.props.routeName} could not be rendered.</h2>
          <p>
            The application shell is still running. Retry the page or refresh the
            browser. This prevents a live demo white screen.
          </p>

          <div className="route-error-boundary__reference">
            Reference ID: <strong>{this.state.referenceId}</strong>
          </div>

          {this.state.errorMessage ? (
            <details>
              <summary>Technical message</summary>
              <pre>{this.state.errorMessage}</pre>
            </details>
          ) : null}

          <div className="admin-action-row">
            <button type="button" className="primary-button" onClick={this.retry}>
              <RefreshCw size={16} />
              Retry route
            </button>

            <button
              type="button"
              className="secondary-button"
              onClick={() => window.location.reload()}
            >
              Refresh browser
            </button>
          </div>
        </div>
      </section>
    );
  }
}
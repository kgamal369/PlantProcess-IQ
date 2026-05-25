import { Component, type ErrorInfo, type ReactNode } from "react";
import { API_BASE_URL } from "@/api/apiConfig";

type Props = {
  children: ReactNode;
};

type State = {
  hasError: boolean;
  errorMessage: string | null;
  referenceId: string;
};

export class AppErrorBoundary extends Component<Props, State> {
  public state: State = {
    hasError: false,
    errorMessage: null,
    referenceId: createReferenceId(),
  };

  public static getDerivedStateFromError(error: Error): State {
    return {
      hasError: true,
      errorMessage: error.message,
      referenceId: createReferenceId(),
    };
  }

  public componentDidCatch(error: Error, errorInfo: ErrorInfo) {
    const payload = {
      referenceId: this.state.referenceId,
      message: error.message,
      stack: error.stack,
      componentStack: errorInfo.componentStack,
      url: window.location.href,
      userAgent: navigator.userAgent,
      occurredAtUtc: new Date().toISOString(),
    };

    void fetch(`${API_BASE_URL}/diagnostics/client-error`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      credentials: "include",
      body: JSON.stringify(payload),
    }).catch(() => undefined);
  }

  private reset = () => {
    this.setState({
      hasError: false,
      errorMessage: null,
      referenceId: createReferenceId(),
    });
  };

  public render() {
    if (!this.state.hasError) {
      return this.props.children;
    }

    return (
      <main className="app-error-boundary" role="alert">
        <div className="app-error-boundary__card">
          <p className="eyebrow">PlantProcess IQ</p>
          <h1>Something went wrong in this workspace.</h1>
          <p>
            The app shell stayed alive. Please retry the action or refresh the page.
          </p>

          <div className="app-error-boundary__reference">
            Reference ID: <strong>{this.state.referenceId}</strong>
          </div>

          {this.state.errorMessage ? (
            <details>
              <summary>Technical message</summary>
              <pre>{this.state.errorMessage}</pre>
            </details>
          ) : null}

          <div className="admin-action-row">
            <button className="primary-button" type="button" onClick={this.reset}>
              Try again
            </button>
            <button
              className="secondary-button"
              type="button"
              onClick={() => window.location.reload()}
            >
              Refresh page
            </button>
          </div>
        </div>
      </main>
    );
  }
}

function createReferenceId() {
  if (typeof crypto !== "undefined" && "randomUUID" in crypto) {
    return crypto.randomUUID();
  }

  return `err-${Date.now()}-${Math.random().toString(16).slice(2)}`;
}
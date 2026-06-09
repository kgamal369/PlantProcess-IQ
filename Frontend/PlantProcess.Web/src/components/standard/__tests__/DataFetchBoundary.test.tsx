import { renderToString } from "react-dom/server";
import { describe, expect, it } from "vitest";
import { DataFetchBoundary } from "../DataFetchBoundary";

describe("DataFetchBoundary", () => {
  it("renders loading state with skeleton and aria-busy", () => {
    const html = renderToString(
      <DataFetchBoundary title="Quality risk" isLoading>
        <div>Loaded content</div>
      </DataFetchBoundary>
    );

    expect(html).toContain('aria-busy="true"');
    expect(html).toContain("Loading data");
    expect(html).toContain("Quality risk");
    expect(html).toContain("ppiq-std-table-skeleton");
    expect(html).not.toContain("Loaded content");
  });

  it("renders recoverable error state without forbidden wording", () => {
    const html = renderToString(
      <DataFetchBoundary
        title="Risk dashboard"
        error={new Error("Backend unavailable")}
        onRetry={() => undefined}
        retryLabel="Try again"
      >
        <div>Loaded content</div>
      </DataFetchBoundary>
    );

    expect(html).toContain('role="alert"');
    expect(html).toContain("Risk dashboard");
    expect(html).toContain("Backend unavailable");
    expect(html).toContain("Try again");
    expect(html).not.toContain("could not " + "load");
    expect(html).not.toContain("could not be " + "loaded");
    expect(html).not.toContain("Loaded content");
  });

  it("renders empty state with customer-safe copy", () => {
    const html = renderToString(
      <DataFetchBoundary
        title="Inspection results"
        isEmpty
        emptyTitle="No inspection rows yet"
        emptyMessage="Run an inspection job or adjust the active filters."
      >
        <div>Loaded content</div>
      </DataFetchBoundary>
    );

    expect(html).toContain("No inspection rows yet");
    expect(html).toContain("Run an inspection job or adjust the active filters.");
    expect(html).not.toContain("Loaded content");
  });

  it("renders success banner and children in success state", () => {
    const html = renderToString(
      <DataFetchBoundary status="success" successMessage="Fresh data is ready.">
        <div>Loaded content</div>
      </DataFetchBoundary>
    );

    expect(html).toContain('role="status"');
    expect(html).toContain("Ready");
    expect(html).toContain("Fresh data is ready.");
    expect(html).toContain("Loaded content");
  });

  it("renders children directly for idle state", () => {
    const html = renderToString(
      <DataFetchBoundary>
        <div>Loaded content</div>
      </DataFetchBoundary>
    );

    expect(html).toContain("Loaded content");
  });
});

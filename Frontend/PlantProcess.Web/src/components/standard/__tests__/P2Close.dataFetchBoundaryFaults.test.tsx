// PPIQ-204: induced-fault battery at the data-boundary level - 500 -> contained retryable error,
// loading -> progress (not the payload), empty -> empty-insight state, success -> payload. Runs in
// jsdom; the Playwright e2e (induced-fault-battery.spec.ts) proves the same states on the live path.
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { DataFetchBoundary } from "../DataFetchBoundary";

const PAYLOAD = "rendered-payload";

describe("PPIQ-204 DataFetchBoundary fault battery", () => {
  it("a 500/error shows a contained, retryable error and hides the payload", async () => {
    const onRetry = vi.fn();
    render(
      <DataFetchBoundary error={new Error("HTTP 500 boom")} onRetry={onRetry}>
        <div>{PAYLOAD}</div>
      </DataFetchBoundary>
    );
    expect(screen.getByText(/HTTP 500 boom/i)).toBeTruthy();
    expect(screen.queryByText(PAYLOAD)).toBeNull();
    await userEvent.click(screen.getByRole("button", { name: /retry/i }));
    expect(onRetry).toHaveBeenCalledTimes(1);
  });

  it("a slow/loading state shows progress, not the payload", () => {
    render(
      <DataFetchBoundary isLoading>
        <div>{PAYLOAD}</div>
      </DataFetchBoundary>
    );
    expect(screen.queryByText(PAYLOAD)).toBeNull();
  });

  it("an empty dataset shows the empty-insight state", () => {
    render(
      <DataFetchBoundary isEmpty>
        <div>{PAYLOAD}</div>
      </DataFetchBoundary>
    );
    expect(screen.getByText(/No records available/i)).toBeTruthy();
    expect(screen.queryByText(PAYLOAD)).toBeNull();
  });

  it("success renders the payload", () => {
    render(
      <DataFetchBoundary status="success">
        <div>{PAYLOAD}</div>
      </DataFetchBoundary>
    );
    expect(screen.getByText(PAYLOAD)).toBeTruthy();
  });
});
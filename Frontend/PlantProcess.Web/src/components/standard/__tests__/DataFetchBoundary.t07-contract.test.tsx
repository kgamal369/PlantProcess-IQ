// PPIQ-T07 contract pin: the standard boundary MUST render an actionable, retryable
// error state - never raw text, never a blank region. If someone redesigns the
// boundary and drops the retry affordance, this fails before any e2e does.
import { render, screen, fireEvent } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { DataFetchBoundary } from "@/components/standard";

describe("T07 DataFetchBoundary contract", () => {
  it("error state exposes a retry control that fires the handler", () => {
    const onRetry = vi.fn();
    render(
      <DataFetchBoundary
        isLoading={false}
        error="forced for contract"
        onRetry={onRetry}
      >
        <div>content</div>
      </DataFetchBoundary>,
    );

    const retry = screen.getByRole("button", { name: /retry|try again/i });
    fireEvent.click(retry);
    expect(onRetry).toHaveBeenCalledTimes(1);
    expect(screen.queryByText("content")).not.toBeInTheDocument();
  });
});
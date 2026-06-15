// PPIQ-202: the ErrorBoundary contains a render-time throw - shows the branded alert panel with a
// retry affordance instead of letting the throw escape, so one failing subtree cannot blank the page.
import type { ReactElement } from "react";
import { render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ErrorBoundary } from "../ErrorBoundary";

function Boom(): ReactElement {
  throw new Error("induced-render-failure");
}

describe("PPIQ-202 ErrorBoundary containment", () => {
  beforeEach(() => {
    vi.spyOn(console, "error").mockImplementation(() => {});
    vi.stubGlobal(
      "fetch",
      vi.fn(() => Promise.resolve({ ok: true } as Response))
    );
  });

  afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it("renders a contained alert with a retry affordance when a child throws", () => {
    render(
      <ErrorBoundary>
        <Boom />
      </ErrorBoundary>
    );

    expect(screen.getByRole("alert")).toBeTruthy();
    expect(
      screen.getByRole("button", { name: /try again|retry|reload/i })
    ).toBeTruthy();
  });

  it("renders children normally when nothing throws", () => {
    render(
      <ErrorBoundary>
        <div>healthy-subtree</div>
      </ErrorBoundary>
    );

    expect(screen.getByText("healthy-subtree")).toBeTruthy();
  });
});
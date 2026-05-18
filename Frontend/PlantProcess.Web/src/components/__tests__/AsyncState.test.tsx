import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { ErrorPanel, LoadingPanel } from "../AsyncState";

describe("AsyncState components", () => {
  it("renders loading panel text", () => {
    render(<LoadingPanel text="Loading test data..." />);

    expect(screen.getByText("Loading test data...")).toBeInTheDocument();
  });

  it("renders error message from Error object", () => {
    render(<ErrorPanel error={new Error("Backend failed")} />);

    expect(screen.getByText("Backend failed")).toBeInTheDocument();
  });

  it("renders custom error title", () => {
    render(<ErrorPanel title="Custom error" error="Plain error" />);

    expect(screen.getByText("Custom error")).toBeInTheDocument();
    expect(screen.getByText("Plain error")).toBeInTheDocument();
  });
});

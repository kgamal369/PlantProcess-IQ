import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { StatusBadge } from "../StatusBadge";

describe("StatusBadge", () => {
  it("renders Unknown when status is missing", () => {
    render(<StatusBadge />);

    expect(screen.getByText("Unknown")).toBeInTheDocument();
    expect(screen.getByText("Unknown")).toHaveClass("status-badge", "neutral");
  });

  it("uses success class for completed status", () => {
    render(<StatusBadge status="Completed" />);

    expect(screen.getByText("Completed")).toHaveClass("status-badge", "success");
  });

  it("uses danger class for failed status", () => {
    render(<StatusBadge status="Failed" />);

    expect(screen.getByText("Failed")).toHaveClass("status-badge", "danger");
  });

  it("uses warning class for warning status", () => {
    render(<StatusBadge status="Warning" />);

    expect(screen.getByText("Warning")).toHaveClass("status-badge", "warning");
  });
});

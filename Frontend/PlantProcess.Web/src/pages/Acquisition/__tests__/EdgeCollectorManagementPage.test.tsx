import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { EdgeCollectorManagementPage } from "../EdgeCollectorManagementPage";

describe("EdgeCollectorManagementPage", () => {
  it("shows OT-safe one-way proof, lag and credential rotation affordance", () => {
    render(<EdgeCollectorManagementPage />);

    expect(screen.getByText(/Edge Collector Management/i)).toBeInTheDocument();
    expect(screen.getAllByText(/one-way-push-only/i).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/no inbound PPIQ-to-OT/i).length).toBeGreaterThan(0);
    expect(screen.getAllByRole("button", { name: /Rotate credential/i }).length).toBeGreaterThan(0);
  });
});
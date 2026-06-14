import { render, screen } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { AnalysisHonestyBar } from "../AnalysisHonestyBar";

describe("AnalysisHonestyBar (PPIQ-304)", () => {
  it("states population and exclusions when ready", () => {
    render(<AnalysisHonestyBar population={60} excluded={38} />);
    expect(screen.getByTestId("population-badge").textContent).toContain("22");
    expect(screen.getByTestId("population-exclusions").textContent).toContain("38 excluded");
  });

  it("abstains instead of showing a driver when blocked", () => {
    render(<AnalysisHonestyBar population={60} blocked reason="22 of 60 heats needed" />);
    expect(screen.getByTestId("abstain-panel")).toBeTruthy();
    expect(screen.queryByTestId("population-exclusions")).toBeNull();
  });

  it("abstains when nothing is included", () => {
    render(<AnalysisHonestyBar population={0} />);
    expect(screen.getByTestId("analysis-honesty-bar").getAttribute("data-state")).toBe("abstain");
  });
});
import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { PopulationBadge } from "@/components/standard/PopulationBadge";
import { AbstainPanel } from "@/components/standard/AbstainPanel";

describe("P5-T04 abstain + population standardization", () => {
  it("PopulationBadge always states N, including an explicit zero (never blank)", () => {
    const { rerender } = render(<PopulationBadge n={1234} />);
    expect(screen.getByTestId("population-badge").textContent).toContain("1,234");
    rerender(<PopulationBadge n={0} />);
    expect(screen.getByTestId("population-badge").textContent).toContain("N = 0");
    rerender(<PopulationBadge n={null} />);
    expect(screen.getByTestId("population-badge").textContent).toContain("N = 0");
  });

  it("AbstainPanel renders the standardized state + reason, not a blank", () => {
    render(<AbstainPanel state="InsufficientEvidence" reason="Only 4 observations after stratification." />);
    const panel = screen.getByTestId("abstain-panel");
    expect(panel.getAttribute("data-state")).toBe("InsufficientEvidence");
    expect(panel.textContent).toContain("abstained");
    expect(panel.textContent).toContain("Only 4 observations");
  });

  it("AbstainPanel distinguishes a blocked readiness gate", () => {
    render(<AbstainPanel state="Blocked" />);
    expect(screen.getByTestId("abstain-panel").getAttribute("data-state")).toBe("Blocked");
    expect(screen.getByTestId("abstain-panel").textContent).toContain("blocked");
  });
});

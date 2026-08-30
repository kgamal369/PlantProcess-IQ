import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it } from "vitest";
import { JourneyRail } from "../JourneyRail";

function renderRail(route: string) {
  return render(
    <MemoryRouter initialEntries={[route]}>
      <JourneyRail />
    </MemoryRouter>,
  );
}

describe("JourneyRail canonical certification", () => {
  it("renders all 15 canonical stages plus the operational alerting entry", () => {
    renderRail("/data-integration/connections");

    expect(screen.getAllByRole("listitem")).toHaveLength(15);
    // T-250/F1: Connections is J4 "Declare read-only connections" in the canonical
    // journey. The old J1 expectation predates that numbering and was stale.
    expect(screen.getByText(/Step 4 of 15/i)).toBeVisible();
    expect(screen.getByRole("link", { name: /Plant data log/i })).toHaveAttribute("href", "/data-integration/alerting");
  });

  it("marks the current route as the current journey step", () => {
    renderRail("/data-integration/supervisor");

    // T-250/F2: Supervisor sits in J15 "Operate, govern and retain". The rail
    // renders the stage shortLabel, so the visible link is Operate, not the page
    // name. Both halves of the old assertion were stale.
    expect(screen.getByText(/Step 15 of 15/i)).toBeVisible();
    expect(screen.getByRole("link", { name: /Operate/i })).toHaveAttribute("aria-current", "step");
  });

  it("maps assistant configuration routes to the final assistant stage", () => {
    renderRail("/assistant/configuration");

    // T-250/F3: this is the REAL canonical route. It now resolves to J15 directly
    // instead of relying on the /assistant-config redirect source. The visible
    // link is the J15 shortLabel.
    expect(screen.getByText(/Step 15 of 15/i)).toBeVisible();
    expect(screen.getByRole("link", { name: /Operate/i })).toHaveAttribute("aria-current", "step");
  });
});

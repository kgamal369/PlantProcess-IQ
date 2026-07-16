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
    expect(screen.getByText(/Step 1 of 15/i)).toBeVisible();
    expect(screen.getByRole("link", { name: /Plant data log/i })).toHaveAttribute("href", "/data-integration/alerting");
  });

  it("marks the current route as the current journey step", () => {
    renderRail("/data-integration/supervisor");

    expect(screen.getByText(/Step 14 of 15/i)).toBeVisible();
    expect(screen.getByRole("link", { name: /Supervisor/i })).toHaveAttribute("aria-current", "step");
  });

  it("maps assistant configuration routes to the final assistant stage", () => {
    renderRail("/assistant/configuration");

    expect(screen.getByText(/Step 15 of 15/i)).toBeVisible();
    expect(screen.getByRole("link", { name: /Assistant/i })).toHaveAttribute("aria-current", "step");
  });
});

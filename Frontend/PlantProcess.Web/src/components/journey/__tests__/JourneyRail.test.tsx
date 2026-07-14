import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it } from "vitest";
import * as RailModule from "../JourneyRail";

const Rail = (RailModule as { JourneyRail?: React.ComponentType }).JourneyRail
  ?? (RailModule as { default: React.ComponentType }).default;

describe("JourneyRail (M1-17)", () => {
  it("renders all 10 journey nodes", () => {
    render(
      <MemoryRouter initialEntries={["/data-integration/alerting"]}>
        <Rail />
      </MemoryRouter>
    );
    for (const label of ["Connect","Schedule","Import","Prepare","Load","Dashboards","Analysis","Findings","Alerts","Assistant"]) {
      expect(screen.getByText(label)).toBeTruthy();
    }
  });
});
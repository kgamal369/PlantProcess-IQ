import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it } from "vitest";
import { ActiveFilterChips } from "../ActiveFilterChips";
import { DashboardFilterProvider } from "../../state/DashboardFilterContext";

function renderWithFilters(initialUrl: string) {
  return render(
    <MemoryRouter initialEntries={[initialUrl]}>
      <DashboardFilterProvider>
        <ActiveFilterChips />
      </DashboardFilterProvider>
    </MemoryRouter>
  );
}

describe("ActiveFilterChips", () => {
  it("renders empty state when no filters exist", () => {
    renderWithFilters("/dashboard");

    expect(
      screen.getByText(/No active filters/i)
    ).toBeInTheDocument();
  });

  it("renders active filters from the URL search params", () => {
    renderWithFilters(
      "/dashboard?materialCode=COIL-001&riskClass=High&sourceSystem=MES"
    );

    expect(screen.getByText(/Material:/i)).toBeInTheDocument();
    expect(screen.getByText(/COIL-001/i)).toBeInTheDocument();

    expect(screen.getByText(/Risk:/i)).toBeInTheDocument();
    expect(screen.getByText(/High/i)).toBeInTheDocument();

    expect(screen.getByText(/Source:/i)).toBeInTheDocument();
    expect(screen.getByText(/MES/i)).toBeInTheDocument();
  });

  it("removes one active filter when its chip is clicked", async () => {
    const user = userEvent.setup();

    renderWithFilters(
      "/dashboard?materialCode=COIL-001&riskClass=High"
    );

    expect(screen.getByText(/COIL-001/i)).toBeInTheDocument();
    expect(screen.getByText(/High/i)).toBeInTheDocument();

    const materialChip = screen.getByRole("button", {
      name: /Material.*COIL-001/i
    });

    await user.click(materialChip);

    expect(screen.queryByText(/COIL-001/i)).not.toBeInTheDocument();
    expect(screen.getByText(/High/i)).toBeInTheDocument();
  });
});
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it } from "vitest";
import { SelectionBreadcrumb } from "../SelectionBreadcrumb";
import { DashboardFilterProvider } from "../../../state/DashboardFilterContext";
import {
  DashboardSelectionProvider,
  useDashboardSelections,
} from "../../../state/DashboardSelectionContext";
import { DashboardGridLayoutProvider } from "../../../state/DashboardGridLayoutContext";
import { StandardButton } from "@/components/standard";

// T-043 slice 1 proofs. The acceptance sentence being held here is the one in
// the frozen task: "Apply three selections and confirm three removable chips
// appear and that removing one updates every widget." The widget half is a
// browser row; what a component test can prove honestly is that ONE selection
// can be removed on its own, which is what the product could not do before.

const SEEDS = [
  { field: "materialCode", value: "M-1", label: "M-1", widget: "Material explorer" },
  { field: "riskClass", value: "High", label: "High", widget: "Risk distribution" },
  { field: "shiftCode", value: "A", label: "A", widget: "Shift breakdown" },
] as const;

function Seeder() {
  const { applySelection } = useDashboardSelections();
  return (
    <>
      {SEEDS.map((seed) => (
        <StandardButton
          key={seed.field}
          type="button"
          onClick={() =>
            applySelection({
              type: "generic",
              field: seed.field,
              value: seed.value,
              label: seed.label,
              sourceWidget: seed.widget,
            })
          }
        >
          {"seed " + seed.field}
        </StandardButton>
      ))}
    </>
  );
}

function renderBar() {
  return render(
    <MemoryRouter initialEntries={["/workspace/PRODUCTION_OVERVIEW"]}>
      <DashboardFilterProvider>
        <DashboardSelectionProvider>
          <DashboardGridLayoutProvider>
            <Seeder />
            <SelectionBreadcrumb />
          </DashboardGridLayoutProvider>
        </DashboardSelectionProvider>
      </DashboardFilterProvider>
    </MemoryRouter>
  );
}

async function seedAll(user: ReturnType<typeof userEvent.setup>) {
  for (const seed of SEEDS) {
    await user.click(screen.getByRole("button", { name: "seed " + seed.field }));
  }
}

describe("T-043 the permanent selections bar", () => {
  it("is present with no selection and reads the wording of Chapter 4 5.1.2", () => {
    renderBar();

    expect(screen.getByTestId("selections-bar")).toBeInTheDocument();
    expect(screen.getByTestId("selections-bar-state")).toHaveTextContent(
      "No selections applied"
    );
    expect(screen.queryAllByTestId("selection-chip")).toHaveLength(0);
  });

  it("renders one removable chip per selection", async () => {
    const user = userEvent.setup();
    renderBar();
    await seedAll(user);

    expect(screen.getAllByTestId("selection-chip")).toHaveLength(3);
    for (const seed of SEEDS) {
      expect(
        screen.getByRole("button", {
          name: "Remove selection " + seed.widget + ": " + seed.label,
        })
      ).toBeInTheDocument();
    }
  });

  it("removes exactly one selection, not the last one and not all of them", async () => {
    const user = userEvent.setup();
    renderBar();
    await seedAll(user);

    const first = SEEDS[0];
    await user.click(
      screen.getByRole("button", {
        name: "Remove selection " + first.widget + ": " + first.label,
      })
    );

    expect(screen.getAllByTestId("selection-chip")).toHaveLength(2);
    expect(
      screen.queryByRole("button", {
        name: "Remove selection " + first.widget + ": " + first.label,
      })
    ).toBeNull();
    for (const seed of SEEDS.slice(1)) {
      expect(
        screen.getByRole("button", {
          name: "Remove selection " + seed.widget + ": " + seed.label,
        })
      ).toBeInTheDocument();
    }
  });

  it("returns to the empty sentence when the last chip is removed", async () => {
    const user = userEvent.setup();
    renderBar();
    await user.click(screen.getByRole("button", { name: "seed materialCode" }));

    await user.click(
      screen.getByRole("button", {
        name: "Remove selection Material explorer: M-1",
      })
    );

    expect(screen.getByTestId("selections-bar-state")).toHaveTextContent(
      "No selections applied"
    );
  });
});
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { InteractiveWorkspacePage } from "../InteractiveWorkspacePage";

// T-043 slice 3. Only the active sheet's widgets render, and switching sheets
// leaves the selections region standing.
//
// The persistence hook is NOT mocked here. The whole point of option A is that
// sheets travel on the T-039 path, so this test feeds a real layout_json out of
// the dashboard API and lets useDashboardLayoutPersistence carry it. Mocking
// the hook would prove the page and leave the claim untested.

const LAYOUT_JSON = JSON.stringify({
  lg: [],
  sheets: [
    { id: "default", name: "Sheet 1" },
    { id: "quality", name: "Quality" },
  ],
  widgetSheets: { w2: "quality" },
});

vi.mock("@/authoring/SharedAuthoringShell", () => ({ default: () => null }));

vi.mock("@/api/dashboarding/dashboarding.api", () => ({
  dashboardingApi: {
    getDashboardDefinitions: () =>
      Promise.resolve([{ id: "dash-1", dashboardCode: "TEST_BOARD", name: "Test board" }]),
    getDashboardDefinition: () =>
      Promise.resolve({
        id: "dash-1",
        name: "Test board",
        description: "",
        layoutJson: LAYOUT_JSON,
        widgets: [
          { id: "w1", widgetTitle: "First widget" },
          { id: "w2", widgetTitle: "Second widget" },
        ],
      }),
    updateDashboardLayout: () => Promise.resolve(),
  },
}));

vi.mock("@/state/DashboardGridLayoutContext", () => ({
  useDashboardGridLayout: () => ({
    resetGridLayout: () => undefined,
    serializeLayouts: () => JSON.stringify({ lg: [] }),
    replaceLayoutsFromJson: () => undefined,
  }),
}));
vi.mock("@/state/DashboardSelectionContext", () => ({
  useDashboardSelections: () => ({ resetLayout: () => undefined }),
}));

vi.mock("@/components/dashboard/DashboardGridLayout", () => ({
  DashboardGridLayout: (props: { children?: unknown }) => <div>{props.children as never}</div>,
}));
vi.mock("@/components/dashboard/SavedDashboardWidget", () => ({
  SavedDashboardWidget: (props: { widget: { widgetTitle?: string } }) => (
    <div>{props.widget.widgetTitle}</div>
  ),
}));
vi.mock("@/components/DashboardFilterBar", () => ({ DashboardFilterBar: () => null }));
vi.mock("@/components/dashboard/AssociativePanel", () => ({ AssociativePanel: () => null }));
vi.mock("@/components/dashboard/DrilldownDrawer", () => ({ DrilldownDrawer: () => null }));
vi.mock("@/components/dashboard/SelectionBreadcrumb", () => ({
  SelectionBreadcrumb: () => <div data-testid="selections-bar">Selections</div>,
}));

async function openSheet(name: string): Promise<void> {
  const user = userEvent.setup();
  const selector = screen.getByTestId("workspace-sheet-selector");
  await user.click(within(selector).getByRole("button"));
  await user.click(screen.getByRole("option", { name }));
}

describe("T-043 sheets carried on the layout document", () => {
  it("renders only the widgets belonging to the active sheet", async () => {
    render(<InteractiveWorkspacePage dashboardCode="TEST_BOARD" />);

    expect(await screen.findByText("First widget")).toBeInTheDocument();
    expect(screen.queryByText("Second widget")).toBeNull();
  });

  it("switches the rendered widgets when the navigator changes sheet", async () => {
    render(<InteractiveWorkspacePage dashboardCode="TEST_BOARD" />);
    await screen.findByText("First widget");

    await openSheet("Quality");

    expect(await screen.findByText("Second widget")).toBeInTheDocument();
    expect(screen.queryByText("First widget")).toBeNull();
  });

  it("keeps the selections region standing across a sheet change", async () => {
    render(<InteractiveWorkspacePage dashboardCode="TEST_BOARD" />);
    await screen.findByText("First widget");
    expect(screen.getByTestId("selections-bar")).toBeInTheDocument();

    await openSheet("Quality");
    await screen.findByText("Second widget");

    expect(screen.getByTestId("selections-bar")).toBeInTheDocument();
  });

  it("offers both persisted sheets in the navigator", async () => {
    render(<InteractiveWorkspacePage dashboardCode="TEST_BOARD" />);
    await screen.findByText("First widget");

    const user = userEvent.setup();
    const selector = screen.getByTestId("workspace-sheet-selector");
    await user.click(within(selector).getByRole("button"));

    expect(screen.getByRole("option", { name: "Sheet 1" })).toBeInTheDocument();
    expect(screen.getByRole("option", { name: "Quality" })).toBeInTheDocument();
  });
});
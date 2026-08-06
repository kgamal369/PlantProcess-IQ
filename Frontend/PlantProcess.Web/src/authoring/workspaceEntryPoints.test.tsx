// PPIQ T-038 pack 03b. BOTH ENTRY POINTS, ONE SURFACE.
//
// His acceptance, at the entry points rather than in the model: Add Widget
// opens the shared shell in S2 mode, Edit Widget opens THE SAME component with
// the existing definition loaded, and no second door exists.
//
// The shell itself is mocked to a probe that renders its own props. What this
// file proves is the WIRING - which component is reached and what it is handed.
// The shell's behaviour is proved exhaustively in s2ShellSave.test.tsx, and
// proving it twice here would only make the suite slower and more brittle.
//
// It lives beside the authoring track rather than beside the page because the
// pack gate runs src/authoring, and a test the gate does not run is a test that
// silently stops being true.

import { readFileSync } from "node:fs";
import { join } from "node:path";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { InteractiveWorkspacePage } from "@/pages/Dashboard/InteractiveWorkspacePage";

const shellProps = vi.fn();

vi.mock("@/authoring/SharedAuthoringShell", () => ({
  default: (props: Record<string, unknown>) => {
    shellProps(props);
    return (
      <div data-testid="authoring-surface">
        <span data-testid="surface-purpose">{String(props.purpose)}</span>
        <span data-testid="surface-widget">
          {props.existingWidget ? String((props.existingWidget as { id?: string }).id) : "none"}
        </span>
      </div>
    );
  },
}));

vi.mock("@/api/dashboarding/dashboarding.api", () => ({
  dashboardingApi: {
    getDashboardDefinitions: () => Promise.resolve([{ id: "dash-1", dashboardCode: "TEST_BOARD", name: "Test board" }]),
    getDashboardDefinition: () => Promise.resolve({
      id: "dash-1", name: "Test board", description: "",
      widgets: [{ id: "widget-1", widgetTitle: "First widget" }],
    }),
  },
}));

vi.mock("@/hooks/useDashboardLayoutPersistence", () => ({
  useDashboardLayoutPersistence: () => ({
    reloadLayout: () => Promise.resolve(),
    saveLayout: () => Promise.resolve(),
    isSavingLayout: false,
  }),
}));

vi.mock("@/components/dashboard/DashboardGridLayout", () => ({
  DashboardGridLayout: (props: { children?: unknown }) => <div>{props.children as never}</div>,
}));
vi.mock("@/components/dashboard/SavedDashboardWidget", () => ({
  SavedDashboardWidget: (props: { onEdit: () => void }) => (
    <button type="button" onClick={props.onEdit}>Edit this widget</button>
  ),
}));
vi.mock("@/components/DashboardFilterBar", () => ({ DashboardFilterBar: () => null }));
vi.mock("@/components/dashboard/AssociativePanel", () => ({ AssociativePanel: () => null }));
vi.mock("@/components/dashboard/DrilldownDrawer", () => ({ DrilldownDrawer: () => null }));
vi.mock("@/components/dashboard/SelectionBreadcrumb", () => ({ SelectionBreadcrumb: () => null }));

const PAGE = "src/pages/Dashboard/InteractiveWorkspacePage.tsx";

describe("T-038 Add and Edit reach the one authoring surface", () => {
  it("Add Widget opens the shared shell in S2 with no widget loaded", async () => {
    render(<InteractiveWorkspacePage dashboardCode="TEST_BOARD" />);
    await userEvent.click(await screen.findByTestId("workspace-add-widget"));

    expect(await screen.findByTestId("authoring-surface")).toBeInTheDocument();
    expect(screen.getByTestId("surface-purpose")).toHaveTextContent("S2");
    expect(screen.getByTestId("surface-widget")).toHaveTextContent("none");
  });

  it("Edit Widget opens the SAME surface with the existing definition loaded", async () => {
    render(<InteractiveWorkspacePage dashboardCode="TEST_BOARD" />);
    await userEvent.click(await screen.findByRole("button", { name: "Edit this widget" }));

    expect(await screen.findByTestId("authoring-surface")).toBeInTheDocument();
    expect(screen.getByTestId("surface-purpose")).toHaveTextContent("S2");
    expect(screen.getByTestId("surface-widget")).toHaveTextContent("widget-1");
  });

  it("hands the shell the dashboard to save into, and something to do when it closes", async () => {
    shellProps.mockClear();
    render(<InteractiveWorkspacePage dashboardCode="TEST_BOARD" />);
    await userEvent.click(await screen.findByTestId("workspace-add-widget"));
    await waitFor(() => expect(shellProps).toHaveBeenCalled());

    const props = shellProps.mock.calls[shellProps.mock.calls.length - 1][0] as Record<string, unknown>;
    expect(props.dashboardDefinitionId).toBe("dash-1");
    expect(typeof props.onSaved).toBe("function");
    expect(typeof props.onClose).toBe("function");
  });

  it("opens exactly one authoring surface, never two", async () => {
    render(<InteractiveWorkspacePage dashboardCode="TEST_BOARD" />);
    await userEvent.click(await screen.findByTestId("workspace-add-widget"));
    await screen.findByTestId("authoring-surface");
    expect(screen.getAllByTestId("authoring-surface").length).toBe(1);
  });

  it("shows no authoring surface until an entry point is used", async () => {
    render(<InteractiveWorkspacePage dashboardCode="TEST_BOARD" />);
    await screen.findByTestId("workspace-add-widget");
    expect(screen.queryByTestId("authoring-surface")).toBeNull();
  });
});

describe("T-038 the page carries no trace of what was retired", () => {
  const source = readFileSync(join(process.cwd(), PAGE), "utf8");

  it("does not reference the retired widget authoring surface", () => {
    expect(source.indexOf("WidgetAuthoring" + "Panel")).toBe(-1);
  });

  it("uses the word the design uses for the open state", () => {
    // The old name was a leftover from a component that no longer exists, and
    // the design does not use that word anywhere.
    expect(source.indexOf("authoringOpen")).toBeGreaterThan(-1);
    expect(source.toLowerCase().indexOf("wiz" + "ard")).toBe(-1);
  });
});
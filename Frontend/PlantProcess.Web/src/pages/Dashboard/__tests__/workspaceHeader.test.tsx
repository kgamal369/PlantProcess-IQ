import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it, vi } from "vitest";
import { WorkspaceHeader, formatAsOf } from "../WorkspaceHeader";
import type { WorkspaceHeaderProps } from "../WorkspaceHeader";
import { DEFAULT_SHEET_ID, DEFAULT_SHEET_NAME } from "../workspaceSheets";
import type { WorkspaceSheet } from "../workspaceSheets";
import { SelectionBreadcrumb } from "@/components/dashboard/SelectionBreadcrumb";
import { DashboardFilterProvider } from "@/state/DashboardFilterContext";
import { DashboardSelectionProvider } from "@/state/DashboardSelectionContext";

// T-043 slice 2 proofs. The header is a pure-prop component precisely so these
// can run without the workspace's API clients and without the grid, which
// needs a ResizeObserver this environment does not provide.
//
// The props object is typed as WorkspaceHeaderProps and spread as itself. An
// earlier draft cast it through `never` to save declaring the type, which
// compiled in no version of TypeScript and was caught by tsc while every test
// here passed. A green suite is not a green build.

const ONE_SHEET: WorkspaceSheet[] = [{ id: DEFAULT_SHEET_ID, name: DEFAULT_SHEET_NAME }];
const TWO_SHEETS: WorkspaceSheet[] = [...ONE_SHEET, { id: "quality", name: "Quality" }];

function renderHeader(overrides: Partial<WorkspaceHeaderProps> = {}): void {
  const props: WorkspaceHeaderProps = {
    title: "Production overview",
    description: "Shift and grade performance",
    sheets: ONE_SHEET,
    activeSheetId: DEFAULT_SHEET_ID,
    onSheetChange: vi.fn(),
    asOfUtc: "2026-08-10T11:35:14.123Z",
    isEditing: false,
    onToggleEdit: vi.fn(),
    onSaveLayout: vi.fn(),
    isSavingLayout: false,
    onResetLayout: vi.fn(),
    onRefresh: vi.fn(),
    onAddWidget: vi.fn(),
    onCreateSheet: vi.fn(),
    ...overrides,
  };

  render(<WorkspaceHeader {...props} />);
}

describe("T-043 the page header of Chapter 4 5.1.2", () => {
  it("carries the sheet selector, the as-of and the edit toggle in presentation mode", () => {
    renderHeader();

    expect(screen.getByTestId("workspace-sheet-selector")).toBeInTheDocument();
    expect(screen.getByTestId("workspace-as-of")).toBeInTheDocument();
    expect(screen.getByTestId("workspace-edit-toggle")).toBeInTheDocument();
  });

  /**
   * DEMO-008. The opening state a customer sees is a reader's state. Save and
   * Reset rewrite the page being demonstrated, so they belong with the other
   * authoring affordances that 5.1.7 already hides until Edit layout is
   * pressed. Nothing is removed - the next test proves both return.
   */
  it("hides the layout authoring controls until edit mode is entered", () => {
    renderHeader();

    expect(screen.queryByTestId("workspace-save-layout")).not.toBeInTheDocument();
    expect(screen.queryByTestId("workspace-reset-layout")).not.toBeInTheDocument();
  });

  it("reveals both layout controls in edit mode", () => {
    renderHeader({ isEditing: true });

    expect(screen.getByTestId("workspace-save-layout")).toBeInTheDocument();
    expect(screen.getByTestId("workspace-reset-layout")).toBeInTheDocument();
  });

  it("keeps Refresh available in presentation mode, because reading is not authoring", () => {
    renderHeader();

    expect(screen.getByText("Refresh widgets")).toBeInTheDocument();
  });

  it("states the as-of in UTC to the minute and never in the reader's timezone", () => {
    renderHeader();

    expect(screen.getByTestId("workspace-as-of")).toHaveTextContent(
      "Data as of 2026-08-10 11:35 UTC"
    );
    expect(formatAsOf(null)).toBe("not read yet");
  });

  it("disables the sheet selector while the page has one sheet", () => {
    renderHeader();

    const selector = screen.getByTestId("workspace-sheet-selector");
    expect(selector).toHaveTextContent(DEFAULT_SHEET_NAME);
    expect(within(selector).getByRole("button")).toBeDisabled();
  });

  it("enables the sheet selector once a second sheet exists", () => {
    renderHeader({ sheets: TWO_SHEETS });

    const selector = screen.getByTestId("workspace-sheet-selector");
    expect(within(selector).getByRole("button")).toBeEnabled();
  });

  it("reports edit mode as a pressed state and reports the change", async () => {
    const user = userEvent.setup();
    const onToggleEdit = vi.fn();
    renderHeader({ onToggleEdit });

    const toggle = screen.getByTestId("workspace-edit-toggle");
    expect(toggle).toHaveAttribute("aria-pressed", "false");

    await user.click(toggle);
    expect(onToggleEdit).toHaveBeenCalledTimes(1);
  });

  it("shows edit mode as pressed when it is on", () => {
    renderHeader({ isEditing: true });

    expect(screen.getByTestId("workspace-edit-toggle")).toHaveAttribute(
      "aria-pressed",
      "true"
    );
  });

  it("does not offer Add widget in view mode", () => {
    renderHeader({ isEditing: false });

    expect(screen.queryByTestId("workspace-add-widget")).toBeNull();
  });

  it("offers Add widget in edit mode and reports the press", async () => {
    const user = userEvent.setup();
    const onAddWidget = vi.fn();
    renderHeader({ isEditing: true, onAddWidget });

    const add = screen.getByTestId("workspace-add-widget");
    expect(add).toBeInTheDocument();

    await user.click(add);
    expect(onAddWidget).toHaveBeenCalledTimes(1);
  });

  it("does not offer New sheet in view mode", () => {
    renderHeader({ sheets: TWO_SHEETS });

    expect(screen.queryByTestId("workspace-new-sheet")).toBeNull();
  });

  it("offers New sheet in edit mode and reports the press", async () => {
    const user = userEvent.setup();
    const onCreateSheet = vi.fn();
    renderHeader({ isEditing: true, onCreateSheet });

    await user.click(screen.getByTestId("workspace-new-sheet"));
    expect(onCreateSheet).toHaveBeenCalledTimes(1);
  });

  it("reports the chosen sheet when the navigator changes", async () => {
    const user = userEvent.setup();
    const onSheetChange = vi.fn();
    renderHeader({ sheets: TWO_SHEETS, onSheetChange });

    const selector = screen.getByTestId("workspace-sheet-selector");
    await user.click(within(selector).getByRole("button"));
    await user.click(screen.getByRole("option", { name: "Quality" }));

    expect(onSheetChange).toHaveBeenCalledWith("quality");
  });

  it("reports save and reset through their own handlers", async () => {
    const user = userEvent.setup();
    const onSaveLayout = vi.fn();
    const onResetLayout = vi.fn();
    renderHeader({ onSaveLayout, onResetLayout, isEditing: true });

    await user.click(screen.getByTestId("workspace-save-layout"));
    await user.click(screen.getByTestId("workspace-reset-layout"));

    expect(onSaveLayout).toHaveBeenCalledTimes(1);
    expect(onResetLayout).toHaveBeenCalledTimes(1);
  });
});

describe("T-043 the layout controls left the selections bar", () => {
  it("the selections bar carries no reset-layout control", () => {
    render(
      <MemoryRouter initialEntries={["/workspace/PRODUCTION_OVERVIEW"]}>
        <DashboardFilterProvider>
          <DashboardSelectionProvider>
            <SelectionBreadcrumb />
          </DashboardSelectionProvider>
        </DashboardFilterProvider>
      </MemoryRouter>
    );

    expect(screen.getByTestId("selections-bar")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /reset layout/i })).toBeNull();
    expect(screen.queryByRole("button", { name: /save layout/i })).toBeNull();
  });
});
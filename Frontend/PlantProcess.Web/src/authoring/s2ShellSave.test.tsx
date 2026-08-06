// PPIQ T-038 pack 03a. THE SHELL SAVES A WIDGET DEFINITION.
//
// This is where his Edit acceptance is proved at the surface rather than in the
// model: open the shell on a saved widget, change nothing, save, and what
// reaches the server is the same widget. Pack 01 proved the compile; this
// proves the shell actually compiles through it instead of building a payload
// of its own.
//
// The board is mocked because ReactFlow needs a layout engine jsdom does not
// have, and every api is mocked because nothing here may reach a server.

import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { SharedAuthoringShell } from "./SharedAuthoringShell";
import { readRoleBinding } from "@/api/product-core/widget-role-binding";
import type { WidgetDefinitionRecord } from "./widgetDefinitionModel";

const createWidget = vi.fn();
const updateWidget = vi.fn();

vi.mock("@/canvas/CanvasShell", () => ({
  CanvasShell: () => <div data-testid="authoring-board" />,
}));

vi.mock("@/api/canvasApi", () => ({
  listStagedDatasets: () => Promise.resolve([]),
  createSession: () => Promise.resolve({ sessionId: "s" }),
  saveGraph: () => Promise.resolve({ ok: true }),
  runDryRun: () => Promise.resolve({ dryRunId: "d", status: "succeeded", rowCount: 0, columns: [], rows: [] }),
  publishVersion: () => Promise.resolve({ versionId: "v", versionNumber: 1 }),
  runAuthoredSql: () => Promise.resolve({ status: "succeeded", rowCount: 0, columns: [], rows: [], message: "", errorCode: null, sql: null, appliedRowLimit: 100 }),
  saveSqlVersion: () => Promise.resolve({ saved: true, versionNumber: 1, id: "1", message: "saved", errorCode: null }),
}));

vi.mock("@/api/dashboarding/dashboarding.api", () => ({
  dashboardingApi: {
    getDashboardMetadata: () => Promise.resolve({
      chartTypes: [{ code: "ct_bar", label: "Bars", category: "chart", supportsDimension: true, supportsMeasure: true }],
      dimensions: [{ code: "dim_group", label: "Group", compatibleChartTypes: ["ct_bar"], requiresParameterCode: false }],
      measures: [{ code: "mea_total", label: "Total", compatibleChartTypes: ["ct_bar"], requiresParameterCode: false }],
      filters: [],
    }),
    getDashboardReferenceData: () => Promise.resolve({}),
    executeWidgetQueryExpression: () => Promise.resolve({ columns: [], rows: [], warnings: [] }),
    createDashboardWidgetDefinition: (id: string, payload: unknown) => createWidget(id, payload),
    updateDashboardWidgetDefinition: (id: string, widgetId: string, payload: unknown) => updateWidget(id, widgetId, payload),
  },
}));

const SAVED: WidgetDefinitionRecord = {
  id: "widget-1",
  widgetCode: "throughput_by_group_a1b2c",
  widgetTitle: "Throughput by group",
  widgetType: "chart",
  chartType: "ct_bar",
  dimensionCode: "dim_group",
  measureCode: "mea_total",
  parameterCode: null,
  filterJson: "{\"filter_window\":\"last_7_days\"}",
  layoutJson: "{\"x\":2,\"y\":3}",
  displayOptionsJson: "{\"legend\":\"right\",\"roleBinding\":{\"category\":\"group_code\",\"value\":\"measured_value\",\"secondary\":null}}",
  sortOrder: 4,
  queryExpression: "dimension group_code",
};

beforeEach(() => {
  createWidget.mockReset().mockResolvedValue({});
  updateWidget.mockReset().mockResolvedValue({});
});

describe("T-038 the S2 shell offers saving, not board actions", () => {
  it("shows Save widget where a preparation purpose shows Run and Publish", async () => {
    render(<SharedAuthoringShell purpose="S2" dashboardDefinitionId="dash-1" existingWidget={SAVED} />);
    await screen.findByTestId("s2-query-binding");
    expect(screen.getByRole("button", { name: "Save widget" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Run" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Publish version" })).toBeNull();
  });

  it("loads the saved widget's title into the one name control", async () => {
    render(<SharedAuthoringShell purpose="S2" dashboardDefinitionId="dash-1" existingWidget={SAVED} />);
    await screen.findByTestId("s2-query-binding");
    expect(screen.getByLabelText("Definition name")).toHaveValue("Throughput by group");
    expect(screen.getByTestId("authoring-validity")).toHaveTextContent("Ready to save");
  });

  it("refuses to save a widget with nothing in it, and says which thing is missing", async () => {
    render(<SharedAuthoringShell purpose="S2" dashboardDefinitionId="dash-1" />);
    await screen.findByTestId("s2-query-binding");
    expect(screen.getByTestId("authoring-validity")).toHaveTextContent("Invalid");
    expect(screen.getByRole("button", { name: "Save widget" })).toBeDisabled();
  });
});

describe("T-038 Edit saves the same widget back", () => {
  it("updates by id and sends a definition equal to the one it opened", async () => {
    const onSaved = vi.fn();
    const onClose = vi.fn();
    render(
      <SharedAuthoringShell
        purpose="S2" dashboardDefinitionId="dash-1" existingWidget={SAVED}
        onSaved={onSaved} onClose={onClose}
      />,
    );
    await screen.findByTestId("s2-query-binding");
    await userEvent.click(screen.getByRole("button", { name: "Save widget" }));

    await waitFor(() => expect(updateWidget).toHaveBeenCalledTimes(1));
    expect(createWidget).not.toHaveBeenCalled();
    const [dashboardId, widgetId, payload] = updateWidget.mock.calls[0] as [string, string, Record<string, unknown>];
    expect(dashboardId).toBe("dash-1");
    expect(widgetId).toBe("widget-1");
    expect(payload.widgetCode).toBe(SAVED.widgetCode);
    expect(payload.widgetTitle).toBe(SAVED.widgetTitle);
    expect(payload.chartType).toBe(SAVED.chartType);
    expect(payload.queryExpression).toBe(SAVED.queryExpression);
    expect(payload.layoutJson).toBe(SAVED.layoutJson);
    expect(payload.sortOrder).toBe(SAVED.sortOrder);
    expect(JSON.parse(String(payload.filterJson))).toEqual(JSON.parse(String(SAVED.filterJson)));
    // The role binding and the unrelated display option both survive the trip.
    const options = JSON.parse(String(payload.displayOptionsJson)) as Record<string, unknown>;
    expect(options.legend).toBe("right");
    expect(readRoleBinding(String(payload.displayOptionsJson)))
      .toEqual(readRoleBinding(SAVED.displayOptionsJson));
  });

  it("tells the caller it saved and then closes", async () => {
    const onSaved = vi.fn();
    const onClose = vi.fn();
    render(
      <SharedAuthoringShell
        purpose="S2" dashboardDefinitionId="dash-1" existingWidget={SAVED}
        onSaved={onSaved} onClose={onClose}
      />,
    );
    await screen.findByTestId("s2-query-binding");
    await userEvent.click(screen.getByRole("button", { name: "Save widget" }));
    await waitFor(() => expect(onSaved).toHaveBeenCalledTimes(1));
    expect(onClose).toHaveBeenCalledTimes(1);
  });
});

describe("T-038 Add creates a widget the dashboard did not have", () => {
  it("creates rather than updates when there is nothing to edit", async () => {
    render(<SharedAuthoringShell purpose="S2" dashboardDefinitionId="dash-1" />);
    await screen.findByTestId("s2-query-binding");
    await userEvent.type(screen.getByLabelText("Definition name"), "New widget");
    await userEvent.selectOptions(await screen.findByLabelText("Chart type"), "ct_bar");
    await userEvent.selectOptions(screen.getByLabelText("Measure"), "mea_total");
    await userEvent.click(screen.getByRole("button", { name: "Save widget" }));

    await waitFor(() => expect(createWidget).toHaveBeenCalledTimes(1));
    expect(updateWidget).not.toHaveBeenCalled();
    const [dashboardId, payload] = createWidget.mock.calls[0] as [string, Record<string, unknown>];
    expect(dashboardId).toBe("dash-1");
    expect(payload.widgetTitle).toBe("New widget");
    expect(payload.chartType).toBe("ct_bar");
    expect(String(payload.widgetCode).indexOf("new_widget_")).toBe(0);
    // A catalogue-bound widget carries no expression and no role mapping.
    expect(payload.queryExpression).toBeNull();
    expect(readRoleBinding(String(payload.displayOptionsJson))).toBeNull();
  });
});

describe("T-038 the save never lies about what went wrong", () => {
  it("shows no raw exception when the server refuses", async () => {
    updateWidget.mockRejectedValue(new Error("23505 duplicate key value violates unique constraint"));
    render(<SharedAuthoringShell purpose="S2" dashboardDefinitionId="dash-1" existingWidget={SAVED} />);
    await screen.findByTestId("s2-query-binding");
    await userEvent.click(screen.getByRole("button", { name: "Save widget" }));

    await waitFor(() => expect(updateWidget).toHaveBeenCalledTimes(1));
    const log = await screen.findByText(/did not complete/);
    expect(log).toBeInTheDocument();
    expect(screen.queryByText(/23505/)).toBeNull();
  });

  it("refuses without calling the server when it was opened with no dashboard", async () => {
    render(<SharedAuthoringShell purpose="S2" existingWidget={SAVED} />);
    await screen.findByTestId("s2-query-binding");
    await userEvent.click(screen.getByRole("button", { name: "Save widget" }));

    expect(await screen.findByText(/without a dashboard to save into/)).toBeInTheDocument();
    expect(createWidget).not.toHaveBeenCalled();
    expect(updateWidget).not.toHaveBeenCalled();
  });
});
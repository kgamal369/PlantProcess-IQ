// PPIQ T-042 S5 - THE LAYOUT ROUND TRIP, AND THE REFUSAL S4 BUILT.
//
// Two real widgets, arranged into visibly different geometry, saved, reloaded,
// and compared by NORMALISED VALUES - not by widget count. A count proves the
// page came back; only the values prove it came back as the author left it.
//
// The last two proofs exercise the failure path S4 installed and nothing else
// touches: a saved layout that cannot be read must REFUSE and name the widget
// and the field, leaving the page on screen exactly as it was. A load that
// silently repacks a customer's page is the defect this whole slice exists to
// prevent, and a passing gate over untested refusal code would have hidden it.

import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { describe, expect, it, vi, beforeEach } from "vitest";

const { pageApi, dashApi, shell } = vi.hoisted(() => ({
  pageApi: {
    listMine: vi.fn(),
    getBySlug: vi.fn(),
    create: vi.fn(),
    update: vi.fn(),
    delete: vi.fn(),
  },
  dashApi: {
    getDashboardMetadata: vi.fn(),
    getDashboardDefinitions: vi.fn(),
    getDashboardDefinition: vi.fn(),
    createDashboardDefinition: vi.fn(),
  },
  shell: { props: null as { dashboardDefinitionId?: string; onSaved?: () => void } | null },
}));

vi.mock("@/api/pageBuilder", () => ({ pageBuilderApi: pageApi }));
vi.mock("@/api/dashboarding/dashboarding.api", () => ({ dashboardingApi: dashApi }));
vi.mock("@/authoring/SharedAuthoringShell", () => ({
  SharedAuthoringShell: (props: { dashboardDefinitionId?: string; onSaved?: () => void }) => {
    shell.props = props;
    return (
      <div data-testid="shell-stub">
        <button type="button" onClick={() => props.onSaved?.()}>stub save</button>
      </div>
    );
  },
}));

import { PageBuilderPage } from "../PageBuilderPage.implementation";

const DASHBOARD_ID = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";

function page(overrides: Record<string, unknown> = {}) {
  return {
    id: "11111111-2222-3333-4444-555555555555",
    tenantId: "demo",
    slug: "shift-production",
    title: "Shift production",
    ownerUserName: "e2eadmin",
    visibility: "Shared",
    audienceRoles: ["Engineer"],
    version: 1,
    layoutJson: {},
    widgetBindingsJson: {},
    updatedAtUtc: new Date().toISOString(),
    backingDashboardDefinitionId: DASHBOARD_ID,
    publishedAtUtc: null,
    ...overrides,
  };
}

const TWO_PERSISTED_WIDGETS = [
  { id: "widget-one", widgetTitle: "Yield by grade", widgetKind: "chart", widgetCode: "YIELD" },
  { id: "widget-two", widgetTitle: "Downtime by line", widgetKind: "table", widgetCode: "DOWNTIME" },
];

async function pageWithTwoWidgets() {
  render(<PageBuilderPage />);
  await screen.findByTestId("widget-kind-picker");

  fireEvent.change(screen.getByLabelText("Title"), { target: { value: "Shift production" } });
  fireEvent.change(screen.getByLabelText("Slug"), { target: { value: "shift-production" } });
  fireEvent.click(screen.getByLabelText("Engineer"));

  fireEvent.click(screen.getByTestId("widget-kind-chart"));
  fireEvent.change(screen.getByLabelText("Widget name"), { target: { value: "Yield by grade" } });
  fireEvent.click(screen.getByTestId("ctl-open-authoring"));

  await waitFor(() => expect(screen.getByTestId("shell-stub")).toBeInTheDocument());
  fireEvent.click(screen.getByText("stub save"));

  await waitFor(() => expect(screen.getByText("Downtime by line")).toBeInTheDocument());
}

function widgetCard(id: string) {
  return document.querySelector('[data-widget-id="' + id + '"]') as HTMLElement;
}

function geometryOf(id: string): string {
  return (widgetCard(id).textContent ?? "").replace(/\s+/g, " ");
}

function savedWidgets(): Array<Record<string, unknown>> {
  const calls = [...pageApi.create.mock.calls, ...pageApi.update.mock.calls];
  const last = calls[calls.length - 1];
  const body = (last.length > 1 ? last[1] : last[0]) as Record<string, unknown>;
  const layout = body.layoutJson as Record<string, unknown>;

  return layout.widgets as Array<Record<string, unknown>>;
}

beforeEach(() => {
  shell.props = null;
  vi.clearAllMocks();

  dashApi.getDashboardMetadata.mockResolvedValue({
    widgetKinds: [
      { code: "chart", label: "Chart", usesChartType: true, usesQuery: true, description: "Plots a query." },
    ],
  });
  dashApi.getDashboardDefinitions.mockResolvedValue([]);
  dashApi.getDashboardDefinition.mockResolvedValue({ widgets: TWO_PERSISTED_WIDGETS });
  dashApi.createDashboardDefinition.mockResolvedValue({ id: DASHBOARD_ID });
  pageApi.create.mockResolvedValue(page());
  pageApi.update.mockResolvedValue(page());
  pageApi.getBySlug.mockResolvedValue(page());
});

describe("T-042 the layout round trip", () => {
  it("puts both persisted widgets on the grid under their server identities", async () => {
    await pageWithTwoWidgets();

    expect(widgetCard("widget-one")).toBeTruthy();
    expect(widgetCard("widget-two")).toBeTruthy();
  });

  it("saves the arranged geometry, not the geometry it started with", async () => {
    await pageWithTwoWidgets();

    const before = geometryOf("widget-two");

    fireEvent.click(within(widgetCard("widget-two")).getByText("Move right"));
    fireEvent.click(within(widgetCard("widget-two")).getByText("Resize wider"));

    expect(geometryOf("widget-two")).not.toBe(before);

    fireEvent.click(screen.getByTestId("ctl-save-page"));

    await waitFor(() => expect(savedWidgets()).toHaveLength(2));

    const arranged = savedWidgets().find((widget) => widget.id === "widget-two");
    const onScreen = geometryOf("widget-two");

    expect(onScreen).toContain("x:" + String(arranged?.x));
    expect(onScreen).toContain("y:" + String(arranged?.y));
    expect(onScreen).toContain("w:" + String(arranged?.w));
    expect(onScreen).toContain("h:" + String(arranged?.h));
  });

  it("comes back from a reload with the same normalised values, not merely the same count", async () => {
    await pageWithTwoWidgets();

    fireEvent.click(within(widgetCard("widget-two")).getByText("Move right"));
    fireEvent.click(within(widgetCard("widget-two")).getByText("Resize wider"));
    fireEvent.click(screen.getByTestId("ctl-save-page"));

    await waitFor(() => expect(savedWidgets()).toHaveLength(2));

    const persisted = savedWidgets();
    const geometryBefore = [geometryOf("widget-one"), geometryOf("widget-two")];

    // The server hands back exactly what was stored.
    pageApi.getBySlug.mockResolvedValue(page({ layoutJson: { grid: { columns: 12, rowHeight: 80 }, widgets: persisted } }));

    fireEvent.click(screen.getByText("Load by slug"));

    await waitFor(() => expect(screen.getByRole("status")).toHaveTextContent(/Loaded PageDefinition/));

    expect([geometryOf("widget-one"), geometryOf("widget-two")]).toEqual(geometryBefore);
  });

  it("refuses a layout whose geometry cannot be read, and names the widget and the field", async () => {
    await pageWithTwoWidgets();

    const geometryBefore = [geometryOf("widget-one"), geometryOf("widget-two")];

    pageApi.getBySlug.mockResolvedValue(page({
      layoutJson: {
        grid: { columns: 12, rowHeight: 80 },
        widgets: [{ id: "widget-one", kind: "chart", title: "Yield by grade", x: 0, w: 4, h: 3, source: "" }],
      },
    }));

    fireEvent.click(screen.getByText("Load by slug"));

    await waitFor(() => expect(screen.getByRole("status")).toHaveTextContent(/has no usable y/));

    // THE POINT OF THE WHOLE SLICE: the page on screen is untouched.
    expect([geometryOf("widget-one"), geometryOf("widget-two")]).toEqual(geometryBefore);
  });

  it("refuses a layout whose widget has no identity rather than inventing one", async () => {
    await pageWithTwoWidgets();

    pageApi.getBySlug.mockResolvedValue(page({
      layoutJson: {
        grid: { columns: 12, rowHeight: 80 },
        widgets: [{ kind: "chart", title: "Nameless", x: 0, y: 0, w: 4, h: 3, source: "" }],
      },
    }));

    fireEvent.click(screen.getByText("Load by slug"));

    await waitFor(() => expect(screen.getByRole("status")).toHaveTextContent(/has no usable id/));
    expect(widgetCard("widget-one")).toBeTruthy();
  });
});
// PPIQ T-042 S2 PROOFS - THE BACKING BRIDGE, INCLUDING ITS FAILURE PATHS.
//
// WHY THESE ARE NOT A BROWSER ROW. C and G require a PARTIAL FAILURE: the
// dashboard is created and the patch that stores its id then fails. A browser
// can only produce that by intercepting a route, which would make the proof
// rest on the interception rather than on the code - and the interception is
// the part most likely to be wrong. Here the failure is produced directly, and
// the assertion that matters is exact: createDashboardDefinition is called ONCE
// across both attempts.
//
// E and F end to end - a real widget authored, saved and read back - belong to
// the browser row in the lifecycle slice, where a real save actually happens.

import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { describe, expect, it, vi, beforeEach } from "vitest";

// vi.mock is HOISTED above everything else in the file, so a factory that closes
// over a plain const reads it before it exists. vi.hoisted is the declared way
// to give the factories something that is already there when they run.
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
// The page reads the current role to decide what Publish can truthfully say, so
// a bare render now needs the auth contract. Engineer, because that is the
// audience these pages are authored for.
vi.mock("@/state/AuthContext", () => ({ useAuth: () => ({ user: { role: "Engineer" } }) }));
vi.mock("@/api/dashboarding/dashboarding.api", () => ({ dashboardingApi: dashApi }));
vi.mock("@/authoring/SharedAuthoringShell", () => ({
  SharedAuthoringShell: (props: { dashboardDefinitionId?: string; onSaved?: () => void }) => {
    shell.props = props;
    return (
      <div data-testid="shell-stub" data-dashboard={props.dashboardDefinitionId ?? "none"}>
        <button type="button" onClick={() => props.onSaved?.()}>stub save</button>
      </div>
    );
  },
}));

import { PageBuilderPage } from "../PageBuilderPage.implementation";

const PAGE_ID = "11111111-2222-3333-4444-555555555555";
const RECOVERY_CODE = "PAGE_111111112222";
const DASHBOARD_ID = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";

function page(overrides: Record<string, unknown> = {}) {
  return {
    id: PAGE_ID,
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
    backingDashboardDefinitionId: null,
    publishedAtUtc: null,
    ...overrides,
  };
}

async function authorAWidget() {
  render(<PageBuilderPage />);
  await screen.findByTestId("widget-kind-picker");

  fireEvent.change(screen.getByLabelText("Title"), { target: { value: "Shift production" } });
  fireEvent.change(screen.getByLabelText("Slug"), { target: { value: "shift-production" } });
  fireEvent.click(screen.getByLabelText("Engineer"));

  fireEvent.click(screen.getByTestId("widget-kind-chart"));
  fireEvent.change(screen.getByLabelText("Widget name"), { target: { value: "Yield by grade" } });
  fireEvent.click(screen.getByTestId("ctl-open-authoring"));
}

beforeEach(() => {
  shell.props = null;
  vi.clearAllMocks();

  dashApi.getDashboardMetadata.mockResolvedValue({
    widgetKinds: [
      { code: "chart", label: "Chart", usesChartType: true, usesQuery: true, description: "Plots a query." },
      { code: "table", label: "Table", usesChartType: false, usesQuery: true, description: "Shows rows." },
    ],
  });
  dashApi.getDashboardDefinitions.mockResolvedValue([]);
  dashApi.getDashboardDefinition.mockResolvedValue({ widgets: [] });
  dashApi.createDashboardDefinition.mockResolvedValue({ id: DASHBOARD_ID });
  pageApi.create.mockResolvedValue(page());
  pageApi.update.mockResolvedValue(page({ backingDashboardDefinitionId: DASHBOARD_ID }));
  pageApi.getBySlug.mockResolvedValue(page({ backingDashboardDefinitionId: DASHBOARD_ID }));
});

describe("T-042 the backing bridge", () => {
  it("A creates exactly one backing workspace for a page that has none", async () => {
    await authorAWidget();

    await waitFor(() => expect(dashApi.createDashboardDefinition).toHaveBeenCalledTimes(1));

    const payload = dashApi.createDashboardDefinition.mock.calls[0][0] as Record<string, unknown>;
    expect(payload.dashboardCode).toBe(RECOVERY_CODE);
    // It must never be mistaken for a seeded showcase workspace.
    expect(payload.isSystemTemplate).toBe(false);
    expect(payload.isSynthetic).toBe(false);
  });

  it("B stores that workspace id on the page and confirms it by re-reading", async () => {
    await authorAWidget();

    await waitFor(() => expect(pageApi.getBySlug).toHaveBeenCalled());

    const patched = pageApi.update.mock.calls.map((call) => call[1] as Record<string, unknown>);
    expect(patched.some((body) => body.backingDashboardDefinitionId === DASHBOARD_ID)).toBe(true);
  });

  it("C reuses an existing backing workspace and creates no second one", async () => {
    pageApi.create.mockResolvedValue(page({ backingDashboardDefinitionId: DASHBOARD_ID }));

    await authorAWidget();

    await waitFor(() => expect(screen.getByTestId("shell-stub")).toBeInTheDocument());
    expect(dashApi.createDashboardDefinition).not.toHaveBeenCalled();
  });

  it("C after a patch fails, the retry recovers the same workspace and creates no second one", async () => {
    // First attempt: the dashboard is created, the patch that stores it fails.
    pageApi.update.mockRejectedValueOnce(new Error("network"));

    await authorAWidget();
    await waitFor(() => expect(screen.getByTestId("bridge-failed")).toBeInTheDocument());
    expect(dashApi.createDashboardDefinition).toHaveBeenCalledTimes(1);
    expect(screen.queryByTestId("shell-stub")).toBeNull();

    // The retry sees a page whose link is STILL null, and must find the
    // dashboard by its recovery code rather than create another.
    dashApi.getDashboardDefinitions.mockResolvedValue([
      { id: DASHBOARD_ID, dashboardCode: RECOVERY_CODE, name: "Shift production" },
    ]);

    fireEvent.click(screen.getByTestId("widget-kind-chart"));
    fireEvent.change(screen.getByLabelText("Widget name"), { target: { value: "Yield by grade" } });
    fireEvent.click(screen.getByTestId("ctl-open-authoring"));

    await waitFor(() => expect(screen.getByTestId("shell-stub")).toBeInTheDocument());
    expect(dashApi.createDashboardDefinition).toHaveBeenCalledTimes(1);
  });

  it("D hands the shell that exact workspace id and nothing invented", async () => {
    await authorAWidget();

    await waitFor(() => expect(screen.getByTestId("shell-stub")).toBeInTheDocument());
    expect(screen.getByTestId("shell-stub").getAttribute("data-dashboard")).toBe(DASHBOARD_ID);
    expect(shell.props?.dashboardDefinitionId).toBe(DASHBOARD_ID);
  });

  it("G a failed workspace creation does not open the shell and places no widget", async () => {
    dashApi.createDashboardDefinition.mockRejectedValue(new Error("refused"));

    await authorAWidget();

    await waitFor(() => expect(screen.getByTestId("bridge-failed")).toBeInTheDocument());
    expect(screen.queryByTestId("shell-stub")).toBeNull();
    expect(screen.getByTestId("page-empty")).toBeInTheDocument();
  });

  it("G a link the page did not keep does not open the shell", async () => {
    // The patch reports success and the page comes back without the link.
    pageApi.getBySlug.mockResolvedValue(page({ backingDashboardDefinitionId: null }));

    await authorAWidget();

    await waitFor(() => expect(screen.getByTestId("bridge-failed")).toBeInTheDocument());
    expect(screen.queryByTestId("shell-stub")).toBeNull();
  });

  it("F the grid is rebuilt from the server list, not from what the client hoped", async () => {
    dashApi.getDashboardDefinition.mockResolvedValue({
      widgets: [
        { id: "widget-persisted-1", widgetTitle: "Yield by grade", widgetKind: "chart", widgetCode: "YIELD_BY_GRADE" },
      ],
    });

    await authorAWidget();
    await waitFor(() => expect(screen.getByTestId("shell-stub")).toBeInTheDocument());

    fireEvent.click(screen.getByText("stub save"));

    await waitFor(() => expect(screen.queryByTestId("page-empty")).toBeNull());
    expect(await screen.findByText("Yield by grade")).toBeInTheDocument();
    expect(dashApi.getDashboardDefinition).toHaveBeenCalledWith(DASHBOARD_ID);
  });
});
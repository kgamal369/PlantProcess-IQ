// PPIQ T-040 03a1. THE CENTRE REGION KEEPS ITS SHAPE.
//
// The four regions of section 5.2.3 are a contract, and this step adds a
// container INSIDE one of them. What has to stay true is that there are still
// four, that the centre still holds whichever face is active, and that the
// face is inside the body rather than beside it - because a face that escaped
// the body would take the grid cell back and the banner would become a fifth
// region by accident.
//
// The mocked catalogue returns nothing, so the tree renders its empty message.
// Waiting for THAT is what proves the catalogue effect has settled - an earlier
// version waited for a dataset name the mock never supplied, and failed four
// tests over a wrapper that was already correct.

import { render, screen, within } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { SharedAuthoringShell } from "./SharedAuthoringShell";

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
    getDashboardMetadata: () => Promise.resolve({ chartTypes: [], dimensions: [], measures: [], filters: [] }),
    getDashboardReferenceData: () => Promise.resolve({}),
    executeWidgetQueryExpression: () => Promise.resolve({ columns: [], rows: [], warnings: [] }),
    createDashboardWidgetDefinition: () => Promise.resolve({}),
    updateDashboardWidgetDefinition: () => Promise.resolve({}),
  },
}));

describe("T-040 the centre region gains a container, not a region", () => {
  it("still renders exactly one centre region", async () => {
    render(<SharedAuthoringShell purpose="S1" />);
    await screen.findByText(/No staged datasets/);
    expect(screen.getAllByTestId("authoring-centre-region").length).toBe(1);
    expect(screen.getAllByTestId("authoring-centre-body").length).toBe(1);
  });

  it("keeps the board INSIDE the centre body, not beside it", async () => {
    render(<SharedAuthoringShell purpose="S1" />);
    await screen.findByText(/No staged datasets/);
    const body = screen.getByTestId("authoring-centre-body");
    expect(within(body).getByTestId("authoring-board")).toBeInTheDocument();
  });

  it("keeps the S2 face inside the same body, so all faces share one centre", async () => {
    render(<SharedAuthoringShell purpose="S2" />);
    const body = await screen.findByTestId("authoring-centre-body");
    expect(await within(body).findByTestId("s2-query-binding")).toBeInTheDocument();
    expect(screen.queryByTestId("authoring-board")).toBeNull();
  });

  it("leaves the other three regions exactly where they were", async () => {
    render(<SharedAuthoringShell purpose="S1" />);
    await screen.findByText(/No staged datasets/);
    expect(screen.getByTestId("authoring-mode-bar")).toBeInTheDocument();
    expect(screen.getByTestId("canvas-schema-tree")).toBeInTheDocument();
    expect(screen.getByTestId("authoring-toolbox-region")).toBeInTheDocument();
  });

  it("mounts the banner point above the body, once, and in the centre", async () => {
    render(<SharedAuthoringShell purpose="S1" />);
    await screen.findByText(/No staged datasets/);
    const region = screen.getByTestId("authoring-centre-region");
    const mount = screen.getByTestId("authoring-centre-banner");
    expect(within(region).getByTestId("authoring-centre-banner")).toBe(mount);
    // Above the body in document order, which is what the column layout means.
    expect(mount.compareDocumentPosition(screen.getByTestId("authoring-centre-body")))
      .toBe(Node.DOCUMENT_POSITION_FOLLOWING);
  });
});
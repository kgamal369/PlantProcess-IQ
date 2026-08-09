// PPIQ T-042 S6 - THE WORKSPACE PROJECTION.
//
// Seven classification cases and one invalidation case. The projection decides
// what appears in a customer's navigation, so its failures are not cosmetic:
// showing a draft is a leak, hiding a seeded workspace is a regression, and
// showing a deleted page is the resurrection defect this design was corrected
// to prevent.
//
// The fail-closed proof asserts the PREVIOUS links survive, not merely that the
// result is non-empty. "The authority became unavailable" must never turn into
// "assume no PageBuilder ownership exists".

import { renderHook, waitFor } from "@testing-library/react";
import { describe, expect, it, vi, beforeEach } from "vitest";

const { api, pageApi } = vi.hoisted(() => ({
  api: { get: vi.fn() },
  pageApi: { listMine: vi.fn() },
}));

vi.mock("@/api/http/apiClient", () => ({ apiClient: api, ApiError: class ApiError extends Error {} }));
vi.mock("@/api/pageBuilder", () => ({ pageBuilderApi: pageApi }));
vi.mock("../../state/AuthContext", () => ({ useAuth: () => ({ user: { role: "Engineer" } }) }));

import { useWorkspaceLinks } from "../AppLayout";
import { notifyWorkspaceLinksChanged } from "../../state/workspaceLinksSignal";

const SEEDED = { id: "d-seeded", dashboardCode: "PRODUCTION_OVERVIEW", name: "Production Overview" };
const BACKED = { id: "d-backed", dashboardCode: "PAGE_ABC123", name: "PAGE_ABC123" };

function authoredPage(overrides: Record<string, unknown> = {}) {
  return {
    id: "p-1",
    slug: "shift-production",
    title: "Shift production",
    audienceRoles: ["Engineer"],
    backingDashboardDefinitionId: "d-backed",
    publishedAtUtc: "2026-08-08T20:00:00Z",
    isDeleted: false,
    ...overrides,
  };
}

function labels(result: { current: Array<{ label: string; to: string }> }) {
  return result.current.map((entry) => entry.label);
}

beforeEach(() => {
  vi.clearAllMocks();
  api.get.mockResolvedValue([SEEDED, BACKED]);
  pageApi.listMine.mockResolvedValue([]);
});

describe("T-042 the workspace projection", () => {
  it("1 preserves a seeded dashboard that no page ever claimed", async () => {
    const { result } = renderHook(() => useWorkspaceLinks());

    await waitFor(() => expect(labels(result)).toContain("Production Overview"));
  });

  it("2 hides a backed dashboard whose page is still a draft", async () => {
    pageApi.listMine.mockResolvedValue([authoredPage({ publishedAtUtc: null })]);

    const { result } = renderHook(() => useWorkspaceLinks());

    await waitFor(() => expect(labels(result)).toContain("Production Overview"));
    expect(labels(result)).not.toContain("PAGE_ABC123");
    expect(labels(result)).not.toContain("Shift production");
  });

  it("3 shows a published page once, under the title its author gave it", async () => {
    pageApi.listMine.mockResolvedValue([authoredPage()]);

    const { result } = renderHook(() => useWorkspaceLinks());

    await waitFor(() => expect(labels(result)).toContain("Shift production"));
    expect(labels(result).filter((label) => label === "Shift production")).toHaveLength(1);
    expect(labels(result)).not.toContain("PAGE_ABC123");
    expect(result.current.find((entry) => entry.label === "Shift production")?.to)
      .toBe("/workspace/PAGE_ABC123");
  });

  it("4 hides a published page whose audience does not contain this exact role", async () => {
    pageApi.listMine.mockResolvedValue([authoredPage({ audienceRoles: ["Admin"] })]);

    const { result } = renderHook(() => useWorkspaceLinks());

    await waitFor(() => expect(labels(result)).toContain("Production Overview"));
    expect(labels(result)).not.toContain("Shift production");
  });

  it("5 shows a published page whose audience contains this role", async () => {
    pageApi.listMine.mockResolvedValue([authoredPage({ audienceRoles: ["Viewer", "Engineer"] })]);

    const { result } = renderHook(() => useWorkspaceLinks());

    await waitFor(() => expect(labels(result)).toContain("Shift production"));
  });

  it("6 keeps a deleted page's dashboard hidden - it must not resurrect as seeded", async () => {
    // Published, then deleted. The dashboard is still there and the page still
    // owns it; without the deleted row in the projection this would come back
    // as a seeded workspace under its dashboard code.
    pageApi.listMine.mockResolvedValue([authoredPage({ isDeleted: true })]);

    const { result } = renderHook(() => useWorkspaceLinks());

    await waitFor(() => expect(labels(result)).toContain("Production Overview"));
    expect(labels(result)).not.toContain("Shift production");
    expect(labels(result)).not.toContain("PAGE_ABC123");
  });

  it("7 fails closed: a page-fetch failure leaves the previous links exactly as they were", async () => {
    pageApi.listMine.mockResolvedValue([authoredPage()]);

    const { result } = renderHook(() => useWorkspaceLinks());

    await waitFor(() => expect(labels(result)).toContain("Shift production"));

    const knownGood = labels(result);

    // The page authority goes away. It must NOT be read as "no PageBuilder
    // pages exist", which would expose every backed dashboard.
    pageApi.listMine.mockRejectedValue(new Error("page authority unavailable"));
    notifyWorkspaceLinksChanged();

    await new Promise((resolve) => setTimeout(resolve, 50));

    expect(labels(result)).toEqual(knownGood);
    expect(labels(result)).not.toContain("PAGE_ABC123");
  });

  it("8 refreshes navigation when the shared signal fires, with no reload", async () => {
    pageApi.listMine.mockResolvedValue([authoredPage({ publishedAtUtc: null })]);

    const { result } = renderHook(() => useWorkspaceLinks());

    await waitFor(() => expect(labels(result)).toContain("Production Overview"));
    expect(labels(result)).not.toContain("Shift production");

    const before = pageApi.listMine.mock.calls.length;

    // The server state changes - this is what Publish leaves behind.
    pageApi.listMine.mockResolvedValue([authoredPage()]);
    notifyWorkspaceLinksChanged();

    await waitFor(() => expect(labels(result)).toContain("Shift production"));
    expect(pageApi.listMine.mock.calls.length).toBeGreaterThan(before);
  });
});
// @vitest-environment node
import { existsSync, readFileSync } from "node:fs";
import { resolve } from "node:path";
import { describe, expect, it } from "vitest";

import { buildAssistantContext } from "../assistantPageContext";

/* PPIQ-T072 frontend half.
 *
 * The envelope must describe the surface without inventing anything, and the
 * wiring that carries it must stay in place. Both are asserted here.
 */

const webRoot = resolve(__dirname, "..", "..", "..", "..");
const read = (rel: string) => (existsSync(resolve(webRoot, rel)) ? readFileSync(resolve(webRoot, rel), "utf8") : "");

describe("T-072 the envelope describes the surface", () => {
  it("takes the page code from the first segment, not a row identifier", () => {
    const detail = buildAssistantContext({ pathname: "/materials/8f14e45f-ceea-467a-9f6a-1b2c3d4e5f60" });
    expect(detail.pageCode).toBe("materials");
    expect(detail.route).toBe("/materials/8f14e45f-ceea-467a-9f6a-1b2c3d4e5f60");
  });

  it("carries selections and names the widget the last one came from", () => {
    const context = buildAssistantContext({
      pathname: "/dashboard",
      selections: [
        { field: "siteId", value: "SITE_ALPHA", sourceWidget: "WIDGET_ALPHA" },
        { field: "equipmentId", value: "EQUIP_BETA", sourceWidget: "WIDGET_BETA" },
      ],
    });

    expect(context.selections).toEqual(["siteId:SITE_ALPHA", "equipmentId:EQUIP_BETA"]);
    expect(context.widgetCode).toBe("WIDGET_BETA");
  });

  it("drops pagination and ordering keys and keeps the real filters", () => {
    const context = buildAssistantContext({
      pathname: "/dashboard",
      filters: { siteId: "SITE_ALPHA", page: 2, pageSize: 50, sortBy: "createdAt", areaId: "" },
    });

    expect(context.filters).toEqual(["siteId:SITE_ALPHA"]);
  });

  it("never invents a result summary or an evidence handle", () => {
    const context = buildAssistantContext({ pathname: "/dashboard" });
    expect(context.lastResultSummary).toBeNull();
    expect(context.evidenceHandles).toBeNull();
  });

  it("survives an empty surface", () => {
    const context = buildAssistantContext({ pathname: "" });
    expect(context.route).toBeNull();
    expect(context.pageCode).toBeNull();
    expect(context.selections).toEqual([]);
    expect(context.filters).toEqual([]);
  });
});

describe("T-072 the wiring that carries the envelope", () => {
  it("the dashboard providers wrap the authenticated layout", () => {
    const app = read("src/App.tsx");
    const filterProvider = app.indexOf("<DashboardFilterProvider>");
    const selectionProvider = app.indexOf("<DashboardSelectionProvider>");
    const layout = app.indexOf("element={<AppLayout />}");

    expect(filterProvider).toBeGreaterThan(-1);
    expect(selectionProvider).toBeGreaterThan(-1);
    expect(layout).toBeGreaterThan(-1);
    /* The dock reads those providers, so the layout must stay inside them. */
    expect(filterProvider).toBeLessThan(layout);
    expect(selectionProvider).toBeLessThan(layout);
  });

  it("the dock passes the surface into ask and the provider forwards it", () => {
    const dock = read("src/components/assistant/AssistantDock.tsx");
    expect(dock).toContain("useAssistantPageContext()");
    expect(dock).toContain("ask(question, pageContext)");

    const provider = read("src/components/assistant/AssistantDockContext.tsx");
    expect(provider).toContain("context?: AssistantContextPayload | null");
    expect(provider).toContain("context ?? null,");
  });

  it("the provider still holds no dashboard hook, so it mounts anywhere", () => {
    const provider = read("src/components/assistant/AssistantDockContext.tsx");
    expect(provider).not.toContain("useDashboardFilters");
    expect(provider).not.toContain("useDashboardSelection");
  });

  it("the request body carries the envelope", () => {
    const client = read("src/api/assistantApi.ts");
    expect(client).toContain("export type AssistantContextPayload");
    expect(client).toContain("context: context ?? null,");
  });
});
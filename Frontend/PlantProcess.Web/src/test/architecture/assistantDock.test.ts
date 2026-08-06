// @vitest-environment node
import { existsSync, readFileSync } from "node:fs";
import { resolve } from "node:path";
import { describe, expect, it } from "vitest";

/* PPIQ-T071 structural guards.
 *
 * The dock is a LAYOUT concern, not a route, and the provider is the single
 * owner of the api call. These assert that arrangement in the source, so a
 * later refactor cannot quietly put the conversation back inside a page.
 */

const webRoot = resolve(__dirname, "../..", "..");
const read = (rel: string) => readFileSync(resolve(webRoot, rel), "utf8");

describe("T-071 assistant dock architecture", () => {
  it("the provider exists and owns the single askAssistant call", () => {
    const ctx = read("src/components/assistant/AssistantDockContext.tsx");
    expect(ctx).toContain("assistantApi.askAssistant(");
    expect(ctx).toContain("AssistantDockProvider");
    expect(ctx).toContain("useAssistantDock");
  });

  it("the page no longer calls the assistant api directly", () => {
    const page = read("src/pages/Phase8/AssistantRuntimePage.tsx");
    expect(page).not.toContain("assistantApi.askAssistant(");
    expect(page).toContain("useAssistantDock(");
    expect(page).toContain("<AssistantChat");
  });

  it("the dock does not call the assistant api directly", () => {
    const dock = read("src/components/assistant/AssistantDock.tsx");
    expect(dock).not.toContain("assistantApi.askAssistant(");
    expect(dock).toContain("useAssistantDock(");
  });

  it("the dock is mounted by the layout and is not a route element", () => {
    const layout = read("src/components/AppLayout.tsx");
    expect(layout).toContain("<AssistantDockProvider>");
    expect(layout).toContain("<AssistantDock />");
    /* Mounted as a sibling of the outlet, inside the provider. */
    expect(layout.indexOf("<AssistantDockProvider>")).toBeLessThan(layout.indexOf("<Outlet />"));
    expect(layout.indexOf("<Outlet />")).toBeLessThan(layout.indexOf("<AssistantDock />"));

    const app = existsSync(resolve(webRoot, "src/App.tsx")) ? read("src/App.tsx") : "";
    expect(app).not.toContain("element={<AssistantDock");
  });

  it("the full-page assistant suppresses the global dock", () => {
    const dock = read("src/components/assistant/AssistantDock.tsx");
    expect(dock).toContain('location.pathname.startsWith("/assistant")');
    expect(dock).toContain("return null;");
  });

  it("no browser storage is used for the conversation", () => {
    for (const rel of [
      "src/components/assistant/AssistantDockContext.tsx",
      "src/components/assistant/AssistantDock.tsx",
      "src/components/assistant/AssistantChat.tsx",
      "src/pages/Phase8/AssistantRuntimePage.tsx",
    ]) {
      const source = read(rel);
      expect(source, rel).not.toContain("localStorage");
      expect(source, rel).not.toContain("sessionStorage");
    }
  });

  it("no test-only mutation hatch was added to production code", () => {
    const ctx = read("src/components/assistant/AssistantDockContext.tsx");
    expect(ctx).not.toContain("setTurns:");
    expect(ctx).not.toContain("__test");
  });
});
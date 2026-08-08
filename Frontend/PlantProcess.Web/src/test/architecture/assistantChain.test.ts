// @vitest-environment node
// ============================================================
// M1-11 gate: there is exactly ONE assistant, and it goes through the api client.
// ============================================================
import { describe, expect, it } from "vitest";
import { readdirSync, readFileSync, statSync, existsSync } from "node:fs";
import { resolve, join } from "node:path";

const webRoot = resolve(__dirname, "../../..");
const read = (rel: string): string => {
  const p = resolve(webRoot, rel);
  return existsSync(p) ? readFileSync(p, "utf8") : "";
};

function walk(dir: string, out: string[] = []): string[] {
  for (const entry of readdirSync(dir)) {
    const full = join(dir, entry);
    if (/node_modules|_phase9_standardbutton_dedupe_backup/.test(full)) continue;
    if (statSync(full).isDirectory()) walk(full, out);
    else if (/\.tsx?$/.test(entry)) out.push(full);
  }
  return out;
}

describe("M1-11: one assistant, wired through the api client", () => {
  it("the orphaned GroundedAssistantPage is gone", () => {
    expect(existsSync(resolve(webRoot, "src/pages/Assistant/GroundedAssistantPage.tsx"))).toBe(false);
  });

  /* PPIQ-T071 G1: the assistant is a shell component, not a page. The rule this
     assertion protects is unchanged - there is exactly ONE surface mounting the
     chat - but that surface is now the dock, and the standalone runtime page it
     used to name has been retired. A test that pins a retired architecture is
     worse than no test. */
  it("the dock mounts AssistantChat and no standalone runtime page remains", () => {
    const dock = read("src/components/assistant/AssistantDock.tsx");
    expect(dock.length).toBeGreaterThan(0);
    expect(dock).toContain("<AssistantChat");
    expect(existsSync(resolve(webRoot, "src/pages/Phase8/AssistantRuntimePage.tsx"))).toBe(false);
  });

  /* PPIQ-T071: the single askAssistant call MOVED from the page to the dock
     provider, because the conversation now lives above the router outlet so it
     survives navigation. The assertion moves with the responsibility - the rule
     it protects is unchanged: every assistant request goes through the existing
     api client, from exactly one place. */
  it("the dock provider owns the single askAssistant call", () => {
    const ctx = read("src/components/assistant/AssistantDockContext.tsx");
    expect(ctx).toContain("assistantApi.askAssistant(");
    const dock = read("src/components/assistant/AssistantDock.tsx");
    expect(dock).not.toContain("assistantApi.askAssistant(");
  });

  it("AssistantChat does not redeclare the wire types", () => {
    const chat = read("src/components/assistant/AssistantChat.tsx");
    expect(chat).toContain('from "@/api/assistantApi"');
    expect(chat).not.toContain("export type AssistantAnswer");
  });

  it("nothing calls the ask endpoint outside the api client", () => {
    // Built from fragments so this assertion does not match ITSELF: walk() scans
    // all of src/, including this file. (Guards that match their own text have
    // reverted four correct packs today.)
    const endpoint = "/api/" + "assistant/ask";
    const offenders: string[] = [];
    for (const file of walk(resolve(webRoot, "src"))) {
      if (file.endsWith("assistantApi.ts")) continue;
      if (/[\\/]test[\\/]/.test(file)) continue; // the architecture suite itself
      const src = readFileSync(file, "utf8");
      if (src.includes(endpoint)) offenders.push(file.replace(webRoot, ""));
    }
    expect(offenders, `raw assistant fetches:\n  ${offenders.join("\n  ")}`).toHaveLength(0);
  });

  it("the role map points at the canonical assistant route", () => {
    const roles = read("src/security/roleAccess.ts");
    expect(roles).toContain('"/assistant": "AssistantChat"');
    expect(roles).not.toContain('"/phase8/assistant": "AssistantChat"');
  });
});
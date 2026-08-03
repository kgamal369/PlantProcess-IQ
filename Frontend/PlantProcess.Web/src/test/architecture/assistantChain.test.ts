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

  it("the live assistant page mounts AssistantChat", () => {
    const page = read("src/pages/Phase8/AssistantRuntimePage.tsx");
    expect(page.length).toBeGreaterThan(0);
    expect(page).toContain("<AssistantChat");
    expect(page).toContain("assistantApi.askAssistant(");
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
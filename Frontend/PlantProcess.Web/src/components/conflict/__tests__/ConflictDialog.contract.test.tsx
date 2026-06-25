import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

// P4-T04: the conflict dialog must show the other editor's identity, offer reload,
// and gate overwrite behind an explicit confirm (no silent last-write-wins).
function readDialogSource() {
  return fs.readFileSync(
    path.join(process.cwd(), "src", "components", "conflict", "ConflictDialog.tsx"),
    "utf8",
  );
}

describe("ConflictDialog contract (P4-T04)", () => {
  it("shows the other editor and current version", () => {
    const src = readDialogSource();
    expect(src).toContain("conflict-editor");
    expect(src).toMatch(/editor/);
    expect(src).toMatch(/currentVersion/);
  });

  it("gates overwrite behind an explicit confirm", () => {
    const src = readDialogSource();
    expect(src).toContain("conflict-overwrite-confirm");
    // Overwrite stays gated until the confirm box is ticked. Track the behaviour
    // (disabled bound to !confirmOverwrite) rather than the exact prop spelling,
    // and require the honest disabled-reason per the action-button standard.
    expect(src).toMatch(/(?:is)?[Dd]isabled=\{!confirmOverwrite\}/);
    expect(src).toMatch(/data-disabled-reason/);
  });

  it("offers a reload action", () => {
    expect(readDialogSource()).toContain("conflict-reload");
  });
});
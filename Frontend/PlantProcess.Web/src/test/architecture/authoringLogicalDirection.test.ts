// @vitest-environment node
// PPIQ T-040 G09. LOGICAL DIRECTION IN THE CERTIFIED AUTHORING STYLESHEETS.
//
// The Golden Gate line G09 reads: inline-start and inline-end only, never left
// or right. This ratchet holds that for the five stylesheets the authoring
// surface renders, and DELIBERATELY NOT for the repository. A repo-wide ban
// would fail on files nobody is certifying today and would be switched off
// within a week; a scoped ratchet is one that survives.
//
// Two of the declarations it protects are not spacing. The schema tree's indent
// is how a column reads as belonging to its table, and the debug log's stripe
// is how a severity reads at a glance. Under RTL a physical left puts both on
// the wrong edge, which does not look untidy - it looks like different data.

import { describe, expect, it } from "vitest";
import { existsSync, readFileSync } from "node:fs";
import { join } from "node:path";

const ROOT = process.cwd();

/** Exactly the stylesheets T-040 certifies. Adding one is a deliberate act. */
const CERTIFIED = [
  "src/authoring/authoring-shell.css",
  "src/authoring/authoring-states.css",
  "src/authoring/role-binding.css",
  "src/authoring/s2-query-binding.css",
  "src/canvas/canvas.css",
  "src/pages/Prep/CanvasDebugLog.css",
  "src/pages/Prep/CanvasModeBar.css",
  "src/pages/Prep/CanvasSchemaTree.css",
];

// Assembled so this file is not itself an offender in any later text scan.
const LEFT = "l" + "eft";
const RIGHT = "r" + "ight";

const FORBIDDEN: { label: string; pattern: RegExp }[] = [
  { label: "text-align: " + LEFT, pattern: new RegExp("text-align\\s*:\\s*" + LEFT) },
  { label: "text-align: " + RIGHT, pattern: new RegExp("text-align\\s*:\\s*" + RIGHT) },
  { label: "margin-" + LEFT, pattern: new RegExp("margin-" + LEFT + "\\s*:") },
  { label: "margin-" + RIGHT, pattern: new RegExp("margin-" + RIGHT + "\\s*:") },
  { label: "padding-" + LEFT, pattern: new RegExp("padding-" + LEFT + "\\s*:") },
  { label: "padding-" + RIGHT, pattern: new RegExp("padding-" + RIGHT + "\\s*:") },
  { label: "border-" + LEFT, pattern: new RegExp("border-" + LEFT + "[a-z-]*\\s*:") },
  { label: "border-" + RIGHT, pattern: new RegExp("border-" + RIGHT + "[a-z-]*\\s*:") },
];

describe("PPIQ T-040 G09 logical direction", () => {
  it("certifies stylesheets that actually exist", () => {
    // A renamed file must fail here rather than silently leave the scope and
    // take its physical directions with it.
    const missing = CERTIFIED.filter((f) => !existsSync(join(ROOT, f)));
    expect(missing, "certified stylesheet missing:\n  " + missing.join("\n  ")).toEqual([]);
  });

  it("uses no physical direction property in any certified stylesheet", () => {
    const offenders: string[] = [];
    for (const file of CERTIFIED) {
      const lines = readFileSync(join(ROOT, file), "utf8").split("\n");
      lines.forEach((line, i) => {
        for (const rule of FORBIDDEN) {
          if (rule.pattern.test(line)) {
            offenders.push(file + ":" + (i + 1) + " " + rule.label);
          }
        }
      });
    }
    expect(offenders, "physical direction survives:\n  " + offenders.join("\n  ")).toEqual([]);
  });

  it("keeps the schema tree indent as an inline-start padding", () => {
    // The indent is meaning, not decoration: it is how a column reads as
    // belonging to its table. It must exist, and it must be logical.
    const css = readFileSync(join(ROOT, "src/pages/Prep/CanvasSchemaTree.css"), "utf8");
    expect(css).toContain("padding-inline-start");
  });

  it("keeps the debug log severity stripe as an inline-start border", () => {
    const css = readFileSync(join(ROOT, "src/pages/Prep/CanvasDebugLog.css"), "utf8");
    expect(css).toContain("border-inline-start");
    expect(css).toContain("border-inline-start-color");
  });
});
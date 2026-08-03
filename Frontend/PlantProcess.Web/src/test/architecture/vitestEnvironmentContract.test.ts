// @vitest-environment node
import { describe, expect, it } from "vitest";
import { readdirSync, readFileSync } from "node:fs";
import { join } from "node:path";

/**
 * PPIQ-T14 - every architecture test declares its environment.
 *
 * These tests only read files from disk. The global vitest environment is
 * jsdom, and with fileParallelism disabled each file pays a browser setup and
 * teardown in sequence - which was roughly two thirds of the suite wall time
 * before T-011. Declaring node removes that cost per file.
 *
 * This is a RATCHET rather than a note in a document, because a note decays and
 * a test does not. A new architecture test added without the pragma fails here
 * immediately, with the reason in the message.
 *
 * If a future architecture test genuinely needs a DOM, declare jsdom
 * explicitly. The rule is that the environment is DECLARED, not that it is
 * always node.
 */
const ROOT = process.cwd();
const ARCH_DIR = join(ROOT, "src", "test", "architecture");
const PRAGMA = /@vitest-environment\s+(\S+)/;

describe("PPIQ-T14 architecture tests declare their environment", () => {
  const files = readdirSync(ARCH_DIR).filter((f) => f.endsWith(".test.ts"));

  it("finds architecture test files to check", () => {
    expect(files.length).toBeGreaterThan(0);
  });

  it("every architecture test file declares @vitest-environment", () => {
    const missing: string[] = [];
    for (const file of files) {
      const text = readFileSync(join(ARCH_DIR, file), "utf8");
      if (!PRAGMA.test(text)) missing.push(file);
    }
    expect(
      missing,
      `These architecture tests do not declare @vitest-environment. Add ` +
        `"// @vitest-environment node" as the first line, or declare jsdom if ` +
        `the test genuinely needs a DOM: ${missing.join(", ")}`
    ).toEqual([]);
  });

  it("declares node unless the file actually touches the DOM", () => {
    const wrong: string[] = [];
    for (const file of files) {
      const text = readFileSync(join(ARCH_DIR, file), "utf8");
      const declared = text.match(PRAGMA)?.[1];
      const touchesDom = /document\.|window\.|@testing-library/.test(
        text.replace(/^\s*\/\/.*$/gm, "").replace(/\/\*[\s\S]*?\*\//g, "")
      );
      if (declared === "jsdom" && !touchesDom) wrong.push(file);
    }
    expect(
      wrong,
      `These declare jsdom but never touch the DOM, so they pay a browser ` +
        `environment for nothing: ${wrong.join(", ")}`
    ).toEqual([]);
  });
});
import { describe, expect, it } from "vitest";
import { readFileSync, readdirSync, statSync } from "node:fs";
import { join, relative } from "node:path";

function read(rel: string): string {
  return readFileSync(join(process.cwd(), rel), "utf8");
}

function sourceFiles(root: string): string[] {
  const out: string[] = [];
  for (const entry of readdirSync(root)) {
    const full = join(root, entry);
    const stat = statSync(full);
    if (stat.isDirectory()) {
      out.push(...sourceFiles(full));
      continue;
    }
    if (/\.(ts|tsx)$/.test(entry)) out.push(full);
  }
  return out;
}

describe("T-241 Canvas architecture guards", () => {
  it("C241-06 has exactly one PortType declaration in product source", () => {
    const root = join(process.cwd(), "src");
    const declarations: string[] = [];
    for (const file of sourceFiles(root)) {
      const text = readFileSync(file, "utf8");
      if (/export\s+type\s+PortType\s*=/.test(text)) {
        declarations.push(relative(process.cwd(), file).replace(/\\/g, "/"));
      }
    }
    expect(declarations).toEqual(["src/canvas/ports.ts"]);
  });

  it("C241-06/07 keeps the T-241 contract free of customer/industry/default-grain vocabulary", () => {
    const text = (
      read("src/authoring/graphSemantics.ts")
      + "\n"
      + read("src/authoring/blockRegistry.ts")
    ).toLowerCase();

    for (const forbidden of [
      "coil", "steel", "oil", "furnace",
      "material_unit_id", "heat_id", "batch_id",
    ]) {
      expect(text, `forbidden generic-product token: ${forbidden}`).not.toContain(forbidden);
    }
  });

  it("C241-10 SharedAuthoringShell remains the single product surface using the existing canvas primitive", () => {
    const shell = read("src/authoring/SharedAuthoringShell.tsx");
    expect(shell).toContain("@/canvas/CanvasShell");
    expect(shell).toContain("./graphSemantics");
    expect(shell).not.toMatch(/\b(CanvasV2|ProductionCanvas|NewCanvas)\b/);
  });

  it("registry availability is compile-time coupled to BoardNodeKind", () => {
    const registry = read("src/authoring/blockRegistry.ts");
    expect(registry).toContain('import type { BoardNodeKind } from "./graphSemantics";');
    expect(registry).toContain("available: true;");
    expect(registry).toContain("boardKind: BoardNodeKind;");
  });

  it("graph validation owns a runtime-checkable executable kind set", () => {
    const graph = read("src/authoring/graphSemantics.ts");
    expect(graph).toContain("EXECUTABLE_BOARD_NODE_KINDS");
    expect(graph).toContain("isExecutableBoardNodeKind");
    expect(graph).toContain("this build has no behaviour for a ");
  });
});

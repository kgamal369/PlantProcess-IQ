import { describe, expect, it } from "vitest";
import { readFileSync, readdirSync, statSync } from "node:fs";
import { join, relative } from "node:path";

import { paletteEligibleBlocks, type BlockDefinition } from "./blockRegistry";
import { isExecutableBoardNodeKind } from "./graphSemantics";

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

  it("palette eligibility is compile-time bound to a valid BoardNodeKind", () => {
    // AMENDED UNDER T-242. This control used to assert that the registry
    // SOURCE contained the literal "available: true;". That tests the spelling
    // of an implementation, not the invariant behind it, so the authorised
    // successor design broke the test without breaking the rule.
    //
    // T-241's actual requirement, restated and proven here: a block the
    // product will OFFER must be bound at compile time to a board kind the
    // graph knows how to validate, so a toolbox can never present something
    // that looks executable and has no behaviour. T-242 replaced stored
    // availability with derived capability; the binding survives untouched.
    const registry = read("src/authoring/blockRegistry.ts");
    expect(registry).toContain('import type { BoardNodeKind } from "./graphSemantics";');
    expect(registry).toContain("boardKind: BoardNodeKind;");

    // The RUNTIME half: everything eligible names a kind the graph executes.
    const eligible = paletteEligibleBlocks();
    expect(eligible.length).toBeGreaterThan(0);
    for (const block of eligible) {
      expect(block.boardKind, block.id).toBeDefined();
      expect(isExecutableBoardNodeKind(block.boardKind as string), block.id).toBe(true);
    }
  });

  it("a placeable block cannot be declared without a valid board kind", () => {
    // The COMPILE-TIME half, as a negative fixture. Each declaration below is
    // rejected by the type contract, and @ts-expect-error fails the BUILD if
    // any of them ever starts compiling - which is the regression this guard
    // exists to catch. A runtime assertion could not prove this at all.
    // The COMPILE-TIME half, as a negative fixture. Both declarations below
    // are rejected by the type contract, and @ts-expect-error fails the BUILD
    // if either ever starts compiling - which is the regression this guard
    // exists to catch. A runtime assertion cannot prove this at all.
    //
    // Each declaration is ONE LINE on purpose: @ts-expect-error suppresses the
    // next line only, and an assignability error inside a multi-line literal
    // is reported at the offending PROPERTY, which the directive would miss.
    const base = { id: "x", label: "x", group: "relational", inputs: "-", outputs: "-" };

    // @ts-expect-error an implemented block must declare a boardKind and a seed
    const missingKind: BlockDefinition = { ...base, implemented: true, capabilities: { evaluable: false, persistable: true } };

    // @ts-expect-error "not-a-kind" is outside the executable vocabulary
    const invalidKind: BlockDefinition = { ...base, implemented: true, boardKind: "not-a-kind", seed: () => ({}), capabilities: { evaluable: false, persistable: true } };

    void missingKind;
    void invalidKind;
  });

  it("the parameter schema module never becomes a second block catalogue", () => {
    // T-242 Stage 4. blockParameters holds reusable SHAPES. The moment it also
    // maps a block id to a title, a kind or an availability, two files answer
    // the same question and can disagree - which is the duplication Stage 3
    // removed from the shell. It may not contain a block id at all.
    const schemas = read("src/authoring/blockParameters.ts");
    const registry = read("src/authoring/blockRegistry.ts");
    for (const blockId of ["expr-arithmetic", "expr-comparison", "expr-logic",
                           "expr-conditional", "loop-for-each", "loop-repeat-n",
                           "loop-while-bounded", "select-columns", "derived-column"]) {
      expect(schemas, blockId + " belongs to the catalogue, not the schemas")
        .not.toContain('"' + blockId + '"');
      expect(registry).toContain('"' + blockId + '"');
    }
    expect(schemas).not.toContain("BLOCK_REGISTRY");
    expect(schemas).not.toContain("isPaletteEligible");
    expect(schemas).not.toContain("implemented:");
  });

  it("the schema module cannot create a runtime import cycle", () => {
    // graphSemantics and blockSemantics both READ blockParameters at runtime,
    // so a runtime import back the other way would be a cycle. Type-only
    // imports are erased and are fine; a value import is not.
    const schemas = read("src/authoring/blockParameters.ts");
    const valueImports = schemas
      .split("\n")
      .filter((l) => l.trim().startsWith("import ") && !l.trim().startsWith("import type"));
    for (const line of valueImports) {
      expect(line, "blockParameters may not import a value from the graph or the catalogue")
        .not.toMatch(/graphSemantics|blockRegistry|blockSemantics|BlockNodes/);
    }
  });

  it("there is one node component for every executable family, not one each", () => {
    // A per-family switch or a component per family would rebuild, in JSX,
    // exactly the duplication the catalogue exists to prevent.
    const nodes = read("src/authoring/BlockNodes.tsx");
    for (const kind of ["arithmetic", "comparison", "logic", "conditional",
                        "for-each", "repeat-n", "while-bounded"]) {
      expect(nodes, kind + " must not have its own component")
        .not.toMatch(new RegExp("function\\s+\\w*" + kind.replace("-", "") + "\\w*Node", "i"));
    }
    expect(nodes).toContain("export function FamilyNode");
    expect(nodes).toContain("contractForKind");
    // The registered families are derived from the catalogue, not listed here.
    expect(nodes).toContain("BLOCK_REGISTRY");
  });

  it("the rendered port sentence has no authority of its own", () => {
    // Comment-stripped, because a guard that scans raw text fires on prose
    // explaining the rule and proves nothing about the code that follows it.
    const codeOnly = (text: string) => text
      .replace(/\/\*[\s\S]*?\*\//g, "")
      .split("\n")
      .map((l) => { const i = l.indexOf("//"); return i >= 0 ? l.slice(0, i) : l; })
      .join("\n");
    // If the node ever hard-codes a phrase like "two numbers", the contract
    // and the prose become two authorities and one of them will go stale.
    const nodes = read("src/authoring/BlockNodes.tsx");
    const semantics = read("src/authoring/blockSemantics.ts");
    expect(nodes).toContain("describeSignature");
    expect(semantics).toContain("describeSignature");
    for (const source of [codeOnly(nodes), codeOnly(semantics)]) {
      expect(source).not.toContain("two numbers and produces");
      expect(source).not.toContain("FAMILY_SIGNATURES");
    }
  });

  it("graph validation owns a runtime-checkable executable kind set", () => {
    const graph = read("src/authoring/graphSemantics.ts");
    expect(graph).toContain("EXECUTABLE_BOARD_NODE_KINDS");
    expect(graph).toContain("isExecutableBoardNodeKind");
    expect(graph).toContain("this build has no behaviour for a ");
  });
});
// @vitest-environment node
import { describe, expect, it } from "vitest";
import { readFileSync } from "node:fs";
import { join } from "node:path";

/**
 * PPIQ-T15 - the JourneyRail carries the canonical journey and nothing else.
 *
 * Chapter 2 section 3.3.1 defines J1 to J15 and says a second journey written
 * anywhere is deleted rather than reconciled. Before T-012 this component
 * declared its own fifteen stages and rendered them as "Step N of 15" - nine of
 * the fifteen meant something different at the same number.
 *
 * The labels below are VERBATIM from the chapter. If someone edits the rail,
 * this fails. If the chapter changes, this fails until the rail follows.
 */
const ROOT = process.cwd();
const RAIL = join(ROOT, "src", "components", "journey", "JourneyRail.tsx");

const CANONICAL = [
  "Install and first login",
  "Activate the licence",
  "Create users and roles",
  "Declare read-only connections",
  "Register datasets",
  "First incremental import",
  "Author the transformation and publish the relationship model",
  "Project to canonical, with validation",
  "Walk the genealogy",
  "Build pages, widgets and filters",
  "Explore associatively",
  "Author and run analysis through the gate",
  "Read findings, risk, practices and value",
  "Decide, act and measure",
  "Operate, govern and retain",
];

// Routes that must never be a journey target. author-mapping is out of the
// presented inventory; analysis-jobs leaves the M1 surface; /assistant has no
// Chapter 3 route at all because the assistant is a shell component.
const FORBIDDEN = ["/data-integration/author-mapping", "/investigate/analysis-jobs", "/assistant"];

function railSource(): string {
  return readFileSync(RAIL, "utf8");
}
function stageBlocks(src: string): string[] {
  const start = src.indexOf("const STAGES: ReadonlyArray<Stage> = [");
  expect(start, "STAGES array not found").toBeGreaterThan(-1);
  const end = src.indexOf("\n];", start);
  return src
    .slice(start, end)
    .split("\n")
    .filter((l) => l.trim().startsWith("{ n:"));
}

describe("PPIQ-T15 JourneyRail is the canonical journey", () => {
  it("declares exactly fifteen stages", () => {
    expect(stageBlocks(railSource())).toHaveLength(15);
  });

  it("uses the Chapter 2 3.3.1 labels, verbatim and in order", () => {
    const blocks = stageBlocks(railSource());
    const labels = blocks.map((b) => b.match(/label:\s*"([^"]+)"/)?.[1] ?? "");
    expect(labels).toEqual(CANONICAL);
  });

  it("numbers the stages 1 to 15 in order", () => {
    const ns = stageBlocks(railSource()).map((b) => Number(b.match(/n:\s*(\d+)/)?.[1] ?? -1));
    expect(ns).toEqual(Array.from({ length: 15 }, (_, i) => i + 1));
  });

  it("gives every match prefix to exactly one stage, so no stage is unreachable", () => {
    const blocks = stageBlocks(railSource());
    const owner = new Map<string, number>();
    const clashes: string[] = [];
    blocks.forEach((b, i) => {
      const raw = b.match(/match:\s*\[([^\]]*)\]/)?.[1] ?? "";
      const prefixes = Array.from(raw.matchAll(/"([^"]+)"/g)).map((m) => m[1]);
      for (const p of prefixes) {
        if (owner.has(p)) clashes.push(`${p} claimed by stage ${owner.get(p)! + 1} and stage ${i + 1}`);
        else owner.set(p, i);
      }
    });
    expect(
      clashes,
      `A prefix claimed twice makes the later stage unreachable - activeIndex keeps ` +
        `a match only when prefix.length is STRICTLY greater, so an equal-length tie ` +
        `goes to the first stage. That is how stage 4 was dead before T-012: ${clashes.join("; ")}`
    ).toEqual([]);
  });

  it("every non-commissioned stage can become current", () => {
    const blocks = stageBlocks(railSource());
    const unreachable: string[] = [];
    blocks.forEach((b, i) => {
      if (/commissioned:\s*true/.test(b)) return;
      const raw = b.match(/match:\s*\[([^\]]*)\]/)?.[1] ?? "";
      if (!/"/.test(raw)) unreachable.push(`stage ${i + 1} has no match prefix`);
    });
    expect(unreachable).toEqual([]);
  });

  it("targets no route that is out of the presented inventory", () => {
    const src = railSource();
    const start = src.indexOf("const STAGES: ReadonlyArray<Stage> = [");
    const stages = src.slice(start, src.indexOf("\n];", start));
    const found = FORBIDDEN.filter((r) => new RegExp(`"${r}"`).test(stages));
    expect(
      found,
      `These are not journey targets: author-mapping is out of inventory, ` +
        `analysis-jobs leaves M1, and the assistant is a shell component with no ` +
        `Chapter 3 route: ${found.join(", ")}`
    ).toEqual([]);
  });

  it("marks J1 to J3 as commissioning so the step count is honest", () => {
    const blocks = stageBlocks(railSource());
    const commissioned = blocks
      .map((b, i) => (/commissioned:\s*true/.test(b) ? i + 1 : 0))
      .filter(Boolean);
    expect(commissioned).toEqual([1, 2, 3]);
  });

  /**
   * T-250/F3g - a redirect alias must land where its stage already lives.
   *
   * /assistant/configuration was a real route that no stage matched, while
   * /assistant-config, a redirect-only legacy alias, matched J15. The journey
   * lit up only for the redirect SOURCE, so the route people actually navigate
   * to left the rail dark.
   *
   * The obvious law - every prefix must be a real route - is WRONG here. Eight
   * legacy aliases are deliberately kept so old links still highlight the rail,
   * and that law would fail all eight. The law that catches the real defect and
   * nothing else is: an alias may resolve only to a redirect, provided the route
   * it redirects to is itself a prefix of the SAME stage.
   */
  it("resolves every redirect-only alias into its own stage", () => {
    const app = readFileSync(join(ROOT, "src", "App.tsx"), "utf8");

    type RouteRow = { path: string; redirect: boolean; target: string };
    const routes: RouteRow[] = [];
    const pathRe = /path=\s*"([^"]+)"/g;
    let m: RegExpExecArray | null;
    while ((m = pathRe.exec(app)) !== null) {
      let tail = app.slice(m.index, m.index + 700);
      const nextRoute = tail.indexOf("<Route", 6);
      if (nextRoute > 0) tail = tail.slice(0, nextRoute);
      const redirect = tail.includes("<Navigate");
      const t = redirect ? /<Navigate[^>]*\sto=\s*"([^"]+)"/.exec(tail) : null;
      routes.push({
        path: m[1].replace(/\*$/, "").replace(/\/$/, ""),
        redirect,
        target: t ? t[1] : "",
      });
    }
    expect(routes.length, "no routes parsed from App.tsx").toBeGreaterThan(20);

    const realRoute = (p: string) =>
      routes.some(
        (r) => !r.redirect && (r.path === p || r.path.startsWith(p + "/") || (p.startsWith(r.path + "/") && r.path.length > 1)),
      );
    const redirectTarget = (p: string) => {
      const exact = routes.find((r) => r.redirect && r.path === p);
      if (exact) return exact.target;
      const child = routes.find((r) => r.redirect && r.path.startsWith(p + "/"));
      return child ? child.target : "";
    };

    const offenders: string[] = [];
    stageBlocks(railSource()).forEach((block, index) => {
      const raw = block.match(/match:\s*\[([^\]]*)\]/)?.[1] ?? "";
      const prefixes = Array.from(raw.matchAll(/"([^"]+)"/g)).map((x) => x[1]);
      for (const prefix of prefixes) {
        if (realRoute(prefix)) continue;
        const target = redirectTarget(prefix);
        if (target === "") {
          offenders.push(`stage ${index + 1} ${prefix}: neither a real route nor a readable redirect`);
          continue;
        }
        const lands = prefixes.some((q) => target === q || target.startsWith(q + "/"));
        if (!lands) offenders.push(`stage ${index + 1} ${prefix} redirects to ${target}, which no prefix of that stage claims`);
      }
    });

    expect(
      offenders,
      `A redirect-only alias whose destination belongs to no prefix of its own stage ` +
        `leaves the real route unlit: ${offenders.join("; ")}`
    ).toEqual([]);
  });
});
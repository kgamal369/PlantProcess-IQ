import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

const root = path.resolve(process.cwd(), "src");

const criticalPages = [
  "pages/DataIntegration/AuthorMappingPage.tsx",
  "pages/DataIntegration/AlertingPage.tsx",
  "pages/DataIntegration/SupervisorReportPage.tsx",
];

function read(relative: string) {
  return fs.readFileSync(path.join(root, relative), "utf8");
}

describe("Journey UI professional contract", () => {
  it("all critical new journey pages use the canonical page header and standard surfaces", () => {
    for (const relative of criticalPages) {
      const source = read(relative);
      expect(source, relative).toContain("StandardPageHeader");
      expect(source, relative).toContain("StandardCard");
      expect(source, relative).not.toMatch(/<table(?=[\s>])/i);
      expect(source, relative).not.toMatch(/<select(?=[\s>])/i);
      expect(source, relative).not.toMatch(/<input(?=[\s>])/i);
    }
  });

  it("raw technical mapping output is progressively disclosed instead of permanently expanded", () => {
    const source = read("pages/DataIntegration/AuthorMappingPage.tsx");
    expect(source).toContain("<details");
    expect(source).toContain("Technical response details");
  });

  it("the canonical journey rail exposes fifteen navigable stages", () => {
    const source = read("components/journey/JourneyRail.tsx");
    const stageCount = (source.match(/\{ n: \d+, label:/g) ?? []).length;
    expect(stageCount).toBe(15);
    expect(source).toContain("Plant data log");
  });

  it("the professional journey stylesheet is loaded globally", () => {
    expect(read("index.css")).toContain('styles/journey-professional.css');
  });
});

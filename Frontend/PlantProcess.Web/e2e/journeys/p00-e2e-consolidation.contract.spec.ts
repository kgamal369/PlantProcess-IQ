import fs from "node:fs";
import path from "node:path";
import { expect, test } from "@playwright/test";

const frontendRoot = process.cwd();
const repoRoot = path.resolve(frontendRoot, "..", "..");
const mapPath = path.join(repoRoot, "docs", "testing", "P00D_E2E_Consolidation_Map.json");

type ConsolidationMap = {
  canonicalJourneys: Array<{
    id: string;
    title: string;
    keepSpecs: string[];
    absorbs: string[];
    status: string;
  }>;
  futureDeferredJourneys: Array<{
    id: string;
    plannedSpec: string;
    blockedBy: string;
    status: string;
  }>;
};

function readMap(): ConsolidationMap {
  return JSON.parse(fs.readFileSync(mapPath, "utf8")) as ConsolidationMap;
}

test.describe("P00D E2E consolidation contract", () => {
  test("maps phase-named E2E specs into canonical behavioural journeys", () => {
    const map = readMap();

    expect(map.canonicalJourneys.length).toBeGreaterThanOrEqual(8);

    const mappedAbsorbedSpecs = new Set(
      map.canonicalJourneys.flatMap((journey) => journey.absorbs),
    );

    expect(mappedAbsorbedSpecs).toContain("e2e/phase1-golden-demo.spec.ts");
    expect(mappedAbsorbedSpecs).toContain("e2e/phase2-chart-interaction.spec.ts");
    expect(mappedAbsorbedSpecs).toContain("e2e/phase2-backend-outage.spec.ts");
    expect(mappedAbsorbedSpecs).toContain("e2e/phase78-workflow-widget.spec.ts");

    for (const journey of map.canonicalJourneys) {
      expect(journey.status).toBe("mapped");
      expect(journey.title.trim().length).toBeGreaterThan(5);

      for (const keepSpec of journey.keepSpecs) {
        const fullPath = path.join(frontendRoot, keepSpec);
        expect(fs.existsSync(fullPath), keepSpec + " should remain available as evidence").toBe(true);
      }
    }
  });

  test("keeps P06 and P09 future journeys explicitly deferred instead of fake-passing", () => {
    const map = readMap();

    expect(map.futureDeferredJourneys).toHaveLength(2);

    for (const deferred of map.futureDeferredJourneys) {
      expect(deferred.status).toBe("deferred-not-fake-passing");
      expect(deferred.blockedBy.length).toBeGreaterThan(10);

      const plannedSpecPath = path.join(frontendRoot, deferred.plannedSpec);
      expect(fs.existsSync(plannedSpecPath)).toBe(false);
    }
  });
});

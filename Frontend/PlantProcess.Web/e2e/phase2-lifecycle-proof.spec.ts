import { expect, test } from "@playwright/test";
import { apiBaseUrl, login } from "./helpers/auth";

async function getJson<T>(
  request: Parameters<Parameters<typeof test>[1]>[0]["request"],
  path: string,
  token: string,
): Promise<T> {
  const response = await request.get(apiBaseUrl + path, {
    headers: {
      Accept: "application/json",
      Authorization: "Bearer " + token,
    },
  });

  expect(
    response.ok(),
    path + " returned HTTP " + response.status() + " body: " + await response.text(),
  ).toBeTruthy();

  return (await response.json()) as T;
}

test.describe("P02 source lifecycle and genealogy acceptance", () => {
  test("eight source lifecycle evidence is available, mapped and internally exposed only", async ({ request }) => {
    const token = await login(request);

    const body = await getJson<{
      status: string;
      sourceCount: number;
      availableSourceCount: number;
      mappedSourceCount: number;
      autoConnectViolations: number;
      externalExposureViolations: number;
      sources: Array<{
        sourceCode: string;
        providerType: string;
        sourceRole: string;
        isAvailableNow: boolean;
        mappingStatus: string;
      }>;
    }>(request, "/phase2/source-lifecycle/acceptance", token);

    expect(body.status).toBe("Passed");
    expect(body.sourceCount).toBeGreaterThanOrEqual(8);
    expect(body.availableSourceCount).toBe(body.sourceCount);
    expect(body.mappedSourceCount).toBe(body.sourceCount);
    expect(body.autoConnectViolations).toBe(0);
    expect(body.externalExposureViolations).toBe(0);

    expect(body.sources.map((source) => source.providerType).sort()).toEqual(
      expect.arrayContaining(["Excel", "MsSql", "MySql", "Oracle", "PostgreSQL"]),
    );

    expect(body.sources.map((source) => source.sourceRole).sort()).toEqual(
      expect.arrayContaining([
        "Caster",
        "Downtime",
        "Downstream",
        "Hot Strip Mill",
        "MeltShop",
        "Quality Lab",
        "Surface Inspection",
        "Yard",
      ]),
    );
  });

  test("ADV_COIL4002 resolves through canonical genealogy and downstream outcomes", async ({ request }) => {
    const token = await login(request);

    const body = await getJson<{
      status: string;
      materialCode: string;
      sourceCount: number;
      resolvedStepCount: number;
      totalStepCount: number;
      mappingEngineRequired: boolean;
      path: Array<{
        canonicalGrain: string;
        sourceRole: string;
        sourceCode: string;
        entityId: string;
        parentEntityId: string | null;
        isResolved: boolean;
      }>;
    }>(request, "/phase2/genealogy/ADV_COIL4002", token);

    expect(body.status).toBe("Passed");
    expect(body.materialCode).toBe("ADV_COIL4002");
    expect(body.sourceCount).toBeGreaterThanOrEqual(8);
    expect(body.resolvedStepCount).toBe(body.totalStepCount);
    expect(body.mappingEngineRequired).toBeTruthy();

    expect(body.path.map((step) => step.canonicalGrain)).toEqual(
      expect.arrayContaining([
        "ChargeOrHeat",
        "LadleOrBatch",
        "IntermediateVessel",
        "LineOrStrand",
        "IntermediateProduct",
        "FinalProductUnit",
        "QualityOutcome",
        "DefectOutcome",
        "DowntimeImpact",
        "InventoryLocation",
      ]),
    );

    expect(body.path.every((step) => step.isResolved)).toBeTruthy();
  });

  test("import lifecycle proves cold, delta, duplicate, crash-resume, drift and orphan handling", async ({ request }) => {
    const token = await login(request);

    const body = await getJson<{
      status: string;
      proofCount: number;
      passedProofCount: number;
      proofs: Array<{ proofCode: string; status: string }>;
    }>(request, "/phase2/import-lifecycle/acceptance", token);

    expect(body.status).toBe("Passed");
    expect(body.proofCount).toBeGreaterThanOrEqual(6);
    expect(body.passedProofCount).toBe(body.proofCount);

    expect(body.proofs.map((proof) => proof.proofCode)).toEqual(
      expect.arrayContaining([
        "P02-IMPORT-001",
        "P02-IMPORT-002",
        "P02-IMPORT-003",
        "P02-IMPORT-004",
        "P02-IMPORT-005",
        "P02-IMPORT-006",
      ]),
    );
  });

  test("P02 acceptance summary closes the data lifecycle proof", async ({ request }) => {
    const token = await login(request);

    const body = await getJson<{
      status: string;
      mappedSourceCount: number;
      resolvedGenealogyStepCount: number;
      passedImportProofCount: number;
      evidence: string[];
    }>(request, "/phase2/acceptance-summary", token);

    expect(body.status).toBe("Passed");
    expect(body.mappedSourceCount).toBeGreaterThanOrEqual(8);
    expect(body.resolvedGenealogyStepCount).toBeGreaterThanOrEqual(8);
    expect(body.passedImportProofCount).toBeGreaterThanOrEqual(6);
    expect(body.evidence.join(" ")).toMatch(/Eight-source|ADV_COIL4002|Cold load/i);
  });
});

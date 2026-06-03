import { describe, expect, it } from "vitest";
import { existsSync, readFileSync, statSync } from "node:fs";
import { join } from "node:path";

function repoPath(relativePath: string) {
  return join(process.cwd(), relativePath);
}

function read(relativePath: string) {
  return readFileSync(repoPath(relativePath), "utf8");
}

describe("frontend architecture guard", () => {
  it("retired legacy API files must stay deleted", () => {
    const forbiddenFiles = [
      "src/api/plantProcessApi.ts",
      "src/api/legacy/plantProcessApi.ts",
      "src/api/legacy/legacyApiHardening.ts"
    ];

    for (const relativePath of forbiddenFiles) {
      expect(
        existsSync(repoPath(relativePath)),
        `${relativePath} must stay deleted after C1/C2 API retirement`
      ).toBe(false);
    }
  });

  it("product API client spine must exist", () => {
    const expectedFiles = [
      "src/api/productApiClient.ts",
      "src/api/productCoreApiClient.ts",
      "src/api/productApiHardening.ts",
      "src/api/admin/index.ts",
      "src/api/analytics/index.ts",
      "src/api/dashboarding/index.ts",
      "src/api/integration/index.ts",
      "src/api/http/index.ts"
    ];

    for (const relativePath of expectedFiles) {
      expect(() => statSync(repoPath(relativePath)), `${relativePath} should exist`).not.toThrow();
    }
  });

  it("product API client spine must not reintroduce plantProcessApi references", () => {
    const guardedFiles = [
      "src/api/productApiClient.ts",
      "src/api/productCoreApiClient.ts",
      "src/api/productApiHardening.ts"
    ];

    for (const relativePath of guardedFiles) {
      const content = read(relativePath);

      expect(
        content.includes("plantProcessApi"),
        `${relativePath} must not contain plantProcessApi`
      ).toBe(false);
    }
  });
});
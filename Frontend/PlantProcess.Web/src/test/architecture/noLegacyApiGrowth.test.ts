import { describe, expect, it } from "vitest";
import { readFileSync, statSync } from "node:fs";
import { join } from "node:path";

describe("frontend architecture guard", () => {
  it("legacy plantProcessApi.ts should not grow again", () => {
    const legacyPath = join(
      process.cwd(),
      "src",
      "api",
      "legacy",
      "plantProcessApi.ts"
    );

    const file = readFileSync(legacyPath, "utf8");
    const lineCount = file.split(/\r?\n/).length;

    expect(lineCount).toBeLessThanOrEqual(
      1310
    );
  });

  it("new domain api folders must exist through index exports", () => {
    const expectedFiles = [
      "src/api/admin/index.ts",
      "src/api/analytics/index.ts",
      "src/api/dashboarding/index.ts",
      "src/api/integration/index.ts",
      "src/api/http/index.ts"
    ];

    for (const relativePath of expectedFiles) {
      const fullPath = join(process.cwd(), relativePath);
      expect(() => statSync(fullPath), `${relativePath} should exist`).not.toThrow();
    }
  });
});
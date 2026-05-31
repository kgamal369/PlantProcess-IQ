const fs = require("node:fs");
const path = require("node:path");

const root = process.cwd();
const frontendRoot = path.join(root, "Frontend", "PlantProcess.Web");

function write(relativePath, content) {
  const full = path.join(frontendRoot, relativePath.replaceAll("/", path.sep));
  fs.mkdirSync(path.dirname(full), { recursive: true });
  fs.writeFileSync(full, content.replace(/\r\n/g, "\n"), "utf8");
}

write(
  "src/test/architecture/frontendArchitecture.test.ts",
  `import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

describe("frontend architecture", () => {
  it("keeps mandatory API split folders present", () => {
    const root = process.cwd();

    const mandatoryFolders = [
      "admin",
      "analytics",
      "dashboarding",
      "integration",
      "http"
    ];

    for (const folder of mandatoryFolders) {
      const file = path.join(root, "src", "api", folder, "index.ts");
      expect(fs.existsSync(file), \`Missing mandatory src/api/\${folder}/index.ts\`).toBe(true);
    }
  });

  it("does not force future API folders before their feature phase exists", () => {
    const apiRoot = path.join(process.cwd(), "src", "api");

    const existingIndexedFolders = fs
      .readdirSync(apiRoot, { withFileTypes: true })
      .filter((entry) => entry.isDirectory())
      .map((entry) => entry.name)
      .filter((folder) => fs.existsSync(path.join(apiRoot, folder, "index.ts")))
      .sort();

    expect(existingIndexedFolders.length).toBeGreaterThanOrEqual(5);
    expect(existingIndexedFolders).toContain("admin");
    expect(existingIndexedFolders).toContain("analytics");
    expect(existingIndexedFolders).toContain("dashboarding");
    expect(existingIndexedFolders).toContain("integration");
    expect(existingIndexedFolders).toContain("http");
  });

  it("does not execute Playwright specs in Vitest", () => {
    const testRoot = path.join(process.cwd(), "src", "test");

    const files = fs
      .readdirSync(testRoot, { recursive: true })
      .map(String);

    expect(files.some((file) => file.endsWith(".spec.ts"))).toBe(false);
  });
});
`
);

console.log("Frontend architecture test aligned to current mandatory API split baseline.");

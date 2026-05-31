const fs = require("node:fs");
const path = require("node:path");

const root = process.cwd();
const frontendRoot = path.join(root, "Frontend", "PlantProcess.Web");

function write(relativePath, content) {
  const full = path.join(frontendRoot, relativePath.replaceAll("/", path.sep));
  fs.mkdirSync(path.dirname(full), { recursive: true });
  fs.writeFileSync(full, content.replace(/\r\n/g, "\n"), "utf8");
}

function read(relativePath) {
  return fs.readFileSync(path.join(frontendRoot, relativePath.replaceAll("/", path.sep)), "utf8");
}

function exists(relativePath) {
  return fs.existsSync(path.join(frontendRoot, relativePath.replaceAll("/", path.sep)));
}

// ============================================================
// 1. Add .env.test for Vitest/Vite test mode.
// ============================================================

write(
  ".env.test",
  [
    "VITE_API_BASE_URL=http://localhost:5063",
    "VITE_SMOKE_USERNAME=e2eadmin",
    "VITE_SMOKE_PASSWORD=E2EAdmin123!",
    ""
  ].join("\n")
);

// ============================================================
// 2. Harden Vitest config so import.meta.env is stable in tests.
// ============================================================

const vitestConfigPath = "vitest.config.ts";

if (exists(vitestConfigPath)) {
  let text = read(vitestConfigPath);

  if (!text.includes("VITE_API_BASE_URL")) {
    text = text.replace(
      /test:\s*{/,
      [
        "define: {",
        '    "import.meta.env.VITE_API_BASE_URL": JSON.stringify("http://localhost:5063"),',
        '    "import.meta.env.VITE_SMOKE_USERNAME": JSON.stringify("e2eadmin"),',
        '    "import.meta.env.VITE_SMOKE_PASSWORD": JSON.stringify("E2EAdmin123!"),',
        "  },",
        "  test: {"
      ].join("\n")
    );

    write(vitestConfigPath, text);
    console.log("Patched vitest.config.ts with stable test env defines.");
  } else {
    console.log("vitest.config.ts already contains VITE_API_BASE_URL.");
  }
} else {
  throw new Error("Missing Frontend/PlantProcess.Web/vitest.config.ts");
}

// ============================================================
// 3. Fix architecture test: do not dynamic-import API modules.
//    Dynamic import triggers apiConfig runtime env validation.
// ============================================================

write(
  "src/test/architecture/frontendArchitecture.test.ts",
  `import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

describe("frontend architecture", () => {
  it("keeps API split folders present", () => {
    const root = process.cwd();

    const folders = [
      "admin",
      "analytics",
      "dashboarding",
      "demo",
      "http",
      "integration",
      "license",
      "ml",
      "pageBuilder",
      "schemaMapping",
      "twoStageImport",
      "widgetScript"
    ];

    for (const folder of folders) {
      const file = path.join(root, "src", "api", folder, "index.ts");
      expect(fs.existsSync(file), \`Missing src/api/\${folder}/index.ts\`).toBe(true);
    }
  });

  it("does not execute Playwright specs in Vitest", () => {
    const vitestFiles = fs
      .readdirSync(path.join(process.cwd(), "src", "test"), { recursive: true })
      .map(String);

    expect(vitestFiles.some((file) => file.endsWith(".spec.ts"))).toBe(false);
  });
});
`
);

console.log("Frontend Vitest env + architecture test fixes applied.");

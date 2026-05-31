const fs = require("node:fs");
const path = require("node:path");

const root = process.cwd();
const frontendRoot = path.join(root, "Frontend", "PlantProcess.Web");

function write(file, text) {
  fs.writeFileSync(file, text.replace(/\r\n/g, "\n"), "utf8");
}

function read(file) {
  return fs.readFileSync(file, "utf8");
}

// ============================================================================
// 1) Fix phase1Workflow.api.ts raw fetch auth.
// This file uses raw fetch for /admin/phase1/connector-truth.
// It must send Authorization: Bearer <token>, same as apiClient.
// ============================================================================

const phase1ApiFile = path.join(
  frontendRoot,
  "src",
  "api",
  "phase1",
  "phase1Workflow.api.ts"
);

let phase1 = read(phase1ApiFile);

if (!phase1.includes('getAccessToken')) {
  phase1 = phase1.replace(
    'import { API_BASE_URL } from "./apiConfig";',
    'import { API_BASE_URL } from "./apiConfig";\nimport { getAccessToken } from "../http/apiClient";'
  );
}

if (!phase1.includes("function buildAuthHeaders")) {
  phase1 = phase1.replace(
    /import \{ getAccessToken \} from "\.\.\/http\/apiClient";\n/,
    `import { getAccessToken } from "../http/apiClient";

function buildAuthHeaders(headers: Record<string, string> = {}): Record<string, string> {
  const token = getAccessToken();

  if (!token) {
    return headers;
  }

  return {
    ...headers,
    Authorization: \`Bearer \${token}\`,
  };
}

`
  );
}

// GET headers
phase1 = phase1.replace(
  /headers:\s*\{\s*Accept:\s*"application\/json"\s*\}/g,
  'headers: buildAuthHeaders({ Accept: "application/json" })'
);

// POST headers
phase1 = phase1.replace(
  /headers:\s*\{\s*Accept:\s*"application\/json",\s*"Content-Type":\s*"application\/json",\s*\}/g,
  'headers: buildAuthHeaders({\n      Accept: "application/json",\n      "Content-Type": "application/json",\n    })'
);

// Remove cookie-based fetch mode completely; PlantProcess IQ uses bearer tokens.
phase1 = phase1.replace(/\s*credentials:\s*["']include["'],?/g, "");
phase1 = phase1.replace(/\s*credentials:\s*["']omit["'],?/g, "");

write(phase1ApiFile, phase1);
console.log("Patched phase1Workflow.api.ts to send Authorization bearer token.");

// ============================================================================
// 2) Replace phase03-two-stage-import.spec.ts to use the working shared auth.
// ============================================================================

const phase03File = path.join(
  frontendRoot,
  "e2e",
  "api",
  "phase03-two-stage-import.spec.ts"
);

const phase03 = `import { expect, test } from "@playwright/test";
import { apiBaseUrl, login } from "../helpers/auth";

test.describe("PPIQ Phase 03 two-stage delta import", () => {
  test("overview is authenticated and exposes source-shaped dump registry", async ({ request }) => {
    const token = await login(request);

    const response = await request.get(\`\${apiBaseUrl}/admin/two-stage-import/overview\`, {
      headers: {
        Authorization: \`Bearer \${token}\`,
        Accept: "application/json",
      },
    });

    expect(response.ok(), \`overview failed with \${response.status()}\`).toBeTruthy();

    const body = await response.json();

    expect(body).toHaveProperty("isReady");
    expect(body).toHaveProperty("sourceTables");
    expect(Array.isArray(body.sourceTables)).toBeTruthy();
    expect(body).toHaveProperty("jobs");
    expect(Array.isArray(body.jobs)).toBeTruthy();
  });

  test("stage1/stage2 full cycle endpoint is authenticated", async ({ request }) => {
    const token = await login(request);

    const response = await request.post(\`\${apiBaseUrl}/admin/two-stage-import/run-full-cycle\`, {
      headers: {
        Authorization: \`Bearer \${token}\`,
        Accept: "application/json",
        "Content-Type": "application/json",
      },
      data: {
        requestedBy: "Playwright Phase03",
        maxRows: 1000,
        timeoutSeconds: 120,
        maxMinutes: 1,
      },
    });

    expect(response.status(), "full-cycle endpoint must not reject authenticated E2E admin").not.toBe(401);
    expect(response.status(), "full-cycle endpoint must not reject authenticated E2E admin").not.toBe(403);
    expect(response.status(), "full-cycle endpoint must not crash").not.toBe(500);

    expect([200, 400, 404, 409, 422]).toContain(response.status());

    if (response.status() === 200) {
      const body = await response.json();

      expect(body).toHaveProperty("stage");
      expect(String(body.stage)).toMatch(/TwoStageFullCycle|FullCycle|Stage/i);
      expect(Array.isArray(body.rows)).toBeTruthy();
    }
  });
});
`;

write(phase03File, phase03);
console.log("Replaced phase03-two-stage-import.spec.ts with shared-auth version.");

// ============================================================================
// 3) Validation.
// ============================================================================

const validationFile = path.join(root, "tools", "p00", "validate-e2e-wave2-auth-cleanup.cjs");
fs.mkdirSync(path.dirname(validationFile), { recursive: true });

const validation = `const fs = require("node:fs");
const path = require("node:path");

const root = process.cwd();
const frontendRoot = path.join(root, "Frontend", "PlantProcess.Web");

const failures = [];

const phase1 = fs.readFileSync(
  path.join(frontendRoot, "src", "api", "phase1", "phase1Workflow.api.ts"),
  "utf8"
);

if (!phase1.includes("getAccessToken")) {
  failures.push("phase1Workflow.api.ts does not import/use getAccessToken.");
}

if (!phase1.includes("Authorization")) {
  failures.push("phase1Workflow.api.ts does not send Authorization header.");
}

if (/credentials\\s*:\\s*["']include["']/.test(phase1)) {
  failures.push("phase1Workflow.api.ts still uses credentials: include.");
}

const phase03 = fs.readFileSync(
  path.join(frontendRoot, "e2e", "api", "phase03-two-stage-import.spec.ts"),
  "utf8"
);

if (!phase03.includes('import { apiBaseUrl, login } from "../helpers/auth";')) {
  failures.push("phase03 spec does not use shared auth helper.");
}

if (/const username|const password|data:\\s*\\{\\s*username\\s*,\\s*password\\s*\\}/.test(phase03)) {
  failures.push("phase03 spec still contains old local username/password login.");
}

if (failures.length > 0) {
  console.error("E2E wave 2 auth cleanup validation failed:");
  for (const failure of failures) {
    console.error(" - " + failure);
  }
  process.exit(1);
}

console.log("E2E wave 2 auth cleanup validation passed.");
`;

write(validationFile, validation);

console.log("E2E wave 2 auth cleanup applied.");

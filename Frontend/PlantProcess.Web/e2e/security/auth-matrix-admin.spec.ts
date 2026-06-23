import fs from "node:fs";
import path from "node:path";
import { expect, request, test, type APIRequestContext } from "@playwright/test";

const apiBaseUrl = process.env.PPIQ_API_BASE_URL ?? process.env.VITE_API_BASE_URL ?? "http://localhost:5063";
const adminUser = process.env.PPIQ_ADMIN_USER ?? process.env.PPIQ_SMOKE_USERNAME ?? "admin";
const adminPassword = process.env.PPIQ_ADMIN_PASSWORD ?? process.env.PPIQ_SMOKE_PASSWORD ?? "";
const operatorUser = process.env.PPIQ_OPERATOR_USER ?? process.env.PPIQ_DATAMANAGER_USER ?? "datamanager";
const operatorPassword = process.env.PPIQ_OPERATOR_PASSWORD ?? process.env.PPIQ_DATAMANAGER_PASSWORD ?? "";

type Expected = readonly number[];
type MatrixRow = { group: string; path: string; anonymous: Expected; operator: Expected; admin: Expected; };

const unauthorized: Expected = [401, 403];
const adminOnlyOperator: Expected = [403];
const dataManagerOk: Expected = [200];
const adminOk: Expected = [200];

const rows: MatrixRow[] = [
  { group: "admin-overview", path: "/admin/overview", anonymous: unauthorized, operator: adminOnlyOperator, admin: adminOk },
  { group: "admin-jobs-monitor", path: "/admin/jobs-monitor/summary", anonymous: unauthorized, operator: dataManagerOk, admin: adminOk },
  { group: "admin-schema-mapping", path: "/admin/schema-mapping/catalog", anonymous: unauthorized, operator: dataManagerOk, admin: adminOk },
  { group: "admin-schema-mapping", path: "/admin/schema-mapping/readiness", anonymous: unauthorized, operator: dataManagerOk, admin: adminOk },
  { group: "admin-two-stage-import", path: "/admin/two-stage-import/overview", anonymous: unauthorized, operator: dataManagerOk, admin: adminOk },
  { group: "admin-two-stage-import", path: "/admin/two-stage-import/source-tables", anonymous: unauthorized, operator: dataManagerOk, admin: adminOk },
  { group: "admin-two-stage-import", path: "/admin/two-stage-import/runs", anonymous: unauthorized, operator: dataManagerOk, admin: adminOk },
  { group: "admin-license", path: "/admin/license/current", anonymous: unauthorized, operator: dataManagerOk, admin: adminOk },
  { group: "admin-workflow-foundation", path: "/admin/workflow-foundation/workflow-truth", anonymous: unauthorized, operator: adminOnlyOperator, admin: adminOk },
  { group: "admin-phase2", path: "/admin/phase2/operations/readiness", anonymous: unauthorized, operator: adminOnlyOperator, admin: adminOk },
  { group: "admin-phase2", path: "/admin/phase2/pilot-readiness", anonymous: unauthorized, operator: adminOnlyOperator, admin: adminOk },
  { group: "admin-widgets", path: "/admin/widgets/proof", anonymous: unauthorized, operator: adminOnlyOperator, admin: adminOk },
  { group: "admin-users", path: "/admin/users/configured-summary", anonymous: unauthorized, operator: adminOnlyOperator, admin: adminOk },
  { group: "demo-lifecycle", path: "/admin/demo-lifecycle/status", anonymous: unauthorized, operator: dataManagerOk, admin: adminOk },
  { group: "diagnostics", path: "/diagnostics/client-errors/summary", anonymous: unauthorized, operator: adminOnlyOperator, admin: adminOk },
  { group: "ml-readiness", path: "/api/ml/readiness", anonymous: unauthorized, operator: dataManagerOk, admin: adminOk },
  { group: "ml-foundation", path: "/api/ml/foundation/readiness", anonymous: unauthorized, operator: dataManagerOk, admin: adminOk },
  { group: "ml-foundation", path: "/api/ml/foundation/feature-definitions", anonymous: unauthorized, operator: dataManagerOk, admin: adminOk },
  { group: "ml-foundation", path: "/api/ml/foundation/outcomes", anonymous: unauthorized, operator: dataManagerOk, admin: adminOk }
];

async function login(userName: string, password: string): Promise<string> {
  const ctx = await request.newContext({ baseURL: apiBaseUrl });
  const response = await ctx.post("/auth/login", { data: { userName, password } });
  const bodyText = await response.text();
  expect(response.ok(), userName + " login failed. Status=" + response.status() + " Body=" + bodyText).toBeTruthy();

  const body = JSON.parse(bodyText) as { accessToken?: string; token?: string; jwt?: string; bearerToken?: string };
  const token = body.accessToken ?? body.token ?? body.jwt ?? body.bearerToken;
  expect(token, userName + " login must return an access token").toBeTruthy();

  await ctx.dispose();
  return token!;
}

function makeContext(token?: string): Promise<APIRequestContext> {
  return request.newContext({
    baseURL: apiBaseUrl,
    extraHTTPHeaders: token ? { Authorization: "Bearer " + token } : undefined
  });
}

async function getStatus(ctx: APIRequestContext, url: string): Promise<number> {
  const response = await ctx.get(url, { failOnStatusCode: false });
  return response.status();
}

function writeArtifacts(resultRows: Array<Record<string, unknown>>) {
  const outDir = path.resolve(process.cwd(), "test-results", "auth-matrix");
  fs.mkdirSync(outDir, { recursive: true });

  const now = new Date().toISOString();
  const jsonPath = path.join(outDir, "auth-matrix.json");
  const mdPath = path.join(outDir, "auth-matrix.md");

  fs.writeFileSync(jsonPath, JSON.stringify({ generatedAtUtc: now, apiBaseUrl, rows: resultRows }, null, 2));

  const lines = [
    "# PlantProcess IQ JWT Auth Matrix",
    "",
    "Generated: " + now,
    "API: " + apiBaseUrl,
    "",
    "| Group | Path | Anonymous | Operator | Admin | Result |",
    "|---|---|---:|---:|---:|---|"
  ];

  for (const row of resultRows) {
    lines.push("| " + row.group + " | " + row.path + " | " + row.anonymousStatus + " | " + row.operatorStatus + " | " + row.adminStatus + " | " + row.result + " |");
  }

  fs.writeFileSync(mdPath, lines.join("\n"));
  return { markdown: lines.join("\n") };
}

test.describe("PPIQ-T204 full JWT auth matrix", () => {
  test("anonymous, operator and admin route authorization is stable", async () => {
    test.skip(!adminPassword, "Set PPIQ_ADMIN_PASSWORD or PPIQ_SMOKE_PASSWORD.");
    test.skip(!operatorPassword, "Set PPIQ_OPERATOR_PASSWORD or PPIQ_DATAMANAGER_PASSWORD.");

    const adminToken = await login(adminUser, adminPassword);
    const operatorToken = await login(operatorUser, operatorPassword);

    const anonymous = await makeContext();
    const operator = await makeContext(operatorToken);
    const admin = await makeContext(adminToken);

    const resultRows: Array<Record<string, unknown>> = [];

    try {
      for (const row of rows) {
        const anonymousStatus = await getStatus(anonymous, row.path);
        const operatorStatus = await getStatus(operator, row.path);
        const adminStatus = await getStatus(admin, row.path);

        const result =
          row.anonymous.includes(anonymousStatus) &&
          row.operator.includes(operatorStatus) &&
          row.admin.includes(adminStatus)
            ? "PASS"
            : "FAIL";

        resultRows.push({
          group: row.group,
          path: row.path,
          anonymousStatus,
          operatorStatus,
          adminStatus,
          expectedAnonymous: row.anonymous,
          expectedOperator: row.operator,
          expectedAdmin: row.admin,
          result
        });

        expect(row.anonymous, row.group + " anonymous " + row.path).toContain(anonymousStatus);
        expect(row.operator, row.group + " operator " + row.path).toContain(operatorStatus);
        expect(row.admin, row.group + " admin " + row.path).toContain(adminStatus);
      }
    } finally {
      const artifacts = writeArtifacts(resultRows);
      await test.info().attach("auth-matrix.md", { body: artifacts.markdown, contentType: "text/markdown" });
      await anonymous.dispose();
      await operator.dispose();
      await admin.dispose();
    }
  });
});

import { expect, request, test } from "@playwright/test";

const apiBaseUrl = process.env.PPIQ_API_BASE_URL ?? process.env.VITE_API_BASE_URL ?? "http://localhost:5063";

const adminUser = process.env.PPIQ_ADMIN_USER ?? "admin";
const adminPassword = process.env.PPIQ_ADMIN_PASSWORD ?? process.env.PPIQ_SMOKE_PASSWORD ?? "";

const operatorUser = process.env.PPIQ_OPERATOR_USER ?? "datamanager";
const operatorPassword = process.env.PPIQ_OPERATOR_PASSWORD ?? process.env.PPIQ_DATAMANAGER_PASSWORD ?? "";

type Expected = readonly number[];

type MatrixRow = {
  area: string;
  path: string;
  anonymous: Expected;
  operator: Expected;
  admin: Expected;
};

const rows: MatrixRow[] = [
  { area: "Admin overview", path: "/admin/overview", anonymous: [401, 403], operator: [403], admin: [200] },
  { area: "Jobs monitor", path: "/admin/jobs-monitor/summary", anonymous: [401, 403], operator: [200], admin: [200] },
  { area: "Schema mapping catalog", path: "/admin/schema-mapping/catalog", anonymous: [401, 403], operator: [200], admin: [200] },
  { area: "Schema mapping readiness", path: "/admin/schema-mapping/readiness", anonymous: [401, 403], operator: [200], admin: [200] },
  { area: "Two-stage import readiness", path: "/admin/two-stage-import/readiness", anonymous: [401, 403, 404], operator: [200, 404], admin: [200, 404] },
  { area: "License current", path: "/admin/license/current", anonymous: [401, 403], operator: [200], admin: [200] },
  { area: "Phase 1 connector truth", path: "/admin/phase1/connector-truth", anonymous: [401, 403, 404], operator: [403, 404], admin: [200, 404] },
  { area: "Phase 2 operation readiness", path: "/admin/phase2/operations/readiness", anonymous: [401, 403, 404], operator: [403, 404], admin: [200, 404] },
  { area: "Diagnostics", path: "/diagnostics/client-errors/summary", anonymous: [401, 403, 404], operator: [403, 404], admin: [200, 404] },
  { area: "ML readiness", path: "/api/ml/readiness", anonymous: [401, 403, 404], operator: [200, 404], admin: [200, 404] },
  { area: "ML foundation readiness", path: "/api/ml/foundation/readiness", anonymous: [401, 403], operator: [200], admin: [200] },
  { area: "Demo lifecycle status", path: "/admin/demo-lifecycle/status", anonymous: [401, 403, 404], operator: [200, 404], admin: [200, 404] },
];

async function login(userName: string, password: string): Promise<string> {
  const ctx = await request.newContext({ baseURL: apiBaseUrl });
  const response = await ctx.post("/auth/login", {
    data: { userName, password },
  });

  expect(response.ok(), `${userName} login returned ${response.status()}`).toBeTruthy();

  const body = await response.json();
  const token = body.accessToken ?? body.token ?? body.jwt ?? body.bearerToken;
  expect(token, `${userName} login must return an access token`).toBeTruthy();

  await ctx.dispose();
  return token;
}

test.describe("PPIQ-T204 full JWT auth matrix", () => {
  test("anonymous, operator and admin identities match expected route authorization", async () => {
    test.skip(!adminPassword, "Set PPIQ_ADMIN_PASSWORD or PPIQ_SMOKE_PASSWORD.");
    test.skip(!operatorPassword, "Set PPIQ_OPERATOR_PASSWORD or PPIQ_DATAMANAGER_PASSWORD.");

    const now = new Date().toISOString().replace(/[:.]/g, "-");
    const matrix: string[] = [
      `# PlantProcess IQ Auth Matrix`,
      ``,
      `Generated: ${new Date().toISOString()}`,
      `API: ${apiBaseUrl}`,
      ``,
      `| Area | Path | Anonymous | Operator | Admin |`,
      `|---|---:|---:|---:|---:|`,
    ];

    const anonymous = await request.newContext({ baseURL: apiBaseUrl });

    const operatorToken = await login(operatorUser, operatorPassword);
    const operator = await request.newContext({
      baseURL: apiBaseUrl,
      extraHTTPHeaders: { Authorization: `Bearer ${operatorToken}` },
    });

    const adminToken = await login(adminUser, adminPassword);
    const admin = await request.newContext({
      baseURL: apiBaseUrl,
      extraHTTPHeaders: { Authorization: `Bearer ${adminToken}` },
    });

    for (const row of rows) {
      const a = await anonymous.get(row.path);
      const o = await operator.get(row.path);
      const ad = await admin.get(row.path);

      matrix.push(`| ${row.area} | \`${row.path}\` | ${a.status()} | ${o.status()} | ${ad.status()} |`);

      expect(row.anonymous, `${row.area} anonymous ${row.path}`).toContain(a.status());
      expect(row.operator, `${row.area} operator ${row.path}`).toContain(o.status());
      expect(row.admin, `${row.area} admin ${row.path}`).toContain(ad.status());
    }

    await anonymous.dispose();
    await operator.dispose();
    await admin.dispose();

    await test.info().attach(`auth-matrix-${now}.md`, {
      body: matrix.join("\n"),
      contentType: "text/markdown",
    });
  });
});
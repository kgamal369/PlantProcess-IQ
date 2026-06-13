import { expect, test, type APIRequestContext } from "@playwright/test";

// PPIQ-T25: each role sees a meaningfully different scope. Logs in as each TestMode-seeded role and
// asserts a role-discriminating capability grounded in src/security/phase9RoleAccess.ts:
//   PlantAdmin  -> admin configuration reachable
//   Executive / ProcessEngineer / Operator -> scoped OUT of admin configuration (locked or redirect)
// Behavioral assertion (no brittle nav-DOM selectors). Env-gated: skips if a role user isn't seeded.
const API = process.env.PLAYWRIGHT_API_URL || process.env.VITE_API_BASE_URL || "http://localhost:5063";

type RoleCase = { label: string; user: string; pass: string; adminAllowed: boolean };

const ROLES: RoleCase[] = [
  { label: "admin",     user: process.env.PPIQ_ROLE_ADMIN_USER    ?? "tm-admin",    pass: process.env.PPIQ_ROLE_ADMIN_PASS    ?? "TestMode-Admin-123!",    adminAllowed: true  },
  { label: "executive", user: process.env.PPIQ_ROLE_CEO_USER      ?? "tm-ceo",      pass: process.env.PPIQ_ROLE_CEO_PASS      ?? "TestMode-Ceo-123!",      adminAllowed: false },
  { label: "engineer",  user: process.env.PPIQ_ROLE_ENGINEER_USER ?? "tm-engineer", pass: process.env.PPIQ_ROLE_ENGINEER_PASS ?? "TestMode-Engineer-123!", adminAllowed: false },
  { label: "operator",  user: process.env.PPIQ_ROLE_OPERATOR_USER ?? "tm-operator", pass: process.env.PPIQ_ROLE_OPERATOR_PASS ?? "TestMode-Operator-123!", adminAllowed: false },
];

async function loginAsRole(request: APIRequestContext, user: string, pass: string): Promise<boolean> {
  const resp = await request
    .post(`${API}/auth/login`, { data: { userName: user, password: pass } })
    .catch(() => null);
  return !!resp && resp.ok();
}

test.describe("PPIQ-T25 role-scoped views", () => {
  for (const role of ROLES) {
    test(`role ${role.label}: admin configuration is ${role.adminAllowed ? "reachable" : "scoped out"}`, async ({ page }) => {
      // Login through the PAGE's request context so the auth cookie belongs to the page.
      const ok = await loginAsRole(page.context().request, role.user, role.pass);
      test.skip(!ok, `role user ${role.user} not seeded - enable TestMode SeedUsers to run this role`);

      await page.addInitScript((baseUrl) => {
        localStorage.setItem("ppiq-demo-mode", "true");
        localStorage.setItem("VITE_API_BASE_URL", baseUrl as string);
      }, API);

      await page.goto("/admin", { waitUntil: "domcontentloaded" }).catch(() => {});
      await page.waitForLoadState("networkidle").catch(() => {});

      const body = (await page.locator("body").innerText().catch(() => "")) ?? "";
      const scopedOut =
        /locked|not available|no access|insufficient|unauthor|forbidden|request a higher tier/i.test(body) ||
        !page.url().includes("/admin");

      if (role.adminAllowed) {
        expect(scopedOut, `${role.label} must reach admin configuration`).toBeFalsy();
      } else {
        expect(scopedOut, `${role.label} must be scoped OUT of admin configuration (locked overlay or redirect)`).toBeTruthy();
      }
    });
  }
});

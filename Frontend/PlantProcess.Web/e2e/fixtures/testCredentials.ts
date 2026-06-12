// PPIQ-T04: single source of e2e credentials + base URL. Specs import from here ONLY -
// never hardcode users, passwords, or ports in a spec again.
// Defaults match the PPIQ-T022 test-mode profile / integration-test admin.
export const E2E = {
  baseUrl: process.env.PPIQ_E2E_BASE_URL ?? "http://localhost:5063",
  admin: {
    user: process.env.PPIQ_E2E_USER ?? "admin",
    pass: process.env.PPIQ_E2E_PASS ?? "ChangeMe123!",
  },
  // T22 seeded role users (when PPIQ_TESTMODE__SeedUsers=true):
  ceo:      { user: "tm-ceo",      pass: "TestMode-Ceo-123!" },
  engineer: { user: "tm-engineer", pass: "TestMode-Engineer-123!" },
  operator: { user: "tm-operator", pass: "TestMode-Operator-123!" },
} as const;
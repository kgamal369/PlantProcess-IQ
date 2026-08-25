// ============================================================================
// Customer route invariant - dedicated Playwright configuration.
//
// Backlog origin: T-203   Release: M2   Owner: Worker 2 (Release Truth)
//
// Deterministic by construction: one worker, no retries, declared timeouts,
// machine-readable JSON alongside the gate's own evidence file. It inherits the
// base configuration so credentials, webServer and baseURL stay in one place;
// no parallel credential convention is introduced.
// ============================================================================

import { defineConfig } from "@playwright/test";
import base from "./playwright.config";

export default defineConfig({
  ...base,
  globalSetup: "./release-truth.globalSetup.ts",
  testDir: "./e2e/release-truth",
  testMatch: /customer-route-invariant\.spec\.ts/,
  fullyParallel: false,
  workers: 1,
  retries: 0,
  timeout: 120_000,
  expect: { timeout: 20_000 },
  reporter: [
    ["line"],
    ["json", { outputFile: "reports/release-truth/customer_route_invariant.playwright.json" }],
  ],
});
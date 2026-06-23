import { test, expect, request as playwrightRequest } from "@playwright/test";
import { readFileSync } from "node:fs";
import path from "node:path";
import { login, apiBaseUrl } from "./helpers/auth";

// Runs as a Playwright "setup" project (a dependency of the functional projects), so it
// executes AFTER the webServer is up. Activates the committed, signed Enterprise Ed25519
// license token through the product's REAL activation endpoint - the exact artifact and
// endpoint the deploy pipeline's presentation-defaults stage uses. This writes
// public.ppiq_ed25519_activated_licenses, the single source of truth the license service
// reads (GetCurrentTier -> ppiq_v_ed25519_current_entitlements), unlocking Pro/Enterprise
// demo features exactly as a licensed customer environment would. Not a tier backdoor.
test("activate the signed Enterprise Ed25519 license for the default tenant", async () => {
  const tokenPath = path.resolve(process.cwd(), "../../deploy/fixtures/license/enterprise.token");
  const licenseJws = readFileSync(tokenPath, "utf8").trim();
  expect(licenseJws.split(".").length, "enterprise.token must be a compact JWS (header.payload.signature)").toBe(3);

  const ctx = await playwrightRequest.newContext();
  try {
    const token = await login(ctx);
    const headers = {
      Authorization: `Bearer ${token}`,
      "Content-Type": "application/json",
    };

    const res = await ctx.post(`${apiBaseUrl}/api/v5/licensing/ed25519/activate`, {
      headers,
      data: { licenseJws, expectedInstanceId: null },
    });
    expect(res.ok(), `ed25519/activate HTTP ${res.status()}: ${await res.text()}`).toBeTruthy();
    const result = await res.json();
    expect(result.activated, `activation response: ${JSON.stringify(result)}`).toBeTruthy();
  } finally {
    await ctx.dispose();
  }
});
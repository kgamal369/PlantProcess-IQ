import { defineConfig, devices } from "@playwright/test";

/* PPIQ-T069-W3. Runs against the dev server you already have on 5180.
 * It deliberately does NOT start its own server - starting a second one would
 * verify a different build from the one on your screen. */
export default defineConfig({
  testDir: "./tests/verify",
  timeout: 30000,
  retries: 0,
  reporter: [["list"]],
  outputDir: "./test-results/verify",
  use: {
    baseURL: process.env.PPIQ_VERIFY_URL || "http://localhost:5180",
    screenshot: "off",
    trace: "off",
  },
  projects: [{ name: "chromium", use: { ...devices["Desktop Chrome"] } }],
});
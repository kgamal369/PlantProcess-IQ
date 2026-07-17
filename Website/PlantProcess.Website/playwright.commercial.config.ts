import { defineConfig, devices } from "@playwright/test";

const evidenceDir = process.env.PPIQ_COMMERCIAL_EVIDENCE_DIR || "test-results/commercial-v2";
const executablePath = process.env.PPIQ_CHROMIUM_EXECUTABLE || undefined;
const port = Number(process.env.PPIQ_COMMERCIAL_PORT || "4173");
const baseURL = `http://127.0.0.1:${port}`;

export default defineConfig({
  testDir: "./tests/e2e",
  testMatch: /commercial-v2\.spec\.ts/,
  fullyParallel: false,
  workers: 1,
  timeout: 45_000,
  expect: { timeout: 8_000 },
  outputDir: `${evidenceDir}/artifacts`,
  reporter: [
    ["line"],
    ["html", { outputFolder: `${evidenceDir}/html`, open: "never" }],
    ["json", { outputFile: `${evidenceDir}/playwright.json` }],
  ],
  use: {
    baseURL,
    trace: "retain-on-failure",
    screenshot: "only-on-failure",
    video: "off",
    colorScheme: "dark",
    launchOptions: executablePath ? { executablePath } : undefined,
  },
  projects: [
    { name: "chromium", use: { ...devices["Desktop Chrome"], viewport: { width: 1440, height: 1000 } } },
  ],
  webServer: {
    command: `npm run preview -- --host 127.0.0.1 --port ${port}`,
    url: baseURL,
    timeout: 120_000,
    reuseExistingServer: false,
  },
});

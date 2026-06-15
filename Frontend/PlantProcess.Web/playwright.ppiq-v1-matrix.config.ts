import { defineConfig, devices, type PlaywrightTestConfig } from "@playwright/test";
import baseConfig from "./playwright.config";

const httpBase = process.env.PPIQ_APP_HTTP_URL || "http://127.0.0.1:5173";
const httpsBase = process.env.PPIQ_APP_HTTPS_URL || "";
const viewports = [
  { key: "375", viewport: { width: 375, height: 800 } },
  { key: "768", viewport: { width: 768, height: 1024 } },
  { key: "1440", viewport: { width: 1440, height: 900 } },
];
const engines = [
  { key: "chromium", device: devices["Desktop Chrome"] },
  { key: "firefox", device: devices["Desktop Firefox"] },
  { key: "webkit", device: devices["Desktop Safari"] },
];
const protocols = [
  { key: "http", baseURL: httpBase },
  { key: "https", baseURL: httpsBase },
];

const projects: NonNullable<PlaywrightTestConfig["projects"]> = [];
for (const engine of engines) {
  for (const size of viewports) {
    for (const protocol of protocols) {
      projects.push({
        name: `${engine.key}-${size.key}-${protocol.key}`,
        use: {
          ...engine.device,
          browserName: engine.key as "chromium" | "firefox" | "webkit",
          viewport: size.viewport,
          baseURL: protocol.baseURL,
          ignoreHTTPSErrors: false,
        },
      });
    }
  }
}

const inheritedServers = Array.isArray(baseConfig.webServer)
  ? baseConfig.webServer
  : baseConfig.webServer
    ? [baseConfig.webServer]
    : [];

export default defineConfig({
  ...baseConfig,
  webServer: [
    ...inheritedServers,
    {
      command: "npm run dev -- --host 127.0.0.1 --port 4174",
      cwd: "../../Website/PlantProcess.Website",
      url: process.env.PPIQ_WEB_HTTP_URL || "http://127.0.0.1:4174",
      reuseExistingServer: true,
      timeout: 120_000,
    },
  ],
  testDir: "./e2e",
  testMatch: /ppiq-v1-cross-browser-matrix\.spec\.ts/,
  fullyParallel: false,
  retries: 0,
  reporter: [["list"], ["html", { open: "never", outputFolder: "playwright-report-cross-browser" }]],
  projects,
});
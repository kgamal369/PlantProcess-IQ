// SOU-COMMERCIAL-CERTIFICATION-V3
import { expect, test, type Page } from "@playwright/test";
import fs from "node:fs";
import path from "node:path";

const evidenceDir =
  process.env.PPIQ_COMMERCIAL_EVIDENCE_DIR || "test-results/commercial-v2";
const port = Number(process.env.PPIQ_COMMERCIAL_PORT || "4173");
const browserOrigin = `http://127.0.0.1:${port}`;

const polishedRoutes = [
  ["home", "/"],
  ["products", "/products"],
  ["ppiq", "/products/plantprocess-iq"],
  ["mes", "/products/mes"],
  ["qes", "/products/qes"],
  ["yard", "/products/yard-warehouse-management"],
  ["energy", "/products/energy-management"],
  ["proof", "/proof"],
  ["security", "/security"],
  ["pricing", "/pricing"],
  ["about", "/about"],
  ["contact", "/contact"],
] as const;

const products = [
  ["/products/plantprocess-iq", /PlantProcess IQ/i],
  ["/products/mes", /Manufacturing Execution System/i],
  ["/products/qes", /Quality Execution System/i],
  ["/products/yard-warehouse-management", /Yard (?:and|&) Warehouse Management/i],
  ["/products/energy-management", /Energy Management System/i],
] as const;

async function assertProfessionalPage(page: Page) {
  await expect(page.locator("h1")).toHaveCount(1);
  await expect(page.locator("h1")).toBeVisible();
  await expect(page.locator("header")).toBeVisible();
  await expect(page.locator("footer")).toHaveCount(1);

  const metrics = await page.evaluate(() => ({
    scrollWidth: document.documentElement.scrollWidth,
    clientWidth: document.documentElement.clientWidth,
    bodyScrollWidth: document.body.scrollWidth,
    unnamedButtons: [...document.querySelectorAll("button")].filter(
      (button) => !(button.textContent?.trim() || button.getAttribute("aria-label")),
    ).length,
    emptyLinks: [...document.querySelectorAll("a")].filter(
      (link) => !(link.textContent?.trim() || link.getAttribute("aria-label")),
    ).length,
  }));

  expect(metrics.scrollWidth).toBeLessThanOrEqual(metrics.clientWidth + 2);
  expect(metrics.bodyScrollWidth).toBeLessThanOrEqual(metrics.clientWidth + 2);
  expect(metrics.unnamedButtons).toBe(0);
  expect(metrics.emptyLinks).toBe(0);
}

test.beforeAll(() => {
  fs.mkdirSync(path.resolve(evidenceDir, "screenshots"), { recursive: true });
});

test("corporate root tells the SOU company story", async ({ page }, testInfo) => {
  const consoleErrors: string[] = [];
  page.on("console", (message) => {
    if (
      message.type() === "error" &&
      !message.text().includes("404") &&
      !message.text().includes("favicon")
    ) {
      consoleErrors.push(message.text());
    }
  });

  await page.goto("/", { waitUntil: "domcontentloaded" });
  await assertProfessionalPage(page);

  await expect(page).toHaveTitle(/SOU Industrial Software/i);
  await expect(page.locator("body")).toContainText("SOU Industrial Software");
  await expect(page.locator("body")).toContainText("PlantProcess IQ");
  await expect(page.locator("body")).toContainText(/five|5/i);
  await expect(page.locator("body")).not.toContainText("info@plantprocessiq.com");

  expect(consoleErrors).toEqual([]);

  await page.screenshot({
    path: path.resolve(
      evidenceDir,
      "screenshots",
      `home-${testInfo.project.name}.png`,
    ),
    fullPage: true,
    animations: "disabled",
  });
});

test("five sibling products keep canonical routes and identity", async ({ page }) => {
  await page.goto("/products", { waitUntil: "domcontentloaded" });
  await assertProfessionalPage(page);

  for (const [productPath] of products) {
    expect(await page.locator(`a[href="${productPath}"]`).count()).toBeGreaterThan(0);
  }

  for (const [productPath, productName] of products) {
    await page.goto(productPath, { waitUntil: "domcontentloaded" });
    await assertProfessionalPage(page);

    expect(new URL(page.url()).pathname).toBe(productPath);
    await expect(page.locator("body")).toContainText(productName);
  }

  await page.goto("/product", { waitUntil: "domcontentloaded" });
  await expect(page).toHaveURL(/\/products\/plantprocess-iq\/?$/);
});

test("all current commercial routes are polished on desktop and mobile", async ({
  page,
}, testInfo) => {
  const viewports = [
    { name: "desktop", width: 1440, height: 1000 },
    { name: "mobile", width: 412, height: 915 },
  ];

  for (const viewport of viewports) {
    await page.setViewportSize({
      width: viewport.width,
      height: viewport.height,
    });

    for (const [name, route] of polishedRoutes) {
      await page.goto(route, { waitUntil: "domcontentloaded" });
      await assertProfessionalPage(page);

      if (["products", "about"].includes(name)) {
        await page.screenshot({
          path: path.resolve(
            evidenceDir,
            "screenshots",
            `${name}-${viewport.name}-${testInfo.project.name}.png`,
          ),
          fullPage: true,
          animations: "disabled",
        });
      }
    }
  }
});

test("about page carries approved founder, experience and D\u00FCsseldorf spelling", async ({
  page,
}) => {
  await page.goto("/about", { waitUntil: "domcontentloaded" });
  await assertProfessionalPage(page);

  const body = page.locator("body");
  await expect(body).toContainText("Karim Gamal");
  await expect(body).toContainText(/14\s*(?:\+?\s*)?years?/i);
  await expect(body).toContainText(/D\u00FCsseldorf/i);
  await expect(body).not.toContainText(/Duesseldorf|DÃ¼sseldorf|13\+\s*years/i);
});

test("pricing and contact use the approved public commercial policy", async ({ page }) => {
  await page.goto("/pricing", { waitUntil: "domcontentloaded" });
  await assertProfessionalPage(page);

  const pricingText = await page.locator("body").innerText();
  expect(pricingText).not.toMatch(/\$(?:12k|50k|6k|25k)\b/i);

  await page.goto("/contact", { waitUntil: "domcontentloaded" });
  await assertProfessionalPage(page);

  await expect(page.locator("body")).toContainText("info@souindustrial.com");
});

test("lead capture posts to the backend contract and renders success", async ({
  page,
}) => {
  await page.route("**/api/v5/outbound/leads", async (route) => {
    const request = route.request();

    if (request.method() === "OPTIONS") {
      await route.fulfill({
        status: 204,
        headers: {
          "access-control-allow-origin": browserOrigin,
          "access-control-allow-credentials": "true",
          "access-control-allow-methods": "POST, OPTIONS",
          "access-control-allow-headers": "content-type",
        },
      });
      return;
    }

    expect(request.method()).toBe("POST");
    const payload = request.postDataJSON();
    expect(payload.companyName).toBe("SOU E2E Customer");
    expect(payload.consentGiven).toBe(true);
    expect(payload.honeypot).toBe("");

    await route.fulfill({
      status: 200,
      headers: {
        "content-type": "application/json",
        "access-control-allow-origin": browserOrigin,
        "access-control-allow-credentials": "true",
      },
      body: JSON.stringify({
        leadId: "e2e-commercial-001",
        status: "new",
        fitScore: 0.84,
        notificationQueued: true,
      }),
    });
  });

  await page.goto("/contact", { waitUntil: "domcontentloaded" });

  const form = page.getByTestId("demo-request-form");
  await expect(form).toBeVisible();

  await page.getByLabel("Your name").fill("Commercial E2E");
  await page.getByLabel("Company").fill("SOU E2E Customer");
  await page.getByLabel("Work email").fill("commercial.e2e@example.com");
  await page.getByLabel("Role").fill("Plant Manager");
  await page.getByLabel("Plant / industry type").fill("Manufacturing plant");
  await page.getByLabel("Source systems").fill("MES, QMS, SQL Server");
  await page
    .getByLabel("Main quality / process pain")
    .fill("Recurring quality losses across fragmented systems");
  await page.getByLabel(/I agree to be contacted/i).check();

  await page
    .getByRole("button", {
      name: /Capture lead|Request the live demo|Submit/i,
    })
    .click();

  await expect(page.getByTestId("lead-capture-success")).toBeVisible();
  await expect(page.getByTestId("lead-capture-success")).toContainText(
    /Lead captured/i,
  );
});
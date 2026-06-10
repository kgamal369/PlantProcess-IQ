
import { expect, test } from "@playwright/test";

test.describe("P3-T14 Value/ROI executive surface", () => {
  test("renders engine Low/Mid/High, provenance, report button, and abstain proof", async ({ page }) => {
    await page.route("**/api/value/cost-assumptions", async (route) => {
      await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ ok: true }) });
    });

    await page.route("**/api/value/impact", async (route) => {
      const body = JSON.stringify({
        Currency: "EUR",
        Low: 28000,
        Mid: 42000,
        High: 56000,
        Expected: 42000,
        IsAbstained: false,
        AssumptionVersion: 7,
        SupportStatus: "BoundedRange",
        HonestyCaveat: "Projected bounded opportunity only; every figure is tied to assumptions, inputs, and provenance.",
        Terms: [
          {
            Name: "Downgrade impact",
            InputsJson: "{\"affectedTons\":200,\"monthlyVolumeTons\":10000,\"defectRateDelta\":0.02}",
            Low: 28000,
            Mid: 42000,
            High: 56000,
            Handle: { Handle: "prov:value:edge-crack:001" },
          },
        ],
      });

      await route.fulfill({ status: 200, contentType: "application/json", body });
    });

    await page.goto("/value/executive");
    await page.getByRole("button", { name: /run approved finding/i }).click();

    await expect(page.getByTestId("p3-t14-low")).toContainText(/28,000|28\s000/);
    await expect(page.getByTestId("p3-t14-mid")).toContainText(/42,000|42\s000/);
    await expect(page.getByTestId("p3-t14-high")).toContainText(/56,000|56\s000/);
    await expect(page.getByText("prov:value:edge-crack:001")).toBeVisible();
    await expect(page.getByRole("button", { name: /monthly value report pdf/i })).toBeEnabled();

    await expect(page.locator("body")).not.toContainText(/guaranteed|will save/i);
  });
});

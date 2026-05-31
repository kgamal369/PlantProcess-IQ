const fs = require("node:fs");
const path = require("node:path");

const file = path.join(
  process.cwd(),
  "Frontend",
  "PlantProcess.Web",
  "e2e",
  "a11y",
  "phase56-accessibility.spec.ts"
);

const content = `import { expect, test } from "@playwright/test";
import { prepareAuthenticatedPage } from "../helpers/hardening";

const routes = [
  "/dashboard",
  "/materials",
  "/risk",
  "/data-quality",
  "/correlations",
  "/ml-readiness",
  "/demo-lifecycle",
  "/admin-preview",
  "/admin",
  "/brand",
];

const expectedPageText =
  /plantprocess iq|dashboard|material|risk|quality|correlation|ml|readiness|demo|admin|brand/i;

test.describe("PPIQ Phase 6 accessibility smoke", () => {
  for (const route of routes) {
    test("PPIQ Phase 6 accessibility smoke " + route, async ({ page, request }) => {
      await prepareAuthenticatedPage(page, request);

      await page.goto(route, {
        waitUntil: "domcontentloaded",
        timeout: 30_000,
      });

      await page.waitForLoadState("networkidle", { timeout: 8_000 }).catch(() => {
        // Background polling/retries should not fail an accessibility smoke alone.
      });

      const body = page.locator("body");

      await expect(body).toBeVisible({ timeout: 30_000 });
      await expect(body).toContainText(expectedPageText, { timeout: 30_000 });

      const bodyText = (await body.innerText()).toLowerCase();

      expect(bodyText).not.toContain("could not be loaded");
      expect(bodyText).not.toContain("could not load");
      expect(bodyText).not.toContain("cannot read properties");
      expect(bodyText).not.toContain("is not a function");
      expect(bodyText).not.toContain("uncaught");
      expect(bodyText).not.toContain("stack trace");
      expect(bodyText).not.toContain("missing-smoke-password");

      const missingButtonNames = await page.locator("button").evaluateAll((buttons) => {
        const isVisibleElement = (element: Element): boolean => {
          const htmlElement = element as HTMLElement;
          const style = window.getComputedStyle(htmlElement);

          return (
            style.display !== "none" &&
            style.visibility !== "hidden" &&
            htmlElement.getClientRects().length > 0
          );
        };

        const accessibleName = (element: Element): string => {
          const ariaLabel = element.getAttribute("aria-label")?.trim();
          if (ariaLabel) return ariaLabel;

          const title = element.getAttribute("title")?.trim();
          if (title) return title;

          const labelledBy = element.getAttribute("aria-labelledby")?.trim();
          if (labelledBy) {
            const labelText = labelledBy
              .split(/\\s+/)
              .map((id) => document.getElementById(id)?.textContent?.trim() ?? "")
              .filter(Boolean)
              .join(" ")
              .trim();

            if (labelText) return labelText;
          }

          return element.textContent?.trim() ?? "";
        };

        return buttons
          .filter((button) => isVisibleElement(button))
          .filter((button) => !accessibleName(button))
          .map((button) => button.outerHTML);
      });

      const missingInputs = await page
        .locator("input, select, textarea")
        .evaluateAll((controls) => {
          const isVisibleElement = (element: Element): boolean => {
            const htmlElement = element as HTMLElement;
            const style = window.getComputedStyle(htmlElement);

            return (
              style.display !== "none" &&
              style.visibility !== "hidden" &&
              htmlElement.getClientRects().length > 0
            );
          };

          const isHiddenInput = (element: Element): boolean => {
            return element instanceof HTMLInputElement && element.type === "hidden";
          };

          const accessibleName = (element: Element): string => {
            const ariaLabel = element.getAttribute("aria-label")?.trim();
            if (ariaLabel) return ariaLabel;

            const title = element.getAttribute("title")?.trim();
            if (title) return title;

            const labelledBy = element.getAttribute("aria-labelledby")?.trim();
            if (labelledBy) {
              const labelText = labelledBy
                .split(/\\s+/)
                .map((id) => document.getElementById(id)?.textContent?.trim() ?? "")
                .filter(Boolean)
                .join(" ")
                .trim();

              if (labelText) return labelText;
            }

            return "";
          };

          const hasControlAccessibleName = (element: Element): boolean => {
            if (accessibleName(element)) {
              return true;
            }

            const id = element.getAttribute("id");
            if (id) {
              const hasLabelFor = Array.from(document.querySelectorAll("label")).some(
                (label) => label.getAttribute("for") === id
              );

              if (hasLabelFor) {
                return true;
              }
            }

            return !!element.closest("label");
          };

          return controls
            .filter((control) => isVisibleElement(control))
            .filter((control) => !isHiddenInput(control))
            .filter((control) => !hasControlAccessibleName(control))
            .map((control) => control.outerHTML);
        });

      expect(missingButtonNames).toEqual([]);
      expect(missingInputs).toEqual([]);
    });
  }
});
`;

fs.writeFileSync(file, content.replace(/\r\n/g, "\n"), "utf8");

console.log("Fixed phase56-accessibility.spec.ts: evaluateAll callbacks are now browser-context-safe.");

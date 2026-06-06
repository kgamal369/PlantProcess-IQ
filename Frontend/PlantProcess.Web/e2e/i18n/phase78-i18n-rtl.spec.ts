import { expect, test } from "@playwright/test";

const routes = ["/", "/dashboard", "/materials", "/ml-readiness", "/i18n-rtl"];

for (const theme of ["dark", "light"] as const) {
  for (const locale of ["en", "ar"] as const) {
    test.describe(theme + " " + locale, () => {
      for (const route of routes) {
        test(route + " keeps locale, dir and mobile shell", async ({ page }) => {
          await page.addInitScript(({ themeName, localeName }) => {
            window.localStorage.setItem("plantprocess.theme.v1", themeName);
            window.localStorage.setItem("plantprocess.locale.v1", localeName);
          }, { themeName: theme, localeName: locale });
          await page.setViewportSize({ width: 390, height: 844 });
          await page.goto(route);
          await expect(page.locator("html")).toHaveAttribute("lang", locale);
          await expect(page.locator("html")).toHaveAttribute("dir", locale === "ar" ? "rtl" : "ltr");
          await expect(page.locator("#ppiq-phase78-language-toggle")).toBeAttached();
          const overflow = await page.evaluate(() => document.documentElement.scrollWidth > document.documentElement.clientWidth + 2);
          expect(overflow).toBeFalsy();
        });
      }
    });
  }
}

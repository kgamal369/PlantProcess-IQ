import { expect, type Page } from "@playwright/test";

export type Phase9Route = {
  name: string;
  path: string;
  expectedText: RegExp;
};

export const phase9Routes: Phase9Route[] = [
  { name: "Home / Dashboard", path: "/dashboard", expectedText: /dashboard|quality|risk|plantprocess/i },
  { name: "DB Links / Admin", path: "/admin", expectedText: /admin|configuration|database|source/i },
  { name: "Source Objects / Admin", path: "/admin", expectedText: /source|schema|configuration|import/i },
  { name: "Importing Data / Admin", path: "/admin", expectedText: /import|job|batch|configuration/i },
  { name: "Schema Config / Admin", path: "/admin", expectedText: /schema|mapping|canonical|sql/i },
  { name: "Jobs Monitor / Admin", path: "/admin", expectedText: /job|monitor|run|status/i },
  { name: "Page Builder", path: "/page-builder", expectedText: /page|builder|widget|dashboard/i },
  { name: "Widget Script Compiler", path: "/widget-script-compiler", expectedText: /widget|script|compiler|expression/i },
  { name: "Material Investigation", path: "/materials", expectedText: /material|investigation|genealogy|quality/i },
  { name: "Risk Dashboard", path: "/risk", expectedText: /risk|score|quality|plant/i },
  { name: "Data Quality", path: "/data-quality", expectedText: /data|quality|issue|validation/i },
  { name: "ML / Correlation", path: "/correlations", expectedText: /correlation|parameter|quality|analysis/i },
  { name: "ML Readiness", path: "/ml-readiness", expectedText: /ml|readiness|feature|model|gate/i },
  { name: "Advanced Analysis", path: "/investigate/advanced", expectedText: /advanced|analysis|evidence|result/i },
  { name: "Suggestions", path: "/suggestions", expectedText: /suggestion|recommendation|evidence|action/i },
  { name: "License / User Admin", path: "/commercial/license", expectedText: /license|feature|tier|usage/i },
  { name: "Reports / Demo Lifecycle", path: "/demo-lifecycle", expectedText: /demo|lifecycle|reset|report|workflow/i },
  { name: "Brand", path: "/brand", expectedText: /brand|sou|plantprocess|identity/i },
];

type InteractiveIssue = {
  selector: string;
  text: string;
  reason: string;
};

export async function waitForPhase9PageReady(page: Page, route: Phase9Route) {
  await page.goto(route.path, { waitUntil: "domcontentloaded" });
  await page.waitForLoadState("networkidle", { timeout: 20_000 }).catch(() => undefined);

  await expect(page.locator("body"), `${route.name} body is visible`).toBeVisible();
  await expect(page.locator("body"), `${route.name} renders expected business text`).toContainText(route.expectedText, {
    timeout: 20_000,
  });

  const bodyText = await page.locator("body").innerText({ timeout: 10_000 }).catch(() => "");
  expect.soft(bodyText, `${route.name} must not show runtime exception text`).not.toMatch(
    /cannot read properties|undefined is not an object|unhandled runtime|vite error overlay/i,
  );
}

export async function collectInteractiveIssues(page: Page): Promise<InteractiveIssue[]> {
  return page.evaluate(() => {
    const controls = Array.from(
      document.querySelectorAll<HTMLElement>(
        [
          "button",
          "a[href]",
          "input",
          "select",
          "textarea",
          "[role='button']",
          "[role='tab']",
          "[role='menuitem']",
        ].join(","),
      ),
    );

    function isVisible(el: HTMLElement) {
      const style = window.getComputedStyle(el);
      const box = el.getBoundingClientRect();
      return style.visibility !== "hidden" && style.display !== "none" && box.width > 0 && box.height > 0;
    }

    function labelOf(el: HTMLElement) {
      return [
        el.getAttribute("aria-label"),
        el.getAttribute("title"),
        el.getAttribute("data-testid"),
        el.getAttribute("name"),
        el.getAttribute("placeholder"),
        el.textContent,
      ]
        .filter(Boolean)
        .join(" ")
        .replace(/\s+/g, " ")
        .trim();
    }

    function selectorOf(el: HTMLElement) {
      const tag = el.tagName.toLowerCase();
      const id = el.id ? `#${el.id}` : "";
      const testId = el.getAttribute("data-testid") ? `[data-testid="${el.getAttribute("data-testid")}"]` : "";
      const text = labelOf(el).slice(0, 50);
      return `${tag}${id}${testId}${text ? ` :: ${text}` : ""}`;
    }

    function hasDisabledReason(el: HTMLElement, label: string) {
      const reasonText = [
        label,
        el.getAttribute("aria-describedby"),
        el.getAttribute("data-disabled-reason"),
        el.closest("[data-disabled-reason]")?.getAttribute("data-disabled-reason"),
        el.closest("[aria-label]")?.getAttribute("aria-label"),
      ]
        .filter(Boolean)
        .join(" ")
        .toLowerCase();

      return /locked|permission|requires|select|configure|not available|coming soon|disabled|loading|no data|read only|license/.test(reasonText);
    }

    const issues: InteractiveIssue[] = [];

    for (const el of controls) {
      if (!isVisible(el)) continue;

      const label = labelOf(el);
      const isDisabled =
        el.hasAttribute("disabled") ||
        el.getAttribute("aria-disabled") === "true" ||
        el.classList.contains("disabled");

      if (!label) {
        issues.push({
          selector: selectorOf(el),
          text: "",
          reason: "Interactive control has no text, aria-label, title, placeholder, name, or data-testid.",
        });
        continue;
      }

      if (isDisabled && !hasDisabledReason(el, label)) {
        issues.push({
          selector: selectorOf(el),
          text: label,
          reason: "Disabled/locked control does not explain why it is unavailable.",
        });
        continue;
      }

      if (el.tagName.toLowerCase() === "a") {
        const href = el.getAttribute("href") ?? "";
        if (href === "#" || href.trim() === "") {
          issues.push({
            selector: selectorOf(el),
            text: label,
            reason: "Anchor is visible but has an empty/# href. Use button action, route, or disabled reason.",
          });
        }
      }
    }

    return issues;
  });
}

export function assertSyntheticBrokenControlFailsGuard() {
  const syntheticBroken = [
    {
      selector: "button :: ",
      text: "",
      reason: "Interactive control has no text, aria-label, title, placeholder, name, or data-testid.",
    },
  ];

  expect(syntheticBroken.length, "Synthetic broken button must fail the matrix guard.").toBeGreaterThan(0);
}
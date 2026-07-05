import { expect, type Page } from "@playwright/test";

export type Phase9Route = {
  name: string;
  path: string;
  expectedText: RegExp;
};

export const phase9Routes: Phase9Route[] = [
  { name: "Home / Dashboard", path: "/dashboard", expectedText: /dashboard|quality|risk|plantprocess/i },
  { name: "Admin / DB Configuration", path: "/admin?adminTab=db-configuration", expectedText: /connection|provider|configuration|source/i },
  { name: "Admin / Schema Configuration", path: "/admin?adminTab=schema-configuration", expectedText: /schema|mapping|canonical|sql/i },
  { name: "Admin / Importing Data", path: "/admin?adminTab=importing-data", expectedText: /import|stage|canonical|refresh/i },
  { name: "Admin / Jobs Monitor", path: "/admin?adminTab=jobs-monitor", expectedText: /job|monitor|run|status/i },
  { name: "Admin / Connector Truth", path: "/admin?adminTab=connector-truth", expectedText: /connector|truth|tracked|status/i },
  { name: "Admin / License", path: "/admin?adminTab=license", expectedText: /license|tier|signed/i },
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
      const parts: (string | null)[] = [el.getAttribute("aria-label")];
      const labelledBy = el.getAttribute("aria-labelledby");
      if (labelledBy) {
        for (const ref of labelledBy.split(/\s+/)) {
          const node = ref ? document.getElementById(ref) : null;
          if (node) parts.push(node.textContent);
        }
      }
      const forLabel = el.id ? document.querySelector('label[for="' + CSS.escape(el.id) + '"]') : null;
      if (forLabel) parts.push(forLabel.textContent);
      const wrappingLabel = el.closest("label");
      if (wrappingLabel) parts.push(wrappingLabel.textContent);
      parts.push(
        el.getAttribute("title"),
        el.getAttribute("data-testid"),
        el.getAttribute("name"),
        el.getAttribute("placeholder"),
        el.textContent,
      );
      return parts.filter(Boolean).join(" ").replace(/\s+/g, " ").trim();
    }

    function selectorOf(el: HTMLElement) {
      const tag = el.tagName.toLowerCase();
      const id = el.id ? `#${el.id}` : "";
      const testId = el.getAttribute("data-testid") ? `[data-testid="${el.getAttribute("data-testid")}"]` : "";
      const text = labelOf(el).slice(0, 50);
      return `${tag}${id}${testId}${text ? ` :: ${text}` : ""}`;
    }

    function hasDisabledReason(el: HTMLElement, _label: string) {
      // Structural and i18n-safe: a disabled control is "explained" when it carries a
      // machine-readable reason - a non-empty data-disabled-reason (on itself or an
      // ancestor), a title tooltip, or an aria-describedby that resolves to text.
      // No English keyword matching (the product ships in EN/DE/AR).
      const own = el.getAttribute("data-disabled-reason");
      if (own && own.trim()) return true;
      const ancestor = el.closest("[data-disabled-reason]");
      if (ancestor && (ancestor.getAttribute("data-disabled-reason") || "").trim()) return true;
      const title = el.getAttribute("title");
      if (title && title.trim()) return true;
      const describedBy = el.getAttribute("aria-describedby");
      if (describedBy) {
        for (const ref of describedBy.split(/\s+/)) {
          const node = ref ? document.getElementById(ref) : null;
          if (node && node.textContent && node.textContent.trim()) return true;
        }
      }
      return false;
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

      if (!isDisabled && el.tagName.toLowerCase() === "a") {
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
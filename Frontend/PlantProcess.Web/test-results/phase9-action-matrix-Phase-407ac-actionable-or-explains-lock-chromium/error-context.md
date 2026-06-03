# Instructions

- Following Playwright test failed.
- Explain why, be concise, respect Playwright best practices.
- Provide a snippet of code with the fix, if possible.

# Test info

- Name: phase9-action-matrix.spec.ts >> Phase 09 — P9-01 action-matrix coverage >> DB Links / Admin: every visible control is labeled/actionable or explains lock
- Location: e2e\phase9-action-matrix.spec.ts:11:5

# Error details

```
Error: DB Links / Admin renders expected business text

expect(locator).toContainText(expected) failed

Locator: locator('body')
Timeout: 20000ms
Expected pattern: /admin|configuration|database|source/i
Received string:  "
    Backend connection failedBackend API is unreachable. Confirm PlantProcess.Api is running and VITE_API_BASE_URL points to it. Details: Failed to fetchRetry connection········
"

Call log:
  - DB Links / Admin renders expected business text with timeout 20000ms
  - waiting for locator('body')
    43 × locator resolved to <body>…</body>
       - unexpected value "
    Backend connection failedBackend API is unreachable. Confirm PlantProcess.Api is running and VITE_API_BASE_URL points to it. Details: Failed to fetchRetry connection
    
  
"

```

```yaml
- region "Notifications alt+T"
- img "SOU"
- paragraph: Backend connection failed
- paragraph: "Backend API is unreachable. Confirm PlantProcess.Api is running and VITE_API_BASE_URL points to it. Details: Failed to fetch"
- button "Retry connection"
- region "Notifications alt+T"
```

# Test source

```ts
  1   | import { expect, type Page } from "@playwright/test";
  2   | 
  3   | export type Phase9Route = {
  4   |   name: string;
  5   |   path: string;
  6   |   expectedText: RegExp;
  7   | };
  8   | 
  9   | export const phase9Routes: Phase9Route[] = [
  10  |   { name: "Home / Dashboard", path: "/dashboard", expectedText: /dashboard|quality|risk|plantprocess/i },
  11  |   { name: "DB Links / Admin", path: "/admin", expectedText: /admin|configuration|database|source/i },
  12  |   { name: "Source Objects / Admin", path: "/admin", expectedText: /source|schema|configuration|import/i },
  13  |   { name: "Importing Data / Admin", path: "/admin", expectedText: /import|job|batch|configuration/i },
  14  |   { name: "Schema Config / Admin", path: "/admin", expectedText: /schema|mapping|canonical|sql/i },
  15  |   { name: "Jobs Monitor / Admin", path: "/admin", expectedText: /job|monitor|run|status/i },
  16  |   { name: "Page Builder", path: "/page-builder", expectedText: /page|builder|widget|dashboard/i },
  17  |   { name: "Widget Script Compiler", path: "/widget-script-compiler", expectedText: /widget|script|compiler|expression/i },
  18  |   { name: "Material Investigation", path: "/materials", expectedText: /material|investigation|genealogy|quality/i },
  19  |   { name: "Risk Dashboard", path: "/risk", expectedText: /risk|score|quality|plant/i },
  20  |   { name: "Data Quality", path: "/data-quality", expectedText: /data|quality|issue|validation/i },
  21  |   { name: "ML / Correlation", path: "/correlations", expectedText: /correlation|parameter|quality|analysis/i },
  22  |   { name: "ML Readiness", path: "/ml-readiness", expectedText: /ml|readiness|feature|model|gate/i },
  23  |   { name: "Advanced Analysis", path: "/investigate/advanced", expectedText: /advanced|analysis|evidence|result/i },
  24  |   { name: "Suggestions", path: "/suggestions", expectedText: /suggestion|recommendation|evidence|action/i },
  25  |   { name: "License / User Admin", path: "/commercial/license", expectedText: /license|feature|tier|usage/i },
  26  |   { name: "Reports / Demo Lifecycle", path: "/demo-lifecycle", expectedText: /demo|lifecycle|reset|report|workflow/i },
  27  |   { name: "Brand", path: "/brand", expectedText: /brand|sou|plantprocess|identity/i },
  28  | ];
  29  | 
  30  | type InteractiveIssue = {
  31  |   selector: string;
  32  |   text: string;
  33  |   reason: string;
  34  | };
  35  | 
  36  | export async function waitForPhase9PageReady(page: Page, route: Phase9Route) {
  37  |   await page.goto(route.path, { waitUntil: "domcontentloaded" });
  38  |   await page.waitForLoadState("networkidle", { timeout: 20_000 }).catch(() => undefined);
  39  | 
  40  |   await expect(page.locator("body"), `${route.name} body is visible`).toBeVisible();
> 41  |   await expect(page.locator("body"), `${route.name} renders expected business text`).toContainText(route.expectedText, {
      |                                                                                      ^ Error: DB Links / Admin renders expected business text
  42  |     timeout: 20_000,
  43  |   });
  44  | 
  45  |   const bodyText = await page.locator("body").innerText({ timeout: 10_000 }).catch(() => "");
  46  |   expect.soft(bodyText, `${route.name} must not show runtime exception text`).not.toMatch(
  47  |     /cannot read properties|undefined is not an object|unhandled runtime|vite error overlay/i,
  48  |   );
  49  | }
  50  | 
  51  | export async function collectInteractiveIssues(page: Page): Promise<InteractiveIssue[]> {
  52  |   return page.evaluate(() => {
  53  |     const controls = Array.from(
  54  |       document.querySelectorAll<HTMLElement>(
  55  |         [
  56  |           "button",
  57  |           "a[href]",
  58  |           "input",
  59  |           "select",
  60  |           "textarea",
  61  |           "[role='button']",
  62  |           "[role='tab']",
  63  |           "[role='menuitem']",
  64  |         ].join(","),
  65  |       ),
  66  |     );
  67  | 
  68  |     function isVisible(el: HTMLElement) {
  69  |       const style = window.getComputedStyle(el);
  70  |       const box = el.getBoundingClientRect();
  71  |       return style.visibility !== "hidden" && style.display !== "none" && box.width > 0 && box.height > 0;
  72  |     }
  73  | 
  74  |     function labelOf(el: HTMLElement) {
  75  |       return [
  76  |         el.getAttribute("aria-label"),
  77  |         el.getAttribute("title"),
  78  |         el.getAttribute("data-testid"),
  79  |         el.getAttribute("name"),
  80  |         el.getAttribute("placeholder"),
  81  |         el.textContent,
  82  |       ]
  83  |         .filter(Boolean)
  84  |         .join(" ")
  85  |         .replace(/\s+/g, " ")
  86  |         .trim();
  87  |     }
  88  | 
  89  |     function selectorOf(el: HTMLElement) {
  90  |       const tag = el.tagName.toLowerCase();
  91  |       const id = el.id ? `#${el.id}` : "";
  92  |       const testId = el.getAttribute("data-testid") ? `[data-testid="${el.getAttribute("data-testid")}"]` : "";
  93  |       const text = labelOf(el).slice(0, 50);
  94  |       return `${tag}${id}${testId}${text ? ` :: ${text}` : ""}`;
  95  |     }
  96  | 
  97  |     function hasDisabledReason(el: HTMLElement, label: string) {
  98  |       const reasonText = [
  99  |         label,
  100 |         el.getAttribute("aria-describedby"),
  101 |         el.getAttribute("data-disabled-reason"),
  102 |         el.closest("[data-disabled-reason]")?.getAttribute("data-disabled-reason"),
  103 |         el.closest("[aria-label]")?.getAttribute("aria-label"),
  104 |       ]
  105 |         .filter(Boolean)
  106 |         .join(" ")
  107 |         .toLowerCase();
  108 | 
  109 |       return /locked|permission|requires|select|configure|not available|coming soon|disabled|loading|no data|read only|license/.test(reasonText);
  110 |     }
  111 | 
  112 |     const issues: InteractiveIssue[] = [];
  113 | 
  114 |     for (const el of controls) {
  115 |       if (!isVisible(el)) continue;
  116 | 
  117 |       const label = labelOf(el);
  118 |       const isDisabled =
  119 |         el.hasAttribute("disabled") ||
  120 |         el.getAttribute("aria-disabled") === "true" ||
  121 |         el.classList.contains("disabled");
  122 | 
  123 |       if (!label) {
  124 |         issues.push({
  125 |           selector: selectorOf(el),
  126 |           text: "",
  127 |           reason: "Interactive control has no text, aria-label, title, placeholder, name, or data-testid.",
  128 |         });
  129 |         continue;
  130 |       }
  131 | 
  132 |       if (isDisabled && !hasDisabledReason(el, label)) {
  133 |         issues.push({
  134 |           selector: selectorOf(el),
  135 |           text: label,
  136 |           reason: "Disabled/locked control does not explain why it is unavailable.",
  137 |         });
  138 |         continue;
  139 |       }
  140 | 
  141 |       if (el.tagName.toLowerCase() === "a") {
```
// ============================================================================
// Customer route invariant guard.
//
// Backlog origin: T-203   Release: M2   Owner: Worker 2 (Release Truth)
//
// Observes one route for the whole of its visit and reports every violation by
// name. Nothing is silently tolerated: an allowance must be declared, and every
// declared allowance is recorded in the evidence so a green run still shows
// what was forgiven.
//
// This guard deliberately does NOT reuse e2e/helpers/e2eFailureFilters.ts.
// That module treats "failed to load resource" as ignorable, which is exactly
// the class of masking T-203 exists to prevent.
// ============================================================================

import type { ConsoleMessage, Page, Request, Response } from "@playwright/test";

export type Violation = { kind: string; detail: string };

export type RouteObservation = {
  route: string;
  settled: boolean;
  violations: Violation[];
  allowances: string[];
  requestCount: number;
  widgetStates: string[];
};

export type Allowance = {
  /** Substring of the request URL this allowance applies to. */
  urlFragment?: string;
  /** Status this allowance permits. 401/403 must name the role reason. */
  status?: number;
  /** Why this is not a defect. Required: an allowance without a reason is a whitelist. */
  reason: string;
};

export type GuardOptions = {
  allowances?: Allowance[];
  settleQuietMs?: number;
  settleTimeoutMs?: number;
};

export function installRouteInvariantGuard(page: Page, route: string, options: GuardOptions = {}) {
  const violations: Violation[] = [];
  const allowances: string[] = [];
  const declared = options.allowances ?? [];
  let requestCount = 0;
  let inFlight = 0;
  let lastQuietAt = Date.now();

  const permitted = (url: string, status: number): Allowance | undefined =>
    declared.find(
      (a) =>
        (a.urlFragment === undefined || url.includes(a.urlFragment)) &&
        (a.status === undefined || a.status === status)
    );

  page.on("request", () => { requestCount += 1; inFlight += 1; });

  const settleTick = () => {
    inFlight = Math.max(0, inFlight - 1);
    if (inFlight === 0) lastQuietAt = Date.now();
  };
  page.on("requestfinished", settleTick);

  page.on("requestfailed", (request: Request) => {
    settleTick();
    const url = request.url();
    const allow = permitted(url, 0);
    if (allow) { allowances.push(`request failed, allowed: ${url} (${allow.reason})`); return; }
    violations.push({
      kind: "request-failed",
      detail: `${request.method()} ${url} :: ${request.failure()?.errorText ?? "unknown"}`,
    });
  });

  page.on("response", (response: Response) => {
    const status = response.status();
    if (status < 400) return;
    const url = response.url();
    const allow = permitted(url, status);
    if (allow) { allowances.push(`http ${status}, allowed: ${url} (${allow.reason})`); return; }
    violations.push({ kind: "http-error", detail: `${status} ${response.request().method()} ${url}` });
  });

  page.on("console", (message: ConsoleMessage) => {
    if (message.type() !== "error") return;
    violations.push({ kind: "console-error", detail: message.text().slice(0, 300) });
  });

  page.on("pageerror", (error: Error) => {
    violations.push({ kind: "uncaught-exception", detail: (error.message ?? String(error)).slice(0, 300) });
  });

  return {
    async settle(): Promise<boolean> {
      const quietMs = options.settleQuietMs ?? 700;
      const timeoutMs = options.settleTimeoutMs ?? 25_000;
      const deadline = Date.now() + timeoutMs;
      while (Date.now() < deadline) {
        if (inFlight === 0 && Date.now() - lastQuietAt >= quietMs) return true;
        await page.waitForTimeout(120);
      }
      return false;
    },

    async collect(settled: boolean): Promise<RouteObservation> {
      // Visible customer-facing error surfaces.
      const alerts = await page.locator('[role="alert"]').allTextContents();
      for (const text of alerts) {
        const trimmed = text.trim();
        if (trimmed.length === 0) continue;
        violations.push({ kind: "error-surface", detail: trimmed.slice(0, 200) });
      }

      // Widget terminal states. A danger tone is a Failed widget.
      const widgetStates: string[] = [];
      const panels = page.locator("[data-widget-state]");
      const count = await panels.count();
      for (let i = 0; i < count; i += 1) {
        const state = (await panels.nth(i).getAttribute("data-widget-state")) ?? "unknown";
        const tone = (await panels.nth(i).getAttribute("data-widget-tone")) ?? "unknown";
        widgetStates.push(`${state}/${tone}`);
        if (tone === "danger") {
          violations.push({ kind: "widget-failed", detail: `widget state ${state} (tone ${tone})` });
        }
      }

      if (!settled) {
        violations.push({ kind: "never-settled", detail: `route did not reach a quiet state` });
      }

      return { route, settled, violations, allowances, requestCount, widgetStates };
    },
  };
}
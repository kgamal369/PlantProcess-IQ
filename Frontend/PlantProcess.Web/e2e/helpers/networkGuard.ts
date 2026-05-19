import type { Page } from "@playwright/test";
import { expect } from "@playwright/test";

const ignoredUrlParts = [
  "favicon",
  "vite",
  "sockjs",
  "hot-update",
];

export function installNetworkGuard(page: Page) {
  const failedResponses: string[] = [];
  const consoleErrors: string[] = [];

  page.on("response", (response) => {
    const url = response.url();
    const status = response.status();

    if (ignoredUrlParts.some((part) => url.includes(part))) return;

    if ([401, 403, 404, 500].includes(status)) {
      failedResponses.push(`${status} ${url}`);
    }
  });

  page.on("console", (message) => {
    if (message.type() === "error") {
      consoleErrors.push(message.text());
    }
  });

  return async function assertNoNetworkFailures() {
    expect(failedResponses, `Unexpected API failures:\n${failedResponses.join("\n")}`)
      .toEqual([]);

    expect(consoleErrors, `Unexpected browser console errors:\n${consoleErrors.join("\n")}`)
      .toEqual([]);
  };
}
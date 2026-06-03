import { expect, test } from "@playwright/test";
import { apiBaseUrl, login } from "./helpers/auth";

const slug = "e2e-pagebuilder-persistence";

const payload = {
  slug,
  title: "E2E Page Builder Persistence",
  visibility: "Shared",
  layoutJson: {
    grid: { columns: 12, rowHeight: 80 },
    widgets: [
      { id: "w-risk", kind: "kpi", title: "Risk KPI", x: 0, y: 0, w: 3, h: 2, source: "schema_view:risk_summary" },
      { id: "w-defects", kind: "bar", title: "Defect breakdown", x: 3, y: 0, w: 5, h: 3, source: "schema_view:defect_breakdown" },
      { id: "w-filter", kind: "filter-list", title: "Plant filter", x: 8, y: 0, w: 4, h: 2, source: "filter:plant" },
    ],
  },
  widgetBindingsJson: {
    bindings: [
      { widgetId: "w-risk", source: "schema_view:risk_summary" },
      { widgetId: "w-defects", source: "schema_view:defect_breakdown" },
      { widgetId: "w-filter", source: "filter:plant" },
    ],
  },
};

function headers(token: string) {
  return {
    Authorization: "Bearer " + token,
    Accept: "application/json",
    "Content-Type": "application/json",
  };
}

async function responseDetails(response: { status: () => number; text: () => Promise<string> }) {
  let body: string;
  try {
    body = await response.text();
  } catch {
    body = "<unable to read response body>";
  }

  return "HTTP " + response.status() + " body: " + body;
}

test.describe("P03 PageDefinition persistence acceptance", () => {
  test("API creates, reloads, updates, lists, validates and deletes a user-created page", async ({ request }) => {
    const token = await login(request);
    const authHeaders = headers(token);

    await request.delete(apiBaseUrl + "/pages/" + slug, {
      headers: authHeaders,
    }).catch(() => undefined);

    const create = await request.post(apiBaseUrl + "/pages", {
      headers: authHeaders,
      data: payload,
    });

    expect(create.ok(), "create failed with " + await responseDetails(create)).toBeTruthy();

    const created = await create.json();
    expect(created.slug).toBe(slug);
    expect(created.title).toBe(payload.title);
    expect(created.visibility).toBe("Shared");
    expect(created.layoutJson.widgets).toHaveLength(3);
    expect(created.widgetBindingsJson.bindings).toHaveLength(3);

    const loadedResponse = await request.get(apiBaseUrl + "/pages/" + slug, {
      headers: authHeaders,
    });

    expect(loadedResponse.ok(), "load failed with " + await responseDetails(loadedResponse)).toBeTruthy();

    const loaded = await loadedResponse.json();
    expect(loaded.slug).toBe(slug);
    expect(loaded.layoutJson.widgets.map((widget: { id: string }) => widget.id)).toEqual([
      "w-risk",
      "w-defects",
      "w-filter",
    ]);

    const updatedResponse = await request.put(apiBaseUrl + "/pages/" + slug, {
      headers: authHeaders,
      data: {
        ...payload,
        title: "E2E Page Builder Persistence Updated",
      },
    });

    expect(updatedResponse.ok(), "update failed with " + await responseDetails(updatedResponse)).toBeTruthy();

    const updated = await updatedResponse.json();
    expect(updated.title).toBe("E2E Page Builder Persistence Updated");
    expect(updated.version).toBeGreaterThanOrEqual(created.version);

    const listResponse = await request.get(apiBaseUrl + "/pages", {
      headers: authHeaders,
    });

    expect(listResponse.ok(), "list failed with " + await responseDetails(listResponse)).toBeTruthy();

    const pages = await listResponse.json();
    expect(pages.some((page: { slug: string }) => page.slug === slug)).toBeTruthy();

    const invalid = await request.post(apiBaseUrl + "/pages", {
      headers: authHeaders,
      data: {
        ...payload,
        slug: "Invalid Slug With Spaces",
      },
    });

    expect(invalid.status()).toBe(400);
    expect(await invalid.text()).toMatch(/slug|url-safe/i);

    const deleteResponse = await request.delete(apiBaseUrl + "/pages/" + slug, {
      headers: authHeaders,
    });

    expect(deleteResponse.ok(), "delete failed with " + await responseDetails(deleteResponse)).toBeTruthy();

    const deleted = await deleteResponse.json();
    expect(deleted.deleted).toBeTruthy();
  });

  test("UI saves and reloads the metadata page definition through the backend", async ({ page, request }) => {
    const token = await login(request);

    /* P01: no browser token seeding; AuthProvider performs cookie refresh/login bootstrap. */

    await page.goto("/page-builder");

    await expect(
      page.getByRole("heading", { name: /User-created pages, not coded pages/i }),
    ).toBeVisible({ timeout: 15_000 });

    await page.getByLabel("Slug", { exact: true }).fill("e2e-pagebuilder-ui");
    await page.getByLabel("Title", { exact: true }).fill("E2E PageBuilder UI");

    await page.getByRole("button", { name: /Add List-of-values filter/i }).click();

    await expect(page.locator("[data-widget-id]")).toHaveCount(4);

    await page.getByRole("button", { name: /^Save page definition$/i }).click();

    await expect(page.getByRole("status")).toContainText(/Saved PageDefinition 'e2e-pagebuilder-ui'/i, {
      timeout: 15_000,
    });

    await page.getByRole("button", { name: /^Load by slug$/i }).click();

    await expect(page.getByRole("status")).toContainText(/Loaded PageDefinition 'e2e-pagebuilder-ui'/i, {
      timeout: 15_000,
    });

    await expect(page.locator("[data-widget-id]")).toHaveCount(4);
  });
});
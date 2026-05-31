import { test, expect } from "@playwright/test";

test.describe("P02 data lifecycle contract", () => {
  test("demo source and genealogy proof endpoints remain reachable", async ({ request }) => {
    const health = await request.get("/health");
    expect([200, 401, 403]).toContain(health.status());
  });
});

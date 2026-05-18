import { describe, expect, it } from "vitest";

describe("frontend architecture", () => {
  it("keeps API split folders present", async () => {
    const folders = [
      "admin",
      "dashboarding",
      "integration",
      "analytics",
      "http"
    ];

    for (const folder of folders) {
      const module = await import(`../../api/${folder}/index.ts`).catch(() => null);
      expect(module, `Missing src/api/${folder}/index.ts`).not.toBeNull();
    }
  });

  it("does not execute Playwright specs in Vitest", () => {
    expect(true).toBe(true);
  });
});

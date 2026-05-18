import { describe, expect, it } from "vitest";

describe("frontend integration with mocked backend", () => {
  it("calls mocked health endpoint", async () => {
    const response = await fetch("http://localhost:5063/health");
    const body = await response.json();

    expect(response.ok).toBe(true);
    expect(body.service).toBe("PlantProcess IQ API");
    expect(body.status).toBe("Healthy");
  });
});

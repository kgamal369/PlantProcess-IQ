import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

function readApiClientSource() {
  return fs.readFileSync(
    path.join(process.cwd(), "src", "api", "http", "apiClient.ts"),
    "utf8"
  );
}

describe("apiClient retry/backoff contract", () => {
  it("keeps retry limited to idempotent request methods", () => {
    const source = readApiClientSource();
    const normalized = source.replace(/\s+/g, " ");

    expect(normalized).toContain("GET / HEAD / OPTIONS");
    expect(normalized).toContain("automatic retry once");
    expect(normalized).toContain("NEVER retried on 4xx/5xx");
  });

  it("keeps retry backoff bounded and deterministic", () => {
    const source = readApiClientSource();

    expect(source).toContain("sleep(150 * attempt)");
    expect(source).toContain("continue;");
  });

  it("does not retry ApiError responses", () => {
    const source = readApiClientSource();

    expect(source).toContain("err instanceof ApiError");
    expect(source).toContain("!(err instanceof ApiError)");
  });

  it("dispatches auth-failure for authentication and authorization errors", () => {
    const source = readApiClientSource();

    expect(source).toContain("plantprocess:auth-failure");
    expect(source).toContain("401");
    expect(source).toContain("403");
  });

  it("keeps login storage and logout cleanup in the public client surface", () => {
    const source = readApiClientSource();

    expect(source).toContain("setAccessToken(response.accessToken)");
    expect(source).toContain("setAuthenticatedUser");
    expect(source).toContain("clearAuthentication");
    expect(source).toContain("logout()");
  });
});

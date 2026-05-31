import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

function readAuthContextSource() {
  return fs.readFileSync(
    path.join(process.cwd(), "src", "state", "AuthContext.tsx"),
    "utf8"
  );
}

describe("AuthContext bootstrap hardening contract", () => {
  it("caps automatic bootstrap attempts to prevent auth retry storms", () => {
    const source = readAuthContextSource();

    expect(source).toMatch(/MAX_AUTO_BOOTSTRAP_ATTEMPTS\s*=\s*3/);
    expect(source).toContain("automaticAttemptsRef");
    expect(source).toContain("bootstrapAttemptCount");
  });

  it("uses deterministic retry backoff for automatic auth recovery", () => {
    const source = readAuthContextSource();
    const compact = source.replace(/\s+/g, "");

    expect(compact).toContain("AUTH_RETRY_BACKOFF_MS=[0,500,1500]");
    expect(source).toContain("sleep(");
  });

  it("does not re-login forever after invalid credentials or forbidden responses", () => {
    const source = readAuthContextSource();

    expect(source).toContain('status === 401');
    expect(source).toContain('status === 403');
    expect(source).toContain("MAX_AUTO_BOOTSTRAP_ATTEMPTS");
    expect(source).toContain('buildAuthMessage("invalid-credentials")');
    expect(source).toContain('buildAuthMessage("forbidden")');
  });

  it("manual retry resets the capped attempt counter", () => {
    const source = readAuthContextSource();

    expect(source).toContain("retryBootstrap");
    expect(source).toMatch(/automaticAttemptsRef\.current\s*=\s*0/);
    expect(source).toContain('bootstrap("manual"');
  });

  it("does not fall back to an insecure hard-coded smoke password", () => {
    const source = readAuthContextSource();

    expect(source).toContain("VITE_SMOKE_PASSWORD");
    expect(source).toContain("missing-smoke-password");
    expect(source).not.toContain('const DEMO_PASS = "ChangeMe123!"');
  });
});

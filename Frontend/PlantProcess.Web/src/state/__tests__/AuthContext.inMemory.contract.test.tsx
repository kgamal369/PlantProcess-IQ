import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

// P2-T05: the auth/session token must live only in memory (React state/refs),
// never in browser storage. Mirrors the existing AuthContext contract-test style.
function readAuthContextSource() {
  return fs.readFileSync(
    path.join(process.cwd(), "src", "state", "AuthContext.tsx"),
    "utf8",
  );
}

describe("AuthContext token storage contract (P2-T05)", () => {
  it("keeps auth state in memory via React state/refs", () => {
    const source = readAuthContextSource();
    expect(source).toMatch(/useState/);
    expect(source).toMatch(/useRef/);
  });

  it("never persists tokens to localStorage or sessionStorage", () => {
    const source = readAuthContextSource();
    expect(source).not.toMatch(/localStorage/);
    expect(source).not.toMatch(/sessionStorage/);
  });
});
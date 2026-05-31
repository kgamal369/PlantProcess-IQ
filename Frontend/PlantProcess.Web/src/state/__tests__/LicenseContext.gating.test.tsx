import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

function readLicenseContextSource() {
  return fs.readFileSync(
    path.join(process.cwd(), "src", "state", "LicenseContext.tsx"),
    "utf8"
  );
}

describe("LicenseContext feature gating contract", () => {
  it("exposes hasFeature and getFeature as the only feature-gate read API", () => {
    const source = readLicenseContextSource();

    expect(source).toContain("hasFeature:");
    expect(source).toContain("getFeature:");
    expect(source).toContain("hasFeature");
    expect(source).toContain("getFeature");
  });

  it("performs case-insensitive feature lookup", () => {
    const source = readLicenseContextSource();

    expect(source).toContain("toLowerCase()");
    expect(source).toContain("feature.toLowerCase()");
  });

  it("fails closed when a feature is absent", () => {
    const source = readLicenseContextSource();

    expect(source).toMatch(/getFeature\(feature\)\?\.isEnabled\s*\?\?\s*false/);
  });

  it("clears license state while authentication is missing or bootstrapping", () => {
    const source = readLicenseContextSource();

    expect(source).toContain("useAuth");
    expect(source).toContain("isAuthenticated");
    expect(source).toContain("isBootstrapping");
    expect(source).toContain("clearLicenseState");
  });

  it("loads current license, usage and commercial readiness together", () => {
    const source = readLicenseContextSource();

    expect(source).toContain("licenseApi.getCurrent()");
    expect(source).toContain("licenseUsageApi.getUsage()");
    expect(source).toContain("licenseUsageApi.getCommercialReadiness()");
  });
});

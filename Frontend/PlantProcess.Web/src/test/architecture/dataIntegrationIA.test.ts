// ============================================================
// M1-06 gate: Data Integration is its own area, and Connector Truth tells the truth.
// ============================================================
import { describe, expect, it } from "vitest";
import { readdirSync, readFileSync, statSync, existsSync } from "node:fs";
import { resolve, join } from "node:path";

const webRoot = resolve(__dirname, "../../..");
const read = (rel: string): string => {
  const p = resolve(webRoot, rel);
  return existsSync(p) ? readFileSync(p, "utf8") : "";
};

function walk(dir: string, out: string[] = []): string[] {
  for (const entry of readdirSync(dir)) {
    const full = join(dir, entry);
    if (statSync(full).isDirectory()) walk(full, out);
    else if (/\.tsx?$/.test(entry)) out.push(full);
  }
  return out;
}

describe("M1-06: data integration lives outside Administrator", () => {
  it("Administrator imports nothing from PlatformOps or the demo stylesheet", () => {
    const admin = read("src/pages/Admin/AdminPageContent.tsx");
    expect(admin.length).toBeGreaterThan(0);
    expect(admin).not.toContain("PlatformOps");
    expect(admin).not.toContain("demo-analytics");
  });

  it("Administrator no longer renders the data-integration tabs", () => {
    const admin = read("src/pages/Admin/AdminPageContent.tsx");
    for (const tab of ["DbConfigurationTab", "SchemaConfigurationTab", "ImportingDataTab", "JobsMonitorTab"]) {
      expect(admin, `Administrator still renders ${tab}`).not.toContain(tab);
    }
  });

  it("no page under DataIntegration fabricates connector rows", () => {
    const root = resolve(webRoot, "src/pages/DataIntegration");
    expect(existsSync(root)).toBe(true);
    const offenders: string[] = [];
    for (const file of walk(root)) {
      const src = readFileSync(file, "utf8");
      // The old page hardcoded these when the API returned nothing.
      if (/MeltShop PostgreSQL|Caster Oracle Shape/.test(src)) {
        offenders.push(file.replace(webRoot, ""));
      }
    }
    expect(offenders, `fabricated connector rows:\n  ${offenders.join("\n  ")}`).toHaveLength(0);
  });

  it("the five data-integration routes exist", () => {
    const app = read("src/App.tsx");
    expect(app).toContain('path="/data-integration"');
    for (const p of ["connections", "registry", "importing", "jobs", "connector-truth"]) {
      expect(app, `missing route ${p}`).toContain(`path="${p}"`);
    }
  });

  it("old ?adminTab deep links are mapped, not dropped", () => {
    const redirect = read("src/pages/DataIntegration/AdminTabRedirect.tsx");
    for (const tab of ["db-configuration", "schema-configuration", "importing-data", "jobs-monitor", "connector-truth"]) {
      expect(redirect, `adminTab=${tab} has no target`).toContain(`"${tab}"`);
    }
  });

  it("the sidebar exposes Data Integration as a top-level area", () => {
    const layout = read("src/components/AppLayout.tsx");
    expect(layout).toContain("NAV_DATA_INTEGRATION");
    expect(layout).toContain('"/data-integration/connections"');
    expect(layout).not.toContain('desc: "DB config, schema mapping and jobs"');
  });
});
import fs from "node:fs";
import path from "node:path";

const root = process.cwd();
const reportDir = path.join(root, "Documentation", "v5");
fs.mkdirSync(reportDir, { recursive: true });

function p(relative) {
  return path.join(root, relative);
}

function exists(relative) {
  return fs.existsSync(p(relative));
}

function read(relative) {
  return fs.readFileSync(p(relative), "utf8");
}

function ok(label, condition, evidence = "") {
  if (!condition) throw new Error(`${label} failed${evidence ? `: ${evidence}` : ""}`);
  console.log(`OK ${label}${evidence ? `: ${evidence}` : ""}`);
}

function walk(dir, output = []) {
  if (!fs.existsSync(dir)) return output;

  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);

    if (entry.isDirectory()) {
      if (["bin", "obj", ".git", ".vs", "node_modules", "dist", "build", "coverage"].includes(entry.name)) continue;
      if (entry.name.startsWith("_pack_")) continue;
      if (entry.name.startsWith("tmp")) continue;
      walk(full, output);
    } else {
      output.push(full);
    }
  }

  return output;
}

const kpiEngine = read("Backend/PlantProcess.Analytics.Core/Kpi/KpiEngine.cs");
const appSafeSql = read("Backend/PlantProcess.Application/Integration/Security/SafeSqlValidator.cs");
const provider = read("Backend/PlantProcess.Application/Integration/Security/SqlAllowlistProvider.cs");
const program = read("Backend/PlantProcess.Api/Program.cs");
const kpiTests = read("Backend/tests/PlantProcess.Application.UnitTests/Phase1_KpiSqlGuardTests.cs");
const sqlTests = read("Backend/tests/PlantProcess.Application.UnitTests/Security/SafeSqlValidatorPhase1Tests.cs");
const gitignore = exists(".gitignore") ? read(".gitignore") : "";

const guardName = ["Basic", "Sql", "Guard"].join("");
const saveTokenAssignments = [...program.matchAll(/options\.SaveToken\s*=/g)].length;

const runtimeBasicGuardOffenders = walk(path.join(root, "Backend"))
  .filter(x => x.endsWith(".cs"))
  .map(x => path.relative(root, x).replaceAll(path.sep, "/"))
  .filter(x => !x.includes("/tests/"))
  .filter(x => !x.includes("/tools/"))
  .filter(x => !x.includes("/docs/"))
  .filter(x => !x.includes("_backup"))
  .filter(x => fs.readFileSync(path.join(root, x), "utf8").includes(guardName));

ok("PPIQ-T001 KPI SafeSqlValidator exists in Analytics.Core", kpiEngine.includes("public static class SafeSqlValidator"));
ok("PPIQ-T001 runtime BasicSqlGuard retired", runtimeBasicGuardOffenders.length === 0, `${runtimeBasicGuardOffenders.length} offender(s)`);
ok("PPIQ-T001 created_at/updated_at regression exists", kpiTests.includes("created_at") && kpiTests.includes("updated_at"));
ok("PPIQ-T001 OFFSET 10 regression exists", kpiTests.toLowerCase().includes("offset 10"));
ok("PPIQ-T001 DROP TABLE rejection exists", kpiTests.toLowerCase().includes("drop table"));

ok("PPIQ-T003 exactly one SaveToken assignment", saveTokenAssignments === 1, `${saveTokenAssignments} assignment(s)`);

ok("PPIQ-T004 SqlAllowlistProvider exists", exists("Backend/PlantProcess.Application/Integration/Security/SqlAllowlistProvider.cs"));
ok("PPIQ-T004 SafeSqlValidator uses provider", appSafeSql.includes("SqlAllowlistProvider"));
ok("PPIQ-T004 defect catalog canonical table is plural", provider.includes('"defect_catalogs"') && !provider.includes('"defect_catalog"'));
ok("PPIQ-T004 downtime_events appears once", (provider.match(/"downtime_events"/g) ?? []).length === 1);

ok("PPIQ-T005 dynamic allowlist overload exists", appSafeSql.includes("IEnumerable<string>? allowedTables"));
ok("PPIQ-T005 dynamic allowlist regression exists", sqlTests.includes("tenant_registered_view"));

ok("PPIQ-T006 KPI SQL path no longer calls BasicSqlGuard", !kpiEngine.includes(`${guardName}.Validate`));

ok("PPIQ-T008 gitignore backup hygiene exists", gitignore.includes("PPIQ-T008") && gitignore.includes("*.bak_*"));

ok("PPIQ-T009 SQL-injection corpus exists", sqlTests.includes("pg_read_file") && sqlTests.includes("information_schema"));

const report = {
  generatedAtUtc: new Date().toISOString(),
  saveTokenAssignments,
  runtimeBasicGuardOffenders,
  status: "green"
};

fs.writeFileSync(
  path.join(reportDir, "pack-p1-t001-t009-validation-report.json"),
  JSON.stringify(report, null, 2),
  "utf8"
);

console.log("");
console.log("Pack P1A T001-T009 validation passed.");
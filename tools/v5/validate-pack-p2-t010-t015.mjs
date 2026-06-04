import fs from "node:fs";
import path from "node:path";

const root = process.cwd();
const backend = path.join(root, "Backend");
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
    } else if (entry.name.endsWith(".cs")) {
      output.push(full);
    }
  }

  return output;
}

const iface = read("Backend/PlantProcess.Application/Analytics/Engines/ICorrelationEngine.cs");
const canonical = read("Backend/PlantProcess.Application/Analytics/Engines/CanonicalCorrelationEngine.cs");
const registry = read("Backend/PlantProcess.Application/Analytics/Engines/CorrelationEngineRegistry.cs");
const appDi = read("Backend/PlantProcess.Application/DependencyInjection.cs");
const endpoints = read("Backend/PlantProcess.Api/Endpoints/Analytics/CorrelationEndpoints.cs");
const service = read("Backend/PlantProcess.Application/Analytics/Services/CorrelationService.cs");
const safeSql = read("Backend/PlantProcess.Application/Integration/Security/SafeSqlValidator.cs");

const advancedFiles = walk(path.join(backend, "PlantProcess.Application", "Analytics", "Advanced"))
  .map((file) => fs.readFileSync(file, "utf8"))
  .join("\n");

const providerFiles = walk(path.join(backend, "PlantProcess.Application", "Analytics", "Services"))
  .map((file) => fs.readFileSync(file, "utf8"))
  .join("\n");

ok("T010 ICorrelationEngine is authoritative AdvancedAnalysisRunResult contract", iface.includes("Task<AdvancedAnalysisRunResult>"));
ok("T010 canonical engine wraps IAdvancedCorrelationService", canonical.includes("IAdvancedCorrelationService"));
ok("T010 registry default is canonical", registry.includes("Default => _byKey[CanonicalCorrelationEngine.CanonicalKey]"));

ok(
  "T010 DI registers canonical ICorrelationEngine",
  /AddScoped\s*<\s*ICorrelationEngine\s*,\s*CanonicalCorrelationEngine\s*>\s*\(/.test(appDi)
);

ok(
  "T010 DI registers ICorrelationEngineRegistry",
  /AddScoped\s*<\s*ICorrelationEngineRegistry\s*,\s*CorrelationEngineRegistry\s*>\s*\(/.test(appDi)
);

for (const field of [
  "AdvancedFinding",
  "EffectSize",
  "QValue",
  "StabilityLower",
  "StabilityUpper",
  "StabilityConsistency",
  "SurvivesStratification",
  "ProvenanceHandle",
  "HonestyCaveat"
]) {
  ok(`T010 result contract has ${field}`, advancedFiles.includes(field));
}

ok("T011 canonical API endpoint exists", endpoints.includes("/canonical/run"));
ok("T011 canonical endpoint method exists", endpoints.includes("RunCanonicalCorrelationAsync"));
ok("T011 canonical endpoint uses ICorrelationEngineRegistry", endpoints.includes("ICorrelationEngineRegistry"));
ok("T011 canonical endpoint returns honesty caveat", endpoints.includes("honestyCaveat"));
ok("T011 canonical endpoint uses AdvancedAnalysisRequest", endpoints.includes("AdvancedAnalysisRequest"));

ok("T012 legacy CorrelationService is obsolete", service.includes("Obsolete") && service.includes("PPIQ-T012"));

ok("T013 narrative contract exists", exists("Backend/PlantProcess.Application/Analytics/Interfaces/INarrativeProvider.cs"));
ok("T013 local narrative strategy exists", providerFiles.includes("LocalNarrativeProvider"));
ok("T013 api narrative strategy exists", providerFiles.includes("ApiNarrativeProvider"));
ok("T013 configured narrative strategy exists", providerFiles.includes("ConfiguredNarrativeProvider"));

ok("T014 deterministic seed exists", advancedFiles.includes("20260603UL"));
ok("T014 bootstrap/stability logic exists", /Bootstrap/i.test(advancedFiles) && /Stability/i.test(advancedFiles));
ok("T014 stratification logic exists", /EvaluateStrata|Stratification/i.test(advancedFiles));

ok("T015 P2 architecture tests exist", exists("Backend/tests/PlantProcess.Application.UnitTests/Analytics/P2_CanonicalAnalyticsArchitectureTests.cs"));
ok("T015 SafeSql comma-join guard exists", safeSql.includes("ValidateCommaJoinIdentifiers"));

const report = {
  generatedAtUtc: new Date().toISOString(),
  status: "green",
  note: "P2A establishes canonical engine live endpoint and architecture gates. Legacy MVP endpoints remain backward-compatible."
};

fs.writeFileSync(
  path.join(reportDir, "pack-p2-t010-t015-validation-report.json"),
  JSON.stringify(report, null, 2),
  "utf8"
);

console.log("");
console.log("Pack P2A T010-T015 validation passed.");
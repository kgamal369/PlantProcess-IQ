const fs = require("fs");
const path = require("path");
const cp = require("child_process");
const root = process.cwd();
function read(relativePath) {
  const file = path.join(root, relativePath);
  if (!fs.existsSync(file)) throw new Error("Missing file: " + relativePath);
  return fs.readFileSync(file, "utf8");
}
function has(relativePath, marker) {
  const text = read(relativePath);
  if (!text.includes(marker)) throw new Error(relativePath + " missing marker: " + marker);
}
has("Frontend/PlantProcess.Web/src/i18n/phase78/phase78I18n.ts", "PPIQ_PHASE7_I18N_RTL");
has("Frontend/PlantProcess.Web/src/i18n/phase78/phase78I18n.ts", "ar:");
has("Frontend/PlantProcess.Web/src/i18n/phase78/phase78I18nRuntime.ts", "document.documentElement.dir");
has("Frontend/PlantProcess.Web/src/main.tsx", "./i18n/phase78/phase78I18nRuntime");
has("Frontend/PlantProcess.Web/src/styles/global.css", "./phase78/phase78-i18n-rtl.css");
has("Frontend/PlantProcess.Web/src/App.implementation.tsx", 'path="/i18n-rtl"');
has("Frontend/PlantProcess.Web/e2e/i18n/phase78-i18n-rtl.spec.ts", "plantprocess.locale.v1");
has("docs/ux/I18N_RTL.md", "plantprocess.locale.v1");
has("docs/phase8/backend-api-hygiene-report.json", "PPIQ_PHASE8_BACKEND_API_HYGIENE");
has("docs/phase8/openapi-source-contract-snapshot.json", "PPIQ_PHASE8_SOURCE_ROUTE_CONTRACT_SNAPSHOT");
cp.execFileSync("node", ["tools/phase78/check-backend-api-hygiene.cjs"], { cwd: root, stdio: "inherit" });
if (fs.existsSync(path.join(root, "tools", "phase56", "validate-phase56.cjs"))) cp.execFileSync("node", ["tools/phase56/validate-phase56.cjs"], { cwd: root, stdio: "inherit" });
console.log("Phase 7 + Phase 8 source validation passed.");

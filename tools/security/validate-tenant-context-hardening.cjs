const fs = require("fs");
const path = require("path");

const root = process.cwd();
const apiRoot = path.join(root, "Backend", "PlantProcess.Api");
const failures = [];

function walk(dir, predicate, output = []) {
  if (!fs.existsSync(dir)) return output;

  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);

    if (entry.isDirectory()) {
      if (["bin", "obj", ".git", "node_modules", "dist", "coverage"].includes(entry.name)) continue;
      walk(full, predicate, output);
      continue;
    }

    if (predicate(full)) output.push(full);
  }

  return output;
}

function rel(file) {
  return path.relative(root, file).split(path.sep).join("/");
}

const files = walk(apiRoot, (file) => file.endsWith(".cs"));

for (const file of files) {
  const relative = rel(file);
  const text = fs.readFileSync(file, "utf8");

  if ((/\bDefaultTenantId\b|\bDemoTenantId\b/i.test(text) || /demo-tenant/i.test(text)) && !relative.endsWith("TenantClaimReader.cs")) {
    failures.push({ file: relative, reason: "tenant fallback/hardcoded tenant text remains" });
  }
}

const tenantContext = path.join(apiRoot, "Security", "TenantContextAccessor.cs");
const program = path.join(apiRoot, "Program.cs");

if (!fs.existsSync(tenantContext)) {
  failures.push({ file: rel(tenantContext), reason: "TenantContextAccessor.cs missing" });
} else {
  const text = fs.readFileSync(tenantContext, "utf8");
  for (const signal of [
    "PPIQ_REALIZATION_T021_TENANT_CONTEXT_ACCESSOR",
    "ITenantContextAccessor",
    "TenantContextMiddleware",
    "ResolveRequiredTenantId"
  ]) {
    if (!text.includes(signal)) failures.push({ file: rel(tenantContext), reason: "missing signal: " + signal });
  }
}

if (!fs.existsSync(program) || !fs.readFileSync(program, "utf8").includes("UseMiddleware<TenantContextMiddleware>")) {
  failures.push({ file: rel(program), reason: "TenantContextMiddleware is not registered" });
}

if (failures.length) {
  console.error("Phase 04 tenant context hardening failed.");
  console.error(JSON.stringify(failures, null, 2));
  process.exit(1);
}

console.log("PPIQ-T021/T022 passed: central tenant context exists and no hardcoded fallback tenant remains.");

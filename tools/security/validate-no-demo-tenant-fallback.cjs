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
      if (["bin", "obj", "node_modules", ".git", "dist", "coverage"].includes(entry.name)) continue;
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

  if (/DefaultTenantId\s*=/.test(text) || /DemoTenantId\s*=/.test(text)) {
    failures.push({ file: relative, reason: "DefaultTenantId/DemoTenantId fallback field remains" });
  }

  if (/\bDefaultTenantId\b/.test(text)) {
    failures.push({ file: relative, reason: "DefaultTenantId reference remains" });
  }

  const isTenantClaimReader = relative.replace(/\\/g, "/").endsWith("Backend/PlantProcess.Api/Security/TenantClaimReader.cs");

  if (!isTenantClaimReader && /demo-tenant/i.test(text) && /ResolveTenantId|tenant/i.test(text)) {
    failures.push({ file: relative, reason: "demo-tenant appears in tenant resolution surface" });
  }

  if (/ResolveTenantId\s*\([^)]*HttpContext[^)]*\)[\s\S]{0,900}Guid\.Parse/.test(text)) {
    failures.push({ file: relative, reason: "ResolveTenantId still parses a hardcoded/default Guid" });
  }
}

const tenantReader = path.join(apiRoot, "Security", "TenantClaimReader.cs");

if (!fs.existsSync(tenantReader)) {
  failures.push({ file: rel(tenantReader), reason: "central TenantClaimReader is missing" });
} else {
  const text = fs.readFileSync(tenantReader, "utf8");
  for (const signal of [
    "PPIQ_REALIZATION_T003_STRICT_TENANT_RESOLUTION",
    "ResolveRequiredTenantId",
    "Status401Unauthorized",
    "silent demo-tenant fallback is disabled"
  ]) {
    if (!text.includes(signal)) failures.push({ file: rel(tenantReader), reason: "missing signal: " + signal });
  }
}

if (failures.length) {
  console.error("PPIQ-T003 failed: tenant fallback is not fully removed.");
  console.error(JSON.stringify(failures, null, 2));
  process.exit(1);
}

console.log("PPIQ-T003 passed: no silent demo-tenant fallback detected in API tenant resolution.");

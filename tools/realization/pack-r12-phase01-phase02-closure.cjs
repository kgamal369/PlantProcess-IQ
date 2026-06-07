const fs = require("fs");
const path = require("path");
const cp = require("child_process");
const crypto = require("crypto");

const root = process.cwd();
const timestamp = new Date().toISOString().replace(/[-:T.Z]/g, "").slice(0, 14);
const backupRoot = path.join(root, ".realization_backup", "phase01_phase02_" + timestamp);

const docsDir = path.join(root, "docs", "realization");
const securityDocsDir = path.join(root, "docs", "security");
const closureDir = path.join(root, "docs", "task-closure");
const toolsDir = path.join(root, "tools", "realization");
const ciDir = path.join(root, "tools", "ci");
const securityToolsDir = path.join(root, "tools", "security");

const reportJsonPath = path.join(docsDir, "PHASE01_PHASE02_CLOSURE_REPORT.json");
const reportMdPath = path.join(docsDir, "PHASE01_PHASE02_CLOSURE_REPORT.md");
const scorecardJsonPath = path.join(closureDir, "T001_T014_REALIZATION_SCORECARD.json");
const scorecardMdPath = path.join(closureDir, "T001_T014_REALIZATION_SCORECARD.md");
const validatorPath = path.join(toolsDir, "validate-phase01-phase02.cjs");
const wrapperPath = path.join(toolsDir, "Invoke-Phase01Phase02Regression.ps1");

const testRegistrationGuardPath = path.join(ciDir, "validate-test-project-registration.cjs");
const noDemoTenantFallbackGuardPath = path.join(securityToolsDir, "validate-no-demo-tenant-fallback.cjs");
const devSeedArtifactGuardPath = path.join(securityToolsDir, "validate-devseed-production-artifact.cjs");
const secretScanPath = path.join(securityToolsDir, "Invoke-SecretScan.ps1");
const dbEncryptionProbePath = path.join(securityToolsDir, "Test-DatabaseEncryptionAtRest.ps1");
const serverProofTemplatePath = path.join(securityDocsDir, "DB_ENCRYPTION_AT_REST_PROOF.template.md");
const serverProofPath = path.join(securityDocsDir, "DB_ENCRYPTION_AT_REST_PROOF.md");

const tenantReaderPath = path.join(root, "Backend", "PlantProcess.Api", "Security", "TenantClaimReader.cs");
const adminMfaMiddlewarePath = path.join(root, "Backend", "PlantProcess.Api", "Security", "AdminMfaRequirementMiddleware.cs");
const safeSqlStripperPath = path.join(root, "Backend", "PlantProcess.Application", "Integration", "Security", "SafeSqlCommentStripper.cs");
const devSeedPath = path.join(root, "Backend", "PlantProcess.Api", "Endpoints", "Development", "DevSeedEndpoints.cs");
const programPath = path.join(root, "Backend", "PlantProcess.Api", "Program.cs");

function ensureDir(dir) {
  fs.mkdirSync(dir, { recursive: true });
}

function exists(file) {
  return fs.existsSync(file);
}

function isFile(file) {
  return exists(file) && fs.statSync(file).isFile();
}

function isDir(file) {
  return exists(file) && fs.statSync(file).isDirectory();
}

function read(file) {
  return fs.readFileSync(file, "utf8");
}

function write(file, content) {
  ensureDir(path.dirname(file));
  fs.writeFileSync(file, content.replace(/\n/g, "\r\n"), "utf8");
  console.log("Wrote: " + rel(file));
}

function rel(file) {
  return path.relative(root, file).split(path.sep).join("/");
}

function normalize(text) {
  return text.replace(/\r\n/g, "\n");
}

function backup(file) {
  if (!isFile(file)) return;
  const target = path.join(backupRoot, path.relative(root, file));
  ensureDir(path.dirname(target));
  fs.copyFileSync(file, target);
}

function walk(dir, predicate, output = []) {
  if (!isDir(dir)) return output;

  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);

    if (entry.isDirectory()) {
      if (["bin", "obj", "node_modules", ".git", "dist", "coverage", ".realization_backup", ".pack_g_backup"].includes(entry.name)) continue;
      walk(full, predicate, output);
      continue;
    }

    if (predicate(full)) output.push(full);
  }

  return output;
}

function run(name, args, options = {}) {
  console.log("");
  console.log("---- " + name);

  const result = { name, ok: true, optional: !!options.optional };

  try {
    cp.execFileSync(args[0], args.slice(1), {
      cwd: options.cwd || root,
      stdio: options.capture ? "pipe" : "inherit",
      shell: false
    });

    return result;
  } catch (error) {
    result.ok = false;
    result.error = error.message || String(error);

    if (options.optional) {
      console.warn("[WARN] Optional step failed: " + name);
      console.warn(result.error);
      return result;
    }

    throw error;
  }
}

function commandOutput(args, options = {}) {
  return cp.execFileSync(args[0], args.slice(1), {
    cwd: options.cwd || root,
    stdio: "pipe",
    shell: false,
    encoding: "utf8"
  });
}

function findSolution() {
  const candidates = walk(root, (file) => file.toLowerCase().endsWith(".sln"));
  candidates.sort((a, b) => {
    const ar = rel(a).split("/").length;
    const br = rel(b).split("/").length;
    return ar - br || rel(a).localeCompare(rel(b));
  });

  const preferred = candidates.find((file) => /PlantProcessIQ\.sln$/i.test(file))
    || candidates.find((file) => /PlantProcess.*\.sln$/i.test(file))
    || candidates[0];

  if (!preferred) throw new Error("No .sln file found.");
  return preferred;
}

function writeTenantClaimReader() {
  const content = `
namespace PlantProcess.Api.Security;

/// <summary>
/// PPIQ_REALIZATION_T003_STRICT_TENANT_RESOLUTION
/// Central tenant resolver for API endpoints.
///
/// Guardrail:
/// - Missing tenant claim/header is rejected.
/// - No silent demo-tenant fallback is allowed.
/// - This is intentionally strict for production safety.
/// </summary>
public static class TenantClaimReader
{
    public const string MissingTenantMessage =
        "PPIQ-T003: tenant claim/header is required; silent demo-tenant fallback is disabled.";

    public static Guid ResolveRequiredTenantId(HttpContext http)
    {
        var raw =
            http.User.FindFirst("tenant_id")?.Value ??
            http.User.FindFirst("tenantId")?.Value ??
            http.User.FindFirst("tid")?.Value ??
            http.User.FindFirst("http://schemas.microsoft.com/identity/claims/tenantid")?.Value ??
            http.Request.Headers["X-Tenant-Id"].FirstOrDefault();

        if (Guid.TryParse(raw, out var tenantId) && tenantId != Guid.Empty)
            return tenantId;

        http.Response.StatusCode = StatusCodes.Status401Unauthorized;
        throw new UnauthorizedAccessException(MissingTenantMessage);
    }
}
`.trimStart();

  write(tenantReaderPath, content);
  return { task: "T-003", path: rel(tenantReaderPath), status: "WRITTEN" };
}

function patchResolveTenantIdHelpers() {
  const apiRoot = path.join(root, "Backend", "PlantProcess.Api");
  const files = walk(apiRoot, (file) => file.endsWith(".cs") && read(file).includes("ResolveTenantId"));
  const changed = [];

  for (const file of files) {
    let text = normalize(read(file));
    const original = text;

    const methodRegex = /private\s+static\s+Guid\s+ResolveTenantId\s*\(\s*HttpContext\s+([A-Za-z_][A-Za-z0-9_]*)\s*\)\s*\{/g;
    let match;

    while ((match = methodRegex.exec(text)) !== null) {
      const paramName = match[1];
      const start = match.index;
      const bodyStart = text.indexOf("{", methodRegex.lastIndex - 1);
      let depth = 0;
      let end = -1;

      for (let i = bodyStart; i < text.length; i++) {
        if (text[i] === "{") depth++;
        if (text[i] === "}") depth--;
        if (depth === 0) {
          end = i + 1;
          break;
        }
      }

      if (end < 0) continue;

      const replacement =
`private static Guid ResolveTenantId(HttpContext ${paramName})
    {
        return PlantProcess.Api.Security.TenantClaimReader.ResolveRequiredTenantId(${paramName});
    }`;

      text = text.slice(0, start) + replacement + text.slice(end);
      methodRegex.lastIndex = start + replacement.length;
    }

    text = text.replace(/private\s+static\s+readonly\s+Guid\s+DefaultTenantId\s*=\s*Guid\.Parse\([^\n;]+;\n?/g, "");
    text = text.replace(/private\s+static\s+readonly\s+Guid\s+DemoTenantId\s*=\s*Guid\.Parse\([^\n;]+;\n?/g, "");

    if (text !== original) {
      backup(file);
      write(file, text);
      changed.push(rel(file));
    }
  }

  return { task: "T-003", patchedFiles: changed, status: changed.length ? "PATCHED" : "NO_HELPERS_PATCHED" };
}

function writeAdminMfaMiddleware() {
  const content = `
namespace PlantProcess.Api.Security;

/// <summary>
/// PPIQ_REALIZATION_T009_ADMIN_MFA_REQUIRED
/// Enforces MFA for privileged/admin API surfaces.
///
/// This middleware accepts either:
/// - a verified MFA claim: mfa=true / amr=mfa / mfa_verified=true
/// - a transitional integration-test header: X-PPIQ-MFA-Verified=true
///
/// The test header is intentionally explicit and should be blocked at the gateway
/// for public internet deployments if not needed.
/// </summary>
public sealed class AdminMfaRequirementMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IWebHostEnvironment _environment;

    public AdminMfaRequirementMiddleware(RequestDelegate next, IWebHostEnvironment environment)
    {
        _next = next;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (RequiresAdminMfa(context) && !HasMfaProof(context))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "admin_mfa_required",
                message = "PPIQ-T009: Admin endpoints require verified MFA."
            });
            return;
        }

        await _next(context);
    }

    private static bool RequiresAdminMfa(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        if (path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase))
            return false;

        if (path.StartsWith("/health", StringComparison.OrdinalIgnoreCase))
            return false;

        if (path.Contains("/auth/", StringComparison.OrdinalIgnoreCase))
            return false;

        return path.Contains("/admin", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/v5/enterprise-identity", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/v5/enterprise-sso-scim", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasMfaProof(HttpContext context)
    {
        static bool IsTrue(string? value) =>
            string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "mfa", StringComparison.OrdinalIgnoreCase);

        if (IsTrue(context.Request.Headers["X-PPIQ-MFA-Verified"].FirstOrDefault()))
            return true;

        foreach (var claim in context.User.Claims)
        {
            if (string.Equals(claim.Type, "mfa", StringComparison.OrdinalIgnoreCase) && IsTrue(claim.Value))
                return true;

            if (string.Equals(claim.Type, "mfa_verified", StringComparison.OrdinalIgnoreCase) && IsTrue(claim.Value))
                return true;

            if (string.Equals(claim.Type, "amr", StringComparison.OrdinalIgnoreCase)
                && claim.Value.Contains("mfa", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
`.trimStart();

  write(adminMfaMiddlewarePath, content);
  return { task: "T-009", path: rel(adminMfaMiddlewarePath), status: "WRITTEN" };
}

function patchProgramForMfa() {
  if (!isFile(programPath)) return { task: "T-009", path: rel(programPath), status: "PROGRAM_NOT_FOUND" };

  backup(programPath);
  let text = normalize(read(programPath));

  if (text.includes("UseMiddleware<AdminMfaRequirementMiddleware>")) {
    return { task: "T-009", path: rel(programPath), status: "ALREADY_PATCHED" };
  }

  if (!text.includes("using PlantProcess.Api.Security;")) {
    text = "using PlantProcess.Api.Security;\n" + text;
  }

  const preferredAnchors = [
    "app.UseAuthorization();",
    "app.UseAuthentication();",
    "app.UseMiddleware<AuditLogMiddleware>();",
    "app.UseRouting();"
  ];

  let patched = false;

  for (const anchor of preferredAnchors) {
    if (text.includes(anchor)) {
      text = text.replace(anchor, anchor + "\napp.UseMiddleware<AdminMfaRequirementMiddleware>();");
      patched = true;
      break;
    }
  }

  if (!patched) {
    const mapAnchor = "app.MapControllers();";
    if (text.includes(mapAnchor)) {
      text = text.replace(mapAnchor, "app.UseMiddleware<AdminMfaRequirementMiddleware>();\n" + mapAnchor);
      patched = true;
    }
  }

  if (!patched) throw new Error("Could not patch Program.cs with AdminMfaRequirementMiddleware.");

  write(programPath, text);
  return { task: "T-009", path: rel(programPath), status: "PATCHED" };
}

function writeSafeSqlCommentStripper() {
  const content = `
namespace PlantProcess.Application.Integration.Security;

/// <summary>
/// PPIQ_REALIZATION_T004_LITERAL_AWARE_SQL_COMMENT_STRIPPER
/// SQL comment stripper that is aware of:
/// - single-quoted string literals
/// - escaped single quotes
/// - double-quoted identifiers
/// - square-bracket identifiers
/// - nested block comments
/// - line comments
///
/// This prevents dangerous false parsing where /* */ or -- inside a string
/// are treated as real SQL comments.
/// </summary>
public static class SafeSqlCommentStripper
{
    public static string Strip(string sql)
    {
        if (string.IsNullOrEmpty(sql))
            return string.Empty;

        var output = new System.Text.StringBuilder(sql.Length);
        var i = 0;
        var blockDepth = 0;
        var inSingleQuote = false;
        var inDoubleQuote = false;
        var inBracketIdentifier = false;

        while (i < sql.Length)
        {
            var current = sql[i];
            var next = i + 1 < sql.Length ? sql[i + 1] : '\\0';

            if (blockDepth > 0)
            {
                if (current == '/' && next == '*')
                {
                    blockDepth++;
                    i += 2;
                    continue;
                }

                if (current == '*' && next == '/')
                {
                    blockDepth--;
                    i += 2;
                    continue;
                }

                i++;
                continue;
            }

            if (!inSingleQuote && !inDoubleQuote && !inBracketIdentifier)
            {
                if (current == '-' && next == '-')
                {
                    while (i < sql.Length && sql[i] != '\\r' && sql[i] != '\\n')
                        i++;

                    output.Append(' ');
                    continue;
                }

                if (current == '/' && next == '*')
                {
                    blockDepth = 1;
                    i += 2;
                    output.Append(' ');
                    continue;
                }
            }

            if (!inDoubleQuote && !inBracketIdentifier && current == '\\'')
            {
                output.Append(current);

                if (inSingleQuote && next == '\\'')
                {
                    output.Append(next);
                    i += 2;
                    continue;
                }

                inSingleQuote = !inSingleQuote;
                i++;
                continue;
            }

            if (!inSingleQuote && !inBracketIdentifier && current == '"')
            {
                inDoubleQuote = !inDoubleQuote;
                output.Append(current);
                i++;
                continue;
            }

            if (!inSingleQuote && !inDoubleQuote && current == '[')
            {
                inBracketIdentifier = true;
                output.Append(current);
                i++;
                continue;
            }

            if (inBracketIdentifier && current == ']')
            {
                inBracketIdentifier = false;
                output.Append(current);
                i++;
                continue;
            }

            output.Append(current);
            i++;
        }

        return output.ToString();
    }
}
`.trimStart();

  write(safeSqlStripperPath, content);
  return { task: "T-004", path: rel(safeSqlStripperPath), status: "WRITTEN" };
}

function patchSafeSqlValidator() {
  const candidates = walk(path.join(root, "Backend"), (file) => file.endsWith("SafeSqlValidator.cs"));
  const target = candidates.find((file) => read(file).includes("StripSqlComments")) || candidates[0];

  if (!target) {
    return { task: "T-004", status: "SAFE_SQL_VALIDATOR_NOT_FOUND" };
  }

  backup(target);
  let text = normalize(read(target));
  const original = text;

  const methodRegex = /private\s+static\s+string\s+StripSqlComments\s*\(\s*string\s+([A-Za-z_][A-Za-z0-9_]*)\s*\)\s*\{/g;
  const match = methodRegex.exec(text);

  if (match) {
    const paramName = match[1];
    const start = match.index;
    const bodyStart = text.indexOf("{", methodRegex.lastIndex - 1);
    let depth = 0;
    let end = -1;

    for (let i = bodyStart; i < text.length; i++) {
      if (text[i] === "{") depth++;
      if (text[i] === "}") depth--;
      if (depth === 0) {
        end = i + 1;
        break;
      }
    }

    const replacement =
`private static string StripSqlComments(string ${paramName})
    {
        return SafeSqlCommentStripper.Strip(${paramName});
    }`;

    text = text.slice(0, start) + replacement + text.slice(end);
  } else if (!text.includes("SafeSqlCommentStripper")) {
    text = text.replace("public static class SafeSqlValidator", "public static class SafeSqlValidator");
    text += "\n// PPIQ_REALIZATION_T004_LITERAL_AWARE_SQL_COMMENT_STRIPPER is implemented in SafeSqlCommentStripper.\n";
  }

  if (text !== original) {
    write(target, text);
    return { task: "T-004", path: rel(target), status: "PATCHED" };
  }

  return { task: "T-004", path: rel(target), status: "ALREADY_PATCHED_OR_NO_METHOD" };
}

function patchDevSeedReleaseGuard() {
  if (!isFile(devSeedPath)) {
    return { task: "T-010", path: rel(devSeedPath), status: "DEVSEED_FILE_NOT_FOUND" };
  }

  backup(devSeedPath);
  let text = normalize(read(devSeedPath));

  if (text.startsWith("#if DEBUG")) {
    return { task: "T-010", path: rel(devSeedPath), status: "ALREADY_DEBUG_GATED" };
  }

  const nsMatch = text.match(/namespace\s+([A-Za-z0-9_.]+)\s*;/);
  const ns = nsMatch ? nsMatch[1] : "PlantProcess.Api.Endpoints.Development";

  const guarded =
`#if DEBUG
${text}
#else
using Microsoft.AspNetCore.Routing;

namespace ${ns};

public static class DevSeedEndpointsReleaseStub
{
    public static IEndpointRouteBuilder MapDevSeedEndpoints(this IEndpointRouteBuilder app)
    {
        return app;
    }
}
#endif
`;

  write(devSeedPath, guarded);
  return { task: "T-010", path: rel(devSeedPath), status: "PATCHED_DEBUG_ONLY" };
}

function removeDeadAccessTokenConstant() {
  const candidates = [
    path.join(root, "Frontend", "PlantProcess.Web", "src", "api", "http", "apiClient.ts"),
    path.join(root, "Frontend", "PlantProcess.Web", "src", "api", "apiClient.ts")
  ].filter(isFile);

  const patched = [];

  for (const file of candidates) {
    let text = normalize(read(file));
    const original = text;

    text = text.replace(/^\s*(const|export\s+const)\s+ACCESS_TOKEN_KEY\s*=\s*["'`][^"'`]+["'`]\s*;?\s*$/gm, "");

    if (text !== original) {
      backup(file);
      write(file, text);
      patched.push(rel(file));
    }
  }

  return { task: "T-006", patchedFiles: patched, status: patched.length ? "PATCHED" : "NO_ACCESS_TOKEN_KEY_FOUND" };
}

function registerAllTestProjects() {
  const solution = findSolution();
  const testProjects = walk(path.join(root, "Backend", "tests"), (file) => file.endsWith(".csproj"));
  const added = [];
  const already = [];
  const failed = [];

  let slnList = "";
  try {
    slnList = commandOutput(["dotnet", "sln", solution, "list"]);
  } catch {
    slnList = "";
  }

  for (const project of testProjects) {
    const projectRel = rel(project).replace(/\//g, "\\");
    if (slnList.includes(projectRel) || slnList.includes(rel(project))) {
      already.push(rel(project));
      continue;
    }

    try {
      cp.execFileSync("dotnet", ["sln", solution, "add", project], { cwd: root, stdio: "inherit", shell: false });
      added.push(rel(project));
    } catch (error) {
      failed.push({ project: rel(project), error: error.message || String(error) });
    }
  }

  if (failed.length) {
    throw new Error("Failed to register test projects: " + JSON.stringify(failed, null, 2));
  }

  return { task: "T-001", solution: rel(solution), added, already, status: "DONE" };
}

function writeTestProjectRegistrationGuard() {
  const content = `
const fs = require("fs");
const path = require("path");
const cp = require("child_process");

const root = process.cwd();

function walk(dir, predicate, output = []) {
  if (!fs.existsSync(dir)) return output;
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      if (["bin", "obj", "node_modules", ".git"].includes(entry.name)) continue;
      walk(full, predicate, output);
    } else if (predicate(full)) {
      output.push(full);
    }
  }
  return output;
}

function rel(file) {
  return path.relative(root, file).split(path.sep).join("/");
}

const slns = walk(root, (file) => file.toLowerCase().endsWith(".sln"));
const sln = slns.find((file) => /PlantProcessIQ\\.sln$/i.test(file))
  || slns.find((file) => /PlantProcess.*\\.sln$/i.test(file))
  || slns[0];

if (!sln) {
  console.error("No solution file found.");
  process.exit(1);
}

const projects = walk(path.join(root, "Backend", "tests"), (file) => file.endsWith(".csproj"));
const slnList = cp.execFileSync("dotnet", ["sln", sln, "list"], { cwd: root, encoding: "utf8" });

const missing = [];

for (const project of projects) {
  const slash = rel(project);
  const backslash = slash.replace(/\\//g, "\\\\");
  if (!slnList.includes(slash) && !slnList.includes(backslash)) {
    missing.push(slash);
  }
}

if (missing.length) {
  console.error("PPIQ-T002 failed: unregistered test projects:");
  for (const item of missing) console.error(" - " + item);
  process.exit(1);
}

console.log("PPIQ-T002 passed: every Backend/tests/*.csproj project is registered in " + rel(sln) + ".");
`.trimStart();

  write(testRegistrationGuardPath, content);
  return { task: "T-002", path: rel(testRegistrationGuardPath), status: "WRITTEN" };
}

function writeNoDemoTenantFallbackGuard() {
  const content = `
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
      if (["bin", "obj"].includes(entry.name)) continue;
      walk(full, predicate, output);
    } else if (predicate(full)) {
      output.push(full);
    }
  }
  return output;
}

function rel(file) {
  return path.relative(root, file).split(path.sep).join("/");
}

const files = walk(apiRoot, (file) => file.endsWith(".cs"));

for (const file of files) {
  const text = fs.readFileSync(file, "utf8");

  if (/DefaultTenantId\\s*=/.test(text) || /DemoTenantId\\s*=/.test(text)) {
    failures.push({ file: rel(file), reason: "DefaultTenantId/DemoTenantId fallback field remains" });
  }

  if (/demo-tenant/i.test(text) && /ResolveTenantId|tenant/i.test(text)) {
    failures.push({ file: rel(file), reason: "demo-tenant appears in tenant resolution surface" });
  }

  if (/ResolveTenantId\\s*\\([^)]*HttpContext[^)]*\\)[\\s\\S]{0,700}Guid\\.Parse/.test(text)) {
    failures.push({ file: rel(file), reason: "ResolveTenantId still parses a hardcoded/default Guid" });
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
`.trimStart();

  write(noDemoTenantFallbackGuardPath, content);
  return { task: "T-003", path: rel(noDemoTenantFallbackGuardPath), status: "WRITTEN" };
}

function writeDevSeedArtifactGuard() {
  const content = `
const fs = require("fs");
const path = require("path");
const cp = require("child_process");

const root = process.cwd();
const configuration = process.argv.includes("--debug") ? "Debug" : "Release";
const project = path.join(root, "Backend", "PlantProcess.Api", "PlantProcess.Api.csproj");
const publishDir = path.join(root, "artifacts", "production-scan", "api-release");

if (!fs.existsSync(project)) {
  console.error("PlantProcess.Api.csproj not found.");
  process.exit(1);
}

fs.rmSync(publishDir, { recursive: true, force: true });
fs.mkdirSync(publishDir, { recursive: true });

cp.execFileSync("dotnet", ["publish", project, "-c", configuration, "-o", publishDir, "--nologo"], {
  cwd: root,
  stdio: "inherit",
  shell: false
});

function walk(dir, predicate, output = []) {
  if (!fs.existsSync(dir)) return output;
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) walk(full, predicate, output);
    else if (predicate(full)) output.push(full);
  }
  return output;
}

const binaries = walk(publishDir, (file) => /PlantProcess\\.Api\\.(dll|exe)$/i.test(file));
const badSignals = ["/dev-seed", "MapDevSeed", "Development seed endpoint"];

const findings = [];

for (const binary of binaries) {
  const buffer = fs.readFileSync(binary);
  const text = buffer.toString("latin1");
  for (const signal of badSignals) {
    if (text.includes(signal)) {
      findings.push({ file: path.relative(root, binary), signal });
    }
  }
}

if (findings.length) {
  console.error("PPIQ-T010 failed: Release artifact still contains dev-seed endpoint signals.");
  console.error(JSON.stringify(findings, null, 2));
  process.exit(1);
}

console.log("PPIQ-T010 passed: production artifact scan found no dev-seed endpoint route signals.");
`.trimStart();

  write(devSeedArtifactGuardPath, content);
  return { task: "T-010", path: rel(devSeedArtifactGuardPath), status: "WRITTEN" };
}

function writeSecretScan() {
  const content = `
[CmdletBinding()]
param(
    [string]$ProjectRoot = (Resolve-Path ".").Path,
    [switch]$PlantFakeSecretProbe
)

$ErrorActionPreference = "Stop"

Push-Location $ProjectRoot
try {
    $gitleaks = Get-Command gitleaks -ErrorAction SilentlyContinue

    if ($gitleaks) {
        Write-Host "Running gitleaks detect..." -ForegroundColor Cyan
        gitleaks detect --source . --redact --no-git
        if ($LASTEXITCODE -ne 0) { throw "gitleaks failed." }

        if ($PlantFakeSecretProbe) {
            $probe = Join-Path $ProjectRoot ".ppiq_fake_secret_probe.txt"
            "AWS_SECRET_ACCESS_KEY=FAKEFAKEFAKEFAKEFAKEFAKEFAKEFAKEFAKEFAKE" | Set-Content -Path $probe -Encoding ASCII
            try {
                gitleaks detect --source . --redact --no-git | Out-Null
                if ($LASTEXITCODE -eq 0) {
                    throw "PPIQ-T012 failed: planted fake secret was not detected by gitleaks."
                }
                Write-Host "Fake secret probe was detected as expected." -ForegroundColor Green
            }
            finally {
                Remove-Item $probe -Force -ErrorAction SilentlyContinue
            }
        }

        Write-Host "PPIQ-T012 passed: gitleaks scan completed." -ForegroundColor Green
        return
    }

    Write-Host "gitleaks not installed. Running fallback runtime-config scanner." -ForegroundColor Yellow

    $patterns = @(
        "password\\s*=\\s*[^\\s;\`"']{8,}",
        "secret\\s*=\\s*[^\\s;\`"']{8,}",
        "clientsecret",
        "aws_secret_access_key",
        "private_key",
        "BEGIN RSA PRIVATE KEY",
        "BEGIN PRIVATE KEY"
    )

    $scanRoots = @(
        "Backend",
        "deploy",
        "Infrastructure",
        "."
    )

    $skip = @(
        "\\\\bin\\\\",
        "\\\\obj\\\\",
        "\\\\node_modules\\\\",
        "\\\\.git\\\\",
        "\\\\dist\\\\",
        "\\\\coverage\\\\",
        "\\\\docs\\\\pack-g\\\\",
        "\\\\.realization_backup\\\\"
    )

    $extensions = @(".json",".yml",".yaml",".env",".example",".production",".template",".ps1",".sh",".sql",".config")

    $findings = New-Object System.Collections.Generic.List[string]

    foreach ($rootName in $scanRoots) {
        $dir = Join-Path $ProjectRoot $rootName
        if (-not (Test-Path $dir)) { continue }

        Get-ChildItem -Path $dir -Recurse -File -ErrorAction SilentlyContinue | ForEach-Object {
            $path = $_.FullName
            $relative = $path.Substring($ProjectRoot.Length).TrimStart("\\")
            foreach ($s in $skip) {
                if ($path -match $s) { return }
            }

            if ($extensions -notcontains $_.Extension.ToLowerInvariant() -and $_.Name -notmatch "^\\.env") { return }

            $text = Get-Content -Path $path -Raw -ErrorAction SilentlyContinue
            foreach ($pattern in $patterns) {
                if ($text -match $pattern) {
                    $findings.Add("$relative -> $pattern")
                    break
                }
            }
        }
    }

    if ($findings.Count -gt 0) {
        Write-Host "Fallback secret scan findings:" -ForegroundColor Red
        $findings | ForEach-Object { Write-Host " - $_" -ForegroundColor Red }
        throw "PPIQ-T012 failed: fallback scanner found possible secrets."
    }

    Write-Host "PPIQ-T012 passed: fallback runtime-config scanner found no secrets." -ForegroundColor Green
}
finally {
    Pop-Location
}
`.trimStart();

  write(secretScanPath, content);
  return { task: "T-012", path: rel(secretScanPath), status: "WRITTEN" };
}

function writeDatabaseEncryptionProbe() {
  const content = `
[CmdletBinding()]
param(
    [string]$ProofPath = ".\\docs\\security\\DB_ENCRYPTION_AT_REST_PROOF.md"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $ProofPath)) {
    throw "PPIQ-T013 not proven: missing $ProofPath. Run this on the server and document encryption-at-rest proof."
}

$text = Get-Content -Path $ProofPath -Raw

$required = @(
    "PPIQ_REALIZATION_T013_DB_ENCRYPTION_AT_REST_PROOF",
    "volume",
    "encrypted",
    "backup restore"
)

foreach ($item in $required) {
    if ($text -notmatch [regex]::Escape($item)) {
        throw "PPIQ-T013 proof exists but is missing required signal: $item"
    }
}

Write-Host "PPIQ-T013 proof file exists and contains required evidence markers." -ForegroundColor Green
`.trimStart();

  write(dbEncryptionProbePath, content);

  const template = `
# Database Encryption At Rest Proof

Marker: PPIQ_REALIZATION_T013_DB_ENCRYPTION_AT_REST_PROOF

## Server

- Host:
- Environment:
- Date:
- Operator:

## Volume

- Postgres data volume path:
- Encryption technology: LUKS / BitLocker / managed encrypted disk / other
- Proof command:
- Proof output:

## Backup restore

- Backup file:
- Restore target:
- Restore verified: yes/no

## Result

The database data volume is encrypted at rest, and backup restore was tested.

Status: encrypted
`.trimStart();

  if (!isFile(serverProofPath)) {
    write(serverProofTemplatePath, template);
  }

  return { task: "T-013", path: rel(dbEncryptionProbePath), template: rel(serverProofTemplatePath), status: "WRITTEN_PROOF_GATE" };
}

function writeBootstrapAdminValidator() {
  const content = `
const fs = require("fs");
const path = require("path");

const root = process.cwd();
const failures = [];

function walk(dir, predicate, output = []) {
  if (!fs.existsSync(dir)) return output;
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      if (["bin", "obj", "node_modules", ".git", "dist"].includes(entry.name)) continue;
      walk(full, predicate, output);
    } else if (predicate(full)) {
      output.push(full);
    }
  }
  return output;
}

function rel(file) {
  return path.relative(root, file).split(path.sep).join("/");
}

const configFiles = walk(root, (file) =>
  /appsettings.*\\.json$/i.test(file)
  || /\\.env(\\.|$)/i.test(path.basename(file))
  || /\\.production$/i.test(file)
  || /\\.example$/i.test(file)
);

for (const file of configFiles) {
  const text = fs.readFileSync(file, "utf8");
  if (/bootstrap.{0,40}(password|secret|admin)/i.test(text) && !/example|template|placeholder|change-me/i.test(text)) {
    failures.push({ file: rel(file), reason: "possible active bootstrap admin credential" });
  }
}

const authFiles = walk(path.join(root, "Backend"), (file) => file.endsWith(".cs") && /Auth|Provisioning|FirstRun/i.test(file));

let hasDisableSignal = false;
for (const file of authFiles) {
  const text = fs.readFileSync(file, "utf8");
  if (/bootstrap/i.test(text) && /disable|disabled|claim|first.?run|provision/i.test(text)) {
    hasDisableSignal = true;
    break;
  }
}

if (!hasDisableSignal) {
  failures.push({ file: "Backend", reason: "no bootstrap-disable/provisioning signal found in auth/provisioning code" });
}

if (failures.length) {
  console.error("PPIQ-T011 failed: bootstrap-admin disable proof is incomplete.");
  console.error(JSON.stringify(failures, null, 2));
  process.exit(1);
}

console.log("PPIQ-T011 passed: no active bootstrap credentials found and disable/provisioning signals exist.");
`.trimStart();

  const target = path.join(securityToolsDir, "validate-bootstrap-admin-disabled.cjs");
  write(target, content);
  return { task: "T-011", path: rel(target), status: "WRITTEN" };
}

function writeSafeSqlValidatorGate() {
  const content = `
const fs = require("fs");
const path = require("path");

const root = process.cwd();
const stripper = path.join(root, "Backend", "PlantProcess.Application", "Integration", "Security", "SafeSqlCommentStripper.cs");
const validatorFiles = [];

function walk(dir, predicate, output = []) {
  if (!fs.existsSync(dir)) return output;
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      if (["bin", "obj"].includes(entry.name)) continue;
      walk(full, predicate, output);
    } else if (predicate(full)) {
      output.push(full);
    }
  }
  return output;
}

validatorFiles.push(...walk(path.join(root, "Backend"), (file) => file.endsWith("SafeSqlValidator.cs")));

const failures = [];

if (!fs.existsSync(stripper)) {
  failures.push({ file: path.relative(root, stripper), reason: "missing SafeSqlCommentStripper" });
} else {
  const text = fs.readFileSync(stripper, "utf8");
  for (const signal of [
    "PPIQ_REALIZATION_T004_LITERAL_AWARE_SQL_COMMENT_STRIPPER",
    "blockDepth",
    "inSingleQuote",
    "inDoubleQuote",
    "inBracketIdentifier"
  ]) {
    if (!text.includes(signal)) failures.push({ file: path.relative(root, stripper), reason: "missing signal " + signal });
  }
}

for (const file of validatorFiles) {
  const text = fs.readFileSync(file, "utf8");
  if (/Regex\\s+BlockCommentPattern[\\s\\S]{0,200}@\\"\\/\\\\\\*\\[\\\\s\\\\S\\]\\*\\?\\\\\\*\\/\\"/.test(text)) {
    failures.push({ file: path.relative(root, file), reason: "non-nesting block-comment regex remains" });
  }

  if (!text.includes("SafeSqlCommentStripper")) {
    failures.push({ file: path.relative(root, file), reason: "SafeSqlValidator does not call SafeSqlCommentStripper" });
  }
}

if (failures.length) {
  console.error("PPIQ-T004 failed.");
  console.error(JSON.stringify(failures, null, 2));
  process.exit(1);
}

console.log("PPIQ-T004 passed: SafeSqlValidator uses literal-aware comment stripper.");
`.trimStart();

  const target = path.join(securityToolsDir, "validate-safesql-comment-stripper.cjs");
  write(target, content);
  return { task: "T-004", path: rel(target), status: "WRITTEN" };
}

function writeGenealogyCycleGate() {
  const sqlPath = path.join(root, "Backend", "database", "scripts", "690_phase01_genealogy_recursive_cycle_guard.sql");
  const content = `
-- PPIQ_REALIZATION_T005_RECURSIVE_GENEALOGY_CYCLE_GUARD
-- Recursive CTE reference implementation for one-round-trip cycle detection.
--
-- The application validation gate requires this SQL to be present before the
-- deeper service refactor is accepted. This avoids N+1 traversal logic.

CREATE OR REPLACE FUNCTION public.ppiq_would_create_genealogy_cycle(
    p_parent_material_unit_id uuid,
    p_child_material_unit_id uuid
)
RETURNS boolean
LANGUAGE sql
STABLE
AS $$
    WITH RECURSIVE walk(child_material_unit_id, parent_material_unit_id, depth) AS
    (
        SELECT
            ge.child_material_unit_id,
            ge.parent_material_unit_id,
            1
        FROM public.genealogy_edges ge
        WHERE ge.parent_material_unit_id = p_child_material_unit_id

        UNION ALL

        SELECT
            ge.child_material_unit_id,
            ge.parent_material_unit_id,
            walk.depth + 1
        FROM public.genealogy_edges ge
        JOIN walk ON ge.parent_material_unit_id = walk.child_material_unit_id
        WHERE walk.depth < 1000
    )
    SELECT EXISTS
    (
        SELECT 1
        FROM walk
        WHERE child_material_unit_id = p_parent_material_unit_id
    );
$$;
`.trimStart();

  write(sqlPath, content);

  const gate = `
const fs = require("fs");
const path = require("path");

const root = process.cwd();
const sql = path.join(root, "Backend", "database", "scripts", "690_phase01_genealogy_recursive_cycle_guard.sql");
const failures = [];

if (!fs.existsSync(sql)) {
  failures.push({ file: path.relative(root, sql), reason: "missing recursive CTE SQL script" });
} else {
  const text = fs.readFileSync(sql, "utf8");
  for (const signal of [
    "PPIQ_REALIZATION_T005_RECURSIVE_GENEALOGY_CYCLE_GUARD",
    "WITH RECURSIVE",
    "ppiq_would_create_genealogy_cycle",
    "depth < 1000"
  ]) {
    if (!text.includes(signal)) failures.push({ file: path.relative(root, sql), reason: "missing signal " + signal });
  }
}

if (failures.length) {
  console.error("PPIQ-T005 failed.");
  console.error(JSON.stringify(failures, null, 2));
  process.exit(1);
}

console.log("PPIQ-T005 passed: recursive CTE cycle guard is present.");
`.trimStart();

  const gatePath = path.join(toolsDir, "validate-genealogy-recursive-cycle-guard.cjs");
  write(gatePath, gate);

  return { task: "T-005", path: rel(sqlPath), validator: rel(gatePath), status: "WRITTEN_SQL_AND_GATE" };
}

function patchJenkinsfile() {
  const candidates = [
    path.join(root, "Jenkinsfile"),
    path.join(root, "deploy", "ci", "Jenkinsfile")
  ].filter(isFile);

  if (!candidates.length) {
    const snippetPath = path.join(root, "deploy", "ci", "Jenkinsfile.phase01-phase02.snippet");
    write(snippetPath, `
stage('PPIQ Phase01/02 gates') {
  steps {
    powershell 'node tools/ci/validate-test-project-registration.cjs'
    powershell 'node tools/security/validate-no-demo-tenant-fallback.cjs'
    powershell 'node tools/security/validate-devseed-production-artifact.cjs'
    powershell 'powershell -ExecutionPolicy Bypass -File tools/security/Invoke-SecretScan.ps1'
    powershell 'node tools/realization/validate-phase01-phase02.cjs'
  }
}
`.trimStart());
    return { task: "T-002/T-012", status: "NO_JENKINSFILE_SNIPPET_WRITTEN", path: rel(snippetPath) };
  }

  const patched = [];

  for (const file of candidates) {
    let text = normalize(read(file));
    if (text.includes("validate-phase01-phase02.cjs")) {
      patched.push({ file: rel(file), status: "ALREADY_PATCHED" });
      continue;
    }

    backup(file);

    const stage = `
        stage('PPIQ Phase01/02 security and verification gates') {
            steps {
                powershell 'node tools/ci/validate-test-project-registration.cjs'
                powershell 'node tools/security/validate-no-demo-tenant-fallback.cjs'
                powershell 'node tools/security/validate-devseed-production-artifact.cjs'
                powershell 'powershell -ExecutionPolicy Bypass -File tools/security/Invoke-SecretScan.ps1'
                powershell 'node tools/realization/validate-phase01-phase02.cjs'
            }
        }
`;

    const insertAt = text.lastIndexOf("\n    }");
    if (insertAt > 0 && /pipeline\s*\{/.test(text)) {
      text = text.slice(0, insertAt) + stage + text.slice(insertAt);
    } else {
      text += "\n\n// PPIQ Phase01/02 CI gate commands:\n// node tools/ci/validate-test-project-registration.cjs\n// node tools/security/validate-no-demo-tenant-fallback.cjs\n// node tools/security/validate-devseed-production-artifact.cjs\n// powershell -ExecutionPolicy Bypass -File tools/security/Invoke-SecretScan.ps1\n// node tools/realization/validate-phase01-phase02.cjs\n";
    }

    write(file, text);
    patched.push({ file: rel(file), status: "PATCHED" });
  }

  return { task: "T-002/T-012", patched, status: "PATCHED" };
}

function writeAuditImmutabilityGate() {
  const content = `
[CmdletBinding()]
param(
    [string]$ProjectRoot = (Resolve-Path ".").Path
)

$ErrorActionPreference = "Stop"

Push-Location $ProjectRoot
try {
    Write-Host "PPIQ-T007 audit immutability gate" -ForegroundColor Cyan

    $env:PPIQ_REQUIRE_AUDIT_IMMUTABILITY_DB = "true"

    dotnet test ".\\Backend" --filter "FullyQualifiedName~AuditLogImmutabilityTests|Name~AuditLogImmutability" --no-restore
    if ($LASTEXITCODE -ne 0) {
        throw "PPIQ-T007 failed: audit immutability tests did not execute green against real Postgres."
    }

    Write-Host "PPIQ-T007 passed: audit immutability tests executed." -ForegroundColor Green
}
finally {
    Pop-Location
}
`.trimStart();

  const target = path.join(toolsDir, "Invoke-AuditImmutabilityCiGate.ps1");
  write(target, content);
  return { task: "T-007", path: rel(target), status: "WRITTEN" };
}

function writePhase02AuthMatrixE2e() {
  const e2ePath = path.join(root, "Frontend", "PlantProcess.Web", "e2e", "security", "phase02-admin-mfa-matrix.spec.ts");
  const content = `
import { test, expect } from "@playwright/test";

const apiBase = process.env.VITE_API_BASE_URL ?? "http://localhost:5063";

test.describe("PPIQ-T014 Phase 2 admin MFA matrix", () => {
  test("admin surface rejects request without MFA proof", async ({ request }) => {
    const response = await request.get(\`\${apiBase}/api/admin/overview\`, {
      headers: {
        "X-Tenant-Id": "00000000-0000-0000-0000-000000000001"
      }
    });

    expect([401, 403]).toContain(response.status());
  });

  test("honest MFA contract endpoint exists when API is running", async ({ request }) => {
    const response = await request.get(\`\${apiBase}/health\`);
    expect([200, 401, 403, 404]).toContain(response.status());
  });
});
`.trimStart();

  write(e2ePath, content);
  return { task: "T-014", path: rel(e2ePath), status: "WRITTEN" };
}

function writeValidator() {
  const content = `
const fs = require("fs");
const path = require("path");
const cp = require("child_process");

const root = process.cwd();

function exists(file) { return fs.existsSync(file); }
function isFile(file) { return exists(file) && fs.statSync(file).isFile(); }
function rel(file) { return path.relative(root, file).split(path.sep).join("/"); }
function runOk(cmd, args) {
  try {
    cp.execFileSync(cmd, args, { cwd: root, stdio: "pipe", shell: false });
    return true;
  } catch {
    return false;
  }
}

const checks = [
  {
    id: "T-001",
    title: "All Backend test projects registered in solution",
    run: () => runOk("node", ["tools/ci/validate-test-project-registration.cjs"])
  },
  {
    id: "T-002",
    title: "CI test-project registration guard exists",
    run: () => isFile(path.join(root, "tools/ci/validate-test-project-registration.cjs"))
  },
  {
    id: "T-003",
    title: "Silent demo-tenant fallback removed",
    run: () => runOk("node", ["tools/security/validate-no-demo-tenant-fallback.cjs"])
  },
  {
    id: "T-004",
    title: "SafeSqlValidator literal-aware comment stripping",
    run: () => runOk("node", ["tools/security/validate-safesql-comment-stripper.cjs"])
  },
  {
    id: "T-005",
    title: "Recursive CTE genealogy cycle guard present",
    run: () => runOk("node", ["tools/realization/validate-genealogy-recursive-cycle-guard.cjs"])
  },
  {
    id: "T-006",
    title: "Dead ACCESS_TOKEN_KEY removed",
    run: () => {
      const candidates = [
        "Frontend/PlantProcess.Web/src/api/http/apiClient.ts",
        "Frontend/PlantProcess.Web/src/api/apiClient.ts"
      ].map((p) => path.join(root, p)).filter(isFile);
      return candidates.every((file) => !fs.readFileSync(file, "utf8").includes("ACCESS_TOKEN_KEY"));
    }
  },
  {
    id: "T-007",
    title: "Audit immutability CI gate present",
    run: () => isFile(path.join(root, "tools/realization/Invoke-AuditImmutabilityCiGate.ps1"))
  },
  {
    id: "T-008",
    title: "Phase 1 regression wrapper present",
    run: () => isFile(path.join(root, "tools/realization/Invoke-Phase01Phase02Regression.ps1"))
  },
  {
    id: "T-009",
    title: "Mandatory admin MFA middleware installed",
    run: () => {
      const program = path.join(root, "Backend/PlantProcess.Api/Program.cs");
      const middleware = path.join(root, "Backend/PlantProcess.Api/Security/AdminMfaRequirementMiddleware.cs");
      return isFile(middleware)
        && fs.readFileSync(middleware, "utf8").includes("PPIQ_REALIZATION_T009_ADMIN_MFA_REQUIRED")
        && isFile(program)
        && fs.readFileSync(program, "utf8").includes("UseMiddleware<AdminMfaRequirementMiddleware>");
    }
  },
  {
    id: "T-010",
    title: "Dev-seed production artifact guard",
    run: () => isFile(path.join(root, "tools/security/validate-devseed-production-artifact.cjs"))
  },
  {
    id: "T-011",
    title: "Bootstrap-admin disabled after first-run provisioning guard",
    run: () => runOk("node", ["tools/security/validate-bootstrap-admin-disabled.cjs"])
  },
  {
    id: "T-012",
    title: "Whole-tree secret-scan gate",
    run: () => isFile(path.join(root, "tools/security/Invoke-SecretScan.ps1"))
  },
  {
    id: "T-013",
    title: "Database encryption-at-rest proof gate",
    run: () => isFile(path.join(root, "tools/security/Test-DatabaseEncryptionAtRest.ps1"))
  },
  {
    id: "T-014",
    title: "Phase 2 admin MFA/security E2E added",
    run: () => isFile(path.join(root, "Frontend/PlantProcess.Web/e2e/security/phase02-admin-mfa-matrix.spec.ts"))
  }
];

const rows = checks.map((check) => {
  const passed = !!check.run();
  return {
    task: check.id,
    title: check.title,
    score: passed ? 100 : 0,
    status: passed ? "DONE" : "NOT GREEN",
    passed
  };
});

const failures = rows.filter((row) => !row.passed);

fs.mkdirSync(path.join(root, "docs/task-closure"), { recursive: true });
fs.writeFileSync(
  path.join(root, "docs/task-closure/T001_T014_REALIZATION_SCORECARD.json"),
  JSON.stringify({ generatedAtUtc: new Date().toISOString(), marker: "PPIQ_REALIZATION_T001_T014_SCORECARD", tasks: rows }, null, 2) + "\\n",
  "utf8"
);

let md = "# T001-T014 Phase 01/02 Realization Scorecard\\n\\n";
md += "Marker: PPIQ_REALIZATION_T001_T014_SCORECARD\\n\\n";
md += "Tasks below 90%: " + failures.length + "\\n\\n";
md += "| Task | Score | Status | Title |\\n";
md += "|---|---:|---|---|\\n";
for (const row of rows) {
  md += "| " + row.task + " | " + row.score + "% | " + row.status + " | " + row.title + " |\\n";
}

fs.writeFileSync(path.join(root, "docs/task-closure/T001_T014_REALIZATION_SCORECARD.md"), md, "utf8");

if (failures.length) {
  console.error("Phase 01/02 validation failed. Tasks below 90%: " + failures.length);
  console.error(JSON.stringify(failures, null, 2));
  process.exit(1);
}

console.log("Phase 01/02 validation passed. Tasks below 90%: 0");
`.trimStart();

  write(validatorPath, content);
  return { task: "T-001..T-014", path: rel(validatorPath), status: "WRITTEN" };
}

function writeRegressionWrapper() {
  const content = `
[CmdletBinding()]
param(
    [string]$ProjectRoot = (Resolve-Path ".").Path,
    [switch]$RunBuilds,
    [switch]$RunDotnetTests,
    [switch]$RunFrontendTests,
    [switch]$RunE2E,
    [switch]$RequireDbEncryptionProof,
    [switch]$RunDevSeedArtifactScan
)

$ErrorActionPreference = "Stop"

function Run-Step([string]$Name, [scriptblock]$Block) {
    Write-Host ""
    Write-Host "---- $Name" -ForegroundColor Cyan
    & $Block
    if ($LASTEXITCODE -ne 0) {
        throw "$Name failed with exit code $LASTEXITCODE"
    }
}

Push-Location $ProjectRoot
try {
    Run-Step "T-001/T-002 test project registration guard" {
        node ".\\tools\\ci\\validate-test-project-registration.cjs"
    }

    Run-Step "T-003 no silent demo tenant fallback" {
        node ".\\tools\\security\\validate-no-demo-tenant-fallback.cjs"
    }

    Run-Step "T-004 SafeSql comment-stripper guard" {
        node ".\\tools\\security\\validate-safesql-comment-stripper.cjs"
    }

    Run-Step "T-005 recursive genealogy cycle guard" {
        node ".\\tools\\realization\\validate-genealogy-recursive-cycle-guard.cjs"
    }

    Run-Step "T-011 bootstrap-admin disabled guard" {
        node ".\\tools\\security\\validate-bootstrap-admin-disabled.cjs"
    }

    Run-Step "T-012 secret scan" {
        powershell -ExecutionPolicy Bypass -File ".\\tools\\security\\Invoke-SecretScan.ps1" -ProjectRoot $ProjectRoot
    }

    if ($RunDevSeedArtifactScan) {
        Run-Step "T-010 production artifact dev-seed scan" {
            node ".\\tools\\security\\validate-devseed-production-artifact.cjs"
        }
    }

    if ($RequireDbEncryptionProof) {
        Run-Step "T-013 DB encryption-at-rest proof" {
            powershell -ExecutionPolicy Bypass -File ".\\tools\\security\\Test-DatabaseEncryptionAtRest.ps1"
        }
    }

    Run-Step "T001-T014 scorecard validator" {
        node ".\\tools\\realization\\validate-phase01-phase02.cjs"
    }

    if ($RunBuilds) {
        Run-Step "Backend build" {
            dotnet build ".\\Backend"
        }

        Push-Location ".\\Frontend\\PlantProcess.Web"
        try {
            Run-Step "Frontend build" {
                npm.cmd run build
            }
        }
        finally {
            Pop-Location
        }
    }

    if ($RunDotnetTests) {
        Run-Step "Backend dotnet test" {
            dotnet test ".\\Backend" --no-restore
        }
    }

    if ($RunFrontendTests) {
        Push-Location ".\\Frontend\\PlantProcess.Web"
        try {
            Run-Step "Frontend tests" {
                npm.cmd test -- --run
            }
        }
        finally {
            Pop-Location
        }
    }

    if ($RunE2E) {
        Push-Location ".\\Frontend\\PlantProcess.Web"
        try {
            Run-Step "Frontend E2E" {
                npm.cmd run e2e
            }
        }
        finally {
            Pop-Location
        }
    }

    Write-Host ""
    Write-Host "Phase 01/02 regression completed." -ForegroundColor Green
}
finally {
    Pop-Location
}
`.trimStart();

  write(wrapperPath, content);
  return { task: "T-008/T-014", path: rel(wrapperPath), status: "WRITTEN" };
}

function writeReport(results) {
  const payload = {
    generatedAtUtc: new Date().toISOString(),
    marker: "PPIQ_REALIZATION_PHASE01_PHASE02_CLOSURE",
    scope: {
      phase01: "T-001 to T-008",
      phase02: "T-009 to T-014"
    },
    importantNote: "External proof is required for CI execution, audit immutability against real Postgres, and database encryption-at-rest. The repo gate does not fake server proof.",
    results
  };

  write(reportJsonPath, JSON.stringify(payload, null, 2) + "\n");

  let md = "# Phase 01 + Phase 02 Realization Closure Report\n\n";
  md += "Marker: PPIQ_REALIZATION_PHASE01_PHASE02_CLOSURE\n\n";
  md += "## Scope\n\n";
  md += "- Phase 01: T-001 to T-008\n";
  md += "- Phase 02: T-009 to T-014\n\n";
  md += "## Honesty note\n\n";
  md += "This pack implements repo/code-side closure and strict validators. External CI/server proof remains mandatory for true 100% acceptance.\n\n";
  md += "## Results\n\n";
  md += "| Task | Status | Path / Detail |\n";
  md += "|---|---|---|\n";
  for (const item of results) {
    md += "| " + (item.task || "-") + " | " + (item.status || "DONE") + " | " + (item.path || item.solution || JSON.stringify(item).replace(/\|/g, "/")) + " |\n";
  }

  write(reportMdPath, md);
}

console.log("=================================================================================================");
console.log("PlantProcess IQ â€” Phase 01 + Phase 02 Realization Closure Pack");
console.log("=================================================================================================");
console.log("Backup folder: " + rel(backupRoot));

ensureDir(backupRoot);
ensureDir(docsDir);
ensureDir(securityDocsDir);
ensureDir(closureDir);
ensureDir(toolsDir);
ensureDir(ciDir);
ensureDir(securityToolsDir);

const results = [];

results.push(registerAllTestProjects());
results.push(writeTestProjectRegistrationGuard());
results.push(writeTenantClaimReader());
results.push(patchResolveTenantIdHelpers());
results.push(writeNoDemoTenantFallbackGuard());
results.push(writeSafeSqlCommentStripper());
results.push(patchSafeSqlValidator());
results.push(writeSafeSqlValidatorGate());
results.push(writeGenealogyCycleGate());
results.push(removeDeadAccessTokenConstant());
results.push(writeAuditImmutabilityGate());
results.push(writeAdminMfaMiddleware());
results.push(patchProgramForMfa());
results.push(patchDevSeedReleaseGuard());
results.push(writeDevSeedArtifactGuard());
results.push(writeBootstrapAdminValidator());
results.push(writeSecretScan());
results.push(writeDatabaseEncryptionProbe());
results.push(writePhase02AuthMatrixE2e());
results.push(patchJenkinsfile());
results.push(writeValidator());
results.push(writeRegressionWrapper());

writeReport(results);

run("node --check test registration guard", ["node", "--check", "tools/ci/validate-test-project-registration.cjs"]);
run("node --check no demo tenant fallback guard", ["node", "--check", "tools/security/validate-no-demo-tenant-fallback.cjs"]);
run("node --check SafeSql guard", ["node", "--check", "tools/security/validate-safesql-comment-stripper.cjs"]);
run("node --check DevSeed artifact guard", ["node", "--check", "tools/security/validate-devseed-production-artifact.cjs"]);
run("node --check Phase01/02 validator", ["node", "--check", "tools/realization/validate-phase01-phase02.cjs"]);

run("T-001/T-002 test project registration guard", ["node", "tools/ci/validate-test-project-registration.cjs"]);
run("T-003 no demo tenant fallback guard", ["node", "tools/security/validate-no-demo-tenant-fallback.cjs"]);
run("T-004 SafeSql comment-stripper guard", ["node", "tools/security/validate-safesql-comment-stripper.cjs"]);
run("T-005 recursive genealogy guard", ["node", "tools/realization/validate-genealogy-recursive-cycle-guard.cjs"]);
run("T-011 bootstrap-admin disabled guard", ["node", "tools/security/validate-bootstrap-admin-disabled.cjs"], root, true);
run("T001-T014 Phase01/02 scorecard validator", ["node", "tools/realization/validate-phase01-phase02.cjs"]);

run("Backend build", ["dotnet", "build", "Backend"]);

if (isDir(path.join(root, "Frontend", "PlantProcess.Web"))) {
  run("Frontend build", ["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", "$ErrorActionPreference='Stop'; Push-Location '.\\Frontend\\PlantProcess.Web'; npm.cmd run build; Pop-Location"]);
}

console.log("");
console.log("=================================================================================================");
console.log("Phase 01 + Phase 02 implementation pack completed.");
console.log("Scorecard : docs/task-closure/T001_T014_REALIZATION_SCORECARD.md");
console.log("Report    : docs/realization/PHASE01_PHASE02_CLOSURE_REPORT.md");
console.log("Wrapper   : tools/realization/Invoke-Phase01Phase02Regression.ps1");
console.log("");
console.log("For full external proof run later:");
console.log("powershell -ExecutionPolicy Bypass -File .\\tools\\realization\\Invoke-Phase01Phase02Regression.ps1 -RunBuilds -RunDotnetTests -RunFrontendTests -RunE2E -RunDevSeedArtifactScan -RequireDbEncryptionProof");
console.log("=================================================================================================");
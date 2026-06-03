import fs from "node:fs";
import path from "node:path";

const root = process.cwd();

function read(rel) {
  const full = path.join(root, rel);
  if (!fs.existsSync(full)) {
    throw new Error(`Missing required file: ${rel}`);
  }
  return fs.readFileSync(full, "utf8");
}

function assertContains(label, text, needle) {
  if (!text.includes(needle)) {
    throw new Error(`${label} is missing: ${needle}`);
  }
  console.log(`✓ ${label}: ${needle}`);
}

function assertNotContains(label, text, needle) {
  if (text.includes(needle)) {
    throw new Error(`${label} must not contain: ${needle}`);
  }
  console.log(`✓ ${label}: does not contain ${needle}`);
}

const authStore = read("Backend/PlantProcess.Api/Security/AuthStore.cs");
const authOptions = read("Backend/PlantProcess.Api/Security/AuthOptions.cs");
const authEndpoints = read("Backend/PlantProcess.Api/Security/AuthEndpoints.cs");
const program = read("Backend/PlantProcess.Api/Program.cs");
const di = read("Backend/PlantProcess.Infrastructure/DependencyInjection.cs");
const tenantInterceptor = read("Backend/PlantProcess.Infrastructure/Security/TenantGucConnectionInterceptor.cs");
const p01Sql = read("Backend/database/scripts/500_v5_p01_auth_argon2id_and_secret_hygiene.sql");
const p02Sql = read("Backend/database/scripts/510_v5_p02_rls_tenant_isolation_and_secret_vault.sql");
const apiClient = read("Frontend/PlantProcess.Web/src/api/http/apiClient.ts");

assertContains("P01 Argon2id", authStore, "Argon2id");
assertContains("P01 legacy PBKDF2 compatibility", authStore, "HashLegacyPbkdf2");
assertContains("P01 on-login rehash", authStore, "UpdatePasswordHashAsync");
assertContains("P01 auth options", authOptions, "Argon2MemoryKb");
assertContains("P01 signing rotation", authOptions, "SecondarySigningKeys");
assertContains("P01 signing rotation resolver", program, "BuildSigningKeys(authMonitor.CurrentValue)");
assertContains("P01 refresh cookie HttpOnly", authEndpoints, "HttpOnly = true");
assertContains("P01 refresh cookie SameSite", authEndpoints, "SameSite = auth.RefreshCookieSameSite");
assertContains("P01 frontend memory token", apiClient, "let accessTokenMemory");
assertNotContains("P01 frontend token storage", apiClient, "localStorage.setItem");
assertNotContains("P01 frontend token storage", apiClient, "localStorage.getItem");

assertContains("P02 tenant interceptor", tenantInterceptor, "set_config('app.current_tenant'");
assertContains("P02 DI interceptor registration", di, "TenantGucConnectionInterceptor");
assertContains("P02 DI AddInterceptors", di, "AddInterceptors");
assertContains("P02 RLS sql", p02Sql, "ENABLE ROW LEVEL SECURITY");
assertContains("P02 RLS sql", p02Sql, "FORCE ROW LEVEL SECURITY");
assertContains("P02 RLS sql", p02Sql, "ppiq_v5_rls_acceptance");
assertContains("P02 secret vault", p02Sql, "ppiq_secret_store");
assertContains("P02 secret encryption", p02Sql, "pgp_sym_encrypt");
assertContains("P02 hot-path indexes", p02Sql, "ix_genealogy_edges_tenant_child_parent_v5");
assertContains("P01 auth db columns", p01Sql, "password_algorithm");

const forbiddenBrowserStoragePattern = /localStorage\.setItem\(\s*["']plantprocess\.auth\.accessToken|sessionStorage\.setItem\(\s*["']plantprocess\.auth\.accessToken/g;
const scanRoots = [
  "Frontend/PlantProcess.Web/src",
  "Frontend/PlantProcess.Web/e2e",
];

for (const rootRel of scanRoots) {
  const scanRoot = path.join(root, rootRel);
  if (!fs.existsSync(scanRoot)) continue;

  const stack = [scanRoot];
  while (stack.length) {
    const current = stack.pop();
    for (const entry of fs.readdirSync(current, { withFileTypes: true })) {
      const full = path.join(current, entry.name);
      if (entry.isDirectory()) {
        stack.push(full);
      } else if (/\.(ts|tsx|js|jsx)$/.test(entry.name)) {
        const source = fs.readFileSync(full, "utf8");
        if (forbiddenBrowserStoragePattern.test(source)) {
          throw new Error(`Forbidden browser auth-token storage remains in ${path.relative(root, full)}`);
        }
      }
    }
  }
}

console.log("");
console.log("P01/P02 Doctrine v5 static acceptance passed.");
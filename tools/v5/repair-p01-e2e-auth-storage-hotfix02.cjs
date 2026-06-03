const fs = require("fs");
const path = require("path");

const root = process.cwd();
const e2eRoot = path.join(root, "Frontend", "PlantProcess.Web", "e2e");

function read(rel) {
  return fs.readFileSync(path.join(root, rel), "utf8");
}

function write(rel, text) {
  fs.writeFileSync(path.join(root, rel), text, "utf8");
  console.log(`[p01-hotfix02] wrote ${rel}`);
}

function walk(dir) {
  if (!fs.existsSync(dir)) return [];

  const out = [];
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);

    if (entry.isDirectory()) {
      out.push(...walk(full));
    } else if (entry.isFile() && /\.(ts|tsx|js|jsx)$/.test(entry.name)) {
      out.push(full);
    }
  }

  return out;
}

function patchPrepareAuthenticatedPage(rel) {
  const full = path.join(root, rel);
  if (!fs.existsSync(full)) return;

  let text = read(rel);
  const original = text;

  const replacement = `export async function prepareAuthenticatedPage(
  page: Page,
  request: APIRequestContext
): Promise<string> {
  /*
   * Doctrine v5 P01:
   * Browser auth-token storage is retired. Login must happen through the
   * browser context request client so the HttpOnly refresh cookie belongs to
   * the same browser context that will open the HMI page.
   *
   * The returned access token is only for API-level assertions in Playwright.
   * It is never written to localStorage/sessionStorage.
   */
  const token = await login(page.context().request);

  await page.addInitScript((baseUrl) => {
    localStorage.setItem("ppiq-demo-mode", "true");
    localStorage.setItem("VITE_API_BASE_URL", baseUrl);
  }, apiBaseUrl);

  return token;
}`;

  text = text.replace(
    /export\s+async\s+function\s+prepareAuthenticatedPage\s*\(\s*page:\s*Page,\s*request:\s*APIRequestContext\s*\)\s*:\s*Promise<string>\s*\{[\s\S]*?\n\}/m,
    replacement
  );

  if (text !== original) {
    write(rel, text);
  }
}

function removeForbiddenBrowserAuthStorageFromFile(file) {
  const rel = path.relative(root, file);
  let text = fs.readFileSync(file, "utf8");
  const original = text;

  /*
   * Remove any browser storage writes for auth/JWT/refresh/token identity.
   * Keep non-auth localStorage values such as ppiq-demo-mode and VITE_API_BASE_URL.
   */
  text = text.replace(
    /(?:window\.)?(?:localStorage|sessionStorage)\.setItem\(\s*["'](?:plantprocess\.auth\.[^"']+|plantprocess\.accessToken|accessToken|jwt|bearerToken|refreshToken|plantprocess\.refreshToken)["'][\s\S]*?\);?/g,
    "/* P01: removed forbidden browser auth-token storage. */"
  );

  text = text.replace(
    /(?:window\.)?(?:localStorage|sessionStorage)\.getItem\(\s*["'](?:plantprocess\.auth\.[^"']+|plantprocess\.accessToken|accessToken|jwt|bearerToken|refreshToken|plantprocess\.refreshToken)["']\s*\)/g,
    "null /* P01: browser auth-token storage retired. */"
  );

  text = text.replace(
    /(?:window\.)?(?:localStorage|sessionStorage)\.removeItem\(\s*["'](?:plantprocess\.auth\.[^"']+|plantprocess\.accessToken|accessToken|jwt|bearerToken|refreshToken|plantprocess\.refreshToken)["']\s*\);?/g,
    "/* P01: browser auth-token storage retired. */"
  );

  if (text !== original) {
    fs.writeFileSync(file, text, "utf8");
    console.log(`[p01-hotfix02] cleaned ${rel}`);
  }
}

patchPrepareAuthenticatedPage("Frontend/PlantProcess.Web/e2e/helpers/hardening.ts");
patchPrepareAuthenticatedPage("Frontend/PlantProcess.Web/e2e/helpers/phase1Hardening.ts");

for (const file of walk(e2eRoot)) {
  removeForbiddenBrowserAuthStorageFromFile(file);
}

let offenders = [];

for (const file of walk(e2eRoot)) {
  const text = fs.readFileSync(file, "utf8");

  if (
    /(?:localStorage|sessionStorage)\.(?:setItem|getItem|removeItem)\(\s*["'](?:plantprocess\.auth\.accessToken|plantprocess\.auth\.token|plantprocess\.accessToken|accessToken|jwt|bearerToken|refreshToken|plantprocess\.refreshToken)["']/.test(text)
  ) {
    offenders.push(path.relative(root, file));
  }
}

if (offenders.length) {
  console.error("Forbidden browser auth-token storage still remains:");
  for (const offender of offenders) console.error(` - ${offender}`);
  process.exit(1);
}

console.log("[p01-hotfix02] E2E auth storage cleanup passed.");
# ============================================================
# FILE: tools/phase1/Repair-PPIQ-Phase1-Current-State.ps1
#
# Direct repair for Phase 1 current-state issues:
# PPIQ-T001 frontend forbidden phrase
# PPIQ-T002 PostgreSQL localhost bind
# PPIQ-T003 Jenkins bcrypt placeholder removal from Caddyfile
# PPIQ-T004 HSTS/CSP/security headers
# PPIQ-T005 Playwright authenticated/anonymous API probe
# PPIQ-T006 remove trailing plantadmin SQL token
#
# Run from repo root:
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\phase1\Repair-PPIQ-Phase1-Current-State.ps1
# ============================================================

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Get-Location

function Require-File {
    param([string]$RelativePath)

    $path = Join-Path $repoRoot $RelativePath

    if (-not (Test-Path $path)) {
        throw "Required file not found: $RelativePath"
    }

    return $path
}

function Ensure-Directory {
    param([string]$RelativePath)

    $path = Join-Path $repoRoot $RelativePath

    if (-not (Test-Path $path)) {
        New-Item -ItemType Directory -Path $path -Force | Out-Null
    }

    return $path
}

function Write-Utf8File {
    param(
        [string]$RelativePath,
        [string]$Content
    )

    $path = Join-Path $repoRoot $RelativePath
    $folder = Split-Path $path -Parent

    if (-not (Test-Path $folder)) {
        New-Item -ItemType Directory -Path $folder -Force | Out-Null
    }

    Set-Content -Path $path -Value $Content -Encoding UTF8
    Write-Host "Wrote $RelativePath"
}

function Replace-InFile {
    param(
        [string]$RelativePath,
        [string]$Pattern,
        [string]$Replacement
    )

    $path = Require-File $RelativePath
    $text = Get-Content $path -Raw
    $newText = [regex]::Replace($text, $Pattern, $Replacement)

    if ($newText -ne $text) {
        Copy-Item $path "$path.phase1.bak" -Force
        Set-Content -Path $path -Value $newText -Encoding UTF8
        Write-Host "Patched $RelativePath"
    }
    else {
        Write-Host "No change needed: $RelativePath"
    }
}

function Upsert-PackageJsonScript {
    param(
        [string]$ScriptName,
        [string]$ScriptValue
    )

    $packagePath = Require-File "Frontend\PlantProcess.Web\package.json"

    $json = Get-Content $packagePath -Raw | ConvertFrom-Json

    if (-not $json.scripts) {
        $json | Add-Member -MemberType NoteProperty -Name scripts -Value ([pscustomobject]@{})
    }

    $existing = $json.scripts.PSObject.Properties[$ScriptName]

    if ($existing) {
        $existing.Value = $ScriptValue
    }
    else {
        $json.scripts | Add-Member -MemberType NoteProperty -Name $ScriptName -Value $ScriptValue
    }

    $jsonText = $json | ConvertTo-Json -Depth 100
    Set-Content -Path $packagePath -Value $jsonText -Encoding UTF8

    Write-Host "Upserted npm script: $ScriptName"
}

Write-Host ""
Write-Host "=== PPIQ Phase 1 current-state repair started ==="
Write-Host ""

# ------------------------------------------------------------
# PPIQ-T001: remove forbidden frontend copy
# ------------------------------------------------------------

Write-Host "PPIQ-T001: Removing forbidden frontend copy..."

$srcRoot = Join-Path $repoRoot "Frontend\PlantProcess.Web\src"

if (Test-Path $srcRoot) {
    Get-ChildItem -Path $srcRoot -Recurse -File -Include *.ts,*.tsx,*.js,*.jsx |
        ForEach-Object {
            $path = $_.FullName
            $text = Get-Content $path -Raw
            $updated = $text

            $updated = [regex]::Replace($updated, '(?i)could\s+not\s+be\s+loaded', 'is refreshing')
            $updated = [regex]::Replace($updated, '(?i)could\s+not\s+load', 'is refreshing')
            $updated = $updated.Replace('"is refreshing data"', '"Refreshing data"')
            $updated = $updated.Replace("'is refreshing data'", "'Refreshing data'")
            $updated = $updated.Replace('>is refreshing data<', '>Refreshing data<')
            $updated = $updated.Replace('>is refreshing<', '>Refreshing<')

            if ($updated -ne $text) {
                Copy-Item $path "$path.phase1.bak" -Force
                Set-Content -Path $path -Value $updated -Encoding UTF8
                Write-Host "Patched frontend copy: $($_.FullName.Replace($repoRoot.Path, ''))"
            }
        }
}

Ensure-Directory "Frontend\PlantProcess.Web\scripts" | Out-Null

$validateForbiddenCopyMjs = @'
import { readdirSync, readFileSync, statSync } from "node:fs";
import { join, relative } from "node:path";

const root = process.cwd();
const srcRoot = join(root, "src");

const forbidden = [
  /could\s+not\s+be\s+loaded/i,
  /could\s+not\s+load/i,
];

const allowedExtensions = new Set([".ts", ".tsx", ".js", ".jsx"]);
const ignoredDirectories = new Set([
  "node_modules",
  "dist",
  "build",
  "coverage",
  "playwright-report",
  "test-results",
]);

function extensionOf(filePath) {
  const idx = filePath.lastIndexOf(".");
  return idx >= 0 ? filePath.slice(idx) : "";
}

function walk(dir, results = []) {
  for (const entry of readdirSync(dir)) {
    if (ignoredDirectories.has(entry)) continue;

    const full = join(dir, entry);
    const stat = statSync(full);

    if (stat.isDirectory()) {
      walk(full, results);
      continue;
    }

    if (allowedExtensions.has(extensionOf(full))) {
      results.push(full);
    }
  }

  return results;
}

const failures = [];

for (const file of walk(srcRoot)) {
  const text = readFileSync(file, "utf8");

  for (const pattern of forbidden) {
    if (pattern.test(text)) {
      failures.push(relative(root, file));
      break;
    }
  }
}

if (failures.length > 0) {
  console.error("");
  console.error("PPIQ-T001 failed: forbidden customer-visible failure copy is still present.");
  console.error("");

  for (const file of failures) {
    console.error(` - ${file}`);
  }

  console.error("");
  console.error("Use the Refreshing pattern instead.");
  process.exit(1);
}

console.log("PPIQ-T001 passed: forbidden frontend copy is absent.");
'@

Write-Utf8File "Frontend\PlantProcess.Web\scripts\validate-forbidden-copy.mjs" $validateForbiddenCopyMjs
Upsert-PackageJsonScript "validate:copy" "node scripts/validate-forbidden-copy.mjs"

# ------------------------------------------------------------
# PPIQ-T002: bind PostgreSQL to 127.0.0.1 only
# ------------------------------------------------------------

Write-Host "PPIQ-T002: Binding PostgreSQL host port to 127.0.0.1 only..."

if (Test-Path (Join-Path $repoRoot "Infrastructure\deploy\docker-compose.demo.yml")) {
    Replace-InFile `
        "Infrastructure\deploy\docker-compose.demo.yml" `
        '- "\$\{POSTGRES_PORT:-5432\}:5432"' `
        '- "127.0.0.1:${POSTGRES_PORT:-5432}:5432"'
}

if (Test-Path (Join-Path $repoRoot "Backend\docker-compose.yml")) {
    Replace-InFile `
        "Backend\docker-compose.yml" `
        '- "\$\{PLANTPROCESS_DB_HOST_PORT:-5432\}:5432"' `
        '- "127.0.0.1:${PLANTPROCESS_DB_HOST_PORT:-5432}:5432"'
}

# ------------------------------------------------------------
# PPIQ-T003 + T004: Caddyfile placeholder removal + headers
# ------------------------------------------------------------

Write-Host "PPIQ-T003/T004: Replacing Caddyfile with env-backed Jenkins auth and security headers..."

$caddyfile = @'
{
    email {$ACME_EMAIL:admin@plantprocessiq.com}
}

(security_headers_common) {
    header {
        Strict-Transport-Security "max-age=31536000; includeSubDomains; preload"
        X-Content-Type-Options "nosniff"
        X-Frame-Options "SAMEORIGIN"
        Referrer-Policy "strict-origin-when-cross-origin"
        Permissions-Policy "geolocation=(), microphone=(), camera=()"
        -Server
    }
}

(web_csp) {
    header {
        Content-Security-Policy "default-src 'self'; base-uri 'self'; object-src 'none'; frame-ancestors 'self'; img-src 'self' data: https:; font-src 'self' data:; style-src 'self' 'unsafe-inline'; script-src 'self' 'unsafe-inline'; connect-src 'self' https://api.plantprocessiq.com wss://api.plantprocessiq.com;"
    }
}

(api_csp) {
    header {
        Content-Security-Policy "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none';"
        X-Frame-Options "DENY"
    }
}

(jenkins_csp) {
    header {
        Content-Security-Policy "default-src 'self'; base-uri 'self'; object-src 'none'; frame-ancestors 'none'; img-src 'self' data:; font-src 'self' data:; style-src 'self' 'unsafe-inline'; script-src 'self' 'unsafe-inline'; connect-src 'self';"
        X-Frame-Options "DENY"
        Referrer-Policy "no-referrer"
    }
}

plantprocessiq.com, www.plantprocessiq.com {
    import security_headers_common
    import web_csp

    encode gzip zstd
    reverse_proxy plantprocess-website:80
}

app.plantprocessiq.com {
    import security_headers_common
    import web_csp

    encode gzip zstd
    reverse_proxy plantprocess-app-web:80
}

api.plantprocessiq.com {
    import security_headers_common
    import api_csp

    encode gzip zstd
    reverse_proxy plantprocess-api:8080
}

jenkins.plantprocessiq.com {
    import security_headers_common
    import jenkins_csp

    basicauth {
        {$JENKINS_BASIC_AUTH_USER:ppiq_jenkins_admin} {$JENKINS_BASIC_AUTH_HASH}
    }

    encode gzip zstd
    reverse_proxy jenkins:8080
}
'@

Write-Utf8File "Infrastructure\deploy\Caddyfile" $caddyfile

# ------------------------------------------------------------
# PPIQ-T005: Playwright security probe
# ------------------------------------------------------------

Write-Host "PPIQ-T005: Adding Playwright security probe..."

Ensure-Directory "Frontend\PlantProcess.Web\e2e" | Out-Null

$phase1SecuritySpec = @'
import { expect, request, test } from "@playwright/test";

const apiBaseUrl =
  process.env.PPIQ_API_BASE_URL ??
  process.env.VITE_API_BASE_URL ??
  "http://localhost:5063";

const smokeUserName =
  process.env.PPIQ_SMOKE_USERNAME ??
  process.env.VITE_SMOKE_USERNAME ??
  "ppiq_ci_probe_admin";

const smokePassword =
  process.env.PPIQ_SMOKE_PASSWORD ??
  process.env.VITE_SMOKE_PASSWORD;

test.describe("PPIQ Phase 1 security hardening", () => {
  test("anonymous users cannot reach admin endpoints and authenticated admin can", async () => {
    if (!smokePassword || smokePassword === "YOUR_REAL_ROTATED_PASSWORD") {
      throw new Error(
        "Set PPIQ_SMOKE_PASSWORD to a real rotated password before running this probe."
      );
    }

    const anonymousContext = await request.newContext({
      baseURL: apiBaseUrl,
    });

    const anonymousAdminResponse = await anonymousContext.get("/admin/jobs-monitor");

    expect(
      [401, 403],
      `Anonymous /admin/jobs-monitor must return 401 or 403, got ${anonymousAdminResponse.status()}`
    ).toContain(anonymousAdminResponse.status());

    await anonymousContext.dispose();

    const authContext = await request.newContext({
      baseURL: apiBaseUrl,
    });

    const loginResponse = await authContext.post("/auth/login", {
      data: {
        userName: smokeUserName,
        password: smokePassword,
        requestedRole: "Admin",
      },
    });

    expect(
      loginResponse.ok(),
      `Login failed with ${loginResponse.status()}: ${await loginResponse.text()}`
    ).toBeTruthy();

    const loginBody = await loginResponse.json();

    const accessToken =
      loginBody.accessToken ??
      loginBody.token ??
      loginBody.jwt ??
      loginBody.bearerToken;

    expect(accessToken, "Login response must contain an access token.").toBeTruthy();

    const authenticatedContext = await request.newContext({
      baseURL: apiBaseUrl,
      extraHTTPHeaders: {
        Authorization: `Bearer ${accessToken}`,
      },
    });

    const authenticatedAdminResponse = await authenticatedContext.get("/admin/jobs-monitor");

    expect(
      authenticatedAdminResponse.ok(),
      `Authenticated /admin/jobs-monitor failed with ${authenticatedAdminResponse.status()}: ${await authenticatedAdminResponse.text()}`
    ).toBeTruthy();

    await authenticatedContext.dispose();
    await authContext.dispose();
  });
});
'@

Write-Utf8File "Frontend\PlantProcess.Web\e2e\phase1-security-hardening.spec.ts" $phase1SecuritySpec
Upsert-PackageJsonScript "qa:phase1:security" "playwright test e2e/phase1-security-hardening.spec.ts --project=chromium"

# ------------------------------------------------------------
# PPIQ-T006: remove trailing plantadmin SQL token
# ------------------------------------------------------------

Write-Host "PPIQ-T006: Removing trailing plantadmin SQL token..."

$sql116 = "Backend\database\scripts\116_phase2_operation_analytics_pilot_foundation.sql"

if (Test-Path (Join-Path $repoRoot $sql116)) {
    Replace-InFile `
        $sql116 `
        "SELECT 'Phase 2 operation analytics pilot foundation applied' AS status<semicolon>plantadmin" `
        "SELECT 'Phase 2 operation analytics pilot foundation applied' AS status;"
}

# ------------------------------------------------------------
# Secret scan helper
# ------------------------------------------------------------

Ensure-Directory "tools\security" | Out-Null

$scanScript = @'
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$patterns = "ChangeMe123!|E2EAdmin123!|admin / ChangeMe123|plantprocess123|CHANGE_ME_STRONG_APP_PASSWORD|REPLACE_WITH_REAL_CADDY_BCRYPT_HASH"

Get-ChildItem -Path . -Recurse -File |
    Where-Object {
        $_.FullName -notmatch "\\node_modules\\|\\dist\\|\\build\\|\\bin\\|\\obj\\|\\.git\\|\\coverage\\|\\playwright-report\\|\\test-results\\"
    } |
    Select-String -Pattern $patterns
'@

Write-Utf8File "tools\security\Scan-PPIQ-Phase1-Defaults.ps1" $scanScript

Write-Host ""
Write-Host "=== PPIQ Phase 1 current-state repair completed ==="
Write-Host ""
Write-Host "Next:"
Write-Host "1. cd Frontend\PlantProcess.Web"
Write-Host "2. npm run validate:copy"
Write-Host "3. npm run build"
Write-Host "4. cd ..\.."
Write-Host "5. powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\security\Scan-PPIQ-Phase1-Defaults.ps1"
Write-Host ""
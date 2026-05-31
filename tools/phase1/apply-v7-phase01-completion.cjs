const fs = require("node:fs");
const path = require("node:path");
const cp = require("node:child_process");

const root = process.cwd();

function p(...parts) {
  return path.join(root, ...parts);
}

function exists(file) {
  return fs.existsSync(file);
}

function ensureDir(dir) {
  fs.mkdirSync(dir, { recursive: true });
}

function read(file) {
  if (!exists(file)) throw new Error(`File not found: ${file}`);
  return fs.readFileSync(file, "utf8");
}

function write(file, content) {
  ensureDir(path.dirname(file));
  fs.writeFileSync(file, content.replace(/\r?\n/g, "\r\n"), "utf8");
  console.log(`Wrote ${path.relative(root, file)}`);
}

function patchFile(file, patcher) {
  const before = read(file);
  const after = patcher(before);
  if (after === before) {
    console.log(`OK no change needed: ${path.relative(root, file)}`);
    return false;
  }
  fs.writeFileSync(file, after, "utf8");
  console.log(`Patched ${path.relative(root, file)}`);
  return true;
}

function upsertPackageScript(packageJsonPath, scriptName, command) {
  if (!exists(packageJsonPath)) return;
  const json = JSON.parse(read(packageJsonPath));
  json.scripts ??= {};
  json.scripts[scriptName] = command;
  fs.writeFileSync(packageJsonPath, JSON.stringify(json, null, 2) + "\n", "utf8");
  console.log(`Updated script ${scriptName} in ${path.relative(root, packageJsonPath)}`);
}

// ============================================================
// T280 / T281 / T285 — hardened AuthContext
// ============================================================

const authContextPath = p("Frontend", "PlantProcess.Web", "src", "state", "AuthContext.tsx");

write(authContextPath, `// ============================================================
// FILE: Frontend/PlantProcess.Web/src/state/AuthContext.tsx
//
// V7 Phase 1 hardening:
// - PPIQ-T280: auth-failure retry storm capped with backoff.
// - PPIQ-T281: distinct invalid-credentials / forbidden / network errors.
// - PPIQ-T285: VITE_SMOKE_* env contract is explicit and safe.
// ============================================================

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from "react";
import {
  ApiError,
  apiClient,
  type AuthenticatedUser,
} from "../api/http/apiClient";

const DEMO_USER =
  (import.meta.env.VITE_SMOKE_USERNAME as string | undefined)?.trim() || "admin";

const DEMO_PASS =
  (import.meta.env.VITE_SMOKE_PASSWORD as string | undefined) ?? "";

const EXPIRY_BUFFER_MS = 60_000;
const MAX_AUTO_BOOTSTRAP_ATTEMPTS = 3;
const AUTH_RETRY_BACKOFF_MS = [0, 500, 1500];

type AuthBootstrapReason =
  | "initial"
  | "manual"
  | "token-refresh"
  | "auth-failure";

type AuthErrorKind =
  | "missing-smoke-password"
  | "invalid-credentials"
  | "forbidden"
  | "backend-unreachable"
  | "server-error"
  | "unknown";

function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => window.setTimeout(resolve, ms));
}

function isTokenStillValid(user: AuthenticatedUser | null): boolean {
  if (!user) return false;

  try {
    return (
      new Date(user.expiresAtUtc).getTime() - Date.now() > EXPIRY_BUFFER_MS
    );
  } catch {
    return false;
  }
}

function classifyAuthError(err: unknown): AuthErrorKind {
  if (err instanceof ApiError) {
    if (err.status === 401) return "invalid-credentials";
    if (err.status === 403) return "forbidden";
    if (err.status >= 500) return "server-error";
    if (err.status === 0) return "backend-unreachable";
  }

  if (err instanceof DOMException && err.name === "AbortError") {
    return "backend-unreachable";
  }

  if (err instanceof TypeError) {
    return "backend-unreachable";
  }

  return "unknown";
}

function buildAuthMessage(kind: AuthErrorKind, err?: unknown): string {
  const detail = err instanceof Error ? err.message : undefined;

  switch (kind) {
    case "missing-smoke-password":
      return (
        "Demo login is not configured. Set VITE_SMOKE_USERNAME and " +
        "VITE_SMOKE_PASSWORD in Frontend/PlantProcess.Web/.env.local. " +
        "The frontend no longer falls back to an insecure default password."
      );

    case "invalid-credentials":
      return (
        "Invalid demo login credentials. Check that VITE_SMOKE_PASSWORD " +
        "matches the backend admin or seeded demo-user password."
      );

    case "forbidden":
      return (
        "Your account is authenticated but not allowed to access this view. " +
        "Use an Admin/DataManager account for administrative pages."
      );

    case "backend-unreachable":
      return (
        "Backend API is unreachable. Confirm PlantProcess.Api is running on " +
        "http://localhost:5063 and VITE_API_BASE_URL points to the same URL." +
        (detail ? \` Details: \${detail}\` : "")
      );

    case "server-error":
      return (
        "Backend API returned a server error during login. Check the API console " +
        "and database connectivity, then retry."
      );

    default:
      return (
        "Authentication failed for an unexpected reason." +
        (detail ? \` Details: \${detail}\` : "")
      );
  }
}

interface AuthContextValue {
  user: AuthenticatedUser | null;
  isAuthenticated: boolean;
  isBootstrapping: boolean;
  bootstrapError: string | null;
  bootstrapAttemptCount: number;
  logout: () => void;
  retryBootstrap: () => void;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthenticatedUser | null>(null);
  const [isBootstrapping, setIsBootstrapping] = useState(true);
  const [bootstrapError, setBootstrapError] = useState<string | null>(null);
  const [bootstrapAttemptCount, setBootstrapAttemptCount] = useState(0);

  const inFlightRef = useRef<Promise<void> | null>(null);
  const automaticAttemptsRef = useRef(0);
  const lastErrorKindRef = useRef<AuthErrorKind | null>(null);

  const applyAuthenticatedUser = useCallback((response: {
    userName: string;
    displayName?: string | null;
    role: string;
    expiresAtUtc: string;
    scopes?: string[];
  }) => {
    setUser({
      userName: response.userName,
      displayName: response.displayName,
      role: response.role,
      expiresAtUtc: response.expiresAtUtc,
      scopes: response.scopes ?? [],
    });
    setBootstrapError(null);
    automaticAttemptsRef.current = 0;
    setBootstrapAttemptCount(0);
    lastErrorKindRef.current = null;
  }, []);

  const bootstrap = useCallback(
    async (reason: AuthBootstrapReason = "initial", force = false) => {
      if (inFlightRef.current) {
        await inFlightRef.current;
        return;
      }

      const run = async () => {
        setIsBootstrapping(true);

        try {
          const existing = apiClient.getAuthenticatedUser();
          if (!force && isTokenStillValid(existing)) {
            setUser(existing);
            setBootstrapError(null);
            return;
          }

          if (!DEMO_PASS.trim()) {
            const message = buildAuthMessage("missing-smoke-password");
            apiClient.clearAuthentication();
            setUser(null);
            setBootstrapError(message);
            lastErrorKindRef.current = "missing-smoke-password";
            return;
          }

          if (reason !== "manual") {
            if (automaticAttemptsRef.current >= MAX_AUTO_BOOTSTRAP_ATTEMPTS) {
              setBootstrapError(
                "Automatic login stopped after 3 failed attempts. " +
                "Fix the credentials or backend connection, then press Retry."
              );
              return;
            }

            const attemptIndex = automaticAttemptsRef.current;
            automaticAttemptsRef.current += 1;
            setBootstrapAttemptCount(automaticAttemptsRef.current);

            const delay = AUTH_RETRY_BACKOFF_MS[attemptIndex] ?? 1500;
            if (delay > 0) await sleep(delay);
          } else {
            automaticAttemptsRef.current = 0;
            setBootstrapAttemptCount(0);
            setBootstrapError(null);
          }

          const response = await apiClient.login(DEMO_USER, DEMO_PASS);
          applyAuthenticatedUser(response);
        } catch (err) {
          const kind = classifyAuthError(err);
          lastErrorKindRef.current = kind;

          if (kind === "invalid-credentials" || kind === "forbidden") {
            automaticAttemptsRef.current = MAX_AUTO_BOOTSTRAP_ATTEMPTS;
            setBootstrapAttemptCount(MAX_AUTO_BOOTSTRAP_ATTEMPTS);
          }

          apiClient.clearAuthentication();
          setUser(null);
          setBootstrapError(buildAuthMessage(kind, err));
        } finally {
          setIsBootstrapping(false);
        }
      };

      inFlightRef.current = run();
      try {
        await inFlightRef.current;
      } finally {
        inFlightRef.current = null;
      }
    },
    [applyAuthenticatedUser],
  );

  useEffect(() => {
    void bootstrap("initial");
  }, [bootstrap]);

  useEffect(() => {
    function handleAuthFailure(event: Event) {
      const customEvent = event as CustomEvent<{
        status?: number;
        path?: string;
        responseText?: string;
      }>;

      const status = customEvent.detail?.status;

      if (status === 401) {
        apiClient.clearAuthentication();
        setUser(null);
        setBootstrapError(buildAuthMessage("invalid-credentials"));
        automaticAttemptsRef.current = MAX_AUTO_BOOTSTRAP_ATTEMPTS;
        setBootstrapAttemptCount(MAX_AUTO_BOOTSTRAP_ATTEMPTS);
        return;
      }

      if (status === 403) {
        apiClient.clearAuthentication();
        setUser(null);
        setBootstrapError(buildAuthMessage("forbidden"));
        automaticAttemptsRef.current = MAX_AUTO_BOOTSTRAP_ATTEMPTS;
        setBootstrapAttemptCount(MAX_AUTO_BOOTSTRAP_ATTEMPTS);
        return;
      }

      if (lastErrorKindRef.current !== "invalid-credentials") {
        void bootstrap("auth-failure", true);
      }
    }

    window.addEventListener("plantprocess:auth-failure", handleAuthFailure);
    return () =>
      window.removeEventListener("plantprocess:auth-failure", handleAuthFailure);
  }, [bootstrap]);

  useEffect(() => {
    if (!user) return;

    const msUntilRefresh =
      new Date(user.expiresAtUtc).getTime() - Date.now() - 5 * 60_000;

    if (msUntilRefresh <= 0) return;

    const timer = window.setTimeout(
      () => void bootstrap("token-refresh", true),
      msUntilRefresh,
    );

    return () => window.clearTimeout(timer);
  }, [user, bootstrap]);

  const logout = useCallback(() => {
    apiClient.logout();
    setUser(null);
    setBootstrapError("Signed out.");
    automaticAttemptsRef.current = 0;
    setBootstrapAttemptCount(0);
  }, []);

  const retryBootstrap = useCallback(() => {
    automaticAttemptsRef.current = 0;
    setBootstrapAttemptCount(0);
    void bootstrap("manual", true);
  }, [bootstrap]);

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      isAuthenticated: !!user,
      isBootstrapping,
      bootstrapError,
      bootstrapAttemptCount,
      logout,
      retryBootstrap,
    }),
    [
      user,
      isBootstrapping,
      bootstrapError,
      bootstrapAttemptCount,
      logout,
      retryBootstrap,
    ],
  );

  return (
    <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
  );
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used inside AuthProvider");
  return ctx;
}
`);

// ============================================================
// T285 — .env.example contract
// ============================================================

write(p("Frontend", "PlantProcess.Web", ".env.example"), `# PlantProcess IQ frontend local environment
# ------------------------------------------------------------
# The frontend auto-login/smoke flow uses these credentials.
# They MUST match a backend configured user from:
#   Backend/PlantProcess.Api/appsettings.Development.json
# or environment variables:
#   PlantProcess__Auth__Users__0__UserName
#   PlantProcess__Auth__Users__0__Password
#
# Do not commit real production credentials.

VITE_API_BASE_URL=http://localhost:5063
VITE_SMOKE_USERNAME=admin
VITE_SMOKE_PASSWORD=ChangeMe123!
`);

// ============================================================
// T282 / T287 — backend auth startup guard
// ============================================================

const programPath = p("Backend", "PlantProcess.Api", "Program.cs");
patchFile(programPath, (source) => {
  if (source.includes("Auth bound from PlantProcess:Auth")) return source;

  const needle = `var authOptions = builder.Configuration
        .GetSection("PlantProcess:Auth")
        .Get<AuthOptions>() ?? new AuthOptions();`;

  if (!source.includes(needle)) {
    console.warn("WARN Program.cs auth options needle not found; validator will still catch this.");
    return source;
  }

  const replacement = `${needle}

    Log.Information(
        "Auth bound from PlantProcess:Auth. RealUsers={RealUserCount}, RealAdmins={RealAdminCount}, BootstrapConfigured={BootstrapConfigured}, Environment={EnvironmentName}",
        authOptions.Users.Count(user =>
            !user.IsBootstrapAdmin &&
            !string.IsNullOrWhiteSpace(user.UserName) &&
            !string.IsNullOrWhiteSpace(user.Password)),
        authOptions.Users.Count(user =>
            !user.IsBootstrapAdmin &&
            string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(user.UserName) &&
            !string.IsNullOrWhiteSpace(user.Password)),
        !string.IsNullOrWhiteSpace(authOptions.BootstrapAdminUser) &&
            !string.IsNullOrWhiteSpace(authOptions.BootstrapAdminPassword),
        builder.Environment.EnvironmentName);`;

  return source.replace(needle, replacement);
});

const startupValidatorPath = p(
  "Backend",
  "PlantProcess.Api",
  "Configuration",
  "StartupConfigurationValidator.cs",
);

patchFile(startupValidatorPath, (source) => {
  let result = source;

  if (!result.includes("Legacy Auth:* configuration is not supported")) {
    const needle = `var audience = configuration["PlantProcess:Auth:Audience"];`;
    if (result.includes(needle)) {
      result = result.replace(
        needle,
        `${needle}
        var legacyAuthKeys = new[]
        {
            "Auth:SigningKey",
            "Auth:Issuer",
            "Auth:Audience",
            "Auth:BootstrapAdminUser",
            "Auth:BootstrapAdminPassword"
        };

        if (legacyAuthKeys.Any(key => !string.IsNullOrWhiteSpace(configuration[key])))
        {
            errors.Add(
                "Legacy Auth:* configuration is not supported. " +
                "Use PlantProcess:Auth:* or PlantProcess__Auth__* environment variables only.");
        }

        var configuredUsers = configuration
            .GetSection("PlantProcess:Auth:Users")
            .GetChildren()
            .ToList();

        var realAdminCount = configuredUsers.Count(user =>
            !string.Equals(user["IsBootstrapAdmin"], "true", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(user["Role"], "Admin", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(user["UserName"]) &&
            !string.IsNullOrWhiteSpace(user["Password"]));

        var bootstrapCollidesWithConfiguredUser =
            !string.IsNullOrWhiteSpace(bootstrapUser) &&
            configuredUsers.Any(user =>
                string.Equals(user["UserName"], bootstrapUser, StringComparison.OrdinalIgnoreCase));`,
      );
    }
  }

  if (!result.includes("Non-development requires at least one real configured admin")) {
    const needle = `if (!environment.IsDevelopment())
        {`;
    if (result.includes(needle)) {
      result = result.replace(
        needle,
        `if (!environment.IsDevelopment())
        {
            if (realAdminCount == 0)
            {
                errors.Add(
                    "Non-development requires at least one real configured admin under " +
                    "PlantProcess:Auth:Users with Role=Admin and IsBootstrapAdmin=false.");
            }

            if (bootstrapCollidesWithConfiguredUser)
            {
                errors.Add(
                    "Bootstrap admin user must not collide with any configured real user. " +
                    "Use a disabled sentinel bootstrap user outside Development.");
            }`,
      );
    }
  }

  return result;
});

// ============================================================
// T208 / T286 — Docker exposure hardening
// ============================================================

const composePath = p("Infrastructure", "deploy", "docker-compose.demo.yml");
if (exists(composePath)) {
  patchFile(composePath, (source) => {
    let s = source;

    s = s.replace(
      /-\s*"\$\{POSTGRES_PORT:-5432\}:5432"/g,
      '- "127.0.0.1:\${POSTGRES_PORT:-5432}:5432"',
    );

    s = s.replace(
      /-\s*"\$\{REDIS_PORT:-6379\}:6379"/g,
      '- "127.0.0.1:${REDIS_PORT:-6379}:6379"',
    );

    s = s.replace(
      /-\s*"\$\{SEQ_PORT:-5341\}:5341"/g,
      '- "127.0.0.1:${SEQ_PORT:-5341}:5341"',
    );

    s = s.replace(
      /-\s*"\$\{JAEGER_PORT:-16686\}:16686"/g,
      '- "127.0.0.1:${JAEGER_PORT:-16686}:16686"',
    );

    s = s.replace(
      /-\s*"\$\{PROMETHEUS_PORT:-9091\}:9090"/g,
      '- "127.0.0.1:${PROMETHEUS_PORT:-9091}:9090"',
    );

    s = s.replace(
      /-\s*"\$\{GRAFANA_PORT:-3001\}:3000"/g,
      '- "127.0.0.1:${GRAFANA_PORT:-3001}:3000"',
    );

    return s;
  });
}

write(p("Infrastructure", "deploy", "verify-server-exposure.sh"), `#!/usr/bin/env bash
set -euo pipefail

HOST="\${1:-178.105.152.180}"

echo "PlantProcess IQ server exposure proof"
echo "Target: \${HOST}"
echo

if ! command -v nmap >/dev/null 2>&1; then
  echo "ERROR: nmap is required on the machine running this proof." >&2
  exit 1
fi

SCAN_OUTPUT="$(nmap -Pn -p 22,80,443,9090,5432,6379,3000,3001,5063,5341,16686 "\${HOST}")"
echo "\${SCAN_OUTPUT}"

echo
echo "Validation rules:"
echo " - Public web ports 80/443 may be open."
echo " - Jenkins 9090 may be open only if protected by Caddy basicauth."
echo " - PostgreSQL 5432 must be closed/filtered from the public internet."
echo " - Redis, Grafana, Prometheus, Jaeger, direct API/internal ports must be closed/filtered."

if echo "\${SCAN_OUTPUT}" | grep -E "^5432/tcp\\s+open" >/dev/null; then
  echo "FAIL: PostgreSQL 5432 is publicly open."
  exit 1
fi

for port in 6379 3000 3001 5063 5341 16686; do
  if echo "\${SCAN_OUTPUT}" | grep -E "^\${port}/tcp\\s+open" >/dev/null; then
    echo "FAIL: internal service port \${port} is publicly open."
    exit 1
  fi
done

echo "PASS: public exposure proof passed."
`);

write(p("Infrastructure", "deploy", "apply-server-db-scripts.sh"), `#!/usr/bin/env bash
set -euo pipefail

POSTGRES_CONTAINER="\${POSTGRES_CONTAINER:-ppiq-postgres}"
POSTGRES_USER="\${POSTGRES_USER:?Set POSTGRES_USER from the server .env}"
POSTGRES_DB="\${POSTGRES_APP_DB:?Set POSTGRES_APP_DB from the server .env}"

SCRIPT_ROOT="\${SCRIPT_ROOT:-/opt/plantprocess-iq/Backend/database/scripts}"

scripts=(
  "200_phase02_ml_foundation_feature_store_pgvector.sql"
  "201_phase02_ml_feature_store_v6_completion.sql"
  "202_phase02_ml_compute_basic_correlations_hotfix.sql"
  "203_phase02_ml_compute_v6_wrapper_hotfix.sql"
)

for script in "\${scripts[@]}"; do
  echo "Applying \${script} to server Docker PostgreSQL..."
  docker exec -i "\${POSTGRES_CONTAINER}" psql \\
    -U "\${POSTGRES_USER}" \\
    -d "\${POSTGRES_DB}" \\
    -v ON_ERROR_STOP=1 \\
    < "\${SCRIPT_ROOT}/\${script}"
done

echo "Running server ML proof..."
docker exec -i "\${POSTGRES_CONTAINER}" psql -U "\${POSTGRES_USER}" -d "\${POSTGRES_DB}" -v ON_ERROR_STOP=1 <<'SQL'
SELECT * FROM public.ppiq_ml_seed_foundation_catalog();
SELECT * FROM public.ppiq_ml_refresh_feature_store_v6(3650);
SELECT * FROM public.ppiq_ml_compute_correlations_v6('defect.rate_per_m2', 'coil', 3650);
SQL

echo "PASS: server DB scripts and ML proof completed."
`);

const deployReadme = p("Infrastructure", "deploy", "README.md");
const deployBlock = `
## V7 Phase 1 exposure and server DB proof

### Public exposure rule

Only the reverse proxy should be public. PostgreSQL and internal services must not be reachable from the public internet.

Expected public exposure:

- 80/tcp: open
- 443/tcp: open
- 9090/tcp: optional, only when protected by Caddy basicauth
- 5432/tcp PostgreSQL: closed or filtered
- Redis/Grafana/Prometheus/Jaeger/direct API/internal ports: closed or filtered

Run from an external network:

\`\`\`bash
bash Infrastructure/deploy/verify-server-exposure.sh 178.105.152.180
\`\`\`

### Server DB deployment

Local PostgreSQL changes are not pushed automatically by Git. Server Docker PostgreSQL must receive SQL scripts explicitly with server credentials.

Apply:

\`\`\`bash
cd /opt/plantprocess-iq
set -a
source Infrastructure/deploy/.env
set +a
bash Infrastructure/deploy/apply-server-db-scripts.sh
\`\`\`

Never reuse local laptop credentials on the server.
`;

if (exists(deployReadme)) {
  patchFile(deployReadme, (source) =>
    source.includes("V7 Phase 1 exposure and server DB proof")
      ? source
      : `${source.trim()}\n\n${deployBlock}\n`,
  );
} else {
  write(deployReadme, `# PlantProcess IQ Deployment\n\n${deployBlock}\n`);
}

// ============================================================
// T284 — kill-by-port helper
// ============================================================

write(p("tools", "dev", "Stop-PPIQ-Port.ps1"), `# Stops the process owning a TCP port.
# PPIQ-T284: kill by port, not process name.

[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [int]$Port,

    [switch]$WhatIfOnly
)

$ErrorActionPreference = "Stop"

$connections = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue

if(-not $connections){
    Write-Host "OK: no listener found on port $Port"
    exit 0
}

$pids = $connections | Select-Object -ExpandProperty OwningProcess -Unique

foreach($pidValue in $pids){
    $proc = Get-Process -Id $pidValue -ErrorAction SilentlyContinue
    if($null -eq $proc){
        Write-Host "WARN: PID $pidValue no longer exists"
        continue
    }

    Write-Host "Port $Port is owned by PID $pidValue ($($proc.ProcessName))"

    if($WhatIfOnly){
        continue
    }

    Stop-Process -Id $pidValue -Force
    Write-Host "Stopped PID $pidValue"
}
`);

write(p("tools", "dev", "Restart-PPIQ-Local.ps1"), `# Local restart helper using kill-by-port.
# PPIQ-T284.

[CmdletBinding()]
param(
    [int]$ApiPort = 5063,
    [int]$WebPort = 5173,
    [int]$ReportPort = 9323
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent (Split-Path -Parent $PSCommandPath)

& "$Root\\tools\\dev\\Stop-PPIQ-Port.ps1" -Port $ApiPort
& "$Root\\tools\\dev\\Stop-PPIQ-Port.ps1" -Port $WebPort
& "$Root\\tools\\dev\\Stop-PPIQ-Port.ps1" -Port $ReportPort

Write-Host "Ports freed: $ApiPort, $WebPort, $ReportPort"
`);

// ============================================================
// T283 / T288 — SQL hygiene and dev bootstrap validation
// ============================================================

write(p("tools", "validation", "validate-sql-script-hygiene.cjs"), `const fs = require("node:fs");
const path = require("node:path");

const root = process.cwd();
const scriptsDir = path.join(root, "Backend", "database", "scripts");

const required = [
  "200_phase02_ml_foundation_feature_store_pgvector.sql",
  "201_phase02_ml_feature_store_v6_completion.sql",
  "202_phase02_ml_compute_basic_correlations_hotfix.sql",
  "203_phase02_ml_compute_v6_wrapper_hotfix.sql",
];

const failures = [];

for (const name of required) {
  const file = path.join(scriptsDir, name);

  if (!fs.existsSync(file)) {
    failures.push(\`Missing required SQL script: \${name}\`);
    continue;
  }

  const buffer = fs.readFileSync(file);
  const text = buffer.toString("utf8");

  if (buffer.length >= 3 && buffer[0] === 0xef && buffer[1] === 0xbb && buffer[2] === 0xbf) {
    failures.push(\`\${name}: UTF-8 BOM detected\`);
  }

  if (/DO \\$\\s/i.test(text) || /END \\$;/i.test(text)) {
    failures.push(\`\${name}: invalid DO $ / END $ block detected; use DO $$ / END $$\`);
  }

  if (/CREATE EXTENSION IF NOT EXISTS vector/i.test(text) && !/EXCEPTION\\s+WHEN\\s+OTHERS/i.test(text)) {
    failures.push(\`\${name}: pgvector extension is not fallback-safe\`);
  }
}

const bootstrap = path.join(root, "tools", "dev-bootstrap.ps1");
if (fs.existsSync(bootstrap)) {
  const text = fs.readFileSync(bootstrap, "utf8");
  if (!text.includes("ON_ERROR_STOP=1")) {
    failures.push("tools/dev-bootstrap.ps1 must use ON_ERROR_STOP=1 for SQL application");
  }
  if (!text.includes("$LASTEXITCODE")) {
    failures.push("tools/dev-bootstrap.ps1 must rely on process exit code, not NOTICE output text");
  }
}

if (failures.length) {
  console.error("PPIQ-T283/T288 validation failed:");
  for (const failure of failures) console.error(" - " + failure);
  process.exit(1);
}

console.log("PPIQ-T283/T288 passed: SQL script hygiene and dev-bootstrap safety checks passed.");
`);

// ============================================================
// T205 — standard-import proof helper
// ============================================================

write(p("tools", "validation", "prove-standard-import-gate.cjs"), `const fs = require("node:fs");
const path = require("node:path");
const cp = require("node:child_process");

const root = process.cwd();
const web = path.join(root, "Frontend", "PlantProcess.Web");
const target = path.join(web, "src", "pages", "__standard_import_gate_negative__.tsx");

function run(command, options = {}) {
  return cp.spawnSync(command, {
    cwd: options.cwd ?? root,
    shell: true,
    encoding: "utf8",
    stdio: "pipe",
  });
}

try {
  fs.writeFileSync(
    target,
    "export function StandardImportGateNegative(){ return <button>bad</button>; }\\n",
    "utf8",
  );

  const negative = run("npm run validate:standard-imports", { cwd: web });

  if (negative.status === 0) {
    console.error("FAIL: standard-import validator did not fail for native <button>.");
    console.error(negative.stdout);
    console.error(negative.stderr);
    process.exit(1);
  }

  console.log("OK negative proof: native <button> was rejected.");
} finally {
  if (fs.existsSync(target)) fs.unlinkSync(target);
}

const positive = run("npm run validate:standard-imports", { cwd: web });

if (positive.status !== 0) {
  console.error("FAIL: standard-import validator did not pass after removing scratch violation.");
  console.error(positive.stdout);
  console.error(positive.stderr);
  process.exit(1);
}

console.log("PPIQ-T205 passed: standard-import gate rejects violation and passes clean tree.");
`);

// ============================================================
// V7 Phase 1 aggregate validator
// ============================================================

write(p("tools", "validation", "validate-v7-phase01.cjs"), `const fs = require("node:fs");
const path = require("node:path");

const root = process.cwd();
const failures = [];

function read(rel) {
  const file = path.join(root, rel);
  if (!fs.existsSync(file)) {
    failures.push(\`Missing file: \${rel}\`);
    return "";
  }
  return fs.readFileSync(file, "utf8");
}

function ok(condition, message) {
  if (!condition) failures.push(message);
}

const authContext = read("Frontend/PlantProcess.Web/src/state/AuthContext.tsx");
ok(authContext.includes("MAX_AUTO_BOOTSTRAP_ATTEMPTS = 3"), "T280 missing max auth attempt cap");
ok(authContext.includes("AUTH_RETRY_BACKOFF_MS"), "T280 missing auth backoff");
ok(authContext.includes("invalid-credentials"), "T281 missing invalid-credentials classification");
ok(authContext.includes("backend-unreachable"), "T281 missing backend-unreachable classification");
ok(authContext.includes("VITE_SMOKE_PASSWORD"), "T285 missing VITE_SMOKE_PASSWORD contract");

const envExample = read("Frontend/PlantProcess.Web/.env.example");
ok(envExample.includes("VITE_SMOKE_USERNAME"), "T285 .env.example missing VITE_SMOKE_USERNAME");
ok(envExample.includes("VITE_SMOKE_PASSWORD"), "T285 .env.example missing VITE_SMOKE_PASSWORD");

const program = read("Backend/PlantProcess.Api/Program.cs");
ok(program.includes('GetSection("PlantProcess:Auth")'), "T282 Program.cs must bind AuthOptions from PlantProcess:Auth");
ok(program.includes("Auth bound from PlantProcess:Auth"), "T282 missing exact auth binding startup log");

const startup = read("Backend/PlantProcess.Api/Configuration/StartupConfigurationValidator.cs");
ok(startup.includes("Legacy Auth:* configuration is not supported"), "T282 missing legacy Auth:* rejection");
ok(startup.includes("Non-development requires at least one real configured admin"), "T287 missing real admin non-development guard");
ok(startup.includes("Bootstrap admin user must not collide"), "T287 missing bootstrap collision guard");

const authEndpoints = read("Backend/PlantProcess.Api/Security/AuthEndpoints.cs");
ok(authEndpoints.includes("Bootstrap admin login rejected because at least one real admin exists"), "T287 missing runtime bootstrap rejection");
ok(authEndpoints.includes("HasRealAdmin"), "T287 missing real-admin guard helper");

const compose = read("Infrastructure/deploy/docker-compose.demo.yml");
ok(!compose.includes('"\${POSTGRES_PORT:-5432}:5432"'), "T286 postgres still binds publicly");
ok(compose.includes('"127.0.0.1:\${POSTGRES_PORT:-5432}:5432"'), "T286 postgres loopback binding missing");

const exposure = read("Infrastructure/deploy/verify-server-exposure.sh");
ok(exposure.includes("5432") && exposure.includes("publicly open"), "T208/T286 exposure proof script incomplete");

const stopPort = read("tools/dev/Stop-PPIQ-Port.ps1");
ok(stopPort.includes("Get-NetTCPConnection") && stopPort.includes("Stop-Process"), "T284 kill-by-port helper incomplete");

const sqlHygiene = read("tools/validation/validate-sql-script-hygiene.cjs");
ok(sqlHygiene.includes("ON_ERROR_STOP=1"), "T283 SQL hygiene validator missing ON_ERROR_STOP check");
ok(sqlHygiene.includes("UTF-8 BOM"), "T288 SQL hygiene validator missing BOM check");

const standardProof = read("tools/validation/prove-standard-import-gate.cjs");
ok(standardProof.includes("<button>bad</button>"), "T205 negative proof script missing native button violation");

if (failures.length > 0) {
  console.error("PlantProcess IQ V7 Phase 1 validation failed:");
  for (const failure of failures) console.error(" - " + failure);
  process.exit(1);
}

console.log("PlantProcess IQ V7 Phase 1 static implementation validation passed.");
console.log("NOTE: T208/T286 still require real external server scan after deployment.");
`);

// ============================================================
// package.json scripts
// ============================================================

upsertPackageScript(
  p("Frontend", "PlantProcess.Web", "package.json"),
  "test:standard-import-gate",
  "node ../../tools/validation/prove-standard-import-gate.cjs",
);

upsertPackageScript(
  p("Frontend", "PlantProcess.Web", "package.json"),
  "validate:auth-env",
  "node ../../tools/validation/validate-v7-phase01.cjs",
);

upsertPackageScript(
  p("package.json"),
  "validate:v7-phase01",
  "node tools/validation/validate-v7-phase01.cjs && node tools/validation/validate-sql-script-hygiene.cjs",
);

// ============================================================
// Final notes
// ============================================================

console.log("");
console.log("V7 Phase 1 completion patch applied.");
console.log("");
console.log("Implemented:");
console.log(" - T205 standard-import negative proof helper");
console.log(" - T208/T286 exposure scripts + compose loopback guard");
console.log(" - T280/T281/T285 frontend auth retry cap, error messages, env contract");
console.log(" - T282/T287 backend auth binding log + startup guards");
console.log(" - T283/T288 SQL/dev-bootstrap hygiene validation");
console.log(" - T284 kill-by-port tooling");
console.log("");
console.log("Next: run validators, frontend build, backend build, and then server external scan after deployment.");

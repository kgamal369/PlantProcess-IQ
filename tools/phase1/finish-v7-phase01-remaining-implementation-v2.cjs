const fs = require("node:fs");
const path = require("node:path");

const root = process.cwd();

function p(...parts) {
  return path.join(root, ...parts);
}

function exists(file) {
  return fs.existsSync(file);
}

function read(file) {
  if (!exists(file)) throw new Error("File not found: " + file);
  return fs.readFileSync(file, "utf8");
}

function write(file, content) {
  fs.mkdirSync(path.dirname(file), { recursive: true });
  fs.writeFileSync(file, content.replace(/\r?\n/g, "\r\n"), "utf8");
  console.log("Wrote " + path.relative(root, file));
}

function patch(file, updater) {
  const before = read(file);
  const after = updater(before);

  if (after === before) {
    console.log("OK no change needed: " + path.relative(root, file));
    return false;
  }

  fs.writeFileSync(file, after, "utf8");
  console.log("Patched " + path.relative(root, file));
  return true;
}

function updatePackageScript(packageJsonPath, scriptName, command) {
  if (!exists(packageJsonPath)) return;

  const json = JSON.parse(read(packageJsonPath));
  json.scripts = json.scripts || {};
  json.scripts[scriptName] = command;

  fs.writeFileSync(packageJsonPath, JSON.stringify(json, null, 2) + "\n", "utf8");
  console.log("Updated package script " + scriptName + " in " + path.relative(root, packageJsonPath));
}

// Keep the previous broken patcher executable-safe.
write(
  p("tools", "phase1", "finish-v7-phase01-remaining-implementation.cjs"),
  [
    'require("./finish-v7-phase01-remaining-implementation-v2.cjs");',
    "",
  ].join("\n"),
);

// ============================================================================
// T205 — Standard UI wrappers.
// ============================================================================

write(
  p("Frontend", "PlantProcess.Web", "src", "components", "standard", "StandardPageCompat.tsx"),
  [
    'import {',
    '  forwardRef,',
    '  type ButtonHTMLAttributes,',
    '  type InputHTMLAttributes,',
    '  type SelectHTMLAttributes,',
    '  type TableHTMLAttributes,',
    '  type TextareaHTMLAttributes,',
    '} from "react";',
    '',
    'import "./standard-components.css";',
    '',
    'function cx(...values: Array<string | false | null | undefined>) {',
    '  return values.filter(Boolean).join(" ");',
    '}',
    '',
    'export type StandardPageButtonProps = ButtonHTMLAttributes<HTMLButtonElement> & {',
    '  isDisabled?: boolean;',
    '  isLoading?: boolean;',
    '};',
    '',
    'export const StandardPageButton = forwardRef<HTMLButtonElement, StandardPageButtonProps>(',
    '  ({ className, type = "button", disabled, isDisabled, isLoading, children, ...rest }, ref) => (',
    '    <button',
    '      ref={ref}',
    '      type={type}',
    '      className={cx("ppiq-std-button", "ppiq-std-button--md", className)}',
    '      disabled={disabled || isDisabled || isLoading}',
    '      aria-disabled={disabled || isDisabled || isLoading || undefined}',
    '      aria-busy={isLoading || undefined}',
    '      {...rest}',
    '    >',
    '      {children}',
    '    </button>',
    '  ),',
    ');',
    '',
    'StandardPageButton.displayName = "StandardPageButton";',
    '',
    'export const StandardPageInput = forwardRef<HTMLInputElement, InputHTMLAttributes<HTMLInputElement>>(',
    '  ({ className, ...rest }, ref) => (',
    '    <input',
    '      ref={ref}',
    '      className={cx("ppiq-std-field__control", className)}',
    '      {...rest}',
    '    />',
    '  ),',
    ');',
    '',
    'StandardPageInput.displayName = "StandardPageInput";',
    '',
    'export const StandardPageSelect = forwardRef<HTMLSelectElement, SelectHTMLAttributes<HTMLSelectElement>>(',
    '  ({ className, children, ...rest }, ref) => (',
    '    <select',
    '      ref={ref}',
    '      className={cx("ppiq-std-field__control", className)}',
    '      {...rest}',
    '    >',
    '      {children}',
    '    </select>',
    '  ),',
    ');',
    '',
    'StandardPageSelect.displayName = "StandardPageSelect";',
    '',
    'export const StandardPageTextArea = forwardRef<HTMLTextAreaElement, TextareaHTMLAttributes<HTMLTextAreaElement>>(',
    '  ({ className, ...rest }, ref) => (',
    '    <textarea',
    '      ref={ref}',
    '      className={cx("ppiq-std-field__control", "ppiq-std-field__textarea", className)}',
    '      {...rest}',
    '    />',
    '  ),',
    ');',
    '',
    'StandardPageTextArea.displayName = "StandardPageTextArea";',
    '',
    'export const StandardPageTable = forwardRef<HTMLTableElement, TableHTMLAttributes<HTMLTableElement>>(',
    '  ({ className, children, ...rest }, ref) => (',
    '    <table',
    '      ref={ref}',
    '      className={cx("ppiq-std-table", className)}',
    '      {...rest}',
    '    >',
    '      {children}',
    '    </table>',
    '  ),',
    ');',
    '',
    'StandardPageTable.displayName = "StandardPageTable";',
    '',
  ].join("\n"),
);

// ============================================================================
// T205 — Replace native page-level controls in failing Admin files.
// ============================================================================

const adminFiles = [
  "Frontend/PlantProcess.Web/src/pages/Admin/AdminDbConfigurationTab.tsx",
  "Frontend/PlantProcess.Web/src/pages/Admin/AdminImportingDataTab.tsx",
  "Frontend/PlantProcess.Web/src/pages/Admin/AdminJobsMonitorTab.tsx",
  "Frontend/PlantProcess.Web/src/pages/Admin/AdminSchemaConfigurationTab.tsx",
  "Frontend/PlantProcess.Web/src/pages/Admin/CanonicalSchemaMappingPanel.tsx",
  "Frontend/PlantProcess.Web/src/pages/Admin/TwoStageImportMonitorPanel.tsx",
];

const tagMap = [
  ["button", "StandardPageButton"],
  ["input", "StandardPageInput"],
  ["select", "StandardPageSelect"],
  ["textarea", "StandardPageTextArea"],
  ["table", "StandardPageTable"],
];

function upsertImport(source, components) {
  if (components.size === 0) return source;

  const importPath = "@/components/standard/StandardPageCompat";
  const wanted = Array.from(components).sort();

  const existingRegex =
    /import\s*\{\s*([^}]+?)\s*\}\s*from\s*["']@\/components\/standard\/StandardPageCompat["'];/m;

  const existing = source.match(existingRegex);

  if (existing) {
    const current = existing[1]
      .split(",")
      .map((x) => x.trim())
      .filter(Boolean);

    const merged = Array.from(new Set([...current, ...wanted])).sort();

    return source.replace(
      existingRegex,
      'import { ' + merged.join(", ") + ' } from "' + importPath + '";',
    );
  }

  const importLine = 'import { ' + wanted.join(", ") + ' } from "' + importPath + '";';
  const imports = [...source.matchAll(/^import .*?;$/gm)];

  if (imports.length === 0) return importLine + "\n" + source;

  const last = imports[imports.length - 1];
  const insertAt = last.index + last[0].length;

  return source.slice(0, insertAt) + "\n" + importLine + source.slice(insertAt);
}

function replaceNativeControls(source) {
  let text = source;
  const used = new Set();

  for (const [tag, component] of tagMap) {
    const openRegex = new RegExp("<" + tag + "(?=[\\s>/])", "g");
    const closeRegex = new RegExp("</" + tag + ">", "g");

    const before = text;
    text = text.replace(openRegex, "<" + component);
    text = text.replace(closeRegex, "</" + component + ">");

    if (text !== before) used.add(component);
  }

  return upsertImport(text, used);
}

for (const rel of adminFiles) {
  const file = p(...rel.split("/"));
  if (!exists(file)) {
    console.log("SKIP missing admin file: " + rel);
    continue;
  }

  patch(file, replaceNativeControls);
}

// ============================================================================
// T205 — Enforced standard-import validator.
// ============================================================================

write(
  p("Frontend", "PlantProcess.Web", "scripts", "validate-standard-imports.mjs"),
  [
    'import fs from "node:fs";',
    'import path from "node:path";',
    '',
    'const root = process.cwd();',
    'const scanRoots = ["src/pages", "src/features"];',
    'const nativeTags = ["button", "input", "select", "textarea", "table"];',
    'const failures = [];',
    '',
    'function walk(dir) {',
    '  if (!fs.existsSync(dir)) return [];',
    '  const entries = fs.readdirSync(dir, { withFileTypes: true });',
    '  const files = [];',
    '  for (const entry of entries) {',
    '    const full = path.join(dir, entry.name);',
    '    if (entry.isDirectory()) files.push(...walk(full));',
    '    else if (/\\.(tsx|ts)$/.test(entry.name)) files.push(full);',
    '  }',
    '  return files;',
    '}',
    '',
    'for (const scanRoot of scanRoots) {',
    '  for (const file of walk(path.join(root, scanRoot))) {',
    '    const rel = path.relative(root, file).replaceAll("\\\\", "/");',
    '    const text = fs.readFileSync(file, "utf8");',
    '',
    '    for (const tag of nativeTags) {',
    '      const regex = new RegExp(`<${tag}(?=[\\\\s>/])`, "i");',
    '      if (regex.test(text)) {',
    '        failures.push(`${rel}: Native <${tag}> element found. Use StandardButton, StandardInput, StandardSelect, StandardTextArea or StandardTable.`);',
    '      }',
    '    }',
    '',
    '    const hardeningImport = /from\\s+["\'][^"\']*hardening[^"\']*["\']|import\\s+["\'][^"\']*hardening[^"\']*["\']/i;',
    '    if (hardeningImport.test(text)) {',
    '      failures.push(`${rel}: Forbidden import from hardening module found. Move reusable UI/contracts into canonical standard/app modules.`);',
    '    }',
    '  }',
    '}',
    '',
    'if (failures.length > 0) {',
    '  console.error("\\n❌ PPIQ-T205 standard import/UI gate failed.\\n");',
    '  for (const failure of failures) console.error("- " + failure);',
    '  process.exit(1);',
    '}',
    '',
    'console.log("✅ PPIQ-T205 standard import/UI gate passed.");',
    '',
  ].join("\n"),
);

// ============================================================================
// T205 — Negative proof script fixed for repo root and frontend cwd.
// ============================================================================

write(
  p("tools", "validation", "prove-standard-import-gate.cjs"),
  [
    'const fs = require("node:fs");',
    'const path = require("node:path");',
    'const cp = require("node:child_process");',
    '',
    'const cwd = process.cwd();',
    '',
    'const isFrontendRoot =',
    '  fs.existsSync(path.join(cwd, "src")) &&',
    '  fs.existsSync(path.join(cwd, "package.json")) &&',
    '  fs.existsSync(path.join(cwd, "scripts", "validate-standard-imports.mjs"));',
    '',
    'const repoRoot = isFrontendRoot ? path.resolve(cwd, "..", "..") : cwd;',
    'const webRoot = isFrontendRoot ? cwd : path.join(repoRoot, "Frontend", "PlantProcess.Web");',
    'const target = path.join(webRoot, "src", "pages", "__standard_import_gate_negative__.tsx");',
    '',
    'function run(command, cwdToUse) {',
    '  return cp.spawnSync(command, {',
    '    cwd: cwdToUse,',
    '    shell: true,',
    '    encoding: "utf8",',
    '    stdio: "pipe",',
    '  });',
    '}',
    '',
    'if (!fs.existsSync(path.join(webRoot, "scripts", "validate-standard-imports.mjs"))) {',
    '  console.error("FAIL: could not locate validate-standard-imports.mjs");',
    '  console.error("cwd=" + cwd);',
    '  console.error("webRoot=" + webRoot);',
    '  process.exit(1);',
    '}',
    '',
    'try {',
    '  fs.writeFileSync(',
    '    target,',
    '    "import \\"@/hardening/forbidden\\";\\nexport function StandardImportGateNegative(){ return <button>bad</button>; }\\n",',
    '    "utf8",',
    '  );',
    '',
    '  const negative = run("npm run validate:standard-imports", webRoot);',
    '',
    '  if (negative.status === 0) {',
    '    console.error("FAIL: standard-import validator did not reject native <button> and hardening import.");',
    '    console.error(negative.stdout);',
    '    console.error(negative.stderr);',
    '    process.exit(1);',
    '  }',
    '',
    '  console.log("OK negative proof: native <button> and hardening import were rejected.");',
    '} finally {',
    '  if (fs.existsSync(target)) fs.unlinkSync(target);',
    '}',
    '',
    'const positive = run("npm run validate:standard-imports", webRoot);',
    '',
    'if (positive.status !== 0) {',
    '  console.error("FAIL: standard-import validator did not pass after removing scratch violation.");',
    '  console.error(positive.stdout);',
    '  console.error(positive.stderr);',
    '  process.exit(1);',
    '}',
    '',
    'console.log("PPIQ-T205 passed: standard-import gate rejects violation and passes clean tree.");',
    '',
  ].join("\n"),
);

// ============================================================================
// T282 — exact auth startup proof.
// ============================================================================

const programFile = p("Backend", "PlantProcess.Api", "Program.cs");

if (exists(programFile)) {
  patch(programFile, (source) => {
    let text = source;

    text = text.replace(
      /\n\/\/ PPIQ-T282:[\s\S]*?Log\.Information\(\s*"Auth bound from PlantProcess:Auth\. Environment=\{EnvironmentName\}"[\s\S]*?builder\.Environment\.EnvironmentName\);\s*/m,
      "\n",
    );

    text = text.replace(
      /\nLog\.Information\(\s*"Auth bound from PlantProcess:Auth\. Environment=\{EnvironmentName\}"[\s\S]*?builder\.Environment\.EnvironmentName\);\s*/m,
      "\n",
    );

    if (
      text.includes("Auth bound from PlantProcess:Auth —") &&
      text.includes("signingKeyLen={SigningKeyLen}") &&
      text.includes("bootstrapCollision={BootstrapCollision}")
    ) {
      return text;
    }

    const block = [
      "// PPIQ-T282: startup auth self-check with no secret leakage.",
      'var ppiqV7AuthSection = builder.Configuration.GetSection("PlantProcess:Auth");',
      'var ppiqV7AuthUsers = ppiqV7AuthSection.GetSection("Users").GetChildren().ToList();',
      'var ppiqV7SigningKey = ppiqV7AuthSection["SigningKey"] ?? string.Empty;',
      'var ppiqV7BootstrapPassword = ppiqV7AuthSection["BootstrapAdminPassword"] ?? string.Empty;',
      "var ppiqV7BootstrapCollision =",
      "    !string.IsNullOrWhiteSpace(ppiqV7BootstrapPassword) &&",
      "    ppiqV7AuthUsers.Any(user =>",
      '        string.Equals(user["Password"], ppiqV7BootstrapPassword, StringComparison.Ordinal));',
      "",
      "Log.Information(",
      '    "Auth bound from PlantProcess:Auth — {UserCount} users, signingKeyLen={SigningKeyLen}, bootstrapCollision={BootstrapCollision}",',
      "    ppiqV7AuthUsers.Count,",
      "    ppiqV7SigningKey.Length,",
      "    ppiqV7BootstrapCollision);",
      "",
      "if (ppiqV7BootstrapCollision)",
      "{",
      "    Log.Warning(",
      '        "Auth bootstrap collision detected: at least one configured user password equals BootstrapAdminPassword. Use a real admin password distinct from the bootstrap password.");',
      "}",
      "",
    ].join("\n");

    if (!text.includes("var app = builder.Build();")) {
      throw new Error("Could not find 'var app = builder.Build();' in Program.cs");
    }

    return text.replace("var app = builder.Build();", block + "\nvar app = builder.Build();");
  });
}

// ============================================================================
// T287 — production secret/bootstrap guard.
// ============================================================================

const startupValidatorFile = p(
  "Backend",
  "PlantProcess.Api",
  "Configuration",
  "StartupConfigurationValidator.cs",
);

if (exists(startupValidatorFile)) {
  patch(startupValidatorFile, (source) => {
    let text = source;

    if (!text.includes("ValidateV7Phase1ProductionSecretGuard(configuration, environment, errors);")) {
      const target = text.includes("ValidateV7Phase1AuthHardening(configuration, environment, errors);")
        ? "ValidateV7Phase1AuthHardening(configuration, environment, errors);"
        : "ValidateAuthentication(configuration, environment, errors);";

      if (!text.includes(target)) {
        throw new Error("Could not find StartupConfigurationValidator insertion point.");
      }

      text = text.replace(
        target,
        target + "\n        ValidateV7Phase1ProductionSecretGuard(configuration, environment, errors);",
      );
    }

    if (text.includes("private static void ValidateV7Phase1ProductionSecretGuard")) {
      return text;
    }

    const method = [
      "",
      "    private static void ValidateV7Phase1ProductionSecretGuard(",
      "        IConfiguration configuration,",
      "        IWebHostEnvironment environment,",
      "        List<string> errors)",
      "    {",
      "        if (environment.IsDevelopment())",
      "        {",
      "            return;",
      "        }",
      "",
      '        var auth = configuration.GetSection("PlantProcess:Auth");',
      '        var signingKey = auth["SigningKey"] ?? string.Empty;',
      '        var bootstrapPassword = auth["BootstrapAdminPassword"] ?? string.Empty;',
      "",
      "        var dangerousPasswords = new HashSet<string>(StringComparer.Ordinal)",
      "        {",
      '            "ChangeMe123!",',
      '            "Admin123!",',
      '            "admin",',
      '            "password",',
      '            "Password123!",',
      '            "plantprocess123"',
      "        };",
      "",
      "        var configuredUsers = auth",
      '            .GetSection("Users")',
      "            .GetChildren()",
      "            .ToList();",
      "",
      "        foreach (var user in configuredUsers)",
      "        {",
      '            var userName = user["UserName"] ?? "(unknown)";',
      '            var password = user["Password"] ?? string.Empty;',
      "",
      "            if (dangerousPasswords.Contains(password))",
      "            {",
      "                errors.Add(",
      '                    $"Production user \'{userName}\' uses a development/default password. " +',
      '                    "Set a strong unique secret via environment variables or production secret storage.");',
      "            }",
      "        }",
      "",
      '        if (signingKey.Contains("DEV_ONLY", StringComparison.OrdinalIgnoreCase) ||',
      '            signingKey.Contains("CHANGE_THIS", StringComparison.OrdinalIgnoreCase) ||',
      "            signingKey.Length < 32)",
      "        {",
      "            errors.Add(",
      '                "Production PlantProcess:Auth:SigningKey must be a strong non-development key with at least 32 characters.");',
      "        }",
      "",
      "        if (!string.IsNullOrWhiteSpace(bootstrapPassword) &&",
      '            !bootstrapPassword.Equals("__DISABLED__", StringComparison.OrdinalIgnoreCase) &&',
      '            !bootstrapPassword.StartsWith("DISABLED-", StringComparison.OrdinalIgnoreCase))',
      "        {",
      "            errors.Add(",
      '                "Production bootstrap password must be a disabled sentinel, for example \'__DISABLED__\' or \'DISABLED-<random>\'.");',
      "        }",
      "    }",
      "",
    ].join("\n");

    const lastBrace = text.lastIndexOf("\n}");
    if (lastBrace < 0) throw new Error("Could not find final class brace in StartupConfigurationValidator.cs");

    return text.slice(0, lastBrace) + method + text.slice(lastBrace);
  });
}

// ============================================================================
// T283 — dev-bootstrap NOTICE safety + PlantProcess:Auth secret paths.
// ============================================================================

const devBootstrapFile = p("tools", "dev-bootstrap.ps1");

if (exists(devBootstrapFile)) {
  patch(devBootstrapFile, (source) => {
    let text = source;

    text = text.replaceAll('"Auth:BootstrapAdminPassword"', '"PlantProcess:Auth:BootstrapAdminPassword"');
    text = text.replaceAll('"Auth:Users:0:Password"', '"PlantProcess:Auth:Users:0:Password"');

    if (!text.includes("PlantProcess:Auth:SigningKey")) {
      text = text.replace(
        '& dotnet user-secrets set "PlantProcess:Auth:Users:0:Password"       $AdminPassword | Out-Null',
        '& dotnet user-secrets set "PlantProcess:Auth:Users:0:Password"       $AdminPassword | Out-Null\n        & dotnet user-secrets set "PlantProcess:Auth:SigningKey"             "DEV_LOCAL_SIGNING_KEY_1234567890_ABCDEF_PPIQ" | Out-Null',
      );
    }

    const oldFunctionRegex = /function\s+Invoke-PsqlFile\s*\(\[string\]\$file\)\s*\{[\s\S]*?\n\}/m;

    const replacementFunction = [
      'function Invoke-PsqlFile([string]$file){',
      '    $previousErrorActionPreference = $ErrorActionPreference',
      '    $previousPgOptions = $env:PGOPTIONS',
      '',
      '    # PPIQ-T283: suppress benign NOTICE output while keeping real SQL failures fatal.',
      '    $ErrorActionPreference = "Continue"',
      '    $env:PGOPTIONS = "-c client_min_messages=warning"',
      '',
      '    try {',
      '        $out = & psql -h $DbHost -p $DbPort -U $DbUser -d $DbName -v ON_ERROR_STOP=1 -q -f $file 2>&1',
      '        $code = $LASTEXITCODE',
      '',
      '        if($out){',
      '            $out | ForEach-Object { Write-Host "      $_" -ForegroundColor DarkGray }',
      '        }',
      '',
      '        return $code',
      '    }',
      '    finally {',
      '        $ErrorActionPreference = $previousErrorActionPreference',
      '        if($null -eq $previousPgOptions){',
      '            Remove-Item Env:\\PGOPTIONS -ErrorAction SilentlyContinue',
      '        }',
      '        else {',
      '            $env:PGOPTIONS = $previousPgOptions',
      '        }',
      '    }',
      '}',
    ].join("\n");

    if (oldFunctionRegex.test(text)) {
      text = text.replace(oldFunctionRegex, replacementFunction);
    }

    return text;
  });
}

// ============================================================================
// T284 — kill by port with PID + StartTime proof.
// ============================================================================

write(
  p("tools", "dev", "Stop-PPIQ-Port.ps1"),
  [
    '# PPIQ-T284',
    '# Stops the process owning a TCP port and prints PID + StartTime proof.',
    '',
    '[CmdletBinding()]',
    'param(',
    '    [Parameter(Mandatory=$true)]',
    '    [int]$Port,',
    '',
    '    [switch]$WhatIfOnly',
    ')',
    '',
    '$ErrorActionPreference = "Stop"',
    '',
    '$connections = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue',
    '',
    'if(-not $connections){',
    '    Write-Host "OK: no listener found on port $Port" -ForegroundColor Green',
    '    exit 0',
    '}',
    '',
    '$pids = $connections | Select-Object -ExpandProperty OwningProcess -Unique',
    '',
    'foreach($pidValue in $pids){',
    '    $proc = Get-Process -Id $pidValue -ErrorAction SilentlyContinue',
    '',
    '    if($null -eq $proc){',
    '        Write-Host "WARN: PID $pidValue no longer exists" -ForegroundColor Yellow',
    '        continue',
    '    }',
    '',
    '    $start = $null',
    '    try { $start = $proc.StartTime } catch { $start = "(unavailable)" }',
    '',
    '    Write-Host "Port $Port owner: PID=$pidValue Process=$($proc.ProcessName) StartTime=$start" -ForegroundColor Yellow',
    '',
    '    if($WhatIfOnly){',
    '        continue',
    '    }',
    '',
    '    Stop-Process -Id $pidValue -Force',
    '    Write-Host "Stopped PID $pidValue on port $Port" -ForegroundColor Green',
    '}',
    '',
  ].join("\n"),
);

write(
  p("tools", "dev", "Show-PPIQ-PortOwner.ps1"),
  [
    '# PPIQ-T284',
    '# Prints the current owner PID + StartTime for a TCP port.',
    '',
    '[CmdletBinding()]',
    'param(',
    '    [Parameter(Mandatory=$true)]',
    '    [int]$Port',
    ')',
    '',
    '$connections = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue',
    '',
    'if(-not $connections){',
    '    Write-Host "No listener on port $Port" -ForegroundColor Yellow',
    '    exit 1',
    '}',
    '',
    '$pids = $connections | Select-Object -ExpandProperty OwningProcess -Unique',
    '',
    'foreach($pidValue in $pids){',
    '    $proc = Get-Process -Id $pidValue -ErrorAction SilentlyContinue',
    '    if($null -eq $proc){',
    '        Write-Host "PID $pidValue no longer exists" -ForegroundColor Yellow',
    '        continue',
    '    }',
    '',
    '    $start = $null',
    '    try { $start = $proc.StartTime } catch { $start = "(unavailable)" }',
    '',
    '    Write-Host "Port $Port listener: PID=$pidValue Process=$($proc.ProcessName) StartTime=$start" -ForegroundColor Green',
    '}',
    '',
  ].join("\n"),
);

// ============================================================================
// T208/T286 — server exposure and password rotation helpers.
// No JS template strings here: Bash ${...} is safely inside normal JS strings.
// ============================================================================

write(
  p("Infrastructure", "deploy", "verify-server-exposure.sh"),
  [
    '#!/usr/bin/env bash',
    'set -euo pipefail',
    '',
    'HOST="${1:-178.105.152.180}"',
    '',
    'echo "PlantProcess IQ V7 Phase 1 external exposure proof"',
    'echo "Target: ${HOST}"',
    'echo',
    '',
    'if ! command -v nmap >/dev/null 2>&1; then',
    '  echo "ERROR: nmap is required on the machine running this proof." >&2',
    '  exit 1',
    'fi',
    '',
    'SCAN_OUTPUT="$(nmap -Pn -p 22,80,443,9090,5432,6379,3000,3001,5063,5341,16686 "${HOST}")"',
    'echo "${SCAN_OUTPUT}"',
    '',
    'fail_if_open() {',
    '  local port="$1"',
    '  local name="$2"',
    '',
    '  if echo "${SCAN_OUTPUT}" | grep -E "^${port}/tcp[[:space:]]+open" >/dev/null; then',
    '    echo "FAIL: ${name} port ${port} is publicly open."',
    '    exit 1',
    '  fi',
    '}',
    '',
    'fail_if_open 5432 "PostgreSQL"',
    'fail_if_open 6379 "Redis"',
    'fail_if_open 3000 "Internal frontend/dev"',
    'fail_if_open 3001 "Grafana"',
    'fail_if_open 5063 "Direct API"',
    'fail_if_open 5341 "Seq"',
    'fail_if_open 16686 "Jaeger"',
    '',
    'echo',
    'echo "PASS: data/internal service ports are not publicly open."',
    'echo "NOTE: 80/443 may be public. 9090 may be public only when protected by Caddy basicauth."',
    '',
  ].join("\n"),
);

write(
  p("Infrastructure", "deploy", "rotate-postgres-password.sh"),
  [
    '#!/usr/bin/env bash',
    'set -euo pipefail',
    '',
    'POSTGRES_CONTAINER="${POSTGRES_CONTAINER:-ppiq-postgres}"',
    'POSTGRES_USER="${POSTGRES_USER:?Set POSTGRES_USER}"',
    'POSTGRES_DB="${POSTGRES_APP_DB:-postgres}"',
    'NEW_POSTGRES_PASSWORD="${NEW_POSTGRES_PASSWORD:?Set NEW_POSTGRES_PASSWORD}"',
    '',
    'echo "Rotating PostgreSQL password for user ${POSTGRES_USER} inside container ${POSTGRES_CONTAINER}..."',
    '',
    'docker exec -i "${POSTGRES_CONTAINER}" psql \\',
    '  -U "${POSTGRES_USER}" \\',
    '  -d "${POSTGRES_DB}" \\',
    '  -v ON_ERROR_STOP=1 \\',
    '  -v postgres_user="${POSTGRES_USER}" \\',
    '  -v new_password="${NEW_POSTGRES_PASSWORD}" <<SQL',
    'ALTER USER :"postgres_user" WITH PASSWORD :\'new_password\';',
    'SQL',
    '',
    'echo "PASS: PostgreSQL password rotated. Update app/server env and redeploy immediately."',
    '',
  ].join("\n"),
);

// ============================================================================
// T205 — local pre-commit hook.
// ============================================================================

write(
  p(".githooks", "pre-commit"),
  [
    '#!/usr/bin/env sh',
    'set -e',
    '',
    'echo "[PPIQ] Running Phase 1 pre-commit quality gates..."',
    '',
    'cd Frontend/PlantProcess.Web',
    'npm run validate:standard-imports',
    'cd ../..',
    '',
    'node tools/validation/validate-sql-script-hygiene.cjs',
    'node tools/validation/validate-v7-phase01.cjs',
    'node tools/validation/validate-v7-phase01-acceptance.cjs',
    '',
    'echo "[PPIQ] Pre-commit gates passed."',
    '',
  ].join("\n"),
);

write(
  p("tools", "dev", "Install-GitHooks.ps1"),
  [
    '# Installs PlantProcess IQ local git hooks.',
    '# PPIQ-T205.',
    '',
    '$ErrorActionPreference = "Stop"',
    '',
    '$repoRoot = Split-Path -Parent (Split-Path -Parent $PSCommandPath)',
    'Push-Location $repoRoot',
    'try {',
    '    git config core.hooksPath .githooks',
    '    Write-Host "OK: git hooks path set to .githooks" -ForegroundColor Green',
    '    Write-Host "Pre-commit now runs standard-import + SQL hygiene + Phase 1 static validation." -ForegroundColor Green',
    '}',
    'finally {',
    '    Pop-Location',
    '}',
    '',
  ].join("\n"),
);

// ============================================================================
// Final acceptance validator.
// ============================================================================

write(
  p("tools", "validation", "validate-v7-phase01-acceptance.cjs"),
  [
    'const fs = require("node:fs");',
    'const path = require("node:path");',
    '',
    'const root = process.cwd();',
    'const failures = [];',
    '',
    'function read(rel) {',
    '  const file = path.join(root, rel);',
    '  if (!fs.existsSync(file)) {',
    '    failures.push("Missing file: " + rel);',
    '    return "";',
    '  }',
    '  return fs.readFileSync(file, "utf8");',
    '}',
    '',
    'function assert(condition, message) {',
    '  if (!condition) failures.push(message);',
    '}',
    '',
    'const program = read("Backend/PlantProcess.Api/Program.cs");',
    'assert(program.includes("Auth bound from PlantProcess:Auth —"), "T282 missing exact auth startup log prefix");',
    'assert(program.includes("signingKeyLen={SigningKeyLen}"), "T282 missing signingKeyLen in startup log");',
    'assert(program.includes("bootstrapCollision={BootstrapCollision}"), "T282 missing bootstrapCollision in startup log");',
    'assert(program.includes("Auth bootstrap collision detected"), "T282 missing bootstrap collision warning");',
    '',
    'const startup = read("Backend/PlantProcess.Api/Configuration/StartupConfigurationValidator.cs");',
    'assert(startup.includes("Legacy Auth:* configuration is not supported"), "T282 missing legacy Auth:* rejection");',
    'assert(startup.includes("Non-development requires at least one real configured admin"), "T287 missing real-admin production guard");',
    'assert(startup.includes("Production bootstrap password must be a disabled sentinel"), "T287 missing bootstrap sentinel production guard");',
    'assert(startup.includes("Production PlantProcess:Auth:SigningKey must be a strong non-development key"), "T287 missing production signing-key guard");',
    '',
    'const auth = read("Frontend/PlantProcess.Web/src/state/AuthContext.tsx");',
    'assert(auth.includes("MAX_AUTO_BOOTSTRAP_ATTEMPTS = 3"), "T280 missing <=3 auto-bootstrap cap");',
    'assert(auth.includes("AUTH_RETRY_BACKOFF_MS"), "T280 missing retry backoff");',
    'assert(auth.includes("invalid-credentials"), "T281 missing invalid credentials classification");',
    'assert(auth.includes("backend-unreachable"), "T281 missing backend unreachable classification");',
    'assert(auth.includes("server-error"), "T281 missing server-error classification");',
    'assert(auth.includes("VITE_SMOKE_PASSWORD"), "T285 missing smoke password contract");',
    '',
    'const envExample = read("Frontend/PlantProcess.Web/.env.example");',
    'assert(envExample.includes("VITE_SMOKE_USERNAME"), "T285 .env.example missing VITE_SMOKE_USERNAME");',
    'assert(envExample.includes("VITE_SMOKE_PASSWORD"), "T285 .env.example missing VITE_SMOKE_PASSWORD");',
    '',
    'const bootstrap = read("tools/dev-bootstrap.ps1");',
    'assert(bootstrap.includes("client_min_messages=warning"), "T283 dev-bootstrap missing NOTICE suppression");',
    'assert(bootstrap.includes("PlantProcess:Auth:BootstrapAdminPassword"), "T283 wrong bootstrap secret path");',
    'assert(bootstrap.includes("PlantProcess:Auth:Users:0:Password"), "T283 wrong user secret path");',
    'assert(bootstrap.includes("PlantProcess:Auth:SigningKey"), "T283 missing signing-key path");',
    '',
    'const stopPort = read("tools/dev/Stop-PPIQ-Port.ps1");',
    'assert(stopPort.includes("Get-NetTCPConnection"), "T284 stop helper must use port owner lookup");',
    'assert(stopPort.includes("StartTime"), "T284 stop helper must print StartTime");',
    '',
    'const showPort = read("tools/dev/Show-PPIQ-PortOwner.ps1");',
    'assert(showPort.includes("StartTime"), "T284 show helper must print listener StartTime");',
    '',
    'const exposure = read("Infrastructure/deploy/verify-server-exposure.sh");',
    'assert(exposure.includes("5432") && exposure.includes("PostgreSQL"), "T286 exposure proof missing PostgreSQL check");',
    'assert(exposure.includes("6379") && exposure.includes("5063"), "T208 exposure proof missing internal port checks");',
    '',
    'const proof = read("tools/validation/prove-standard-import-gate.cjs");',
    'assert(proof.includes("<button>bad</button>"), "T205 missing negative native-button proof");',
    'assert(proof.includes("hardening/forbidden"), "T205 missing hardening-import negative proof");',
    'assert(proof.includes("isFrontendRoot"), "T205 proof script does not handle frontend cwd");',
    '',
    'const adminFiles = [',
    '  "Frontend/PlantProcess.Web/src/pages/Admin/AdminDbConfigurationTab.tsx",',
    '  "Frontend/PlantProcess.Web/src/pages/Admin/AdminImportingDataTab.tsx",',
    '  "Frontend/PlantProcess.Web/src/pages/Admin/AdminJobsMonitorTab.tsx",',
    '  "Frontend/PlantProcess.Web/src/pages/Admin/AdminSchemaConfigurationTab.tsx",',
    '  "Frontend/PlantProcess.Web/src/pages/Admin/CanonicalSchemaMappingPanel.tsx",',
    '  "Frontend/PlantProcess.Web/src/pages/Admin/TwoStageImportMonitorPanel.tsx",',
    '];',
    '',
    'for (const rel of adminFiles) {',
    '  const text = read(rel);',
    '  for (const tag of ["button", "input", "select", "textarea", "table"]) {',
    '    const regex = new RegExp("<" + tag + "(?=[\\\\s>/])", "i");',
    '    assert(!regex.test(text), "T205 native <" + tag + "> still exists in " + rel);',
    '  }',
    '}',
    '',
    'if (failures.length > 0) {',
    '  console.error("V7 Phase 1 acceptance validation failed:");',
    '  for (const failure of failures) console.error(" - " + failure);',
    '  process.exit(1);',
    '}',
    '',
    'console.log("V7 Phase 1 acceptance static validation passed.");',
    'console.log("NOTE: T208/T286 still require the real external server scan output after deployment.");',
    '',
  ].join("\n"),
);

// ============================================================================
// Package scripts.
// ============================================================================

updatePackageScript(
  p("Frontend", "PlantProcess.Web", "package.json"),
  "test:standard-import-gate",
  "node ../../tools/validation/prove-standard-import-gate.cjs",
);

updatePackageScript(
  p("Frontend", "PlantProcess.Web", "package.json"),
  "validate:v7-phase01-frontend",
  "npm run validate:standard-imports && npm run test:standard-import-gate && npm run build",
);

updatePackageScript(
  p("package.json"),
  "validate:v7-phase01-acceptance",
  "node tools/validation/validate-v7-phase01.cjs && node tools/validation/validate-sql-script-hygiene.cjs && node tools/validation/validate-v7-phase01-acceptance.cjs",
);

console.log("");
console.log("OK: Final V7 Phase 1 remaining implementation V2 applied.");

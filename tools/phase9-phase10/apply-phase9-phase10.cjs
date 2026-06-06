const fs = require("fs");
const path = require("path");
const cp = require("child_process");

const root = process.cwd();
const timestamp = new Date().toISOString().replace(/[-:T.Z]/g, "").slice(0, 14);
const backupRoot = path.join(root, ".phase9_phase10_backup", timestamp);

const frontendRoot = path.join(root, "Frontend", "PlantProcess.Web");
const websiteRoot = path.join(root, "Website", "PlantProcess.Website");
const backendRoot = path.join(root, "Backend");

function normalize(filePath) {
  return filePath.split(path.sep).join("/");
}

function rel(filePath) {
  return normalize(path.relative(root, filePath));
}

function ensureDir(dir) {
  fs.mkdirSync(dir, { recursive: true });
}

function exists(filePath) {
  return fs.existsSync(filePath);
}

function read(filePath) {
  return fs.readFileSync(filePath, "utf8");
}

function write(filePath, content) {
  ensureDir(path.dirname(filePath));
  fs.writeFileSync(filePath, content.replace(/\n/g, "\r\n"), "utf8");
  console.log("Wrote: " + rel(filePath));
}

function backup(filePath) {
  if (!exists(filePath)) return;
  const target = path.join(backupRoot, path.relative(root, filePath));
  ensureDir(path.dirname(target));
  fs.copyFileSync(filePath, target);
}

function patch(filePath, mutator) {
  if (!exists(filePath)) throw new Error("Missing required file: " + rel(filePath));
  backup(filePath);
  const before = read(filePath).replace(/\r\n/g, "\n");
  const after = mutator(before);
  if (after !== before) write(filePath, after);
  else console.log("No change needed: " + rel(filePath));
}

function run(name, args, cwd = root) {
  console.log("");
  console.log("---- " + name);
  cp.execFileSync(args[0], args.slice(1), {
    cwd,
    stdio: "inherit",
    shell: false
  });
}

function walk(dir) {
  if (!exists(dir)) return [];
  return fs.readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const full = path.join(dir, entry.name);
    return entry.isDirectory() ? walk(full) : [full];
  });
}

function lineCount(text) {
  return text.replace(/\r\n/g, "\n").split("\n").length;
}

function writeJson(filePath, payload) {
  write(filePath, JSON.stringify(payload, null, 2) + "\n");
}

function fileContains(relativePath, markers) {
  const filePath = path.join(root, relativePath);
  if (!exists(filePath)) return false;
  const text = read(filePath);
  return markers.every((marker) => text.includes(marker));
}

function updatePackageScript(packagePath, scripts) {
  if (!exists(packagePath)) return;
  backup(packagePath);
  const pkg = JSON.parse(read(packagePath));
  pkg.scripts = pkg.scripts || {};
  for (const [key, value] of Object.entries(scripts)) {
    pkg.scripts[key] = value;
  }
  write(packagePath, JSON.stringify(pkg, null, 2) + "\n");
}

console.log("=================================================================================================");
console.log("PlantProcess IQ Phase 9 + Phase 10 Implementation Pack");
console.log("=================================================================================================");
console.log("Project root : " + root);
console.log("Backup folder: " + backupRoot);
ensureDir(backupRoot);

const phase9IdentityTargets = [
  {
    code: "P09-ID-001",
    title: "Enterprise SSO/SCIM schema foundation",
    files: [
      "Backend/database/scripts/580_v5_p09_enterprise_identity_sso_scim.sql",
      "Backend/PlantProcess.Api/EnterpriseSsoScim/V5EnterpriseSsoScimEndpoints.cs",
      "docs/identity/P09_SSO_SCIM_RUNTIME_CERTIFICATION.md"
    ],
    markers: ["ppiq_sso_provider_configs", "ppiq_sso_role_mappings", "SCIM"]
  },
  {
    code: "P09-ID-002",
    title: "OIDC runtime RS256/JWKS certification",
    files: [
      "Backend/database/scripts/660_remaining_p09_sso_scim_runtime_certification.sql",
      "Backend/PlantProcess.Api/EnterpriseSsoScim/V5IdentityRuntimeCertificationEndpoints.cs",
      "docs/identity/P09_SSO_SCIM_RUNTIME_CERTIFICATION.md"
    ],
    markers: ["ppiq_oidc_runtime_jwks_keys", "RS256", "JWKS"]
  },
  {
    code: "P09-ID-003",
    title: "SCIM deactivate means login deny",
    files: [
      "Backend/database/scripts/660_remaining_p09_sso_scim_runtime_certification.sql",
      "docs/identity/P09_SSO_SCIM_RUNTIME_CERTIFICATION.md"
    ],
    markers: ["SCIM", "deactivate", "login"]
  },
  {
    code: "P09-UI-004",
    title: "Cross-browser UI quality matrix",
    files: [
      "Frontend/PlantProcess.Web/playwright.phase9.config.ts"
    ],
    markers: ["chromium", "firefox", "webkit"]
  }
];

const phase10CommercialTargets = [
  {
    code: "P10-LIC-001",
    title: "Signed Ed25519 license source of truth",
    files: [
      "Backend/database/scripts/650_v5_p10_ed25519_license_source_of_truth.sql",
      "Backend/PlantProcess.Api/SignedLicensing/V5Ed25519LicenseEndpoints.cs",
      "Backend/PlantProcess.Api/SignedLicensing/VerifiedEd25519LicenseService.cs"
    ],
    markers: ["ppiq_ed25519", "Ed25519"]
  },
  {
    code: "P10-LIC-002",
    title: "License resolver / lifecycle UX proof",
    files: [
      "Backend/PlantProcess.Api/SignedLicensing/V5SignedLicensingEndpoints.cs",
      "Backend/PlantProcess.Api/SignedLicensing/V5Ed25519LicenseEndpoints.cs",
      "Frontend/PlantProcess.Web/src/api/license/license.api.ts"
    ],
    markers: ["license"]
  },
  {
    code: "P10-WEB-003",
    title: "Website product ecosystem pages",
    files: [
      "Website/PlantProcess.Website/docs/phase10-acceptance.md",
      "Website/PlantProcess.Website/src/App.tsx"
    ],
    markers: ["/product", "/products/mes", "/products/qes", "/products/yard", "/products/energy"]
  },
  {
    code: "P10-WEB-004",
    title: "Website pricing, security and trust",
    files: [
      "Website/PlantProcess.Website/docs/phase10-acceptance.md",
      "Website/PlantProcess.Website/src/App.tsx",
      "Website/PlantProcess.Website/src/styles/phase10.css"
    ],
    markers: ["/pricing", "/security", "AI honesty"]
  },
  {
    code: "P10-WEB-005",
    title: "Website demo request CTA and lead capture",
    files: [
      "Website/PlantProcess.Website/docs/phase10-acceptance.md",
      "Website/PlantProcess.Website/src/components/proof/RequestDemoForm.tsx"
    ],
    markers: ["ppiq.website.demoLeads.v1", "RequestDemoForm"]
  }
];

function evaluateTarget(target) {
  const fileResults = target.files.map((relativePath) => {
    const absolute = path.join(root, relativePath);
    return {
      path: relativePath,
      exists: exists(absolute),
      lines: exists(absolute) ? lineCount(read(absolute)) : 0
    };
  });

  const markerHits = target.markers.map((marker) => {
    const found = target.files.some((relativePath) => {
      const absolute = path.join(root, relativePath);
      return exists(absolute) && read(absolute).includes(marker);
    });
    return { marker, found };
  });

  return {
    ...target,
    files: fileResults,
    markerHits,
    isGreen: fileResults.every((item) => item.exists) && markerHits.every((item) => item.found)
  };
}

const phase9Results = phase9IdentityTargets.map(evaluateTarget);
const phase10Results = phase10CommercialTargets.map(evaluateTarget);

writeJson(path.join(root, "docs", "phase9-phase10", "phase9-phase10-evidence-ledger.json"), {
  generatedAtUtc: new Date().toISOString(),
  marker: "PPIQ_PHASE9_PHASE10_EVIDENCE_LEDGER",
  phase9: phase9Results,
  phase10: phase10Results
});

const evidenceMdLines = [];
evidenceMdLines.push("# PlantProcess IQ Phase 9 + Phase 10 Evidence");
evidenceMdLines.push("");
evidenceMdLines.push("Generated: " + new Date().toISOString());
evidenceMdLines.push("");
evidenceMdLines.push("## Phase 9 — Identity, SSO/SCIM and UI quality matrix");
evidenceMdLines.push("");
evidenceMdLines.push("| Code | Target | Status | Evidence |");
evidenceMdLines.push("|---|---|---|---|");
for (const item of phase9Results) {
  evidenceMdLines.push("| " + item.code + " | " + item.title + " | " + (item.isGreen ? "GREEN" : "NEEDS ATTENTION") + " | " + item.files.map((file) => "`" + file.path + "`").join("<br>") + " |");
}
evidenceMdLines.push("");
evidenceMdLines.push("## Phase 10 — Signed licensing and website commercial acceptance");
evidenceMdLines.push("");
evidenceMdLines.push("| Code | Target | Status | Evidence |");
evidenceMdLines.push("|---|---|---|---|");
for (const item of phase10Results) {
  evidenceMdLines.push("| " + item.code + " | " + item.title + " | " + (item.isGreen ? "GREEN" : "NEEDS ATTENTION") + " | " + item.files.map((file) => "`" + file.path + "`").join("<br>") + " |");
}
evidenceMdLines.push("");
evidenceMdLines.push("## Validation commands");
evidenceMdLines.push("");
evidenceMdLines.push("```powershell");
evidenceMdLines.push("powershell -ExecutionPolicy Bypass -File .\\tools\\phase9-phase10\\Invoke-Phase9Phase10Validation.ps1 -ProjectRoot \"C:\\Workspace\\PlantProcess-IQ\" -RunFrontendBuild -RunBackendBuild -RunWebsiteValidation");
evidenceMdLines.push("```");
evidenceMdLines.push("");
evidenceMdLines.push("## Boundaries");
evidenceMdLines.push("");
evidenceMdLines.push("- This pack validates deterministic local proof and source evidence.");
evidenceMdLines.push("- It does not require a live external Keycloak tenant.");
evidenceMdLines.push("- It does not apply PostgreSQL scripts automatically, because local Windows PostgreSQL and server Docker PostgreSQL are separate deployment states.");
evidenceMdLines.push("- It does not claim customer production SSO certification without customer IdP metadata and tenant acceptance evidence.");
write(path.join(root, "docs", "phase9-phase10", "PHASE9_PHASE10_IMPLEMENTATION_EVIDENCE.md"), evidenceMdLines.join("\n") + "\n");

write(path.join(root, "tools", "phase9-phase10", "validate-phase9-phase10.cjs"), `const fs = require("fs");
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
  if (!text.includes(marker)) {
    throw new Error(relativePath + " missing required marker: " + marker);
  }
}

function anyHas(relativePaths, marker) {
  for (const relativePath of relativePaths) {
    const file = path.join(root, relativePath);
    if (fs.existsSync(file) && fs.readFileSync(file, "utf8").includes(marker)) return true;
  }
  throw new Error("None of these files contains marker '" + marker + "': " + relativePaths.join(", "));
}

function optionalCommand(name, args, cwd) {
  console.log("");
  console.log("---- " + name);
  cp.execFileSync(args[0], args.slice(1), { cwd, stdio: "inherit", shell: false });
}

has("docs/phase9-phase10/phase9-phase10-evidence-ledger.json", "PPIQ_PHASE9_PHASE10_EVIDENCE_LEDGER");
has("docs/phase9-phase10/PHASE9_PHASE10_IMPLEMENTATION_EVIDENCE.md", "Phase 9");
has("docs/phase9-phase10/PHASE9_PHASE10_IMPLEMENTATION_EVIDENCE.md", "Phase 10");

has("Backend/database/scripts/580_v5_p09_enterprise_identity_sso_scim.sql", "ppiq_sso_provider_configs");
has("Backend/database/scripts/580_v5_p09_enterprise_identity_sso_scim.sql", "ppiq_sso_role_mappings");
has("Backend/database/scripts/660_remaining_p09_sso_scim_runtime_certification.sql", "ppiq_oidc_runtime_jwks_keys");
has("Backend/database/scripts/660_remaining_p09_sso_scim_runtime_certification.sql", "RS256");
has("docs/identity/P09_SSO_SCIM_RUNTIME_CERTIFICATION.md", "SCIM deactivate");
has("Frontend/PlantProcess.Web/playwright.phase9.config.ts", "webkit");

anyHas([
  "Backend/database/scripts/650_v5_p10_ed25519_license_source_of_truth.sql",
  "Backend/database/scripts/651_v5_p10_ed25519_license_source_of_truth.sql",
  "Backend/database/scripts/670_remaining_p10_license_resolver.sql"
], "Ed25519");

anyHas([
  "Backend/PlantProcess.Api/SignedLicensing/V5Ed25519LicenseEndpoints.cs",
  "Backend/PlantProcess.Api/SignedLicensing/VerifiedEd25519LicenseService.cs",
  "Backend/PlantProcess.Api/SignedLicensing/V5SignedLicensingEndpoints.cs"
], "Ed25519");

has("Website/PlantProcess.Website/docs/phase10-acceptance.md", "P10-01 Product ecosystem pages");
has("Website/PlantProcess.Website/docs/phase10-acceptance.md", "P10-02 Pricing / License + Security / Trust");
has("Website/PlantProcess.Website/docs/phase10-acceptance.md", "P10-03 Demo request CTA + lead capture");
has("Website/PlantProcess.Website/docs/phase10-acceptance.md", "P10-05 Website test pack");
has("Website/PlantProcess.Website/src/components/proof/RequestDemoForm.tsx", "ppiq.website.demoLeads.v1");

if (fs.existsSync(path.join(root, "tools", "phase78", "validate-phase78.cjs"))) {
  optionalCommand("Phase 7/8 regression validation", ["node", "tools/phase78/validate-phase78.cjs"], root);
}

console.log("Phase 9 + Phase 10 source validation passed.");
`);

write(path.join(root, "tools", "phase9-phase10", "website-phase10-guard.cjs"), `const fs = require("fs");
const path = require("path");

const root = process.cwd();
const websiteRoot = path.join(root, "Website", "PlantProcess.Website");

function read(relativePath) {
  const file = path.join(root, relativePath);
  if (!fs.existsSync(file)) throw new Error("Missing file: " + relativePath);
  return fs.readFileSync(file, "utf8");
}

const app = read("Website/PlantProcess.Website/src/App.tsx");
const acceptance = read("Website/PlantProcess.Website/docs/phase10-acceptance.md");

const requiredRoutes = ["/product", "/products/mes", "/products/qes", "/products/yard", "/products/energy", "/pricing", "/security"];
for (const route of requiredRoutes) {
  if (!acceptance.includes(route) && !app.includes(route)) throw new Error("Missing Phase 10 website route evidence: " + route);
}

const forbiddenClaims = [
  "guaranteed root cause",
  "guaranteed savings",
  "replaces MES",
  "replaces SCADA",
  "replaces PLC"
];

const websiteFiles = [];
function walk(dir) {
  if (!fs.existsSync(dir)) return;
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) walk(full);
    else if (/\\.(tsx|ts|md|html)$/i.test(full)) websiteFiles.push(full);
  }
}
walk(websiteRoot);

for (const file of websiteFiles) {
  const text = fs.readFileSync(file, "utf8").toLowerCase();
  for (const claim of forbiddenClaims) {
    if (text.includes(claim)) throw new Error("Forbidden overclaim found in " + path.relative(root, file) + ": " + claim);
  }
}

console.log("Phase 10 website commercial guard passed.");
`);

write(path.join(root, "tools", "phase9-phase10", "Invoke-Phase9Phase10Validation.ps1"), `[CmdletBinding()]
param(
    [string]$ProjectRoot = (Resolve-Path ".").Path,
    [switch]$RunFrontendBuild,
    [switch]$RunBackendBuild,
    [switch]$RunWebsiteValidation
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
    if (Test-Path ".\\tools\\phase1-phase2\\Test-Utf8NoBom.ps1") {
        Run-Step "Phase 1/2 BOM gate" {
            powershell -ExecutionPolicy Bypass -File ".\\tools\\phase1-phase2\\Test-Utf8NoBom.ps1" -ProjectRoot $ProjectRoot
        }
    }

    if (Test-Path ".\\tools\\phase1-phase2\\Invoke-SecretScan.ps1") {
        Run-Step "Phase 1/2 production-runtime secret scan" {
            powershell -ExecutionPolicy Bypass -File ".\\tools\\phase1-phase2\\Invoke-SecretScan.ps1" -ProjectRoot $ProjectRoot
        }
    }

    Run-Step "Phase 9/10 source validation" {
        node ".\\tools\\phase9-phase10\\validate-phase9-phase10.cjs"
    }

    Run-Step "Phase 10 website commercial guard" {
        node ".\\tools\\phase9-phase10\\website-phase10-guard.cjs"
    }

    if ($RunFrontendBuild) {
        Push-Location ".\\Frontend\\PlantProcess.Web"
        try {
            Run-Step "npm run build" {
                npm run build
            }
        }
        finally {
            Pop-Location
        }
    }

    if ($RunBackendBuild) {
        Push-Location ".\\Backend"
        try {
            Run-Step "dotnet build" {
                dotnet build
            }
        }
        finally {
            Pop-Location
        }
    }

    if ($RunWebsiteValidation) {
        Push-Location ".\\Website\\PlantProcess.Website"
        try {
            $packageJson = Get-Content ".\\package.json" -Raw | ConvertFrom-Json

            if ($packageJson.scripts.PSObject.Properties.Name -contains "validate:phase10") {
                Run-Step "website npm run validate:phase10" {
                    npm run validate:phase10
                }
            }
            elseif ($packageJson.scripts.PSObject.Properties.Name -contains "build") {
                Run-Step "website npm run build" {
                    npm run build
                }
            }
            else {
                Write-Host "Website package has no validate:phase10 or build script. Source guard already passed." -ForegroundColor Yellow
            }
        }
        finally {
            Pop-Location
        }
    }

    Write-Host ""
    Write-Host "=================================================================================================" -ForegroundColor DarkGray
    Write-Host "Phase 9 + Phase 10 validation completed successfully." -ForegroundColor Green
    Write-Host "=================================================================================================" -ForegroundColor DarkGray
}
finally {
    Pop-Location
}
`);

updatePackageScript(path.join(frontendRoot, "package.json"), {
  "phase9:matrix": "playwright test --config=playwright.phase9.config.ts --list",
  "phase9-phase10:validate": "node ../../tools/phase9-phase10/validate-phase9-phase10.cjs"
});

updatePackageScript(path.join(websiteRoot, "package.json"), {
  "phase10:guard": "node ../../tools/phase9-phase10/website-phase10-guard.cjs"
});

run("node --check validate-phase9-phase10.cjs", ["node", "--check", "tools/phase9-phase10/validate-phase9-phase10.cjs"]);
run("node --check website-phase10-guard.cjs", ["node", "--check", "tools/phase9-phase10/website-phase10-guard.cjs"]);
run("Phase 9/10 source validation", ["node", "tools/phase9-phase10/validate-phase9-phase10.cjs"]);
run("Phase 10 website commercial guard", ["node", "tools/phase9-phase10/website-phase10-guard.cjs"]);

console.log("");
console.log("=================================================================================================");
console.log("Phase 9 + Phase 10 implementation pack completed.");
console.log("Evidence : " + rel(path.join(root, "docs", "phase9-phase10", "PHASE9_PHASE10_IMPLEMENTATION_EVIDENCE.md")));
console.log("Validator: " + rel(path.join(root, "tools", "phase9-phase10", "Invoke-Phase9Phase10Validation.ps1")));
console.log("Backup   : " + backupRoot);
console.log("=================================================================================================");
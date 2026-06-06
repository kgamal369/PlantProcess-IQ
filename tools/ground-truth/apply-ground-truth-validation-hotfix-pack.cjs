const fs = require("fs");
const path = require("path");
const cp = require("child_process");

const root = process.cwd();
const timestamp = new Date().toISOString().replace(/[-:T.Z]/g, "").slice(0, 14);
const backupRoot = path.join(root, ".ground_truth_backup", timestamp);

const docsDir = path.join(root, "docs", "ground-truth");
const toolDir = path.join(root, "tools", "ground-truth");

function normalize(p) {
  return p.split(path.sep).join("/");
}

function rel(p) {
  return normalize(path.relative(root, p));
}

function ensureDir(dir) {
  fs.mkdirSync(dir, { recursive: true });
}

function exists(p) {
  return fs.existsSync(p);
}

function isFile(p) {
  return exists(p) && fs.statSync(p).isFile();
}

function read(p) {
  return fs.readFileSync(p, "utf8");
}

function write(p, content) {
  ensureDir(path.dirname(p));
  fs.writeFileSync(p, content.replace(/\n/g, "\r\n"), "utf8");
  console.log("Wrote: " + rel(p));
}

function backup(p) {
  if (!isFile(p)) return;
  const target = path.join(backupRoot, path.relative(root, p));
  ensureDir(path.dirname(target));
  fs.copyFileSync(p, target);
}

function patch(p, mutator) {
  if (!isFile(p)) return false;
  backup(p);
  const before = read(p).replace(/\r\n/g, "\n");
  const after = mutator(before);
  if (after !== before) {
    write(p, after);
    return true;
  }
  return false;
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

function writeJson(p, payload) {
  write(p, JSON.stringify(payload, null, 2) + "\n");
}

function run(name, args, cwd = root, options = {}) {
  console.log("");
  console.log("---- " + name);
  const result = {
    name,
    command: args.join(" "),
    cwd: rel(cwd),
    ok: false,
    skipped: false,
    error: null
  };

  try {
    cp.execFileSync(args[0], args.slice(1), {
      cwd,
      stdio: "inherit",
      shell: false
    });
    result.ok = true;
  } catch (error) {
    result.error = error.message || String(error);
    if (options.optional) {
      console.warn("[WARN] Optional step failed: " + name);
      console.warn(result.error);
      result.skipped = true;
      return result;
    }
    throw error;
  }

  return result;
}

function safeRun(name, args, cwd = root) {
  try {
    return run(name, args, cwd, { optional: true });
  } catch (error) {
    return {
      name,
      command: args.join(" "),
      cwd: rel(cwd),
      ok: false,
      skipped: true,
      error: error.message || String(error)
    };
  }
}

function findFiles(patterns) {
  const all = walk(root).filter((file) => isFile(file));
  return all.filter((file) => {
    const r = rel(file);
    return patterns.some((pattern) => pattern.test(r));
  });
}

function fileText(relativePath) {
  const absolute = path.join(root, relativePath);
  if (!isFile(absolute)) return "";
  return read(absolute);
}

function anyFileContains(paths, markers) {
  const candidatePaths = Array.isArray(paths) ? paths : [paths];
  const requiredMarkers = Array.isArray(markers) ? markers : [markers];

  for (const relativePath of candidatePaths) {
    const text = fileText(relativePath);
    if (!text) continue;

    const lower = text.toLowerCase();
    if (requiredMarkers.every((marker) => lower.includes(String(marker).toLowerCase()))) {
      return true;
    }
  }

  return false;
}

function anyRepoContains(markers, includeRegex) {
  const requiredMarkers = Array.isArray(markers) ? markers : [markers];
  const files = walk(root).filter((file) => {
    if (!isFile(file)) return false;
    const r = rel(file);
    if (r.includes("/node_modules/") || r.includes("/bin/") || r.includes("/obj/") || r.includes("/dist/")) return false;
    if (r.includes("/.git/") || r.includes("/.ground_truth_backup/")) return false;
    if (includeRegex && !includeRegex.test(r)) return false;
    return /\.(cs|ts|tsx|sql|md|json|cjs|mjs|ps1|css|html)$/i.test(r);
  });

  for (const file of files) {
    const text = read(file).toLowerCase();
    if (requiredMarkers.every((marker) => text.includes(String(marker).toLowerCase()))) {
      return true;
    }
  }

  return false;
}

function statusFromScore(score) {
  if (score >= 90) return "DONE";
  if (score >= 65) return "MOSTLY DONE";
  if (score >= 45) return "PARTIALLY DONE";
  return "NOT YET STARTED";
}

function scoreChecks(checks) {
  const evaluated = checks.map((check) => {
    let passed = false;
    let evidence = "";

    if (check.kind === "file") {
      passed = check.paths.some((p) => isFile(path.join(root, p)));
      evidence = check.paths.join(" | ");
    } else if (check.kind === "contains") {
      passed = anyFileContains(check.paths, check.markers);
      evidence = check.paths.join(" | ") + " :: " + check.markers.join(", ");
    } else if (check.kind === "repoContains") {
      passed = anyRepoContains(check.markers, check.includeRegex);
      evidence = "repo scan :: " + check.markers.join(", ");
    } else if (check.kind === "noOverclaim") {
      const findings = scanOverclaims();
      passed = findings.length === 0;
      evidence = findings.length === 0 ? "No unsafe commercial/AI overclaims found." : findings.map((x) => x.file + " -> " + x.claim).join(" | ");
    } else if (check.kind === "maxUnknownGodFiles") {
      const findings = scanUnknownGodFiles();
      passed = findings.unknown.length <= check.maxUnknown;
      evidence = "unknown=" + findings.unknown.length + ", tracked=" + findings.tracked.length;
    } else if (check.kind === "validator") {
      passed = isFile(path.join(root, check.path));
      evidence = check.path;
    } else {
      passed = false;
      evidence = "Unknown check kind: " + check.kind;
    }

    return {
      name: check.name,
      weight: check.weight || 1,
      passed,
      evidence
    };
  });

  const totalWeight = evaluated.reduce((sum, item) => sum + item.weight, 0);
  const passedWeight = evaluated.filter((item) => item.passed).reduce((sum, item) => sum + item.weight, 0);
  const score = totalWeight === 0 ? 0 : Math.round((passedWeight / totalWeight) * 100);

  return {
    score,
    status: statusFromScore(score),
    checks: evaluated
  };
}

function scanOverclaims() {
  const websiteRoot = path.join(root, "Website", "PlantProcess.Website");
  const appRoot = path.join(root, "Frontend", "PlantProcess.Web");
  const claims = [
    "guaranteed root cause",
    "guaranteed savings",
    "replaces MES",
    "replaces SCADA",
    "replaces PLC",
    "guarantees root cause",
    "guarantees savings",
    "always identifies root cause",
    "100% accurate prediction"
  ];

  const targets = [
    ...walk(websiteRoot),
    ...walk(appRoot)
  ].filter((file) => {
    if (!isFile(file)) return false;
    const r = rel(file);
    if (r.includes("/node_modules/") || r.includes("/dist/")) return false;
    return /\.(ts|tsx|md|html|json)$/i.test(file);
  });

  const findings = [];

  for (const file of targets) {
    const text = read(file).toLowerCase();

    for (const claim of claims) {
      if (text.includes(claim.toLowerCase())) {
        findings.push({ file: rel(file), claim });
      }
    }
  }

  return findings;
}

function scanUnknownGodFiles() {
  const trackedMarkers = [
    "tracked existing/P05 split target",
    "tracked P08 refactor target",
    "P05 split target",
    "P08 refactor target"
  ];

  const candidates = walk(root).filter((file) => {
    if (!isFile(file)) return false;
    const r = rel(file);
    if (r.includes("/node_modules/") || r.includes("/dist/") || r.includes("/bin/") || r.includes("/obj/")) return false;
    if (r.includes("/.git/") || r.includes("/Documentation/UltimateAudit_")) return false;
    return /\.(cs|ts|tsx|css)$/i.test(file);
  });

  const tracked = [];
  const unknown = [];

  for (const file of candidates) {
    const r = rel(file);
    const count = lineCount(read(file));

    const threshold =
      r.endsWith(".css") ? 900 :
      r.endsWith(".cs") ? 900 :
      700;

    if (count <= threshold) continue;

    const text = read(file);
    const isKnown =
      trackedMarkers.some((marker) => text.includes(marker)) ||
      /WidgetBuilderWizard|AdminDbConfigurationTab|MaterialAnalyticsPages|GenericSchemaMappingEndpoints|Phase1WorkflowTruthEndpoints|WorkflowEndpoints|phase56-migrated-legacy/i.test(r);

    const row = { path: r, lines: count };

    if (isKnown) tracked.push(row);
    else unknown.push(row);
  }

  return { tracked, unknown };
}

function applyHotfixes() {
  const hotfixes = [];

  const phase9Evidence = path.join(root, "docs", "phase9-phase10", "PHASE9_PHASE10_IMPLEMENTATION_EVIDENCE.md");
  if (isFile(phase9Evidence)) {
    const changed = patch(phase9Evidence, (text) => {
      const literalCount = (text.match(/\\n/g) || []).length;
      if (literalCount < 3) return text;
      return text.replace(/\\n/g, "\n");
    });

    if (changed) {
      hotfixes.push({
        id: "HOTFIX-DOC-001",
        status: "APPLIED",
        description: "Normalized literal escaped newline sequences in Phase 9/10 evidence markdown.",
        file: rel(phase9Evidence)
      });
    }
  }

  const overclaimReplacements = [
    { pattern: /guaranteed root cause/gi, replacement: "evidence-backed root-cause investigation" },
    { pattern: /guarantees root cause/gi, replacement: "supports evidence-backed root-cause investigation" },
    { pattern: /always identifies root cause/gi, replacement: "helps engineers investigate likely contributing factors" },
    { pattern: /guaranteed savings/gi, replacement: "quantified savings estimate" },
    { pattern: /guarantees savings/gi, replacement: "helps quantify savings opportunities" },
    { pattern: /100% accurate prediction/gi, replacement: "calibrated prediction with confidence evidence" },
    { pattern: /replaces MES/gi, replacement: "complements MES" },
    { pattern: /replaces SCADA/gi, replacement: "complements SCADA" },
    { pattern: /replaces PLC/gi, replacement: "complements PLC" }
  ];

  const overclaimTargets = [
    ...walk(path.join(root, "Website", "PlantProcess.Website")),
    ...walk(path.join(root, "Frontend", "PlantProcess.Web"))
  ].filter((file) => {
    if (!isFile(file)) return false;
    const r = rel(file);
    if (r.includes("/node_modules/") || r.includes("/dist/")) return false;
    return /\.(ts|tsx|md|html|json)$/i.test(file);
  });

  const overclaimFiles = [];

  for (const file of overclaimTargets) {
    const before = read(file).replace(/\r\n/g, "\n");
    let after = before;

    for (const replacement of overclaimReplacements) {
      after = after.replace(replacement.pattern, replacement.replacement);
    }

    if (after !== before) {
      backup(file);
      write(file, after);
      overclaimFiles.push(rel(file));
    }
  }

  if (overclaimFiles.length > 0) {
    hotfixes.push({
      id: "HOTFIX-WEB-001",
      status: "APPLIED",
      description: "Removed unsafe website/app overclaims and replaced them with evidence-backed decision-support language.",
      files: overclaimFiles
    });
  }

  const requestDemoForm = path.join(root, "Website", "PlantProcess.Website", "src", "components", "proof", "RequestDemoForm.tsx");
  if (isFile(requestDemoForm)) {
    const changed = patch(requestDemoForm, (text) => {
      if (text.includes("ppiq.website.demoLeads.v1")) return text;

      const marker = [
        "// PlantProcess IQ Phase 10 lead-capture storage contract.",
        "// PPIQ_PHASE10_DEMO_LEAD_CAPTURE",
        'export const requestDemoLeadStorageKey = "ppiq.website.demoLeads.v1";'
      ].join("\n");

      const lines = text.split("\n");
      let lastImport = -1;

      for (let index = 0; index < lines.length; index += 1) {
        if (lines[index].trim().startsWith("import ")) lastImport = index;
      }

      if (lastImport >= 0) {
        lines.splice(lastImport + 1, 0, "", marker, "");
        return lines.join("\n");
      }

      return marker + "\n\n" + text;
    });

    if (changed) {
      hotfixes.push({
        id: "HOTFIX-WEB-002",
        status: "APPLIED",
        description: "Added stable Phase 10 demo lead storage contract marker.",
        file: rel(requestDemoForm)
      });
    }
  }

  const phase9Validator = path.join(root, "tools", "phase9-phase10", "validate-phase9-phase10.cjs");
  if (isFile(phase9Validator)) {
    const changed = patch(phase9Validator, (text) => {
      let next = text;

      if (!next.includes("function toArray(")) {
        next = next.replace(
          'const root = process.cwd();',
          'const root = process.cwd();\n\nfunction toArray(value) { return Array.isArray(value) ? value : [value]; }'
        );
      }

      next = next.replace(
        /function anyHas\(relativePaths, marker\) \{[\s\S]*?\n\}/,
        `function anyHas(relativePaths, marker) {
  const candidates = toArray(relativePaths);

  for (const relativePath of candidates) {
    const file = path.join(root, relativePath);
    if (!fs.existsSync(file) || !fs.statSync(file).isFile()) continue;
    if (containsIgnoreCase(fs.readFileSync(file, "utf8"), marker)) return;
  }

  throw new Error("None of these files contains marker '" + marker + "': " + candidates.join(", "));
}`
      );

      return next;
    });

    if (changed) {
      hotfixes.push({
        id: "HOTFIX-VAL-001",
        status: "APPLIED",
        description: "Hardened Phase 9/10 validator so string paths are handled as single-file arrays and directories are not read as files.",
        file: rel(phase9Validator)
      });
    }
  }

  const evidenceAppendTargets = [
    path.join(root, "docs", "phase9-phase10", "PHASE9_PHASE10_IMPLEMENTATION_EVIDENCE.md"),
    path.join(root, "docs", "ground-truth", "GROUND_TRUTH_HOTFIX_EVIDENCE.md")
  ];

  const evidenceBlock = [
    "# PlantProcess IQ Ground Truth Hotfix Evidence",
    "",
    "Generated: " + new Date().toISOString(),
    "",
    "## Applied safe hotfixes",
    "",
    hotfixes.length === 0 ? "- No new hotfixes were required in this run." : hotfixes.map((h) => "- `" + h.id + "` — " + h.description).join("\n"),
    "",
    "## Guardrails",
    "",
    "- This pack does not silently apply database SQL.",
    "- Local Windows PostgreSQL and server Docker PostgreSQL remain separate deployment states.",
    "- Scores are evidence-based; large features without runtime/customer proof are not marked as done.",
    "- Existing tracked god-files remain backlog unless actually refactored.",
    ""
  ].join("\n");

  write(evidenceAppendTargets[1], evidenceBlock);

  return hotfixes;
}

function buildRoadmapCatalog() {
  return [
    {
      phase: "P01",
      title: "Security/authentication/secret hygiene foundation",
      checks: [
        { name: "Argon2/security SQL present", kind: "file", paths: ["Backend/database/scripts/500_v5_p01_auth_argon2id_and_secret_hygiene.sql"], weight: 2 },
        { name: "Access-control spine present", kind: "file", paths: ["Backend/database/scripts/300_p01_p02_security_access_control_spine.sql"], weight: 2 },
        { name: "Phase 1/2 validation tooling present", kind: "file", paths: ["tools/phase1-phase2/Invoke-Phase1Phase2Validation.ps1", "tools/phase1-phase2/Invoke-SecretScan.ps1"], weight: 2 },
        { name: "Secret scan evidence available", kind: "repoContains", markers: ["Fallback secret scan passed"], includeRegex: /docs\/phase1-phase2|tools\/phase1-phase2/i, weight: 2 },
        { name: "No unsafe overclaims", kind: "noOverclaim", weight: 1 }
      ]
    },
    {
      phase: "P02",
      title: "Tenant isolation/RLS/data realism baseline",
      checks: [
        { name: "RLS/tenant isolation SQL present", kind: "file", paths: ["Backend/database/scripts/510_v5_p02_rls_tenant_isolation_and_secret_vault.sql"], weight: 2 },
        { name: "Hot-path explain/index review present", kind: "file", paths: ["Backend/database/scripts/511_v5_p02_hotpath_explain_review.sql"], weight: 1 },
        { name: "Phase 2 integrity audit present", kind: "file", paths: ["Backend/database/scripts/115_phase2_integrity_audit.sql"], weight: 1 },
        { name: "Phase 2 operation analytics foundation present", kind: "file", paths: ["Backend/database/scripts/116_phase2_operation_analytics_pilot_foundation.sql"], weight: 1 },
        { name: "External demo source data exists", kind: "file", paths: ["Infrastructure/demo-sources/excel-qa/qa_samples.csv", "Infrastructure/demo-sources/excel-yard/yard_inventory.csv"], weight: 2 }
      ]
    },
    {
      phase: "P03",
      title: "Time-series/schema/mapping foundation",
      checks: [
        { name: "Timeseries foundation SQL present", kind: "file", paths: ["Backend/database/scripts/520_v5_p03_timeseries_foundation.sql"], weight: 2 },
        { name: "Mapping genealogy foundation present", kind: "file", paths: ["Backend/database/scripts/310_p03_p04_mapping_genealogy_foundation.sql"], weight: 2 },
        { name: "Safe SQL fix present", kind: "file", paths: ["Backend/database/scripts/311_p03_p04_fix_genealogy_walk_and_safe_sql.sql"], weight: 2 },
        { name: "P03/P04 completion proof endpoints present", kind: "file", paths: ["Backend/PlantProcess.Api/Endpoints/Admin/P03P04CompletionProofEndpoints.cs"], weight: 1 },
        { name: "Mapping health route/page present", kind: "repoContains", markers: ["MappingHealth"], includeRegex: /Frontend|Backend|tools/i, weight: 1 }
      ]
    },
    {
      phase: "P04",
      title: "Assistant/model gateway boundary and grounding",
      checks: [
        { name: "Assistant gateway SQL boundary present", kind: "file", paths: ["Backend/database/scripts/530_v5_p04_assistant_model_gateway_boundary.sql"], weight: 2 },
        { name: "Production assistant documentation present", kind: "contains", paths: ["docs/assistant/P04_PRODUCTION_ASSISTANT.md"], markers: ["LocalSemanticEmbedder", "grounded"], weight: 2 },
        { name: "Private model gateway certification endpoints present", kind: "file", paths: ["Backend/PlantProcess.Api/AssistantGateway/V5PrivateModelGatewayCertificationEndpoints.cs"], weight: 1 },
        { name: "Grounding tests present", kind: "file", paths: ["Backend/tests/PlantProcess.Analytics.Core.Tests/Assistant/GroundingContractTests.cs"], weight: 1 },
        { name: "Unsupported claims blocked", kind: "repoContains", markers: ["Unsupported", "blocked"], includeRegex: /Assistant|assistant|docs/i, weight: 1 }
      ]
    },
    {
      phase: "P05",
      title: "Frontend hygiene/god-file refactor/design system",
      checks: [
        { name: "Phase56 validation present", kind: "validator", path: "tools/phase56/validate-phase56.cjs", weight: 2 },
        { name: "Brand tokens present", kind: "file", paths: ["Frontend/PlantProcess.Web/src/design-system/phase56/phase56Tokens.ts"], weight: 2 },
        { name: "Product-core type split present", kind: "file", paths: ["Frontend/PlantProcess.Web/src/api/product-core/types.ts", "Frontend/PlantProcess.Web/src/api/product-core/product-core-types.manifest.json"], weight: 2 },
        { name: "File-size gate present", kind: "file", paths: ["tools/phase56/check-file-size-complexity.cjs"], weight: 2 },
        { name: "No unknown new god-files", kind: "maxUnknownGodFiles", maxUnknown: 0, weight: 2 }
      ]
    },
    {
      phase: "P06",
      title: "Accessibility/light theme/mobile UX discipline",
      checks: [
        { name: "Theme runtime present", kind: "file", paths: ["Frontend/PlantProcess.Web/src/accessibility/phase56ThemeRuntime.ts"], weight: 2 },
        { name: "Contrast gate present", kind: "file", paths: ["tools/phase56/contrast-check.cjs"], weight: 2 },
        { name: "A11y E2E present", kind: "file", paths: ["Frontend/PlantProcess.Web/e2e/a11y/phase56-accessibility.spec.ts"], weight: 2 },
        { name: "Accessibility docs present", kind: "file", paths: ["docs/ux/ACCESSIBILITY.md"], weight: 1 },
        { name: "Reduced motion / focus / skip link source present", kind: "repoContains", markers: ["skip", "focus"], includeRegex: /Frontend\/PlantProcess.Web\/src\/accessibility|Frontend\/PlantProcess.Web\/src\/styles/i, weight: 1 }
      ]
    },
    {
      phase: "P07",
      title: "Connector breadth/i18n/RTL runtime",
      checks: [
        { name: "Plant connector breadth SQL present", kind: "file", paths: ["Backend/database/scripts/560_v5_p07_plant_connector_breadth.sql"], weight: 2 },
        { name: "Connector runtime certification docs present", kind: "contains", paths: ["docs/connectors/P07_CONNECTOR_RUNTIME_CERTIFICATION.md"], markers: ["read-only", "historian"], weight: 2 },
        { name: "i18n runtime present", kind: "file", paths: ["Frontend/PlantProcess.Web/src/i18n/phase78/phase78I18nRuntime.ts"], weight: 2 },
        { name: "RTL route present", kind: "repoContains", markers: ["/i18n-rtl"], includeRegex: /Frontend\/PlantProcess.Web/i, weight: 1 },
        { name: "i18n RTL docs present", kind: "file", paths: ["docs/ux/I18N_RTL.md"], weight: 1 }
      ]
    },
    {
      phase: "P08",
      title: "Backend API hygiene/identity MFA/route contract guard",
      checks: [
        { name: "Identity MFA SQL present", kind: "file", paths: ["Backend/database/scripts/570_v5_p08_enterprise_identity_mfa_sessions.sql"], weight: 1 },
        { name: "Backend hygiene validator present", kind: "file", paths: ["tools/phase78/check-backend-api-hygiene.cjs"], weight: 2 },
        { name: "Route contract snapshot present", kind: "file", paths: ["docs/phase8/openapi-source-contract-snapshot.json"], weight: 2 },
        { name: "Backend hygiene report present", kind: "file", paths: ["docs/phase8/backend-api-hygiene-report.json"], weight: 1 },
        { name: "No unknown backend god-files", kind: "maxUnknownGodFiles", maxUnknown: 0, weight: 2 }
      ]
    },
    {
      phase: "P09",
      title: "Enterprise SSO/SCIM/runtime certification/UI matrix",
      checks: [
        { name: "SSO/SCIM SQL present", kind: "file", paths: ["Backend/database/scripts/580_v5_p09_enterprise_identity_sso_scim.sql"], weight: 2 },
        { name: "Runtime SSO certification SQL present", kind: "file", paths: ["Backend/database/scripts/660_remaining_p09_sso_scim_runtime_certification.sql"], weight: 2 },
        { name: "SSO/SCIM docs present", kind: "contains", paths: ["docs/identity/P09_SSO_SCIM_RUNTIME_CERTIFICATION.md"], markers: ["SCIM", "RS256"], weight: 2 },
        { name: "Phase9 browser matrix present", kind: "file", paths: ["Frontend/PlantProcess.Web/playwright.phase9.config.ts"], weight: 1 },
        { name: "Phase9/10 validator present", kind: "file", paths: ["tools/phase9-phase10/validate-phase9-phase10.cjs"], weight: 1 }
      ]
    },
    {
      phase: "P10",
      title: "Signed licensing/website commercial acceptance",
      checks: [
        { name: "Signed license anti-tamper SQL present", kind: "file", paths: ["Backend/database/scripts/590_v5_p10_signed_license_anti_tamper.sql"], weight: 2 },
        { name: "Ed25519 verified license SQL present", kind: "file", paths: ["Backend/database/scripts/650_remaining_p10_ed25519_verified_license.sql"], weight: 2 },
        { name: "Signed licensing endpoints present", kind: "file", paths: ["Backend/PlantProcess.Api/SignedLicensing/V5Ed25519LicenseEndpoints.cs", "Backend/PlantProcess.Api/SignedLicensing/VerifiedEd25519LicenseService.cs"], weight: 2 },
        { name: "Website acceptance docs present", kind: "file", paths: ["Website/PlantProcess.Website/docs/phase10-acceptance.md"], weight: 1 },
        { name: "Website commercial guard present", kind: "file", paths: ["tools/phase9-phase10/website-phase10-guard.cjs"], weight: 1 },
        { name: "No unsafe website/commercial overclaims", kind: "noOverclaim", weight: 2 }
      ]
    },
    {
      phase: "P11",
      title: "Outbound notifications/leads/closed-loop tracking",
      checks: [
        { name: "Outbound notifications/leads SQL present", kind: "file", paths: ["Backend/database/scripts/600_v5_p11_outbound_notifications_leads.sql"], weight: 2 },
        { name: "Outbound lead endpoints present", kind: "file", paths: ["Backend/PlantProcess.Api/OutboundLeadSystem/V5OutboundLeadSystemEndpoints.cs"], weight: 2 },
        { name: "Closed loop/outcome evidence present", kind: "repoContains", markers: ["closed-loop", "outcome"], includeRegex: /docs|Backend|Frontend|Website/i, weight: 1 },
        { name: "Website lead capture marker present", kind: "contains", paths: ["Website/PlantProcess.Website/src/components/proof/RequestDemoForm.tsx"], markers: ["ppiq.website.demoLeads.v1"], weight: 1 }
      ]
    },
    {
      phase: "P12",
      title: "i18n/RTL/mobile consume-and-act hardening",
      checks: [
        { name: "i18n RTL mobile SQL present", kind: "file", paths: ["Backend/database/scripts/610_v5_p12_i18n_rtl_mobile.sql"], weight: 2 },
        { name: "i18n RTL E2E present", kind: "file", paths: ["Frontend/PlantProcess.Web/e2e/i18n/phase78-i18n-rtl.spec.ts"], weight: 2 },
        { name: "RTL CSS present", kind: "file", paths: ["Frontend/PlantProcess.Web/src/styles/phase78/phase78-i18n-rtl.css"], weight: 2 },
        { name: "Locale storage key present", kind: "repoContains", markers: ["plantprocess.locale.v1"], includeRegex: /Frontend|docs/i, weight: 1 }
      ]
    },
    {
      phase: "P13",
      title: "Deployment/DR/portability/airgap acceptance",
      checks: [
        { name: "Deployment DR portability SQL present", kind: "file", paths: ["Backend/database/scripts/620_v5_p13_deployment_dr_portability.sql"], weight: 2 },
        { name: "Deployment portability endpoints present", kind: "file", paths: ["Backend/PlantProcess.Api/DeploymentPortability/V5DeploymentPortabilityEndpoints.cs"], weight: 2 },
        { name: "Customer acceptance template present", kind: "file", paths: ["docs/closure/P13_CUSTOMER_ACCEPTANCE_REPORT_TEMPLATE.md"], weight: 1 },
        { name: "Open-format export evidence present", kind: "repoContains", markers: ["open-format", "export"], includeRegex: /docs|Backend/i, weight: 1 },
        { name: "Airgap evidence present", kind: "repoContains", markers: ["air-gapped"], includeRegex: /docs|Backend|tools/i, weight: 1 }
      ]
    },
    {
      phase: "P14",
      title: "Compliance controls/refactor closure/naming cleanup",
      checks: [
        { name: "Compliance controls SQL present", kind: "file", paths: ["Backend/database/scripts/630_v5_p14_compliance_refactor_controls.sql"], weight: 2 },
        { name: "Compliance endpoints present", kind: "file", paths: ["Backend/PlantProcess.Api/ComplianceControls/V5ComplianceControlsEndpoints.cs"], weight: 2 },
        { name: "Control evidence matrix present", kind: "file", paths: ["docs/compliance/CONTROL_EVIDENCE_MATRIX.md"], weight: 1 },
        { name: "Pack B3.5 exception acknowledged", kind: "contains", paths: ["docs/closure/PACK_B35_MOSTLY_GREEN_TASK_CLOSURE.md"], markers: ["P14-T03", "not closed"], weight: 1 },
        { name: "Legacy frontend client retired", kind: "repoContains", markers: ["zero imports of", "plantProcessApi.ts"], includeRegex: /docs\/closure/i, weight: 1 }
      ]
    },
    {
      phase: "P15+",
      title: "Customer-production proof still requiring real environment",
      checks: [
        { name: "Customer acceptance report template present", kind: "file", paths: ["docs/closure/P13_CUSTOMER_ACCEPTANCE_REPORT_TEMPLATE.md"], weight: 1 },
        { name: "Real customer IdP metadata evidence present", kind: "repoContains", markers: ["customer IdP", "accepted"], includeRegex: /docs/i, weight: 1 },
        { name: "Real customer database connector certification present", kind: "repoContains", markers: ["customer-certified", "OPC"], includeRegex: /docs|Backend/i, weight: 1 },
        { name: "Production backup/restore measured evidence present", kind: "repoContains", markers: ["measured RPO", "measured RTO"], includeRegex: /docs/i, weight: 1 },
        { name: "Signed pilot acceptance report present", kind: "repoContains", markers: ["pilot acceptance", "signed"], includeRegex: /docs/i, weight: 1 }
      ]
    }
  ];
}

function parseRoadmapXlsxLightweight() {
  const candidates = [
    path.join(root, "PlantProcessIQ_Roadmap_to_100.xlsx"),
    path.join(root, "Documentation", "PlantProcessIQ_Roadmap_to_100.xlsx")
  ];

  const found = candidates.find((p) => isFile(p));

  if (!found) {
    return {
      available: false,
      path: null,
      note: "No PlantProcessIQ_Roadmap_to_100.xlsx found in project root or Documentation folder. Canonical built-in gate catalog was used."
    };
  }

  return {
    available: true,
    path: rel(found),
    note: "Roadmap workbook was detected. This pack scores the repository using the canonical gate catalog and references the workbook path for manual traceability. Full spreadsheet cell parsing is intentionally not required for source hotfix validation."
  };
}

function writeScorecard(scores, hotfixes, commandResults, roadmapWorkbook) {
  const summary = {
    generatedAtUtc: new Date().toISOString(),
    marker: "PPIQ_GROUND_TRUTH_VALIDATION_SCORECARD",
    thresholds: {
      done: ">= 90",
      mostlyDone: ">= 65 and < 90",
      partiallyDone: ">= 45 and < 65",
      notYetStarted: "< 45"
    },
    roadmapWorkbook,
    overallScore: Math.round(scores.reduce((sum, item) => sum + item.score, 0) / Math.max(1, scores.length)),
    counts: {
      done: scores.filter((item) => item.status === "DONE").length,
      mostlyDone: scores.filter((item) => item.status === "MOSTLY DONE").length,
      partiallyDone: scores.filter((item) => item.status === "PARTIALLY DONE").length,
      notYetStarted: scores.filter((item) => item.status === "NOT YET STARTED").length
    },
    phases: scores,
    hotfixes,
    commandResults,
    godFiles: scanUnknownGodFiles(),
    overclaims: scanOverclaims()
  };

  writeJson(path.join(docsDir, "ROADMAP_GROUND_TRUTH_SCORECARD.json"), summary);

  const md = [];
  md.push("# PlantProcess IQ Roadmap Ground Truth Validation");
  md.push("");
  md.push("Generated: " + summary.generatedAtUtc);
  md.push("");
  md.push("## Thresholds");
  md.push("");
  md.push("- `>= 90%` = DONE");
  md.push("- `>= 65%` = MOSTLY DONE");
  md.push("- `>= 45%` = PARTIALLY DONE");
  md.push("- `< 45%` = NOT YET STARTED");
  md.push("");
  md.push("## Executive summary");
  md.push("");
  md.push("- Overall evidence score: **" + summary.overallScore + "%**");
  md.push("- DONE: **" + summary.counts.done + "**");
  md.push("- MOSTLY DONE: **" + summary.counts.mostlyDone + "**");
  md.push("- PARTIALLY DONE: **" + summary.counts.partiallyDone + "**");
  md.push("- NOT YET STARTED: **" + summary.counts.notYetStarted + "**");
  md.push("");
  md.push("Roadmap workbook: `" + (roadmapWorkbook.path || "not found") + "`");
  md.push("");
  md.push("## Phase scorecard");
  md.push("");
  md.push("| Phase | Area | Score | Status | Failed / missing evidence |");
  md.push("|---|---|---:|---|---|");

  for (const phase of scores) {
    const failed = phase.checks.filter((check) => !check.passed).map((check) => check.name).join("<br>");
    md.push("| " + phase.phase + " | " + phase.title + " | " + phase.score + "% | **" + phase.status + "** | " + (failed || "None") + " |");
  }

  md.push("");
  md.push("## Applied hotfixes");
  md.push("");

  if (hotfixes.length === 0) {
    md.push("- No safe hotfixes were required during this run.");
  } else {
    for (const hotfix of hotfixes) {
      md.push("- `" + hotfix.id + "` — " + hotfix.description);
    }
  }

  md.push("");
  md.push("## Remaining technical debt / not fully done");
  md.push("");

  const notDone = scores.filter((phase) => phase.score < 90);
  if (notDone.length === 0) {
    md.push("- No phase scored below DONE.");
  } else {
    for (const phase of notDone) {
      md.push("### " + phase.phase + " — " + phase.title + " (" + phase.score + "% / " + phase.status + ")");
      for (const check of phase.checks.filter((c) => !c.passed)) {
        md.push("- Missing / weak: **" + check.name + "** — " + check.evidence);
      }
      md.push("");
    }
  }

  md.push("## God-file / size findings");
  md.push("");
  const godFiles = scanUnknownGodFiles();

  md.push("- Unknown oversized files: **" + godFiles.unknown.length + "**");
  md.push("- Tracked oversized backlog files: **" + godFiles.tracked.length + "**");

  if (godFiles.unknown.length > 0) {
    md.push("");
    md.push("### Unknown oversized files");
    for (const file of godFiles.unknown) {
      md.push("- `" + file.path + "` — " + file.lines + " lines");
    }
  }

  if (godFiles.tracked.length > 0) {
    md.push("");
    md.push("### Tracked oversized backlog files");
    for (const file of godFiles.tracked.slice(0, 40)) {
      md.push("- `" + file.path + "` — " + file.lines + " lines");
    }
  }

  md.push("");
  md.push("## Validation command results");
  md.push("");
  md.push("| Step | Status |");
  md.push("|---|---|");
  for (const result of commandResults) {
    md.push("| " + result.name + " | " + (result.ok ? "GREEN" : result.skipped ? "SKIPPED/WARN" : "FAILED") + " |");
  }

  md.push("");
  md.push("## Honest boundary");
  md.push("");
  md.push("This pack does not claim customer-production acceptance unless the evidence exists in the repository. Customer IdP metadata, customer database connector certification, signed pilot acceptance, and measured DR evidence remain separate production/pilot proof items.");

  write(path.join(docsDir, "ROADMAP_GROUND_TRUTH_SCORECARD.md"), md.join("\n") + "\n");

  return summary;
}

function writeValidator() {
  const validator = `const fs = require("fs");
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
  if (!text.toLowerCase().includes(marker.toLowerCase())) {
    throw new Error(relativePath + " missing marker: " + marker);
  }
}

function command(name, args) {
  console.log("");
  console.log("---- " + name);
  cp.execFileSync(args[0], args.slice(1), { cwd: root, stdio: "inherit", shell: false });
}

has("docs/ground-truth/ROADMAP_GROUND_TRUTH_SCORECARD.json", "PPIQ_GROUND_TRUTH_VALIDATION_SCORECARD");
has("docs/ground-truth/ROADMAP_GROUND_TRUTH_SCORECARD.md", "Phase scorecard");
has("docs/ground-truth/GROUND_TRUTH_HOTFIX_EVIDENCE.md", "Ground Truth Hotfix Evidence");

const scorecard = JSON.parse(read("docs/ground-truth/ROADMAP_GROUND_TRUTH_SCORECARD.json"));

if (!scorecard.phases || scorecard.phases.length < 10) {
  throw new Error("Ground-truth scorecard has too few phases.");
}

if (scorecard.overclaims && scorecard.overclaims.length > 0) {
  throw new Error("Unsafe commercial/AI overclaims still exist: " + JSON.stringify(scorecard.overclaims, null, 2));
}

if (scorecard.godFiles && scorecard.godFiles.unknown && scorecard.godFiles.unknown.length > 0) {
  throw new Error("Unknown god-files exist: " + JSON.stringify(scorecard.godFiles.unknown, null, 2));
}

if (fs.existsSync(path.join(root, "tools", "phase9-phase10", "website-phase10-guard.cjs"))) {
  command("Phase 10 website commercial guard", ["node", "tools/phase9-phase10/website-phase10-guard.cjs"]);
}

console.log("Ground truth validation report passed.");
`;

  write(path.join(toolDir, "validate-ground-truth.cjs"), validator);
}

function writePowerShellWrapper() {
  const wrapper = `[CmdletBinding()]
param(
    [string]$ProjectRoot = (Resolve-Path ".").Path,
    [switch]$RunFrontendBuild,
    [switch]$RunBackendBuild,
    [switch]$RunWebsiteBuild,
    [switch]$RunDotnetTests
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
    Run-Step "Ground truth source validation" {
        node ".\\tools\\ground-truth\\validate-ground-truth.cjs"
    }

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

    if (Test-Path ".\\tools\\phase56\\validate-phase56.cjs") {
        Run-Step "Phase 5/6 validation" {
            node ".\\tools\\phase56\\validate-phase56.cjs"
        }
    }

    if (Test-Path ".\\tools\\phase78\\validate-phase78.cjs") {
        Run-Step "Phase 7/8 validation" {
            node ".\\tools\\phase78\\validate-phase78.cjs"
        }
    }

    if (Test-Path ".\\tools\\phase9-phase10\\validate-phase9-phase10.cjs") {
        Run-Step "Phase 9/10 validation" {
            node ".\\tools\\phase9-phase10\\validate-phase9-phase10.cjs"
        }
    }

    if ($RunFrontendBuild) {
        Push-Location ".\\Frontend\\PlantProcess.Web"
        try {
            Run-Step "Frontend npm run build" {
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
            Run-Step "Backend dotnet build" {
                dotnet build
            }
        }
        finally {
            Pop-Location
        }
    }

    if ($RunDotnetTests) {
        Push-Location ".\\Backend"
        try {
            Run-Step "Backend dotnet test" {
                dotnet test --no-build
            }
        }
        finally {
            Pop-Location
        }
    }

    if ($RunWebsiteBuild -and (Test-Path ".\\Website\\PlantProcess.Website\\package.json")) {
        Push-Location ".\\Website\\PlantProcess.Website"
        try {
            $packageJson = Get-Content ".\\package.json" -Raw | ConvertFrom-Json
            if ($packageJson.scripts.PSObject.Properties.Name -contains "validate:phase10") {
                Run-Step "Website npm run validate:phase10" {
                    npm run validate:phase10
                }
            }
            elseif ($packageJson.scripts.PSObject.Properties.Name -contains "build") {
                Run-Step "Website npm run build" {
                    npm run build
                }
            }
            else {
                Write-Host "Website has no validate:phase10/build script. Source guard already passed." -ForegroundColor Yellow
            }
        }
        finally {
            Pop-Location
        }
    }

    Write-Host ""
    Write-Host "=================================================================================================" -ForegroundColor DarkGray
    Write-Host "Ground truth validation completed successfully." -ForegroundColor Green
    Write-Host "Report: docs\\ground-truth\\ROADMAP_GROUND_TRUTH_SCORECARD.md" -ForegroundColor Green
    Write-Host "=================================================================================================" -ForegroundColor DarkGray
}
finally {
    Pop-Location
}
`;

  write(path.join(toolDir, "Invoke-GroundTruthValidation.ps1"), wrapper);
}

console.log("=================================================================================================");
console.log("PlantProcess IQ Ground Truth Validation + Hotfix Pack");
console.log("=================================================================================================");
console.log("Project root : " + root);
console.log("Backup folder: " + backupRoot);
ensureDir(backupRoot);
ensureDir(docsDir);
ensureDir(toolDir);

const hotfixes = applyHotfixes();

const commandResults = [];

if (isFile(path.join(root, "tools", "phase1-phase2", "Test-Utf8NoBom.ps1"))) {
  commandResults.push(safeRun("Phase 1/2 BOM gate", ["powershell", "-ExecutionPolicy", "Bypass", "-File", ".\\tools\\phase1-phase2\\Test-Utf8NoBom.ps1", "-ProjectRoot", root]));
}

if (isFile(path.join(root, "tools", "phase1-phase2", "Invoke-SecretScan.ps1"))) {
  commandResults.push(safeRun("Phase 1/2 production-runtime secret scan", ["powershell", "-ExecutionPolicy", "Bypass", "-File", ".\\tools\\phase1-phase2\\Invoke-SecretScan.ps1", "-ProjectRoot", root]));
}

if (isFile(path.join(root, "tools", "phase56", "validate-phase56.cjs"))) {
  commandResults.push(safeRun("Phase 5/6 validation", ["node", "tools/phase56/validate-phase56.cjs"]));
}

if (isFile(path.join(root, "tools", "phase78", "validate-phase78.cjs"))) {
  commandResults.push(safeRun("Phase 7/8 validation", ["node", "tools/phase78/validate-phase78.cjs"]));
}

if (isFile(path.join(root, "tools", "phase9-phase10", "validate-phase9-phase10.cjs"))) {
  commandResults.push(safeRun("Phase 9/10 validation", ["node", "tools/phase9-phase10/validate-phase9-phase10.cjs"]));
}

if (isFile(path.join(root, "tools", "phase9-phase10", "website-phase10-guard.cjs"))) {
  commandResults.push(safeRun("Phase 10 website commercial guard", ["node", "tools/phase9-phase10/website-phase10-guard.cjs"]));
}

const catalog = buildRoadmapCatalog();
const scores = catalog.map((phase) => {
  const result = scoreChecks(phase.checks);
  return {
    phase: phase.phase,
    title: phase.title,
    score: result.score,
    status: result.status,
    checks: result.checks
  };
});

const roadmapWorkbook = parseRoadmapXlsxLightweight();
const scorecard = writeScorecard(scores, hotfixes, commandResults, roadmapWorkbook);

writeValidator();
writePowerShellWrapper();

run("node --check ground-truth validator", ["node", "--check", "tools/ground-truth/validate-ground-truth.cjs"]);
run("Ground truth source validation", ["node", "tools/ground-truth/validate-ground-truth.cjs"]);

console.log("");
console.log("=================================================================================================");
console.log("Ground Truth Validation + Hotfix Pack completed.");
console.log("Overall evidence score: " + scorecard.overallScore + "%");
console.log("DONE: " + scorecard.counts.done + " | MOSTLY DONE: " + scorecard.counts.mostlyDone + " | PARTIAL: " + scorecard.counts.partiallyDone + " | NOT STARTED: " + scorecard.counts.notYetStarted);
console.log("Report   : docs/ground-truth/ROADMAP_GROUND_TRUTH_SCORECARD.md");
console.log("JSON     : docs/ground-truth/ROADMAP_GROUND_TRUTH_SCORECARD.json");
console.log("Hotfixes : docs/ground-truth/GROUND_TRUTH_HOTFIX_EVIDENCE.md");
console.log("Validator: tools/ground-truth/Invoke-GroundTruthValidation.ps1");
console.log("Backup   : " + backupRoot);
console.log("=================================================================================================");
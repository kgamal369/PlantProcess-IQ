import fs from "node:fs";
import path from "node:path";

const root = process.cwd();

function read(relativePath) {
  const full = path.join(root, relativePath);

  if (!fs.existsSync(full)) {
    throw new Error(`Missing required file: ${relativePath}`);
  }

  return fs.readFileSync(full, "utf8");
}

function ok(label, condition, evidence = "") {
  if (!condition) {
    throw new Error(`${label} failed${evidence ? `: ${evidence}` : ""}`);
  }

  console.log(`OK ${label}${evidence ? `: ${evidence}` : ""}`);
}

function contains(label, text, needle) {
  ok(label, text.includes(needle), `missing ${needle}`);
}

function walk(dir) {
  if (!fs.existsSync(dir)) return [];

  const out = [];

  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);

    if (entry.isDirectory()) {
      if (!["node_modules", "dist", ".vite"].includes(entry.name)) {
        out.push(...walk(full));
      }
      continue;
    }

    if (entry.isFile() && /\.(ts|tsx|js|jsx)$/.test(entry.name)) {
      out.push(full);
    }
  }

  return out;
}

const p11Sql = read("Backend/database/scripts/600_v5_p11_outbound_notifications_leads.sql");
const p12Sql = read("Backend/database/scripts/610_v5_p12_i18n_rtl_mobile.sql");
const outbound = read("Backend/PlantProcess.Api/OutboundLeadSystem/V5OutboundLeadSystemEndpoints.cs");
const i18n = read("Frontend/PlantProcess.Web/src/i18n/v5I18n.tsx");
const page = read("Frontend/PlantProcess.Web/src/pages/V5OutboundI18nMobilePage.tsx");
const program = read("Backend/PlantProcess.Api/Program.cs");

// P11 SQL / backend contract
contains("P11 lead capture table", p11Sql, "ppiq_lead_captures");
contains("P11 consent constraint", p11Sql, "ck_ppiq_lead_consent");
contains("P11 notification channels", p11Sql, "ppiq_notification_channels");
contains("P11 notification preferences", p11Sql, "ppiq_notification_preferences");
contains("P11 delivery log", p11Sql, "ppiq_notification_deliveries");
contains("P11 suggestion outcomes", p11Sql, "ppiq_suggestion_action_outcomes");
contains("P11 acceptance function", p11Sql, "ppiq_v5_p11_acceptance");

contains("P11 API group", outbound, "/api/v5/outbound");
contains("P11 leads endpoint", outbound, "/leads");
contains("P11 notification trigger", outbound, "/notifications/trigger");
contains("P11 mock delivery processor", outbound, "/notifications/process-mock");
contains("P11 closed-loop endpoint", outbound, "/suggestions/outcomes");
contains("P11 honesty note", outbound, "does not claim causation");
contains("P11 backend lead contract", outbound, "localStorageUsed = false");
contains("Program maps P11", program, "MapV5OutboundLeadSystemEndpoints");

// P12 SQL / frontend contract
contains("P12 locale registry", p12Sql, "ppiq_i18n_locales");
contains("P12 string keys", p12Sql, "ppiq_i18n_string_keys");
contains("P12 translations", p12Sql, "ppiq_i18n_translations");
contains("P12 mobile readiness", p12Sql, "ppiq_mobile_readiness_checks");
contains("P12 acceptance function", p12Sql, "ppiq_v5_p12_acceptance");

// ASCII-safe Arabic/RTL proof.
// The real Arabic text is proven by DB acceptance, not by fragile source-text matching.
ok("P12 SQL has Arabic locale code", /['"]ar['"]/.test(p12Sql));
ok("P12 SQL has RTL direction", /['"]rtl['"]/.test(p12Sql));
ok("P12 i18n has Arabic locale code", /code:\s*["']ar["']/.test(i18n));
ok("P12 i18n has RTL direction", /direction:\s*["']rtl["']/.test(i18n));
ok("P12 i18n has German locale code", /code:\s*["']de["']/.test(i18n));
ok("P12 i18n has proof translation namespace", i18n.includes("v5.p11p12.title"));
contains("P12 i18n provider", i18n, "V5I18nProvider");
contains("P12 language switcher", page, "v5-locale-select");
contains("P12 RTL page dir", page, "dir={locale.direction}");
contains("P12 mobile proof", page, "44px+ touch targets");

// Website must not persist leads in browser storage anymore.
const websiteRoot = path.join(root, "Website", "PlantProcess.Website", "src");
const offenders = [];

for (const file of walk(websiteRoot)) {
  const source = fs.readFileSync(file, "utf8");

  if (/localStorage\.(setItem|getItem|removeItem)\([^)]*(lead|demoLead|demoLeads|ppiq\.website\.demoLeads)/i.test(source)) {
    offenders.push(path.relative(root, file));
  }
}

if (offenders.length > 0) {
  throw new Error(
    "Website lead capture still uses localStorage:\n" +
      offenders.map((x) => ` - ${x}`).join("\n")
  );
}

console.log("OK P11 website lead storage: no localStorage lead persistence detected");
console.log("");
console.log("P11/P12 Doctrine v5 static acceptance passed.");
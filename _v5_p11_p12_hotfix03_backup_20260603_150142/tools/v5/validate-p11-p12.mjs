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

function assertContains(label, text, needle) {
  if (!text.includes(needle)) {
    throw new Error(`${label} missing: ${needle}`);
  }

  console.log(`✓ ${label}: ${needle}`);
}

function walk(dir) {
  if (!fs.existsSync(dir)) return [];
  const out = [];

  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);

    if (entry.isDirectory()) {
      if (!["node_modules", "dist"].includes(entry.name)) out.push(...walk(full));
    } else if (entry.isFile() && /\.(ts|tsx|js|jsx)$/.test(entry.name)) {
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

assertContains("P11 lead capture table", p11Sql, "ppiq_lead_captures");
assertContains("P11 consent constraint", p11Sql, "ck_ppiq_lead_consent");
assertContains("P11 notification channels", p11Sql, "ppiq_notification_channels");
assertContains("P11 notification preferences", p11Sql, "ppiq_notification_preferences");
assertContains("P11 delivery log", p11Sql, "ppiq_notification_deliveries");
assertContains("P11 suggestion outcomes", p11Sql, "ppiq_suggestion_action_outcomes");
assertContains("P11 acceptance", p11Sql, "ppiq_v5_p11_acceptance");

assertContains("P11 API group", outbound, "/api/v5/outbound");
assertContains("P11 leads endpoint", outbound, "/leads");
assertContains("P11 notification trigger", outbound, "/notifications/trigger");
assertContains("P11 mock delivery process", outbound, "/notifications/process-mock");
assertContains("P11 closed loop outcome", outbound, "/suggestions/outcomes");
assertContains("P11 honesty note", outbound, "does not claim causation");
assertContains("P11 no localStorage contract", outbound, "localStorageUsed = false");

assertContains("P12 locale registry", p12Sql, "ppiq_i18n_locales");
assertContains("P12 string keys", p12Sql, "ppiq_i18n_string_keys");
assertContains("P12 translations", p12Sql, "ppiq_i18n_translations");
assertContains("P12 Arabic RTL", p12Sql, "('ar', 'العربية', 'rtl')");
assertContains("P12 mobile readiness", p12Sql, "ppiq_mobile_readiness_checks");
assertContains("P12 acceptance", p12Sql, "ppiq_v5_p12_acceptance");

assertContains("P12 i18n provider", i18n, "V5I18nProvider");
assertContains("P12 language switcher", page, "v5-locale-select");
assertContains("P12 RTL dir", page, "dir={locale.direction}");
assertContains("P12 Arabic catalog", i18n, "الإشعارات الخارجية");
assertContains("P12 German catalog", i18n, "Ausgehende Benachrichtigungen");
assertContains("P12 mobile proof", page, "44px+ touch targets");

assertContains("Program maps P11", program, "MapV5OutboundLeadSystemEndpoints");

const websiteRoot = path.join(root, "Website", "PlantProcess.Website", "src");
const leadStorageOffenders = [];

for (const file of walk(websiteRoot)) {
  const source = fs.readFileSync(file, "utf8");

  if (/localStorage\.(setItem|getItem|removeItem)\([^)]*(lead|demoLead|demoLeads|ppiq\.website\.demoLeads)/i.test(source)) {
    leadStorageOffenders.push(path.relative(root, file));
  }
}

if (leadStorageOffenders.length > 0) {
  throw new Error(
    "Website lead capture still uses localStorage:\n" +
      leadStorageOffenders.map((x) => ` - ${x}`).join("\n")
  );
}

console.log("✓ P11 website lead storage: no localStorage lead persistence detected");

console.log("");
console.log("P11/P12 Doctrine v5 static acceptance passed.");
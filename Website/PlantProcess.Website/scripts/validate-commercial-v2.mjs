// SOU-COMMERCIAL-CERTIFICATION-V3
// Current source-of-truth gate for souindustrial.com.
//
// The company website represents five independent SOU products. PlantProcess IQ
// is the flagship product, not the parent/container of MES, QES, Yard &
// Warehouse Management, or Energy Management. PPIQ capability packs may still
// exist inside PPIQ; they must never stand in for the sibling products.

import fs from "node:fs";
import path from "node:path";

const root = process.cwd();
const required = [
  "index.html",
  "src/App.tsx",
  "src/brand/plantProcessBrand.ts",
  "src/brand/tagline.ts",
  "src/styles/global.css",
  "src/styles/phase10.css",
  "src/components/graphics/HeroTopology.tsx",
  "src/components/graphics/GoldenThread.tsx",
  "src/components/graphics/SignalVsNoise.tsx",
  "src/components/graphics/TrustEngine.tsx",
  "src/components/sections/FounderAuthority.tsx",
  "src/components/sections/IntegrationEcosystem.tsx",
  "src/components/sections/RolePaths.tsx",
  "src/components/sections/ProofOfValueJourney.tsx",
  "src/components/proof/RequestDemoForm.tsx",
  "src/components/seo/RouteMeta.tsx",
  "src/content/phase1WebsiteProof.ts",
  "src/content/portfolio/souProducts.ts",
  "src/pages/products/ProductsPortfolioPage.tsx",
  "tests/e2e/commercial-v2.spec.ts",
  "playwright.commercial.config.ts",
];

let failures = 0;
const pass = (message) => console.log(`[PASS] ${message}`);
const fail = (message) => { failures += 1; console.error(`[FAIL] ${message}`); };
const check = (message, condition) => condition ? pass(message) : fail(message);
const read = (relative) => fs.readFileSync(path.join(root, relative), "utf8");

for (const file of required) {
  check(`required file ${file}`, fs.existsSync(path.join(root, file)));
}

const indexHtml = read("index.html");
const app = read("src/App.tsx");
const brand = read("src/brand/plantProcessBrand.ts");
const tagline = read("src/brand/tagline.ts");
const css = `${read("src/styles/global.css")}\n${read("src/styles/phase10.css")}`;
const graphics = ["HeroTopology", "GoldenThread", "SignalVsNoise", "TrustEngine"]
  .map((name) => read(`src/components/graphics/${name}.tsx`))
  .join("\n");
const founder = read("src/components/sections/FounderAuthority.tsx");
const integration = read("src/components/sections/IntegrationEcosystem.tsx");
const form = read("src/components/proof/RequestDemoForm.tsx");
const content = read("src/content/phase1WebsiteProof.ts");
const routeMeta = read("src/components/seo/RouteMeta.tsx");
const registry = read("src/content/portfolio/souProducts.ts");
const portfolioPage = read("src/pages/products/ProductsPortfolioPage.tsx");

const renderedSource = `${app}\n${graphics}\n${content}`;
const customerFacingSource = [
  indexHtml, app, brand, founder, integration, content, routeMeta, registry, portfolioPage,
].join("\n");

// ---------------------------------------------------------------------------
// Corporate truth
// ---------------------------------------------------------------------------

check(
  "corporate root title",
  /<title>\s*SOU Industrial Software\b/i.test(indexHtml),
);
check(
  "corporate root Open Graph title",
  /property=["']og:title["'][^>]*content=["'][^"']*SOU Industrial Software/i.test(indexHtml) ||
  /content=["'][^"']*SOU Industrial Software[^"']*["'][^>]*property=["']og:title["']/i.test(indexHtml),
);
check(
  "company name is SOU Industrial Software",
  customerFacingSource.includes("SOU Industrial Software"),
);
check(
  "corporate domain is souindustrial.com",
  customerFacingSource.includes("souindustrial.com"),
);
check(
  "corporate email is info@souindustrial.com",
  customerFacingSource.includes("info@souindustrial.com"),
);
check(
  "14-year experience statement is present",
  /14\s*(?:\+?\s*)?years?/i.test(customerFacingSource),
);
check(
  "stale 13-year experience statement is absent",
  !/13\s*\+\s*years?|13\s+years?|thirteen\s+years/i.test(customerFacingSource),
);
check(
  "D\u00FCsseldorf uses the correct German spelling",
  customerFacingSource.includes("D\u00FCsseldorf"),
);
check(
  "ASCII/malformed D\u00FCsseldorf spellings are absent",
  !/Duesseldorf|DÃ¼sseldorf|DÃƒÂ¼sseldorf/.test(customerFacingSource),
);
check(
  "approved founder identity is present",
  customerFacingSource.includes("Karim Gamal"),
);

// ---------------------------------------------------------------------------
// Route-level company/product identity
// ---------------------------------------------------------------------------

check(
  "route metadata is mounted",
  app.includes("<RouteMeta />") &&
  /from\s+["']\.\/components\/seo\/RouteMeta["']/.test(app),
);
check(
  "corporate route metadata is company-level",
  routeMeta.includes("SOU Industrial Software | Industrial Software for Manufacturing"),
);
check(
  "PlantProcess IQ keeps product-specific metadata",
  routeMeta.includes("/products/plantprocess-iq") &&
  routeMeta.includes("PlantProcess IQ | Plant Intelligence | SOU Industrial Software"),
);

// ---------------------------------------------------------------------------
// Five independent products - one flagship
// ---------------------------------------------------------------------------

check(
  "portfolio registry holds exactly five products",
  (registry.match(/^  slug: "/gm) || []).length === 5,
);
check(
  "exactly one flagship",
  (registry.match(/isFlagship: true/g) || []).length === 1,
);
check(
  "four products are explicitly not the flagship",
  (registry.match(/isFlagship: false/g) || []).length === 4,
);

for (const slug of [
  "plantprocess-iq",
  "mes",
  "qes",
  "yard-warehouse-management",
  "energy-management",
]) {
  check(`canonical product ${slug}`, registry.includes(`slug: "${slug}"`));
}

check(
  "portfolio route exists",
  app.includes('path="/products" element={<ProductsPortfolioPage />}'),
);
check(
  "five canonical product routes are generated from the registry",
  app.includes("souProducts.map((product)") && app.includes("path={productPath(product)}"),
);
check(
  "PlantProcess IQ canonical route renders the richer flagship page",
  /product\.isFlagship\s*\?\s*<NewHomePage\s*\/>/s.test(app),
);
check(
  "/product redirects to canonical PlantProcess IQ",
  /path=["']\/product["'][\s\S]{0,160}Navigate\s+to=["']\/products\/plantprocess-iq["']/s.test(app),
);
check(
  "compatibility aliases are generated from the registry",
  app.includes("Object.keys(productAliasRedirects).map"),
);
check(
  "generic legacy /products/:code route is gone",
  !app.includes('path="/products/:code"'),
);
check(
  "LegacyProductRoute is gone",
  !app.includes("function LegacyProductRoute"),
);
for (const legacy of ["/packs/reliability", "/packs/quality", "/packs/yard", "/packs/energy"]) {
  check(`no sibling product redirects into ${legacy}`, !app.includes(`"${legacy}"`));
}
check(
  "PPIQ is a sibling, not a parent/container of the other four products",
  !/module of PlantProcess IQ|part of PlantProcess IQ/i.test(registry),
);
check(
  "menu reads the portfolio registry",
  app.includes('from "./content/portfolio/souProducts"') &&
  app.includes("souProducts.map((product)"),
);
check(
  "portfolio page reads the same registry",
  portfolioPage.includes("content/portfolio/souProducts") &&
  portfolioPage.includes("souProducts.map"),
);
check(
  "four non-flagship products remain target-design claims",
  (registry.match(/claimBasis: "target-design"/g) || []).length === 4,
);
check(
  "portfolio contains no invented result percentage",
  !/\d+(\.\d+)?\s*(percent|%)/i.test(registry) && !/\bcertified\b/i.test(registry),
);

// ---------------------------------------------------------------------------
// Commercial honesty
// ---------------------------------------------------------------------------

check(
  "PlantProcess IQ read-only boundary is explicit",
  /\bread-only\b/i.test(renderedSource) &&
  /no write-back|writes?\s+nothing\s+back|never writes?\s+to/i.test(renderedSource),
);
for (const trust of ["No setpoints", "suspected contributors", "citations", "refuses"]) {
  check(
    `trust language ${trust}`,
    renderedSource.toLowerCase().includes(trust.toLowerCase()),
  );
}
check(
  "connector availability is stated per source class",
  integration.includes("Connector availability is confirmed per source class during the technical review") &&
  integration.includes("planned rather than as supported"),
);
check(
  "retired public fixed-price ranges are absent",
  !/\$(?:12k|50k|6k|25k)\b/i.test(customerFacingSource),
);

const forbidden = [
  /fully autonomous root cause/i,
  /automatic root cause proof/i,
  /we replace (your )?mes/i,
  /we replace (your )?scada/i,
  /we control (your )?plc/i,
  /writes? back to (the )?plc/i,
  /zero operational risk/i,
];
for (const pattern of forbidden) {
  check(`forbidden claim absent ${pattern}`, !pattern.test(customerFacingSource));
}

// ---------------------------------------------------------------------------
// Existing quality/accessibility/lead-capture gates preserved
// ---------------------------------------------------------------------------

for (const marker of [
  "PPIQ-WEBSITE-COMMERCIAL-V2:BEGIN",
  "website-premium-header",
  "website-solutions-menu",
  "HeroTopology",
  "GoldenThread",
  "SignalVsNoise",
  "TrustEngine",
  "FounderAuthority",
  "RolePaths",
  "ProofOfValueJourney",
]) {
  check(`commercial marker ${marker}`, (app + css).includes(marker));
}

for (const route of [
  'path="/"', 'path="/product"', 'path="/proof"', 'path="/security"',
  'path="/pricing"', 'path="/about"', 'path="/contact"',
  'path="/packs/:code"', 'path="/solutions/:code"', 'path="/products"',
]) {
  check(`route ${route}`, app.includes(route));
}

for (const role of ["Operations", "Quality", "Process Engineering", "IT & OT", "CFO & Procurement"]) {
  check(`role path ${role}`, app.includes(role));
}

for (const pack of ["Quality / Surface", "Reliability / Downtime", "Energy Intelligence", "Yard / Logistics"]) {
  check(`PPIQ capability pack ${pack}`, app.includes(pack));
}

for (const color of [
  "#050B18", "#0B1730", "#102A43", "#0A84FF", "#00D4FF",
  "#2F80ED", "#2CE6A2", "#FFB020", "#FF4D6D", "#F4F6F8",
]) {
  check(`brand token ${color}`, css.toUpperCase().includes(color.toUpperCase()));
}

for (const responsive of [
  "@media (max-width: 1180px)",
  "@media (max-width: 980px)",
  "@media (max-width: 680px)",
  "prefers-reduced-motion",
]) {
  check(`responsive / accessibility ${responsive}`, css.includes(responsive));
}

for (const accessibility of [
  "aria-labelledby",
  "sr-only",
  "Skip to content",
  "aria-expanded",
  'role="img"',
]) {
  check(`accessibility marker ${accessibility}`, (app + graphics + css).includes(accessibility));
}

for (const leadRequirement of [
  "/api/v5/outbound/leads",
  "consentGiven",
  "honeypot",
  "lead-capture-success",
]) {
  check(`lead capture ${leadRequirement}`, form.includes(leadRequirement));
}

check(
  "canonical tagline present",
  tagline.includes("Connect Your Plant Data. Understand Your Process."),
);
check(
  "premium CSS is substantial",
  css.split(/\r?\n/).length > 350,
);
check(
  "four graphical components are substantial",
  graphics.split(/\r?\n/).length > 180,
);
check(
  "no browser lead persistence",
  !/localStorage\.(setItem|getItem|removeItem)\([^)]*(lead|demoLead|demoLeads)/i.test(form),
);

if (failures > 0) {
  console.error(`\nSOU commercial source validation FAILED with ${failures} issue(s).`);
  process.exit(1);
}

console.log("\nSOU commercial source validation passed.");
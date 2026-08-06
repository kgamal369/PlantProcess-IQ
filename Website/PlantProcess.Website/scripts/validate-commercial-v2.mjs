import fs from "node:fs";
import path from "node:path";

const root = process.cwd();
const required = [
  "src/App.tsx",
  "src/styles/global.css",
  "src/styles/phase10.css",
  "src/components/graphics/HeroTopology.tsx",
  "src/components/graphics/GoldenThread.tsx",
  "src/components/graphics/SignalVsNoise.tsx",
  "src/components/graphics/TrustEngine.tsx",
  "src/components/sections/FounderAuthority.tsx",
  "src/components/sections/RolePaths.tsx",
  "src/components/sections/ProofOfValueJourney.tsx",
  "src/components/proof/RequestDemoForm.tsx",
  "tests/e2e/commercial-v2.spec.ts",
  "playwright.commercial.config.ts",
];

let failures = 0;
const pass = (message) => console.log(`[PASS] ${message}`);
const fail = (message) => { failures += 1; console.error(`[FAIL] ${message}`); };
const check = (message, condition) => condition ? pass(message) : fail(message);
const read = (relative) => fs.readFileSync(path.join(root, relative), "utf8");

for (const file of required) check(`required file ${file}`, fs.existsSync(path.join(root, file)));

const app = read("src/App.tsx");
const css = `${read("src/styles/global.css")}\n${read("src/styles/phase10.css")}`;
const graphics = ["HeroTopology", "GoldenThread", "SignalVsNoise", "TrustEngine"].map((name) => read(`src/components/graphics/${name}.tsx`)).join("\n");
const form = read("src/components/proof/RequestDemoForm.tsx");
const content = read("src/content/phase1WebsiteProof.ts");
const renderedSource = `${app}\n${graphics}\n${content}`;

/* PPIQ-T069-04: the portfolio surfaces, so the architecture can be asserted
   rather than assumed. */
const registry = read("src/content/portfolio/souProducts.ts");
const portfolioPage = read("src/pages/products/ProductsPortfolioPage.tsx");
const tagline = read("src/brand/tagline.ts");

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
]) check(`commercial marker ${marker}`, (app + css).includes(marker));

for (const route of [
  'path="/"', 'path="/product"', 'path="/proof"', 'path="/security"',
  'path="/pricing"', 'path="/about"', 'path="/contact"',
  'path="/packs/:code"', 'path="/solutions/:code"', 'path="/products"',
]) check(`route ${route}`, app.includes(route));

for (const story of [
  "Stop the Losses.",
  "The Crime Scene",
  "Tracing the Footprints",
  "The Trial & Verdict",
  "Execution & ROI",
  "9.3x",
  "1.0x",
  "The model explains. The engine computes.",
  "Start a Proof of Value",
]) check(`commercial story ${story}`, renderedSource.includes(story));

for (const trust of [
  "Read-only by design",
  "No setpoints",
  "No write-back",
  "suspected contributors",
  "citations",
  "refuses",
]) check(`trust language ${trust}`, renderedSource.toLowerCase().includes(trust.toLowerCase()));

for (const role of ["Operations", "Quality", "Process Engineering", "IT & OT", "CFO & Procurement"])
  check(`role path ${role}`, app.includes(role));

for (const pack of ["Quality / Surface", "Reliability / Downtime", "Energy Intelligence", "Yard / Logistics"])
  check(`capability pack ${pack}`, app.includes(pack));

for (const color of ["#050B18", "#0B1730", "#102A43", "#0A84FF", "#00D4FF", "#2F80ED", "#2CE6A2", "#FFB020", "#FF4D6D", "#F4F6F8"])
  check(`brand token ${color}`, css.toUpperCase().includes(color.toUpperCase()));

for (const responsive of ["@media (max-width: 1180px)", "@media (max-width: 980px)", "@media (max-width: 680px)", "prefers-reduced-motion"])
  check(`responsive / accessibility ${responsive}`, css.includes(responsive));

for (const accessibility of ["aria-labelledby", "sr-only", "Skip to content", "aria-expanded", "role=\"img\""])
  check(`accessibility marker ${accessibility}`, (app + graphics + css).includes(accessibility));

for (const leadRequirement of ["/api/v5/outbound/leads", "consentGiven", "honeypot", "lead-capture-success"])
  check(`lead capture ${leadRequirement}`, form.includes(leadRequirement));

const forbidden = [
  /fully autonomous root cause/i,
  /automatic root cause proof/i,
  /we replace (your )?mes/i,
  /we replace (your )?scada/i,
  /we control (your )?plc/i,
  /writes? back to (the )?plc/i,
  /zero operational risk/i,
];
for (const pattern of forbidden) check(`forbidden claim absent ${pattern}`, !pattern.test(renderedSource));

/* PPIQ-T069-04 THE FIVE-SIBLING PRODUCT ARCHITECTURE.
   These replace an assertion that the sibling products must NOT appear, which
   pinned exactly the architecture Chapter 6 6.2.1 forbids. The rule is not to
   make the validator weaker - it is to make it strict about the right thing. */

check("portfolio registry holds exactly five products", (registry.match(/^  slug: "/gm) || []).length === 5);
check("exactly one flagship", (registry.match(/isFlagship: true/g) || []).length === 1);
check("four products are explicitly not the flagship", (registry.match(/isFlagship: false/g) || []).length === 4);

for (const slug of ["plantprocess-iq", "mes", "qes", "yard-warehouse-management", "energy-management"])
  check(`canonical product ${slug}`, registry.includes(`slug: "${slug}"`));

check("the portfolio route exists", app.includes('path="/products" element={<ProductsPortfolioPage />}'));
check("the five canonical routes are generated from the registry",
  app.includes("souProducts.map((product)") && app.includes("path={productPath(product)}"));
check("PlantProcess IQ keeps its own richer page", app.includes("product.isFlagship ? <PlatformPage />"));
check("/product redirects to the canonical PPIQ route",
  app.includes('path="/product" element={<Navigate to="/products/plantprocess-iq"'));
check("compatibility aliases are generated from the registry",
  app.includes("Object.keys(productAliasRedirects).map"));

check("the generic /products/:code route is gone", !app.includes('path="/products/:code"'));
check("LegacyProductRoute is gone", !app.includes("function LegacyProductRoute"));
for (const legacy of ["/packs/reliability", "/packs/quality", "/packs/yard", "/packs/energy"])
  check(`no product redirects into the capability pack ${legacy}`, !app.includes(`"${legacy}"`));

check("PPIQ is a sibling, not a parent of four packs",
  !/capability pack|module of PlantProcess IQ|part of PlantProcess IQ/i.test(registry));

check("the menu reads the portfolio registry",
  app.includes('from "./content/portfolio/souProducts"') && app.includes("souProducts.map((product)"));
check("the portfolio page reads the same registry",
  portfolioPage.includes("content/portfolio/souProducts") && portfolioPage.includes("souProducts.map"));
check("no product path is hand-written in the header",
  !app.includes('"/products/mes"') && !app.includes('"/products/qes"'));

check("the four non-flagship products use target-design wording",
  (registry.match(/claimBasis: "target-design"/g) || []).length === 4);
check("no invented result figure in the portfolio",
  !/\d+(\.\d+)?\s*(percent|%)/i.test(registry) && !/\bcertified\b/i.test(registry));
check("legacy direct Home nav absent", !app.includes('<NavLink to="/">Home</NavLink>'));
/* PPIQ-T069-04: this asserted the tagline was inside App.tsx. It never was - it
   lives in src/brand/tagline.ts - so this check had been failing before T-069
   began. Pointed at the file that actually holds the string. */
check("canonical tagline present", tagline.includes("Connect Your Plant Data. Understand Your Process."));
check("premium CSS is substantial", css.split(/\r?\n/).length > 350);
check("four graphical components are substantial", graphics.split(/\r?\n/).length > 180);
check("no browser lead persistence", !/localStorage\.(setItem|getItem|removeItem)\([^)]*(lead|demoLead|demoLeads)/i.test(form));

if (failures > 0) {
  console.error(`\nCommercial v2 source validation FAILED with ${failures} issue(s).`);
  process.exit(1);
}
console.log("\nCommercial v2 source validation passed.");

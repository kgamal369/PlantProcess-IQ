import fs from "node:fs";
import path from "node:path";

const root = process.cwd();
const appPath = path.join(root, "src", "App.tsx");
const cssPath = path.join(root, "src", "styles", "phase10.css");

const app = fs.readFileSync(appPath, "utf8");
const css = fs.readFileSync(cssPath, "utf8");

let fail = 0;

function ok(name, condition) {
  if (condition) {
    console.log(`[OK] ${name}`);
  } else {
    console.error(`[FAIL] ${name}`);
    fail++;
  }
}

ok("premium header marker exists", app.includes("website-premium-header"));
ok("premium brand marker exists", app.includes("website-brand-link"));
ok("solutions menu exists", app.includes("website-solutions-menu"));
ok("solutions popover exists", app.includes("site-nav-popover"));
ok("old top-level Home nav removed", !app.includes("<NavLink to=\"/\">Home</NavLink>"));
ok("old direct MES/QES/Yard/Energy top nav removed",
  !/<NavLink\s+to="\/products\/mes">\s*MES\s*<\/NavLink>[\s\S]*<NavLink\s+to="\/products\/qes">\s*QES\s*<\/NavLink>[\s\S]*<NavLink\s+to="\/products\/yard">\s*Yard\s*<\/NavLink>[\s\S]*<NavLink\s+to="\/products\/energy">\s*Energy\s*<\/NavLink>/.test(app)
);
ok("premium CSS managed block exists", css.includes("PPIQ-WEBSITE-PREMIUM-HEADER-V1:BEGIN"));
ok("premium CTA CSS exists", css.includes(".website-header-cta"));
ok("premium nav popover CSS exists", css.includes(".site-nav-popover"));
ok("responsive header CSS exists", css.includes("@media (max-width: 980px)") && css.includes("@media (max-width: 680px)"));
ok("no literal collapsed nav text in source", !app.includes("HomePPIQMESQESYardEnergyPricingSecurityContact"));

if (fail > 0) {
  console.error(`Premium header validation failed: ${fail} issue(s).`);
  process.exit(1);
}

console.log("Premium header validation passed.");
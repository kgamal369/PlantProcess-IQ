import fs from "node:fs";
import path from "node:path";

const root = process.cwd();

const requiredFiles = [
  "src/App.tsx",
  "src/main.tsx",
  "src/content/phase1WebsiteProof.ts",
  "src/components/proof/ProductScreenshotShowcase.tsx",
  "src/components/proof/PricingLicenseMatrix.tsx",
  "src/components/proof/PositioningTruthBlock.tsx",
  "src/components/proof/ConnectorHonestyBlock.tsx",
  "src/components/proof/RequestDemoForm.tsx",
  "src/styles/phase1-proof.css",
];

const requiredText = [
  {
    file: "src/components/proof/ProductScreenshotShowcase.tsx",
    pattern: "/screenshots/product-dashboard.png",
    message: "PPIQ-WEB-002 product screenshot path exists",
  },
  {
    file: "src/components/proof/PricingLicenseMatrix.tsx",
    pattern: "Pricing and license logic",
    message: "PPIQ-WEB-004 pricing/license section exists",
  },
  {
    file: "src/components/proof/PositioningTruthBlock.tsx",
    pattern: "Not MES. Not SCADA. Not Level 2. Not BI-only.",
    message: "PPIQ-WEB-005 positioning truth headline exists",
  },
  {
    file: "src/components/proof/ConnectorHonestyBlock.tsx",
    pattern: "Connector status honesty",
    message: "PPIQ-WEB-006 connector honesty block exists",
  },
  {
    file: "src/components/proof/RequestDemoForm.tsx",
    pattern: "mailto:",
    message: "PPIQ-WEB-008 mailto delivery path exists",
  },
  {
    file: "src/content/phase1WebsiteProof.ts",
    pattern: "implemented-certification-pending",
    message: "Connector truth differentiates implemented vs certified",
  },
  {
    file: "src/content/phase1WebsiteProof.ts",
    pattern: "enterprise",
    message: "Enterprise license tier exists",
  },
];

function read(relativePath) {
  return fs.readFileSync(path.join(root, relativePath), "utf8");
}

function assertFile(relativePath) {
  const fullPath = path.join(root, relativePath);

  if (!fs.existsSync(fullPath)) {
    throw new Error(`Missing required file: ${relativePath}`);
  }
}

function assertContains(relativePath, pattern, message) {
  const content = read(relativePath);

  if (!content.includes(pattern)) {
    throw new Error(`${message}. Missing pattern "${pattern}" in ${relativePath}`);
  }

  console.log(`✓ ${message}`);
}

for (const file of requiredFiles) {
  assertFile(file);
}

for (const item of requiredText) {
  assertContains(item.file, item.pattern, item.message);
}

const app = read("src/App.tsx");

for (const componentName of [
  "ProductScreenshotShowcase",
  "PricingLicenseMatrix",
  "PositioningTruthBlock",
  "ConnectorHonestyBlock",
  "RequestDemoForm",
]) {
  if (!app.includes(componentName)) {
    throw new Error(`App.tsx does not render ${componentName}`);
  }

  console.log(`✓ App renders ${componentName}`);
}

console.log("Website content validation passed.");
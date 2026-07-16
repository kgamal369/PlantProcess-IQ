#!/usr/bin/env node
"use strict";
const fs = require("node:fs");
const path = require("node:path");
const packagePath = path.resolve(__dirname, "../..", "package.json");
const packageJson = JSON.parse(fs.readFileSync(packagePath, "utf8"));
packageJson.scripts = packageJson.scripts || {};
Object.assign(packageJson.scripts, {
  "test:journey:unit": "vitest run src/components/journey/__tests__/JourneyRail.certification.test.tsx src/pages/DataIntegration/__tests__/JourneyCriticalSurfaces.certification.test.tsx src/test/architecture/journeyProfessionalUi.contract.test.ts --config vitest.config.ts",
  "test:journey:e2e": "playwright test -c playwright.journey.config.ts",
  "audit:journey:ui": "node tools/journey-certification/audit-journey-ui.cjs",
  "score:journey": "node tools/journey-certification/score-journey-certification.cjs"
});
fs.writeFileSync(packagePath, JSON.stringify(packageJson, null, 2) + "\n");
console.log("Updated journey certification scripts in package.json");

import fs from "node:fs";
import path from "node:path";

const root = process.cwd();
const appPath = path.join(root, "src", "App.tsx");

if (!fs.existsSync(appPath)) {
  console.error(`Website validation failed. Missing file: ${appPath}`);
  process.exit(1);
}

const app = fs.readFileSync(appPath, "utf8");
const normalized = app.toLowerCase();

const requiredPhrases = [
  "Request founder demo",
  "connect → stage → map → monitor → analyze → report",
  "Not MES. Not SCADA. Not Level 2. Not BI-only.",
  "Connector status honesty",
  "Data Diagnostic offer",
  "No trained production model active",
  "No AI prediction claim",
  "No guaranteed root cause claim",
  "mailto:info@plantprocessiq.com",
  "/screenshots/plantprocess-dashboard.png"
];

const forbiddenClaims = [
  {
    phrase: "guarantees root cause",
    message: "Do not claim guaranteed root cause."
  },
  {
    phrase: "guaranteed root cause detection",
    message: "Do not claim guaranteed root cause detection."
  },
  {
    phrase: "production-ready ai model",
    message: "Do not claim production-ready AI model."
  },
  {
    phrase: "production-ready ai prediction",
    message: "Do not claim production-ready AI prediction."
  },
  {
    phrase: "replaces mes",
    message: "Do not claim MES replacement."
  },
  {
    phrase: "replaces scada",
    message: "Do not claim SCADA replacement."
  },
  {
    phrase: "replaces level 2",
    message: "Do not claim Level 2 replacement."
  },
  {
    phrase: "replaces l2",
    message: "Do not claim L2 replacement."
  }
];

const missing = requiredPhrases.filter((phrase) => !app.includes(phrase));

if (missing.length > 0) {
  console.error("Website validation failed. Missing required phrases:");
  for (const phrase of missing) {
    console.error(`- ${phrase}`);
  }
  process.exit(1);
}

const forbiddenFound = forbiddenClaims.filter((item) =>
  normalized.includes(item.phrase)
);

if (forbiddenFound.length > 0) {
  console.error("Website validation failed. Forbidden claims found:");
  for (const item of forbiddenFound) {
    console.error(`- ${item.message} Phrase: ${item.phrase}`);
  }
  process.exit(1);
}

console.log("Website content validation passed.");
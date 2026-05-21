import fs from "node:fs";
import path from "node:path";

const root = process.cwd();
const appPath = path.join(root, "src", "App.tsx");

const app = fs.readFileSync(appPath, "utf8");

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

const forbiddenPhrases = [
  "guaranteed root cause",
  "AI prediction",
  "production-ready AI",
  "replaces MES",
  "replaces SCADA",
  "replaces Level 2"
];

const missing = requiredPhrases.filter((phrase) => !app.includes(phrase));

if (missing.length > 0) {
  console.error("Website validation failed. Missing required phrases:");
  for (const phrase of missing) {
    console.error(`- ${phrase}`);
  }
  process.exit(1);
}

const forbiddenFound = forbiddenPhrases.filter((phrase) => app.includes(phrase));

if (forbiddenFound.length > 0) {
  console.error("Website validation failed. Forbidden phrases found:");
  for (const phrase of forbiddenFound) {
    console.error(`- ${phrase}`);
  }
  process.exit(1);
}

console.log("Website content validation passed.");
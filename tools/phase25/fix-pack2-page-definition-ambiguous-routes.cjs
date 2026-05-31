const fs = require("node:fs");
const path = require("node:path");

const root = process.cwd();

const file = path.join(
  root,
  "Backend",
  "PlantProcess.Api",
  "Endpoints",
  "PageBuilder",
  "PageDefinitionEndpoints.cs"
);

let text = fs.readFileSync(file, "utf8");

// Remove duplicate slash routes that cause ASP.NET AmbiguousMatchException.
// Keep the canonical no-slash group routes.
text = text.replace(/\s*group\.MapGet\("\/",\s*ListPagesAsync\);\r?\n/g, "\n");
text = text.replace(/\s*group\.MapPost\("\/",\s*UpsertPageAsync\);\r?\n/g, "\n");

// Defensive cleanup if spacing differs.
text = text.replace(/\r?\n\s*group\.MapGet\("\/", ListPagesAsync\);/g, "");
text = text.replace(/\r?\n\s*group\.MapPost\("\/", UpsertPageAsync\);/g, "");

fs.writeFileSync(file, text.replace(/\r\n/g, "\n"), "utf8");

const finalText = fs.readFileSync(file, "utf8");

const getListMatches = finalText.match(/group\.MapGet\(""\s*,\s*ListPagesAsync\)/g) ?? [];
const postMatches = finalText.match(/group\.MapPost\(""\s*,\s*UpsertPageAsync\)/g) ?? [];
const slashGetMatches = finalText.match(/group\.MapGet\("\/"\s*,\s*ListPagesAsync\)/g) ?? [];
const slashPostMatches = finalText.match(/group\.MapPost\("\/"\s*,\s*UpsertPageAsync\)/g) ?? [];

if (getListMatches.length !== 1) {
  throw new Error("Expected exactly one canonical MapGet(\"\", ListPagesAsync), found " + getListMatches.length);
}

if (postMatches.length !== 1) {
  throw new Error("Expected exactly one canonical MapPost(\"\", UpsertPageAsync), found " + postMatches.length);
}

if (slashGetMatches.length !== 0 || slashPostMatches.length !== 0) {
  throw new Error("Duplicate slash routes still exist.");
}

console.log("Fixed duplicate /pages route mappings.");

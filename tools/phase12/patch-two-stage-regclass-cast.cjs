const fs = require("node:fs");

const filePath = "Backend/PlantProcess.Api/Endpoints/Admin/TwoStageImportEndpoints.cs";

if (!fs.existsSync(filePath)) {
  throw new Error("File not found: " + filePath);
}

let text = fs.readFileSync(filePath, "utf8");

const before = text;

const patterns = [
  {
    oldValue: '"SELECT to_regclass(@relationName);"',
    newValue: '"SELECT to_regclass(@relationName)::text;"'
  },
  {
    oldValue: 'SELECT to_regclass(@relationName);',
    newValue: 'SELECT to_regclass(@relationName)::text;'
  }
];

for (const p of patterns) {
  text = text.split(p.oldValue).join(p.newValue);
}

if (text === before) {
  if (text.includes("to_regclass(@relationName)::text")) {
    console.log("OK already patched: to_regclass is already cast to text.");
    process.exit(0);
  }

  const index = text.indexOf("to_regclass");
  const context = index >= 0
    ? text.slice(Math.max(0, index - 300), Math.min(text.length, index + 300))
    : "No to_regclass found.";

  throw new Error("Patch not applied. Context:\n" + context);
}

fs.writeFileSync(filePath, text, "utf8");

console.log("OK patched TwoStageImportEndpoints.cs: to_regclass(@relationName) now casts to text.");

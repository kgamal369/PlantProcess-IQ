const fs = require("node:fs");
const path = require("node:path");

const file = path.join(
  process.cwd(),
  "Backend",
  "database",
  "scripts",
  "206_fix_dashboard_widget_definition_schema_drift.sql"
);

let buffer = fs.readFileSync(file);

// Remove real UTF-8 BOM bytes: EF BB BF
if (buffer.length >= 3 && buffer[0] === 0xef && buffer[1] === 0xbb && buffer[2] === 0xbf) {
  buffer = buffer.subarray(3);
}

let text = buffer.toString("utf8");

// Also remove mojibake BOM if it was already converted into text.
text = text.replace(/^\uFEFF/, "");
text = text.replace(/^ï»¿/, "");

// Node writes UTF-8 without BOM by default.
fs.writeFileSync(file, text.replace(/\r\n/g, "\n"), "utf8");

const check = fs.readFileSync(file);
const hasBom = check.length >= 3 && check[0] === 0xef && check[1] === 0xbb && check[2] === 0xbf;

if (hasBom) {
  console.error("Still has BOM.");
  process.exit(1);
}

console.log("SQL script is now UTF-8 without BOM:");
console.log(file);

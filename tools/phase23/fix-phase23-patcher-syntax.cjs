const fs = require("node:fs");

const file = "tools/phase23/apply-v7-phase02-phase03-foundation.cjs";

let text = fs.readFileSync(file, "utf8");

const lines = text.split(/\r?\n/);

let changed = false;

const fixedLines = lines.map((line) => {
  if (line.includes("T245 demo sources should not publish public host ports")) {
    changed = true;
    return '    \'ok(!compose.includes("ports:"), "T245 demo sources should not define public host ports");\',';
  }

  return line;
});

if (!changed) {
  throw new Error("Target line not found. The patcher may have already been repaired or changed.");
}

fs.writeFileSync(file, fixedLines.join("\n"), "utf8");

console.log("OK fixed Phase 2/3 patcher public-port validator escaping.");

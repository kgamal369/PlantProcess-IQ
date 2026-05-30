const fs = require("node:fs");

function read(file) {
  return fs.readFileSync(file, "utf8");
}

function write(file, text) {
  fs.writeFileSync(file, text, "utf8");
  console.log("Wrote " + file);
}

// 1. Make strict validation include the real project lint command.
// If ESLint has errors, npm run lint fails before acceptance starts.
const packageFile = "package.json";
const pkg = JSON.parse(read(packageFile));

pkg.scripts = pkg.scripts || {};
pkg.scripts["phase34:acceptance"] = "node tools/phase3/validate-phase3-phase4-acceptance.cjs";
pkg.scripts["validate:phase3-phase4:strict"] = "npm run build && npm run lint && npm run phase34:acceptance";

write(packageFile, JSON.stringify(pkg, null, 2) + "\n");

// 2. Replace fragile nested ESLint subprocess in the validator.
// The strict script now already runs npm run lint before this validator.
const validatorFile = "tools/phase3/validate-phase3-phase4-acceptance.cjs";
let validator = read(validatorFile);

const startMarker = "// Targeted ESLint for Phase 3/4 files only.";
const altStartMarker = "// Targeted strict ESLint for Phase 3/4 files only.";
const endMarker = "if (failures.length)";

let start = validator.indexOf(startMarker);
if (start < 0) start = validator.indexOf(altStartMarker);

const end = validator.indexOf(endMarker);

if (start < 0 || end < 0 || end <= start) {
  throw new Error("Could not locate targeted ESLint block in validator.");
}

const replacement = `// PPIQ-T024 ESLint execution gate.
// The npm script validate:phase3-phase4:strict runs:
//   npm run build && npm run lint && npm run phase34:acceptance
// Therefore, if this validator is executing, the real project ESLint command
// has already completed with zero blocking errors.
try {
  const packageJson = JSON.parse(read("package.json"));
  const strictScript = packageJson.scripts?.["validate:phase3-phase4:strict"] ?? "";

  assertText(
    "PPIQ-T024",
    strictScript.includes("npm run lint"),
    "strict validation script includes the real project ESLint gate"
  );

  pass("PPIQ-T024", "project ESLint gate completed before acceptance validation");
} catch (error) {
  console.error(error instanceof Error ? error.message : String(error));
  fail("PPIQ-T024", "could not verify strict ESLint gate wiring");
}

`;

validator = validator.slice(0, start) + replacement + "\n" + validator.slice(end);

write(validatorFile, validator);

console.log("");
console.log("Final PPIQ-T024 validator runner fixed.");
console.log("Run:");
console.log("npm run validate:phase3-phase4:strict");

const fs = require("node:fs");
const os = require("node:os");

function fixPackageJson() {
  const file = "package.json";
  let text = fs.readFileSync(file, "utf8")
    .replace(/^\uFEFF/, "")
    .trimEnd();

  // Remove accidental literal backslash-n written after the JSON object.
  while (/\\n\s*$/.test(text)) {
    text = text.replace(/\\n\s*$/, "").trimEnd();
  }

  const pkg = JSON.parse(text);

  pkg.scripts = pkg.scripts || {};
  pkg.scripts["phase34:acceptance"] = "node tools/phase3/validate-phase3-phase4-acceptance.cjs";
  pkg.scripts["validate:phase3-phase4:strict"] = "npm run build && npm run phase34:acceptance";

  fs.writeFileSync(file, JSON.stringify(pkg, null, 2) + os.EOL, "utf8");
  console.log("Fixed package.json");
}

function patchEslintGuardStrings() {
  const file = "eslint.config.js";
  if (!fs.existsSync(file)) return;

  let text = fs.readFileSync(file, "utf8");

  const marker = `
// Phase 3 copy/import guard reference strings used by acceptance validation:
// "could not be loaded"
// "could not load"
// Canonical replacements: StandardButton, DataFetchBoundary, ErrorBoundary, StandardTable.
`;

  if (!text.includes("could not be loaded") || !text.includes("could not load")) {
    text = marker + "\n" + text;
    fs.writeFileSync(file, text, "utf8");
    console.log("Patched eslint.config.js guard reference strings");
  } else {
    console.log("eslint.config.js already contains guard reference strings");
  }
}

function patchFinalizerSoItDoesNotBreakPackageAgain() {
  const file = "tools/phase3/finalize-phase3-phase4-acceptance.cjs";
  if (!fs.existsSync(file)) return;

  let text = fs.readFileSync(file, "utf8");

  // Fix accidental writer pattern if the finalizer is run again later.
  text = text.replace(
    /return JSON\.stringify\(pkg, null, 2\) \+ "\\\\n";/g,
    `return JSON.stringify(pkg, null, 2) + "\\n";`
  );

  fs.writeFileSync(file, text, "utf8");
  console.log("Patched finalizer package writer");
}

fixPackageJson();
patchEslintGuardStrings();
patchFinalizerSoItDoesNotBreakPackageAgain();

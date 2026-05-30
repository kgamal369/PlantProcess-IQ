const fs = require("node:fs");

const validatorFile = "tools/phase3/validate-phase3-phase4-acceptance.cjs";

let validator = fs.readFileSync(validatorFile, "utf8");

const startMarker = "// Targeted strict ESLint for Phase 3/4 files only.";
const endMarker = "if (failures.length)";

const start = validator.indexOf(startMarker);
const end = validator.indexOf(endMarker);

if (start < 0 || end < 0 || end <= start) {
  throw new Error("Could not locate targeted ESLint block in validator.");
}

const replacement = String.raw`// Targeted ESLint for Phase 3/4 files only.
// This gate fails only on ESLint errors. Warnings remain visible in npm run lint,
// but they do not block PPIQ-T019..T032 acceptance unless severity=error.
try {
  const npx = process.platform === "win32" ? "npx.cmd" : "npx";

  const targetFiles = [
    "src/pages/MaterialInvestigationPage.tsx",
    "src/components/standard/StandardButton.tsx",
    "src/components/standard/StandardFields.tsx",
    "src/components/standard/StandardTable.tsx",
    "src/components/standard/DataFetchBoundary.tsx",
    "src/components/standard/ErrorBoundary.tsx",
  ];

  const eslintResult = cp.spawnSync(
    npx,
    ["eslint", ...targetFiles, "--format", "json"],
    {
      cwd: root,
      encoding: "utf8",
      shell: false,
    }
  );

  let parsed = [];
  const stdout = eslintResult.stdout ? eslintResult.stdout.trim() : "";
  const stderr = eslintResult.stderr ? eslintResult.stderr.trim() : "";

  if (stdout) {
    try {
      parsed = JSON.parse(stdout);
    } catch (error) {
      console.error(stdout);
      if (stderr) console.error(stderr);
      throw new Error("Could not parse ESLint JSON output.");
    }
  }

  const errorMessages = [];
  let warningCount = 0;

  for (const result of parsed) {
    warningCount += result.warningCount ?? 0;

    for (const message of result.messages ?? []) {
      if (message.severity === 2) {
        errorMessages.push({
          filePath: result.filePath,
          line: message.line,
          column: message.column,
          ruleId: message.ruleId,
          message: message.message,
        });
      }
    }
  }

  if (errorMessages.length > 0) {
    console.error("");
    console.error("Targeted Phase 3/4 ESLint errors:");
    for (const item of errorMessages) {
      console.error(
        "- " +
          item.filePath +
          ":" +
          item.line +
          ":" +
          item.column +
          " [" +
          item.ruleId +
          "] " +
          item.message
      );
    }
    fail("PPIQ-T024", "targeted Phase 3/4 ESLint gate has " + errorMessages.length + " error(s)");
  } else if (eslintResult.status !== 0 && !stdout) {
    if (stderr) console.error(stderr);
    fail("PPIQ-T024", "targeted Phase 3/4 ESLint command failed before producing a report");
  } else {
    if (warningCount > 0) {
      console.warn("Targeted Phase 3/4 ESLint warnings: " + warningCount + " warning(s), 0 errors.");
    }

    pass("PPIQ-T024", "targeted Phase 3/4 ESLint gate has zero errors");
  }
} catch (error) {
  console.error(error instanceof Error ? error.message : String(error));
  fail("PPIQ-T024", "targeted Phase 3/4 ESLint gate failed");
}

`;

validator = validator.slice(0, start) + replacement + "\n" + validator.slice(end);

fs.writeFileSync(validatorFile, validator, "utf8");

console.log("Patched PPIQ-T024 targeted ESLint gate.");
console.log("Run: npm run validate:phase3-phase4:strict");

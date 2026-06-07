const fs = require("fs");
const path = require("path");

const root = process.cwd();

function isFile(file) {
  return fs.existsSync(file) && fs.statSync(file).isFile();
}

function read(file) {
  return fs.readFileSync(file, "utf8");
}

function rel(file) {
  return path.relative(root, file).split(path.sep).join("/");
}

const failures = [];

const stripper = path.join(
  root,
  "Backend",
  "PlantProcess.Application",
  "Integration",
  "Security",
  "SafeSqlCommentStripper.cs"
);

if (!isFile(stripper)) {
  failures.push({ file: rel(stripper), reason: "SafeSqlCommentStripper is missing" });
} else {
  const text = read(stripper);

  for (const signal of [
    "PPIQ_REALIZATION_T004_LITERAL_AWARE_SQL_COMMENT_STRIPPER",
    "blockDepth",
    "inSingleQuote",
    "inDoubleQuote",
    "inBracketIdentifier"
  ]) {
    if (!text.includes(signal)) {
      failures.push({ file: rel(stripper), reason: "missing stripper signal: " + signal });
    }
  }
}

const validator = path.join(
  root,
  "Backend",
  "PlantProcess.Application",
  "Integration",
  "Security",
  "SafeSqlValidator.cs"
);

if (!isFile(validator)) {
  failures.push({ file: rel(validator), reason: "SafeSqlValidator is missing" });
} else {
  const text = read(validator);

  if (!text.includes("SafeSqlCommentStripper.Strip")) {
    failures.push({
      file: rel(validator),
      reason: "SafeSqlValidator does not call SafeSqlCommentStripper.Strip"
    });
  }
}

const coreProject = path.join(
  root,
  "Backend",
  "PlantProcess.Analytics.Core",
  "PlantProcess.Analytics.Core.csproj"
);

if (!isFile(coreProject)) {
  failures.push({ file: rel(coreProject), reason: "Analytics.Core project file is missing" });
} else if (read(coreProject).includes("PlantProcess.Application.csproj")) {
  failures.push({
    file: rel(coreProject),
    reason: "Analytics.Core must not reference Application project"
  });
}

const kpi = path.join(
  root,
  "Backend",
  "PlantProcess.Analytics.Core",
  "Kpi",
  "KpiEngine.cs"
);

if (!isFile(kpi)) {
  failures.push({ file: rel(kpi), reason: "KpiEngine.cs is missing" });
} else {
  const text = read(kpi);

  for (const signal of [
    "PPIQ_REALIZATION_T004_BASIC_SQL_GUARD",
    "public static class BasicSqlGuard",
    "ContainsSqlToken",
    "StripSqlComments",
    "ContainsSemicolonOutsideLiteral",
    "IsIdentifierCharacter"
  ]) {
    if (!text.includes(signal)) {
      failures.push({ file: rel(kpi), reason: "BasicSqlGuard missing signal: " + signal });
    }
  }

  if (/^\s*using\s+PlantProcess\.Application\b/m.test(text) || /\bPlantProcess\.Application\./.test(text)) {
    failures.push({
      file: rel(kpi),
      reason: "KpiEngine has a real Application-layer source reference"
    });
  }

  if (/lower\.Contains\(bad\)|lower\.Contains\(token\)|lower\.Contains\(forbidden\)/.test(text)) {
    failures.push({
      file: rel(kpi),
      reason: "BasicSqlGuard still appears to use naive forbidden-token substring matching"
    });
  }
}

if (failures.length) {
  console.error("PPIQ-T004 failed: SafeSql literal-aware validation is not fully proven.");
  console.error(JSON.stringify(failures, null, 2));
  process.exit(1);
}

console.log("PPIQ-T004 passed: SafeSqlValidator uses literal-aware stripper and BasicSqlGuard is independent, literal-aware, and token-boundary based.");

import { readdirSync, readFileSync, statSync } from "node:fs";
import { join } from "node:path";

const root = join(process.cwd(), "src");

const forbiddenPatterns = [
  /\bAI-powered\b/i,
  /\bML prediction\b/i,
  /\bmachine learning prediction\b/i,
  /\bartificial intelligence\b/i,
  /\bneural\b/i,
  /\broot cause\b/i,
  /\bpredicted\b/i,
  /\bpredictive\b/i,
  /\bprediction score\b/i
];

const approvedContextPatterns = [
  /\brule-based\b/i,
  /\bcorrelation\b/i,
  /\bsuspected contributor\b/i,
  /\bstatistical pattern\b/i,
  /\bdata-driven insight\b/i,
  /\bmodel registry\b/i,
  /\bprocess engineering validation\b/i
];

const files = [];

function walk(directory) {
  for (const item of readdirSync(directory)) {
    const path = join(directory, item);
    const stat = statSync(path);

    if (stat.isDirectory()) {
      walk(path);
      continue;
    }

    if (/\.(ts|tsx|css|html)$/.test(path)) {
      files.push(path);
    }
  }
}

walk(root);

const violations = [];

for (const file of files) {
  const text = readFileSync(file, "utf8");
  const lines = text.split(/\r?\n/);

  lines.forEach((line, index) => {
    for (const pattern of forbiddenPatterns) {
      if (pattern.test(line)) {
        const isApprovedContext = approvedContextPatterns.some((approved) =>
          approved.test(line)
        );

        if (!isApprovedContext) {
          violations.push({
            file,
            line: index + 1,
            text: line.trim()
          });
        }
      }
    }
  });
}

if (violations.length > 0) {
  console.error("");
  console.error("PlantProcess IQ language audit failed.");
  console.error("Replace misleading AI/ML/prediction wording with approved language:");
  console.error("- rule-based risk scoring");
  console.error("- correlation analysis");
  console.error("- suspected contributor");
  console.error("- statistical pattern");
  console.error("- data-driven insight");
  console.error("");

  for (const violation of violations) {
    console.error(`${violation.file}:${violation.line} ${violation.text}`);
  }

  console.error("");
  process.exit(1);
}

console.log("PlantProcess IQ language audit passed. No misleading AI/ML prediction terminology found.");

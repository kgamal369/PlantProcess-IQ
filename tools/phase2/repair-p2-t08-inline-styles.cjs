const fs = require("fs");
const path = require("path");
const childProcess = require("child_process");

const root = process.cwd();
const reportPath = path.join(root, "Documentation", "P2-T08_StandardRollout_Latest", "ui-standard-audit.json");

function fail(message) {
  console.error("[RED] P2-T08 inline-style repair failed: " + message);
  process.exit(1);
}

function backup(file) {
  const stamp = new Date().toISOString().replace(/[-:.TZ]/g, "").slice(0, 14);
  const rel = path.relative(root, file);
  const target = path.join(root, ".phase2_backups", "P2-T08_InlineStyleRepair_" + stamp, rel);
  fs.mkdirSync(path.dirname(target), { recursive: true });
  fs.copyFileSync(file, target);
}

function removeJsxStyleAttributes(text) {
  let output = "";
  let index = 0;
  let removed = 0;

  while (index < text.length) {
    const match = /\s+style\s*=\s*/g;
    match.lastIndex = index;
    const found = match.exec(text);

    if (!found) {
      output += text.slice(index);
      break;
    }

    const start = found.index;
    const valueStart = match.lastIndex;

    output += text.slice(index, start);

    const first = text[valueStart];

    if (first === '"' || first === "'") {
      const quote = first;
      let cursor = valueStart + 1;

      while (cursor < text.length) {
        if (text[cursor] === "\\" && cursor + 1 < text.length) {
          cursor += 2;
          continue;
        }

        if (text[cursor] === quote) {
          cursor += 1;
          break;
        }

        cursor += 1;
      }

      index = cursor;
      removed += 1;
      continue;
    }

    if (first === "{") {
      let cursor = valueStart;
      let depth = 0;
      let stringQuote = null;

      while (cursor < text.length) {
        const ch = text[cursor];

        if (stringQuote) {
          if (ch === "\\" && cursor + 1 < text.length) {
            cursor += 2;
            continue;
          }

          if (ch === stringQuote) {
            stringQuote = null;
          }

          cursor += 1;
          continue;
        }

        if (ch === '"' || ch === "'" || ch === "`") {
          stringQuote = ch;
          cursor += 1;
          continue;
        }

        if (ch === "{") {
          depth += 1;
        } else if (ch === "}") {
          depth -= 1;

          if (depth === 0) {
            cursor += 1;
            break;
          }
        }

        cursor += 1;
      }

      index = cursor;
      removed += 1;
      continue;
    }

    // Unknown style value shape. Keep it rather than corrupting TSX.
    output += text.slice(start, valueStart);
    index = valueStart;
  }

  return { text: output, removed };
}

if (!fs.existsSync(reportPath)) {
  fail("Audit report not found. Run: node tools/ui/audit-ui-instances.cjs");
}

const report = JSON.parse(fs.readFileSync(reportPath, "utf8"));
const inlineFindings = (report.findings || []).filter((x) => x.kind === "inline-style");

console.log("[P2-T08] Inline-style findings: " + inlineFindings.length);

if (inlineFindings.length === 0) {
  console.log("[GREEN] No inline styles to repair.");
  process.exit(0);
}

const files = Array.from(new Set(inlineFindings.map((x) => x.file)));

for (const relFromWebRoot of files) {
  const file = path.join(root, "Frontend", "PlantProcess.Web", relFromWebRoot.replaceAll("/", path.sep));

  if (!fs.existsSync(file)) {
    fail("Finding file not found: " + file);
  }

  const before = fs.readFileSync(file, "utf8");
  const result = removeJsxStyleAttributes(before);

  if (result.removed === 0) {
    fail("Could not remove inline style from " + relFromWebRoot);
  }

  backup(file);
  fs.writeFileSync(file, result.text.replace(/\r?\n/g, "\r\n"), "utf8");

  console.log("[P2-T08] Removed " + result.removed + " inline style attribute(s) from " + relFromWebRoot);
}

const validation = childProcess.spawnSync("node", ["tools/phase2/validate-p2-t08-standard-rollout.cjs"], {
  cwd: root,
  encoding: "utf8",
  shell: process.platform === "win32",
});

process.stdout.write(validation.stdout || "");
process.stderr.write(validation.stderr || "");

if (validation.status !== 0) {
  process.exit(validation.status);
}

console.log("[GREEN] P2-T08 inline-style repair completed and static validation passed.");

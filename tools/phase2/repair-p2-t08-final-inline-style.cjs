const fs = require("fs");
const path = require("path");
const childProcess = require("child_process");

const root = process.cwd();
const reportPath = path.join(root, "Documentation", "P2-T08_StandardRollout_Latest", "ui-standard-audit.json");

function fail(message) {
  console.error("[RED] P2-T08 final inline-style repair failed: " + message);
  process.exit(1);
}

function run(command, args, cwd = root) {
  const result = childProcess.spawnSync(command, args, {
    cwd,
    encoding: "utf8",
    shell: process.platform === "win32",
  });

  process.stdout.write(result.stdout || "");
  process.stderr.write(result.stderr || "");

  if (result.status !== 0) {
    fail(command + " " + args.join(" ") + " failed with exit code " + result.status);
  }

  return result;
}

function backup(file) {
  const stamp = new Date().toISOString().replace(/[-:.TZ]/g, "").slice(0, 14);
  const rel = path.relative(root, file);
  const target = path.join(root, ".phase2_backups", "P2-T08_FinalInlineStyleRepair_" + stamp, rel);
  fs.mkdirSync(path.dirname(target), { recursive: true });
  fs.copyFileSync(file, target);
  return target;
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

      if (depth !== 0) {
        fail("Unbalanced JSX style attribute while repairing.");
      }

      index = cursor;
      removed += 1;
      continue;
    }

    fail("Unsupported style attribute shape near: " + text.slice(start, start + 120));
  }

  return { text: output, removed };
}

console.log("[P2-T08] Refreshing UI audit report...");
run("node", ["tools/ui/audit-ui-instances.cjs"], root);

if (!fs.existsSync(reportPath)) {
  fail("Audit report was not generated.");
}

const report = JSON.parse(fs.readFileSync(reportPath, "utf8"));
const inlineFindings = (report.findings || []).filter((x) => x.kind === "inline-style");

console.log("[P2-T08] Remaining inline-style findings: " + inlineFindings.length);

for (const finding of inlineFindings) {
  console.log(" - " + finding.file + ":" + finding.line + " :: " + finding.snippet);
}

if (inlineFindings.length === 0) {
  console.log("[GREEN] No inline styles remain.");
  process.exit(0);
}

const files = Array.from(new Set(inlineFindings.map((x) => x.file)));

for (const relFromWebRoot of files) {
  const file = path.join(root, "Frontend", "PlantProcess.Web", relFromWebRoot.replaceAll("/", path.sep));

  if (!fs.existsSync(file)) {
    fail("Finding file not found: " + file);
  }

  const before = fs.readFileSync(file, "utf8");
  const repaired = removeJsxStyleAttributes(before);

  if (repaired.removed === 0) {
    fail("No style attribute removed from " + relFromWebRoot);
  }

  const backupPath = backup(file);
  fs.writeFileSync(file, repaired.text.replace(/\r?\n/g, "\r\n"), "utf8");

  console.log("[P2-T08] Removed " + repaired.removed + " style attribute(s) from " + relFromWebRoot);
  console.log("[P2-T08] Backup: " + backupPath);
}

run("node", ["tools/phase2/validate-p2-t08-standard-rollout.cjs"], root);

console.log("[GREEN] P2-T08 final inline-style repair passed static validation.");

#!/usr/bin/env node
// Fails (exit 1) if any CI-critical file contains a CR (\r). Keeps the
// Jenkinsfile/shell-continuation failure class from returning.
import fs from "node:fs";
import path from "node:path";

const root = process.cwd();
const exts = [".sh", ".mjs", ".cjs", ".yml", ".yaml"];
const names = ["Jenkinsfile"];
const offenders = [];

function walk(dir) {
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    if (e.name === "node_modules" || e.name === ".git" || e.name.startsWith("_phase1_backup_")) continue;
    const full = path.join(dir, e.name);
    if (e.isDirectory()) { walk(full); continue; }
    const isTarget = names.includes(e.name) || exts.includes(path.extname(e.name));
    if (!isTarget) continue;
    if (fs.readFileSync(full).includes(0x0d)) offenders.push(path.relative(root, full));
  }
}

walk(root);
if (offenders.length) {
  console.error("CRLF found in CI-critical files (must be LF):");
  for (const f of offenders) console.error("  - " + f);
  process.exit(1);
}
console.log("CRLF check passed: no CR in Jenkinsfile/*.sh/*.mjs/*.cjs/*.yml/*.yaml");
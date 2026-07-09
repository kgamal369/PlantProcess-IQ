// ============================================================
// Encoding regression gate (Step 1, 09-Jul-2026).
// Source files must never again contain UTF-8-decoded-as-Windows-1252 text
// ("mojibake"), e.g. a stray A-circumflex before a middot in "Pack G . T-096".
// Root cause when this fires: a file was read as cp1252 and re-saved as UTF-8.
// Fix by re-saving as UTF-8 (no BOM), not by deleting the characters.
// ============================================================
import { describe, expect, it } from "vitest";
import { readdirSync, readFileSync, statSync } from "node:fs";
import { resolve, join } from "node:path";

const srcRoot = resolve(__dirname, "../..");
const EXCLUDE = /node_modules|dist|_phase9_standardbutton_dedupe_backup/;
const MOJIBAKE = /[\u00C3\u00C2][\u0080-\u00FF\u2000-\u20FF\u0152-\u0178]|\u00E2[\u0080-\u30FF]/;

function walk(dir: string, out: string[] = []): string[] {
  for (const entry of readdirSync(dir)) {
    const full = join(dir, entry);
    if (EXCLUDE.test(full)) continue;
    if (statSync(full).isDirectory()) walk(full, out);
    else if (/\.(ts|tsx|css)$/.test(entry)) out.push(full);
  }
  return out;
}

describe("encoding: no mojibake in source", () => {
  it("no source file contains UTF-8-as-cp1252 corruption", () => {
    const offenders: string[] = [];
    for (const file of walk(srcRoot)) {
      const lines = readFileSync(file, "utf8").split(/\r?\n/);
      lines.forEach((line, i) => {
        if (MOJIBAKE.test(line)) {
          offenders.push(`${file.replace(srcRoot, "src")}:${i + 1}: ${line.trim().slice(0, 60)}`);
        }
      });
    }
    expect(offenders, `Mojibake found:\n  ${offenders.join("\n  ")}`).toHaveLength(0);
  });
});
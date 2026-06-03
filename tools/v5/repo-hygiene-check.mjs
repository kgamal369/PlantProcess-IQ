import fs from "node:fs";
import path from "node:path";

const root = process.cwd();

const banned = [
  /\.bak($|_)/i,
  /\.fixbak$/i,
  /(^|[\\/])storybook-static([\\/]|$)/i,
  /(^|[\\/])dist([\\/]|$)/i,
  /(^|[\\/])coverage([\\/]|$)/i,
  /(^|[\\/])playwright-report([\\/]|$)/i,
  /(^|[\\/])test-results([\\/]|$)/i,
  /(^|[\\/])Documentation[\\/]UltimateAudit_/i,
];

const allowedRoots = new Set([
  "node_modules",
  ".git",
]);

function walk(dir) {
  const out = [];

  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    if (allowedRoots.has(entry.name)) continue;

    const full = path.join(dir, entry.name);
    const rel = path.relative(root, full);

    if (entry.isDirectory()) {
      out.push(...walk(full));
    } else if (entry.isFile()) {
      out.push(rel);
    }
  }

  return out;
}

const offenders = walk(root).filter((rel) => banned.some((rx) => rx.test(rel)));

if (offenders.length > 0) {
  console.error("Repo hygiene failed. Generated/backup artifacts found:");
  for (const offender of offenders.slice(0, 100)) {
    console.error(` - ${offender}`);
  }
  if (offenders.length > 100) console.error(`... and ${offenders.length - 100} more.`);
  process.exit(1);
}

console.log("Repo hygiene check passed.");
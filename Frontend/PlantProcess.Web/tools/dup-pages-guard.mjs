// P1-05 CI guard: fails the build if duplicate/orphan pages exist.
import { execSync } from "node:child_process";
try {
  const out = execSync("node tools/check-duplicate-pages.mjs", { encoding: "utf8" });
  process.stdout.write(out);
  if (/duplicate|orphan/i.test(out) && !/no (duplicate|orphan)/i.test(out)) {
    console.error("\n[dup-pages-guard] Duplicate/orphan pages detected. Failing build.");
    process.exit(1);
  }
  console.log("[dup-pages-guard] OK - no duplicate/orphan pages.");
} catch (e) { console.error(e.stdout || e.message); process.exit(1); }

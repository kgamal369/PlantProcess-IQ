const fs = require("node:fs");
const path = require("node:path");
const cp = require("node:child_process");

const cwd = process.cwd();

const isFrontendRoot =
  fs.existsSync(path.join(cwd, "src")) &&
  fs.existsSync(path.join(cwd, "package.json")) &&
  fs.existsSync(path.join(cwd, "scripts", "validate-standard-imports.mjs"));

const repoRoot = isFrontendRoot ? path.resolve(cwd, "..", "..") : cwd;
const webRoot = isFrontendRoot ? cwd : path.join(repoRoot, "Frontend", "PlantProcess.Web");
const target = path.join(webRoot, "src", "pages", "__standard_import_gate_negative__.tsx");

function run(command, cwdToUse) {
  return cp.spawnSync(command, {
    cwd: cwdToUse,
    shell: true,
    encoding: "utf8",
    stdio: "pipe",
  });
}

if (!fs.existsSync(path.join(webRoot, "scripts", "validate-standard-imports.mjs"))) {
  console.error("FAIL: could not locate validate-standard-imports.mjs");
  console.error("cwd=" + cwd);
  console.error("webRoot=" + webRoot);
  process.exit(1);
}

try {
  fs.writeFileSync(
    target,
    "import \"@/hardening/forbidden\";\nexport function StandardImportGateNegative(){ return <button>bad</button>; }\n",
    "utf8",
  );

  const negative = run("npm run validate:standard-imports", webRoot);

  if (negative.status === 0) {
    console.error("FAIL: standard-import validator did not reject native <button> and hardening import.");
    console.error(negative.stdout);
    console.error(negative.stderr);
    process.exit(1);
  }

  console.log("OK negative proof: native <button> and hardening import were rejected.");
} finally {
  if (fs.existsSync(target)) fs.unlinkSync(target);
}

const positive = run("npm run validate:standard-imports", webRoot);

if (positive.status !== 0) {
  console.error("FAIL: standard-import validator did not pass after removing scratch violation.");
  console.error(positive.stdout);
  console.error(positive.stderr);
  process.exit(1);
}

console.log("PPIQ-T205 passed: standard-import gate rejects violation and passes clean tree.");

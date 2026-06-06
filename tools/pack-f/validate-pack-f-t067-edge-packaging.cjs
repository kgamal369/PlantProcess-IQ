const fs = require("fs");
const path = require("path");
const cp = require("child_process");

const root = process.cwd();

const requiredFiles = [
  "tools/edge-agent/ppiq-edge-agent.cjs",
  "tools/edge-agent/edge-agent.sample.json",
  "tools/edge-agent/package-manifest.json",
  "scripts/edge-agent/Run-PPIQ-EdgeAgent-Local.ps1",
  "scripts/edge-agent/Install-PPIQ-EdgeAgent-Service.ps1",
  "scripts/edge-agent/Uninstall-PPIQ-EdgeAgent-Service.ps1",
  "deploy/edge-agent/Dockerfile",
  "deploy/edge-agent/docker-compose.edge-agent.yml",
  "deploy/edge-agent/.env.edge-agent.template",
  "deploy/edge-agent/README_EDGE_AGENT_DEPLOYMENT.md",
  "docs/developer/OT_SAFE_EDGE_AGENT_DEPLOYMENT_GUIDE.md",
  "docs/pack-f/PACK_F3_T067_EDGE_AGENT_PACKAGING_REPORT.json",
  "docs/pack-f/PACK_F3_T067_EDGE_AGENT_PACKAGING_REPORT.md",
  "docs/pack-f/PACK_F_IMPLEMENTATION_EVIDENCE.md"
];

const requiredSignals = [
  { file: "tools/edge-agent/edge-agent.sample.json", signal: "\"readOnlyCollection\": true" },
  { file: "tools/edge-agent/edge-agent.sample.json", signal: "\"outboundOnly\": true" },
  { file: "tools/edge-agent/edge-agent.sample.json", signal: "\"opensInboundListener\": false" },
  { file: "tools/edge-agent/ppiq-edge-agent.cjs", signal: "read-only-outbound-one-way-push" },
  { file: "tools/edge-agent/ppiq-edge-agent.cjs", signal: "assertSafety" },
  { file: "scripts/edge-agent/Run-PPIQ-EdgeAgent-Local.ps1", signal: "--dry-run" },
  { file: "deploy/edge-agent/Dockerfile", signal: "node:22-alpine" },
  { file: "deploy/edge-agent/docker-compose.edge-agent.yml", signal: "ppiq-edge-agent" },
  { file: "deploy/edge-agent/README_EDGE_AGENT_DEPLOYMENT.md", signal: "PPIQ_PACK_F3_EDGE_AGENT_PACKAGING" },
  { file: "docs/developer/OT_SAFE_EDGE_AGENT_DEPLOYMENT_GUIDE.md", signal: "opensInboundListener=false" },
  { file: "docs/pack-f/PACK_F_IMPLEMENTATION_EVIDENCE.md", signal: "PPIQ_PACK_F3_EDGE_AGENT_PACKAGING" }
];

function isFile(relativePath) {
  const absolute = path.join(root, relativePath);
  return fs.existsSync(absolute) && fs.statSync(absolute).isFile();
}

function read(relativePath) {
  return fs.readFileSync(path.join(root, relativePath), "utf8");
}

function runOk(cmd, args) { try { cp.execFileSync(cmd, args, { cwd: root, stdio: "pipe", shell: false }); return true; } catch { return false; } }

const failures = [];

for (const file of requiredFiles) {
  if (!isFile(file)) failures.push({ file, reason: "missing" });
}

for (const item of requiredSignals) {
  if (!isFile(item.file)) { failures.push({ file: item.file, signal: item.signal, reason: "missing-file" }); continue; }
  if (!read(item.file).includes(item.signal)) failures.push({ file: item.file, signal: item.signal, reason: "missing-signal" });
}

if (!runOk("node", ["--check", "tools/edge-agent/ppiq-edge-agent.cjs"])) failures.push({ reason: "edge-agent-node-check-failed" });
if (!runOk("node", ["tools/edge-agent/ppiq-edge-agent.cjs", "--config=tools/edge-agent/edge-agent.sample.json", "--dry-run", "--once"])) failures.push({ reason: "edge-agent-dry-run-failed" });

if (failures.length) {
  console.error("Pack F-3 T-067 edge packaging validation failed.");
  console.error(JSON.stringify(failures, null, 2));
  process.exit(1);
}

console.log("Pack F-3 T-067 edge packaging/deployment validation passed.");

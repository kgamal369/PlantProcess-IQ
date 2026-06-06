const fs = require("fs");
const path = require("path");

const root = process.cwd();

const snapshotJsonPath = path.join(root, "docs", "pack-f", "PACK_F5_EDGE_FINAL_CONTRACT_SNAPSHOT.json");
const snapshotMdPath = path.join(root, "docs", "pack-f", "PACK_F5_EDGE_FINAL_CONTRACT_SNAPSHOT.md");
const finalAcceptancePath = path.join(root, "docs", "pack-f", "PACK_F_FINAL_ACCEPTANCE.md");

function isFile(file) {
  return fs.existsSync(file) && fs.statSync(file).isFile();
}

function read(relativePath) {
  const absolute = path.join(root, relativePath);
  return isFile(absolute) ? fs.readFileSync(absolute, "utf8").replace(/\r\n/g, "\n") : "";
}

function write(file, content) {
  fs.mkdirSync(path.dirname(file), { recursive: true });
  fs.writeFileSync(file, content.replace(/\n/g, "\r\n"), "utf8");
  console.log("Wrote: " + path.relative(root, file).split(path.sep).join("/"));
}

function has(text, ...needles) {
  const lower = text.toLowerCase();
  return needles.some((needle) => lower.includes(String(needle).toLowerCase()));
}

function readJson(relativePath) {
  const text = read(relativePath);
  return text ? JSON.parse(text) : {};
}

const endpoint = read("Backend/PlantProcess.Api/PlantConnectors/V5OtSafeEdgeCollectorEndpoints.cs");
const contract = read("Backend/PlantProcess.Workers/Edge/OtSafeEdgeAgentContract.cs");
const agent = read("tools/edge-agent/ppiq-edge-agent.cjs");
const configText = read("tools/edge-agent/edge-agent.sample.json");
const config = configText ? JSON.parse(configText) : {};
const safety = config.safety || {};

if (!isFile(snapshotJsonPath)) {
  throw new Error("Missing snapshot JSON: " + snapshotJsonPath);
}

const snapshot = JSON.parse(fs.readFileSync(snapshotJsonPath, "utf8"));

snapshot.safetySignals = [
  {
    signal: "ReadOnlyCollection true",
    present:
      has(endpoint, "ReadOnlyCollection") &&
      has(contract, "ReadOnlyCollection = true", "ReadOnlyCollection: true") &&
      safety.readOnlyCollection === true
  },
  {
    signal: "OutboundOnly true",
    present:
      has(endpoint, "OutboundOnly") &&
      has(contract, "OutboundOnly = true", "OutboundOnly: true") &&
      safety.outboundOnly === true
  },
  {
    signal: "OpensInboundListener false",
    present:
      has(endpoint, "OpensInboundListener") &&
      has(contract, "OpensInboundListener = false", "OpensInboundListener: false") &&
      safety.opensInboundListener === false
  },
  {
    signal: "No inbound OT access",
    present:
      has(endpoint, "noInboundOtAccessRequired", "must not open an inbound listener", "must not open an inbound listener in the OT network") ||
      has(contract, "no inbound listener required in the OT network") ||
      has(read("docs/developer/OT_SAFE_EDGE_AGENT_CONTRACT.md"), "No inbound OT listener")
  },
  {
    signal: "Batch limit",
    present:
      has(endpoint, "5000") &&
      has(agent, "5000")
  },
  {
    signal: "Dry-run safe mode",
    present:
      has(agent, "--dry-run") &&
      has(agent, "Dry-run completed")
  }
];

snapshot.acceptance = snapshot.acceptance || {};
snapshot.acceptance.otSafetyGreen = snapshot.safetySignals.every((item) => item.present);

write(snapshotJsonPath, JSON.stringify(snapshot, null, 2) + "\n");

const md = [];
md.push("# Pack F-5 Edge Final Contract Snapshot");
md.push("");
md.push("Generated: " + snapshot.generatedAtUtc);
md.push("");
md.push("Marker: PPIQ_PACK_F5_EDGE_FINAL_CONTRACT_SNAPSHOT");
md.push("");
md.push("## Acceptance");
md.push("");
md.push("- Backend contract green: **" + snapshot.acceptance.backendContractGreen + "**");
md.push("- OT-safety contract green: **" + snapshot.acceptance.otSafetyGreen + "**");
md.push("- Packaging contract green: **" + snapshot.acceptance.packagingGreen + "**");
md.push("- UX contract green: **" + snapshot.acceptance.uxGreen + "**");
md.push("");
md.push("## OT-safety contract");
md.push("");
md.push("| Signal | Present |");
md.push("|---|---:|");
for (const item of snapshot.safetySignals) {
  md.push("| `" + item.signal + "` | " + (item.present ? "YES" : "NO") + " |");
}
md.push("");

write(snapshotMdPath, md.join("\n"));

const acceptance = [];
acceptance.push("# Pack F Final Acceptance");
acceptance.push("");
acceptance.push("Marker: PPIQ_PACK_F5_EDGE_TESTS_DOCS_REGRESSION");
acceptance.push("");
acceptance.push("## Acceptance status");
acceptance.push("");
acceptance.push("- Backend contract green: **" + snapshot.acceptance.backendContractGreen + "**");
acceptance.push("- OT-safety contract green: **" + snapshot.acceptance.otSafetyGreen + "**");
acceptance.push("- Packaging contract green: **" + snapshot.acceptance.packagingGreen + "**");
acceptance.push("- UX contract green: **" + snapshot.acceptance.uxGreen + "**");
acceptance.push("");
acceptance.push("## Closed Pack F tasks");
acceptance.push("");
acceptance.push("- T-066 — OT-safe edge agent one-way push backend.");
acceptance.push("- T-067 — Edge agent packaging and deployment.");
acceptance.push("- T-068 — Edge collector management UX.");
acceptance.push("- T-071 — Edge tests docs regression.");
acceptance.push("");

write(finalAcceptancePath, acceptance.join("\n"));

console.log("");
console.log("Recomputed OT-safety signals:");
for (const item of snapshot.safetySignals) {
  console.log(" - " + item.signal + ": " + (item.present ? "GREEN" : "RED"));
}

if (!snapshot.acceptance.otSafetyGreen) {
  console.error("");
  console.error("OT-safety still not green. Fix the RED signal above before bridging.");
  process.exit(1);
}

console.log("");
console.log("Pack F-5 OT-safety snapshot fixed.");
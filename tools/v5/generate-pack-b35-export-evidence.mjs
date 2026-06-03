import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";

const root = process.cwd();
const outDir = path.join(root, "Documentation", "v5", "pack-b35-export-evidence");
fs.mkdirSync(outDir, { recursive: true });

const files = [
  "deploy/export/open-format-manifest.schema.json",
  "deploy/export/export-open-format.ps1",
  "docs/escrow/SOURCE_CODE_ESCROW_PROCESS.md",
  "deploy/dr/RPO_RTO_RUNBOOK.md",
  "deploy/airgap/docker-compose.airgap.yml",
  "deploy/airgap/preflight-airgap.ps1"
];

const entries = [];

for (const relative of files) {
  const full = path.join(root, relative);
  if (!fs.existsSync(full)) {
    entries.push({
      path: relative,
      exists: false,
      sha256: null,
      bytes: 0
    });
    continue;
  }

  const bytes = fs.readFileSync(full);
  entries.push({
    path: relative,
    exists: true,
    sha256: crypto.createHash("sha256").update(bytes).digest("hex"),
    bytes: bytes.length
  });
}

const manifest = {
  schemaVersion: "ppiq-pack-b35-export-evidence-v1",
  generatedAtUtc: new Date().toISOString(),
  purpose: "P13-T03/P13-T04 local evidence checksum manifest",
  entries
};

const manifestJson = JSON.stringify(manifest, null, 2);
const manifestSha = crypto.createHash("sha256").update(manifestJson).digest("hex");

fs.writeFileSync(path.join(outDir, "manifest.json"), manifestJson, "utf8");
fs.writeFileSync(path.join(outDir, "manifest.sha256.txt"), `${manifestSha}  manifest.json\n`, "utf8");

console.log(JSON.stringify({
  outDir,
  manifestSha,
  missing: entries.filter((x) => !x.exists).map((x) => x.path)
}, null, 2));

if (entries.some((x) => !x.exists)) {
  process.exit(2);
}
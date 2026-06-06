#!/usr/bin/env node

const fs = require("fs");
const path = require("path");

const args = process.argv.slice(2);
const configArg = args.find((arg) => arg.startsWith("--config="));
const dryRun = args.includes("--dry-run") || !args.includes("--push");
const once = args.includes("--once");

const configPath = configArg
  ? path.resolve(configArg.slice("--config=".length))
  : path.resolve(__dirname, "edge-agent.sample.json");

function readJson(file) {
  return JSON.parse(fs.readFileSync(file, "utf8"));
}

function ensureDir(dir) {
  fs.mkdirSync(dir, { recursive: true });
}

function nowIso() {
  return new Date().toISOString();
}

function assertSafety(config) {
  if (config.safety?.readOnlyCollection !== true) throw new Error("readOnlyCollection must be true");
  if (config.safety?.outboundOnly !== true) throw new Error("outboundOnly must be true");
  if (config.safety?.opensInboundListener !== false) throw new Error("opensInboundListener must be false");
  if (!config.plantProcessIq?.baseUrl) throw new Error("plantProcessIq.baseUrl is required");
  if (!config.collector?.collectorId) throw new Error("collector.collectorId is required");
}

function deterministicSample(profile, tagPath, index) {
  const hash = Math.abs([...tagPath].reduce((sum, ch) => sum + ch.charCodeAt(0), 0));
  return {
    sourceProfile: profile.profileCode,
    tagPath,
    timestampUtc: nowIso(),
    numericValue: Math.round((10 + (hash % 100) + index * 0.5) * 100) / 100,
    textValue: null,
    unit: tagPath.toLowerCase().includes("temperature") ? "degC" : "engineering-unit",
    quality: "Good"
  };
}

async function postJson(url, payload, tokenReference) {
  const headers = { "content-type": "application/json" };
  if (tokenReference) headers["x-ppiq-token-reference"] = tokenReference;

  const response = await fetch(url, { method: "POST", headers, body: JSON.stringify(payload) });
  const text = await response.text();
  if (!response.ok) throw new Error(`${response.status} ${response.statusText}: ${text}`);
  return text ? JSON.parse(text) : {};
}

function buildHeartbeat(config, queueDepth, failedPushCount) {
  return {
    collectorId: config.collector.collectorId,
    agentVersion: config.collector.agentVersion,
    observedAtUtc: nowIso(),
    status: "healthy",
    localQueueDepth: queueDepth,
    failedPushCount,
    lastSuccessfulPushUtc: null,
    lastError: null
  };
}

function buildBatch(config) {
  const samples = [];
  for (const profile of config.sourceProfiles || []) {
    for (const [index, tagPath] of (profile.tagPaths || []).entries()) {
      samples.push(deterministicSample(profile, tagPath, index));
    }
  }

  return {
    collectorId: config.collector.collectorId,
    batchId: `batch-${Date.now()}`,
    createdAtUtc: nowIso(),
    readOnlyCollection: true,
    outboundOnly: true,
    sequenceNumber: 1,
    samples
  };
}

async function main() {
  const config = readJson(configPath);
  assertSafety(config);

  const spoolDir = path.resolve(path.dirname(configPath), config.spool?.directory || "./spool");
  ensureDir(spoolDir);

  const baseUrl = config.plantProcessIq.baseUrl.replace(/\/$/, "");
  const tokenReference = config.plantProcessIq.tokenReference || null;

  const heartbeat = buildHeartbeat(config, 0, 0);
  const batch = buildBatch(config);
  const spoolFile = path.join(spoolDir, `${batch.batchId}.json`);
  fs.writeFileSync(spoolFile, JSON.stringify({ heartbeat, batch }, null, 2), "utf8");

  console.log("PPIQ OT-safe edge agent");
  console.log("Mode: read-only-outbound-one-way-push");
  console.log("Config:", configPath);
  console.log("Spool:", spoolFile);
  console.log("Dry run:", dryRun);

  if (dryRun) {
    console.log("Dry-run completed. No network push executed.");
    return;
  }

  await postJson(`${baseUrl}/api/v5/edge-collector/heartbeat`, heartbeat, tokenReference);
  await postJson(`${baseUrl}/api/v5/edge-collector/push-batch`, batch, tokenReference);

  console.log("Outbound heartbeat and batch push completed.");

  if (!once) {
    console.log("Long-running loop is intentionally not enabled in the package sample. Use service wrapper scheduling for production.");
  }
}

main().catch((error) => {
  console.error(error.message || error);
  process.exit(1);
});

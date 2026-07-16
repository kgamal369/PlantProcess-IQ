#!/usr/bin/env node
"use strict";

const fs = require("node:fs");
const path = require("node:path");

const webRoot = path.resolve(__dirname, "../..");
const resultDir = path.join(webRoot, "test-results", "journey-certification");
const statusPath = path.join(resultDir, "command-status.json");
const playwrightPath = path.join(resultDir, "playwright.json");
const uiAuditPath = path.join(resultDir, "ui-audit.json");

function json(file, fallback) {
  try { return JSON.parse(fs.readFileSync(file, "utf8")); }
  catch { return fallback; }
}

const commandStatus = json(statusPath, { commands: [] });
const playwright = json(playwrightPath, {});
const uiAudit = json(uiAuditPath, { pages: [], pass: false });
const commandMap = new Map((commandStatus.commands || []).map((row) => [row.id, row]));

function commandPassed(id) {
  return commandMap.get(id)?.status === "PASS";
}

function flattenPlaywright(node, inherited = []) {
  const rows = [];
  const title = node.title ? [...inherited, node.title] : inherited;
  for (const spec of node.specs || []) {
    for (const test of spec.tests || []) {
      const results = test.results || [];
      rows.push({
        title: [...title, spec.title, test.projectName].filter(Boolean).join(" â€º "),
        ok: results.length > 0 && results.every((result) => result.status === "passed"),
        skipped: results.length === 0 || results.some((result) => result.status === "skipped"),
      });
    }
  }
  for (const suite of node.suites || []) rows.push(...flattenPlaywright(suite, title));
  return rows;
}

const playwrightTests = flattenPlaywright(playwright);
function e2ePassed(tag) {
  const hits = playwrightTests.filter((row) => row.title.includes(`[${tag}]`));
  return hits.length > 0 && hits.every((row) => row.ok && !row.skipped);
}
function uxPassed(tag) {
  const hits = playwrightTests.filter((row) => row.title.includes(`[UX-${tag}]`));
  return hits.length > 0 && hits.every((row) => row.ok && !row.skipped);
}
function staticUiPassed(tag) {
  return uiAudit.pages?.find((row) => row.tag === tag)?.pass === true;
}

const capabilities = [
  ["J01", "Connect"], ["J02", "Register and schedule"], ["J03", "Incremental import"],
  ["J04", "Data preparation and mapping"], ["J05", "Loading jobs"], ["J06", "Loaded canonical verification"],
  ["J07", "Dashboards and widgets"], ["J08", "Analysis authoring"], ["J09", "Analysis jobs"],
  ["J10", "Findings"], ["J11", "AI/ML readiness"], ["J12", "AI/ML jobs"], ["J13", "AI/ML results"],
  ["J14", "Supervisor"], ["J15", "Assistant"], ["UI4", "Plant Data Log"],
];

const backendUnitTags = new Set(["J08", "J09", "J10", "J11", "J12", "J13", "J15"]);
const backendIntegrationTags = new Set(["J01", "J02", "J03", "J04", "J05", "J06", "J08", "J09", "J10", "J14", "J15", "UI4"]);
const frontendUnitRequiredTags = new Set(["J04", "J14", "UI4"]);
const backendUnitPassed = commandPassed("backend-unit-analytics") && commandPassed("backend-unit-assistant");
const frontendBuildPassed = commandPassed("frontend-build");
const frontendUnitPassed = commandPassed("frontend-unit");

const rows = capabilities.map(([tag, name]) => {
  const backendRelevant = backendUnitTags.has(tag) || backendIntegrationTags.has(tag);
  const backendEvidence =
    (backendUnitTags.has(tag) ? backendUnitPassed : true) &&
    (backendIntegrationTags.has(tag) ? commandPassed("backend-integration") : true);
  const backend = backendRelevant && backendEvidence ? 25 : 0;

  const frontendEvidence =
    frontendBuildPassed &&
    staticUiPassed(tag) &&
    (!frontendUnitRequiredTags.has(tag) || frontendUnitPassed);
  const frontend = frontendEvidence ? 25 : 0;
  const e2e = e2ePassed(tag) ? 35 : 0;
  const ux = uxPassed(tag) ? 15 : 0;
  const score = backend + frontend + e2e + ux;
  return { tag, name, backend, frontend, e2e, ux, score, pass: score >= 75 };
});

const mandatory = new Set(["J01", "J02", "J03", "J04", "J05", "J06", "J07", "J08", "J09", "J10", "J14", "J15", "UI4"]);
const passed = rows.filter((row) => row.pass).length;
const mandatoryPassed = rows.filter((row) => mandatory.has(row.tag)).every((row) => row.pass);
const noSkippedPlaywright = playwrightTests.every((row) => !row.skipped);
const commandFailures = (commandStatus.commands || []).filter((row) => row.required !== false && row.status !== "PASS");
const certified = passed >= 13 && mandatoryPassed && noSkippedPlaywright && commandFailures.length === 0;

const payload = {
  generatedAtUtc: new Date().toISOString(),
  certified,
  passedCapabilities: passed,
  totalCapabilities: rows.length,
  coveragePercent: Number(((passed / rows.length) * 100).toFixed(2)),
  mandatoryPassed,
  noSkippedPlaywright,
  commandFailures,
  capabilities: rows,
};

fs.writeFileSync(path.join(resultDir, "journey-score.json"), JSON.stringify(payload, null, 2));
const markdown = [
  "# PPIQ Automated Journey Certification",
  "",
  `Decision: **${certified ? "AUTOMATED JOURNEY CERTIFIED" : "NOT CERTIFIED"}**`,
  `Capabilities passing â‰¥75: **${passed}/${rows.length} (${payload.coveragePercent}%)**`,
  `Mandatory steps: **${mandatoryPassed ? "PASS" : "FAIL"}**`,
  `Skipped Playwright tests: **${noSkippedPlaywright ? "0" : "PRESENT"}**`,
  "",
  "| Tag | Capability | Backend | Frontend | E2E | UI/UX | Score | Result |",
  "|---|---|---:|---:|---:|---:|---:|---|",
  ...rows.map((row) => `| ${row.tag} | ${row.name} | ${row.backend} | ${row.frontend} | ${row.e2e} | ${row.ux} | ${row.score} | ${row.pass ? "PASS" : "FAIL"} |`),
  "",
  "## Command failures",
  ...(commandFailures.length ? commandFailures.map((row) => `- ${row.id}: ${row.status}`) : ["- None"]),
].join("\n");
fs.writeFileSync(path.join(resultDir, "journey-score.md"), markdown);
console.log(markdown);
process.exit(certified ? 0 : 1);

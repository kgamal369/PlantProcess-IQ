#!/usr/bin/env node
"use strict";

const fs = require("node:fs");
const path = require("node:path");

const webRoot = path.resolve(__dirname, "../..");
const outputDir = path.join(webRoot, "test-results", "journey-certification");
fs.mkdirSync(outputDir, { recursive: true });

const pages = [
  { tag: "J01", name: "Connections", files: ["src/pages/Admin/AdminDbConfigurationTab.tsx", "src/pages/DataIntegration/DataIntegrationLayout.tsx", "src/pages/DataIntegration/DataIntegrationRoutes.tsx"] },
  { tag: "J02", name: "Registry", files: ["src/pages/Admin/AdminSchemaConfigurationTab.tsx", "src/pages/Admin/AdminSchemaConfigurationTab.implementation.tsx", "src/pages/DataIntegration/DataIntegrationLayout.tsx", "src/pages/DataIntegration/DataIntegrationRoutes.tsx"] },
  { tag: "J03", name: "Importing", files: ["src/pages/Admin/AdminImportingDataTab.tsx", "src/pages/DataIntegration/DataIntegrationLayout.tsx", "src/pages/DataIntegration/DataIntegrationRoutes.tsx"] },
  { tag: "J04", name: "Author mapping", files: ["src/pages/DataIntegration/AuthorMappingPage.tsx"] },
  { tag: "J05", name: "Jobs monitor", files: ["src/pages/Admin/AdminJobsMonitorTab.tsx", "src/pages/DataIntegration/DataIntegrationLayout.tsx", "src/pages/DataIntegration/DataIntegrationRoutes.tsx"] },
  { tag: "J06", name: "Material investigation", files: ["src/pages/MaterialInvestigationPage.tsx", "src/pages/MaterialAnalytics/MaterialAnalyticsPages.tsx", "src/components/materials/GenealogyThreadPanel.tsx", "src/components/dashboard/widget-builder/WidgetScriptBuilderPanel.tsx"] },
  { tag: "J07", name: "Dashboard", files: ["src/pages/Dashboard/DashboardPage.tsx", "src/pages/Dashboard/DashboardPageContent.tsx"] },
  { tag: "J08", name: "Analysis authoring", files: ["src/pages/AnalysisJobConfigPage.tsx"] },
  { tag: "J09", name: "Analysis jobs", files: ["src/pages/AnalysisJobConfigPage.tsx"] },
  { tag: "J10", name: "Findings", files: ["src/pages/Correlation/CorrelationPage.tsx", "src/pages/MaterialAnalytics/MaterialAnalyticsPages.tsx"] },
  { tag: "J11", name: "ML readiness", files: ["src/pages/MlReadiness/MlReadinessPage.tsx", "src/pages/MaterialAnalytics/MaterialAnalyticsPages.tsx"] },
  { tag: "J12", name: "ML jobs", files: ["src/pages/Admin/AdminJobsMonitorTab.tsx", "src/pages/DataIntegration/DataIntegrationLayout.tsx", "src/pages/DataIntegration/DataIntegrationRoutes.tsx"] },
  { tag: "J13", name: "Suggestions", files: ["src/pages/Phase8/SuggestionRecommendationPage.tsx"] },
  { tag: "J14", name: "Supervisor", files: ["src/pages/DataIntegration/SupervisorReportPage.tsx"] },
  { tag: "J15", name: "Assistant", files: ["src/components/assistant/AssistantDock.tsx", "src/components/assistant/AssistantChat.tsx"] },
  { tag: "UI4", name: "Plant Data Log", files: ["src/pages/DataIntegration/AlertingPage.tsx"] },
];

function read(relative) {
  const full = path.join(webRoot, relative);
  return fs.existsSync(full) ? fs.readFileSync(full, "utf8") : "";
}

function count(source, expression) {
  return (source.match(expression) || []).length;
}

function auditPage(page) {
  const existingFiles = page.files.filter((file) => fs.existsSync(path.join(webRoot, file)));
  const source = existingFiles.map(read).join("\n");
  const findings = [];
  let score = 0;

  const hasHeader = /StandardPageHeader|<h1\b/.test(source);
  score += hasHeader ? 12 : 0;
  if (!hasHeader) findings.push("Missing canonical page header or h1.");

  const standardControls = count(source, /Standard(?:Page)?(Button|Input|Select|Table|Card|Tabs|DataTable|StatGrid)|AdminPanel|MiniKpi|WidgetScriptBuilderPanel/g);
  score += Math.min(14, standardControls * 2);
  if (standardControls < 3) findings.push("Low adoption of Standard* controls.");

  const rawControls = count(source, /<(button|input|select|textarea|table)(?=[\s>])/gi);
  score += rawControls === 0 ? 14 : Math.max(0, 14 - rawControls * 2);
  if (rawControls > 0) findings.push(`${rawControls} raw control(s) remain.`);

  const inlineStyles = count(source, /style=\{\{/g);
  score += inlineStyles === 0 ? 8 : Math.max(0, 8 - inlineStyles);
  if (inlineStyles > 3) findings.push(`${inlineStyles} inline-style blocks reduce consistency.`);

  const hasAsyncStates = /DataFetchBoundary|isLoading|emptyMessage|emptyDescription|ErrorBoundary/.test(source);
  score += hasAsyncStates ? 10 : 0;
  if (!hasAsyncStates) findings.push("Loading, empty and error state ownership is not obvious.");

  const hasStructure = /StandardCard|AdminPanel|PageGrid|<section\b|<details\b|StandardTabs/.test(source);
  score += hasStructure ? 10 : 0;
  if (!hasStructure) findings.push("No clear section/card/disclosure structure found.");

  const hasIcons = /lucide-react|leadingIcon|trailingIcon|iconOnly/.test(source);
  score += hasIcons ? 6 : 3;

  const longLiteral = [...source.matchAll(/(?:subtitle|description)=\"([^\"]+)\"/g)]
    .map((match) => match[1].length)
    .some((length) => length > 240);
  score += longLiteral ? 2 : 8;
  if (longLiteral) findings.push("Header copy exceeds the concise enterprise wording limit.");

  const hasInternalCopy = /\b(M1-|M2-|phase\s*\d+|Two-Stage Import Model|fixture)\b/i.test(source.replace(/\/\*[\s\S]*?\*\//g, "").replace(/\/\/.*$/gm, ""));
  score += hasInternalCopy ? 0 : 8;
  if (hasInternalCopy) findings.push("Customer-visible source may contain internal delivery wording.");

  const hasResponsiveCss = existingFiles.some((file) => {
    const css = file.replace(/\.tsx?$/, ".css");
    return /@media/.test(read(css));
  }) || /journey-professional\.css/.test(read("src/index.css"));
  score += hasResponsiveCss ? 10 : 0;
  if (!hasResponsiveCss) findings.push("No responsive rule found for this surface.");

  return {
    tag: page.tag,
    name: page.name,
    files: existingFiles,
    score: Math.min(100, score),
    pass: existingFiles.length > 0 && score >= 80,
    findings,
    metrics: { standardControls, rawControls, inlineStyles },
  };
}

const results = pages.map(auditPage);
const globalChecks = {
  professionalStylesheetLoaded: read("src/index.css").includes("styles/journey-professional.css"),
  journeyRailHas15Stages: count(read("src/components/journey/JourneyRail.tsx"), /\{ n: \d+, label:/g) === 15,
  keyPagesUseNoRawControls: [
    "src/pages/DataIntegration/AuthorMappingPage.tsx",
    "src/pages/DataIntegration/AlertingPage.tsx",
    "src/pages/DataIntegration/SupervisorReportPage.tsx",
  ].every((file) => !/<(button|input|select|textarea|table)(?=[\s>])/i.test(read(file))),
};

const average = results.reduce((sum, row) => sum + row.score, 0) / results.length;
const payload = {
  generatedAtUtc: new Date().toISOString(),
  averageScore: Number(average.toFixed(2)),
  passedPages: results.filter((row) => row.pass).length,
  totalPages: results.length,
  globalChecks,
  pass: average >= 80 && Object.values(globalChecks).every(Boolean),
  pages: results,
};

const jsonPath = path.join(outputDir, "ui-audit.json");
const mdPath = path.join(outputDir, "ui-audit.md");
fs.writeFileSync(jsonPath, JSON.stringify(payload, null, 2));

const markdown = [
  "# PPIQ Journey UI Audit",
  "",
  `Generated: ${payload.generatedAtUtc}`,
  `Average static score: **${payload.averageScore}/100**`,
  `Passing pages: **${payload.passedPages}/${payload.totalPages}**`,
  "",
  "| Tag | Page | Score | Result | Findings |",
  "|---|---|---:|---|---|",
  ...results.map((row) => `| ${row.tag} | ${row.name} | ${row.score} | ${row.pass ? "PASS" : "FAIL"} | ${row.findings.join("; ") || "None"} |`),
  "",
  "## Global checks",
  ...Object.entries(globalChecks).map(([key, value]) => `- ${value ? "PASS" : "FAIL"}: ${key}`),
  "",
  "> Static scoring is a preflight. Runtime Playwright checks remain the authority for alignment, overflow, responsiveness and visual behavior.",
].join("\n");
fs.writeFileSync(mdPath, markdown);

console.log(markdown);
process.exit(payload.pass ? 0 : 1);
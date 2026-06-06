const fs = require("fs");
const path = require("path");
const cp = require("child_process");

const root = process.cwd();

const docsDir = path.join(root, "docs", "pack-g");
const taskClosureDir = path.join(root, "docs", "task-closure");
const toolsDir = path.join(root, "tools", "pack-g");

const auditJsonPath = path.join(docsDir, "PACK_G1_PHASE15_AUDIT.json");
const auditMdPath = path.join(docsDir, "PACK_G1_PHASE15_AUDIT.md");

const closureJsonPath = path.join(docsDir, "PACK_G_PHASE15_CLOSURE_MAP.json");
const closureMdPath = path.join(docsDir, "PACK_G_PHASE15_CLOSURE_MAP.md");

const scorecardJsonPath = path.join(taskClosureDir, "T096_T102_PHASE15_SCORECARD.json");
const scorecardMdPath = path.join(taskClosureDir, "T096_T102_PHASE15_SCORECARD.md");

const evidencePath = path.join(docsDir, "PACK_G_IMPLEMENTATION_EVIDENCE.md");
const validatorPath = path.join(toolsDir, "validate-pack-g-phase15-closure-map.cjs");
const wrapperPath = path.join(toolsDir, "Invoke-PackG-Phase15Regression.ps1");

const phase15Tasks = [
  {
    task: "T-096",
    phase: "P15",
    pack: "G",
    closeIn: "Pack G-3",
    title: "What-if / scenario simulation engine",
    track: "AI+ML",
    area: "Simulation",
    importance: "Important",
    type: "new feature",
    points: 8,
    initialScore: 0,
    status: "NOT YET STARTED",
    objective:
      "Build a deterministic what-if engine: given a finding and adjustable process parameters, simulate projected e-impact distribution from fitted association/statistical model. Projection only; never auto-apply anything to the process.",
    safetyRules: [
      "Projection only, not guaranteed saving.",
      "Deterministic fixed-seed output.",
      "Must abstain when input is outside the observed data envelope.",
      "Must reuse Analytics.Core or a canonical analytics contract.",
      "Must expose backend endpoint and simulation panel."
    ]
  },
  {
    task: "T-097",
    phase: "P15",
    pack: "G",
    closeIn: "Pack G-4",
    title: "Recommendation generator with expected €-impact",
    track: "AI+ML",
    area: "Recommendations",
    importance: "Very important",
    type: "new feature",
    points: 10,
    initialScore: 0,
    status: "NOT YET STARTED",
    objective:
      "For each statistically-supported finding, generate recommended parameter window plus expected e-impact range, confidence and provenance; require human approval before any downstream write path.",
    safetyRules: [
      "No causal claim unless proven.",
      "Every recommendation must carry evidence, confidence and provenance.",
      "Recommendations must be blocked under weak or insufficient evidence.",
      "No direct process write-back.",
      "Approval/rejection workflow must be explicit."
    ]
  },
  {
    task: "T-098",
    phase: "P15",
    pack: "G",
    closeIn: "Pack G-5",
    title: "Value-realization tracking baseline vs actual",
    track: "AI+ML",
    area: "Value-Realization",
    importance: "Very important",
    type: "new feature",
    points: 10,
    initialScore: 0,
    status: "NOT YET STARTED",
    objective:
      "Capture a pre-change baseline KPI window, measure post-change actuals versus comparable baseline window, compute realized e-savings with explicit attribution caveats, and store per-customer realized-value ledger linked to recommendation that prompted the change.",
    safetyRules: [
      "Baseline vs actual must be reproducible.",
      "Attribution caveat must be visible.",
      "Correlation is not causation.",
      "Changing seeded actuals must change the realized value.",
      "Ledger entry must link to the source recommendation."
    ]
  },
  {
    task: "T-099",
    phase: "P15",
    pack: "G",
    closeIn: "Pack G-6",
    title: "ROI / CFO value dashboard",
    track: "AI+ML",
    area: "Value-Dashboard",
    importance: "Important",
    type: "new feature",
    points: 8,
    initialScore: 0,
    status: "NOT YET STARTED",
    objective:
      "Build a buyer-facing value dashboard showing potential vs realized e-value, payback period and savings by area/finding/time, with exportable CFO-friendly evidence pack.",
    safetyRules: [
      "Separate potential from realized value.",
      "No dead buttons.",
      "Dark and light themes must remain accessible.",
      "Export must reconcile with ledger values.",
      "Dashboard must explain evidence and caveats."
    ]
  },
  {
    task: "T-100",
    phase: "P15",
    pack: "G",
    closeIn: "Pack G-7",
    title: "Cross-plant & industry benchmarking",
    track: "AI+ML",
    area: "Benchmarking",
    importance: "Important",
    type: "new feature",
    points: 8,
    initialScore: 0,
    status: "NOT YET STARTED",
    objective:
      "Add privacy-preserving benchmarking: compare tenant KPI/finding against anonymized aggregate across other tenants and configured industry reference bands, only above minimum cohort size.",
    safetyRules: [
      "No identifiable cross-tenant row exposure.",
      "Only k-anonymized aggregates above minimum cohort size.",
      "Suppress benchmarks under minimum sample size.",
      "Reference bands must be configurable.",
      "Cross-tenant access must be test-certified."
    ]
  },
  {
    task: "T-101",
    phase: "P15",
    pack: "G",
    closeIn: "Pack G-8",
    title: "Recommendation honesty & approval certification",
    track: "AI+ML",
    area: "Governance",
    importance: "Very important",
    type: "test",
    points: 6,
    initialScore: 0,
    status: "NOT YET STARTED",
    objective:
      "Adversarial certification of the advisory layer: recommendations must never assert causation, must carry confidence, evidence and extrapolation status, and must require explicit human approval before downstream action.",
    safetyRules: [
      "No causal language.",
      "No approval bypass.",
      "No write-back without approval record.",
      "Weak evidence must block recommendation.",
      "Out-of-envelope projection must abstain."
    ]
  },
  {
    task: "T-102",
    phase: "P15",
    pack: "G",
    closeIn: "Pack G-9",
    title: "Phase 15 regression",
    track: "AI+ML",
    area: "Regression",
    importance: "Very important",
    type: "test",
    points: 8,
    initialScore: 0,
    status: "NOT YET STARTED",
    objective:
      "Run full suite including dotnet test, npm build/test/e2e where available, and certification stage after advisory/value-realization features are touched.",
    safetyRules: [
      "All Phase 15 validators green.",
      "Backend build green.",
      "Frontend build green.",
      "No regression in prior gate-exit tests.",
      "Final Phase 15 scorecard bridge leaves zero below-90 tasks."
    ]
  }
];

function ensureDir(dir) {
  fs.mkdirSync(dir, { recursive: true });
}

function exists(file) {
  return fs.existsSync(file);
}

function isFile(file) {
  return exists(file) && fs.statSync(file).isFile();
}

function read(file) {
  return fs.readFileSync(file, "utf8");
}

function write(file, content) {
  ensureDir(path.dirname(file));
  fs.writeFileSync(file, content.replace(/\n/g, "\r\n"), "utf8");
  console.log("Wrote: " + rel(file));
}

function rel(file) {
  return path.relative(root, file).split(path.sep).join("/");
}

function normalize(text) {
  return text.replace(/\r\n/g, "\n");
}

function shouldSkipDir(name, fullPath) {
  const relative = rel(fullPath).toLowerCase();

  return [
    ".git",
    ".vs",
    "bin",
    "obj",
    "node_modules",
    "dist",
    "coverage"
  ].includes(name.toLowerCase()) ||
  relative.includes("/tools/_archive/") ||
  relative.includes("/.pack_a_backup/") ||
  relative.includes("/.pack_b_backup/") ||
  relative.includes("/.pack_d_backup/") ||
  relative.includes("/.pack_e_backup/") ||
  relative.includes("/.pack_f_backup/") ||
  relative.includes("/.pack_g_backup/");
}

function walk(dir) {
  if (!exists(dir)) return [];

  return fs.readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const full = path.join(dir, entry.name);

    if (entry.isDirectory()) {
      if (shouldSkipDir(entry.name, full)) return [];
      return walk(full);
    }

    return [full];
  });
}

function getTextFiles() {
  const allowed = new Set([
    ".cs",
    ".csproj",
    ".json",
    ".yml",
    ".yaml",
    ".ps1",
    ".cjs",
    ".js",
    ".ts",
    ".tsx",
    ".md",
    ".txt",
    ".sql",
    ".css"
  ]);

  return walk(root).filter((file) => {
    const base = path.basename(file).toLowerCase();
    return allowed.has(path.extname(file).toLowerCase()) || base === "dockerfile";
  });
}

function safeRead(file) {
  try {
    return normalize(read(file));
  } catch {
    return "";
  }
}

function lineCount(file) {
  if (!isFile(file)) return 0;
  return normalize(read(file)).split("\n").length;
}

function searchFiles(patterns, files) {
  const result = [];

  for (const file of files) {
    const relative = rel(file);
    const text = safeRead(file);
    const hits = [];

    for (const pattern of patterns) {
      if (pattern.test(relative) || pattern.test(text)) {
        hits.push(pattern.toString());
      }
    }

    if (hits.length) {
      result.push({
        path: relative,
        lines: lineCount(file),
        hits
      });
    }
  }

  return result.sort((a, b) => a.path.localeCompare(b.path));
}

function run(name, args, cwd = root, optional = false) {
  console.log("");
  console.log("---- " + name);

  try {
    cp.execFileSync(args[0], args.slice(1), {
      cwd,
      stdio: "inherit",
      shell: false
    });

    return { ok: true };
  } catch (error) {
    if (optional) {
      console.warn("[WARN] Optional step failed: " + name);
      console.warn(error.message || String(error));
      return { ok: false, error: error.message || String(error) };
    }

    throw error;
  }
}

function classifyBackend(files) {
  const backendFiles = files.filter((file) => rel(file).startsWith("Backend/"));

  return {
    analyticsCoreSignals: searchFiles([
      /Analytics\.Core/i,
      /Correlation/i,
      /confidence/i,
      /evidence/i,
      /provenance/i,
      /explainability/i,
      /risk/i,
      /finding/i
    ], backendFiles),

    scenarioSignals: searchFiles([
      /what[- ]?if/i,
      /scenario/i,
      /simulation/i,
      /projected/i,
      /projection/i,
      /envelope/i,
      /deterministic/i,
      /seed/i,
      /counterfactual/i
    ], backendFiles),

    recommendationSignals: searchFiles([
      /recommendation/i,
      /advisor/i,
      /advisory/i,
      /prescriptive/i,
      /parameter window/i,
      /expected.*impact/i,
      /approval/i,
      /dismiss/i
    ], backendFiles),

    valueRealizationSignals: searchFiles([
      /value[- ]?realization/i,
      /realized.*value/i,
      /baseline/i,
      /actual/i,
      /savings/i,
      /ledger/i,
      /attribution/i,
      /payback/i,
      /roi/i,
      /cfo/i
    ], backendFiles),

    benchmarkingSignals: searchFiles([
      /benchmark/i,
      /cross[- ]?plant/i,
      /cross[- ]?tenant/i,
      /industry/i,
      /reference band/i,
      /percentile/i,
      /k[- ]?anonym/i,
      /cohort/i,
      /tenant isolation/i
    ], backendFiles),

    governanceSignals: searchFiles([
      /honesty/i,
      /causation/i,
      /causal/i,
      /approval/i,
      /certification/i,
      /guardrail/i,
      /weak evidence/i,
      /abstain/i,
      /insufficient/i,
      /write[- ]?back/i
    ], backendFiles),

    testSignals: searchFiles([
      /what[- ]?if/i,
      /scenario/i,
      /recommendation/i,
      /value[- ]?realization/i,
      /benchmark/i,
      /approval/i,
      /honesty/i,
      /deterministic/i
    ], backendFiles.filter((file) => rel(file).includes("/tests/")))
  };
}

function classifyFrontend(files) {
  const frontendFiles = files.filter((file) => rel(file).startsWith("Frontend/"));

  return {
    advisoryPageSignals: searchFiles([
      /advisory/i,
      /advisor/i,
      /recommendation/i,
      /prescriptive/i,
      /what[- ]?if/i,
      /scenario/i,
      /simulation/i
    ], frontendFiles),

    valueDashboardSignals: searchFiles([
      /roi/i,
      /cfo/i,
      /value[- ]?dashboard/i,
      /realized.*value/i,
      /baseline/i,
      /actual/i,
      /savings/i,
      /payback/i
    ], frontendFiles),

    benchmarkingSignals: searchFiles([
      /benchmark/i,
      /cross[- ]?plant/i,
      /industry/i,
      /fleet/i,
      /reference band/i,
      /percentile/i
    ], frontendFiles),

    apiClientSignals: searchFiles([
      /scenario/i,
      /recommendation/i,
      /value[- ]?realization/i,
      /benchmark/i,
      /approval/i,
      /roi/i,
      /advisory/i
    ], frontendFiles.filter((file) => file.endsWith(".ts") || file.endsWith(".tsx")))
  };
}

function classifyDocs(files) {
  const docFiles = files.filter((file) => {
    const relative = rel(file).toLowerCase();
    return relative.startsWith("docs/") || relative.endsWith(".md") || relative.includes("roadmap");
  });

  return {
    phase15Docs: searchFiles([
      /phase 15/i,
      /P15/i,
      /prescriptive/i,
      /advisory/i,
      /value realization/i,
      /what[- ]?if/i,
      /recommendation/i,
      /ROI/i,
      /CFO/i,
      /benchmark/i
    ], docFiles),

    honestyDocs: searchFiles([
      /no causal/i,
      /causation/i,
      /honesty/i,
      /confidence/i,
      /evidence/i,
      /provenance/i,
      /approval/i,
      /abstain/i,
      /insufficient/i
    ], docFiles),

    regressionDocs: searchFiles([
      /regression/i,
      /validator/i,
      /certification/i,
      /gate/i,
      /scorecard/i,
      /closure/i
    ], docFiles)
  };
}

function readExistingScorecards() {
  const candidates = [
    "docs/task-closure/T001_T071_TASK_CLOSURE_SCORECARD.json",
    "docs/task-closure/T096_T102_PHASE15_SCORECARD.json",
    "docs/task-closure/T001_T132_TASK_CLOSURE_SCORECARD.json",
    "docs/task-closure/T001_T132_ROADMAP_SCORECARD.json"
  ];

  return candidates.map((relative) => {
    const absolute = path.join(root, relative);

    if (!isFile(absolute)) {
      return { path: relative, exists: false };
    }

    try {
      const json = JSON.parse(read(absolute));
      return { path: relative, exists: true, parseOk: true, sizeBytes: fs.statSync(absolute).size, keys: Object.keys(json).slice(0, 20) };
    } catch (error) {
      return { path: relative, exists: true, parseOk: false, error: error.message };
    }
  });
}

function buildClosureMap(audit) {
  const closureMap = [
    {
      task: "T-096",
      packStep: "Pack G-3",
      priority: 1,
      risk: "HIGH",
      title: "What-if / scenario simulation engine",
      reason:
        "The advisory layer must first have a deterministic and bounded projection engine. Recommendations and value dashboards depend on scenario output.",
      acceptance: [
        "Domain/API contract for scenario request and response exists.",
        "Simulation is deterministic for the same seed and inputs.",
        "Out-of-envelope input returns abstain/insufficient-support, not fake projection.",
        "Projection explicitly says projection-only and never applies process changes.",
        "Backend endpoint exists.",
        "Frontend simulation panel exists.",
        "Unit or integration test proves deterministic behavior."
      ],
      auditFindings: {
        backendScenarioSignals: audit.backend.scenarioSignals.length,
        frontendAdvisorySignals: audit.frontend.advisoryPageSignals.length
      }
    },
    {
      task: "T-097",
      packStep: "Pack G-4",
      priority: 2,
      risk: "HIGH",
      title: "Recommendation generator with expected e-impact",
      reason:
        "After scenario projection exists, the system can generate guarded recommendations with value impact, evidence, confidence and provenance.",
      acceptance: [
        "Recommendation generator contract exists.",
        "Each recommendation carries expected e-impact range.",
        "Each recommendation carries confidence, evidence and provenance.",
        "Weak evidence blocks recommendation.",
        "Recommendation requires human approval/dismissal.",
        "No direct write-back path exists.",
        "Grounding service style check blocks causal recommendation language."
      ],
      auditFindings: {
        backendRecommendationSignals: audit.backend.recommendationSignals.length,
        governanceSignals: audit.backend.governanceSignals.length
      }
    },
    {
      task: "T-098",
      packStep: "Pack G-5",
      priority: 3,
      risk: "HIGH",
      title: "Value-realization tracking baseline vs actual",
      reason:
        "Real customer value must be tracked after recommendations. This is the bridge between analytics insight and buyer-visible value proof.",
      acceptance: [
        "Baseline KPI window model exists.",
        "Post-change actual KPI window model exists.",
        "Realized e-value is computed from baseline-vs-actual delta, not hardcoded.",
        "Attribution caveat is stored and displayed.",
        "Ledger links realized value to source recommendation.",
        "Seeded before/after test proves reproducibility."
      ],
      auditFindings: {
        valueSignals: audit.backend.valueRealizationSignals.length,
        frontendValueSignals: audit.frontend.valueDashboardSignals.length
      }
    },
    {
      task: "T-099",
      packStep: "Pack G-6",
      priority: 4,
      risk: "MEDIUM",
      title: "ROI / CFO value dashboard",
      reason:
        "After value-realization ledger exists, expose it as CFO-facing dashboard and exportable value evidence.",
      acceptance: [
        "Dashboard separates potential vs realized value.",
        "Dashboard shows payback period and savings by area/finding/time.",
        "Dashboard reconciles with ledger values.",
        "Export evidence pack exists.",
        "No dead buttons.",
        "Dark/light accessibility remains acceptable."
      ],
      auditFindings: {
        frontendValueSignals: audit.frontend.valueDashboardSignals.length
      }
    },
    {
      task: "T-100",
      packStep: "Pack G-7",
      priority: 5,
      risk: "HIGH",
      title: "Cross-plant & industry benchmarking",
      reason:
        "Benchmarking adds customer value but must be privacy-preserving and cross-tenant safe. This must happen after the basic value/advisory contracts are stable.",
      acceptance: [
        "Benchmark model supports tenant/plant KPI/finding comparison.",
        "Only anonymized aggregates above minimum cohort size are returned.",
        "Below-minimum cohort is suppressed.",
        "Reference bands are configurable.",
        "Cross-tenant rows are never exposed.",
        "Test proves identifiable cross-tenant rows do not leak."
      ],
      auditFindings: {
        backendBenchmarkSignals: audit.backend.benchmarkingSignals.length,
        frontendBenchmarkSignals: audit.frontend.benchmarkingSignals.length
      }
    },
    {
      task: "T-101",
      packStep: "Pack G-8",
      priority: 6,
      risk: "HIGH",
      title: "Recommendation honesty & approval certification",
      reason:
        "Before final regression, the advisory layer must pass adversarial honesty certification: no causal claims, no unsafe write-back, no recommendation without approval path.",
      acceptance: [
        "Adversarial tests exist.",
        "Fabricated causal wording is rejected.",
        "Out-of-envelope projection causes abstain.",
        "Weak evidence blocks recommendation.",
        "Recommendation cannot reach write-back path without approval record.",
        "CI certification stage includes Phase 15 honesty gate."
      ],
      auditFindings: {
        governanceSignals: audit.backend.governanceSignals.length,
        testSignals: audit.backend.testSignals.length
      }
    },
    {
      task: "T-102",
      packStep: "Pack G-9",
      priority: 7,
      risk: "MEDIUM",
      title: "Phase 15 regression",
      reason:
        "Final Phase 15 closure must prove all validators, builds and scorecard bridges are green, with no regression in previous Pack E/F work.",
      acceptance: [
        "Pack G-1 to G-8 validators pass.",
        "Backend build passes.",
        "Frontend build passes.",
        "Phase 15 regression report exists.",
        "Phase 15 scorecard bridge marks T-096 to T-102 complete.",
        "No below-90 Phase 15 tasks remain."
      ],
      auditFindings: {
        regressionDocs: audit.docs.regressionDocs.length
      }
    }
  ];

  return {
    generatedAtUtc: new Date().toISOString(),
    marker: "PPIQ_PACK_G1_PHASE15_CLOSURE_MAP",
    phase: "P15",
    pack: "G",
    recommendedOrder: closureMap.map((item) => item.task),
    closureMap
  };
}

function writeScorecard() {
  const scorecard = {
    generatedAtUtc: new Date().toISOString(),
    marker: "PPIQ_T096_T102_PHASE15_SCORECARD",
    phase: "P15",
    pack: "G",
    scoringModel: "Pack G starts Phase 15 with explicit task rows. Bridges from Pack G-3 onward will mark tasks complete when validators pass.",
    tasks: phase15Tasks.map((task) => ({
      task: task.task,
      phase: task.phase,
      pack: task.pack,
      closeIn: task.closeIn,
      title: task.title,
      area: task.area,
      importance: task.importance,
      type: task.type,
      points: task.points,
      score: task.initialScore,
      percent: task.initialScore,
      completionPercent: task.initialScore,
      status: task.status,
      below90: task.initialScore < 90,
      objective: task.objective,
      safetyRules: task.safetyRules
    }))
  };

  write(scorecardJsonPath, JSON.stringify(scorecard, null, 2) + "\n");

  const md = [];

  md.push("# T096-T102 Phase 15 Scorecard");
  md.push("");
  md.push("Generated: " + scorecard.generatedAtUtc);
  md.push("");
  md.push("Marker: PPIQ_T096_T102_PHASE15_SCORECARD");
  md.push("");
  md.push("| Task | Score | Status | Pack Step | Title | Importance | Points |");
  md.push("|---|---:|---|---|---|---|---:|");

  for (const task of scorecard.tasks) {
    md.push("| " + task.task + " | " + task.score + "% | " + task.status + " | " + task.closeIn + " | " + task.title + " | " + task.importance + " | " + task.points + " |");
  }

  md.push("");

  write(scorecardMdPath, md.join("\n"));
}

function writeAuditMarkdown(audit, closureMap) {
  const md = [];

  md.push("# Pack G-1 Phase 15 Audit + Closure Map");
  md.push("");
  md.push("Generated: " + audit.generatedAtUtc);
  md.push("");
  md.push("Marker: PPIQ_PACK_G1_PHASE15_AUDIT_CLOSURE_MAP");
  md.push("");
  md.push("## Executive Summary");
  md.push("");
  md.push("Pack G starts Phase 15: Prescriptive-Advisory & Value Realization Engine. The objective is to move PlantProcess IQ from insight and risk visibility into guarded, buyer-visible value realization: deterministic what-if simulation, recommendations with expected e-impact, baseline-vs-actual realized value, ROI/CFO dashboard, privacy-preserving benchmarking, and honesty certification.");
  md.push("");
  md.push("The core guardrail remains strict: this phase must not create fake AI claims, causal claims without proof, or any automatic process write-back. Recommendations are advisory, evidence-bound, approval-controlled, and projection-only unless realized value is later measured.");
  md.push("");
  md.push("## Phase 15 Tasks");
  md.push("");
  md.push("| Task | Step | Importance | Points | Title |");
  md.push("|---|---|---|---:|---|");

  for (const task of phase15Tasks) {
    md.push("| " + task.task + " | " + task.closeIn + " | " + task.importance + " | " + task.points + " | " + task.title + " |");
  }

  md.push("");
  md.push("## Current Signal Inventory");
  md.push("");
  md.push("### Backend");
  md.push("");
  md.push("- Analytics/Core/evidence signals: **" + audit.backend.analyticsCoreSignals.length + "**");
  md.push("- Scenario/what-if signals: **" + audit.backend.scenarioSignals.length + "**");
  md.push("- Recommendation/advisory signals: **" + audit.backend.recommendationSignals.length + "**");
  md.push("- Value-realization/ROI signals: **" + audit.backend.valueRealizationSignals.length + "**");
  md.push("- Benchmarking signals: **" + audit.backend.benchmarkingSignals.length + "**");
  md.push("- Governance/honesty signals: **" + audit.backend.governanceSignals.length + "**");
  md.push("- Backend test signals: **" + audit.backend.testSignals.length + "**");
  md.push("");
  md.push("### Frontend");
  md.push("");
  md.push("- Advisory/page signals: **" + audit.frontend.advisoryPageSignals.length + "**");
  md.push("- Value dashboard signals: **" + audit.frontend.valueDashboardSignals.length + "**");
  md.push("- Benchmarking signals: **" + audit.frontend.benchmarkingSignals.length + "**");
  md.push("- API client signals: **" + audit.frontend.apiClientSignals.length + "**");
  md.push("");
  md.push("### Docs");
  md.push("");
  md.push("- Phase 15 docs: **" + audit.docs.phase15Docs.length + "**");
  md.push("- Honesty/provenance docs: **" + audit.docs.honestyDocs.length + "**");
  md.push("- Regression/closure docs: **" + audit.docs.regressionDocs.length + "**");
  md.push("");
  md.push("## Recommended Closure Order");
  md.push("");
  md.push("| Priority | Task | Pack Step | Risk | Reason |");
  md.push("|---:|---|---|---|---|");

  for (const item of closureMap.closureMap) {
    md.push("| " + item.priority + " | " + item.task + " | " + item.packStep + " | " + item.risk + " | " + item.reason + " |");
  }

  md.push("");
  md.push("## Next Step");
  md.push("");
  md.push("Next implementation step: **Pack G-2 — Phase 15 advisory/value domain contract**.");
  md.push("");

  write(auditMdPath, md.join("\n"));
}

function writeClosureMarkdown(closureMap) {
  const md = [];

  md.push("# Pack G Phase 15 Closure Map");
  md.push("");
  md.push("Generated: " + closureMap.generatedAtUtc);
  md.push("");
  md.push("Marker: PPIQ_PACK_G1_PHASE15_CLOSURE_MAP");
  md.push("");
  md.push("## Recommended Execution Order");
  md.push("");
  md.push("1. Pack G-2 — Phase 15 advisory/value domain contract.");
  md.push("2. Pack G-3 / T-096 — Deterministic what-if scenario engine.");
  md.push("3. Pack G-4 / T-097 — Recommendation generator with expected e-impact.");
  md.push("4. Pack G-5 / T-098 — Value-realization tracking baseline vs actual.");
  md.push("5. Pack G-6 / T-099 — ROI/CFO value dashboard.");
  md.push("6. Pack G-7 / T-100 — Privacy-preserving benchmarking.");
  md.push("7. Pack G-8 / T-101 — Recommendation honesty and approval certification.");
  md.push("8. Pack G-9 / T-102 — Phase 15 regression and final scorecard bridge.");
  md.push("");
  md.push("## Acceptance by Task");
  md.push("");

  for (const item of closureMap.closureMap) {
    md.push("### " + item.task + " — " + item.title);
    md.push("");
    md.push("- Pack step: " + item.packStep);
    md.push("- Priority: " + item.priority);
    md.push("- Risk: " + item.risk);
    md.push("- Reason: " + item.reason);
    md.push("");
    md.push("Acceptance:");
    md.push("");

    for (const rule of item.acceptance) {
      md.push("- " + rule);
    }

    md.push("");
  }

  write(closureMdPath, md.join("\n"));
}

function writeValidator() {
  const lines = [];

  lines.push('const fs = require("fs");');
  lines.push('const path = require("path");');
  lines.push('');
  lines.push('const root = process.cwd();');
  lines.push('');
  lines.push('const requiredFiles = [');
  lines.push('  "docs/pack-g/PACK_G1_PHASE15_AUDIT.json",');
  lines.push('  "docs/pack-g/PACK_G1_PHASE15_AUDIT.md",');
  lines.push('  "docs/pack-g/PACK_G_PHASE15_CLOSURE_MAP.json",');
  lines.push('  "docs/pack-g/PACK_G_PHASE15_CLOSURE_MAP.md",');
  lines.push('  "docs/task-closure/T096_T102_PHASE15_SCORECARD.json",');
  lines.push('  "docs/task-closure/T096_T102_PHASE15_SCORECARD.md",');
  lines.push('  "docs/pack-g/PACK_G_IMPLEMENTATION_EVIDENCE.md"');
  lines.push('];');
  lines.push('');
  lines.push('function isFile(relativePath) {');
  lines.push('  const absolute = path.join(root, relativePath);');
  lines.push('  return fs.existsSync(absolute) && fs.statSync(absolute).isFile();');
  lines.push('}');
  lines.push('');
  lines.push('function read(relativePath) {');
  lines.push('  return fs.readFileSync(path.join(root, relativePath), "utf8");');
  lines.push('}');
  lines.push('');
  lines.push('const failures = [];');
  lines.push('');
  lines.push('for (const file of requiredFiles) {');
  lines.push('  if (!isFile(file)) failures.push({ file, reason: "missing" });');
  lines.push('}');
  lines.push('');
  lines.push('if (isFile("docs/pack-g/PACK_G_PHASE15_CLOSURE_MAP.json")) {');
  lines.push('  const map = JSON.parse(read("docs/pack-g/PACK_G_PHASE15_CLOSURE_MAP.json"));');
  lines.push('  const tasks = new Set((map.closureMap || []).map((item) => item.task));');
  lines.push('  for (const task of ["T-096", "T-097", "T-098", "T-099", "T-100", "T-101", "T-102"]) {');
  lines.push('    if (!tasks.has(task)) failures.push({ task, reason: "missing-from-closure-map" });');
  lines.push('  }');
  lines.push('}');
  lines.push('');
  lines.push('if (isFile("docs/task-closure/T096_T102_PHASE15_SCORECARD.json")) {');
  lines.push('  const scorecard = JSON.parse(read("docs/task-closure/T096_T102_PHASE15_SCORECARD.json"));');
  lines.push('  const rows = Array.isArray(scorecard.tasks) ? scorecard.tasks : [];');
  lines.push('  const tasks = new Set(rows.map((item) => item.task));');
  lines.push('  for (const task of ["T-096", "T-097", "T-098", "T-099", "T-100", "T-101", "T-102"]) {');
  lines.push('    if (!tasks.has(task)) failures.push({ task, reason: "missing-from-phase15-scorecard" });');
  lines.push('  }');
  lines.push('}');
  lines.push('');
  lines.push('if (isFile("docs/pack-g/PACK_G_IMPLEMENTATION_EVIDENCE.md")) {');
  lines.push('  const evidence = read("docs/pack-g/PACK_G_IMPLEMENTATION_EVIDENCE.md");');
  lines.push('  if (!evidence.includes("PPIQ_PACK_G1_PHASE15_AUDIT_CLOSURE_MAP")) failures.push({ reason: "evidence-marker-missing" });');
  lines.push('}');
  lines.push('');
  lines.push('if (failures.length) {');
  lines.push('  console.error("Pack G-1 Phase 15 closure-map validation failed.");');
  lines.push('  console.error(JSON.stringify(failures, null, 2));');
  lines.push('  process.exit(1);');
  lines.push('}');
  lines.push('');
  lines.push('console.log("Pack G-1 Phase 15 audit/closure-map validation passed.");');

  write(validatorPath, lines.join("\n") + "\n");
}

function writeWrapper() {
  const content = [
    "[CmdletBinding()]",
    "param(",
    "    [string]$ProjectRoot = (Resolve-Path \".\").Path",
    ")",
    "",
    "$ErrorActionPreference = \"Stop\"",
    "",
    "function Run-Step([string]$Name, [scriptblock]$Block) {",
    "    Write-Host \"\"",
    "    Write-Host \"---- $Name\" -ForegroundColor Cyan",
    "    & $Block",
    "    if ($LASTEXITCODE -ne 0) { throw \"$Name failed with exit code $LASTEXITCODE\" }",
    "}",
    "",
    "Push-Location $ProjectRoot",
    "try {",
    "    Run-Step \"Pack G-1 Phase 15 closure-map validation\" {",
    "        node \".\\tools\\pack-g\\validate-pack-g-phase15-closure-map.cjs\"",
    "    }",
    "",
    "    Write-Host \"\"",
    "    Write-Host \"Pack G Phase 15 regression wrapper initialized.\" -ForegroundColor Green",
    "}",
    "finally { Pop-Location }",
    ""
  ].join("\n");

  write(wrapperPath, content);
}

function writeEvidence() {
  let evidence = isFile(evidencePath)
    ? normalize(read(evidencePath))
    : "# PlantProcess IQ Pack G Evidence\n";

  if (!evidence.includes("PPIQ_PACK_G1_PHASE15_AUDIT_CLOSURE_MAP")) {
    evidence += [
      "",
      "## Pack G-1 Phase 15 audit and closure map",
      "",
      "- Marker: PPIQ_PACK_G1_PHASE15_AUDIT_CLOSURE_MAP.",
      "- Started Phase 15: Prescriptive-Advisory & Value Realization Engine.",
      "- Audited backend, frontend and documentation signals for T-096 to T-102.",
      "- Created Phase 15 task scorecard seed.",
      "- Created closure map and recommended implementation order.",
      "- Added Pack G-1 validation wrapper.",
      "",
      "Generated artifacts:",
      "",
      "- docs/pack-g/PACK_G1_PHASE15_AUDIT.md",
      "- docs/pack-g/PACK_G1_PHASE15_AUDIT.json",
      "- docs/pack-g/PACK_G_PHASE15_CLOSURE_MAP.md",
      "- docs/pack-g/PACK_G_PHASE15_CLOSURE_MAP.json",
      "- docs/task-closure/T096_T102_PHASE15_SCORECARD.md",
      "- docs/task-closure/T096_T102_PHASE15_SCORECARD.json",
      "- tools/pack-g/validate-pack-g-phase15-closure-map.cjs",
      "- tools/pack-g/Invoke-PackG-Phase15Regression.ps1",
      ""
    ].join("\n");

    write(evidencePath, evidence);
  }
}

console.log("=================================================================================================");
console.log("PlantProcess IQ Pack G-1 — Phase 15 advisory/value audit + closure map");
console.log("=================================================================================================");

ensureDir(docsDir);
ensureDir(taskClosureDir);
ensureDir(toolsDir);

const files = getTextFiles();

const audit = {
  generatedAtUtc: new Date().toISOString(),
  marker: "PPIQ_PACK_G1_PHASE15_AUDIT_CLOSURE_MAP",
  phase: "P15",
  pack: "G",
  tasks: phase15Tasks,
  scorecards: readExistingScorecards(),
  backend: classifyBackend(files),
  frontend: classifyFrontend(files),
  docs: classifyDocs(files)
};

const closureMap = buildClosureMap(audit);

write(auditJsonPath, JSON.stringify(audit, null, 2) + "\n");
write(closureJsonPath, JSON.stringify(closureMap, null, 2) + "\n");

writeAuditMarkdown(audit, closureMap);
writeClosureMarkdown(closureMap);
writeScorecard();
writeValidator();
writeWrapper();
writeEvidence();

console.log("");
console.log("Pack G-1 audit summary:");
console.log(" - Backend analytics/evidence signals     : " + audit.backend.analyticsCoreSignals.length);
console.log(" - Backend scenario/what-if signals       : " + audit.backend.scenarioSignals.length);
console.log(" - Backend recommendation signals         : " + audit.backend.recommendationSignals.length);
console.log(" - Backend value-realization signals      : " + audit.backend.valueRealizationSignals.length);
console.log(" - Backend benchmarking signals           : " + audit.backend.benchmarkingSignals.length);
console.log(" - Backend governance/honesty signals     : " + audit.backend.governanceSignals.length);
console.log(" - Backend test signals                   : " + audit.backend.testSignals.length);
console.log(" - Frontend advisory/page signals         : " + audit.frontend.advisoryPageSignals.length);
console.log(" - Frontend value dashboard signals       : " + audit.frontend.valueDashboardSignals.length);
console.log(" - Frontend benchmarking signals          : " + audit.frontend.benchmarkingSignals.length);
console.log(" - Frontend API client signals            : " + audit.frontend.apiClientSignals.length);
console.log(" - Phase 15 docs signals                  : " + audit.docs.phase15Docs.length);
console.log(" - Honesty/provenance docs signals        : " + audit.docs.honestyDocs.length);
console.log(" - Regression/closure docs signals        : " + audit.docs.regressionDocs.length);

console.log("");
console.log("Recommended Phase 15 closure order:");
for (const item of closureMap.closureMap) {
  console.log(" - " + item.packStep + " / " + item.task + " / " + item.title);
}

run("node --check Pack G-1 validator", ["node", "--check", "tools/pack-g/validate-pack-g-phase15-closure-map.cjs"]);
run("Pack G-1 Phase 15 closure-map validation", ["node", "tools/pack-g/validate-pack-g-phase15-closure-map.cjs"]);

console.log("");
console.log("=================================================================================================");
console.log("Pack G-1 completed.");
console.log("Audit      : docs/pack-g/PACK_G1_PHASE15_AUDIT.md");
console.log("Closure map: docs/pack-g/PACK_G_PHASE15_CLOSURE_MAP.md");
console.log("Scorecard  : docs/task-closure/T096_T102_PHASE15_SCORECARD.md");
console.log("Next       : Pack G-2 — Phase 15 advisory/value domain contract.");
console.log("=================================================================================================");
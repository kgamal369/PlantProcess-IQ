const fs = require("fs");
const path = require("path");

const root = process.cwd();
const out = path.join(root, "docs", "ci", "gate-report.json");

fs.mkdirSync(path.dirname(out), { recursive: true });

const report = {
  generatedAtUtc: new Date().toISOString(),
  marker: "PPIQ_PACK_A3_GATE_REPORT",
  stage: "Gate-exit certification",
  task: "T-028",
  gates: [
    {
      key: "taskClosure",
      label: "T001-T071 task closure gate",
      command: "node tools/task-closure/validate-t001-t071-task-closure.cjs"
    },
    {
      key: "routeContract",
      label: "Pack D route-contract snapshot",
      command: "node tools/pack-d/validate-pack-d-route-contract-snapshot.cjs"
    },
    {
      key: "packB",
      label: "Pack B P05 closure",
      command: "node tools/pack-b/validate-pack-b-p05-closure.cjs"
    },
    {
      key: "packD",
      label: "Pack D backend thinness",
      command: "node tools/pack-d/validate-pack-d-backend-thinness.cjs"
    },
    {
      key: "phase56",
      label: "Phase 5/6 source validation",
      command: "node tools/phase56/validate-phase56.cjs"
    }
  ]
};

fs.writeFileSync(out, JSON.stringify(report, null, 2) + "\n", "utf8");
console.log("Wrote " + path.relative(root, out).split(path.sep).join("/"));

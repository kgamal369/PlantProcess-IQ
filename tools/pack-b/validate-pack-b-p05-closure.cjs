const fs = require("fs");
const path = require("path");
const cp = require("child_process");

const root = process.cwd();

function exists(file) { return fs.existsSync(file); }
function isFile(file) { return exists(file) && fs.statSync(file).isFile(); }
function read(file) { return fs.readFileSync(file, "utf8"); }
function lines(file) { return isFile(file) ? read(file).replace(/\r\n/g, "\n").split("\n").length : 0; }

function command(name, args) {
  console.log("");
  console.log("---- " + name);
  cp.execFileSync(args[0], args.slice(1), { cwd: root, stdio: "inherit", shell: false });
}

const limits = [
  {
    task: "T-036",
    path: "Frontend/PlantProcess.Web/src/components/dashboard/widget-builder/WidgetBuilderWizard.implementation.tsx",
    max: 400
  },
  {
    task: "T-036",
    path: "Frontend/PlantProcess.Web/src/components/dashboard/widget-builder/WidgetBuilderWizardContent.implementation.tsx",
    max: 400
  },
  {
    task: "T-037",
    path: "Frontend/PlantProcess.Web/src/api/productCoreApiClient.implementation.ts",
    max: 450
  },
  {
    task: "T-037",
    path: "Frontend/PlantProcess.Web/src/pages/Admin/AdminDbConfigurationTab.implementation.tsx",
    max: 450
  },
  {
    task: "T-037",
    path: "Frontend/PlantProcess.Web/src/pages/MaterialAnalytics/MaterialAnalyticsPages.implementation.tsx",
    max: 450
  },
  {
    task: "T-038",
    path: "Frontend/PlantProcess.Web/src/styles/phase56/phase56-migrated-legacy.css",
    max: 120
  }
];

const failures = [];

for (const limit of limits) {
  const absolute = path.join(root, limit.path);
  const count = lines(absolute);

  if (!isFile(absolute)) {
    failures.push({ ...limit, actual: 0, reason: "missing" });
    continue;
  }

  if (count > limit.max) {
    failures.push({ ...limit, actual: count, reason: "too-large" });
  }
}

const chunkDir = path.join(root, "Frontend/PlantProcess.Web/src/styles/phase56/legacy-chunks");
if (exists(chunkDir)) {
  for (const entry of fs.readdirSync(chunkDir)) {
    if (!entry.endsWith(".css")) continue;
    const file = path.join(chunkDir, entry);
    const count = lines(file);
    if (count > 450) {
      failures.push({
        task: "T-038",
        path: path.relative(root, file).split(path.sep).join("/"),
        max: 450,
        actual: count,
        reason: "legacy-css-chunk-too-large"
      });
    }
  }
}

command("Pack B brand-token gate", ["node", "tools/pack-b/validate-raw-brand-tokens.cjs"]);

if (!isFile(path.join(root, "tools/task-closure/Invoke-Frontend-Regression.ps1"))) {
  failures.push({
    task: "T-040",
    path: "tools/task-closure/Invoke-Frontend-Regression.ps1",
    max: 1,
    actual: 0,
    reason: "missing regression wrapper"
  });
}

if (failures.length) {
  console.error("");
  console.error("Pack B P05 closure gate failed. Remaining non-DONE evidence:");
  console.error(JSON.stringify(failures, null, 2));
  process.exit(1);
}

console.log("Pack B P05 closure gate passed.");

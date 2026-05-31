const fs = require("node:fs");

const file = "Frontend/PlantProcess.Web/package.json";
const pkg = JSON.parse(fs.readFileSync(file, "utf8"));

pkg.scripts = pkg.scripts || {};

if (!pkg.scripts["test:run"]) {
  pkg.scripts["test:run"] = "vitest run";
  console.log("Added frontend script: test:run = vitest run");
} else {
  console.log("Frontend script test:run already exists:", pkg.scripts["test:run"]);
}

fs.writeFileSync(file, JSON.stringify(pkg, null, 2) + "\n", "utf8");

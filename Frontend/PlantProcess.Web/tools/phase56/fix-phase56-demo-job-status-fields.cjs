const fs = require("node:fs");

const file = "src/pages/Phase56/Phase56Pages.tsx";
let text = fs.readFileSync(file, "utf8");

text = text
  .replaceAll("job.lastRunCompletedAtUtc", "job.lastRunFinishedAtUtc")
  .replaceAll(
    'String(job.lastRunMessage ?? "Operation did not complete.")',
    'String(job.operationalRole ?? "Operation did not complete.")'
  );

fs.writeFileSync(file, text, "utf8");

console.log("Fixed DemoJobStatus field names: lastRunFinishedAtUtc + operationalRole.");

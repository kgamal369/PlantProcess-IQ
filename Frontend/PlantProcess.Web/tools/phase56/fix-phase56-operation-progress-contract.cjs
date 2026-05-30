const fs = require("node:fs");

const file = "src/pages/Phase56/Phase56Pages.tsx";
let text = fs.readFileSync(file, "utf8");

const before = /const progressRows = lifecycle\.data\.jobChain\.jobs\.map\(\(job\) => \(\{[\s\S]*?\}\)\);/m;

const after = `const progressRows = lifecycle.data.jobChain.jobs.map((job, index) => ({
    id: String(job.jobId ?? "demo-operation-" + index),
    operationCode: String(job.jobCode ?? job.jobName ?? "DEMO_OPERATION_" + index)
      .replace(/[^a-zA-Z0-9_]/g, "_")
      .toUpperCase(),
    operationType: String(job.jobType ?? "DemoLifecycle"),
    operationName: String(job.jobName ?? "Demo lifecycle operation"),
    status: String(job.lastRunStatus ?? "Tracked"),
    percentComplete: job.lastRunStatus === "Succeeded" ? 100 : job.lastRunStatus === "Running" ? 65 : 25,
    currentStep: String(job.jobType ?? "Workflow"),
    totalSteps: 4,
    completedSteps: job.lastRunStatus === "Succeeded" ? 4 : job.lastRunStatus === "Running" ? 2 : 1,
    message: String(job.operationalRole ?? "Demo lifecycle operation is tracked."),
    startedAtUtc: String(job.lastRunStartedAtUtc ?? lifecycle.data.generatedAtUtc ?? new Date().toISOString()),
    completedAtUtc: job.lastRunCompletedAtUtc ?? null,
    failedAtUtc: job.lastRunStatus === "Failed" ? String(job.lastRunCompletedAtUtc ?? lifecycle.data.generatedAtUtc ?? new Date().toISOString()) : null,
    failureReason: job.lastRunStatus === "Failed" ? String(job.lastRunMessage ?? "Operation did not complete.") : null,
    correlationId: null,
    requestedBy: "PlantProcess IQ Demo",
    metadataJson: JSON.stringify({
      source: "Phase56DemoLifecyclePage",
      jobId: job.jobId ?? null,
      jobCode: job.jobCode ?? null,
      jobType: job.jobType ?? null,
      generatedAtUtc: lifecycle.data.generatedAtUtc,
    }),
  }));`;

if (!before.test(text)) {
  throw new Error("Could not find progressRows block in Phase56Pages.tsx");
}

text = text.replace(before, after);

fs.writeFileSync(file, text, "utf8");

console.log("Fixed Phase56 progressRows to satisfy OperationProgressRow contract.");

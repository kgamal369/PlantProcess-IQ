const fs = require("node:fs");
const path = require("node:path");
const os = require("node:os");

const root = process.cwd();

function p(file) {
  return path.join(root, file.split("/").join(path.sep));
}

function read(file) {
  return fs.existsSync(p(file)) ? fs.readFileSync(p(file), "utf8") : "";
}

function write(file, text) {
  fs.mkdirSync(path.dirname(p(file)), { recursive: true });
  fs.writeFileSync(p(file), text, "utf8");
  console.log("Wrote " + file);
}

function patch(file, patcher) {
  const before = read(file);
  if (!before) {
    console.log("Skipped missing " + file);
    return;
  }

  const after = patcher(before);

  if (after !== before) {
    write(file, after);
  } else {
    console.log("No change " + file);
  }
}

// ============================================================
// 1. Fix C# invalid char literals in generated SQL interpolation.
//    Converts {'Started'} / {'override'} / {'Failed'} etc.
//    into {"Started"} / {"override"} / {"Failed"}.
// ============================================================

for (const file of [
  "Backend/PlantProcess.Api/Endpoints/Admin/LicenseAdminEndpoints.cs",
  "Backend/PlantProcess.Api/Endpoints/Demo/DemoLifecycleEndpoints.cs",
]) {
  patch(file, (text) => {
    return text.replace(/\{'([^']{2,})'\}/g, (_, value) => `{"${value}"}`);
  });
}

// ============================================================
// 2. Fix DemoLifecycleEndpoints.ToAuditJson() broken raw string.
//    Replace it with System.Text.Json serializer.
// ============================================================

patch("Backend/PlantProcess.Api/Endpoints/Demo/DemoLifecycleEndpoints.cs", (text) => {
  let output = text;

  if (!output.includes("using System.Text.Json;")) {
    output = output.replace(
      "using System.Collections.Concurrent;",
      "using System.Collections.Concurrent;\nusing System.Text.Json;"
    );
  }

  const methodPattern =
    /public string ToAuditJson\(\)\s*\{[\s\S]*?\n\s*\}\s*\n\s*\}\s*\n\s*private sealed class DemoResetStepDto/m;

  if (!methodPattern.test(output)) {
    console.log("Could not find old ToAuditJson block with strict pattern; trying fallback.");
    return output;
  }

  output = output.replace(
    methodPattern,
    `public string ToAuditJson()
        {
            return JsonSerializer.Serialize(new
            {
                jobId = JobId,
                scope = Scope,
                operatorName = OperatorName,
                status = Status,
                percentComplete = PercentComplete,
                startedAtUtc = StartedAtUtc,
                completedAtUtc = CompletedAtUtc,
                failureReason = FailureReason,
                steps = Steps.Select(step => new
                {
                    step.Code,
                    step.Label,
                    step.Status,
                    step.PercentComplete,
                    step.ExceptionDetail
                }).ToArray()
            });
        }
    }

    private sealed class DemoResetStepDto`
  );

  return output;
});

// ============================================================
// 3. Fix package.json literal trailing \\n after final JSON object.
// ============================================================

const packageFile = "Frontend/PlantProcess.Web/package.json";
let packageText = read(packageFile)
  .replace(/^\uFEFF/, "")
  .trimEnd();

while (/\\n\s*$/.test(packageText)) {
  packageText = packageText.replace(/\\n\s*$/, "").trimEnd();
}

const pkg = JSON.parse(packageText);

pkg.scripts = pkg.scripts || {};
pkg.scripts["phase78:acceptance"] = "node ../../tools/phase78/validate-phase7-phase8-acceptance.cjs";
pkg.scripts["validate:phase7-phase8:strict"] = "npm run build && npm run lint && npm run phase78:acceptance";
pkg.scripts["test:phase78:e2e"] = "playwright test e2e/phase78-workflow-widget.spec.ts --project=chromium";

write(packageFile, JSON.stringify(pkg, null, 2) + os.EOL);

// ============================================================
// 4. Patch the Phase 7/8 generator so rerunning it later will not
//    break package.json again.
// ============================================================

patch("tools/phase78/apply-phase7-phase8-full-implementation.cjs", (text) => {
  return text
    .replaceAll('JSON.stringify(pkg, null, 2) + "\\\\n"', 'JSON.stringify(pkg, null, 2) + "\\n"')
    .replaceAll('JSON.stringify(pkg, null, 2) + "\\\\\\\\n"', 'JSON.stringify(pkg, null, 2) + "\\n"');
});

// ============================================================
// 5. Patch the acceptance validator root detection.
//    It is executed from Frontend/PlantProcess.Web, but checks
//    repo-root paths like Frontend/PlantProcess.Web/src/...
// ============================================================

patch("tools/phase78/validate-phase7-phase8-acceptance.cjs", (text) => {
  let output = text;

  output = output.replace(
    `const root = process.cwd();`,
    `const cwd = process.cwd();
const root = fs.existsSync(path.join(cwd, "Backend"))
  ? cwd
  : path.resolve(cwd, "..", "..");`
  );

  return output;
});

console.log("");
console.log("Phase 7/8 blocker patch applied.");
console.log("");
console.log("Next commands:");
console.log("  cd Backend");
console.log("  dotnet build .\\\\PlantProcessIQ.sln");
console.log("  cd ..\\\\Frontend\\\\PlantProcess.Web");
console.log("  npm run validate:phase7-phase8:strict");

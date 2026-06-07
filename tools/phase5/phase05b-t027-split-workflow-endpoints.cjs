const fs = require("fs");
const path = require("path");
const cp = require("child_process");

const root = process.cwd();

function p(relativePath) {
  return path.join(root, relativePath.replace(/\//g, path.sep));
}

function ensureDir(dir) {
  fs.mkdirSync(dir, { recursive: true });
}

function exists(relativePath) {
  return fs.existsSync(p(relativePath));
}

function read(relativePath) {
  return fs.readFileSync(p(relativePath), "utf8");
}

function write(relativePath, content) {
  const target = p(relativePath);
  ensureDir(path.dirname(target));
  fs.writeFileSync(target, content.replace(/\n/g, "\r\n"), "utf8");
  console.log("Wrote: " + relativePath);
}

function run(name, command, args) {
  console.log("");
  console.log("---- " + name);
  cp.execFileSync(command, args, {
    cwd: root,
    stdio: "inherit",
    shell: false
  });
}

function lineCount(text) {
  return text.replace(/\r\n/g, "\n").split("\n").length;
}

function sanitizeName(name) {
  return name
    .replace(/[^A-Za-z0-9_]+/g, "_")
    .replace(/^_+|_+$/g, "")
    .slice(0, 80) || "Member";
}

function findNamespace(source) {
  const match = /namespace\s+([A-Za-z0-9_.]+)\s*;/.exec(source);
  if (!match) throw new Error("Cannot locate namespace.");
  return match[1];
}

function originalUsings(source) {
  const ns = source.indexOf("namespace ");
  if (ns < 0) throw new Error("Cannot locate namespace.");
  return source.slice(0, ns).trim();
}

function findClassBlock(source, className) {
  const regex = new RegExp("public\\s+static\\s+(?:partial\\s+)?class\\s+" + className + "\\b");
  const match = regex.exec(source);
  if (!match) throw new Error("Cannot locate class: " + className);

  const braceStart = source.indexOf("{", match.index);
  if (braceStart < 0) throw new Error("Cannot locate class opening brace: " + className);

  let depth = 0;
  let inString = false;
  let inVerbatim = false;
  let inChar = false;
  let inLineComment = false;
  let inBlockComment = false;

  for (let i = braceStart; i < source.length; i++) {
    const ch = source[i];
    const next = source[i + 1];

    if (inLineComment) {
      if (ch === "\n") inLineComment = false;
      continue;
    }

    if (inBlockComment) {
      if (ch === "*" && next === "/") {
        inBlockComment = false;
        i++;
      }
      continue;
    }

    if (inString) {
      if (inVerbatim) {
        if (ch === '"' && next === '"') {
          i++;
          continue;
        }
        if (ch === '"') {
          inString = false;
          inVerbatim = false;
        }
        continue;
      }

      if (ch === "\\") {
        i++;
        continue;
      }

      if (ch === '"') inString = false;
      continue;
    }

    if (inChar) {
      if (ch === "\\") {
        i++;
        continue;
      }

      if (ch === "'") inChar = false;
      continue;
    }

    if (ch === "/" && next === "/") {
      inLineComment = true;
      i++;
      continue;
    }

    if (ch === "/" && next === "*") {
      inBlockComment = true;
      i++;
      continue;
    }

    if (ch === "@" && next === '"') {
      inString = true;
      inVerbatim = true;
      i++;
      continue;
    }

    if (ch === '"') {
      inString = true;
      inVerbatim = false;
      continue;
    }

    if (ch === "'") {
      inChar = true;
      continue;
    }

    if (ch === "{") depth++;
    if (ch === "}") {
      depth--;
      if (depth === 0) {
        return {
          classStart: match.index,
          bodyStart: braceStart + 1,
          bodyEnd: i,
          body: source.slice(braceStart + 1, i)
        };
      }
    }
  }

  throw new Error("Cannot locate class closing brace: " + className);
}

function skipTrivia(text, index) {
  let i = index;

  while (i < text.length) {
    while (i < text.length && /\s/.test(text[i])) i++;

    if (text[i] === "/" && text[i + 1] === "/") {
      i += 2;
      while (i < text.length && text[i] !== "\n") i++;
      continue;
    }

    if (text[i] === "/" && text[i + 1] === "*") {
      i += 2;
      while (i < text.length && !(text[i] === "*" && text[i + 1] === "/")) i++;
      i += 2;
      continue;
    }

    break;
  }

  return i;
}

function findMemberEnd(text, start) {
  let parenDepth = 0;
  let bracketDepth = 0;
  let braceDepth = 0;
  let bodyBraceStart = -1;
  let inString = false;
  let inVerbatim = false;
  let inChar = false;
  let inLineComment = false;
  let inBlockComment = false;

  for (let i = start; i < text.length; i++) {
    const ch = text[i];
    const next = text[i + 1];

    if (inLineComment) {
      if (ch === "\n") inLineComment = false;
      continue;
    }

    if (inBlockComment) {
      if (ch === "*" && next === "/") {
        inBlockComment = false;
        i++;
      }
      continue;
    }

    if (inString) {
      if (inVerbatim) {
        if (ch === '"' && next === '"') {
          i++;
          continue;
        }
        if (ch === '"') {
          inString = false;
          inVerbatim = false;
        }
        continue;
      }

      if (ch === "\\") {
        i++;
        continue;
      }

      if (ch === '"') inString = false;
      continue;
    }

    if (inChar) {
      if (ch === "\\") {
        i++;
        continue;
      }

      if (ch === "'") inChar = false;
      continue;
    }

    if (ch === "/" && next === "/") {
      inLineComment = true;
      i++;
      continue;
    }

    if (ch === "/" && next === "*") {
      inBlockComment = true;
      i++;
      continue;
    }

    if (ch === "@" && next === '"') {
      inString = true;
      inVerbatim = true;
      i++;
      continue;
    }

    if (ch === '"') {
      inString = true;
      inVerbatim = false;
      continue;
    }

    if (ch === "'") {
      inChar = true;
      continue;
    }

    if (braceDepth === 0) {
      if (ch === "(") parenDepth++;
      else if (ch === ")") parenDepth = Math.max(0, parenDepth - 1);
      else if (ch === "[") bracketDepth++;
      else if (ch === "]") bracketDepth = Math.max(0, bracketDepth - 1);
    }

    if (ch === "{" && parenDepth === 0 && bracketDepth === 0) {
      if (braceDepth === 0 && bodyBraceStart < 0) {
        const header = text.slice(start, i);
        const looksLikeInitializer = header.includes("=") && !/\b(namespace|class|record|struct|interface|enum)\b/.test(header);
        if (!looksLikeInitializer) bodyBraceStart = i;
      }

      braceDepth++;
      continue;
    }

    if (ch === "}") {
      if (braceDepth > 0) braceDepth--;

      if (braceDepth === 0 && bodyBraceStart >= 0) {
        let end = i + 1;

        while (end < text.length && /\s/.test(text[end])) end++;
        if (text[end] === ";") end++;

        return end;
      }

      continue;
    }

    if (ch === ";" && parenDepth === 0 && bracketDepth === 0 && braceDepth === 0) {
      return i + 1;
    }
  }

  return text.length;
}

function extractMembers(classBody) {
  const members = [];
  let i = 0;

  while (i < classBody.length) {
    i = skipTrivia(classBody, i);
    if (i >= classBody.length) break;

    const end = findMemberEnd(classBody, i);
    const text = classBody.slice(i, end).trim();

    if (text.length > 0) members.push(text);
    i = end;
  }

  return members;
}

function memberName(memberText, fallbackIndex) {
  const compact = memberText.replace(/\r\n/g, "\n");

  const methodMatch = /(?:public|private|internal)\s+static\s+(?:async\s+)?[\s\S]*?\s+([A-Za-z_][A-Za-z0-9_]*)\s*\(/.exec(compact);
  if (methodMatch) return methodMatch[1];

  const recordMatch = /\brecord\s+([A-Za-z_][A-Za-z0-9_]*)\b/.exec(compact);
  if (recordMatch) return recordMatch[1];

  const fieldMatch = /([A-Za-z_][A-Za-z0-9_]*)\s*(?:=|;)/.exec(compact);
  if (fieldMatch) return fieldMatch[1];

  return "Member" + String(fallbackIndex).padStart(3, "0");
}

function classifyMember(name, memberText) {
  if (name.startsWith("Map")) return "Route";

  if (/\brecord\b/.test(memberText)) return "Contracts";

  if (/^\s*(private|public|internal)\s+static\s+readonly\b/m.test(memberText) ||
      /^\s*(private|public|internal)\s+const\b/m.test(memberText)) {
    return "Fields";
  }

  if (/Async\s*\(/.test(memberText)) return "Handlers";

  if (/IResult\s+/.test(memberText) || /Results\./.test(memberText)) return "Handlers";

  return "Helpers";
}

function partialFile(usings, namespaceName, className, marker, members) {
  return [
    usings,
    "",
    "namespace " + namespaceName + ";",
    "",
    "// " + marker,
    "public static partial class " + className,
    "{",
    members.join("\n\n"),
    "}",
    ""
  ].join("\n");
}

function splitEndpoint(options) {
  const className = options.className;
  const routePath = options.routePath;
  const runtimePath = options.runtimePath;
  const markerBase = options.markerBase;

  const runtimeExists = exists(runtimePath);
  const routeExists = exists(routePath);

  if (!runtimeExists && !routeExists) {
    throw new Error("Neither route nor runtime source exists for " + className);
  }

  const sourcePath = runtimeExists && read(runtimePath).includes("class " + className)
    ? runtimePath
    : routePath;

  const source = read(sourcePath);

  if (!source.includes("class " + className)) {
    throw new Error("Source does not contain expected class " + className + ": " + sourcePath);
  }

  const backupDir = ".phase5_backup/t027_workflow_split_" +
    new Date().toISOString().replace(/[-:]/g, "").replace(/\..+/, "").replace("T", "_") +
    "/" + className;

  ensureDir(p(backupDir));
  fs.copyFileSync(p(sourcePath), p(backupDir + "/" + path.basename(sourcePath) + ".before"));

  if (routeExists) {
    fs.copyFileSync(p(routePath), p(backupDir + "/" + path.basename(routePath) + ".before"));
  }

  if (runtimeExists) {
    fs.copyFileSync(p(runtimePath), p(backupDir + "/" + path.basename(runtimePath) + ".before"));
  }

  const namespaceName = findNamespace(source);
  const usings = originalUsings(source);
  const classBlock = findClassBlock(source, className);
  const members = extractMembers(classBlock.body);

  if (members.length < 5) {
    throw new Error("Suspiciously low member count for " + className + ": " + members.length);
  }

  const routeMembers = [];
  const buckets = {
    Fields: [],
    Handlers: [],
    Helpers: [],
    Contracts: []
  };

  let index = 0;

  for (const member of members) {
    index++;
    const name = memberName(member, index);
    const type = classifyMember(name, member);

    if (type === "Route") {
      routeMembers.push(member);
      continue;
    }

    if (type === "Fields") {
      buckets.Fields.push(member);
      continue;
    }

    if (type === "Contracts") {
      buckets.Contracts.push(member);
      continue;
    }

    const fileName =
      path.dirname(routePath).replace(/\\/g, "/") + "/" +
      className + "." + type + "." + String(index).padStart(3, "0") + "." + sanitizeName(name) + ".cs";

    write(
      fileName,
      partialFile(
        usings,
        namespaceName,
        className,
        markerBase + "_" + type.toUpperCase() + "_SPLIT",
        [member]
      )
    );
  }

  if (routeMembers.length === 0) {
    throw new Error("No route/map member found for " + className);
  }

  write(
    routePath,
    partialFile(
      usings,
      namespaceName,
      className,
      markerBase + "_ROUTE_SPLIT",
      routeMembers
    )
  );

  if (buckets.Fields.length > 0) {
    write(
      path.dirname(routePath).replace(/\\/g, "/") + "/" + className + ".Fields.cs",
      partialFile(
        usings,
        namespaceName,
        className,
        markerBase + "_FIELDS_SPLIT",
        buckets.Fields
      )
    );
  }

  if (buckets.Contracts.length > 0) {
    const contractChunks = [];
    let current = [];

    for (const item of buckets.Contracts) {
      const candidate = partialFile(usings, namespaceName, className, markerBase + "_CONTRACTS_SPLIT", current.concat([item]));
      if (lineCount(candidate) > 260 && current.length > 0) {
        contractChunks.push(current);
        current = [item];
      } else {
        current.push(item);
      }
    }

    if (current.length > 0) contractChunks.push(current);

    contractChunks.forEach(function(chunk, chunkIndex) {
      write(
        path.dirname(routePath).replace(/\\/g, "/") + "/" + className + ".Contracts." + String(chunkIndex + 1).padStart(3, "0") + ".cs",
        partialFile(
          usings,
          namespaceName,
          className,
          markerBase + "_CONTRACTS_SPLIT",
          chunk
        )
      );
    });
  }

  if (buckets.Helpers.length > 0) {
    buckets.Helpers.forEach(function(item, helperIndex) {
      const name = memberName(item, helperIndex + 1);
      write(
        path.dirname(routePath).replace(/\\/g, "/") + "/" + className + ".Helpers." + String(helperIndex + 1).padStart(3, "0") + "." + sanitizeName(name) + ".cs",
        partialFile(
          usings,
          namespaceName,
          className,
          markerBase + "_HELPERS_SPLIT",
          [item]
        )
      );
    });
  }

  if (runtimePath) {
    write(
      runtimePath,
      [
        "// " + markerBase + "_RUNTIME_RETIRED",
        "// Runtime mega-file retired by Phase 05 T-027.",
        "// Real implementation now lives in route, handler, helper and contract partial files.",
        "",
        "namespace " + namespaceName + ";",
        "",
        "public static partial class " + className,
        "{",
        "}",
        ""
      ].join("\n")
    );
  }

  return {
    className,
    sourcePath,
    routePath,
    runtimePath,
    backupDir,
    members: members.length
  };
}

const results = [];

results.push(splitEndpoint({
  className: "Phase1WorkflowTruthEndpoints",
  routePath: "Backend/PlantProcess.Api/Endpoints/Admin/Phase1WorkflowTruthEndpoints.cs",
  runtimePath: "Backend/PlantProcess.Api/Endpoints/Admin/Phase1WorkflowTruthEndpoints.runtime.cs",
  markerBase: "PPIQ_REALIZATION_T027_PHASE1_WORKFLOW_TRUTH"
}));

results.push(splitEndpoint({
  className: "WorkflowEndpoints",
  routePath: "Backend/PlantProcess.Api/Endpoints/Workflow/WorkflowEndpoints.cs",
  runtimePath: "Backend/PlantProcess.Api/Endpoints/Workflow/WorkflowEndpoints.runtime.cs",
  markerBase: "PPIQ_REALIZATION_T027_WORKFLOW_ENDPOINTS"
}));

write("tools/phase5/validate-t027-workflow-endpoint-split.cjs", [
  "const fs = require('fs');",
  "const path = require('path');",
  "",
  "const root = process.cwd();",
  "const failures = [];",
  "",
  "function rel(file) { return path.relative(root, file).split(path.sep).join('/'); }",
  "function read(file) { return fs.readFileSync(file, 'utf8'); }",
  "function lineCount(text) { return text.replace(/\\r\\n/g, '\\n').split('\\n').length; }",
  "",
  "function checkClass(config) {",
  "  const dir = path.join(root, config.dir);",
  "  const files = fs.readdirSync(dir)",
  "    .filter(function(name) { return name.startsWith(config.className) && name.endsWith('.cs'); })",
  "    .map(function(name) { return path.join(dir, name); });",
  "",
  "  if (files.length < config.minFiles) {",
  "    failures.push({ className: config.className, reason: 'not enough split files', actual: files.length, expectedAtLeast: config.minFiles });",
  "  }",
  "",
  "  for (const file of files) {",
  "    const text = read(file);",
  "    const lines = lineCount(text);",
  "    if (lines > 300) failures.push({ file: rel(file), reason: 'file exceeds 300 lines', lines });",
  "  }",
  "",
  "  const route = path.join(root, config.routePath);",
  "  const runtime = path.join(root, config.runtimePath);",
  "",
  "  if (!fs.existsSync(route)) failures.push({ file: config.routePath, reason: 'route file missing' });",
  "  else if (!read(route).includes(config.routeMarker)) failures.push({ file: config.routePath, reason: 'route split marker missing' });",
  "",
  "  if (!fs.existsSync(runtime)) failures.push({ file: config.runtimePath, reason: 'runtime file missing' });",
  "  else {",
  "    const runtimeText = read(runtime);",
  "    if (!runtimeText.includes(config.runtimeMarker)) failures.push({ file: config.runtimePath, reason: 'runtime retirement marker missing' });",
  "    if (runtimeText.includes('Map' + config.className) || runtimeText.includes('GetWorkflowOverview') || runtimeText.includes('GetConnectorTruthAsync')) {",
  "      failures.push({ file: config.runtimePath, reason: 'runtime implementation still present' });",
  "    }",
  "  }",
  "",
  "  const allText = files.map(read).join('\\n');",
  "  for (const signal of config.signals) {",
  "    if (!allText.includes(signal)) failures.push({ className: config.className, reason: 'missing signal: ' + signal });",
  "  }",
  "}",
  "",
  "checkClass({",
  "  className: 'Phase1WorkflowTruthEndpoints',",
  "  dir: 'Backend/PlantProcess.Api/Endpoints/Admin',",
  "  routePath: 'Backend/PlantProcess.Api/Endpoints/Admin/Phase1WorkflowTruthEndpoints.cs',",
  "  runtimePath: 'Backend/PlantProcess.Api/Endpoints/Admin/Phase1WorkflowTruthEndpoints.runtime.cs',",
  "  routeMarker: 'PPIQ_REALIZATION_T027_PHASE1_WORKFLOW_TRUTH_ROUTE_SPLIT',",
  "  runtimeMarker: 'PPIQ_REALIZATION_T027_PHASE1_WORKFLOW_TRUTH_RUNTIME_RETIRED',",
  "  minFiles: 10,",
  "  signals: [",
  "    'MapPhase1WorkflowTruthEndpoints',",
  "    'GetConnectorTruthAsync',",
  "    'GetConnectorCertificationAsync',",
  "    'GetSourceScheduleBoardAsync',",
  "    'RunDueSourceImportsAsync',",
  "    'GetStagingSummaryAsync',",
  "    'GetSchemaMappingWorkbenchAsync',",
  "    'GetImportJobConfigurationBoardAsync'",
  "  ]",
  "});",
  "",
  "checkClass({",
  "  className: 'WorkflowEndpoints',",
  "  dir: 'Backend/PlantProcess.Api/Endpoints/Workflow',",
  "  routePath: 'Backend/PlantProcess.Api/Endpoints/Workflow/WorkflowEndpoints.cs',",
  "  runtimePath: 'Backend/PlantProcess.Api/Endpoints/Workflow/WorkflowEndpoints.runtime.cs',",
  "  routeMarker: 'PPIQ_REALIZATION_T027_WORKFLOW_ENDPOINTS_ROUTE_SPLIT',",
  "  runtimeMarker: 'PPIQ_REALIZATION_T027_WORKFLOW_ENDPOINTS_RUNTIME_RETIRED',",
  "  minFiles: 10,",
  "  signals: [",
  "    'MapWorkflowEndpoints',",
  "    'GetWorkflowOverview',",
  "    'GetWorkflowStatusAsync',",
  "    'RegisterSourceSystemAsync',",
  "    'CreateImportBatchAsync',",
  "    'CreateMaterialAsync',",
  "    'InvestigateMaterialAsync'",
  "  ]",
  "});",
  "",
  "if (failures.length) {",
  "  console.error('PPIQ-T027 failed: workflow endpoint split is incomplete.');",
  "  console.error(JSON.stringify(failures, null, 2));",
  "  process.exit(1);",
  "}",
  "",
  "console.log('PPIQ-T027 passed: workflow runtime mega-files retired and all split files are below 300 lines.');",
  ""
].join("\n"));

write("docs/phase5/T027_WORKFLOW_ENDPOINT_SPLIT.md", [
  "# T-027 Workflow Endpoint Split",
  "",
  "Marker: PPIQ_REALIZATION_T027_WORKFLOW_ENDPOINT_SPLIT",
  "",
  "## Result",
  "",
  "Phase1WorkflowTruthEndpoints and WorkflowEndpoints runtime mega-files were decomposed into partial route, handler, helper and contract files.",
  "",
  "## Validation",
  "",
  "Run:",
  "",
  "    node tools/phase5/validate-t027-workflow-endpoint-split.cjs",
  "    dotnet build Backend",
  "",
  "## Backups",
  "",
  results.map(function(r) { return "- " + r.className + ": " + r.backupDir; }).join("\n"),
  ""
].join("\n"));

run("node --check T-027 validator", "node", ["--check", "tools/phase5/validate-t027-workflow-endpoint-split.cjs"]);
run("T-027 validator", "node", ["tools/phase5/validate-t027-workflow-endpoint-split.cjs"]);
run("Backend build after T-027", "dotnet", ["build", "Backend"]);

console.log("");
console.log("=================================================================================================");
console.log("T-027 completed: workflow endpoint mega-files are split and backend build is green.");
for (const r of results) {
  console.log("- " + r.className + ": " + r.members + " members split; backup=" + r.backupDir);
}
console.log("=================================================================================================");
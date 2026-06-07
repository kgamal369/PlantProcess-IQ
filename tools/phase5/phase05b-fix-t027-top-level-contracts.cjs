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

function write(relativePath, content) {
  const target = p(relativePath);
  ensureDir(path.dirname(target));
  fs.writeFileSync(target, content.replace(/\n/g, "\r\n"), "utf8");
  console.log("Wrote: " + relativePath);
}

function read(relativePath) {
  return fs.readFileSync(p(relativePath), "utf8");
}

function readAbsolute(file) {
  return fs.readFileSync(file, "utf8");
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

function collectBackups(dir, output) {
  if (!fs.existsSync(dir)) return output;

  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);

    if (entry.isDirectory()) {
      collectBackups(full, output);
      continue;
    }

    if (entry.name.endsWith(".runtime.cs.before")) {
      output.push(full);
    }
  }

  return output;
}

function latestBackupFor(className) {
  const files = collectBackups(p(".phase5_backup"), [])
    .filter(function(file) {
      return path.basename(file).startsWith(className + ".runtime.cs.before") ||
        file.includes(path.sep + className + path.sep);
    });

  if (files.length === 0) {
    throw new Error("No T-027 backup found for " + className);
  }

  files.sort(function(a, b) {
    return fs.statSync(b).mtimeMs - fs.statSync(a).mtimeMs;
  });

  return files[0];
}

function findNamespace(source) {
  const match = /namespace\s+([A-Za-z0-9_.]+)\s*;/.exec(source);
  if (!match) throw new Error("Cannot locate namespace.");
  return match[1];
}

function originalUsings(source) {
  const namespaceIndex = source.indexOf("namespace ");
  if (namespaceIndex < 0) throw new Error("Cannot locate namespace.");
  return source.slice(0, namespaceIndex).trim();
}

function findClassSpan(source, className) {
  const regex = new RegExp("public\\s+static\\s+(?:partial\\s+)?class\\s+" + className + "\\b");
  const match = regex.exec(source);
  if (!match) throw new Error("Cannot locate class " + className);

  const braceStart = source.indexOf("{", match.index);
  if (braceStart < 0) throw new Error("Cannot locate class opening brace for " + className);

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
          start: match.index,
          end: i + 1
        };
      }
    }
  }

  throw new Error("Cannot locate class closing brace for " + className);
}

function removeClass(source, className) {
  const span = findClassSpan(source, className);
  return source.slice(0, span.start) + "\n" + source.slice(span.end);
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

function memberEnd(text, start) {
  let parenDepth = 0;
  let angleDepth = 0;
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
      else if (ch === "<") angleDepth++;
      else if (ch === ">") angleDepth = Math.max(0, angleDepth - 1);
    }

    if (ch === "{" && parenDepth === 0) {
      bodyBraceStart = bodyBraceStart < 0 ? i : bodyBraceStart;
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

    if (ch === ";" && parenDepth === 0 && angleDepth === 0 && braceDepth === 0) {
      return i + 1;
    }
  }

  return text.length;
}

function extractTopLevelContracts(source, className) {
  const namespaceName = findNamespace(source);
  const withoutClass = removeClass(source, className);
  const namespaceIndex = withoutClass.indexOf("namespace ");
  const semicolonIndex = withoutClass.indexOf(";", namespaceIndex);

  if (namespaceIndex < 0 || semicolonIndex < 0) {
    throw new Error("Cannot locate file-scoped namespace in contract source.");
  }

  const body = withoutClass.slice(semicolonIndex + 1);
  const contracts = [];
  let i = 0;

  while (i < body.length) {
    i = skipTrivia(body, i);
    if (i >= body.length) break;

    const rest = body.slice(i);
    const match = /^(?:public|internal|private)?\s*(?:sealed\s+)?(?:record|record\s+struct|readonly\s+record\s+struct|class)\s+[A-Za-z_][A-Za-z0-9_]*/.exec(rest);

    if (!match) {
      i++;
      continue;
    }

    const end = memberEnd(body, i);
    const declaration = body.slice(i, end).trim();

    if (/\b(record|class)\b/.test(declaration)) {
      contracts.push(declaration);
    }

    i = end;
  }

  return {
    namespaceName,
    contracts
  };
}

function lineCount(text) {
  return text.replace(/\r\n/g, "\n").split("\n").length;
}

function writeContractChunks(options) {
  const className = options.className;
  const outDir = options.outDir;
  const marker = options.marker;
  const backupFile = latestBackupFor(className);
  const source = readAbsolute(backupFile);
  const usings = originalUsings(source);
  const result = extractTopLevelContracts(source, className);

  console.log("Using contract source for " + className + ": " + path.relative(root, backupFile).split(path.sep).join("/"));
  console.log("Top-level contracts found for " + className + ": " + result.contracts.length);

  if (result.contracts.length === 0) {
    throw new Error("No top-level contracts found for " + className);
  }

  let chunk = [];
  let chunkIndex = 1;

  function flush() {
    if (chunk.length === 0) return;

    const content = [
      usings,
      "",
      "namespace " + result.namespaceName + ";",
      "",
      "// " + marker,
      "",
      chunk.join("\n\n"),
      ""
    ].join("\n");

    write(
      outDir + "/" + className + ".TopLevelContracts." + String(chunkIndex).padStart(3, "0") + ".cs",
      content
    );

    chunkIndex++;
    chunk = [];
  }

  for (const contract of result.contracts) {
    const candidate = [
      usings,
      "",
      "namespace " + result.namespaceName + ";",
      "",
      "// " + marker,
      "",
      chunk.concat([contract]).join("\n\n"),
      ""
    ].join("\n");

    if (lineCount(candidate) > 260 && chunk.length > 0) {
      flush();
    }

    chunk.push(contract);
  }

  flush();
}

writeContractChunks({
  className: "Phase1WorkflowTruthEndpoints",
  outDir: "Backend/PlantProcess.Api/Endpoints/Admin",
  marker: "PPIQ_REALIZATION_T027_PHASE1_WORKFLOW_TRUTH_TOP_LEVEL_CONTRACTS"
});

writeContractChunks({
  className: "WorkflowEndpoints",
  outDir: "Backend/PlantProcess.Api/Endpoints/Workflow",
  marker: "PPIQ_REALIZATION_T027_WORKFLOW_ENDPOINTS_TOP_LEVEL_CONTRACTS"
});

const validatorPath = "tools/phase5/validate-t027-workflow-endpoint-split.cjs";
let validator = read(validatorPath);

if (!validator.includes("TOP_LEVEL_CONTRACTS")) {
  validator = validator.replace(
    "for (const signal of config.signals) {",
    [
      "if (!allText.includes(config.topLevelMarker)) {",
      "    failures.push({ className: config.className, reason: 'missing top-level contract marker: ' + config.topLevelMarker });",
      "  }",
      "",
      "  for (const signal of config.signals) {"
    ].join("\n")
  );

  validator = validator.replace(
    "signals: [\n    'MapPhase1WorkflowTruthEndpoints',",
    "topLevelMarker: 'PPIQ_REALIZATION_T027_PHASE1_WORKFLOW_TRUTH_TOP_LEVEL_CONTRACTS',\n  signals: [\n    'MapPhase1WorkflowTruthEndpoints',"
  );

  validator = validator.replace(
    "signals: [\n    'MapWorkflowEndpoints',",
    "topLevelMarker: 'PPIQ_REALIZATION_T027_WORKFLOW_ENDPOINTS_TOP_LEVEL_CONTRACTS',\n  signals: [\n    'MapWorkflowEndpoints',"
  );

  validator = validator.replace(
    "'GetImportJobConfigurationBoardAsync'",
    "'GetImportJobConfigurationBoardAsync',\n    'RunDueSourceImportsRequest',\n    'UpdateDatasetCursorRequest',\n    'PreviewSchemaViewRequest',\n    'CreateImportJobFromMappingRequest',\n    'ConnectorProviderTruthRow',\n    'ConnectorCertificationRow',\n    'CanonicalTargetRow',\n    'SchemaViewPreviewColumn'"
  );
}

write(validatorPath, validator);

run("node --check T-027 validator", "node", ["--check", validatorPath]);
run("T-027 validator", "node", [validatorPath]);
run("Backend build after T-027 top-level contracts fix", "dotnet", ["build", "Backend"]);

console.log("");
console.log("T-027 top-level contracts restored and backend build is green if the build passed.");
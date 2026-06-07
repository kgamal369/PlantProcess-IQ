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

function readAbsolute(file) {
  return fs.readFileSync(file, "utf8");
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
    .slice(0, 100) || "Member";
}

function collectBackups(dir, output) {
  if (!fs.existsSync(dir)) return output;

  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);

    if (entry.isDirectory()) {
      collectBackups(full, output);
      continue;
    }

    if (entry.name === "ConnectorConfigurationService.runtime.cs.before") {
      output.push(full);
    }
  }

  return output;
}

function findLatestBackup() {
  const files = collectBackups(p(".phase5_backup"), []);

  if (files.length === 0) {
    throw new Error("No T-028 backup found under .phase5_backup.");
  }

  files.sort(function(a, b) {
    return fs.statSync(b).mtimeMs - fs.statSync(a).mtimeMs;
  });

  return files[0];
}

function originalUsings(source) {
  const index = source.indexOf("namespace ");
  if (index < 0) throw new Error("Cannot locate namespace.");
  return source.slice(0, index).trim();
}

function findNamespace(source) {
  const match = /namespace\s+([A-Za-z0-9_.]+)\s*;/.exec(source);
  if (!match) throw new Error("Cannot locate namespace.");
  return match[1];
}

function findClassBlock(source, className) {
  const regex = new RegExp("public\\s+sealed\\s+(?:partial\\s+)?class\\s+" + className + "\\b");
  const match = regex.exec(source);
  if (!match) throw new Error("Cannot locate class: " + className);

  const braceStart = source.indexOf("{", match.index);
  if (braceStart < 0) throw new Error("Cannot locate class opening brace.");

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
          body: source.slice(braceStart + 1, i)
        };
      }
    }
  }

  throw new Error("Cannot locate class closing brace.");
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
      if (braceDepth === 0 && bodyBraceStart < 0) bodyBraceStart = i;
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
  const constructorMatch = /\bpublic\s+ConnectorConfigurationService\s*\(/.exec(memberText);
  if (constructorMatch) return "Constructor";

  const methodMatch = /\b(?:public|private|internal)\s+(?:static\s+)?(?:async\s+)?[\s\S]*?\s+([A-Za-z_][A-Za-z0-9_]*)\s*\(/.exec(memberText);
  if (methodMatch) return methodMatch[1];

  const recordMatch = /\brecord\s+([A-Za-z_][A-Za-z0-9_]*)\b/.exec(memberText);
  if (recordMatch) return recordMatch[1];

  const classMatch = /\bclass\s+([A-Za-z_][A-Za-z0-9_]*)\b/.exec(memberText);
  if (classMatch) return classMatch[1];

  const fieldMatch = /([A-Za-z_][A-Za-z0-9_]*)\s*(?:=|;)/.exec(memberText);
  if (fieldMatch) return fieldMatch[1];

  return "Member" + String(fallbackIndex).padStart(3, "0");
}

function groupFor(name, memberText) {
  if (name === "Constructor") return "Surface";

  if (/^\s*private\s+(?:readonly|static\s+readonly|const)\b/.test(memberText)) return "Surface";

  if (name === "CsvParseResult" || name === "CsvTextParser") return "CsvParser";

  if (name === "GetProviderTypes") return "ProviderTypes";

  if (name.includes("ConnectionProfile") || name === "TestConnectionProfileAsync") return "Profiles";

  if (name.includes("Dataset") || name === "DiscoverSchemaAsync" || name === "ToDatasetDto") return "Datasets";

  if (name.includes("Csv") || name.includes("Delimiter")) return "Csv";

  if (name.includes("Query") || name === "GetFieldDtosAsync") return "Queries";

  if (name.includes("FieldDefinition")) return "Fields";

  if (name.includes("Validate")) return "Validation";

  return "Helpers";
}

function partialFile(usings, namespaceName, marker, members) {
  return [
    usings,
    "",
    "namespace " + namespaceName + ";",
    "",
    "// " + marker,
    "public sealed partial class ConnectorConfigurationService",
    "{",
    members.join("\n\n"),
    "}",
    ""
  ].join("\n");
}

const backupFile = findLatestBackup();
console.log("Using source backup: " + path.relative(root, backupFile).split(path.sep).join("/"));

const source = readAbsolute(backupFile);

if (!source.includes("public sealed class ConnectorConfigurationService")) {
  throw new Error("Backup does not look like the original ConnectorConfigurationService runtime source.");
}

const usings = originalUsings(source);
const namespaceName = findNamespace(source);
const classBody = findClassBlock(source, "ConnectorConfigurationService").body;
const members = extractMembers(classBody);

if (members.length < 20) {
  throw new Error("Suspiciously low ConnectorConfigurationService member count: " + members.length);
}

const serviceDir = p("Backend/PlantProcess.Application/Integration/Services/Connectors");

for (const name of fs.readdirSync(serviceDir)) {
  if (name.startsWith("ConnectorConfigurationService") && name.endsWith(".cs")) {
    fs.unlinkSync(path.join(serviceDir, name));
  }
}

const surfaceMembers = [];
const csvParserMembers = [];
const methodMembers = [];

let index = 0;

for (const member of members) {
  index++;
  const name = memberName(member, index);
  const group = groupFor(name, member);

  if (group === "Surface") {
    surfaceMembers.push(member);
    continue;
  }

  if (group === "CsvParser") {
    csvParserMembers.push(member);
    continue;
  }

  methodMembers.push({
    name,
    group,
    member
  });
}

if (surfaceMembers.length < 3) {
  throw new Error("Surface extraction failed. Expected fields + constructor.");
}

write(
  "Backend/PlantProcess.Application/Integration/Services/Connectors/ConnectorConfigurationService.cs",
  [
    usings,
    "",
    "namespace " + namespaceName + ";",
    "",
    "// PPIQ_REALIZATION_T028_CONNECTOR_CONFIGURATION_SERVICE_SURFACE_SPLIT",
    "public sealed partial class ConnectorConfigurationService : IConnectorConfigurationService",
    "{",
    surfaceMembers.join("\n\n"),
    "}",
    ""
  ].join("\n")
);

let sequence = 1;

for (const item of methodMembers) {
  const file =
    "Backend/PlantProcess.Application/Integration/Services/Connectors/ConnectorConfigurationService." +
    item.group + "." + String(sequence).padStart(3, "0") + "." + sanitizeName(item.name) + ".cs";

  write(
    file,
    partialFile(
      usings,
      namespaceName,
      "PPIQ_REALIZATION_T028_CONNECTOR_CONFIGURATION_SERVICE_" + item.group.toUpperCase() + "_SPLIT",
      [item.member]
    )
  );

  sequence++;
}

if (csvParserMembers.length > 0) {
  write(
    "Backend/PlantProcess.Application/Integration/Services/Connectors/ConnectorConfigurationService.CsvParser.999.CsvTextParser.cs",
    partialFile(
      usings,
      namespaceName,
      "PPIQ_REALIZATION_T028_CONNECTOR_CONFIGURATION_SERVICE_CSV_TEXT_PARSER_SPLIT",
      csvParserMembers
    )
  );
}

write(
  "Backend/PlantProcess.Application/Integration/Services/Connectors/ConnectorConfigurationService.runtime.cs",
  [
    "// PPIQ_REALIZATION_T028_CONNECTOR_CONFIGURATION_SERVICE_RUNTIME_RETIRED",
    "// Runtime mega-file retired by Phase 05 T-028.",
    "// Real implementation now lives in small partial files beside ConnectorConfigurationService.cs.",
    "",
    "namespace " + namespaceName + ";",
    "",
    "public sealed partial class ConnectorConfigurationService",
    "{",
    "}",
    ""
  ].join("\n")
);

write("tools/phase5/validate-t028-connector-configuration-service-split.cjs", [
  "const fs = require('fs');",
  "const path = require('path');",
  "",
  "const root = process.cwd();",
  "const dir = path.join(root, 'Backend', 'PlantProcess.Application', 'Integration', 'Services', 'Connectors');",
  "const failures = [];",
  "",
  "function rel(file) { return path.relative(root, file).split(path.sep).join('/'); }",
  "function read(file) { return fs.readFileSync(file, 'utf8'); }",
  "function lineCount(text) { return text.replace(/\\r\\n/g, '\\n').split('\\n').length; }",
  "",
  "const files = fs.readdirSync(dir)",
  "  .filter(function(name) { return name.startsWith('ConnectorConfigurationService') && name.endsWith('.cs'); })",
  "  .map(function(name) { return path.join(dir, name); });",
  "",
  "if (files.length < 25) {",
  "  failures.push({ reason: 'not enough ConnectorConfigurationService split files', actual: files.length, expectedAtLeast: 25 });",
  "}",
  "",
  "for (const file of files) {",
  "  const text = read(file);",
  "  const lines = lineCount(text);",
  "  if (lines > 300) failures.push({ file: rel(file), reason: 'file exceeds 300 lines', lines });",
  "}",
  "",
  "const service = path.join(dir, 'ConnectorConfigurationService.cs');",
  "const runtime = path.join(dir, 'ConnectorConfigurationService.runtime.cs');",
  "",
  "if (!fs.existsSync(service)) failures.push({ file: rel(service), reason: 'service surface file missing' });",
  "else {",
  "  const text = read(service);",
  "  if (!text.includes('PPIQ_REALIZATION_T028_CONNECTOR_CONFIGURATION_SERVICE_SURFACE_SPLIT')) failures.push({ file: rel(service), reason: 'surface split marker missing' });",
  "  if (!text.includes('public sealed partial class ConnectorConfigurationService : IConnectorConfigurationService')) failures.push({ file: rel(service), reason: 'service surface does not implement interface' });",
  "}",
  "",
  "if (!fs.existsSync(runtime)) failures.push({ file: rel(runtime), reason: 'runtime file missing' });",
  "else {",
  "  const text = read(runtime);",
  "  if (!text.includes('PPIQ_REALIZATION_T028_CONNECTOR_CONFIGURATION_SERVICE_RUNTIME_RETIRED')) failures.push({ file: rel(runtime), reason: 'runtime retirement marker missing' });",
  "  if (text.includes('GetProviderTypes') || text.includes('CreateConnectionProfileAsync') || text.includes('ImportCsvSnapshotAsync')) failures.push({ file: rel(runtime), reason: 'runtime implementation still present' });",
  "}",
  "",
  "const allText = files.map(read).join('\\n');",
  "",
  "for (const signal of [",
  "  'GetProviderTypes',",
  "  'GetConnectionProfilesAsync',",
  "  'GetConnectionProfileByIdAsync',",
  "  'CreateConnectionProfileAsync',",
  "  'UpdateConnectionProfileAsync',",
  "  'ActivateConnectionProfileAsync',",
  "  'DeactivateConnectionProfileAsync',",
  "  'TestConnectionProfileAsync',",
  "  'DiscoverSchemaAsync',",
  "  'GetDatasetsAsync',",
  "  'CreateDatasetAsync',",
  "  'DiscoverCsvSchemaAsync',",
  "  'PreviewCsvAsync',",
  "  'ImportCsvSnapshotAsync',",
  "  'GetConnectionProfileDtoQuery',",
  "  'GetDatasetDtoQuery',",
  "  'GetFieldDtosAsync',",
  "  'BuildFieldDefinitions',",
  "  'BuildFieldDefinitionDtos',",
  "  'ValidateConnectionProfileRequest',",
  "  'CsvTextParser',",
  "  'CsvParseResult',",
  "  'PPIQ_REALIZATION_T028_CONNECTOR_CONFIGURATION_SERVICE_CSV_TEXT_PARSER_SPLIT'",
  "]) {",
  "  if (!allText.includes(signal)) failures.push({ reason: 'missing signal: ' + signal });",
  "}",
  "",
  "if (failures.length) {",
  "  console.error('PPIQ-T028 failed: ConnectorConfigurationService split is incomplete.');",
  "  console.error(JSON.stringify(failures, null, 2));",
  "  process.exit(1);",
  "}",
  "",
  "console.log('PPIQ-T028 passed: ConnectorConfigurationService runtime mega-file retired and all split files are below 300 lines.');",
  ""
].join("\n"));

write("docs/phase5/T028_CONNECTOR_CONFIGURATION_SERVICE_SPLIT.md", [
  "# T-028 ConnectorConfigurationService Split",
  "",
  "Marker: PPIQ_REALIZATION_T028_CONNECTOR_CONFIGURATION_SERVICE_SURFACE_SPLIT",
  "",
  "ConnectorConfigurationService.runtime.cs was regenerated from backup using a top-level member extractor.",
  "",
  "Validation:",
  "",
  "    node tools/phase5/validate-t028-connector-configuration-service-split.cjs",
  "    dotnet build Backend",
  "",
  "Source backup:",
  "",
  path.relative(root, backupFile).split(path.sep).join("/"),
  ""
].join("\n"));

run("node --check T-028 validator", "node", ["--check", "tools/phase5/validate-t028-connector-configuration-service-split.cjs"]);
run("T-028 validator", "node", ["tools/phase5/validate-t028-connector-configuration-service-split.cjs"]);
run("Backend build after T-028 regenerate", "dotnet", ["build", "Backend"]);

console.log("");
console.log("T-028 regenerated cleanly from backup and build is green if the build passed.");
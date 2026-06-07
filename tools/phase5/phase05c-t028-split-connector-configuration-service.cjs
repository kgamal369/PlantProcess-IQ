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
    .slice(0, 100) || "Member";
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

function findClassSpan(source, className) {
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
          classStart: match.index,
          braceStart,
          bodyStart: braceStart + 1,
          bodyEnd: i,
          body: source.slice(braceStart + 1, i)
        };
      }
    }
  }

  throw new Error("Cannot locate class closing brace.");
}

function findBlockFromSignature(source, signatureRegex, displayName) {
  const match = signatureRegex.exec(source);
  if (!match) throw new Error("Cannot find signature: " + displayName);

  const start = match.index;
  const braceStart = source.indexOf("{", match.index);
  if (braceStart < 0) throw new Error("Cannot find opening brace: " + displayName);

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
        let end = i + 1;
        while (end < source.length && /\s/.test(source[end])) end++;
        if (source[end] === ";") end++;
        return source.slice(start, end).trim();
      }
    }
  }

  throw new Error("Cannot find closing brace: " + displayName);
}

function findMethod(source, methodName) {
  return findBlockFromSignature(
    source,
    new RegExp("^[ \\t]*(?:public|private|internal)\\s+(?:static\\s+)?(?:async\\s+)?[\\s\\S]*?\\b" + methodName + "\\s*\\(", "m"),
    methodName
  );
}

function findConstructor(source) {
  return findBlockFromSignature(
    source,
    /^[ \t]*public\s+ConnectorConfigurationService\s*\(/m,
    "ConnectorConfigurationService constructor"
  );
}

function findNestedRecord(source, recordName) {
  return findBlockFromSignature(
    source,
    new RegExp("^[ \\t]*private\\s+sealed\\s+record\\s+" + recordName + "\\s*\\(", "m"),
    recordName
  );
}

function findNestedStaticClass(source, className) {
  return findBlockFromSignature(
    source,
    new RegExp("^[ \\t]*private\\s+static\\s+class\\s+" + className + "\\b", "m"),
    className
  );
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

const runtimePath = "Backend/PlantProcess.Application/Integration/Services/Connectors/ConnectorConfigurationService.runtime.cs";
const servicePath = "Backend/PlantProcess.Application/Integration/Services/Connectors/ConnectorConfigurationService.cs";

if (!exists(runtimePath)) {
  throw new Error("Missing runtime file: " + runtimePath);
}

let source = read(runtimePath);

if (!source.includes("public sealed class ConnectorConfigurationService") &&
    !source.includes("public sealed partial class ConnectorConfigurationService")) {
  throw new Error("Unexpected ConnectorConfigurationService runtime shape.");
}

const backupDir = ".phase5_backup/t028_connector_configuration_service_split_" +
  new Date().toISOString().replace(/[-:]/g, "").replace(/\..+/, "").replace("T", "_");

ensureDir(p(backupDir));
fs.copyFileSync(p(runtimePath), p(backupDir + "/ConnectorConfigurationService.runtime.cs.before"));
if (exists(servicePath)) {
  fs.copyFileSync(p(servicePath), p(backupDir + "/ConnectorConfigurationService.cs.before"));
}

const usings = originalUsings(source);
const namespaceName = findNamespace(source);
const classSpan = findClassSpan(source, "ConnectorConfigurationService");
const classBody = classSpan.body;

const constructor = findConstructor(source);
const constructorIndex = source.indexOf(constructor);
const fieldBlock = source.slice(classSpan.bodyStart, constructorIndex).trim();

write(servicePath, [
  usings,
  "",
  "namespace " + namespaceName + ";",
  "",
  "// PPIQ_REALIZATION_T028_CONNECTOR_CONFIGURATION_SERVICE_SURFACE_SPLIT",
  "public sealed partial class ConnectorConfigurationService : IConnectorConfigurationService",
  "{",
  fieldBlock,
  "",
  constructor,
  "}",
  ""
].join("\n"));

const methods = [
  ["ProviderTypes", "GetProviderTypes"],
  ["Profiles", "GetConnectionProfilesAsync"],
  ["Profiles", "GetConnectionProfileByIdAsync"],
  ["Profiles", "CreateConnectionProfileAsync"],
  ["Profiles", "UpdateConnectionProfileAsync"],
  ["Profiles", "ActivateConnectionProfileAsync"],
  ["Profiles", "DeactivateConnectionProfileAsync"],
  ["Profiles", "TestConnectionProfileAsync"],
  ["Datasets", "ToDatasetDto"],
  ["Datasets", "DiscoverSchemaAsync"],
  ["Datasets", "GetDatasetsAsync"],
  ["Datasets", "CreateDatasetAsync"],
  ["Csv", "DiscoverCsvSchemaAsync"],
  ["Csv", "PreviewCsvAsync"],
  ["Csv", "ImportCsvSnapshotAsync"],
  ["Queries", "GetConnectionProfileDtoQuery"],
  ["Queries", "GetDatasetDtoQuery"],
  ["Queries", "GetFieldDtosAsync"],
  ["Fields", "BuildFieldDefinitions"],
  ["Fields", "BuildFieldDefinitionDtos"],
  ["Validation", "ValidateConnectionProfileRequest"],
  ["Helpers", "NormalizeCode"],
  ["Helpers", "NormalizeProviderType"],
  ["Helpers", "ResolveDelimiter"],
  ["Helpers", "InferDataType"],
  ["Helpers", "LooksLikeKey"],
  ["Helpers", "LooksLikeTimestamp"]
];

let sequence = 1;

for (const [group, methodName] of methods) {
  const member = findMethod(source, methodName);

  write(
    "Backend/PlantProcess.Application/Integration/Services/Connectors/ConnectorConfigurationService." +
      group + "." + String(sequence).padStart(3, "0") + "." + sanitizeName(methodName) + ".cs",
    partialFile(
      usings,
      namespaceName,
      "PPIQ_REALIZATION_T028_CONNECTOR_CONFIGURATION_SERVICE_" + group.toUpperCase() + "_SPLIT",
      [member]
    )
  );

  sequence++;
}

const csvParseRecord = findNestedRecord(source, "CsvParseResult");
const csvTextParser = findNestedStaticClass(source, "CsvTextParser");

write(
  "Backend/PlantProcess.Application/Integration/Services/Connectors/ConnectorConfigurationService.Csv.999.CsvTextParser.cs",
  partialFile(
    usings,
    namespaceName,
    "PPIQ_REALIZATION_T028_CONNECTOR_CONFIGURATION_SERVICE_CSV_TEXT_PARSER_SPLIT",
    [csvParseRecord, csvTextParser]
  )
);

write(runtimePath, [
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
].join("\n"));

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
  "## Result",
  "",
  "ConnectorConfigurationService.runtime.cs was retired and decomposed into small partial files by responsibility:",
  "",
  "- provider types",
  "- connection profiles",
  "- datasets",
  "- CSV discovery / preview / import",
  "- query projections",
  "- field definition helpers",
  "- validation and normalization helpers",
  "",
  "## Validation",
  "",
  "Run:",
  "",
  "    node tools/phase5/validate-t028-connector-configuration-service-split.cjs",
  "    dotnet build Backend",
  "",
  "## Backup",
  "",
  backupDir,
  ""
].join("\n"));

run("node --check T-028 validator", "node", ["--check", "tools/phase5/validate-t028-connector-configuration-service-split.cjs"]);
run("T-028 validator", "node", ["tools/phase5/validate-t028-connector-configuration-service-split.cjs"]);
run("Backend build after T-028", "dotnet", ["build", "Backend"]);

console.log("");
console.log("=================================================================================================");
console.log("T-028 completed: ConnectorConfigurationService real split is green.");
console.log("Backup: " + backupDir);
console.log("=================================================================================================");
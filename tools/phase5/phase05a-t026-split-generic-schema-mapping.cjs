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

function read(relativePath) {
  return fs.readFileSync(p(relativePath), "utf8");
}

function write(relativePath, content) {
  const target = p(relativePath);
  ensureDir(path.dirname(target));
  fs.writeFileSync(target, content.replace(/\n/g, "\r\n"), "utf8");
  console.log("Wrote: " + relativePath);
}

function exists(relativePath) {
  return fs.existsSync(p(relativePath));
}

function run(name, command, args, cwd = root) {
  console.log("");
  console.log("---- " + name);
  cp.execFileSync(command, args, {
    cwd,
    stdio: "inherit",
    shell: false
  });
}

function lineCount(text) {
  return text.replace(/\r\n/g, "\n").split("\n").length;
}

function findMethodBlock(source, methodName) {
  const pattern = new RegExp(
    "(?:public|private|internal)\\s+static\\s+(?:async\\s+)?[\\s\\S]{0,240}?\\s+" +
      methodName +
      "\\s*\\(",
    "m"
  );

  const match = pattern.exec(source);
  if (!match) {
    throw new Error("Cannot find method: " + methodName);
  }

  let start = match.index;

  while (start > 0 && source[start - 1] !== "\n") {
    start--;
  }

  const braceStart = source.indexOf("{", match.index);
  if (braceStart < 0) {
    throw new Error("Cannot find opening brace for method: " + methodName);
  }

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

      if (ch === '"') {
        inString = false;
      }

      continue;
    }

    if (inChar) {
      if (ch === "\\") {
        i++;
        continue;
      }

      if (ch === "'") {
        inChar = false;
      }

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

    if (ch === "{") {
      depth++;
    } else if (ch === "}") {
      depth--;

      if (depth === 0) {
        return source.slice(start, i + 1).trim();
      }
    }
  }

  throw new Error("Cannot find closing brace for method: " + methodName);
}

function findRecordsBlock(source) {
  const first = source.indexOf("    public sealed record RegisterCanonicalViewRequest");
  if (first < 0) {
    throw new Error("Cannot find DTO record block.");
  }

  const lastBrace = source.lastIndexOf("\n}");
  if (lastBrace < first) {
    throw new Error("Cannot locate class closing brace.");
  }

  return source.slice(first, lastBrace).trim();
}

function makePartial(fileName, body, marker) {
  const content =
`using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlantProcess.Api.ErrorHandling;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Api.Endpoints.Admin;

// ${marker}
public static partial class GenericSchemaMappingEndpoints
{
${body.split("\n").map(line => "    " + line).join("\n")}
}
`;

  write("Backend/PlantProcess.Api/Endpoints/Admin/" + fileName, content);
}

const runtimePath = "Backend/PlantProcess.Api/Endpoints/Admin/GenericSchemaMappingEndpoints.runtime.cs";
const routePath = "Backend/PlantProcess.Api/Endpoints/Admin/GenericSchemaMappingEndpoints.cs";

if (!exists(runtimePath)) {
  throw new Error("Missing runtime file: " + runtimePath);
}

const source = read(runtimePath);

if (!source.includes("public static class GenericSchemaMappingEndpoints")) {
  throw new Error("Unexpected source shape. Expected public static class GenericSchemaMappingEndpoints.");
}

const backupDir = ".phase5_backup/t026_generic_schema_mapping_split_" +
  new Date().toISOString().replace(/[-:]/g, "").replace(/\..+/, "").replace("T", "_");

ensureDir(p(backupDir));
fs.copyFileSync(p(runtimePath), p(backupDir + "/GenericSchemaMappingEndpoints.runtime.cs.before"));
if (exists(routePath)) {
  fs.copyFileSync(p(routePath), p(backupDir + "/GenericSchemaMappingEndpoints.cs.before"));
}

const safeIdentifierStart = source.indexOf("    private static readonly Regex SafeIdentifier");
const mapBlock = findMethodBlock(source, "MapGenericSchemaMappingEndpoints");

if (safeIdentifierStart < 0) {
  throw new Error("Cannot find SafeIdentifier field.");
}

const mapStart = source.indexOf(mapBlock);
const fieldBlock = source.slice(safeIdentifierStart, mapStart).trim();

const routeFile =
`using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Api.Endpoints.Admin;

/// <summary>
/// PPIQ_REALIZATION_T026_GENERIC_SCHEMA_MAPPING_ROUTE_SPLIT
/// Thin route registration surface. Runtime implementation was decomposed into cohesive partial files.
/// </summary>
public static partial class GenericSchemaMappingEndpoints
{
${fieldBlock.split("\n").map(line => "    " + line).join("\n")}

${mapBlock.split("\n").map(line => "    " + line).join("\n")}
}
`;

write(routePath, routeFile);

makePartial(
  "GenericSchemaMappingEndpoints.Catalog.cs",
  [
    findMethodBlock(source, "GetCatalogAsync"),
    findMethodBlock(source, "RegisterCanonicalViewAsync"),
    findMethodBlock(source, "EnsureCatalogAsync"),
    findMethodBlock(source, "ValidateRegisterRequest"),
    findMethodBlock(source, "UpsertCatalogAsync"),
    findMethodBlock(source, "GetCatalogByIdAsync")
  ].join("\n\n"),
  "PPIQ_REALIZATION_T026_GENERIC_SCHEMA_MAPPING_CATALOG_SPLIT"
);

makePartial(
  "GenericSchemaMappingEndpoints.Resolver.cs",
  [
    findMethodBlock(source, "ResolveSchemaViewAsync"),
    findMethodBlock(source, "ValidateRequestedResolverColumns")
  ].join("\n\n"),
  "PPIQ_REALIZATION_T026_GENERIC_SCHEMA_MAPPING_RESOLVER_SPLIT"
);

makePartial(
  "GenericSchemaMappingEndpoints.Joins.cs",
  [
    findMethodBlock(source, "PreviewJoinAsync"),
    findMethodBlock(source, "MaterializeJoinAsync"),
    findMethodBlock(source, "BuildJoinSql")
  ].join("\n\n"),
  "PPIQ_REALIZATION_T026_GENERIC_SCHEMA_MAPPING_JOIN_SPLIT"
);

makePartial(
  "GenericSchemaMappingEndpoints.Kpi.cs",
  [
    findMethodBlock(source, "CreateKpiViewAsync"),
    findMethodBlock(source, "TryInsertKpiDefinitionAsync")
  ].join("\n\n"),
  "PPIQ_REALIZATION_T026_GENERIC_SCHEMA_MAPPING_KPI_SPLIT"
);

makePartial(
  "GenericSchemaMappingEndpoints.Execution.cs",
  [
    findMethodBlock(source, "ExecuteMappingAsync"),
    findMethodBlock(source, "GetReadinessAsync"),
    findMethodBlock(source, "CreateOrReplaceViewAsync"),
    findMethodBlock(source, "PreviewSchemaOnlyAsync"),
    findMethodBlock(source, "CountRowsAsync")
  ].join("\n\n"),
  "PPIQ_REALIZATION_T026_GENERIC_SCHEMA_MAPPING_EXECUTION_SPLIT"
);

makePartial(
  "GenericSchemaMappingEndpoints.SqlHelpers.cs",
  [
    findMethodBlock(source, "NormalizeSelectSql"),
    findMethodBlock(source, "StripTrailingSemicolon"),
    findMethodBlock(source, "QueryAsync"),
    findMethodBlock(source, "ExecuteNonQueryAsync"),
    findMethodBlock(source, "AddParameter"),
    findMethodBlock(source, "CleanIdentifier"),
    findMethodBlock(source, "QuoteIdentifier"),
    findMethodBlock(source, "NormalizeCode"),
    findMethodBlock(source, "EmptyToNull"),
    findMethodBlock(source, "GetActor")
  ].join("\n\n"),
  "PPIQ_REALIZATION_T026_GENERIC_SCHEMA_MAPPING_SQL_HELPERS_SPLIT"
);

makePartial(
  "GenericSchemaMappingEndpoints.Contracts.cs",
  findRecordsBlock(source),
  "PPIQ_REALIZATION_T026_GENERIC_SCHEMA_MAPPING_CONTRACTS_SPLIT"
);

write(runtimePath, `// PPIQ_REALIZATION_T026_GENERIC_SCHEMA_MAPPING_RUNTIME_RETIRED
// GenericSchemaMappingEndpoints.runtime.cs was retired by Phase 05 T-026.
// Real implementation now lives in cohesive partial files:
// - GenericSchemaMappingEndpoints.cs
// - GenericSchemaMappingEndpoints.Catalog.cs
// - GenericSchemaMappingEndpoints.Resolver.cs
// - GenericSchemaMappingEndpoints.Joins.cs
// - GenericSchemaMappingEndpoints.Kpi.cs
// - GenericSchemaMappingEndpoints.Execution.cs
// - GenericSchemaMappingEndpoints.SqlHelpers.cs
// - GenericSchemaMappingEndpoints.Contracts.cs
//
// This file intentionally contains no compiled endpoint implementation.
`);

write("tools/phase5/validate-t026-generic-schema-mapping-split.cjs", String.raw`const fs = require("fs");
const path = require("path");

const root = process.cwd();
const dir = path.join(root, "Backend", "PlantProcess.Api", "Endpoints", "Admin");
const failures = [];

function rel(file) {
  return path.relative(root, file).split(path.sep).join("/");
}

function read(file) {
  return fs.readFileSync(file, "utf8");
}

function lineCount(text) {
  return text.replace(/\r\n/g, "\n").split("\n").length;
}

const required = [
  "GenericSchemaMappingEndpoints.cs",
  "GenericSchemaMappingEndpoints.Catalog.cs",
  "GenericSchemaMappingEndpoints.Resolver.cs",
  "GenericSchemaMappingEndpoints.Joins.cs",
  "GenericSchemaMappingEndpoints.Kpi.cs",
  "GenericSchemaMappingEndpoints.Execution.cs",
  "GenericSchemaMappingEndpoints.SqlHelpers.cs",
  "GenericSchemaMappingEndpoints.Contracts.cs",
  "GenericSchemaMappingEndpoints.runtime.cs"
];

for (const name of required) {
  const file = path.join(dir, name);

  if (!fs.existsSync(file)) {
    failures.push({ file: rel(file), reason: "required split file missing" });
    continue;
  }

  const text = read(file);
  const lines = lineCount(text);

  if (lines > 300) {
    failures.push({ file: rel(file), reason: "file exceeds 300 lines", lines });
  }
}

const route = read(path.join(dir, "GenericSchemaMappingEndpoints.cs"));
const runtime = read(path.join(dir, "GenericSchemaMappingEndpoints.runtime.cs"));

if (!route.includes("PPIQ_REALIZATION_T026_GENERIC_SCHEMA_MAPPING_ROUTE_SPLIT")) {
  failures.push({ file: "GenericSchemaMappingEndpoints.cs", reason: "route split marker missing" });
}

if (!runtime.includes("PPIQ_REALIZATION_T026_GENERIC_SCHEMA_MAPPING_RUNTIME_RETIRED")) {
  failures.push({ file: "GenericSchemaMappingEndpoints.runtime.cs", reason: "runtime retirement marker missing" });
}

if (runtime.includes("MapGenericSchemaMappingEndpoints") || runtime.includes("GetCatalogAsync")) {
  failures.push({ file: "GenericSchemaMappingEndpoints.runtime.cs", reason: "runtime implementation still present" });
}

const allText = required
  .filter(name => name !== "GenericSchemaMappingEndpoints.runtime.cs")
  .map(name => read(path.join(dir, name)))
  .join("\n");

for (const signal of [
  "GetCatalogAsync",
  "RegisterCanonicalViewAsync",
  "ResolveSchemaViewAsync",
  "PreviewJoinAsync",
  "MaterializeJoinAsync",
  "CreateKpiViewAsync",
  "ExecuteMappingAsync",
  "GetReadinessAsync",
  "QueryAsync",
  "RegisterCanonicalViewRequest"
]) {
  if (!allText.includes(signal)) {
    failures.push({ file: "GenericSchemaMappingEndpoints split", reason: "missing signal: " + signal });
  }
}

if (failures.length) {
  console.error("PPIQ-T026 failed: GenericSchemaMappingEndpoints split is incomplete.");
  console.error(JSON.stringify(failures, null, 2));
  process.exit(1);
}

console.log("PPIQ-T026 passed: GenericSchemaMappingEndpoints runtime shim retired and all split files are below 300 lines.");
`);

write("docs/phase5/T026_GENERIC_SCHEMA_MAPPING_SPLIT.md", `# T-026 GenericSchemaMappingEndpoints Split

Marker: PPIQ_REALIZATION_T026_GENERIC_SCHEMA_MAPPING_ROUTE_SPLIT

## Result

The previous runtime shim has been retired and the endpoint has been decomposed into cohesive partial files.

## Files

- Backend/PlantProcess.Api/Endpoints/Admin/GenericSchemaMappingEndpoints.cs
- Backend/PlantProcess.Api/Endpoints/Admin/GenericSchemaMappingEndpoints.Catalog.cs
- Backend/PlantProcess.Api/Endpoints/Admin/GenericSchemaMappingEndpoints.Resolver.cs
- Backend/PlantProcess.Api/Endpoints/Admin/GenericSchemaMappingEndpoints.Joins.cs
- Backend/PlantProcess.Api/Endpoints/Admin/GenericSchemaMappingEndpoints.Kpi.cs
- Backend/PlantProcess.Api/Endpoints/Admin/GenericSchemaMappingEndpoints.Execution.cs
- Backend/PlantProcess.Api/Endpoints/Admin/GenericSchemaMappingEndpoints.SqlHelpers.cs
- Backend/PlantProcess.Api/Endpoints/Admin/GenericSchemaMappingEndpoints.Contracts.cs

## Validation

Run:

    node tools/phase5/validate-t026-generic-schema-mapping-split.cjs
    dotnet build Backend

## Backup

${backupDir}
`);

run("node --check T-026 validator", "node", ["--check", "tools/phase5/validate-t026-generic-schema-mapping-split.cjs"]);
run("T-026 validator", "node", ["tools/phase5/validate-t026-generic-schema-mapping-split.cjs"]);
run("Backend build after T-026", "dotnet", ["build", "Backend"]);

console.log("");
console.log("=================================================================================================");
console.log("T-026 completed: GenericSchemaMappingEndpoints real split is green.");
console.log("Backup: " + backupDir);
console.log("=================================================================================================");
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

function run(name, command, args, cwd) {
  console.log("");
  console.log("---- " + name);
  cp.execFileSync(command, args, {
    cwd: cwd || root,
    stdio: "inherit",
    shell: false
  });
}

function findMethodBlock(source, methodName) {
  const pattern = new RegExp(
    "(?:public|private|internal)\\s+static\\s+(?:async\\s+)?[\\s\\S]{0,260}?\\s+" +
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

function partialFile(marker, methods) {
  const header = [
    "using System.Data;",
    "using System.Data.Common;",
    "using System.Diagnostics;",
    "using System.Security.Claims;",
    "using System.Text.Json;",
    "using System.Text.RegularExpressions;",
    "using Microsoft.AspNetCore.Mvc;",
    "using Microsoft.EntityFrameworkCore;",
    "using PlantProcess.Api.ErrorHandling;",
    "using PlantProcess.Infrastructure.Persistence;",
    "",
    "namespace PlantProcess.Api.Endpoints.Admin;",
    "",
    "// " + marker,
    "public static partial class GenericSchemaMappingEndpoints",
    "{"
  ];

  const body = methods
    .join("\n\n")
    .split("\n")
    .map(function(line) { return "    " + line; });

  const footer = [
    "}",
    ""
  ];

  return header.concat(body).concat(footer).join("\n");
}

const catalogPath = "Backend/PlantProcess.Api/Endpoints/Admin/GenericSchemaMappingEndpoints.Catalog.cs";
const catalog = read(catalogPath);

const queryMethods = [
  findMethodBlock(catalog, "GetCatalogAsync"),
  findMethodBlock(catalog, "EnsureCatalogAsync"),
  findMethodBlock(catalog, "GetCatalogByIdAsync")
];

const registrationMethods = [
  findMethodBlock(catalog, "RegisterCanonicalViewAsync"),
  findMethodBlock(catalog, "ValidateRegisterRequest"),
  findMethodBlock(catalog, "UpsertCatalogAsync")
];

write(
  "Backend/PlantProcess.Api/Endpoints/Admin/GenericSchemaMappingEndpoints.Catalog.Query.cs",
  partialFile("PPIQ_REALIZATION_T026_GENERIC_SCHEMA_MAPPING_CATALOG_QUERY_SPLIT", queryMethods)
);

write(
  "Backend/PlantProcess.Api/Endpoints/Admin/GenericSchemaMappingEndpoints.Catalog.Registration.cs",
  partialFile("PPIQ_REALIZATION_T026_GENERIC_SCHEMA_MAPPING_CATALOG_REGISTRATION_SPLIT", registrationMethods)
);

write(
  catalogPath,
  [
    "// PPIQ_REALIZATION_T026_GENERIC_SCHEMA_MAPPING_CATALOG_INDEX_RETIRED",
    "// Catalog implementation was split further because the first pass still exceeded 300 lines.",
    "// Real catalog implementation now lives in:",
    "// - GenericSchemaMappingEndpoints.Catalog.Query.cs",
    "// - GenericSchemaMappingEndpoints.Catalog.Registration.cs",
    "",
    "namespace PlantProcess.Api.Endpoints.Admin;",
    "",
    "public static partial class GenericSchemaMappingEndpoints",
    "{",
    "}",
    ""
  ].join("\n")
);

const validatorPath = "tools/phase5/validate-t026-generic-schema-mapping-split.cjs";
let validator = read(validatorPath);

validator = validator.replace(
  '"GenericSchemaMappingEndpoints.Catalog.cs",',
  [
    '"GenericSchemaMappingEndpoints.Catalog.cs",',
    '  "GenericSchemaMappingEndpoints.Catalog.Query.cs",',
    '  "GenericSchemaMappingEndpoints.Catalog.Registration.cs",'
  ].join("\n")
);

validator = validator.replace(
  '"RegisterCanonicalViewRequest"',
  [
    '"RegisterCanonicalViewRequest",',
    '  "PPIQ_REALIZATION_T026_GENERIC_SCHEMA_MAPPING_CATALOG_QUERY_SPLIT",',
    '  "PPIQ_REALIZATION_T026_GENERIC_SCHEMA_MAPPING_CATALOG_REGISTRATION_SPLIT"'
  ].join("\n")
);

write(validatorPath, validator);

run("node --check T-026 validator", "node", ["--check", validatorPath]);
run("T-026 validator", "node", [validatorPath]);
run("Backend build after T-026 catalog split", "dotnet", ["build", "Backend"]);

console.log("");
console.log("T-026 catalog oversize fixed. Continue to T-027 after this is green.");
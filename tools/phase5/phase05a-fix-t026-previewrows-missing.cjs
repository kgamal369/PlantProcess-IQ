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

    if (entry.name === "GenericSchemaMappingEndpoints.runtime.cs.before") {
      output.push(full);
    }
  }

  return output;
}

function findLatestBackup() {
  const files = collectBackups(p(".phase5_backup"), []);

  if (files.length === 0) {
    throw new Error("No T-026 backup found under .phase5_backup.");
  }

  files.sort(function(a, b) {
    return fs.statSync(b).mtimeMs - fs.statSync(a).mtimeMs;
  });

  return files[0];
}

function escapeRegex(text) {
  return text.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

function findMethodBlock(source, methodName) {
  const methodPattern = new RegExp(
    "^[ \\t]*(?:public|private|internal)[^\\r\\n]*\\b" + escapeRegex(methodName) + "\\s*\\(",
    "m"
  );

  const match = methodPattern.exec(source);
  if (!match) {
    throw new Error("Cannot find method signature line: " + methodName);
  }

  const start = match.index;
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

function originalUsings(source) {
  const namespaceIndex = source.indexOf("namespace PlantProcess.Api.Endpoints.Admin;");

  if (namespaceIndex < 0) {
    throw new Error("Cannot locate namespace declaration.");
  }

  return source.slice(0, namespaceIndex).trim();
}

function partialFile(usings, marker, methods) {
  return [
    usings,
    "",
    "namespace PlantProcess.Api.Endpoints.Admin;",
    "",
    "// " + marker,
    "public static partial class GenericSchemaMappingEndpoints",
    "{",
    methods.join("\n\n"),
    "}",
    ""
  ].join("\n");
}

const backupFile = findLatestBackup();
console.log("Using source backup: " + path.relative(root, backupFile).split(path.sep).join("/"));

const source = readAbsolute(backupFile);
const usings = originalUsings(source);

write(
  "Backend/PlantProcess.Api/Endpoints/Admin/GenericSchemaMappingEndpoints.PreviewRows.cs",
  partialFile(usings, "PPIQ_REALIZATION_T026_GENERIC_SCHEMA_MAPPING_PREVIEW_ROWS_SPLIT", [
    findMethodBlock(source, "PreviewRowsAsync")
  ])
);

const validatorPath = "tools/phase5/validate-t026-generic-schema-mapping-split.cjs";
let validator = read(validatorPath);

if (!validator.includes("'GenericSchemaMappingEndpoints.PreviewRows.cs'")) {
  validator = validator.replace(
    "'GenericSchemaMappingEndpoints.SqlHelpers.cs',",
    "'GenericSchemaMappingEndpoints.SqlHelpers.cs',\n  'GenericSchemaMappingEndpoints.PreviewRows.cs',"
  );
}

if (!validator.includes("'PreviewRowsAsync'")) {
  validator = validator.replace(
    "'PreviewSchemaViewRequest',",
    "'PreviewSchemaViewRequest',\n  'PreviewRowsAsync',"
  );

  if (!validator.includes("'PreviewSchemaViewRequest'")) {
    validator = validator.replace(
      "'QueryAsync',",
      "'QueryAsync',\n  'PreviewRowsAsync',"
    );
  }
}

if (!validator.includes("'PPIQ_REALIZATION_T026_GENERIC_SCHEMA_MAPPING_PREVIEW_ROWS_SPLIT'")) {
  validator = validator.replace(
    "'PPIQ_REALIZATION_T026_GENERIC_SCHEMA_MAPPING_CATALOG_REGISTRATION_SPLIT'",
    "'PPIQ_REALIZATION_T026_GENERIC_SCHEMA_MAPPING_CATALOG_REGISTRATION_SPLIT',\n  'PPIQ_REALIZATION_T026_GENERIC_SCHEMA_MAPPING_PREVIEW_ROWS_SPLIT'"
  );
}

write(validatorPath, validator);

run("node --check T-026 validator", "node", ["--check", validatorPath]);
run("T-026 validator", "node", [validatorPath]);
run("Backend build after adding PreviewRowsAsync split", "dotnet", ["build", "Backend"]);

console.log("");
console.log("T-026 PreviewRowsAsync split fixed. T-026 is now build-verifiable if the build is green.");
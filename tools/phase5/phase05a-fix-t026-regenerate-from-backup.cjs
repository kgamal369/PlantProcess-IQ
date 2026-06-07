const fs = require('fs');
const path = require('path');
const cp = require('child_process');

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
  fs.writeFileSync(target, content.replace(/\n/g, '\r\n'), 'utf8');
  console.log('Wrote: ' + relativePath);
}

function readAbsolute(file) {
  return fs.readFileSync(file, 'utf8');
}

function run(name, command, args) {
  console.log('');
  console.log('---- ' + name);
  cp.execFileSync(command, args, {
    cwd: root,
    stdio: 'inherit',
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

    if (entry.name === 'GenericSchemaMappingEndpoints.runtime.cs.before') {
      output.push(full);
    }
  }

  return output;
}

function findLatestBackup() {
  const backupRoot = p('.phase5_backup');
  const files = collectBackups(backupRoot, []);

  if (files.length === 0) {
    throw new Error('No T-026 backup found under .phase5_backup. Cannot safely regenerate.');
  }

  files.sort(function(a, b) {
    return fs.statSync(b).mtimeMs - fs.statSync(a).mtimeMs;
  });

  return files[0];
}

function escapeRegex(text) {
  return text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

function findMethodBlock(source, methodName) {
  const methodPattern = new RegExp(
    '^[ \\t]*(?:public|private|internal)[^\\r\\n]*\\b' + escapeRegex(methodName) + '\\s*\\(',
    'm'
  );

  const match = methodPattern.exec(source);
  if (!match) {
    throw new Error('Cannot find method signature line: ' + methodName);
  }

  const start = match.index;
  const braceStart = source.indexOf('{', match.index);

  if (braceStart < 0) {
    throw new Error('Cannot find opening brace for method: ' + methodName);
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
      if (ch === '\n') inLineComment = false;
      continue;
    }

    if (inBlockComment) {
      if (ch === '*' && next === '/') {
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

      if (ch === '\\') {
        i++;
        continue;
      }

      if (ch === '"') {
        inString = false;
      }

      continue;
    }

    if (inChar) {
      if (ch === '\\') {
        i++;
        continue;
      }

      if (ch === "'") {
        inChar = false;
      }

      continue;
    }

    if (ch === '/' && next === '/') {
      inLineComment = true;
      i++;
      continue;
    }

    if (ch === '/' && next === '*') {
      inBlockComment = true;
      i++;
      continue;
    }

    if (ch === '@' && next === '"') {
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

    if (ch === '{') {
      depth++;
    } else if (ch === '}') {
      depth--;

      if (depth === 0) {
        return source.slice(start, i + 1).trim();
      }
    }
  }

  throw new Error('Cannot find closing brace for method: ' + methodName);
}

function findRecordsBlock(source) {
  const first = source.indexOf('    public sealed record RegisterCanonicalViewRequest');
  if (first < 0) {
    throw new Error('Cannot find DTO record block.');
  }

  const lastBrace = source.lastIndexOf('\n}');
  if (lastBrace < first) {
    throw new Error('Cannot locate class closing brace.');
  }

  return source.slice(first, lastBrace).trim();
}

function originalUsings(source) {
  const namespaceIndex = source.indexOf('namespace PlantProcess.Api.Endpoints.Admin;');

  if (namespaceIndex < 0) {
    throw new Error('Cannot locate namespace declaration.');
  }

  return source.slice(0, namespaceIndex).trim();
}

function partialFile(usings, marker, methods) {
  return [
    usings,
    '',
    'namespace PlantProcess.Api.Endpoints.Admin;',
    '',
    '// ' + marker,
    'public static partial class GenericSchemaMappingEndpoints',
    '{',
    methods.join('\n\n'),
    '}',
    ''
  ].join('\n');
}

const backupFile = findLatestBackup();
console.log('Using source backup: ' + path.relative(root, backupFile).split(path.sep).join('/'));

const source = readAbsolute(backupFile);
const usings = originalUsings(source);

const runtimePath = 'Backend/PlantProcess.Api/Endpoints/Admin/GenericSchemaMappingEndpoints.runtime.cs';
const routePath = 'Backend/PlantProcess.Api/Endpoints/Admin/GenericSchemaMappingEndpoints.cs';

const safeIdentifierStart = source.indexOf('    private static readonly Regex SafeIdentifier');
const mapBlock = findMethodBlock(source, 'MapGenericSchemaMappingEndpoints');
const mapStart = source.indexOf(mapBlock);

if (safeIdentifierStart < 0 || mapStart < 0) {
  throw new Error('Cannot locate route field/map sections.');
}

const fieldBlock = source.slice(safeIdentifierStart, mapStart).trim();

write(routePath, [
  usings,
  '',
  'namespace PlantProcess.Api.Endpoints.Admin;',
  '',
  '/// <summary>',
  '/// PPIQ_REALIZATION_T026_GENERIC_SCHEMA_MAPPING_ROUTE_SPLIT',
  '/// Thin route registration surface. Runtime implementation was decomposed into cohesive partial files.',
  '/// </summary>',
  'public static partial class GenericSchemaMappingEndpoints',
  '{',
  fieldBlock,
  '',
  mapBlock,
  '}',
  ''
].join('\n'));

write(
  'Backend/PlantProcess.Api/Endpoints/Admin/GenericSchemaMappingEndpoints.Catalog.cs',
  [
    '// PPIQ_REALIZATION_T026_GENERIC_SCHEMA_MAPPING_CATALOG_INDEX_RETIRED',
    '// Catalog implementation is split into query and registration partial files.',
    '',
    'namespace PlantProcess.Api.Endpoints.Admin;',
    '',
    'public static partial class GenericSchemaMappingEndpoints',
    '{',
    '}',
    ''
  ].join('\n')
);

write(
  'Backend/PlantProcess.Api/Endpoints/Admin/GenericSchemaMappingEndpoints.Catalog.Query.cs',
  partialFile(usings, 'PPIQ_REALIZATION_T026_GENERIC_SCHEMA_MAPPING_CATALOG_QUERY_SPLIT', [
    findMethodBlock(source, 'GetCatalogAsync'),
    findMethodBlock(source, 'EnsureCatalogAsync'),
    findMethodBlock(source, 'GetCatalogByIdAsync')
  ])
);

write(
  'Backend/PlantProcess.Api/Endpoints/Admin/GenericSchemaMappingEndpoints.Catalog.Registration.cs',
  partialFile(usings, 'PPIQ_REALIZATION_T026_GENERIC_SCHEMA_MAPPING_CATALOG_REGISTRATION_SPLIT', [
    findMethodBlock(source, 'RegisterCanonicalViewAsync'),
    findMethodBlock(source, 'ValidateRegisterRequest'),
    findMethodBlock(source, 'UpsertCatalogAsync')
  ])
);

write(
  'Backend/PlantProcess.Api/Endpoints/Admin/GenericSchemaMappingEndpoints.Resolver.cs',
  partialFile(usings, 'PPIQ_REALIZATION_T026_GENERIC_SCHEMA_MAPPING_RESOLVER_SPLIT', [
    findMethodBlock(source, 'ResolveSchemaViewAsync'),
    findMethodBlock(source, 'ValidateRequestedResolverColumns')
  ])
);

write(
  'Backend/PlantProcess.Api/Endpoints/Admin/GenericSchemaMappingEndpoints.Joins.cs',
  partialFile(usings, 'PPIQ_REALIZATION_T026_GENERIC_SCHEMA_MAPPING_JOIN_SPLIT', [
    findMethodBlock(source, 'PreviewJoinAsync'),
    findMethodBlock(source, 'MaterializeJoinAsync'),
    findMethodBlock(source, 'BuildJoinSql')
  ])
);

write(
  'Backend/PlantProcess.Api/Endpoints/Admin/GenericSchemaMappingEndpoints.Kpi.cs',
  partialFile(usings, 'PPIQ_REALIZATION_T026_GENERIC_SCHEMA_MAPPING_KPI_SPLIT', [
    findMethodBlock(source, 'CreateKpiViewAsync'),
    findMethodBlock(source, 'TryInsertKpiDefinitionAsync')
  ])
);

write(
  'Backend/PlantProcess.Api/Endpoints/Admin/GenericSchemaMappingEndpoints.Execution.cs',
  partialFile(usings, 'PPIQ_REALIZATION_T026_GENERIC_SCHEMA_MAPPING_EXECUTION_SPLIT', [
    findMethodBlock(source, 'ExecuteMappingAsync'),
    findMethodBlock(source, 'GetReadinessAsync'),
    findMethodBlock(source, 'CreateOrReplaceViewAsync'),
    findMethodBlock(source, 'PreviewSchemaOnlyAsync'),
    findMethodBlock(source, 'CountRowsAsync')
  ])
);

write(
  'Backend/PlantProcess.Api/Endpoints/Admin/GenericSchemaMappingEndpoints.SqlHelpers.cs',
  partialFile(usings, 'PPIQ_REALIZATION_T026_GENERIC_SCHEMA_MAPPING_SQL_HELPERS_SPLIT', [
    findMethodBlock(source, 'NormalizeSelectSql'),
    findMethodBlock(source, 'StripTrailingSemicolon'),
    findMethodBlock(source, 'QueryAsync'),
    findMethodBlock(source, 'ExecuteNonQueryAsync'),
    findMethodBlock(source, 'AddParameter'),
    findMethodBlock(source, 'CleanIdentifier'),
    findMethodBlock(source, 'QuoteIdentifier'),
    findMethodBlock(source, 'NormalizeCode'),
    findMethodBlock(source, 'EmptyToNull'),
    findMethodBlock(source, 'GetActor')
  ])
);

write(
  'Backend/PlantProcess.Api/Endpoints/Admin/GenericSchemaMappingEndpoints.Contracts.cs',
  partialFile(usings, 'PPIQ_REALIZATION_T026_GENERIC_SCHEMA_MAPPING_CONTRACTS_SPLIT', [
    findRecordsBlock(source)
  ])
);

write(runtimePath, [
  '// PPIQ_REALIZATION_T026_GENERIC_SCHEMA_MAPPING_RUNTIME_RETIRED',
  '// GenericSchemaMappingEndpoints.runtime.cs was retired by Phase 05 T-026.',
  '// Real implementation now lives in cohesive partial files.',
  '',
  'namespace PlantProcess.Api.Endpoints.Admin;',
  '',
  'public static partial class GenericSchemaMappingEndpoints',
  '{',
  '}',
  ''
].join('\n'));

write('tools/phase5/validate-t026-generic-schema-mapping-split.cjs', [
  "const fs = require('fs');",
  "const path = require('path');",
  '',
  'const root = process.cwd();',
  "const dir = path.join(root, 'Backend', 'PlantProcess.Api', 'Endpoints', 'Admin');",
  'const failures = [];',
  '',
  'function rel(file) {',
  "  return path.relative(root, file).split(path.sep).join('/');",
  '}',
  '',
  'function read(file) {',
  "  return fs.readFileSync(file, 'utf8');",
  '}',
  '',
  'function lineCount(text) {',
  "  return text.replace(/\\r\\n/g, '\\n').split('\\n').length;",
  '}',
  '',
  'const required = [',
  "  'GenericSchemaMappingEndpoints.cs',",
  "  'GenericSchemaMappingEndpoints.Catalog.cs',",
  "  'GenericSchemaMappingEndpoints.Catalog.Query.cs',",
  "  'GenericSchemaMappingEndpoints.Catalog.Registration.cs',",
  "  'GenericSchemaMappingEndpoints.Resolver.cs',",
  "  'GenericSchemaMappingEndpoints.Joins.cs',",
  "  'GenericSchemaMappingEndpoints.Kpi.cs',",
  "  'GenericSchemaMappingEndpoints.Execution.cs',",
  "  'GenericSchemaMappingEndpoints.SqlHelpers.cs',",
  "  'GenericSchemaMappingEndpoints.Contracts.cs',",
  "  'GenericSchemaMappingEndpoints.runtime.cs'",
  '];',
  '',
  'for (const name of required) {',
  '  const file = path.join(dir, name);',
  '',
  '  if (!fs.existsSync(file)) {',
  "    failures.push({ file: rel(file), reason: 'required split file missing' });",
  '    continue;',
  '  }',
  '',
  '  const text = read(file);',
  '  const lines = lineCount(text);',
  '',
  '  if (lines > 300) {',
  "    failures.push({ file: rel(file), reason: 'file exceeds 300 lines', lines });",
  '  }',
  '}',
  '',
  "const route = read(path.join(dir, 'GenericSchemaMappingEndpoints.cs'));",
  "const runtime = read(path.join(dir, 'GenericSchemaMappingEndpoints.runtime.cs'));",
  "const allText = required.map(function(name) { return read(path.join(dir, name)); }).join('\\n');",
  '',
  "if (!route.includes('PPIQ_REALIZATION_T026_GENERIC_SCHEMA_MAPPING_ROUTE_SPLIT')) {",
  "  failures.push({ file: 'GenericSchemaMappingEndpoints.cs', reason: 'route split marker missing' });",
  '}',
  '',
  "if (!runtime.includes('PPIQ_REALIZATION_T026_GENERIC_SCHEMA_MAPPING_RUNTIME_RETIRED')) {",
  "  failures.push({ file: 'GenericSchemaMappingEndpoints.runtime.cs', reason: 'runtime retirement marker missing' });",
  '}',
  '',
  "if (runtime.includes('GetCatalogAsync') || runtime.includes('RegisterCanonicalViewAsync')) {",
  "  failures.push({ file: 'GenericSchemaMappingEndpoints.runtime.cs', reason: 'runtime implementation still present' });",
  '}',
  '',
  'for (const signal of [',
  "  'GetCatalogAsync',",
  "  'RegisterCanonicalViewAsync',",
  "  'ResolveSchemaViewAsync',",
  "  'PreviewJoinAsync',",
  "  'MaterializeJoinAsync',",
  "  'CreateKpiViewAsync',",
  "  'ExecuteMappingAsync',",
  "  'GetReadinessAsync',",
  "  'QueryAsync',",
  "  'RegisterCanonicalViewRequest',",
  "  'PPIQ_REALIZATION_T026_GENERIC_SCHEMA_MAPPING_CATALOG_QUERY_SPLIT',",
  "  'PPIQ_REALIZATION_T026_GENERIC_SCHEMA_MAPPING_CATALOG_REGISTRATION_SPLIT'",
  ']) {',
  '  if (!allText.includes(signal)) {',
  "    failures.push({ file: 'GenericSchemaMappingEndpoints split', reason: 'missing signal: ' + signal });",
  '  }',
  '}',
  '',
  'if (failures.length) {',
  "  console.error('PPIQ-T026 failed: GenericSchemaMappingEndpoints split is incomplete.');",
  '  console.error(JSON.stringify(failures, null, 2));',
  '  process.exit(1);',
  '}',
  '',
  "console.log('PPIQ-T026 passed: GenericSchemaMappingEndpoints runtime shim retired and all split files are below 300 lines.');",
  ''
].join('\n'));

run('node --check T-026 validator', 'node', ['--check', 'tools/phase5/validate-t026-generic-schema-mapping-split.cjs']);
run('T-026 validator', 'node', ['tools/phase5/validate-t026-generic-schema-mapping-split.cjs']);
run('Backend build after T-026 regenerate', 'dotnet', ['build', 'Backend']);

console.log('');
console.log('T-026 regenerated cleanly from backup and validated.');
const fs = require('fs');
const path = require('path');
const cp = require('child_process');

const root = process.cwd();
const timestamp = new Date().toISOString().replace(/[-:T.Z]/g, '').slice(0, 14);
const backupRoot = path.join(root, '.phase5b_backup', 'product_core_types_' + timestamp);

const sourceRel = 'Frontend/PlantProcess.Web/src/api/productCoreApiClient.implementation.ts';
const sourcePath = path.join(root, sourceRel);
const typesRel = 'Frontend/PlantProcess.Web/src/api/product-core/types.ts';
const typesPath = path.join(root, typesRel);
const validatorPath = path.join(root, 'tools/phase5b/validate-product-core-type-split.cjs');
const evidencePath = path.join(root, 'docs/phase5b/PHASE5B_1_PRODUCT_CORE_TYPE_SPLIT.md');

const marker = 'PPIQ_PHASE5B_PRODUCT_CORE_TYPES_SPLIT';

function normalize(p) {
  return p.split(path.sep).join('/');
}

function rel(p) {
  return normalize(path.relative(root, p));
}

function ensureDir(dir) {
  fs.mkdirSync(dir, { recursive: true });
}

function readText(file) {
  return fs.readFileSync(file, 'utf8');
}

function writeText(file, content) {
  ensureDir(path.dirname(file));
  fs.writeFileSync(file, content.replace(/\n/g, '\r\n'), 'utf8');
  console.log('Wrote: ' + rel(file));
}

function backup(file) {
  if (!fs.existsSync(file)) return;

  const target = path.join(backupRoot, path.relative(root, file));
  ensureDir(path.dirname(target));
  fs.copyFileSync(file, target);
}

function run(name, command, cwd = root) {
  console.log('');
  console.log('---- ' + name);
  cp.execFileSync(command[0], command.slice(1), {
    cwd,
    stdio: 'inherit',
    shell: false
  });
}

function loadTypeScript() {
  const candidates = [
    path.join(root, 'Frontend', 'PlantProcess.Web', 'node_modules', 'typescript'),
    'typescript'
  ];

  for (const candidate of candidates) {
    try {
      return require(candidate);
    } catch {
      // try next
    }
  }

  throw new Error('Could not load TypeScript compiler API. Run npm install in Frontend/PlantProcess.Web first.');
}

function getModifiers(ts, node) {
  if (typeof ts.canHaveModifiers === 'function' && typeof ts.getModifiers === 'function') {
    return ts.canHaveModifiers(node) ? (ts.getModifiers(node) || []) : [];
  }

  return node.modifiers || [];
}

function hasExportModifier(ts, node) {
  return getModifiers(ts, node).some((modifier) => modifier.kind === ts.SyntaxKind.ExportKeyword);
}

function statementKind(ts, statement) {
  if (ts.isInterfaceDeclaration(statement)) return 'interface';
  if (ts.isTypeAliasDeclaration(statement)) return 'type';
  return null;
}

function lineCount(text) {
  return text.replace(/\r\n/g, '\n').split('\n').length;
}

function unique(values) {
  return Array.from(new Set(values));
}

function formatTypeList(names) {
  return names.map((name) => '  ' + name + ',').join('\n');
}

function insertAfterImports(ts, sourceFile, source, block) {
  const importStatements = sourceFile.statements.filter((statement) => ts.isImportDeclaration(statement));

  if (importStatements.length === 0) {
    return block + '\n' + source;
  }

  const lastImport = importStatements[importStatements.length - 1];
  return source.slice(0, lastImport.end) + '\n' + block + source.slice(lastImport.end);
}

function removeSegments(source, segments) {
  let result = source;

  for (const segment of segments.sort((a, b) => b.start - a.start)) {
    result = result.slice(0, segment.start) + '\n' + result.slice(segment.end);
  }

  return result
    .replace(/\n{4,}/g, '\n\n\n')
    .replace(/^\s+/, '');
}

function collectExportedTypes(ts, sourceFile, source) {
  const collected = [];

  for (const statement of sourceFile.statements) {
    const kind = statementKind(ts, statement);

    if (!kind) continue;
    if (!hasExportModifier(ts, statement)) continue;

    const name = statement.name && statement.name.text;

    if (!name) continue;

    collected.push({
      kind,
      name,
      start: statement.getFullStart(),
      end: statement.end,
      text: source.slice(statement.getFullStart(), statement.end).trim()
    });
  }

  return collected;
}

function escapeRegExp(value) {
  return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

function isNameUsedInText(name, text) {
  return new RegExp('\\b' + escapeRegExp(name) + '\\b').test(text);
}

function writeValidator(typeNames) {
  const validator = `const fs = require('fs');
const path = require('path');
const cp = require('child_process');

const root = process.cwd();
const sourcePath = path.join(root, '${sourceRel}');
const typesPath = path.join(root, '${typesRel}');

function read(file) {
  if (!fs.existsSync(file)) {
    throw new Error('Missing file: ' + path.relative(root, file));
  }

  return fs.readFileSync(file, 'utf8');
}

const source = read(sourcePath);
const types = read(typesPath);

if (!source.includes('${marker}')) {
  throw new Error('Missing ${marker} marker in productCoreApiClient.implementation.ts');
}

if (!source.includes('from "./product-core/types"')) {
  throw new Error('productCoreApiClient.implementation.ts does not import/export product-core/types');
}

if (!types.includes('export interface') && !types.includes('export type')) {
  throw new Error('product-core/types.ts does not contain exported API types');
}

const forbiddenTopLevel = /^export\\s+(interface|type)\\s+([A-Za-z_$][\\w$]*)/gm;
const offenders = [];
let match;

while ((match = forbiddenTopLevel.exec(source)) !== null) {
  offenders.push(match[0]);
}

if (offenders.length > 0) {
  throw new Error('Exported type/interface declarations still remain in implementation file: ' + offenders.join(', '));
}

const requiredTypeNames = ${JSON.stringify(typeNames, null, 2)};

for (const typeName of requiredTypeNames) {
  const typeRegex = new RegExp('export\\\\s+(interface|type)\\\\s+' + typeName + '\\\\b');

  if (!typeRegex.test(types)) {
    throw new Error('Missing moved API type in product-core/types.ts: ' + typeName);
  }
}

cp.execFileSync('node', ['tools/phase56/validate-phase56.cjs'], {
  cwd: root,
  stdio: 'inherit'
});

console.log('Phase 5B-1 product core type split validation passed.');
`;

  writeText(validatorPath, validator);
}

function main() {
  if (!fs.existsSync(sourcePath)) {
    throw new Error('Missing source file: ' + sourceRel);
  }

  ensureDir(backupRoot);

  console.log('=================================================================================================');
  console.log('PlantProcess IQ Phase 5B-1 Product Core API Type Split');
  console.log('=================================================================================================');
  console.log('Project root : ' + root);
  console.log('Backup folder: ' + backupRoot);

  const ts = loadTypeScript();
  const original = readText(sourcePath);

  backup(sourcePath);
  backup(typesPath);
  backup(validatorPath);
  backup(evidencePath);

  if (original.includes(marker) && fs.existsSync(typesPath)) {
    console.log('Product core type split marker already exists. Skipping extraction and running validation.');
    const existingTypes = readText(typesPath);
    const names = unique(
      Array.from(existingTypes.matchAll(/^export\s+(?:interface|type)\s+([A-Za-z_$][\w$]*)/gm))
        .map((match) => match[1])
    );

    writeValidator(names);
    run('node --check product-core type split validator', ['node', '--check', 'tools/phase5b/validate-product-core-type-split.cjs']);
    run('Phase 5B-1 validator', ['node', 'tools/phase5b/validate-product-core-type-split.cjs']);
    return;
  }

  const sourceFile = ts.createSourceFile(
    sourceRel,
    original,
    ts.ScriptTarget.Latest,
    true,
    ts.ScriptKind.TS
  );

  const moved = collectExportedTypes(ts, sourceFile, original);

  if (moved.length === 0) {
    throw new Error('No exported interface/type declarations found to move.');
  }

  const movedNames = unique(moved.map((item) => item.name)).sort((a, b) => a.localeCompare(b));
  let implementationWithoutTypes = removeSegments(original, moved.map((item) => ({ start: item.start, end: item.end })));

  const remainingSourceForUsage = implementationWithoutTypes;
  const namesUsedByImplementation = movedNames.filter((name) => isNameUsedInText(name, remainingSourceForUsage));

  const typeBody = moved
    .sort((a, b) => a.start - b.start)
    .map((item) => item.text)
    .join('\n\n');

  let typeImports = '';

  if (/\bQueryParams\b/.test(typeBody)) {
    typeImports += 'import type { QueryParams } from "../productApiHardening";\n';
  }

  const typeFileContent = [
    '// PlantProcess IQ Phase 5B-1',
    '// Exported product-core API DTO/filter/read-model types extracted from productCoreApiClient.implementation.ts.',
    '// Runtime behavior stays in the implementation file; this module is type-only.',
    '',
    typeImports.trim(),
    '',
    typeBody,
    ''
  ]
    .filter((part) => part !== '')
    .join('\n');

  const typeImportBlockParts = [
    '// ' + marker,
    '// Exported DTO/filter/read-model types moved to ./product-core/types.',
    'export type {',
    formatTypeList(movedNames),
    '} from "./product-core/types";'
  ];

  if (namesUsedByImplementation.length > 0) {
    typeImportBlockParts.push('');
    typeImportBlockParts.push('import type {');
    typeImportBlockParts.push(formatTypeList(namesUsedByImplementation));
    typeImportBlockParts.push('} from "./product-core/types";');
  }

  const typeImportBlock = typeImportBlockParts.join('\n');

  const refreshedSourceFile = ts.createSourceFile(
    sourceRel,
    implementationWithoutTypes,
    ts.ScriptTarget.Latest,
    true,
    ts.ScriptKind.TS
  );

  const finalImplementation = insertAfterImports(ts, refreshedSourceFile, implementationWithoutTypes, typeImportBlock)
    .replace(/\n{4,}/g, '\n\n\n');

  writeText(typesPath, typeFileContent);
  writeText(sourcePath, finalImplementation);

  writeValidator(movedNames);

  const evidence = [
    '# Phase 5B-1 Product Core API Type Split',
    '',
    'Generated: ' + new Date().toISOString(),
    '',
    '## Scope',
    '',
    'This step performs the safest first decomposition of `productCoreApiClient.implementation.ts`.',
    '',
    '- Moves exported `type` and `interface` declarations to `src/api/product-core/types.ts`.',
    '- Keeps runtime API functions and the `productApi` object in the implementation file.',
    '- Re-exports all moved public API types from `productCoreApiClient.implementation.ts` to preserve imports.',
    '- Imports back only the moved types still used by runtime function signatures.',
    '',
    '## Counts',
    '',
    '- Moved exported type/interface declarations: ' + movedNames.length,
    '- Types still needed by implementation signatures: ' + namesUsedByImplementation.length,
    '- Original implementation lines: ' + lineCount(original),
    '- New implementation lines: ' + lineCount(finalImplementation),
    '- New type module lines: ' + lineCount(typeFileContent),
    '',
    '## Moved type names',
    '',
    ...movedNames.map((name) => '- `' + name + '`'),
    '',
    '## Validation',
    '',
    '- `node tools/phase5b/validate-product-core-type-split.cjs`',
    '- `powershell -ExecutionPolicy Bypass -File .\\tools\\phase56\\Invoke-Phase5Phase6Validation.ps1 -ProjectRoot "C:\\Workspace\\PlantProcess-IQ" -RunFrontendBuild`',
    ''
  ].join('\n');

  writeText(evidencePath, evidence);

  run('node --check product-core type split validator', ['node', '--check', 'tools/phase5b/validate-product-core-type-split.cjs']);
  run('Phase 5B-1 validator', ['node', 'tools/phase5b/validate-product-core-type-split.cjs']);

  if (fs.existsSync(path.join(root, 'tools', 'phase5b', 'analyze-phase5b-refactor.cjs'))) {
    run('Refresh Phase 5B refactor report after product-core type split', ['node', 'tools/phase5b/analyze-phase5b-refactor.cjs']);
  }

  console.log('');
  console.log('=================================================================================================');
  console.log('Phase 5B-1 product core API type split completed.');
  console.log('Moved exported type/interface declarations: ' + movedNames.length);
  console.log('Old implementation lines: ' + lineCount(original));
  console.log('New implementation lines: ' + lineCount(finalImplementation));
  console.log('Type module lines: ' + lineCount(typeFileContent));
  console.log('Evidence: ' + rel(evidencePath));
  console.log('Backup  : ' + backupRoot);
  console.log('=================================================================================================');
}

main();
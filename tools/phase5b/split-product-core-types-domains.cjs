const fs = require('fs');
const path = require('path');
const cp = require('child_process');

const root = process.cwd();
const timestamp = new Date().toISOString().replace(/[-:T.Z]/g, '').slice(0, 14);
const backupRoot = path.join(root, '.phase5b_backup', 'product_core_domain_types_' + timestamp);

const apiDir = path.join(root, 'Frontend', 'PlantProcess.Web', 'src', 'api');
const productCoreDir = path.join(apiDir, 'product-core');

const implementationRel = 'Frontend/PlantProcess.Web/src/api/productCoreApiClient.implementation.ts';
const implementationPath = path.join(root, implementationRel);

const barrelRel = 'Frontend/PlantProcess.Web/src/api/product-core/types.ts';
const barrelPath = path.join(root, barrelRel);

const manifestRel = 'Frontend/PlantProcess.Web/src/api/product-core/product-core-types.manifest.json';
const manifestPath = path.join(root, manifestRel);

const validatorRel = 'tools/phase5b/validate-product-core-type-split.cjs';
const validatorPath = path.join(root, validatorRel);

const evidenceRel = 'docs/phase5b/PHASE5B_1_PRODUCT_CORE_TYPE_SPLIT.md';
const evidencePath = path.join(root, evidenceRel);

const domainMarker = 'PPIQ_PHASE5B_PRODUCT_CORE_TYPES_DOMAIN_SPLIT';
const firstSplitMarker = 'PPIQ_PHASE5B_PRODUCT_CORE_TYPES_SPLIT';

const maxModuleLines = 420;

function normalize(filePath) {
  return filePath.split(path.sep).join('/');
}

function rel(filePath) {
  return normalize(path.relative(root, filePath));
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

function lineCount(text) {
  return text.replace(/\r\n/g, '\n').split('\n').length;
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
      // try next candidate
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

function isTypeDeclaration(ts, statement) {
  return ts.isInterfaceDeclaration(statement) || ts.isTypeAliasDeclaration(statement);
}

function isImportDeclaration(ts, statement) {
  return ts.isImportDeclaration(statement);
}

function collectImportedNames(ts, statement) {
  const names = [];

  if (!statement.importClause) return names;

  if (statement.importClause.name) {
    names.push(statement.importClause.name.text);
  }

  const namedBindings = statement.importClause.namedBindings;

  if (!namedBindings) return names;

  if (ts.isNamespaceImport(namedBindings)) {
    names.push(namedBindings.name.text);
  }

  if (ts.isNamedImports(namedBindings)) {
    for (const element of namedBindings.elements) {
      names.push(element.name.text);
    }
  }

  return names;
}

function escapeRegExp(value) {
  return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

function containsName(text, name) {
  return new RegExp('\\b' + escapeRegExp(name) + '\\b').test(text);
}

function stripExtension(fileName) {
  return fileName.replace(/\.ts$/, '');
}

function sanitizeFileName(value) {
  return value
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '');
}

function classifyTypeName(name) {
  const n = name.toLowerCase();

  if (/dashboard|widget|pagebuilder|builder|layout|grid|tile|visual|chart/.test(n)) return 'dashboard-widget-types';
  if (/material|coil|slab|heat|genealogy|unit|process|route|operation|production/.test(n)) return 'material-process-types';
  if (/quality|defect|inspection|risk|correlation|analytics|analysis|metric|kpi|trend|finding/.test(n)) return 'analytics-quality-types';
  if (/admin|schema|mapping|configuration|config|import|job|connector|source|database|tenant/.test(n)) return 'admin-mapping-types';
  if (/license|commercial|feature|plan|pricing|gate|subscription/.test(n)) return 'license-commercial-types';
  if (/workflow|approval|task|status|event|timeline|notification/.test(n)) return 'workflow-types';
  if (/demo|scenario|readiness|brand|proof|evidence/.test(n)) return 'demo-proof-types';

  return 'shared-types';
}

function collectDeclarations(ts, source, sourceFile) {
  const declarations = [];

  for (const statement of sourceFile.statements) {
    if (!isTypeDeclaration(ts, statement)) continue;
    if (!hasExportModifier(ts, statement)) continue;
    if (!statement.name || !statement.name.text) continue;

    declarations.push({
      name: statement.name.text,
      start: statement.getFullStart(),
      end: statement.end,
      text: source.slice(statement.getFullStart(), statement.end).trim()
    });
  }

  return declarations;
}

function collectImports(ts, source, sourceFile) {
  const imports = [];

  for (const statement of sourceFile.statements) {
    if (!isImportDeclaration(ts, statement)) continue;

    const moduleSpecifier = statement.moduleSpecifier && statement.moduleSpecifier.text;

    imports.push({
      text: source.slice(statement.getFullStart(), statement.end).trim(),
      names: collectImportedNames(ts, statement),
      moduleSpecifier
    });
  }

  return imports;
}

function makeChunks(declarations) {
  const byGroup = new Map();

  for (const declaration of declarations) {
    const group = classifyTypeName(declaration.name);

    if (!byGroup.has(group)) {
      byGroup.set(group, []);
    }

    byGroup.get(group).push(declaration);
  }

  const modules = [];

  for (const [group, items] of Array.from(byGroup.entries()).sort((a, b) => a[0].localeCompare(b[0]))) {
    let chunk = [];
    let chunkLineCount = 0;
    let chunkIndex = 1;

    for (const item of items) {
      const itemLines = lineCount(item.text);
      const wouldExceed = chunk.length > 0 && chunkLineCount + itemLines > maxModuleLines;

      if (wouldExceed) {
        const fileBase = items.length === chunk.length ? group : group + '-' + chunkIndex;
        modules.push({
          group,
          fileName: sanitizeFileName(fileBase) + '.ts',
          declarations: chunk
        });

        chunk = [];
        chunkLineCount = 0;
        chunkIndex += 1;
      }

      chunk.push(item);
      chunkLineCount += itemLines;
    }

    if (chunk.length > 0) {
      const needsSuffix = chunkIndex > 1 || chunkLineCount > maxModuleLines;
      const fileBase = needsSuffix ? group + '-' + chunkIndex : group;

      modules.push({
        group,
        fileName: sanitizeFileName(fileBase) + '.ts',
        declarations: chunk
      });
    }
  }

  return modules;
}

function moduleText(tsImports, module, typeNameToFile) {
  const ownNames = new Set(module.declarations.map((declaration) => declaration.name));
  const ownText = module.declarations.map((declaration) => declaration.text).join('\n\n');

  const importLines = [];

  for (const originalImport of tsImports) {
    if (originalImport.moduleSpecifier && originalImport.moduleSpecifier.startsWith('./')) {
      continue;
    }

    const used = originalImport.names.some((name) => containsName(ownText, name));

    if (used) {
      importLines.push(originalImport.text);
    }
  }

  const dependencyByFile = new Map();

  for (const [typeName, fileName] of typeNameToFile.entries()) {
    if (ownNames.has(typeName)) continue;
    if (!containsName(ownText, typeName)) continue;

    if (!dependencyByFile.has(fileName)) {
      dependencyByFile.set(fileName, []);
    }

    dependencyByFile.get(fileName).push(typeName);
  }

  for (const [fileName, typeNames] of Array.from(dependencyByFile.entries()).sort((a, b) => a[0].localeCompare(b[0]))) {
    if (fileName === module.fileName) continue;

    const names = Array.from(new Set(typeNames)).sort((a, b) => a.localeCompare(b));
    importLines.push(`import type { ${names.join(', ')} } from "./${stripExtension(fileName)}";`);
  }

  const header = [
    '// PlantProcess IQ Phase 5B-1 domain type module.',
    '// Generated from productCoreApiClient.implementation.ts exported DTO/filter/read-model declarations.',
    '// Runtime API behavior remains in productCoreApiClient.implementation.ts.',
    ''
  ];

  return [
    ...header,
    ...Array.from(new Set(importLines)).sort(),
    importLines.length > 0 ? '' : '',
    ownText,
    ''
  ].join('\n');
}

function writeValidator(allTypeNames, moduleFiles) {
  const validator = `const fs = require('fs');
const path = require('path');
const cp = require('child_process');

const root = process.cwd();

const implementationPath = path.join(root, '${implementationRel}');
const barrelPath = path.join(root, '${barrelRel}');
const manifestPath = path.join(root, '${manifestRel}');

function read(file) {
  if (!fs.existsSync(file)) throw new Error('Missing file: ' + path.relative(root, file));
  return fs.readFileSync(file, 'utf8');
}

const implementation = read(implementationPath);
const barrel = read(barrelPath);
const manifest = JSON.parse(read(manifestPath));

if (!implementation.includes('${firstSplitMarker}')) {
  throw new Error('Missing ${firstSplitMarker} marker in implementation file.');
}

if (!barrel.includes('${domainMarker}')) {
  throw new Error('Missing ${domainMarker} marker in product-core/types.ts barrel.');
}

if (!implementation.includes('from "./product-core/types"')) {
  throw new Error('Implementation does not import/export the product-core barrel.');
}

const forbiddenTopLevel = /^export\\s+(interface|type)\\s+([A-Za-z_$][\\w$]*)/gm;
const offenders = [];
let match;

while ((match = forbiddenTopLevel.exec(implementation)) !== null) {
  offenders.push(match[0]);
}

if (offenders.length > 0) {
  throw new Error('Exported type/interface declarations still remain in implementation file: ' + offenders.join(', '));
}

const requiredTypeNames = ${JSON.stringify(allTypeNames, null, 2)};

for (const typeName of requiredTypeNames) {
  const declarationRegex = new RegExp('export\\\\s+(interface|type)\\\\s+' + typeName + '\\\\b');
  const moduleEntry = manifest.types.find((entry) => entry.name === typeName);

  if (!moduleEntry) {
    throw new Error('Missing manifest entry for type: ' + typeName);
  }

  const modulePath = path.join(root, 'Frontend', 'PlantProcess.Web', 'src', 'api', 'product-core', moduleEntry.file);
  const moduleText = read(modulePath);

  if (!declarationRegex.test(moduleText)) {
    throw new Error('Type not found in declared module: ' + typeName + ' -> ' + moduleEntry.file);
  }

  if (!barrel.includes(moduleEntry.file.replace(/\\.ts$/, ''))) {
    throw new Error('Barrel does not re-export module: ' + moduleEntry.file);
  }
}

for (const moduleFile of ${JSON.stringify(moduleFiles, null, 2)}) {
  const modulePath = path.join(root, 'Frontend', 'PlantProcess.Web', 'src', 'api', 'product-core', moduleFile);
  const moduleText = read(modulePath);
  const lines = moduleText.split(/\\r?\\n/).length;

  if (lines > 700) {
    throw new Error(moduleFile + ' is still too large: ' + lines + ' lines');
  }
}

cp.execFileSync('node', ['tools/phase56/validate-phase56.cjs'], {
  cwd: root,
  stdio: 'inherit'
});

console.log('Phase 5B-1 product-core domain type split validation passed.');
`;

  writeText(validatorPath, validator);
}

function writeManifest(allTypeNames, modules) {
  const entries = [];

  for (const module of modules) {
    for (const declaration of module.declarations) {
      entries.push({
        name: declaration.name,
        file: module.fileName,
        group: module.group
      });
    }
  }

  const manifest = {
    generatedAtUtc: new Date().toISOString(),
    marker: domainMarker,
    source: barrelRel,
    moduleCount: modules.length,
    typeCount: allTypeNames.length,
    types: entries.sort((a, b) => a.name.localeCompare(b.name))
  };

  writeText(manifestPath, JSON.stringify(manifest, null, 2) + '\n');
}

function writeEvidence(originalBarrel, newBarrel, modules, allTypeNames) {
  const lines = [];

  lines.push('# Phase 5B-1 Product Core API Type Split');
  lines.push('');
  lines.push('Generated: ' + new Date().toISOString());
  lines.push('');
  lines.push('## What changed');
  lines.push('');
  lines.push('- `productCoreApiClient.implementation.ts` no longer owns exported DTO/filter/read-model declarations.');
  lines.push('- `product-core/types.ts` is now a thin barrel export.');
  lines.push('- Product-core API types are split into domain modules under `src/api/product-core`.');
  lines.push('- Runtime API behavior remains in `productCoreApiClient.implementation.ts`.');
  lines.push('');
  lines.push('## Counts');
  lines.push('');
  lines.push('- Original barrel/type file lines before domain split: ' + lineCount(originalBarrel));
  lines.push('- New barrel lines: ' + lineCount(newBarrel));
  lines.push('- Domain module count: ' + modules.length);
  lines.push('- Exported type/interface declarations: ' + allTypeNames.length);
  lines.push('');
  lines.push('## Domain modules');
  lines.push('');
  lines.push('| File | Group | Declarations | Lines |');
  lines.push('|---|---|---:|---:|');

  for (const module of modules) {
    const modulePath = path.join(productCoreDir, module.fileName);
    const moduleContent = readText(modulePath);

    lines.push(`| \`${module.fileName}\` | ${module.group} | ${module.declarations.length} | ${lineCount(moduleContent)} |`);
  }

  lines.push('');
  lines.push('## Validation');
  lines.push('');
  lines.push('```powershell');
  lines.push('node tools/phase5b/validate-product-core-type-split.cjs');
  lines.push('powershell -ExecutionPolicy Bypass -File .\\tools\\phase56\\Invoke-Phase5Phase6Validation.ps1 -ProjectRoot "C:\\Workspace\\PlantProcess-IQ" -RunFrontendBuild');
  lines.push('```');
  lines.push('');

  writeText(evidencePath, lines.join('\n') + '\n');
}

function alreadyDomainSplit() {
  if (!fs.existsSync(barrelPath)) return false;
  return readText(barrelPath).includes(domainMarker) && fs.existsSync(manifestPath);
}

function validateOnly() {
  const manifest = JSON.parse(readText(manifestPath));
  writeValidator(manifest.types.map((entry) => entry.name), Array.from(new Set(manifest.types.map((entry) => entry.file))));
  run('node --check product-core domain type split validator', ['node', '--check', validatorRel]);
  run('Phase 5B-1 domain type split validator', ['node', validatorRel]);
}

function main() {
  console.log('=================================================================================================');
  console.log('PlantProcess IQ Phase 5B-1 Repair — Product Core Domain Type Split');
  console.log('=================================================================================================');
  console.log('Project root : ' + root);
  console.log('Backup folder: ' + backupRoot);

  ensureDir(backupRoot);

  if (!fs.existsSync(barrelPath)) {
    throw new Error('Missing current product-core type barrel/source file: ' + barrelRel);
  }

  backup(barrelPath);
  backup(manifestPath);
  backup(validatorPath);
  backup(evidencePath);

  for (const file of fs.existsSync(productCoreDir) ? fs.readdirSync(productCoreDir) : []) {
    if (/^(dashboard-widget|material-process|analytics-quality|admin-mapping|license-commercial|workflow|demo-proof|shared)-types(-\d+)?\.ts$/.test(file)) {
      backup(path.join(productCoreDir, file));
    }
  }

  if (alreadyDomainSplit()) {
    console.log('Domain split already present. Running validation only.');
    validateOnly();
    return;
  }

  const ts = loadTypeScript();
  const original = readText(barrelPath);
  const sourceFile = ts.createSourceFile(barrelRel, original, ts.ScriptTarget.Latest, true, ts.ScriptKind.TS);

  const declarations = collectDeclarations(ts, original, sourceFile);

  if (declarations.length === 0) {
    throw new Error('No exported type/interface declarations found in product-core/types.ts. Cannot split domains safely.');
  }

  const imports = collectImports(ts, original, sourceFile);
  const modules = makeChunks(declarations);

  const typeNameToFile = new Map();

  for (const module of modules) {
    for (const declaration of module.declarations) {
      typeNameToFile.set(declaration.name, module.fileName);
    }
  }

  const allTypeNames = Array.from(typeNameToFile.keys()).sort((a, b) => a.localeCompare(b));

  for (const module of modules) {
    const content = moduleText(imports, module, typeNameToFile);
    writeText(path.join(productCoreDir, module.fileName), content);
  }

  const barrelLines = [
    '// PlantProcess IQ Phase 5B-1',
    '// ' + domainMarker,
    '// Thin barrel for product-core API DTO/filter/read-model types.',
    '// Do not add large declarations here; create or update domain type modules instead.',
    ''
  ];

  for (const module of modules.sort((a, b) => a.fileName.localeCompare(b.fileName))) {
    const moduleBase = stripExtension(module.fileName);
    const names = module.declarations.map((declaration) => declaration.name).sort((a, b) => a.localeCompare(b));

    barrelLines.push(`export type {`);
    for (const name of names) {
      barrelLines.push(`  ${name},`);
    }
    barrelLines.push(`} from "./${moduleBase}";`);
    barrelLines.push('');
  }

  const newBarrel = barrelLines.join('\n');

  writeText(barrelPath, newBarrel);
  writeManifest(allTypeNames, modules);
  writeValidator(allTypeNames, modules.map((module) => module.fileName));
  writeEvidence(original, newBarrel, modules, allTypeNames);

  run('node --check product-core domain type split validator', ['node', '--check', validatorRel]);
  run('Phase 5B-1 domain type split validator', ['node', validatorRel]);

  if (fs.existsSync(path.join(root, 'tools', 'phase5b', 'analyze-phase5b-refactor.cjs'))) {
    run('Refresh Phase 5B refactor report after product-core domain type split', ['node', 'tools/phase5b/analyze-phase5b-refactor.cjs']);
  }

  console.log('');
  console.log('=================================================================================================');
  console.log('Phase 5B-1 product-core domain type split completed.');
  console.log('Domain modules: ' + modules.length);
  console.log('Type declarations: ' + allTypeNames.length);
  console.log('Barrel lines: ' + lineCount(newBarrel));
  console.log('Evidence: ' + evidenceRel);
  console.log('Backup  : ' + backupRoot);
  console.log('=================================================================================================');
}

main();
const fs = require('fs');
const path = require('path');
const cp = require('child_process');

const root = process.cwd();

function fileText(relativePath) {
  const file = path.join(root, relativePath);

  if (!fs.existsSync(file)) {
    throw new Error(`Missing required file: ${relativePath}`);
  }

  return fs.readFileSync(file, 'utf8');
}

function expectContains(relativePath, marker) {
  const text = fileText(relativePath);

  if (!text.includes(marker)) {
    throw new Error(`${relativePath} does not contain required marker: ${marker}`);
  }
}

function expectNotContains(relativePath, marker) {
  const text = fileText(relativePath);

  if (text.includes(marker)) {
    throw new Error(`${relativePath} still contains forbidden marker: ${marker}`);
  }
}

expectContains('Frontend/PlantProcess.Web/src/styles/global.css', './phase56/phase56-tokens.css');
expectContains('Frontend/PlantProcess.Web/src/styles/global.css', './phase56/phase56-accessibility.css');
expectNotContains('Frontend/PlantProcess.Web/src/styles/global.css', './legacy/legacy-global.css');

if (fs.existsSync(path.join(root, 'Frontend/PlantProcess.Web/src/styles/legacy/legacy-global.css'))) {
  throw new Error('legacy-global.css still exists');
}

expectContains('Frontend/PlantProcess.Web/src/main.tsx', './accessibility/phase56ThemeRuntime');

// Correct marker ownership:
// - Literal storage key belongs to the token source-of-truth file.
// - Runtime should import/use phase56ThemeStorageKey, not duplicate the literal.
expectContains('Frontend/PlantProcess.Web/src/design-system/phase56/phase56Tokens.ts', 'plantprocess.theme.v1');
expectContains('Frontend/PlantProcess.Web/src/accessibility/phase56ThemeRuntime.ts', 'phase56ThemeStorageKey');

expectContains('Frontend/PlantProcess.Web/src/accessibility/phase56ThemeRuntime.ts', 'prefers-color-scheme');
expectContains('Frontend/PlantProcess.Web/src/styles/phase56/phase56-accessibility.css', ':focus-visible');
expectContains('Frontend/PlantProcess.Web/src/styles/phase56/phase56-accessibility.css', 'prefers-reduced-motion');
expectContains('Frontend/PlantProcess.Web/e2e/a11y/phase56-accessibility.spec.ts', 'phase56Routes');
expectContains('docs/ux/ACCESSIBILITY.md', 'WCAG 2.1 AA');
expectContains('docs/phase5-phase6/PHASE5_PHASE6_IMPLEMENTATION_EVIDENCE.md', 'T-036');

cp.execFileSync('node', ['tools/phase56/contrast-check.cjs'], {
  cwd: root,
  stdio: 'inherit'
});

cp.execFileSync('node', ['tools/phase56/check-file-size-complexity.cjs'], {
  cwd: root,
  stdio: 'inherit'
});

console.log('Phase 5 + Phase 6 source validation passed.');
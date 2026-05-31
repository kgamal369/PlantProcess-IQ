const fs = require("node:fs");
const path = require("node:path");

const root = process.cwd();
const srcRoot = path.join(root, "Frontend", "PlantProcess.Web", "src");

function walk(dir) {
  const entries = fs.readdirSync(dir, { withFileTypes: true });
  const files = [];

  for (const entry of entries) {
    const full = path.join(dir, entry.name);

    if (entry.isDirectory()) {
      files.push(...walk(full));
    } else if (entry.isFile() && /\.(tsx|jsx)$/.test(entry.name)) {
      files.push(full);
    }
  }

  return files;
}

function hasAccessibleName(openingTag) {
  return (
    /\saria-label\s*=/.test(openingTag) ||
    /\saria-labelledby\s*=/.test(openingTag) ||
    /\sid\s*=/.test(openingTag)
  );
}

let patched = 0;

for (const file of walk(srcRoot)) {
  let text = fs.readFileSync(file, "utf8");
  const original = text;

  text = text.replace(/<select\b([^>]*)>([\s\S]*?)<\/select>/g, (match, attrs, body) => {
    const openingTag = `<select${attrs}>`;

    const looksLikeLicenseSelector =
      body.includes('value="Light"') &&
      body.includes('value="Pro"') &&
      body.includes('value="ProPlus"') &&
      body.includes('value="Enterprise"');

    if (!looksLikeLicenseSelector || hasAccessibleName(openingTag)) {
      return match;
    }

    patched += 1;

    return `<select aria-label="Demo license plan"${attrs}>${body}</select>`;
  });

  if (text !== original) {
    fs.writeFileSync(file, text.replace(/\r\n/g, "\n"), "utf8");
    console.log("Patched unlabeled license select:", path.relative(root, file));
  }
}

if (patched === 0) {
  console.log("No unlabeled Light/Pro/ProPlus/Enterprise select was found.");
  console.log("Run the scanner below to find remaining unlabeled selects.");
} else {
  console.log(`Patched ${patched} unlabeled license select(s).`);
}

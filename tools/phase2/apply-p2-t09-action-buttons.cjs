const fs = require("fs");
const path = require("path");

const root = process.cwd();
const marker = "PPIQ_P2_T09_ACTION_BUTTON_STANDARDIZATION";

function full(rel) {
  return path.join(root, rel.replaceAll("/", path.sep));
}

function exists(rel) {
  return fs.existsSync(full(rel));
}

function readFile(file) {
  return fs.readFileSync(file, "utf8");
}

function writeFile(file, text) {
  fs.writeFileSync(file, text.replace(/\r?\n/g, "\r\n"), "utf8");
}

function backupFile(file) {
  if (!fs.existsSync(file)) return;

  const stamp = new Date().toISOString().replace(/[-:.TZ]/g, "").slice(0, 14);
  const rel = path.relative(root, file);
  const target = path.join(root, ".phase2_backups", "P2-T09_SourcePatch_" + stamp, rel);
  fs.mkdirSync(path.dirname(target), { recursive: true });
  fs.copyFileSync(file, target);
}

function walk(dir, predicate) {
  const output = [];

  if (!fs.existsSync(dir)) return output;

  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const item = path.join(dir, entry.name);

    if (entry.isDirectory()) {
      if (
        entry.name === "node_modules" ||
        entry.name === "dist" ||
        entry.name === "coverage" ||
        entry.name === "playwright-report" ||
        entry.name === "test-results" ||
        entry.name === "__snapshots__"
      ) {
        continue;
      }

      output.push(...walk(item, predicate));
    } else if (!predicate || predicate(item)) {
      output.push(item);
    }
  }

  return output;
}

function rel(file) {
  return path.relative(root, file).replaceAll(path.sep, "/");
}

function escapeRegex(value) {
  return value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

function ensureNamedImport(text, source, name) {
  const already = new RegExp(
    "import\\s+\\{[\\s\\S]*?\\b" + escapeRegex(name) + "\\b[\\s\\S]*?\\}\\s+from\\s+[\"']" + escapeRegex(source) + "[\"']",
    "m",
  );

  if (already.test(text)) return text;

  const importRegex = new RegExp(
    "import\\s+\\{([\\s\\S]*?)\\}\\s+from\\s+[\"']" + escapeRegex(source) + "[\"'];?",
    "m",
  );

  const match = text.match(importRegex);

  if (match) {
    const names = match[1]
      .split(",")
      .map((x) => x.trim())
      .filter(Boolean);

    names.push(name);

    const merged = Array.from(new Set(names)).sort();
    return text.replace(importRegex, "import { " + merged.join(", ") + " } from \"" + source + "\";");
  }

  const imports = Array.from(text.matchAll(/^import .*?;\s*$/gm));

  if (imports.length === 0) {
    return "import { " + name + " } from \"" + source + "\";\n" + text;
  }

  const last = imports[imports.length - 1];
  const insertAt = last.index + last[0].length;

  return text.slice(0, insertAt) + "\nimport { " + name + " } from \"" + source + "\";" + text.slice(insertAt);
}

function removeNamedImport(text, source, name) {
  const importRegex = new RegExp(
    "import\\s+\\{([\\s\\S]*?)\\}\\s+from\\s+[\"']" + escapeRegex(source) + "[\"'];?\\s*",
    "m",
  );

  const match = text.match(importRegex);

  if (!match) return text;

  const names = match[1]
    .split(",")
    .map((x) => x.trim())
    .filter(Boolean)
    .filter((x) => x !== name);

  if (names.length === 0) {
    return text.replace(importRegex, "");
  }

  return text.replace(importRegex, "import { " + names.join(", ") + " } from \"" + source + "\";\n");
}

function findOpeningTagEnd(text, start) {
  let quote = null;
  let braceDepth = 0;

  for (let i = start; i < text.length; i += 1) {
    const ch = text[i];

    if (quote) {
      if (ch === "\\" && i + 1 < text.length) {
        i += 1;
        continue;
      }

      if (ch === quote) {
        quote = null;
      }

      continue;
    }

    if (ch === "\"" || ch === "'" || ch === "`") {
      quote = ch;
      continue;
    }

    if (ch === "{") {
      braceDepth += 1;
      continue;
    }

    if (ch === "}") {
      if (braceDepth > 0) braceDepth -= 1;
      continue;
    }

    if (ch === ">" && braceDepth === 0) {
      return i;
    }
  }

  return -1;
}

function removeProp(tag, prop) {
  let next = tag;
  next = next.replace(new RegExp("\\s+" + escapeRegex(prop) + "\\s*=\\s*\"[^\"]*\"", "g"), "");
  next = next.replace(new RegExp("\\s+" + escapeRegex(prop) + "\\s*=\\s*'[^']*'", "g"), "");
  next = next.replace(new RegExp("\\s+" + escapeRegex(prop) + "\\s*=\\s*\\{[^}]*\\}", "g"), "");
  next = next.replace(new RegExp("\\s+" + escapeRegex(prop) + "(?=\\s|/?>)", "g"), "");
  return next;
}

function addProp(tag, prop, value) {
  if (new RegExp("\\s" + escapeRegex(prop) + "(\\s*=|\\s|/?>)").test(tag)) {
    return tag;
  }

  const close = tag.endsWith("/>") ? "/>" : ">";
  const at = tag.lastIndexOf(close);

  if (at < 0) return tag;

  return tag.slice(0, at) + " " + prop + "=" + value + tag.slice(at);
}

function replaceProp(tag, prop, value) {
  return addProp(removeProp(tag, prop), prop, value);
}

function appendClassName(tag, className) {
  const quoted = tag.match(/\sclassName="([^"]*)"/);

  if (quoted) {
    const current = quoted[1];

    if (current.split(/\s+/).includes(className)) return tag;

    return tag.replace(/\sclassName="([^"]*)"/, " className=\"" + current + " " + className + "\"");
  }

  const single = tag.match(/\sclassName='([^']*)'/);

  if (single) {
    const current = single[1];

    if (current.split(/\s+/).includes(className)) return tag;

    return tag.replace(/\sclassName='([^']*)'/, " className='" + current + " " + className + "'");
  }

  return addProp(tag, "className", "\"" + className + "\"");
}

function transformStandardButtonOpeningTags(text, callback) {
  let output = "";
  let index = 0;
  let buttonIndex = 0;

  while (index < text.length) {
    const start = text.indexOf("<StandardButton", index);

    if (start < 0) {
      output += text.slice(index);
      break;
    }

    output += text.slice(index, start);

    const end = findOpeningTagEnd(text, start);

    if (end < 0) {
      output += text.slice(start);
      break;
    }

    const tag = text.slice(start, end + 1);
    output += callback(tag, buttonIndex);
    buttonIndex += 1;
    index = end + 1;
  }

  return output;
}

function normalizeStandardButtonTag(tag) {
  let next = tag;

  next = next.replace(/\s+disabled\s*=/g, " isDisabled=");
  next = next.replace(/\s+disabled(?=\s|\/?>)/g, " isDisabled");

  return next;
}

function patchSourceFile(file) {
  const r = rel(file);

  if (!r.startsWith("Frontend/PlantProcess.Web/src/")) return false;
  if (!r.endsWith(".tsx")) return false;
  if (r.includes("/__tests__/")) return false;
  if (r.endsWith(".test.tsx") || r.endsWith(".stories.tsx")) return false;
  if (r.startsWith("Frontend/PlantProcess.Web/src/components/standard/")) return false;
  if (r.startsWith("Frontend/PlantProcess.Web/src/ui/")) return false;

  let text = readFile(file);
  const original = text;

  if (text.includes("StandardP2Button")) {
    text = text.replace(/<StandardP2Button\b/g, "<StandardButton");
    text = text.replace(/<\/StandardP2Button>/g, "</StandardButton>");
    text = removeNamedImport(text, "@/components/standard/StandardP2Controls", "StandardP2Button");
  }

  if (text.includes("<StandardButton")) {
    text = ensureNamedImport(text, "@/components/standard", "StandardButton");

    const isMaterialInvestigation = r.includes("MaterialInvestigation");

    text = transformStandardButtonOpeningTags(text, (tag, index) => {
      let next = normalizeStandardButtonTag(tag);

      if (isMaterialInvestigation) {
        next = appendClassName(next, "ppiq-p2t09-material-button");

        if (!/\svariant=/.test(next)) {
          const variant = index === 0 ? "primary" : index === 1 ? "secondary" : "ghost";
          next = addProp(next, "variant", "\"" + variant + "\"");
        }

        if (index === 0) {
          next = appendClassName(next, "ppiq-p2t09-action-primary");
        } else if (index === 1) {
          next = appendClassName(next, "ppiq-p2t09-action-secondary");
        } else {
          next = appendClassName(next, "ppiq-p2t09-action-ghost");
        }
      }

      return next;
    });

    if (isMaterialInvestigation && text.includes("<StandardButton")) {
      if (!text.includes("variant=\"primary\"")) {
        text = text.replace(/<StandardButton\b/, "<StandardButton variant=\"primary\"");
      }

      if (!text.includes("variant=\"secondary\"")) {
        let seen = false;
        text = text.replace(/<StandardButton\b/g, (match) => {
          if (!seen) {
            seen = true;
            return match;
          }

          return "<StandardButton variant=\"secondary\"";
        });
      }
    }
  }

  if (text !== original) {
    backupFile(file);
    writeFile(file, text);
    console.log("[P2-T09] patched " + r);
    return true;
  }

  return false;
}

const files = walk(full("Frontend/PlantProcess.Web/src"), (file) => file.endsWith(".tsx"));
let changed = 0;

for (const file of files) {
  if (patchSourceFile(file)) changed += 1;
}

console.log("[GREEN] P2-T09 apply completed. Patched source files: " + changed);
console.log("Marker: " + marker);
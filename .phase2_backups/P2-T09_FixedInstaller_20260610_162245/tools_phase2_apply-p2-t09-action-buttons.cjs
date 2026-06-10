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

function read(rel) {
  return fs.readFileSync(full(rel), "utf8");
}

function write(rel, content) {
  const target = full(rel);
  fs.mkdirSync(path.dirname(target), { recursive: true });
  fs.writeFileSync(target, content.replace(/\r?\n/g, "\r\n"), "utf8");
  console.log("[P2-T09] wrote " + rel);
}

function backup(rel) {
  if (!exists(rel)) return;

  const stamp = new Date().toISOString().replace(/[-:.TZ]/g, "").slice(0, 14);
  const target = path.join(root, ".phase2_backups", "P2-T09_" + stamp, rel.replaceAll("/", path.sep));
  fs.mkdirSync(path.dirname(target), { recursive: true });
  fs.copyFileSync(full(rel), target);
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

function toRel(file) {
  return path.relative(root, file).replaceAll(path.sep, "/");
}

function escapeRegex(value) {
  return value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

function ensureNamedImport(text, source, name) {
  if (new RegExp("import\\s+\\{[\\s\\S]*?\\b" + escapeRegex(name) + "\\b[\\s\\S]*?\\}\\s+from\\s+[\"']" + escapeRegex(source) + "[\"']").test(text)) {
    return text;
  }

  if (text.includes(`from "${source}"`) || text.includes(`from '${source}'`)) {
    const importRegex = new RegExp("import\\s+\\{([\\s\\S]*?)\\}\\s+from\\s+[\"']" + escapeRegex(source) + "[\"'];?", "m");
    const match = text.match(importRegex);

    if (match) {
      const names = match[1]
        .split(",")
        .map((x) => x.trim())
        .filter(Boolean);

      names.push(name);

      const unique = Array.from(new Set(names)).sort();
      return text.replace(importRegex, `import { ${unique.join(", ")} } from "${source}";`);
    }
  }

  const imports = [...text.matchAll(/^import .*?;\s*$/gm)];

  if (imports.length > 0) {
    const last = imports[imports.length - 1];
    const insertAt = last.index + last[0].length;
    return text.slice(0, insertAt) + `\nimport { ${name} } from "${source}";` + text.slice(insertAt);
  }

  return `import { ${name} } from "${source}";\n` + text;
}

function removeNamedImport(text, source, name) {
  const importRegex = new RegExp("import\\s+\\{([\\s\\S]*?)\\}\\s+from\\s+[\"']" + escapeRegex(source) + "[\"'];?\\s*", "m");
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

  return text.replace(importRegex, `import { ${names.join(", ")} } from "${source}";\n`);
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

    if (ch === '"' || ch === "'" || ch === "`") {
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

function transformStandardButtonOpeningTags(text, transformer) {
  let output = "";
  let index = 0;

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
    output += transformer(tag);
    index = end + 1;
  }

  return output;
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
  const insertAt = tag.lastIndexOf(close);

  if (insertAt < 0) return tag;

  return tag.slice(0, insertAt) + ` ${prop}=${value}` + tag.slice(insertAt);
}

function replaceProp(tag, prop, value) {
  return addProp(removeProp(tag, prop), prop, value);
}

function appendClassName(tag, className) {
  const quotedClass = tag.match(/\sclassName="([^"]*)"/);

  if (quotedClass) {
    const current = quotedClass[1];

    if (current.split(/\s+/).includes(className)) {
      return tag;
    }

    return tag.replace(/\sclassName="([^"]*)"/, ` className="${current} ${className}"`);
  }

  const singleClass = tag.match(/\sclassName='([^']*)'/);

  if (singleClass) {
    const current = singleClass[1];

    if (current.split(/\s+/).includes(className)) {
      return tag;
    }

    return tag.replace(/\sclassName='([^']*)'/, ` className='${current} ${className}'`);
  }

  return addProp(tag, "className", `"${className}"`);
}

function normalizeStandardButtonProps(text) {
  return transformStandardButtonOpeningTags(text, (tag) => {
    let next = tag;

    next = next.replace(/\s+disabled\s*=/g, " isDisabled=");
    next = next.replace(/\s+disabled(?=\s|\/?>)/g, " isDisabled");

    return next;
  });
}

function transformStandardButtonElements(text, transformer) {
  let output = "";
  let index = 0;

  while (index < text.length) {
    const start = text.indexOf("<StandardButton", index);

    if (start < 0) {
      output += text.slice(index);
      break;
    }

    output += text.slice(index, start);

    const openEnd = findOpeningTagEnd(text, start);

    if (openEnd < 0) {
      output += text.slice(start);
      break;
    }

    const opening = text.slice(start, openEnd + 1);

    if (opening.endsWith("/>")) {
      output += transformer(opening, "", "");
      index = openEnd + 1;
      continue;
    }

    const closeTag = "</StandardButton>";
    const closeStart = text.indexOf(closeTag, openEnd + 1);

    if (closeStart < 0) {
      output += transformer(opening, "", "");
      index = openEnd + 1;
      continue;
    }

    const inner = text.slice(openEnd + 1, closeStart);
    output += transformer(opening, inner, closeTag);
    index = closeStart + closeTag.length;
  }

  return output;
}

function classifyButtonInner(opening, inner) {
  const searchable = (opening + "\n" + inner).replace(/\s+/g, " ");

  if (/PDF Report|Export|Download/i.test(searchable)) return { variant: "ghost", className: "ppiq-p2t09-action-ghost" };
  if (/Search|Investigate|Refresh proof|Resolve SQL/i.test(searchable)) return { variant: "primary", className: "ppiq-p2t09-action-primary" };
  if (/Load Investigation|Calculate Risk|Validate|Run proof|Run lifecycle|Validate graph|Retry/i.test(searchable)) return { variant: "secondary", className: "ppiq-p2t09-action-secondary" };
  if (/p03p04-tab/i.test(searchable)) return { variant: "ghost", className: "ppiq-p2t09-action-tab" };

  return null;
}

function tuneMaterialInvestigationButtons(text, rel) {
  if (!rel.includes("MaterialInvestigation")) return text;

  return transformStandardButtonElements(text, (opening, inner, closing) => {
    let tag = opening;
    const classification = classifyButtonInner(opening, inner);

    if (classification) {
      tag = replaceProp(tag, "variant", `"${classification.variant}"`);
      tag = appendClassName(tag, "ppiq-p2t09-material-button");
      tag = appendClassName(tag, classification.className);
    }

    if (/onClick=\{refreshAll\}/.test(tag)) {
      tag = addProp(tag, "isLoading", "{isLoading}");
      tag = addProp(tag, "isDisabled", "{isLoading}");
    }

    if (/type="submit"/.test(tag) && rel.includes("MaterialInvestigation/MaterialInvestigationPage.tsx")) {
      tag = addProp(tag, "isLoading", "{isLoading}");
      tag = addProp(tag, "isDisabled", "{isLoading}");
    }

    return tag + inner + closing;
  });
}

function patchSourceFile(file) {
  const rel = toRel(file);

  if (!rel.startsWith("Frontend/PlantProcess.Web/src/")) return false;
  if (!rel.endsWith(".tsx")) return false;
  if (rel.includes("/__tests__/")) return false;
  if (rel.endsWith(".test.tsx") || rel.endsWith(".stories.tsx")) return false;
  if (rel.startsWith("Frontend/PlantProcess.Web/src/components/standard/")) return false;

  let text = fs.readFileSync(file, "utf8");
  const original = text;

  if (text.includes("StandardP2Button")) {
    text = text.replace(/<StandardP2Button\b/g, "<StandardButton");
    text = text.replace(/<\/StandardP2Button>/g, "</StandardButton>");
    text = removeNamedImport(text, "@/components/standard/StandardP2Controls", "StandardP2Button");
  }

  if (text.includes("<StandardButton")) {
    text = ensureNamedImport(text, "@/components/standard", "StandardButton");
    text = normalizeStandardButtonProps(text);
    text = tuneMaterialInvestigationButtons(text, rel);
  }

  if (text !== original) {
    backup(rel);
    fs.writeFileSync(file, text.replace(/\r?\n/g, "\r\n"), "utf8");
    console.log("[P2-T09] patched " + rel);
    return true;
  }

  return false;
}

write("Frontend/PlantProcess.Web/src/components/standard/StandardButton.tsx", `
import {
  forwardRef,
  type AnchorHTMLAttributes,
  type ButtonHTMLAttributes,
  type ReactNode,
  type Ref,
} from "react";
import { Loader2 } from "lucide-react";
import "./standard-components.css";

export const P2T09_ACTION_BUTTON_STANDARDIZATION_MARKER =
  "${marker}";

export type StandardButtonVariant =
  | "primary"
  | "secondary"
  | "ghost"
  | "danger"
  | "success"
  | "action";

export type StandardButtonSize = "sm" | "md" | "lg";
export type StandardButtonAs = "button" | "a";

type SharedProps = {
  as?: StandardButtonAs;
  variant?: StandardButtonVariant;
  size?: StandardButtonSize;
  leadingIcon?: ReactNode;
  trailingIcon?: ReactNode;
  isLoading?: boolean;
  isDisabled?: boolean;
  loadingLabel?: string;
  fullWidth?: boolean;
  iconOnly?: boolean;
  ariaLabel?: string;
  children?: ReactNode;
};

type ButtonProps = SharedProps &
  Omit<ButtonHTMLAttributes<HTMLButtonElement>, "disabled" | "aria-label"> & {
    as?: "button";
    href?: never;
  };

type AnchorProps = SharedProps &
  Omit<AnchorHTMLAttributes<HTMLAnchorElement>, "aria-label"> & {
    as: "a";
    href: string;
  };

export type StandardButtonProps = ButtonProps | AnchorProps;

function classes(...values: Array<string | false | null | undefined>) {
  return values.filter(Boolean).join(" ");
}

function displayVariant(variant: StandardButtonVariant) {
  return variant === "action" ? "primary" : variant;
}

export const StandardButton = forwardRef<HTMLButtonElement | HTMLAnchorElement, StandardButtonProps>(
  (props, ref) => {
    const {
      as = "button",
      variant = "primary",
      size = "md",
      leadingIcon,
      trailingIcon,
      isLoading = false,
      isDisabled = false,
      loadingLabel = "Loading",
      fullWidth = false,
      iconOnly = false,
      ariaLabel,
      children,
      className,
      onClick,
      ...rest
    } = props;

    const disabled = Boolean(isDisabled || isLoading);
    const renderedVariant = displayVariant(variant);

    const classNames = classes(
      "ppiq-std-button",
      "ppiq-std-button--" + renderedVariant,
      variant === "action" && "ppiq-std-button--action",
      "ppiq-std-button--" + size,
      isLoading && "ppiq-std-button--is-loading",
      disabled && "ppiq-std-button--is-disabled",
      fullWidth && "ppiq-std-button--full",
      iconOnly && "ppiq-std-button--icon-only",
      className,
    );

    const buttonLabel =
      typeof children === "string" ? children : ariaLabel ?? "Action";

    const content = (
      <>
        {isLoading ? (
          <Loader2
            className="ppiq-std-button__spinner"
            size={16}
            aria-hidden="true"
          />
        ) : (
          leadingIcon
        )}

        {!iconOnly ? (
          <span className={isLoading ? "ppiq-std-button__label--loading" : undefined}>
            {isLoading ? loadingLabel : children}
          </span>
        ) : (
          <span className="sr-only">{ariaLabel ?? buttonLabel}</span>
        )}

        {!isLoading ? trailingIcon : null}
      </>
    );

    if (as === "a") {
      const anchorProps = rest as Omit<AnchorHTMLAttributes<HTMLAnchorElement>, "aria-label">;

      return (
        <a
          {...anchorProps}
          ref={ref as Ref<HTMLAnchorElement>}
          className={classNames}
          aria-label={ariaLabel}
          aria-disabled={disabled || undefined}
          aria-busy={isLoading || undefined}
          data-loading={isLoading ? "true" : undefined}
          data-variant={variant}
          tabIndex={disabled ? -1 : anchorProps.tabIndex}
          onClick={(event) => {
            if (disabled) {
              event.preventDefault();
              return;
            }

            const anchorOnClick = onClick as AnchorHTMLAttributes<HTMLAnchorElement>["onClick"] | undefined;
            anchorOnClick?.(event);
          }}
        >
          {content}
        </a>
      );
    }

    const buttonProps = rest as Omit<ButtonHTMLAttributes<HTMLButtonElement>, "disabled" | "aria-label">;

    return (
      <button
        {...buttonProps}
        ref={ref as Ref<HTMLButtonElement>}
        type={buttonProps.type ?? "button"}
        className={classNames}
        disabled={disabled}
        aria-label={ariaLabel}
        aria-busy={isLoading || undefined}
        aria-disabled={disabled || undefined}
        data-loading={isLoading ? "true" : undefined}
        data-variant={variant}
        onClick={onClick as ButtonHTMLAttributes<HTMLButtonElement>["onClick"]}
      >
        {content}
      </button>
    );
  },
);

StandardButton.displayName = "StandardButton";
`);

const cssRel = "Frontend/PlantProcess.Web/src/components/standard/standard-components.css";
if (exists(cssRel)) {
  backup(cssRel);
  let css = read(cssRel);

  const begin = "/* P2-T09 ACTION BUTTON STANDARDIZATION BEGIN */";
  const end = "/* P2-T09 ACTION BUTTON STANDARDIZATION END */";
  const beginIndex = css.indexOf(begin);
  const endIndex = css.indexOf(end);

  if (beginIndex >= 0 && endIndex > beginIndex) {
    css = css.slice(0, beginIndex) + css.slice(endIndex + end.length);
  }

  css = css.replace(
    /\.ppiq-std-button\[aria-disabled="true"\],\s*\.ppiq-std-button:disabled\s*\{\s*opacity:\s*0\.[0-9]+;/m,
    `.ppiq-std-button[aria-disabled="true"],\n.ppiq-std-button:disabled {\n  opacity: 0.35;`
  );

  css += `

${begin}
/* ${marker} */

.ppiq-std-button--action {
  background: linear-gradient(135deg, var(--ppiq-std-brand-blue), var(--ppiq-std-brand-cyan));
  color: #ffffff;
  border-color: rgba(0, 212, 255, 0.78);
  box-shadow: 0 0 22px rgba(0, 212, 255, 0.22);
}

.ppiq-std-button--is-loading {
  cursor: wait;
}

.ppiq-std-button--is-loading .ppiq-std-button__spinner {
  flex: 0 0 auto;
}

.ppiq-std-button--is-disabled,
.ppiq-std-button[aria-disabled="true"],
.ppiq-std-button:disabled {
  opacity: 0.35;
  cursor: not-allowed;
  pointer-events: none;
}

.ppiq-std-button[data-loading="true"] {
  position: relative;
}

.ppiq-std-button:focus-visible {
  outline: 2px solid transparent;
  outline-offset: 2px;
  box-shadow: var(--ppiq-std-focus), 0 0 0 1px rgba(0, 212, 255, 0.26);
}

.ppiq-p2t09-material-button {
  min-width: 132px;
  justify-content: center;
}

.ppiq-p2t09-action-primary {
  order: 1;
}

.ppiq-p2t09-action-secondary {
  order: 2;
}

.ppiq-p2t09-action-ghost {
  order: 3;
}

.ppiq-p2t09-action-tab {
  min-width: 0;
}

@media (max-width: 720px) {
  .ppiq-p2t09-material-button {
    width: 100%;
  }
}
${end}
`;

  write(cssRel, css);
}

const sourceFiles = walk(full("Frontend/PlantProcess.Web/src"), (file) => file.endsWith(".tsx"));
let changed = 0;

for (const file of sourceFiles) {
  if (patchSourceFile(file)) changed += 1;
}

const pkgRel = "Frontend/PlantProcess.Web/package.json";
if (!exists(pkgRel)) {
  throw new Error("Missing " + pkgRel);
}

backup(pkgRel);
const pkg = JSON.parse(read(pkgRel));
pkg.scripts = pkg.scripts || {};
pkg.scripts["audit:action-buttons"] = "node ../../tools/ui/audit-action-buttons.cjs";
pkg.scripts["validate:action-buttons"] = "node ../../tools/ui/audit-action-buttons.cjs --fail";
write(pkgRel, JSON.stringify(pkg, null, 2) + "\n");

write("tools/ui/audit-action-buttons.cjs", `
const fs = require("fs");
const path = require("path");

const root = process.cwd();
const webRoot = fs.existsSync(path.join(root, "Frontend", "PlantProcess.Web"))
  ? path.join(root, "Frontend", "PlantProcess.Web")
  : root;

const srcRoot = path.join(webRoot, "src");
const failMode = process.argv.includes("--fail");
const marker = "${marker}";

function relFromRoot(file) {
  return path.relative(root, file).replaceAll(path.sep, "/");
}

function relFromWeb(file) {
  return path.relative(webRoot, file).replaceAll(path.sep, "/");
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

function findOpeningTagEnd(text, start) {
  let quote = null;
  let braceDepth = 0;

  for (let i = start; i < text.length; i += 1) {
    const ch = text[i];

    if (quote) {
      if (ch === "\\\\" && i + 1 < text.length) {
        i += 1;
        continue;
      }

      if (ch === quote) {
        quote = null;
      }

      continue;
    }

    if (ch === '"' || ch === "'" || ch === "\`") {
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

function collectStandardButtonTags(text) {
  const tags = [];
  let index = 0;

  while (index < text.length) {
    const start = text.indexOf("<StandardButton", index);

    if (start < 0) break;

    const end = findOpeningTagEnd(text, start);

    if (end < 0) break;

    tags.push(text.slice(start, end + 1));
    index = end + 1;
  }

  return tags;
}

function isAuditedSource(file) {
  const rel = relFromWeb(file);

  if (!rel.startsWith("src/pages/") && !rel.startsWith("src/components/")) return false;
  if (rel.startsWith("src/components/standard/")) return false;
  if (rel.includes("/__tests__/")) return false;
  if (rel.endsWith(".test.tsx") || rel.endsWith(".test.ts") || rel.endsWith(".stories.tsx") || rel.endsWith(".stories.ts")) return false;

  return rel.endsWith(".tsx");
}

const files = walk(srcRoot, isAuditedSource);
const findings = [];

for (const file of files) {
  const rel = relFromWeb(file);
  const text = fs.readFileSync(file, "utf8");
  const lines = text.split(/\\r?\\n/);

  lines.forEach((line, index) => {
    if (/<button\\b/.test(line)) {
      findings.push({
        file: rel,
        line: index + 1,
        kind: "raw-button",
        message: "raw <button> is not allowed outside canonical StandardButton wrappers",
        snippet: line.trim().slice(0, 220),
      });
    }

    if (/<a\\b/.test(line) && /PDF|Report|Export|Download/.test(line)) {
      findings.push({
        file: rel,
        line: index + 1,
        kind: "raw-action-anchor",
        message: "raw export/report anchors must use StandardButton as='a'",
        snippet: line.trim().slice(0, 220),
      });
    }

    if (/StandardP2Button/.test(line) && rel.includes("MaterialInvestigation")) {
      findings.push({
        file: rel,
        line: index + 1,
        kind: "material-standard-p2-button",
        message: "Material Investigation must use canonical StandardButton, not StandardP2Button",
        snippet: line.trim().slice(0, 220),
      });
    }
  });

  for (const tag of collectStandardButtonTags(text)) {
    if (/\\sdisabled(\\s|=|\\/?>)/.test(tag)) {
      const line = text.slice(0, text.indexOf(tag)).split(/\\r?\\n/).length;
      findings.push({
        file: rel,
        line,
        kind: "standard-button-disabled-prop",
        message: "StandardButton must use isDisabled, not disabled",
        snippet: tag.replace(/\\s+/g, " ").slice(0, 220),
      });
    }
  }
}

const standardButtonPath = path.join(webRoot, "src", "components", "standard", "StandardButton.tsx");
const standardCssPath = path.join(webRoot, "src", "components", "standard", "standard-components.css");

if (!fs.existsSync(standardButtonPath)) {
  findings.push({
    file: "src/components/standard/StandardButton.tsx",
    line: 0,
    kind: "missing-standard-button",
    message: "Canonical StandardButton is missing",
    snippet: "",
  });
}

if (!fs.existsSync(standardCssPath)) {
  findings.push({
    file: "src/components/standard/standard-components.css",
    line: 0,
    kind: "missing-standard-css",
    message: "Canonical standard CSS is missing",
    snippet: "",
  });
}

const buttonText = fs.existsSync(standardButtonPath) ? fs.readFileSync(standardButtonPath, "utf8") : "";
const cssText = fs.existsSync(standardCssPath) ? fs.readFileSync(standardCssPath, "utf8") : "";

const requiredButtonTokens = [
  marker,
  "isLoading",
  "isDisabled",
  "loadingLabel",
  "aria-busy",
  "ppiq-std-button__spinner",
  "data-loading",
  "StandardButtonVariant",
  "action",
];

for (const token of requiredButtonTokens) {
  if (!buttonText.includes(token)) {
    findings.push({
      file: "src/components/standard/StandardButton.tsx",
      line: 0,
      kind: "standard-button-contract",
      message: "StandardButton missing token: " + token,
      snippet: "",
    });
  }
}

const requiredCssTokens = [
  marker,
  "ppiq-std-button--action",
  "ppiq-std-button--is-loading",
  "opacity: 0.35",
  "focus-visible",
  "ppiq-p2t09-material-button",
];

for (const token of requiredCssTokens) {
  if (!cssText.includes(token)) {
    findings.push({
      file: "src/components/standard/standard-components.css",
      line: 0,
      kind: "button-css-contract",
      message: "Standard button CSS missing token: " + token,
      snippet: "",
    });
  }
}

const materialFiles = files.filter((file) => relFromWeb(file).includes("MaterialInvestigation"));
const materialText = materialFiles.map((file) => fs.readFileSync(file, "utf8")).join("\\n");

if (!materialText.includes("StandardButton")) {
  findings.push({
    file: "src/pages/MaterialInvestigation",
    line: 0,
    kind: "material-button-contract",
    message: "Material Investigation must use StandardButton",
    snippet: "",
  });
}

if (materialText.includes("<StandardP2Button")) {
  findings.push({
    file: "src/pages/MaterialInvestigation",
    line: 0,
    kind: "material-button-contract",
    message: "Material Investigation still uses StandardP2Button",
    snippet: "",
  });
}

if (!materialText.includes("variant=\\"primary\\"") || !materialText.includes("variant=\\"secondary\\"")) {
  findings.push({
    file: "src/pages/MaterialInvestigation",
    line: 0,
    kind: "material-button-hierarchy",
    message: "Material Investigation must contain primary and secondary StandardButton hierarchy",
    snippet: "",
  });
}

if (!materialText.includes("isLoading=")) {
  findings.push({
    file: "src/pages/MaterialInvestigation",
    line: 0,
    kind: "material-loading-state",
    message: "Material Investigation must wire at least one StandardButton loading state",
    snippet: "",
  });
}

const visualSpec = path.join(webRoot, "e2e", "p2-t09-action-button-visual.spec.ts");
const visualText = fs.existsSync(visualSpec) ? fs.readFileSync(visualSpec, "utf8") : "";

if (!visualText.includes("Material Investigation") || !visualText.includes("representativeRoutes")) {
  findings.push({
    file: "e2e/p2-t09-action-button-visual.spec.ts",
    line: 0,
    kind: "visual-regression-contract",
    message: "P2-T09 visual regression spec must cover Material Investigation and representative pages",
    snippet: "",
  });
}

const report = {
  marker,
  generatedAtUtc: new Date().toISOString(),
  auditedFiles: files.length,
  materialFiles: materialFiles.map(relFromWeb),
  findingCount: findings.length,
  findings,
};

const outDir = path.join(root, "Documentation", "P2-T09_ActionButtons_Latest");
fs.mkdirSync(outDir, { recursive: true });
fs.writeFileSync(path.join(outDir, "action-button-audit.json"), JSON.stringify(report, null, 2));

const md = [
  "# P2-T09 Action Button Audit",
  "",
  "Marker: " + marker,
  "",
  "- Audited files: " + report.auditedFiles,
  "- Material files: " + report.materialFiles.length,
  "- Findings: " + report.findingCount,
  "",
  findings.length === 0
    ? "## Result\\n\\nGREEN — action button hierarchy, Material Investigation styling contract, loading/disabled semantics and raw-button guard are clean."
    : "## Findings\\n\\n" + findings.map((x) => "- " + x.file + ":" + x.line + " [" + x.kind + "] " + x.message + (x.snippet ? " — " + x.snippet : "")).join("\\n"),
  "",
].join("\\n");

fs.writeFileSync(path.join(outDir, "action-button-audit.md"), md);

console.log(JSON.stringify({
  marker,
  auditedFiles: report.auditedFiles,
  materialFiles: report.materialFiles.length,
  findingCount: report.findingCount,
}, null, 2));

if (failMode && findings.length > 0) {
  console.error("[RED] P2-T09 action button audit failed. See Documentation/P2-T09_ActionButtons_Latest/action-button-audit.md");
  process.exit(1);
}

console.log("[GREEN] P2-T09 action button audit passed.");
`);

write("tools/phase2/validate-p2-t09-action-buttons.cjs", `
const fs = require("fs");
const path = require("path");
const childProcess = require("child_process");

const root = process.cwd();
const marker = "${marker}";

function full(rel) {
  return path.join(root, rel.replaceAll("/", path.sep));
}

function exists(rel) {
  return fs.existsSync(full(rel));
}

function read(rel) {
  return fs.readFileSync(full(rel), "utf8");
}

function fail(message) {
  console.error("[RED] P2-T09 validation failed: " + message);
  process.exit(1);
}

function run(command, args, cwd = root) {
  const result = childProcess.spawnSync(command, args, {
    cwd,
    encoding: "utf8",
    shell: process.platform === "win32",
  });

  if (result.error) {
    fail(command + " could not start: " + result.error.message);
  }

  if (result.status !== 0) {
    fail(
      command +
        " " +
        args.join(" ") +
        " failed\\nSTDOUT:\\n" +
        (result.stdout || "") +
        "\\nSTDERR:\\n" +
        (result.stderr || "")
    );
  }

  return (result.stdout || "") + (result.stderr || "");
}

const required = [
  "Frontend/PlantProcess.Web/src/components/standard/StandardButton.tsx",
  "Frontend/PlantProcess.Web/src/components/standard/standard-components.css",
  "tools/ui/audit-action-buttons.cjs",
  "Frontend/PlantProcess.Web/e2e/p2-t09-action-button-visual.spec.ts",
  "Frontend/PlantProcess.Web/package.json",
];

for (const rel of required) {
  if (!exists(rel)) fail("missing " + rel);
}

const button = read("Frontend/PlantProcess.Web/src/components/standard/StandardButton.tsx");
const css = read("Frontend/PlantProcess.Web/src/components/standard/standard-components.css");
const visual = read("Frontend/PlantProcess.Web/e2e/p2-t09-action-button-visual.spec.ts");
const pkg = JSON.parse(read("Frontend/PlantProcess.Web/package.json"));

for (const token of [
  marker,
  "isLoading",
  "isDisabled",
  "loadingLabel",
  "aria-busy",
  "data-loading",
  "ppiq-std-button__spinner",
]) {
  if (!button.includes(token)) fail("StandardButton missing " + token);
}

for (const variant of ["primary", "secondary", "ghost", "action"]) {
  if (!button.includes('"' + variant + '"')) fail("StandardButton missing variant " + variant);
}

for (const token of [
  marker,
  "ppiq-std-button--action",
  "ppiq-std-button--is-loading",
  "opacity: 0.35",
  "ppiq-p2t09-material-button",
]) {
  if (!css.includes(token)) fail("standard-components.css missing " + token);
}

if (!pkg.scripts || !pkg.scripts["validate:action-buttons"]) {
  fail("package.json missing validate:action-buttons");
}

if (pkg.scripts["validate:action-buttons"].includes("WARN") || pkg.scripts["validate:action-buttons"].includes("||")) {
  fail("validate:action-buttons is advisory/non-blocking");
}

if (!visual.includes("representativeRoutes") || !visual.includes("Material Investigation")) {
  fail("P2-T09 visual regression spec missing representative route coverage");
}

run("node", ["tools/ui/audit-action-buttons.cjs", "--fail"], root);

console.log("[GREEN] P2-T09 static validation passed.");
`);

write("Frontend/PlantProcess.Web/e2e/p2-t09-action-button-visual.spec.ts", `
import { expect, test } from "@playwright/test";

const representativeRoutes = [
  { name: "Material Investigation", path: "/material-investigation" },
  { name: "Dashboard", path: "/dashboard" },
  { name: "Admin", path: "/admin" },
  { name: "Risk Dashboard", path: "/risk-dashboard" },
  { name: "Value Executive", path: "/value/executive" },
  { name: "Widget Schema Drift", path: "/dashboard/widgets/schema-drift" },
  { name: "Commercial License", path: "/commercial/license" },
];

test.describe("P2-T09 action button visual baseline", () => {
  test.skip(!process.env.PPIQ_RUN_VISUAL_BASELINE, "Set PPIQ_RUN_VISUAL_BASELINE=1 to refresh P2-T09 visual snapshots.");

  for (const route of representativeRoutes) {
    test(route.name + " action button hierarchy", async ({ page }) => {
      await page.goto(route.path);
      await page.waitForLoadState("networkidle");

      const buttons = page.locator(".ppiq-std-button");
      await expect(buttons.first()).toBeVisible();

      await expect(page).toHaveScreenshot(
        "p2-t09-" + route.name.toLowerCase().replace(/[^a-z0-9]+/g, "-") + ".png",
        { fullPage: true },
      );
    });
  }
});
`);

write("docs/phase2/P2_T09_ACTION_BUTTON_STANDARDIZATION.md", `
# P2-T09 — Standardize Action Buttons + Material Investigation Styling

Marker: ${marker}

## Goal

P2-T09 completes the action-button contract after the broader P2-T08 Standard-component rollout.

It standardizes:

- Canonical StandardButton variants:
  - primary
  - secondary
  - ghost
  - action alias
  - danger
  - success
- `isDisabled` instead of native `disabled` on StandardButton callers.
- `isLoading` spinner + `aria-busy`.
- Material Investigation action hierarchy and button treatment.
- Raw `<button>` rejection outside Standard wrappers.
- Visual-regression coverage for Material Investigation plus representative pages.

## Runtime contract

Primary actions must visually stand out, disable while running, and show a spinner when in flight.

Secondary actions must be visually consistent but less dominant.

Ghost actions are used for lightweight report/export/navigation actions.

## Validation

Run:

    node tools/phase2/validate-p2-t09-action-buttons.cjs
    cd Frontend/PlantProcess.Web
    npm run validate:action-buttons
    npm run validate:standard-imports
    npm run build

Optional visual snapshots:

    set PPIQ_RUN_VISUAL_BASELINE=1
    npx playwright test e2e/p2-t09-action-button-visual.spec.ts
`);

write("Documentation/P2-T09_ActionButtons_Latest/README.md", `
# P2-T09 Latest Evidence

Generated by apply-p2-t09-action-buttons.cjs.

Expected proof:

- tools/ui/audit-action-buttons.cjs --fail passes
- npm run validate:action-buttons passes
- npm run validate:standard-imports remains green from P2-T08
- npm run build passes
`);

const changed = sourceFiles.map(patchSourceFile).filter(Boolean).length;

console.log("[GREEN] P2-T09 apply completed. Patched source files: " + changed);
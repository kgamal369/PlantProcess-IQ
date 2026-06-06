const fs = require("fs");
const path = require("path");
const cp = require("child_process");

const root = process.cwd();
const timestamp = new Date().toISOString().replace(/[-:T.Z]/g, "").slice(0, 14);
const backupRoot = path.join(root, ".pack_b_backup", "repair_css_safe_split_" + timestamp);

const frontendRoot = path.join(root, "Frontend", "PlantProcess.Web");
const legacyFacadePath = path.join(frontendRoot, "src", "styles", "phase56", "phase56-migrated-legacy.css");
const chunkDir = path.join(frontendRoot, "src", "styles", "phase56", "legacy-chunks");
const evidencePath = path.join(root, "docs", "pack-b", "PACK_B_IMPLEMENTATION_EVIDENCE.md");

function ensureDir(dir) {
  fs.mkdirSync(dir, { recursive: true });
}

function exists(file) {
  return fs.existsSync(file);
}

function isFile(file) {
  return exists(file) && fs.statSync(file).isFile();
}

function read(file) {
  return fs.readFileSync(file, "utf8");
}

function write(file, content) {
  ensureDir(path.dirname(file));
  fs.writeFileSync(file, content.replace(/\n/g, "\r\n"), "utf8");
  console.log("Wrote: " + rel(file));
}

function rel(file) {
  return path.relative(root, file).split(path.sep).join("/");
}

function backup(file) {
  if (!isFile(file)) return;
  const target = path.join(backupRoot, path.relative(root, file));
  ensureDir(path.dirname(target));
  fs.copyFileSync(file, target);
}

function walk(dir) {
  if (!exists(dir)) return [];

  return fs.readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const full = path.join(dir, entry.name);
    return entry.isDirectory() ? walk(full) : [full];
  });
}

function lineCount(file) {
  return isFile(file) ? read(file).replace(/\r\n/g, "\n").split("\n").length : 0;
}

function run(name, args, cwd = root, optional = false) {
  console.log("");
  console.log("---- " + name);

  try {
    cp.execFileSync(args[0], args.slice(1), {
      cwd,
      stdio: "inherit",
      shell: false
    });

    return { ok: true };
  } catch (error) {
    if (optional) {
      console.warn("[WARN] Optional step failed: " + name);
      console.warn(error.message || String(error));
      return { ok: false, error: error.message || String(error) };
    }

    throw error;
  }
}

function npmArgs(...args) {
  return process.platform === "win32" ? ["cmd", "/c", "npm", ...args] : ["npm", ...args];
}

function findNewestOriginalBackup() {
  const backupRootParent = path.join(root, ".pack_b_backup");
  if (!exists(backupRootParent)) return null;

  const suffix = path.join(
    "Frontend",
    "PlantProcess.Web",
    "src",
    "styles",
    "phase56",
    "phase56-migrated-legacy.css"
  );

  const candidates = walk(backupRootParent)
    .filter((file) => isFile(file))
    .filter((file) => file.endsWith(suffix))
    .map((file) => ({
      file,
      mtime: fs.statSync(file).mtimeMs,
      lines: lineCount(file)
    }))
    .filter((item) => item.lines > 500)
    .sort((a, b) => b.mtime - a.mtime);

  return candidates.length ? candidates[0].file : null;
}

function stripGeneratedChunkHeader(text) {
  const normalized = text.replace(/\r\n/g, "\n");

  if (!normalized.includes("PlantProcess IQ Pack B extracted legacy CSS chunk")) {
    return normalized;
  }

  return normalized.replace(/^\/\*[\s\S]*?\*\/\s*/m, "");
}

function reconstructFromChunks() {
  if (!exists(chunkDir)) return null;

  const chunks = fs.readdirSync(chunkDir)
    .filter((name) => /^legacy-\d+\.css$/i.test(name))
    .sort()
    .map((name) => path.join(chunkDir, name));

  if (!chunks.length) return null;

  return chunks.map((file) => stripGeneratedChunkHeader(read(file))).join("\n");
}

function loadOriginalCss() {
  const originalBackup = findNewestOriginalBackup();

  if (originalBackup) {
    console.log("Using original CSS backup: " + rel(originalBackup));
    return read(originalBackup).replace(/\r\n/g, "\n");
  }

  const reconstructed = reconstructFromChunks();
  if (reconstructed) {
    console.log("Using reconstructed CSS from current chunks.");
    return reconstructed;
  }

  if (isFile(legacyFacadePath)) {
    console.log("Using current facade as fallback.");
    return read(legacyFacadePath).replace(/\r\n/g, "\n");
  }

  throw new Error("Could not find legacy CSS source, backup, or chunks.");
}

function updateCssState(line, state) {
  for (let i = 0; i < line.length; i += 1) {
    const ch = line[i];
    const next = line[i + 1];

    if (state.inBlockComment) {
      if (ch === "*" && next === "/") {
        state.inBlockComment = false;
        i += 1;
      }
      continue;
    }

    if (state.inString) {
      if (state.escape) {
        state.escape = false;
        continue;
      }

      if (ch === "\\") {
        state.escape = true;
        continue;
      }

      if (ch === state.inString) {
        state.inString = null;
      }

      continue;
    }

    if (ch === "/" && next === "*") {
      state.inBlockComment = true;
      i += 1;
      continue;
    }

    if (ch === "'" || ch === '"') {
      state.inString = ch;
      continue;
    }

    if (ch === "{") {
      state.depth += 1;
      continue;
    }

    if (ch === "}") {
      state.depth = Math.max(0, state.depth - 1);
      continue;
    }
  }
}

function splitCssSafely(cssText) {
  const lines = cssText.replace(/\r\n/g, "\n").split("\n");
  const chunks = [];
  let current = [];
  const state = {
    depth: 0,
    inBlockComment: false,
    inString: null,
    escape: false
  };

  const targetLines = 320;

  for (const line of lines) {
    current.push(line);
    updateCssState(line, state);

    if (current.length >= targetLines && state.depth === 0 && !state.inBlockComment && !state.inString) {
      chunks.push(current);
      current = [];
    }
  }

  if (current.length) {
    chunks.push(current);
  }

  return chunks.map((chunk) => chunk.join("\n").trimEnd() + "\n");
}

function assertCssChunksSafe(chunks) {
  const oversized = [];

  chunks.forEach((chunk, index) => {
    const lines = chunk.split("\n").length;
    if (lines > 450) {
      oversized.push({
        chunk: index + 1,
        lines
      });
    }

    const state = {
      depth: 0,
      inBlockComment: false,
      inString: null,
      escape: false
    };

    for (const line of chunk.split("\n")) {
      updateCssState(line, state);
    }

    if (state.depth !== 0 || state.inBlockComment || state.inString) {
      throw new Error(
        "Unsafe CSS split detected in chunk " +
          (index + 1) +
          ": depth=" +
          state.depth +
          ", inBlockComment=" +
          state.inBlockComment +
          ", inString=" +
          state.inString
      );
    }
  });

  if (oversized.length) {
    throw new Error("CSS-safe split produced oversized chunks: " + JSON.stringify(oversized, null, 2));
  }
}

function rewriteCssChunks(cssText) {
  backup(legacyFacadePath);

  if (exists(chunkDir)) {
    for (const file of walk(chunkDir)) {
      if (isFile(file)) {
        backup(file);
        fs.unlinkSync(file);
      }
    }
  }

  ensureDir(chunkDir);

  const chunks = splitCssSafely(cssText);
  assertCssChunksSafe(chunks);

  const written = [];

  chunks.forEach((chunk, index) => {
    const name = "legacy-" + String(index + 1).padStart(3, "0") + ".css";
    const file = path.join(chunkDir, name);

    const content = [
      "/*",
      "  PlantProcess IQ Pack B extracted legacy CSS chunk.",
      "  CSS-safe split: top-level block boundary only.",
      "  Generated: " + new Date().toISOString(),
      "*/",
      "",
      chunk
    ].join("\n");

    write(file, content);
    written.push({
      path: rel(file),
      lines: lineCount(file)
    });
  });

  const facade = [
    "/*",
    "  PlantProcess IQ Pack B CSS retirement facade.",
    "  Original legacy CSS is split into CSS-safe chunks.",
    "  Keep this as the single import point until CSS modules fully replace the legacy layer.",
    "*/",
    "",
    ...written.map((item) => '@import "./legacy-chunks/' + path.basename(item.path) + '";'),
    ""
  ].join("\n");

  write(legacyFacadePath, facade);

  return {
    facade: rel(legacyFacadePath),
    facadeLines: lineCount(legacyFacadePath),
    chunks: written
  };
}

console.log("=================================================================================================");
console.log("PlantProcess IQ Pack B-1 CSS safe-split repair");
console.log("=================================================================================================");
console.log("Backup folder: " + backupRoot);

ensureDir(backupRoot);

const css = loadOriginalCss();
const result = rewriteCssChunks(css);

let evidence = isFile(evidencePath)
  ? read(evidencePath).replace(/\r\n/g, "\n")
  : "# PlantProcess IQ Pack B Evidence\n";

if (!evidence.includes("PPIQ_PACK_B1_CSS_SAFE_SPLIT_REPAIR")) {
  evidence += [
    "",
    "## Pack B-1 CSS safe-split repair",
    "",
    "- Marker: `PPIQ_PACK_B1_CSS_SAFE_SPLIT_REPAIR`.",
    "- Rebuilt `phase56-migrated-legacy.css` chunks using brace-aware top-level CSS block boundaries.",
    "- Fixed the previous unsafe fixed-line split that caused `Unclosed block` in `legacy-001.css`.",
    "- Facade: `" + result.facade + "`.",
    "- Chunk count: " + result.chunks.length + ".",
    ""
  ].join("\n");

  write(evidencePath, evidence);
}

run("node --check Pack B closure validator", ["node", "--check", "tools/pack-b/validate-pack-b-p05-closure.cjs"]);
run("Pack B brand-token gate", ["node", "tools/pack-b/validate-raw-brand-tokens.cjs"]);
run("Frontend npm build", npmArgs("run", "build"), frontendRoot);

if (isFile(path.join(root, "tools", "phase56", "validate-phase56.cjs"))) {
  run("Phase 5/6 regression validation", ["node", "tools/phase56/validate-phase56.cjs"]);
}

run("Pack B closure gate preview", ["node", "tools/pack-b/validate-pack-b-p05-closure.cjs"], root, true);

console.log("");
console.log("=================================================================================================");
console.log("Pack B-1 CSS safe-split repair completed.");
console.log("T-038 should now be build-safe. T-036/T-037 may still remain as real refactor blockers.");
console.log("Backup: " + backupRoot);
console.log("=================================================================================================");
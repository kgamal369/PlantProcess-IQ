const fs = require("fs");
const path = require("path");

const root = process.cwd();
const snapshotPath = path.join(root, "docs", "pack-d", "PACK_D1_ROUTE_CONTRACT_SNAPSHOT.json");
const backendRoot = path.join(root, "Backend");

function exists(file) { return fs.existsSync(file); }
function isFile(file) { return exists(file) && fs.statSync(file).isFile(); }
function read(file) { return fs.readFileSync(file, "utf8"); }
function rel(file) { return path.relative(root, file).split(path.sep).join("/"); }
function walk(dir) {
  if (!exists(dir)) return [];
  return fs.readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      if (["bin", "obj", ".git", ".vs"].includes(entry.name)) return [];
      return walk(full);
    }
    return [full];
  });
}

function extractRouteContractsFromFile(file) {
  const text = read(file).replace(/\r\n/g, "\n");
  const lines = text.split("\n");
  const routes = [];
  const routeRegex = /\.(MapGet|MapPost|MapPut|MapDelete|MapPatch|MapMethods)\s*\(\s*(?:[A-Za-z0-9_]+\s*,\s*)?(@?["'])([^"']+)\2/g;
  let match;

  while ((match = routeRegex.exec(text)) !== null) {
    const before = text.slice(0, match.index);
    const line = before.split("\n").length;
    const context = lines.slice(Math.max(0, line - 4), Math.min(lines.length, line + 8)).join("\n");
    const withName = context.match(/\.WithName\s*\(\s*(@?["'])([^"']+)\1\s*\)/);
    const tag = context.match(/\.WithTags\s*\(\s*(@?["'])([^"']+)\1\s*\)/);
    const group = context.match(/\bMapGroup\s*\(\s*(@?["'])([^"']+)\1\s*\)/);

    routes.push({
      method: match[1].replace("Map", "").toUpperCase(),
      route: match[3],
      withName: withName ? withName[2] : null,
      tag: tag ? tag[2] : null,
      nearestGroup: group ? group[2] : null
    });
  }

  return routes;
}

function buildCurrentRouteKeys() {
  const files = walk(backendRoot).filter((file) => file.endsWith(".cs"));
  const routes = [];

  for (const file of files) {
    const relative = rel(file);
    if (!relative.includes("/Endpoints/") && !relative.endsWith("/Program.cs")) continue;
    if (!read(file).includes("Map")) continue;
    routes.push(...extractRouteContractsFromFile(file));
  }

  return routes.map((route) => {
    return [
      route.method,
      route.route,
      route.withName || "",
      route.tag || "",
      route.nearestGroup || ""
    ].join("|");
  }).sort();
}

if (!isFile(snapshotPath)) {
  console.error("Missing route snapshot: " + snapshotPath);
  process.exit(1);
}

const baseline = JSON.parse(read(snapshotPath));
const baselineKeys = baseline.routes.map((route) => {
  return [
    route.method,
    route.route,
    route.withName || "",
    route.tag || "",
    route.nearestGroup || ""
  ].join("|");
}).sort();

const currentKeys = buildCurrentRouteKeys();

const baselineSet = new Set(baselineKeys);
const currentSet = new Set(currentKeys);

const missing = baselineKeys.filter((key) => !currentSet.has(key));
const added = currentKeys.filter((key) => !baselineSet.has(key));

if (missing.length || added.length) {
  console.error("Pack D route-contract snapshot mismatch.");
  console.error(JSON.stringify({ missing, added }, null, 2));
  process.exit(1);
}

console.log("Pack D route-contract snapshot validation passed.");
console.log("Routes checked: " + baselineKeys.length);

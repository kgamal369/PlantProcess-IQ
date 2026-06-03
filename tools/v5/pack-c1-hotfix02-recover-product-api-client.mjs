import fs from "node:fs";
import path from "node:path";
import childProcess from "node:child_process";

const root = process.cwd();
const frontendRoot = path.join(root, "Frontend", "PlantProcess.Web");
const srcRoot = path.join(frontendRoot, "src");
const apiRoot = path.join(srcRoot, "api");
const reportDir = path.join(root, "Documentation", "v5");

fs.mkdirSync(reportDir, { recursive: true });

function exists(file) {
  return fs.existsSync(file);
}

function read(file) {
  return fs.readFileSync(file, "utf8");
}

function write(file, text) {
  fs.mkdirSync(path.dirname(file), { recursive: true });
  fs.writeFileSync(file, text, "utf8");
}

function toPosix(value) {
  return value.replaceAll(path.sep, "/");
}

function stripExtension(value) {
  return value.replace(/\.(ts|tsx|js|jsx)$/, "");
}

function relativeImport(fromFile, toFile) {
  let relative = path.relative(path.dirname(fromFile), toFile);
  relative = stripExtension(toPosix(relative));
  if (!relative.startsWith(".")) relative = `./${relative}`;
  return relative;
}

function walk(dir, output = []) {
  if (!fs.existsSync(dir)) return output;

  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);

    if (entry.isDirectory()) {
      const ignored = ["node_modules", "dist", "build", "coverage", ".vite", "bin", "obj", ".git"];
      if (ignored.includes(entry.name)) continue;
      walk(full, output);
    } else if (/\.(ts|tsx|js|jsx)$/.test(entry.name)) {
      output.push(full);
    }
  }

  return output;
}

function walkTextFiles(dir, output = []) {
  if (!fs.existsSync(dir)) return output;

  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);

    if (entry.isDirectory()) {
      const relative = toPosix(path.relative(root, full));
      if (
        /node_modules|\.git|bin|obj|dist|build|coverage/i.test(relative) ||
        /_pack_c1_hotfix/i.test(relative)
      ) {
        continue;
      }

      walkTextFiles(full, output);
    } else if (/\.(txt|md)$/i.test(entry.name)) {
      output.push(full);
    }
  }

  return output;
}

function runGitShow(repoPath) {
  try {
    return childProcess.execFileSync(
      "git",
      ["show", `HEAD:${repoPath}`],
      { cwd: root, encoding: "utf8", stdio: ["ignore", "pipe", "ignore"] }
    );
  } catch {
    return null;
  }
}

function isGoodApiClientSource(text) {
  if (!text) return false;

  const required = [
    "export const plantProcessApi",
    "export interface DashboardFilters",
    "export interface DashboardWidgetQueryResult",
    "export interface AdminOverview",
    "export interface ProviderTypeRecord",
    "getAdminOverview",
    "getDashboardReferenceData",
    "queryDashboardWidget",
    "searchDashboardMaterials"
  ];

  return text.length > 20000 && required.every((needle) => text.includes(needle));
}

function extractFileBlocksFromAudit(text) {
  const blocks = [];
  const regex = /\[FILE_START\]\s*\n\[METADATA:[\s\S]*?Path='([^']*plantProcessApi\.ts)'[\s\S]*?\]\s*\n\n([\s\S]*?)\n\[FILE_END\]/g;

  for (const match of text.matchAll(regex)) {
    blocks.push({
      path: match[1],
      content: match[2]
    });
  }

  return blocks;
}

function recoverPlantProcessApiSource() {
  const gitCandidates = [
    "Frontend/PlantProcess.Web/src/api/legacy/plantProcessApi.ts",
    "Frontend/PlantProcess.Web/src/api/plantProcessApi.ts"
  ];

  for (const candidate of gitCandidates) {
    const text = runGitShow(candidate);
    if (isGoodApiClientSource(text)) {
      return {
        sourceKind: "git",
        sourcePath: candidate,
        text
      };
    }
  }

  const localCandidates = [
    path.join(apiRoot, "legacy", "plantProcessApi.ts"),
    path.join(apiRoot, "plantProcessApi.ts"),
    path.join(apiRoot, "productApiClient.ts"),
    path.join(apiRoot, "productCoreApiClient.ts")
  ];

  for (const candidate of localCandidates) {
    if (!exists(candidate)) continue;

    const text = read(candidate);
    if (isGoodApiClientSource(text)) {
      return {
        sourceKind: "local-file",
        sourcePath: toPosix(path.relative(root, candidate)),
        text
      };
    }
  }

  const documentationRoot = path.join(root, "Documentation");
  const textFiles = walkTextFiles(documentationRoot)
    .sort()
    .reverse();

  const auditBlocks = [];

  for (const file of textFiles) {
    let text = "";

    try {
      text = read(file);
    } catch {
      continue;
    }

    if (!text.includes("plantProcessApi.ts")) continue;

    const blocks = extractFileBlocksFromAudit(text);

    for (const block of blocks) {
      if (isGoodApiClientSource(block.content)) {
        auditBlocks.push({
          sourceKind: "documentation-audit",
          sourcePath: `${toPosix(path.relative(root, file))} :: ${block.path}`,
          text: block.content
        });
      }
    }
  }

  if (auditBlocks.length > 0) {
    auditBlocks.sort((a, b) => b.text.length - a.text.length);
    return auditBlocks[0];
  }

  throw new Error(
    [
      "Could not recover the old full plantProcessApi source.",
      "Tried git, current files, and Documentation/*.txt audit packages.",
      "Generate or restore an UltimateAudit file that contains Frontend\\PlantProcess.Web\\src\\api\\legacy\\plantProcessApi.ts, then rerun."
    ].join(" ")
  );
}

function recoverLegacyHardeningSource() {
  const gitCandidates = [
    "Frontend/PlantProcess.Web/src/api/legacy/legacyApiHardening.ts",
    "Frontend/PlantProcess.Web/src/api/legacyApiHardening.ts"
  ];

  for (const candidate of gitCandidates) {
    const text = runGitShow(candidate);
    if (text && text.includes("export") && text.length > 500) {
      return {
        sourceKind: "git",
        sourcePath: candidate,
        text
      };
    }
  }

  const localCandidates = [
    path.join(apiRoot, "legacy", "legacyApiHardening.ts"),
    path.join(apiRoot, "productApiHardening.ts")
  ];

  for (const candidate of localCandidates) {
    if (!exists(candidate)) continue;
    const text = read(candidate);
    if (text && text.includes("export") && text.length > 500) {
      return {
        sourceKind: "local-file",
        sourcePath: toPosix(path.relative(root, candidate)),
        text
      };
    }
  }

  const documentationRoot = path.join(root, "Documentation");
  const textFiles = walkTextFiles(documentationRoot)
    .sort()
    .reverse();

  const regex = /\[FILE_START\]\s*\n\[METADATA:[\s\S]*?Path='([^']*legacyApiHardening\.ts)'[\s\S]*?\]\s*\n\n([\s\S]*?)\n\[FILE_END\]/g;

  for (const file of textFiles) {
    let text = "";
    try {
      text = read(file);
    } catch {
      continue;
    }

    if (!text.includes("legacyApiHardening.ts")) continue;

    for (const match of text.matchAll(regex)) {
      const content = match[2];
      if (content.includes("export") && content.length > 500) {
        return {
          sourceKind: "documentation-audit",
          sourcePath: `${toPosix(path.relative(root, file))} :: ${match[1]}`,
          text: content
        };
      }
    }
  }

  return null;
}

function normalizeProductCore(text) {
  let next = text;

  // Remove audit read error markers just in case.
  next = next.replace(/\[READ_ERROR\][\s\S]*$/g, "");

  // Product-name the runtime symbol and type names.
  next = next.replaceAll("plantProcessApi", "productApi");
  next = next.replaceAll("PlantProcessApi", "ProductApi");

  // Make imports valid from src/api/productCoreApiClient.ts.
  next = next.replace(/from\s+["']\.\.\/apiConfig["']/g, 'from "./apiConfig"');
  next = next.replace(/from\s+["']\.\/apiConfig["']/g, 'from "./apiConfig"');

  next = next.replace(/from\s+["']\.\.\/http["']/g, 'from "./http"');
  next = next.replace(/from\s+["']\.\/http["']/g, 'from "./http"');

  next = next.replace(/from\s+["']\.\/legacyApiHardening["']/g, 'from "./productApiHardening"');
  next = next.replace(/from\s+["']\.\.\/legacyApiHardening["']/g, 'from "./productApiHardening"');

  // Guard against broken old spread typo occasionally produced in old snapshots.
  next = next.replaceAll("{\n      .dashboardQuery(filters),", "{\n      ...dashboardQuery(filters),");
  next = next.replaceAll("{ .dashboardQuery(filters),", "{ ...dashboardQuery(filters),");

  if (!next.includes("export const productApi")) {
    throw new Error("Recovered API source did not convert to export const productApi.");
  }

  if (next.includes("plantProcessApi")) {
    throw new Error("Recovered API source still contains plantProcessApi after normalization.");
  }

  return next;
}

function normalizeHardening(text) {
  let next = text;

  next = next.replaceAll("plantProcessApi", "productApi");
  next = next.replaceAll("PlantProcessApi", "ProductApi");

  next = next.replace(/from\s+["']\.\.\/apiConfig["']/g, 'from "./apiConfig"');
  next = next.replace(/from\s+["']\.\/apiConfig["']/g, 'from "./apiConfig"');
  next = next.replace(/from\s+["']\.\.\/http["']/g, 'from "./http"');
  next = next.replace(/from\s+["']\.\/http["']/g, 'from "./http"');

  return next;
}

function createMinimalHardening() {
  return `import { API_BASE_URL } from "./apiConfig";

export type QueryParams = Record<string, string | number | boolean | null | undefined>;

function buildUrl(path: string, query?: QueryParams): string {
  const url = new URL(path.startsWith("/") ? path : \`/\${path}\`, API_BASE_URL);

  for (const [key, value] of Object.entries(query ?? {})) {
    if (value !== undefined && value !== null && value !== "") {
      url.searchParams.set(key, String(value));
    }
  }

  return url.toString();
}

async function requestJson<T>(
  method: string,
  path: string,
  body?: unknown,
  query?: QueryParams
): Promise<T> {
  const response = await fetch(buildUrl(path, query), {
    method,
    headers: {
      "Content-Type": "application/json"
    },
    body: body === undefined ? undefined : JSON.stringify(body)
  });

  if (!response.ok) {
    const text = await response.text();
    throw new Error(\`API request failed: \${method} \${path} \${response.status} \${text}\`);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

export function getJson<T>(path: string, query?: QueryParams): Promise<T> {
  return requestJson<T>("GET", path, undefined, query);
}

export function postJson<T>(path: string, body?: unknown, query?: QueryParams): Promise<T> {
  return requestJson<T>("POST", path, body, query);
}

export function patchJson<T>(path: string, body?: unknown, query?: QueryParams): Promise<T> {
  return requestJson<T>("PATCH", path, body, query);
}

export function deleteJson<T>(path: string, query?: QueryParams): Promise<T> {
  return requestJson<T>("DELETE", path, undefined, query);
}
`;
}

function createFacade() {
  return `import { productApi as coreProductApi } from "./productCoreApiClient";
import { postJson } from "./productApiHardening";

export type * from "./productCoreApiClient";

type CoreProductApi = typeof coreProductApi;

type ProviderTypeRecord = Awaited<
  ReturnType<CoreProductApi["getConnectorProviderTypes"]>
>[number];

type DashboardDefinitionRecordAlias = Awaited<
  ReturnType<CoreProductApi["getDashboardDefinition"]>
>;

type CreateDashboardWidgetPayload = Parameters<
  CoreProductApi["createDashboardWidgetDefinition"]
>[1];

type UpdateDashboardWidgetPayload = Parameters<
  CoreProductApi["updateDashboardWidgetDefinition"]
>[2];

type CloneDashboardWidgetPayload = Parameters<
  CoreProductApi["cloneDashboardWidgetDefinition"]
>[2];

type DashboardWidgetQueryResultAlias = Awaited<
  ReturnType<CoreProductApi["queryDashboardWidget"]>
>;

type WidgetQueryExpressionRequest = {
  dashboardDefinitionId?: string;
  widgetDefinitionId?: string;
  expression?: string;
  query?: unknown;
  options?: unknown;
};

type ProductApiCompatibilityAliases = {
  getProviderTypes: () => Promise<ProviderTypeRecord[]>;

  getAdminJobs: CoreProductApi["getAdminJobsMonitor"];

  getDashboardDefinitionById: (
    dashboardDefinitionId: string
  ) => Promise<DashboardDefinitionRecordAlias>;

  createDashboardWidget: (
    dashboardDefinitionId: string,
    payload: CreateDashboardWidgetPayload
  ) => ReturnType<CoreProductApi["createDashboardWidgetDefinition"]>;

  updateDashboardWidget: (
    dashboardDefinitionId: string,
    widgetDefinitionId: string,
    payload: UpdateDashboardWidgetPayload
  ) => ReturnType<CoreProductApi["updateDashboardWidgetDefinition"]>;

  deleteDashboardWidget: (
    dashboardDefinitionId: string,
    widgetDefinitionId: string
  ) => ReturnType<CoreProductApi["deactivateDashboardWidgetDefinition"]>;

  cloneDashboardWidget: (
    dashboardDefinitionId: string,
    widgetDefinitionId: string,
    payload: CloneDashboardWidgetPayload
  ) => ReturnType<CoreProductApi["cloneDashboardWidgetDefinition"]>;

  executeWidgetExpression: (
    request: WidgetQueryExpressionRequest
  ) => Promise<DashboardWidgetQueryResultAlias>;

  getLicenseStatus: () => Promise<unknown>;
  getLicensePlans: () => Promise<unknown>;
  getMlReadiness: () => Promise<unknown>;
  getDemoLifecycle: () => Promise<unknown>;
};

export const productApi: CoreProductApi & ProductApiCompatibilityAliases = {
  ...coreProductApi,

  getProviderTypes: () => coreProductApi.getConnectorProviderTypes(),

  getAdminJobs: coreProductApi.getAdminJobsMonitor,

  getDashboardDefinitionById: (dashboardDefinitionId: string) =>
    coreProductApi.getDashboardDefinition(dashboardDefinitionId),

  createDashboardWidget: (
    dashboardDefinitionId: string,
    payload: CreateDashboardWidgetPayload
  ) =>
    coreProductApi.createDashboardWidgetDefinition(
      dashboardDefinitionId,
      payload
    ),

  updateDashboardWidget: (
    dashboardDefinitionId: string,
    widgetDefinitionId: string,
    payload: UpdateDashboardWidgetPayload
  ) =>
    coreProductApi.updateDashboardWidgetDefinition(
      dashboardDefinitionId,
      widgetDefinitionId,
      payload
    ),

  deleteDashboardWidget: (
    dashboardDefinitionId: string,
    widgetDefinitionId: string
  ) =>
    coreProductApi.deactivateDashboardWidgetDefinition(
      dashboardDefinitionId,
      widgetDefinitionId
    ),

  cloneDashboardWidget: (
    dashboardDefinitionId: string,
    widgetDefinitionId: string,
    payload: CloneDashboardWidgetPayload
  ) =>
    coreProductApi.cloneDashboardWidgetDefinition(
      dashboardDefinitionId,
      widgetDefinitionId,
      payload
    ),

  executeWidgetExpression: (request: WidgetQueryExpressionRequest) =>
    postJson<DashboardWidgetQueryResultAlias>(
      "/dashboarding/widget-query-expression/execute",
      request
    ),

  getLicenseStatus: () => coreProductApi.getLicenseStatus(),

  getLicensePlans: () => coreProductApi.getLicensePlans(),

  getMlReadiness: () => coreProductApi.getMlReadiness(),

  getDemoLifecycle: () => coreProductApi.getDemoLifecycle(),
};

export default productApi;
`;
}

const recoveredApi = recoverPlantProcessApiSource();
const recoveredHardening = recoverLegacyHardeningSource();

const productCoreFile = path.join(apiRoot, "productCoreApiClient.ts");
const productFacadeFile = path.join(apiRoot, "productApiClient.ts");
const productHardeningFile = path.join(apiRoot, "productApiHardening.ts");

write(productCoreFile, normalizeProductCore(recoveredApi.text));

if (recoveredHardening) {
  write(productHardeningFile, normalizeHardening(recoveredHardening.text));
} else if (!exists(productHardeningFile)) {
  write(productHardeningFile, createMinimalHardening());
}

write(productFacadeFile, createFacade());

// Rewrite frontend imports and runtime symbol usage.
const replacements = [];
const sourceFiles = walk(srcRoot);

for (const file of sourceFiles) {
  let text = read(file);
  const original = text;

  // Symbol rename everywhere inside frontend source.
  text = text.replaceAll("plantProcessApi", "productApi");

  // Normalize accidental imports to the product facade.
  text = text.replace(
    /((?:import|export)\s+(?:type\s+)?(?:[^'"]*?\s+from\s+)?["'])([^"']*(?:legacy\/)?productApi)(["'])/g,
    (match, prefix, specifier, suffix) => {
      if (
        specifier.endsWith("productApiClient") ||
        specifier.endsWith("productCoreApiClient") ||
        specifier.endsWith("productApiHardening")
      ) {
        return match;
      }

      const nextSpecifier = relativeImport(file, productFacadeFile);
      replacements.push({
        file: toPosix(path.relative(root, file)),
        from: specifier,
        to: nextSpecifier
      });
      return `${prefix}${nextSpecifier}${suffix}`;
    }
  );

  text = text.replace(
    /(import\(\s*["'])([^"']*(?:legacy\/)?productApi)(["']\s*\))/g,
    (match, prefix, specifier, suffix) => {
      if (
        specifier.endsWith("productApiClient") ||
        specifier.endsWith("productCoreApiClient") ||
        specifier.endsWith("productApiHardening")
      ) {
        return match;
      }

      const nextSpecifier = relativeImport(file, productFacadeFile);
      replacements.push({
        file: toPosix(path.relative(root, file)),
        from: specifier,
        to: nextSpecifier
      });
      return `${prefix}${nextSpecifier}${suffix}`;
    }
  );

  if (text !== original) {
    write(file, text);
  }
}

// Patch React key/symbol generic issues caused by keyof after type restoration.
const activeFilterFile = path.join(srcRoot, "components", "ActiveFilterChips.tsx");
if (exists(activeFilterFile)) {
  let text = read(activeFilterFile);
  text = text.replace(/key=\{key\}/g, "key={String(key)}");
  write(activeFilterFile, text);
}

const dashboardFilterContextFile = path.join(srcRoot, "state", "DashboardFilterContext.tsx");
if (exists(dashboardFilterContextFile)) {
  let text = read(dashboardFilterContextFile);
  text = text.replace(/Object\.keys\(([^)]*)\)/g, "Object.keys($1) as string[]");
  text = text.replace(/searchParams\.get\(key\)/g, "searchParams.get(String(key))");
  text = text.replace(/next\.set\(key,/g, "next.set(String(key),");
  text = text.replace(/\[key\]/g, "[String(key)]");
  write(dashboardFilterContextFile, text);
}

// Delete retired legacy files/folder.
const deleted = [];
const oldFiles = [
  path.join(apiRoot, "plantProcessApi.ts"),
  path.join(apiRoot, "legacy", "plantProcessApi.ts"),
  path.join(apiRoot, "legacy", "legacyApiHardening.ts")
];

for (const file of oldFiles) {
  if (exists(file)) {
    fs.unlinkSync(file);
    deleted.push(toPosix(path.relative(root, file)));
  }
}

const legacyDir = path.join(apiRoot, "legacy");
if (exists(legacyDir) && fs.readdirSync(legacyDir).length === 0) {
  fs.rmdirSync(legacyDir);
  deleted.push(toPosix(path.relative(root, legacyDir)));
}

// Final offender report.
const offenders = [];

for (const file of walk(srcRoot)) {
  const text = read(file);
  if (text.includes("plantProcessApi")) {
    offenders.push({
      file: toPosix(path.relative(root, file)),
      count: (text.match(/plantProcessApi/g) ?? []).length
    });
  }
}

const report = {
  generatedAtUtc: new Date().toISOString(),
  recoveredApiSource: {
    kind: recoveredApi.sourceKind,
    path: recoveredApi.sourcePath
  },
  recoveredHardeningSource: recoveredHardening
    ? {
        kind: recoveredHardening.sourceKind,
        path: recoveredHardening.sourcePath
      }
    : null,
  productCoreFile: toPosix(path.relative(root, productCoreFile)),
  productFacadeFile: toPosix(path.relative(root, productFacadeFile)),
  productHardeningFile: toPosix(path.relative(root, productHardeningFile)),
  replacements,
  deleted,
  offenders
};

write(
  path.join(reportDir, "pack-c1-hotfix02-product-api-recovery-report.json"),
  JSON.stringify(report, null, 2)
);

console.log(JSON.stringify(report, null, 2));

if (offenders.length > 0) {
  console.error("Remaining plantProcessApi offenders still exist.");
  process.exit(3);
}

console.log("[pack-c1-hotfix02] product API recovery completed.");
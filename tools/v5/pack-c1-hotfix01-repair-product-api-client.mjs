import fs from "node:fs";
import path from "node:path";

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
      if (["node_modules", "dist", "build", "coverage", ".vite"].includes(entry.name)) continue;
      walk(full, output);
    } else if (/\.(ts|tsx|js|jsx)$/.test(entry.name)) {
      output.push(full);
    }
  }

  return output;
}

function findLatestBackupFile(relativeCandidates) {
  const backupRoots = fs
    .readdirSync(root, { withFileTypes: true })
    .filter((x) => x.isDirectory() && x.name.startsWith("_pack_c1_legacy_api_retirement_backup_"))
    .map((x) => path.join(root, x.name))
    .sort()
    .reverse();

  for (const backupRoot of backupRoots) {
    for (const relative of relativeCandidates) {
      const candidate = path.join(backupRoot, relative);
      if (exists(candidate)) {
        return candidate;
      }
    }
  }

  return null;
}

const legacyCoreBackup = findLatestBackupFile([
  path.join("Frontend", "PlantProcess.Web", "src", "api", "legacy", "plantProcessApi.ts"),
]);

const legacyHardeningBackup = findLatestBackupFile([
  path.join("Frontend", "PlantProcess.Web", "src", "api", "legacy", "legacyApiHardening.ts"),
]);

if (!legacyCoreBackup) {
  throw new Error(
    "Cannot find backed-up src/api/legacy/plantProcessApi.ts. " +
    "Expected it under the latest _pack_c1_legacy_api_retirement_backup_* folder."
  );
}

if (!legacyHardeningBackup) {
  throw new Error(
    "Cannot find backed-up src/api/legacy/legacyApiHardening.ts. " +
    "Expected it under the latest _pack_c1_legacy_api_retirement_backup_* folder."
  );
}

const productCoreFile = path.join(apiRoot, "productCoreApiClient.ts");
const productHardeningFile = path.join(apiRoot, "productApiHardening.ts");
const productFacadeFile = path.join(apiRoot, "productApiClient.ts");

let hardeningText = read(legacyHardeningBackup);
hardeningText = hardeningText
  .replaceAll("plantProcessApi", "productApi")
  .replaceAll("PlantProcessApi", "ProductApi")
  .replace(/from\s+["']\.\.\/apiConfig["']/g, 'from "./apiConfig"');

write(productHardeningFile, hardeningText);

let coreText = read(legacyCoreBackup);
coreText = coreText
  .replaceAll("plantProcessApi", "productApi")
  .replaceAll("PlantProcessApi", "ProductApi")
  .replace(/from\s+["']\.\.\/apiConfig["']/g, 'from "./apiConfig"')
  .replace(/from\s+["']\.\/legacyApiHardening["']/g, 'from "./productApiHardening"')
  .replace(/from\s+["']\.\.\/legacyApiHardening["']/g, 'from "./productApiHardening"');

write(productCoreFile, coreText);

const facadeText = `import { productApi as coreProductApi } from "./productCoreApiClient";
import { postJson } from "./productApiHardening";

export type * from "./productCoreApiClient";

export * from "./http";
export * from "./admin";
export * from "./dashboarding";
export * from "./integration";
export * from "./analytics";
export * from "./license";
export * from "./demo";
export * from "./ml";

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

write(productFacadeFile, facadeText);

// Rewrite remaining frontend source references.
const sourceFiles = walk(srcRoot);
const replacements = [];

for (const file of sourceFiles) {
  let text = read(file);
  const original = text;

  // Replace old symbol name everywhere in frontend source.
  text = text.replaceAll("plantProcessApi", "productApi");

  // Normalize broken paths created by previous partial codemod.
  text = text.replace(
    /((?:import|export)\s+(?:type\s+)?(?:[^'"]*?\s+from\s+)?["'])([^"']*(?:legacy\/)?productApi)(["'])/g,
    (match, prefix, specifier, suffix) => {
      if (specifier.endsWith("productApiClient") || specifier.endsWith("productCoreApiClient")) {
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
      if (specifier.endsWith("productApiClient") || specifier.endsWith("productCoreApiClient")) {
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

// Delete old legacy files/folders again.
const oldFiles = [
  path.join(apiRoot, "plantProcessApi.ts"),
  path.join(apiRoot, "legacy", "plantProcessApi.ts"),
  path.join(apiRoot, "legacy", "legacyApiHardening.ts")
];

const deleted = [];

for (const file of oldFiles) {
  if (exists(file)) {
    fs.unlinkSync(file);
    deleted.push(toPosix(path.relative(root, file)));
  }
}

const legacyDir = path.join(apiRoot, "legacy");
if (exists(legacyDir)) {
  const remaining = fs.readdirSync(legacyDir);
  if (remaining.length === 0) {
    fs.rmdirSync(legacyDir);
    deleted.push(toPosix(path.relative(root, legacyDir)));
  }
}

// Small strict TS patch: React key cannot be symbol.
const activeFilterFile = path.join(srcRoot, "components", "ActiveFilterChips.tsx");
if (exists(activeFilterFile)) {
  let text = read(activeFilterFile);
  text = text.replace(/key=\{key\}/g, "key={String(key)}");
  write(activeFilterFile, text);
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
  legacyCoreBackup: toPosix(path.relative(root, legacyCoreBackup)),
  legacyHardeningBackup: toPosix(path.relative(root, legacyHardeningBackup)),
  productCoreFile: toPosix(path.relative(root, productCoreFile)),
  productHardeningFile: toPosix(path.relative(root, productHardeningFile)),
  productFacadeFile: toPosix(path.relative(root, productFacadeFile)),
  replacements,
  deleted,
  offenders
};

write(
  path.join(reportDir, "pack-c1-hotfix01-product-api-client-repair-report.json"),
  JSON.stringify(report, null, 2)
);

console.log(JSON.stringify(report, null, 2));

if (offenders.length > 0) {
  console.error("Remaining plantProcessApi offenders still exist.");
  process.exit(3);
}

console.log("[pack-c1-hotfix01] product API client repair completed.");
const fs = require("node:fs");
const path = require("node:path");

const root = process.cwd();

function writeRepo(relativePath, content) {
  const file = path.join(root, relativePath.replaceAll("/", path.sep));
  fs.mkdirSync(path.dirname(file), { recursive: true });
  fs.writeFileSync(file, content.replace(/\r\n/g, "\n"), "utf8");
}

function readRepo(relativePath) {
  return fs.readFileSync(path.join(root, relativePath.replaceAll("/", path.sep)), "utf8");
}

function existsRepo(relativePath) {
  return fs.existsSync(path.join(root, relativePath.replaceAll("/", path.sep)));
}

// ============================================================
// 1. Correct Pack C validator location.
// Previous script accidentally wrote under Frontend/tools/p00.
// ============================================================

writeRepo(
  "tools/p00/validate-p00c-frontend-behavioural-tests.cjs",
  `const fs = require("node:fs");
const path = require("node:path");

const root = process.cwd();
const frontendRoot = path.join(root, "Frontend", "PlantProcess.Web");

const requiredFiles = [
  "src/api/__tests__/apiClient.retry-backoff.test.ts",
  "src/state/__tests__/AuthContext.bootstrap.test.tsx",
  "src/state/__tests__/LicenseContext.gating.test.tsx",
  "src/components/standard/__tests__/DataFetchBoundary.test.tsx"
];

const failures = [];

for (const relativePath of requiredFiles) {
  const file = path.join(frontendRoot, relativePath);

  if (!fs.existsSync(file)) {
    failures.push("Missing Pack C test file: " + relativePath);
    continue;
  }

  const text = fs.readFileSync(file, "utf8");

  if (!text.includes("describe(") || !text.includes("it(")) {
    failures.push("Pack C test file has no Vitest tests: " + relativePath);
  }
}

if (failures.length > 0) {
  console.error("P00C frontend behavioural validation failed:");
  for (const failure of failures) {
    console.error(" - " + failure);
  }
  process.exit(1);
}

console.log("P00C frontend behavioural validation passed.");
console.log("Pack C frontend behavioural test files are present.");
`
);

// Optional cleanup of wrong misplaced validator if it exists.
const misplacedValidator = path.join(
  root,
  "Frontend",
  "tools",
  "p00",
  "validate-p00c-frontend-behavioural-tests.cjs"
);

if (fs.existsSync(misplacedValidator)) {
  fs.rmSync(misplacedValidator, { force: true });
  console.log("Removed misplaced Frontend/tools/p00 validator.");
}

// ============================================================
// 2. Patch backend smoke test to perform dashboard template preflight
//    before calling /analytics/dashboard/definitions.
// ============================================================

const smokePath =
  "Backend/tests/PlantProcess.Api.IntegrationTests/Smoke/ApiEndpointCatalogSmokeTests.cs";

if (!existsRepo(smokePath)) {
  throw new Error("Missing " + smokePath);
}

let smoke = readRepo(smokePath);

if (!smoke.includes("using System.Net.Http.Json;")) {
  smoke = smoke.replace(
    "using System.Net;",
    "using System.Net;\nusing System.Net.Http.Json;"
  );
}

if (!smoke.includes("EnsureDashboardDefinitionPreflightAsync")) {
  smoke = smoke.replace(
    `        var response = await client.GetAsync(url);

        response.StatusCode`,
    `        if (url.StartsWith("/analytics/dashboard/definitions", StringComparison.OrdinalIgnoreCase))
        {
            await EnsureDashboardDefinitionPreflightAsync(client);
        }

        var response = await client.GetAsync(url);

        response.StatusCode`
  );

  smoke = smoke.replace(
    `        response.StatusCode
            .Should()
            .NotBe(HttpStatusCode.Forbidden, $"{name} must accept authenticated admin user role/policy");
    }
}`,
    `        response.StatusCode
            .Should()
            .NotBe(HttpStatusCode.Forbidden, $"{name} must accept authenticated admin user role/policy");
    }

    private static async Task EnsureDashboardDefinitionPreflightAsync(HttpClient client)
    {
        var ensureResponse = await client.PostAsJsonAsync(
            "/analytics/dashboard/definitions/system-templates/ensure",
            new { });

        ensureResponse.StatusCode
            .Should()
            .NotBe(HttpStatusCode.InternalServerError, "dashboard template ensure must not return 500 before listing definitions");

        ensureResponse.StatusCode
            .Should()
            .NotBe(HttpStatusCode.NotFound, "dashboard template ensure route must exist");

        var repairResponse = await client.PostAsJsonAsync(
            "/analytics/dashboard/definitions/system-templates/repair",
            new { });

        repairResponse.StatusCode
            .Should()
            .NotBe(HttpStatusCode.InternalServerError, "dashboard template repair must not return 500 before listing definitions");

        repairResponse.StatusCode
            .Should()
            .NotBe(HttpStatusCode.NotFound, "dashboard template repair route must exist");
    }
}`
  );

  console.log("Patched ApiEndpointCatalogSmokeTests with dashboard definition preflight.");
} else {
  console.log("ApiEndpointCatalogSmokeTests already contains dashboard preflight.");
}

console.log("P00C validator + dashboard smoke preflight patch applied.");

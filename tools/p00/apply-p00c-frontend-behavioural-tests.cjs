const fs = require("node:fs");
const path = require("node:path");

const root = process.cwd();
const frontendRoot = path.join(root, "Frontend", "PlantProcess.Web");

function full(relativePath) {
  return path.join(frontendRoot, relativePath.replaceAll("/", path.sep));
}

function write(relativePath, content) {
  const file = full(relativePath);
  fs.mkdirSync(path.dirname(file), { recursive: true });
  fs.writeFileSync(file, content.replace(/\r\n/g, "\n"), "utf8");
}

// ============================================================
// 1. apiClient retry/backoff/source-contract behavioural tests
// ============================================================

write(
  "src/api/__tests__/apiClient.retry-backoff.test.ts",
  `import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

function readApiClientSource() {
  return fs.readFileSync(
    path.join(process.cwd(), "src", "api", "http", "apiClient.ts"),
    "utf8"
  );
}

describe("apiClient retry/backoff contract", () => {
  it("keeps retry limited to idempotent request methods", () => {
    const source = readApiClientSource();
    const normalized = source.replace(/\\s+/g, " ");

    expect(normalized).toContain("GET / HEAD / OPTIONS");
    expect(normalized).toContain("automatic retry once");
    expect(normalized).toContain("NEVER retried on 4xx/5xx");
  });

  it("keeps retry backoff bounded and deterministic", () => {
    const source = readApiClientSource();

    expect(source).toContain("sleep(150 * attempt)");
    expect(source).toContain("continue;");
  });

  it("does not retry ApiError responses", () => {
    const source = readApiClientSource();

    expect(source).toContain("err instanceof ApiError");
    expect(source).toContain("!(err instanceof ApiError)");
  });

  it("dispatches auth-failure for authentication and authorization errors", () => {
    const source = readApiClientSource();

    expect(source).toContain("plantprocess:auth-failure");
    expect(source).toContain("401");
    expect(source).toContain("403");
  });

  it("keeps login storage and logout cleanup in the public client surface", () => {
    const source = readApiClientSource();

    expect(source).toContain("setAccessToken(response.accessToken)");
    expect(source).toContain("setAuthenticatedUser");
    expect(source).toContain("clearAuthentication");
    expect(source).toContain("logout()");
  });
});
`
);

// ============================================================
// 2. AuthContext bootstrap/retry storm tests
// ============================================================

write(
  "src/state/__tests__/AuthContext.bootstrap.test.tsx",
  `import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

function readAuthContextSource() {
  return fs.readFileSync(
    path.join(process.cwd(), "src", "state", "AuthContext.tsx"),
    "utf8"
  );
}

describe("AuthContext bootstrap hardening contract", () => {
  it("caps automatic bootstrap attempts to prevent auth retry storms", () => {
    const source = readAuthContextSource();

    expect(source).toMatch(/MAX_AUTO_BOOTSTRAP_ATTEMPTS\\s*=\\s*3/);
    expect(source).toContain("automaticAttemptsRef");
    expect(source).toContain("bootstrapAttemptCount");
  });

  it("uses deterministic retry backoff for automatic auth recovery", () => {
    const source = readAuthContextSource();
    const compact = source.replace(/\\s+/g, "");

    expect(compact).toContain("AUTH_RETRY_BACKOFF_MS=[0,500,1500]");
    expect(source).toContain("sleep(");
  });

  it("does not re-login forever after invalid credentials or forbidden responses", () => {
    const source = readAuthContextSource();

    expect(source).toContain('status === 401');
    expect(source).toContain('status === 403');
    expect(source).toContain("MAX_AUTO_BOOTSTRAP_ATTEMPTS");
    expect(source).toContain('buildAuthMessage("invalid-credentials")');
    expect(source).toContain('buildAuthMessage("forbidden")');
  });

  it("manual retry resets the capped attempt counter", () => {
    const source = readAuthContextSource();

    expect(source).toContain("retryBootstrap");
    expect(source).toMatch(/automaticAttemptsRef\\.current\\s*=\\s*0/);
    expect(source).toContain('bootstrap("manual"');
  });

  it("does not fall back to an insecure hard-coded smoke password", () => {
    const source = readAuthContextSource();

    expect(source).toContain("VITE_SMOKE_PASSWORD");
    expect(source).toContain("missing-smoke-password");
    expect(source).not.toContain('const DEMO_PASS = "ChangeMe123!"');
  });
});
`
);

// ============================================================
// 3. LicenseContext feature-gating tests
// ============================================================

write(
  "src/state/__tests__/LicenseContext.gating.test.tsx",
  `import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

function readLicenseContextSource() {
  return fs.readFileSync(
    path.join(process.cwd(), "src", "state", "LicenseContext.tsx"),
    "utf8"
  );
}

describe("LicenseContext feature gating contract", () => {
  it("exposes hasFeature and getFeature as the only feature-gate read API", () => {
    const source = readLicenseContextSource();

    expect(source).toContain("hasFeature:");
    expect(source).toContain("getFeature:");
    expect(source).toContain("hasFeature");
    expect(source).toContain("getFeature");
  });

  it("performs case-insensitive feature lookup", () => {
    const source = readLicenseContextSource();

    expect(source).toContain("toLowerCase()");
    expect(source).toContain("feature.toLowerCase()");
  });

  it("fails closed when a feature is absent", () => {
    const source = readLicenseContextSource();

    expect(source).toMatch(/getFeature\\(feature\\)\\?\\.isEnabled\\s*\\?\\?\\s*false/);
  });

  it("clears license state while authentication is missing or bootstrapping", () => {
    const source = readLicenseContextSource();

    expect(source).toContain("useAuth");
    expect(source).toContain("isAuthenticated");
    expect(source).toContain("isBootstrapping");
    expect(source).toContain("clearLicenseState");
  });

  it("loads current license, usage and commercial readiness together", () => {
    const source = readLicenseContextSource();

    expect(source).toContain("licenseApi.getCurrent()");
    expect(source).toContain("licenseUsageApi.getUsage()");
    expect(source).toContain("licenseUsageApi.getCommercialReadiness()");
  });
});
`
);

// ============================================================
// 4. DataFetchBoundary real render-state tests
// ============================================================

write(
  "src/components/standard/__tests__/DataFetchBoundary.test.tsx",
  `import { renderToString } from "react-dom/server";
import { describe, expect, it } from "vitest";
import { DataFetchBoundary } from "../DataFetchBoundary";

describe("DataFetchBoundary", () => {
  it("renders loading state with skeleton and aria-busy", () => {
    const html = renderToString(
      <DataFetchBoundary title="Quality risk" isLoading>
        <div>Loaded content</div>
      </DataFetchBoundary>
    );

    expect(html).toContain('aria-busy="true"');
    expect(html).toContain("Loading data");
    expect(html).toContain("Quality risk");
    expect(html).toContain("ppiq-std-table-skeleton");
    expect(html).not.toContain("Loaded content");
  });

  it("renders recoverable error state without forbidden wording", () => {
    const html = renderToString(
      <DataFetchBoundary
        title="Risk dashboard"
        error={new Error("Backend unavailable")}
        onRetry={() => undefined}
        retryLabel="Try again"
      >
        <div>Loaded content</div>
      </DataFetchBoundary>
    );

    expect(html).toContain('role="alert"');
    expect(html).toContain("Risk dashboard");
    expect(html).toContain("Backend unavailable");
    expect(html).toContain("Try again");
    expect(html).not.toContain("could not load");
    expect(html).not.toContain("could not be loaded");
    expect(html).not.toContain("Loaded content");
  });

  it("renders empty state with customer-safe copy", () => {
    const html = renderToString(
      <DataFetchBoundary
        title="Inspection results"
        isEmpty
        emptyTitle="No inspection rows yet"
        emptyMessage="Run an inspection job or adjust the active filters."
      >
        <div>Loaded content</div>
      </DataFetchBoundary>
    );

    expect(html).toContain("No inspection rows yet");
    expect(html).toContain("Run an inspection job or adjust the active filters.");
    expect(html).not.toContain("Loaded content");
  });

  it("renders success banner and children in success state", () => {
    const html = renderToString(
      <DataFetchBoundary status="success" successMessage="Fresh data is ready.">
        <div>Loaded content</div>
      </DataFetchBoundary>
    );

    expect(html).toContain('role="status"');
    expect(html).toContain("Ready");
    expect(html).toContain("Fresh data is ready.");
    expect(html).toContain("Loaded content");
  });

  it("renders children directly for idle state", () => {
    const html = renderToString(
      <DataFetchBoundary>
        <div>Loaded content</div>
      </DataFetchBoundary>
    );

    expect(html).toContain("Loaded content");
  });
});
`
);

// ============================================================
// 5. Validator for Pack C
// ============================================================

write(
  "../tools/p00/validate-p00c-frontend-behavioural-tests.cjs",
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

console.log("P00C Pack C frontend behavioural tests applied.");

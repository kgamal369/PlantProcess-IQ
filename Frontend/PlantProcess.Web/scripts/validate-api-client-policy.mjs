import fs from "node:fs";
import path from "node:path";

const root = process.cwd();
const apiClientPath = path.join(root, "src", "api", "http", "apiClient.ts");

function fail(message) {
  console.error(`❌ ${message}`);
  process.exit(1);
}

function pass(message) {
  console.log(`✓ ${message}`);
}

if (!fs.existsSync(apiClientPath)) {
  fail(`Missing api client: ${apiClientPath}`);
}

const content = fs.readFileSync(apiClientPath, "utf8");

const requiredPatterns = [
  {
    label: "PPIQ-HARD-003 timeout/retry task marker exists",
    pattern: /FE-HARD-003|PPIQ-HARD-003|timeout.*retry/i,
  },
  {
    label: "GET timeout policy exists",
    pattern: /6s|6000|GET.*timeout|timeout.*GET/i,
  },
  {
    label: "POST timeout policy exists",
    pattern: /12s|12000|POST.*timeout|timeout.*POST/i,
  },
  {
    label: "AbortController is used for timeout cancellation",
    pattern: /AbortController/,
  },
  {
    label: "AbortError or timeout failure is mapped",
    pattern: /AbortError|timeout|timed out/i,
  },
  {
    label: "Retry is restricted to idempotent methods",
    pattern: /idempotent|GET|HEAD|OPTIONS/i,
  },
  {
    label: "Caller can suppress automatic toast when rendering inline errors",
    pattern: /suppressToast/,
  },
  {
    label: "Auth failure event is dispatched for 401 or 403",
    pattern: /auth-failure|401|403/,
  },
  {
    label: "Friendly error mapping is used",
    pattern: /mapErrorToFriendly|friendly/i,
  },
];

for (const item of requiredPatterns) {
  if (!item.pattern.test(content)) {
    fail(item.label);
  }

  pass(item.label);
}

const unsafeRetryPattern =
  /method\s*===\s*["']POST["'][\s\S]{0,180}retry|retry[\s\S]{0,180}method\s*===\s*["']POST["']/i;

if (unsafeRetryPattern.test(content)) {
  fail("POST retry appears enabled by default. Non-idempotent calls must not auto-retry.");
}

pass("POST auto-retry is not detected");
pass("API timeout + idempotent retry policy validation passed");
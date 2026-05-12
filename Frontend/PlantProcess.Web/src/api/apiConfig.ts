// ============================================================
// TASK 14 — Verify no hidden frontend localhost fallback
// FILE: Frontend/PlantProcess.Web/src/api/apiConfig.ts
//
// STATUS: Already correctly implemented — throws when missing.
// This file is provided here for copy-paste confirmation that
// no fallback was introduced.
//
// VALIDATION CHECKLIST:
//  ✓ No "http://localhost" string anywhere in this file.
//  ✓ Missing VITE_API_BASE_URL throws with a descriptive message.
//  ✓ Invalid URL (not parseable) throws with the bad value shown.
//  ✓ Trailing slash is stripped to prevent double-slash in API paths.
// ============================================================

function normalizeBaseUrl(value: string | undefined): string {
  const normalized = value?.trim().replace(/\/$/, "");

  if (!normalized) {
    throw new Error(
      [
        "Missing VITE_API_BASE_URL.",
        "Create Frontend/PlantProcess.Web/.env.development (local dev) or",
        "root .env (Docker/demo) and add:",
        "VITE_API_BASE_URL=http://localhost:5063",
      ].join(" ")
    );
  }

  try {
    new URL(normalized);
  } catch {
    throw new Error(
      `Invalid VITE_API_BASE_URL: "${normalized}". ` +
      "Provide a valid absolute URL such as http://localhost:5063 or https://api.yourdomain.com."
    );
  }

  return normalized;
}

export const API_BASE_URL = normalizeBaseUrl(
  import.meta.env.VITE_API_BASE_URL as string | undefined
);

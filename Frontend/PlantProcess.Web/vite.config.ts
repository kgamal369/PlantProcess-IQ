// ============================================================
// TASK 11 — Verify frontend port configuration
// FILE: Frontend/PlantProcess.Web/vite.config.ts
//
// STATUS: Already correctly implemented — port reads from VITE_PORT.
// Provided here for completeness and copy-paste confirmation.
// strictPort is false so Vite tries the next available port if
// the configured one is busy (safe for local dev).
// ============================================================

import { defineConfig, loadEnv } from "vite";
import react from "@vitejs/plugin-react";

function readPort(value: string | undefined, fallback: number): number {
  if (!value) return fallback;

  const parsed = Number(value);

  if (!Number.isInteger(parsed) || parsed <= 0 || parsed > 65535) {
    throw new Error(
      `Invalid Vite port value: "${value}". ` +
      "Provide an integer between 1 and 65535."
    );
  }

  return parsed;
}

export default defineConfig(({ mode }) => {
  // loadEnv reads .env, .env.{mode}, .env.local, .env.{mode}.local
  // The third argument "" makes ALL env vars available (not only VITE_* ones)
  // so VITE_HOST, VITE_PORT etc. work even without the VITE_ prefix check.
  const env = loadEnv(mode, process.cwd(), "");

  const devPort     = readPort(env.VITE_PORT,         5173);
  const previewPort = readPort(env.VITE_PREVIEW_PORT, 4173);

  return {
    plugins: [react()],
    server: {
      host:       env.VITE_HOST || "0.0.0.0",
      port:       devPort,
      strictPort: false,
    },
    preview: {
      host:       env.VITE_HOST || "0.0.0.0",
      port:       previewPort,
      strictPort: false,
    },
  };
});

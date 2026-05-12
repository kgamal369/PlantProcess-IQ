import { defineConfig, loadEnv } from "vite";
import react from "@vitejs/plugin-react";

function readPort(value: string | undefined, fallback: number): number {
  if (!value) return fallback;

  const parsed = Number(value);

  if (!Number.isInteger(parsed) || parsed <= 0 || parsed > 65535) {
    throw new Error(`Invalid Vite port value: ${value}`);
  }

  return parsed;
}

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), "");

  const devPort = readPort(env.VITE_PORT, 5173);
  const previewPort = readPort(env.VITE_PREVIEW_PORT, 4173);

  return {
    plugins: [react()],
    server: {
      host: env.VITE_HOST || "0.0.0.0",
      port: devPort,
      strictPort: false,
    },
    preview: {
      host: env.VITE_HOST || "0.0.0.0",
      port: previewPort,
      strictPort: false,
    },
  };
});
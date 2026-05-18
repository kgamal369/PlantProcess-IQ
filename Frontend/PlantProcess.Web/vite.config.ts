import { defineConfig, loadEnv } from "vite";
import react from "@vitejs/plugin-react";
import path from "node:path";

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), "");

  const host = env.VITE_HOST || "127.0.0.1";
  const port = Number(env.VITE_PORT || 5173);
  const previewPort = Number(env.VITE_PREVIEW_PORT || 4173);

  return {
    plugins: [react()],
    resolve: {
      alias: {
        "@": path.resolve(__dirname, "src")
      }
    },
    server: {
      host,
      port
    },
    preview: {
      host,
      port: previewPort
    }
  };
});
